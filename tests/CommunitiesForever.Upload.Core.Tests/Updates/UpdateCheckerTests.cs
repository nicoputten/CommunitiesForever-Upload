using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Updates;
using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.Core.Tests.Updates;

/// <summary>Updates (UP-10, boards <c>updates-flow</c> and <c>update-states</c>).</summary>
public sealed class UpdateCheckerTests : IDisposable
{
    private static readonly UpdateOffer Newer = new("1.1.0", 6_600_000);

    private readonly UpdateHarness _h = new();

    private UpdateChecker Checker => _h.Checker;

    [Fact]
    public async Task ChecksAtStartAndThenDaily()
    {
        Checker.Start();
        await UpdateHarness.Until(() => Checker.State == UpdateState.UpToDate);
        Assert.Equal(_h.Time.GetUtcNow(), Checker.LastCheck);
        Assert.Equal(_h.Time.GetUtcNow(), _h.Memory.Saved.LastCheck);

        _h.Time.Advance(TimeSpan.FromHours(23));
        Assert.Equal(1, _h.Updates.Checks);
        _h.Time.Advance(TimeSpan.FromHours(1));
        await UpdateHarness.Until(() => _h.Updates.Checks == 2);
    }

    [Fact]
    public async Task DevBuildsNeverCheck()
    {
        var dev = new UpdateHarness(installed: false);
        using var checker = dev.Checker;

        checker.Start();
        await checker.CheckNowAsync();

        Assert.Equal(UpdateState.DevBuild, checker.State);
        Assert.Equal(0, dev.Updates.Checks);
    }

    [Fact]
    public async Task NotConnectedSkipsTheFeed()
    {
        _h.Tokens.Token = null;

        await Checker.CheckNowAsync();

        Assert.Equal(0, _h.Updates.Checks);
        Assert.Equal(UpdateState.Idle, Checker.State);
    }

    [Fact]
    public async Task ANewerVersionDownloadsQuietlyAndWaits()
    {
        _h.Updates.Newer = Newer;
        _h.Updates.DownloadGate = new TaskCompletionSource();

        var check = Checker.CheckNowAsync();
        await UpdateHarness.Until(() => Checker.State == UpdateState.Downloading);
        Assert.Equal("1.1.0", Checker.NewVersion);
        Assert.Equal(4_092_000, Checker.DownloadedBytes);
        Assert.Equal(6_600_000, Checker.DownloadSize);
        _h.Updates.DownloadGate.SetResult();
        await check;

        Assert.Equal(UpdateState.Ready, Checker.State);
        Assert.Empty(_h.Updates.Applied);
    }

    [Fact]
    public async Task QuitInstallsWithoutRestart()
    {
        _h.Updates.Newer = Newer;
        await Checker.CheckNowAsync();

        Checker.ApplyIfReady();

        Assert.Equal([false], _h.Updates.Applied);
        Assert.Equal("1.1.0", _h.Memory.Saved.HandedOver);
    }

    [Fact]
    public async Task InstallsQuietlyOnceWowIsClosedAndNothingWaits()
    {
        _h.Updates.Newer = Newer;
        var restarts = new List<bool>();
        Checker.RestartRequested += (_, quiet) => restarts.Add(quiet);
        await Checker.CheckNowAsync();
        _h.Games.Running = true;

        _h.Time.Advance(UpdateChecker.SafeMomentPoll);
        Assert.Empty(restarts);

        _h.Games.Running = false;
        ((UpdateLoop)_h.Loop).Set(AccountState.Settling);
        _h.Time.Advance(UpdateChecker.SafeMomentPoll);
        Assert.Empty(restarts);

        ((UpdateLoop)_h.Loop).Set(AccountState.Watching);
        _h.Time.Advance(UpdateChecker.SafeMomentPoll);
        Assert.Equal([true], restarts);
        Assert.Equal([true], _h.Updates.Applied);
    }

    [Fact]
    public async Task TheSettingOffWaitsForQuit()
    {
        new SettingsStore(_h.SettingsFile).Save(new AppSettings { InstallUpdatesWhenWowClosed = false });
        _h.Updates.Newer = Newer;
        bool restarted = false;
        Checker.RestartRequested += (_, _) => restarted = true;
        await Checker.CheckNowAsync();

        _h.Time.Advance(UpdateChecker.SafeMomentPoll * 3);

        Assert.False(restarted);
        Assert.Equal(UpdateState.Ready, Checker.State);
    }

    [Fact]
    public async Task RestartNowDuringAnUploadWaitsForIt()
    {
        var loop = (UpdateLoop)_h.Loop;
        _h.Updates.Newer = Newer;
        var restarts = new List<bool>();
        Checker.RestartRequested += (_, quiet) => restarts.Add(quiet);
        Checker.Start();
        await UpdateHarness.Until(() => Checker.State == UpdateState.Ready);
        loop.Set(AccountState.Uploading);

        Checker.RestartNow();
        Assert.Equal(UpdateState.WaitingForUpload, Checker.State);
        Checker.CancelRestart();
        Assert.Equal(UpdateState.Ready, Checker.State);

        Checker.RestartNow();
        loop.Set(AccountState.Watching);

        Assert.Equal([false], restarts);
    }

    [Fact]
    public async Task UnreachableFeedTriesAgainInAnHour()
    {
        _h.Updates.CheckError = new HttpRequestException("offline");
        await Checker.CheckNowAsync();
        Assert.Equal(UpdateState.CouldntCheck, Checker.State);

        _h.Updates.CheckError = null;
        _h.Time.Advance(UpdateChecker.RetryAfter);

        await UpdateHarness.Until(() => Checker.State == UpdateState.UpToDate);
    }

    [Fact]
    public async Task ABadSignatureOrDownloadIsThrownAwaySilently()
    {
        _h.Updates.Newer = Newer;
        _h.Updates.DownloadError = new InvalidDataException("checksum");

        await Checker.CheckNowAsync();

        Assert.Equal(UpdateState.Idle, Checker.State);
        Assert.Null(Checker.NewVersion);
    }

    [Fact]
    public void AFailedInstallKeepsTheOldVersionAndSaysSo()
    {
        _h.Memory.Saved = new UpdateMemory(HandedOver: "1.1.0");

        Checker.Start();

        Assert.Equal(UpdateState.Failed, Checker.State);
        Assert.Equal("1.1.0", Checker.NewVersion);
        Assert.Null(_h.Memory.Saved.HandedOver);
    }

    [Fact]
    public async Task AfterAnUpdateSaysSoOnceUntilClosed()
    {
        _h.Updates.CurrentVersion = "1.1.0";
        _h.Memory.Saved = new UpdateMemory(HandedOver: "1.1.0");

        Checker.Start();
        Assert.Equal("1.1.0", Checker.UpdatedTo);
        await UpdateHarness.Until(() => Checker.State == UpdateState.UpToDate);

        Checker.DismissUpdated();

        Assert.Null(Checker.UpdatedTo);
        Assert.Null(_h.Memory.Saved.ShowUpdatedTo);
    }

    [Fact]
    public async Task ARequiredUpdateHoldsUploadsUntilItIsIn()
    {
        _h.Site.Status = SiteResult.Ok(new UpdateStatus("1.1.0", null, "1.1.0", new Uri("https://wow.vestras.nl/uploader/changes#1.1.0")));
        _h.Updates.Newer = Newer;

        await Checker.CheckNowAsync();

        Assert.True(Checker.IsRequired);
        Assert.Equal(UpdateChecker.HoldUntil, _h.Loop.PausedUntil);
        Assert.Equal(new Uri("https://wow.vestras.nl/uploader/changes#1.1.0"), Checker.NotesUrl);
        Assert.Equal(UpdateState.Ready, Checker.State);
    }

    [Fact]
    public async Task ReleaseDateShowsForTheNewestVersion()
    {
        var released = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        _h.Site.Status = SiteResult.Ok(new UpdateStatus("1.0.3", released, null, null));

        await Checker.CheckNowAsync();

        Assert.Equal(released, Checker.ReleasedAt);
        Assert.False(Checker.IsRequired);
        Assert.Null(_h.Loop.PausedUntil);
    }

    [Theory]
    [InlineData("1.0.3", "1.1.0", -1)]
    [InlineData("1.10.0", "1.9.9", 1)]
    [InlineData("1.1.0-dev", "1.1.0", 0)]
    [InlineData("nonsense", "0.0.1", -1)]
    public void ComparesVersions(string a, string b, int expected) => Assert.Equal(expected, Math.Sign(UpdateChecker.Compare(a, b)));

    [Fact]
    public void MemoryRoundTripsAndForgivesBadFiles()
    {
        var file = new TextFile();
        var store = new UpdateMemoryStore(file);
        var memory = new UpdateMemory("1.1.0", "1.0.9", new DateTimeOffset(2026, 10, 27, 9, 12, 0, TimeSpan.Zero));

        store.Save(memory);
        Assert.Equal(memory, store.Load());
        Assert.Contains("\"version\": 1", file.Text, StringComparison.Ordinal);

        file.Text = "not json";
        Assert.Equal(new UpdateMemory(), store.Load());
    }

    public void Dispose() => Checker.Dispose();

    private sealed class TextFile : IUpdatesFile
    {
        public string? Text { get; set; }

        public string? Read() => Text;

        public void Write(string text) => Text = text;
    }
}
