using System.Text.Json;
using CommunitiesForever.Upload.Core.Runs;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Tests.Updates;
using CommunitiesForever.Upload.Core.Uploading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CommunitiesForever.Upload.Core.Tests.Runs;

/// <summary>UP-12: when the runs are fetched and when Runs.lua is written (board <c>runs-flow</c>).</summary>
public sealed class RunsSyncTests : IDisposable
{
    private const string Install = @"C:\WoW\_forever_";
    private static readonly AppSettings Settings = new() { Accounts = [new WatchedAccount(Install, "123456789#1"), new WatchedAccount(Install, "987654321#1")] };

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 19, 0, 0, TimeSpan.Zero));
    private readonly RunsSite _site = new();
    private readonly UpdateTokens _tokens = new();
    private readonly UpdateLoop _loop = new();
    private readonly UpdateGames _games = new();
    private readonly FakeRunsFiles _files = new();
    private readonly RunsSync _sync;

    public RunsSyncTests()
    {
        _sync = new RunsSync(_site, _tokens, _loop, _games, _files, _time, NullLogger<RunsSync>.Instance);
    }

    public void Dispose() => _sync.Dispose();

    private async Task Tick(TimeSpan by)
    {
        _time.Advance(by);
        await UpdateHarness.Until(() => !_sync.IsFetching);
    }

    [Fact]
    public async Task Fetches_when_WoW_starts_then_every_5_minutes_and_not_while_closed()
    {
        _sync.Start(Settings);
        await Tick(TimeSpan.FromMinutes(10));
        Assert.Equal(0, _site.Calls);

        _games.Running = true;
        await Tick(UploadLoop.GameCheckInterval);
        await UpdateHarness.Until(() => _site.Calls == 1);
        Assert.Equal(1, _files.Writes); // two accounts, one install: one file
        Assert.Equal(3, _sync.Status.RunCount);
        Assert.True(_sync.Status.GameRunning);
        Assert.Equal(RunsHint.Reload, _sync.Status.Hint);
        Assert.NotNull(_sync.Status.NextAt);

        await Tick(TimeSpan.FromMinutes(4));
        Assert.Equal(1, _site.Calls);
        await Tick(TimeSpan.FromMinutes(1));
        await UpdateHarness.Until(() => _site.Calls == 2);
        Assert.Equal(1, _files.Writes); // nothing changed: not written again

        _games.Running = false;
        await Tick(TimeSpan.FromMinutes(30));
        Assert.Equal(2, _site.Calls);
        Assert.Null(_sync.Status.NextAt);
        Assert.Equal(RunsHint.None, _sync.Status.Hint);
    }

    [Fact]
    public async Task Fetches_right_after_an_upload_also_with_WoW_closed()
    {
        _sync.Start(Settings);
        await Tick(TimeSpan.FromSeconds(10));
        _loop.Statuses = [new AccountStatus(Settings.Accounts[0], AccountState.Watching, LastUpload: Upload(_time.GetUtcNow()))];
        _loop.Raise();
        await UpdateHarness.Until(() => _site.Calls == 1);
        Assert.Equal(1, _files.Writes);
        Assert.Equal(RunsHint.None, _sync.Status.Hint); // WoW closed: read at the next login
    }

    [Fact]
    public async Task Writes_again_when_the_runs_change_or_the_addon_update_reset_the_file()
    {
        _games.Running = true;
        _sync.Start(Settings);
        await UpdateHarness.Until(() => _site.Calls == 1);
        _site.Feed = """{ "v": 1, "updatedAt": 2, "runs": [] }""";
        await Tick(RunsSync.Interval);
        await UpdateHarness.Until(() => _files.Writes == 2);
        Assert.Equal(0, _sync.Status.RunCount);

        _files.Content = "CF_RUNS = nil\n"u8.ToArray(); // the addon update shipped its empty file
        await Tick(RunsSync.Interval);
        await UpdateHarness.Until(() => _files.Writes == 3);
    }

    [Theory]
    [InlineData(AddonFolder.Missing, RunsHint.InstallAddon, 0)]
    [InlineData(AddonFolder.TooOld, RunsHint.UpdateAddon, 0)]
    [InlineData(AddonFolder.NoFile, RunsHint.RestartWow, 1)]
    public async Task Says_what_the_addon_folder_needs(AddonFolder folder, RunsHint hint, int writes)
    {
        _files.Folder = folder;
        _games.Running = true;
        _sync.Start(Settings);
        await UpdateHarness.Until(() => _site.Calls == 1);
        await Tick(TimeSpan.FromSeconds(10));
        Assert.Equal(writes, _files.Writes);
        Assert.Equal(hint, _sync.Status.Hint);
    }

    [Fact]
    public async Task A_failed_fetch_waits_for_the_next_tick_and_keeps_the_last_runs()
    {
        _games.Running = true;
        _sync.Start(Settings);
        await UpdateHarness.Until(() => _site.Calls == 1);
        _site.Fail = true;
        await Tick(RunsSync.Interval);
        await UpdateHarness.Until(() => _sync.Status.Failed);
        Assert.Equal(3, _sync.Status.RunCount);
        Assert.Equal(1, _files.Writes);

        _site.Fail = false;
        await Tick(RunsSync.Interval);
        await UpdateHarness.Until(() => !_sync.Status.Failed);
    }

    [Fact]
    public async Task Does_nothing_without_a_token()
    {
        _tokens.Token = null;
        _games.Running = true;
        _sync.Start(Settings);
        await Tick(RunsSync.Interval);
        Assert.Equal(0, _site.Calls);
        Assert.Null(_sync.Status.RunCount);
    }

    [Fact]
    public async Task Is_collected_after_it_is_stopped()
    {
        var weak = await StartAndStop(_loop, _games);
        for (int i = 0; i < 5 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(weak.IsAlive);
        Assert.Equal(0, _loop.Subscribers);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> StartAndStop(UpdateLoop loop, UpdateGames games)
    {
        games.Running = true;
        var time = new FakeTimeProvider();
        var site = new RunsSite();
        var sync = new RunsSync(site, new UpdateTokens(), loop, games, new FakeRunsFiles(), time, NullLogger<RunsSync>.Instance);
        sync.Start(Settings);
        await UpdateHarness.Until(() => site.Calls == 1 && !sync.IsFetching);
        sync.Dispose();
        return new WeakReference(sync);
    }

    private static HistoryEntry Upload(DateTimeOffset at) => new(1, at, UploadStatus.Ok, null, 1, "1 run answer", [], null);
}

internal sealed class RunsSite : ISiteClient
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public string Feed { get; set; } = """{ "v": 1, "updatedAt": 1, "runs": [ { "id": "r-1" }, { "id": "r-2" }, { "id": "r-3" } ] }""";

    public bool Fail { get; set; }

    public Task<SiteResult<RunsFeed>> GetRunsAsync(AccessToken token, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult<SiteResult<RunsFeed>>(Fail
            ? new SiteFailure(SiteError.Offline)
            : SiteResult.Ok(new RunsFeed(JsonDocument.Parse(Feed).RootElement.Clone())));
    }

    public Task<SiteResult<DeviceAuthorization>> RequestDeviceCodeAsync(string deviceName, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<TokenPoll>> PollTokenAsync(DeviceCode deviceCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<SiteIdentity>> GetIdentityAsync(AccessToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<UploadReceipt>> UploadAsync(AccessToken token, Stream content, DateTimeOffset? savedAt, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<bool>> RevokeAsync(AccessToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<UpdateStatus>> GetUpdateStatusAsync(AccessToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class FakeRunsFiles : IRunsFiles
{
    private int _writes;

    public AddonFolder Folder { get; set; } = AddonFolder.Ready;

    public byte[]? Content { get; set; } = "CF_RUNS = nil\n"u8.ToArray();

    public int Writes => Volatile.Read(ref _writes);

    public string PathFor(string foreverPath) => foreverPath + @"\Interface\AddOns\CommunitiesForever\Data\Runs.lua";

    public AddonFolder Inspect(string foreverPath) => Folder;

    public byte[]? Read(string foreverPath) => Content;

    public void Write(string foreverPath, byte[] content)
    {
        Content = content;
        if (Folder == AddonFolder.NoFile)
        {
            Folder = AddonFolder.Ready;
        }

        Interlocked.Increment(ref _writes);
    }
}
