using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Updates;
using CommunitiesForever.Upload.Core.Uploading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CommunitiesForever.Upload.Core.Tests.Updates;

/// <summary>
/// An update checker over fakes, at 27 Oct 2026 09:12 UTC, installed as 1.0.3. Self-contained, so the app's tests and
/// design checks can link it.
/// </summary>
internal sealed class UpdateHarness
{
    public static readonly WatchedAccount Account = new(@"C:\WoW\_forever_", "123456789#1");

    public UpdateHarness(IUploadLoop? loop = null, bool installed = true, FakeTimeProvider? time = null)
    {
        Time = time ?? new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 9, 12, 0, TimeSpan.Zero));
        Updates.IsInstalled = installed;
        Loop = loop ?? new UpdateLoop();
        Settings = new SettingsStore(SettingsFile);
        Checker = new UpdateChecker(Updates, Site, Tokens, Loop, Games, Settings, Memory, Time, NullLogger<UpdateChecker>.Instance);
    }

    public FakeTimeProvider Time { get; }

    public FakeUpdates Updates { get; } = new();

    public StatusSite Site { get; } = new();

    public UpdateTokens Tokens { get; } = new();

    public IUploadLoop Loop { get; }

    public UpdateGames Games { get; } = new();

    public UpdateSettingsFile SettingsFile { get; } = new();

    public SettingsStore Settings { get; }

    public MemoryUpdates Memory { get; } = new();

    public UpdateChecker Checker { get; }

    public static async Task Until(Func<bool> condition)
    {
        for (int i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }
}

internal sealed class FakeUpdates : IAppUpdates
{
    private int _checks;

    public bool IsInstalled { get; set; } = true;

    public string CurrentVersion { get; set; } = "1.0.3";

    public UpdateOffer? Newer { get; set; }

    public Exception? CheckError { get; set; }

    public Exception? DownloadError { get; set; }

    /// <summary>When set, the check waits for it (to see "Checking").</summary>
    public TaskCompletionSource? CheckGate { get; set; }

    /// <summary>When set, the download waits for it (to see "Downloading").</summary>
    public TaskCompletionSource? DownloadGate { get; set; }

    public int Checks => Volatile.Read(ref _checks);

    public List<bool> Applied { get; } = [];

    public async Task<UpdateOffer?> CheckAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _checks);
        if (CheckGate is { } gate)
        {
            await gate.Task;
        }

        return CheckError is { } error ? throw error : Newer;
    }

    public async Task DownloadAsync(IProgress<long> progress, CancellationToken cancellationToken)
    {
        progress.Report((Newer?.Size ?? 0) * 62 / 100);
        if (DownloadGate is { } gate)
        {
            await gate.Task;
        }

        if (DownloadError is { } error)
        {
            throw error;
        }
    }

    public void ApplyAfterExit(bool restart) => Applied.Add(restart);
}

internal sealed class StatusSite : ISiteClient
{
    public SiteResult<UpdateStatus> Status { get; set; } = new SiteFailure(SiteError.Unexpected, "not_found", 404);

    public Task<SiteResult<UpdateStatus>> GetUpdateStatusAsync(AccessToken token, CancellationToken cancellationToken = default) => Task.FromResult(Status);

    public Task<SiteResult<RunsFeed>> GetRunsAsync(AccessToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<DeviceAuthorization>> RequestDeviceCodeAsync(string deviceName, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<TokenPoll>> PollTokenAsync(DeviceCode deviceCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<SiteIdentity>> GetIdentityAsync(AccessToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<UploadReceipt>> UploadAsync(AccessToken token, Stream content, DateTimeOffset? savedAt, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<bool>> RevokeAsync(AccessToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class UpdateTokens : ITokenStore
{
    public AccessToken? Token { get; set; } = new("cfu_test");

    public AccessToken? Load() => Token;

    public void Save(AccessToken token) => Token = token;

    public void Delete() => Token = null;
}

internal sealed class UpdateLoop : IUploadLoop
{
    public event EventHandler? StatusChanged;

    public IReadOnlyList<AccountStatus> Statuses { get; set; } = [new(UpdateHarness.Account, AccountState.Watching)];

    public DateTimeOffset? PausedUntil { get; private set; }

    public void Set(AccountState state)
    {
        Statuses = [new(UpdateHarness.Account, state)];
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Raise() => StatusChanged?.Invoke(this, EventArgs.Empty);

    public int Subscribers => StatusChanged?.GetInvocationList().Length ?? 0;

    public void UploadNow(WatchedAccount? account = null)
    {
    }

    public void Skip(WatchedAccount account)
    {
    }

    public void Pause(DateTimeOffset until) => PausedUntil = until;

    public void EndPause() => PausedUntil = null;

    public bool IsHeld { get; private set; }

    public void Hold(bool held) => IsHeld = held;
}

internal sealed class UpdateGames : IGameProcesses
{
    public bool Running { get; set; }

    public bool IsRunning(string foreverPath) => Running;
}

internal sealed class UpdateSettingsFile : ISettingsFile
{
    public string? Text { get; set; }

    public string? Read() => Text;

    public void Write(string text) => Text = text;

    public void MoveAsideAsDamaged() => Text = null;
}

internal sealed class MemoryUpdates : IUpdateMemoryFile
{
    public UpdateMemory Saved { get; set; } = new();

    public UpdateMemory Load() => Saved;

    public void Save(UpdateMemory memory) => Saved = memory;
}
