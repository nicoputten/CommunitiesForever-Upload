using CommunitiesForever.Upload.Core.Site;
using Microsoft.Extensions.Time.Testing;

namespace CommunitiesForever.Upload.Core.Tests.Site;

public sealed class ConnectFlowTests
{
    private static readonly AccessToken Token = new("cfu_3q2-7wEXAMPLEexampleEXAMPLEexampleEXAMPLExy");
    private static readonly SiteIdentity Nico = new("Nico#2201", "Doombringers", "DESKTOP-NICO", "1.0.0", DateTimeOffset.UnixEpoch, null);

    private readonly FakeSite _site = new();
    private readonly FakeTokenStore _store = new();
    private readonly FakeBrowser _browser = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 27, 20, 0, 0, TimeSpan.Zero));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ConnectsAfterTheUserAllows()
    {
        _site.Polls.Enqueue(Poll(PollStatus.Pending));
        _site.Polls.Enqueue(Poll(PollStatus.Pending));
        _site.Polls.Enqueue(SiteResult.Ok(new TokenPoll(PollStatus.Granted, Token)));
        var progress = new List<ConnectProgress>();

        var outcome = await RunAsync(new SyncProgress(progress));

        Assert.Equal(new ConnectOutcome.Connected(Nico), outcome);
        Assert.Equal(Token, _store.Token);
        Assert.Equal("DESKTOP-NICO", _site.DeviceName);
        Assert.Equal(new Uri("https://wow.vestras.nl/uploader?code=KXRT-7QMN"), Assert.Single(_browser.Opened));
        Assert.IsType<ConnectProgress.RequestingCode>(progress[0]);
        var waiting = Assert.IsType<ConnectProgress.WaitingForBrowser>(progress[1]);
        Assert.Equal("KXRT-7QMN", waiting.UserCode);
        Assert.Equal(Start.AddMinutes(10), waiting.ExpiresAt);
        Assert.Equal(new Uri("https://wow.vestras.nl/uploader?code=KXRT-7QMN"), waiting.Page);
        // Three polls, five seconds apart.
        Assert.Equal([5, 10, 15], _site.PollTimes.Select(t => (t - Start).TotalSeconds));
    }

    [Fact]
    public async Task SlowDownAddsFiveSeconds()
    {
        _site.Polls.Enqueue(Poll(PollStatus.SlowDown));
        _site.Polls.Enqueue(Poll(PollStatus.Pending));
        _site.Polls.Enqueue(SiteResult.Ok(new TokenPoll(PollStatus.Granted, Token)));

        await RunAsync();

        Assert.Equal([5, 15, 25], _site.PollTimes.Select(t => (t - Start).TotalSeconds));
    }

    [Theory]
    [InlineData(PollStatus.Denied)]
    [InlineData(PollStatus.Expired)]
    public async Task DeniedOrExpiredStoresNothing(PollStatus status)
    {
        _site.Polls.Enqueue(Poll(status));

        var outcome = await RunAsync();

        Assert.IsType(status == PollStatus.Denied ? typeof(ConnectOutcome.Denied) : typeof(ConnectOutcome.Expired), outcome);
        Assert.Null(_store.Token);
    }

    [Fact]
    public async Task StopsWhenTheCodeRunsOut()
    {
        // The site keeps saying pending; after expires_in (600 s) the app gives up without another poll.
        for (int i = 0; i < 200; i++)
        {
            _site.Polls.Enqueue(Poll(PollStatus.Pending));
        }

        var outcome = await RunAsync();

        Assert.IsType<ConnectOutcome.Expired>(outcome);
        Assert.Equal(119, _site.PollTimes.Count);
    }

    [Fact]
    public async Task KeepsPollingWhileOffline()
    {
        _site.Polls.Enqueue(new SiteFailure(SiteError.Offline));
        _site.Polls.Enqueue(new SiteFailure(SiteError.ServerError, Status: 502));
        _site.Polls.Enqueue(SiteResult.Ok(new TokenPoll(PollStatus.Granted, Token)));

        var outcome = await RunAsync();

        Assert.IsType<ConnectOutcome.Connected>(outcome);
    }

    [Fact]
    public async Task RateLimitWaitsAsLongAsAsked()
    {
        _site.Polls.Enqueue(new SiteFailure(SiteError.RateLimited, RetryAfter: TimeSpan.FromSeconds(30)));
        _site.Polls.Enqueue(SiteResult.Ok(new TokenPoll(PollStatus.Granted, Token)));

        await RunAsync();

        Assert.Equal([5, 35], _site.PollTimes.Select(t => (t - Start).TotalSeconds));
    }

    [Fact]
    public async Task OtherPollErrorsFail()
    {
        _site.Polls.Enqueue(new SiteFailure(SiteError.Unexpected, "invalid_client", 401));

        var outcome = await RunAsync();

        Assert.Equal("invalid_client", Assert.IsType<ConnectOutcome.Failed>(outcome).Failure.Code);
    }

    [Fact]
    public async Task NoDeviceCodeFails()
    {
        _site.DeviceCode = new SiteFailure(SiteError.Offline);

        var outcome = await RunAsync();

        Assert.Equal(SiteError.Offline, Assert.IsType<ConnectOutcome.Failed>(outcome).Failure.Kind);
        Assert.Empty(_browser.Opened);
    }

    [Fact]
    public async Task TokenIsStoredOnlyWhenMeAcceptsIt()
    {
        _site.Polls.Enqueue(SiteResult.Ok(new TokenPoll(PollStatus.Granted, Token)));
        _site.Identity = new SiteFailure(SiteError.Offline);

        var outcome = await RunAsync();

        Assert.IsType<ConnectOutcome.Failed>(outcome);
        Assert.Null(_store.Token);
    }

    [Theory]
    [InlineData("http://evil.example/uploader")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    public async Task OpensOnlyHttpsOrLocalPages(string page)
    {
        _site.DeviceCode = SiteResult.Ok(Authorization() with { VerificationUriComplete = new Uri(page) });

        var outcome = await RunAsync();

        Assert.IsType<ConnectOutcome.Failed>(outcome);
        Assert.Empty(_browser.Opened);
    }

    [Fact]
    public async Task LocalDevelopmentSiteMayUseHttp()
    {
        _site.DeviceCode = SiteResult.Ok(Authorization() with { VerificationUriComplete = new Uri("http://localhost:3000/uploader?code=KXRT-7QMN") });
        _site.Polls.Enqueue(SiteResult.Ok(new TokenPoll(PollStatus.Granted, Token)));

        Assert.IsType<ConnectOutcome.Connected>(await RunAsync());
    }

    [Fact]
    public async Task CancelStopsPolling()
    {
        for (int i = 0; i < 10; i++)
        {
            _site.Polls.Enqueue(Poll(PollStatus.Pending));
        }

        _site.Time = _time;
        _site.DeviceCode = SiteResult.Ok(Authorization());
        using var cancel = new CancellationTokenSource();
        var running = Flow().ConnectAsync(null, cancel.Token);
        _time.Advance(TimeSpan.FromSeconds(5));
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Null(_store.Token);
    }

    [Fact]
    public async Task PastedTokenIsCheckedThenStored()
    {
        var outcome = await Flow().PasteTokenAsync("  " + Token.Reveal() + " ", Ct);

        Assert.Equal(new ConnectOutcome.Connected(Nico), outcome);
        Assert.Equal(Token, _store.Token);
    }

    [Fact]
    public async Task MalformedPasteNeverReachesTheSite()
    {
        var outcome = await Flow().PasteTokenAsync("not a token", Ct);

        Assert.IsType<ConnectOutcome.MalformedToken>(outcome);
        Assert.Equal(0, _site.IdentityCalls);
    }

    [Fact]
    public async Task UnknownPastedTokenIsNotStored()
    {
        _site.Identity = new SiteFailure(SiteError.InvalidToken, "invalid_token", 401);

        var outcome = await Flow().PasteTokenAsync(Token.Reveal(), Ct);

        Assert.IsType<ConnectOutcome.UnknownToken>(outcome);
        Assert.Null(_store.Token);
    }

    [Fact]
    public async Task DisconnectRevokesAndDeletes()
    {
        _store.Token = Token;

        var outcome = await Flow().DisconnectAsync(Ct);

        Assert.True(outcome.RevokedOnSite);
        Assert.Equal(Token, _site.Revoked);
        Assert.Null(_store.Token);
    }

    [Fact]
    public async Task DisconnectOfflineStillDeletesHere()
    {
        _store.Token = Token;
        _site.Revoke = new SiteFailure(SiteError.Offline);

        var outcome = await Flow().DisconnectAsync(Ct);

        Assert.False(outcome.RevokedOnSite);
        Assert.Null(_store.Token);
    }

    private DateTimeOffset Start { get; set; }

    private static SiteResult<TokenPoll> Poll(PollStatus status) => SiteResult.Ok(new TokenPoll(status));

    private static DeviceAuthorization Authorization() => new(
        new DeviceCode("kq3V0dXyPZ8m1nR2c9WbTg"),
        "KXRT-7QMN",
        new Uri("https://wow.vestras.nl/uploader"),
        new Uri("https://wow.vestras.nl/uploader?code=KXRT-7QMN"),
        TimeSpan.FromSeconds(600),
        TimeSpan.FromSeconds(5));

    private ConnectFlow Flow() => new(_site, _store, _browser, new FakeDevice(), _time);

    // Runs the flow while moving the fake clock one second at a time until it finishes.
    private async Task<ConnectOutcome> RunAsync(IProgress<ConnectProgress>? progress = null)
    {
        _site.Time = _time;
        _site.DeviceCode ??= SiteResult.Ok(Authorization());
        Start = _time.GetUtcNow();
        var running = Flow().ConnectAsync(progress, Ct);
        for (int i = 0; i < 2000 && !running.IsCompleted; i++)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        return await running;
    }

    private sealed class SyncProgress(List<ConnectProgress> reports) : IProgress<ConnectProgress>
    {
        public void Report(ConnectProgress value) => reports.Add(value);
    }

    private sealed class FakeDevice : IDeviceInfo
    {
        public string DeviceName => "DESKTOP-NICO";
    }

    private sealed class FakeBrowser : IBrowser
    {
        public List<Uri> Opened { get; } = [];

        public void Open(Uri uri) => Opened.Add(uri);
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public AccessToken? Token { get; set; }

        public AccessToken? Load() => Token;

        public void Save(AccessToken token) => Token = token;

        public void Delete() => Token = null;
    }

    private sealed class FakeSite : ISiteClient
    {
        public TimeProvider? Time { get; set; }

        public SiteResult<DeviceAuthorization>? DeviceCode { get; set; }

        public Queue<SiteResult<TokenPoll>> Polls { get; } = new();

        public List<DateTimeOffset> PollTimes { get; } = [];

        public SiteResult<SiteIdentity> Identity { get; set; } = SiteResult.Ok(Nico);

        public SiteResult<bool> Revoke { get; set; } = SiteResult.Ok(true);

        public string? DeviceName { get; private set; }

        public int IdentityCalls { get; private set; }

        public AccessToken? Revoked { get; private set; }

        public Task<SiteResult<DeviceAuthorization>> RequestDeviceCodeAsync(string deviceName, CancellationToken cancellationToken = default)
        {
            DeviceName = deviceName;
            return Task.FromResult(DeviceCode!);
        }

        public Task<SiteResult<TokenPoll>> PollTokenAsync(DeviceCode deviceCode, CancellationToken cancellationToken = default)
        {
            PollTimes.Add(Time!.GetUtcNow());
            return Task.FromResult(Polls.Dequeue());
        }

        public Task<SiteResult<SiteIdentity>> GetIdentityAsync(AccessToken token, CancellationToken cancellationToken = default)
        {
            IdentityCalls++;
            return Task.FromResult(Identity);
        }

        public Task<SiteResult<UploadReceipt>> UploadAsync(AccessToken token, Stream content, DateTimeOffset? savedAt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SiteResult<bool>> RevokeAsync(AccessToken token, CancellationToken cancellationToken = default)
        {
            Revoked = token;
            return Task.FromResult(Revoke);
        }

        public Task<SiteResult<UpdateStatus>> GetUpdateStatusAsync(AccessToken token, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SiteResult<RunsFeed>> GetRunsAsync(AccessToken token, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
