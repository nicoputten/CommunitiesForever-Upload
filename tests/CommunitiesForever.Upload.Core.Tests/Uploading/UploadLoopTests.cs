using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.Core.Tests.Uploading;

public sealed class UploadLoopTests
{
    [Fact]
    public async Task FileSavedWhileClosedGoesUpAtStart()
    {
        await using var h = new LoopHarness();
        h.Save(h.First, "v1");

        await h.StartAsync();
        await h.AdvanceUntil(() => h.Site.Count == 1 && h.Status(h.First).State == AccountState.Watching);

        Assert.Equal("v1", h.Site.Uploaded[0]);
        var last = h.Status(h.First).LastUpload!;
        Assert.Equal("5 levels gained", last.SummaryLine);
        Assert.Equal(1, h.History.LedgerCount(h.First));
    }

    [Fact]
    public async Task WaitsUntilTheFileIsUnchangedForFiveSeconds()
    {
        await using var h = new LoopHarness();
        h.Save(h.First, "v1");
        await h.StartAsync();
        await h.AdvanceUntil(() => h.Site.Count == 1);

        h.Save(h.First, "v2");
        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.Settling);
        h.Time.Advance(TimeSpan.FromSeconds(3));
        h.Save(h.First, "v3");
        h.Time.Advance(TimeSpan.FromSeconds(3));
        await LoopHarness.Settle();
        Assert.Equal(1, h.Site.Count);

        await h.AdvanceUntil(() => h.Site.Count == 2);
        Assert.Equal("v3", h.Site.Uploaded[1]);
    }

    [Fact]
    public async Task SameFileIsNeverSentTwice()
    {
        await using var h = new LoopHarness();
        h.Save(h.First, "v1");
        await h.StartAsync();
        await h.AdvanceUntil(() => h.Site.Count == 1);

        await h.RestartAsync();
        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.Watching, max: TimeSpan.FromSeconds(30));
        h.Time.Advance(UploadLoop.PollInterval);
        await LoopHarness.Settle();

        Assert.Equal(1, h.Site.Count);
    }

    [Fact]
    public async Task OfflineRetriesAfterOneMinuteThenLonger()
    {
        await using var h = new LoopHarness();
        h.Site.Answer(new SiteFailure(SiteError.Offline));
        h.Site.Answer(new SiteFailure(SiteError.ServerError, Status: 502));
        h.Save(h.First, "v1");
        await h.StartAsync();

        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.WaitingToSend);
        var first = h.Status(h.First).NextTryAt!.Value;
        Assert.Equal(UploadLoop.FirstRetry, first - h.Time.GetUtcNow());

        await h.AdvanceUntil(() => h.Site.Count == 2 && h.Status(h.First).State == AccountState.WaitingToSend, TimeSpan.FromSeconds(10));
        Assert.Equal(TimeSpan.FromMinutes(2), h.Status(h.First).NextTryAt!.Value - h.Time.GetUtcNow());

        await h.AdvanceUntil(() => h.Site.Count == 3 && h.Status(h.First).State == AccountState.Watching, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task TryNowSendsWithoutWaiting()
    {
        await using var h = new LoopHarness();
        h.Site.Answer(new SiteFailure(SiteError.Offline));
        h.Save(h.First, "v1");
        await h.StartAsync();
        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.WaitingToSend);

        h.Loop.UploadNow(h.First);
        await LoopHarness.Settle(() => h.Site.Count == 2, TimeSpan.FromSeconds(2));

        Assert.Equal(2, h.Site.Count);
    }

    [Fact]
    public async Task UploadNowWithNothingWaitingDoesNotSkipTheLedgerLater()
    {
        await using var h = new LoopHarness();
        h.Save(h.First, "v1");
        await h.StartAsync();
        await h.AdvanceUntil(() => h.Site.Count == 1);

        h.Loop.UploadNow(h.First);
        await LoopHarness.Settle();
        h.Save(h.First, "v2");
        await h.AdvanceUntil(() => h.Site.Count == 2);
        h.Save(h.First, "v1");
        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.Watching && h.Status(h.First).SettlesAt is null);
        h.Time.Advance(UploadLoop.PollInterval);
        await LoopHarness.Settle();

        Assert.Equal(2, h.Site.Count);
    }

    [Fact]
    public async Task RevokedTokenStopsEveryAccountUntilReconnected()
    {
        await using var h = new LoopHarness("A", "B");
        h.Site.Answer(new SiteFailure(SiteError.InvalidToken, "invalid_token", 401));
        h.Save(h.Accounts[0], "a1");
        await h.StartAsync();

        await h.AdvanceUntil(() => h.Loop.Statuses.All(s => s.State == AccountState.NotConnected));
        Assert.Equal(AccountState.NotConnected, h.Loop.Overall);

        h.Loop.Reconnected();
        await h.AdvanceUntil(() => h.Site.Count == 2);
        Assert.Equal("a1", h.Site.Uploaded[1]);
    }

    [Fact]
    public async Task NoTokenAtAllIsNotConnected()
    {
        await using var h = new LoopHarness();
        h.Tokens.Token = null;
        h.Save(h.First, "v1");

        await h.StartAsync();

        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.NotConnected);
        Assert.Equal(0, h.Site.Count);
    }

    [Fact]
    public async Task NewerFormatIsNotUploadedAndRetriedOnTheNextRun()
    {
        await using var h = new LoopHarness();
        h.Site.Answer(FakeUploads.Receipt(UploadStatus.Rejected, UploadReceipt.Rejections.NewerFormat));
        h.Save(h.First, "v1");
        await h.StartAsync();

        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.NotUploaded);
        Assert.Equal(UploadReceipt.Rejections.NewerFormat, h.Status(h.First).Rejection);
        Assert.Equal(3, h.Status(h.First).SchemaVersion);

        // The app starts again later (the same run does not retry it).
        h.Time.Advance(TimeSpan.FromMinutes(1));
        await h.RestartAsync();
        await h.AdvanceUntil(() => h.Site.Count == 2);
    }

    [Theory]
    [InlineData(SiteError.TooLarge, "too_large")]
    [InlineData(SiteError.EmptyFile, "empty_file")]
    public async Task FilesTheSiteRefusesAreFinal(SiteError kind, string code)
    {
        await using var h = new LoopHarness();
        h.Site.Answer(new SiteFailure(kind, code));
        h.Save(h.First, "v1");
        await h.StartAsync();

        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.NotUploaded);
        await h.RestartAsync();
        h.Time.Advance(UploadLoop.PollInterval);
        await LoopHarness.Settle();

        Assert.Equal(1, h.Site.Count);
    }

    [Fact]
    public async Task MayNotUploadTriesAgainHourly()
    {
        await using var h = new LoopHarness();
        h.Site.Answer(new SiteFailure(SiteError.MayNotUpload, "may_not_upload", 403));
        h.Save(h.First, "v1");
        await h.StartAsync();

        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.NotAllowed);
        Assert.Equal(UploadLoop.NotAllowedRetry, h.Status(h.First).NextTryAt!.Value - h.Time.GetUtcNow());
        Assert.NotNull(h.Tokens.Token);

        await h.AdvanceUntil(() => h.Site.Count == 2, TimeSpan.FromMinutes(1), TimeSpan.FromHours(2));
    }

    [Fact]
    public async Task RateLimitWaitsAsAsked()
    {
        await using var h = new LoopHarness();
        h.Site.Answer(new SiteFailure(SiteError.RateLimited, "rate_limited", 429, TimeSpan.FromMinutes(7)));
        h.Save(h.First, "v1");
        await h.StartAsync();

        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.WaitingToSend);

        Assert.Equal(TimeSpan.FromMinutes(7), h.Status(h.First).NextTryAt!.Value - h.Time.GetUtcNow());
    }

    [Fact]
    public async Task PausedNotesSavesAndSendsTheNewestAfter()
    {
        await using var h = new LoopHarness();
        await h.StartAsync();
        h.Loop.Pause(h.Time.GetUtcNow().AddHours(1));
        h.Save(h.First, "v1");
        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.Paused);
        h.Save(h.First, "v2");
        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.Paused);
        Assert.Equal(0, h.Site.Count);

        h.Loop.EndPause();
        await h.AdvanceUntil(() => h.Site.Count == 1);
        Assert.Equal("v2", h.Site.Uploaded[0]);
    }

    [Fact]
    public async Task PauseEndsByItself()
    {
        await using var h = new LoopHarness();
        await h.StartAsync();
        h.Loop.Pause(h.Time.GetUtcNow().AddMinutes(30));
        h.Save(h.First, "v1");

        await h.AdvanceUntil(() => h.Site.Count == 1, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task WithoutAutomaticUploadsTheFileWaitsForUploadNow()
    {
        await using var h = new LoopHarness();
        h.Settings = h.Settings with { UploadAutomatically = false };
        h.Save(h.First, "v1");
        await h.StartAsync();

        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.ReadyToUpload);
        Assert.Equal(0, h.Site.Count);

        h.Loop.UploadNow(h.First);
        await h.AdvanceUntil(() => h.Site.Count == 1);
    }

    [Fact]
    public async Task SkippedFileIsNeverSent()
    {
        await using var h = new LoopHarness();
        h.Settings = h.Settings with { UploadAutomatically = false };
        h.Save(h.First, "v1");
        await h.StartAsync();
        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.ReadyToUpload);

        h.Loop.Skip(h.First);
        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.Watching);
        await h.RestartAsync();
        h.Time.Advance(UploadLoop.PollInterval);
        await LoopHarness.Settle();

        Assert.Equal(0, h.Site.Count);
    }

    [Fact]
    public async Task GoneFileIsNotWatchingUntilItComesBack()
    {
        await using var h = new LoopHarness();
        await h.StartAsync();
        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.NotWatching);
        Assert.Equal(0, h.Watchers.Active);

        h.Save(h.First, "v1");
        await h.AdvanceUntil(() => h.Site.Count == 1, TimeSpan.FromSeconds(5));
        Assert.Equal(1, h.Watchers.Active);
    }

    [Fact]
    public async Task PlayingWhileTheGameRuns()
    {
        await using var h = new LoopHarness();
        h.Save(h.First, "v1");
        h.Games.Running = true;
        await h.StartAsync();

        await h.AdvanceUntil(() => h.Site.Count == 1 && h.Status(h.First).State == AccountState.Playing);

        h.Games.Running = false;
        await h.AdvanceUntil(() => h.Status(h.First).State == AccountState.Watching);
    }

    [Fact]
    public async Task ReadIsRetriedWhileTheGameHoldsTheFile()
    {
        await using var h = new LoopHarness();
        h.Files.HeldReads = 2;
        h.Save(h.First, "v1");
        await h.StartAsync();

        await h.AdvanceUntil(() => h.Site.Count == 1);
    }

    [Fact]
    public async Task AccountsAreIndependent()
    {
        await using var h = new LoopHarness("A", "B");
        h.Site.Answer(new SiteFailure(SiteError.TooLarge, "too_large"));
        h.Save(h.Accounts[0], "a1");
        await h.StartAsync();
        await h.AdvanceUntil(() => h.Status(h.Accounts[0]).State == AccountState.NotUploaded);

        h.Save(h.Accounts[1], "b1");
        await h.AdvanceUntil(() => h.Site.Count == 2 && h.Status(h.Accounts[1]).State == AccountState.Watching);

        Assert.Equal(AccountState.NotUploaded, h.Status(h.Accounts[0]).State);
        Assert.Equal(AccountState.NotUploaded, h.Loop.Overall);
    }

    [Fact]
    public async Task StoppingReleasesWatchersAndWorkers()
    {
        var h = new LoopHarness("A", "B", "C");
        foreach (var account in h.Accounts)
        {
            h.Save(account, account.Account);
        }

        await h.StartAsync();
        await h.AdvanceUntil(() => h.Site.Count == 3);
        Assert.Equal(3, h.Watchers.Active);

        await h.Loop.StopAsync();

        Assert.Equal(0, h.Watchers.Active);
        Assert.Empty(h.Loop.Statuses);
        await h.DisposeAsync();
    }

    [Fact]
    public async Task LogsNeverHoldTheTokenOrOtherPeoplesNames()
    {
        await using var h = new LoopHarness();
        h.Site.Answer(new SiteFailure(SiteError.Offline));
        h.Save(h.First, "v1");
        await h.StartAsync();
        await h.AdvanceUntil(() => h.Site.Count == 2, TimeSpan.FromSeconds(10));

        Assert.NotEmpty(h.Logger.Lines);
        Assert.All(h.Logger.Lines, line =>
        {
            Assert.DoesNotContain(LoopHarness.Token.Reveal(), line, StringComparison.Ordinal);
            Assert.DoesNotContain("Ves Doombringer", line, StringComparison.Ordinal);
        });
    }
}
