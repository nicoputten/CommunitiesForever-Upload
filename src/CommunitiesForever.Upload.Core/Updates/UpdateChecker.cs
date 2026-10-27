using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Uploading;
using Microsoft.Extensions.Logging;

namespace CommunitiesForever.Upload.Core.Updates;

/// <summary>Board <c>update-states</c>: what Settings › About &amp; updates shows.</summary>
public enum UpdateState
{
    /// <summary>Not an installed release: never checks.</summary>
    DevBuild,

    /// <summary>Installed, not checked yet (or not connected).</summary>
    Idle,
    UpToDate,
    Checking,
    Downloading,

    /// <summary>Downloaded; installs at a safe moment or on "Restart now".</summary>
    Ready,

    /// <summary>"Restart now" during an upload: restarts right after it.</summary>
    WaitingForUpload,

    /// <summary>The feed couldn't be reached; tries again in an hour.</summary>
    CouldntCheck,

    /// <summary>The last install didn't take; the old version runs on.</summary>
    Failed,
}

/// <summary>A newer version in the (signature-checked) feed; <paramref name="Notes"/> is its one-line summary.</summary>
public sealed record UpdateOffer(string Version, long Size, string? Notes = null);

/// <summary>The installer's update machinery (Velopack in the Windows layer).</summary>
public interface IAppUpdates
{
    /// <summary>False for a dev run: nothing to update.</summary>
    bool IsInstalled { get; }

    string CurrentVersion { get; }

    /// <summary>Reads the site's feed (its signature checked); a newer version, or null.</summary>
    Task<UpdateOffer?> CheckAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Downloads the offer of the last check (bytes so far). Throws <see cref="InvalidDataException"/> when the file
    /// doesn't match the feed; it is deleted then.
    /// </summary>
    Task DownloadAsync(IProgress<long> progress, CancellationToken cancellationToken);

    /// <summary>Puts the downloaded version in once the app has quit; <paramref name="restart"/> starts it again in the tray.</summary>
    void ApplyAfterExit(bool restart);
}

/// <summary>What the checker remembers between starts (<c>updates.json</c>, versioned like the settings).</summary>
public sealed record UpdateMemory(string? HandedOver = null, string? ShowUpdatedTo = null, DateTimeOffset? LastCheck = null);

public interface IUpdateMemoryFile
{
    UpdateMemory Load();

    void Save(UpdateMemory memory);
}

/// <summary>
/// Updates (UP-10, board <c>updates-flow</c>): checks the feed at start and every 24 hours with the upload token,
/// downloads quietly and installs at a safe moment: on quit, at the next start, or (setting, on by default) when WoW is
/// closed and nothing waits to upload. Never during an upload. A required update (the site's minimum version) holds
/// uploads until it is installed. Everything here is quiet: the app shows it, no notifications.
/// </summary>
public sealed partial class UpdateChecker : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);
    public static readonly TimeSpan RetryAfter = TimeSpan.FromHours(1);
    public static readonly TimeSpan SafeMomentPoll = TimeSpan.FromMinutes(1);

    private readonly IAppUpdates _updates;
    private readonly ISiteClient _site;
    private readonly ITokenStore _tokens;
    private readonly IUploadLoop _loop;
    private readonly IGameProcesses _games;
    private readonly SettingsStore _settings;
    private readonly IUpdateMemoryFile _memory;
    private readonly TimeProvider _time;
    private readonly ILogger<UpdateChecker> _logger;
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _gate = new();
    private ITimer? _daily;
    private ITimer? _retry;
    private ITimer? _safeMoment;
    private int _checking;
    private bool _restartAsked;

    public UpdateChecker(IAppUpdates updates, ISiteClient site, ITokenStore tokens, IUploadLoop loop, IGameProcesses games, SettingsStore settings, IUpdateMemoryFile memory, TimeProvider time, ILogger<UpdateChecker> logger)
    {
        _updates = updates;
        _site = site;
        _tokens = tokens;
        _loop = loop;
        _games = games;
        _settings = settings;
        _memory = memory;
        _time = time;
        _logger = logger;
        State = updates.IsInstalled ? UpdateState.Idle : UpdateState.DevBuild;
    }

    /// <summary>Raised from the checking thread.</summary>
    public event EventHandler? Changed;

    /// <summary>Time to install: the app quits, the update goes in and (true) the app starts again in the tray.</summary>
    public event EventHandler<bool>? RestartRequested;

    public UpdateState State { get; private set; }

    public string CurrentVersion => _updates.CurrentVersion;

    /// <summary>The version on offer: downloading, ready or waiting.</summary>
    public string? NewVersion { get; private set; }

    /// <summary>The new version's one-line summary ("Fixes a missed upload after long sessions · shows planned runs").</summary>
    public string? NewNotes { get; private set; }

    public long DownloadedBytes { get; private set; }

    public long DownloadSize { get; private set; }

    public DateTimeOffset? LastCheck { get; private set; }

    /// <summary>When the running version came out (from the site's update status, when it is the newest).</summary>
    public DateTimeOffset? ReleasedAt { get; private set; }

    /// <summary>The site's "What's new" page for the newest version.</summary>
    public Uri? NotesUrl { get; private set; }

    /// <summary>The site needs a newer uploader: uploads are held until the update is in.</summary>
    public bool IsRequired { get; private set; }

    /// <summary>"Updated to 1.1 · What's new", once in the main window footer after an update.</summary>
    public string? UpdatedTo { get; private set; }

    /// <summary>Reads what happened to the last update and starts checking: now, then every 24 hours.</summary>
    public void Start()
    {
        if (!_updates.IsInstalled || _daily is not null)
        {
            return;
        }

        var memory = _memory.Load();
        LastCheck = memory.LastCheck;
        UpdatedTo = memory.ShowUpdatedTo;
        if (memory.HandedOver is { } expected)
        {
            if (Compare(CurrentVersion, expected) >= 0)
            {
                UpdatedTo = CurrentVersion;
                LogInstalled(CurrentVersion);
            }
            else
            {
                State = UpdateState.Failed;
                NewVersion = expected;
                LogInstallFailed(expected);
            }

            _memory.Save(memory with { HandedOver = null, ShowUpdatedTo = UpdatedTo });
        }

        _loop.StatusChanged += OnLoopChanged;
        // After a failed install "Update failed" stays until "Try again" or tomorrow's check.
        TimeSpan first = State == UpdateState.Failed ? Interval : TimeSpan.Zero;
        _daily = _time.CreateTimer(_ => _ = CheckNowAsync(), null, first, Interval);
    }

    /// <summary>One check ("Check now", "Try again"); a second call while one runs does nothing.</summary>
    public async Task CheckNowAsync()
    {
        if (!_updates.IsInstalled || State is UpdateState.Ready or UpdateState.WaitingForUpload
            || _tokens.Load() is not { } token || Interlocked.Exchange(ref _checking, 1) == 1)
        {
            return;
        }

        var before = State;
        try
        {
            Set(UpdateState.Checking);
            await ReadStatusAsync(token).ConfigureAwait(false);
            var offer = await _updates.CheckAsync(_stop.Token).ConfigureAwait(false);
            Remember(_time.GetUtcNow());
            if (offer is null)
            {
                NewVersion = null;
                Set(UpdateState.UpToDate);
                return;
            }

            NewVersion = offer.Version;
            NewNotes = offer.Notes;
            DownloadSize = offer.Size;
            DownloadedBytes = 0;
            Set(UpdateState.Downloading);
            await _updates.DownloadAsync(new Progress(this), _stop.Token).ConfigureAwait(false);
            LogReady(offer.Version);
            Set(UpdateState.Ready);
            WatchForSafeMoment();
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // Quitting.
        }
        catch (InvalidDataException e)
        {
            // A bad signature or a damaged download: thrown away, the next check fetches it again. Nothing to show.
            LogRejected(e.Message);
            NewVersion = null;
            Set(before is UpdateState.Failed or UpdateState.CouldntCheck or UpdateState.Checking ? UpdateState.Idle : before);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or TimeoutException or OperationCanceledException)
        {
            // Offline or the site is down: again in an hour, silently.
            LogCouldntCheck(e.GetType().Name);
            Set(UpdateState.CouldntCheck);
            _retry?.Dispose();
            _retry = _time.CreateTimer(_ => _ = CheckNowAsync(), null, RetryAfter, Timeout.InfiniteTimeSpan);
        }
        finally
        {
            Volatile.Write(ref _checking, 0);
        }
    }

    /// <summary>"Restart now" / "Restart and update": right away, or after the upload that is running.</summary>
    public void RestartNow()
    {
        if (State != UpdateState.Ready)
        {
            return;
        }

        if (IsUploadPending())
        {
            Set(UpdateState.WaitingForUpload);
            return;
        }

        Restart(quiet: false);
    }

    /// <summary>"Cancel" while waiting for an upload.</summary>
    public void CancelRestart()
    {
        if (State == UpdateState.WaitingForUpload)
        {
            Set(UpdateState.Ready);
        }
    }

    /// <summary>The "Updated to …" line was closed or its "What's new" opened.</summary>
    public void DismissUpdated()
    {
        if (UpdatedTo is null)
        {
            return;
        }

        UpdatedTo = null;
        _memory.Save(_memory.Load() with { ShowUpdatedTo = null });
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>On quit: hands a downloaded version to the installer.</summary>
    public void ApplyIfReady()
    {
        if (State is UpdateState.Ready or UpdateState.WaitingForUpload && !_restartAsked)
        {
            HandOver(restart: false);
        }
    }

    public void Dispose()
    {
        _loop.StatusChanged -= OnLoopChanged;
        _daily?.Dispose();
        _daily = null;
        _retry?.Dispose();
        _retry = null;
        _safeMoment?.Dispose();
        _safeMoment = null;
        _stop.Cancel();
        _stop.Dispose();
    }

    /// <summary>Compares "1.2.3" versions (a pre-release part is ignored); unreadable ones count as oldest.</summary>
    public static int Compare(string? a, string? b) => Parse(a).CompareTo(Parse(b));

    private static Version Parse(string? version) =>
        Version.TryParse(version?.Split('-', '+')[0], out var parsed) ? parsed : new Version(0, 0);

    // The site's update status: minimum version, release date, What's new. Missing on older sites: then none of it.
    private async Task ReadStatusAsync(AccessToken token)
    {
        var status = await _site.GetUpdateStatusAsync(token, _stop.Token).ConfigureAwait(false);
        if (!status.IsOk)
        {
            if (status.Failure!.Kind == SiteError.Offline)
            {
                throw new HttpRequestException("offline");
            }

            return;
        }

        var value = status.Value!;
        NotesUrl = value.NotesUrl;
        ReleasedAt = Compare(value.Latest, CurrentVersion) == 0 ? value.ReleasedAt : null;
        bool required = value.MinimumVersion is { } minimum && Compare(CurrentVersion, minimum) < 0;
        if (required != IsRequired)
        {
            IsRequired = required;
            if (required)
            {
                LogRequired(value.MinimumVersion!);
            }

            _loop.Hold(required);
        }
    }

    private void OnLoopChanged(object? sender, EventArgs e)
    {
        if (State == UpdateState.WaitingForUpload && !IsUploadPending())
        {
            Restart(quiet: false);
        }
    }

    private void WatchForSafeMoment()
    {
        _safeMoment?.Dispose();
        _safeMoment = _time.CreateTimer(_ => CheckSafeMoment(), null, SafeMomentPoll, SafeMomentPoll);
    }

    // "Install updates when WoW is closed": a quiet restart when the game isn't running and nothing waits to upload.
    private void CheckSafeMoment()
    {
        bool allowed = _settings.Load() is not SettingsLoad.Loaded { Settings: var s } || s.InstallUpdatesWhenWowClosed;
        if (State == UpdateState.Ready && allowed && !IsUploadPending() && !IsGameRunning())
        {
            Restart(quiet: true);
        }
    }

    private bool IsUploadPending() =>
        _loop.Statuses.Any(s => s.State is AccountState.Settling or AccountState.ReadyToUpload or AccountState.Uploading or AccountState.WaitingToSend);

    private bool IsGameRunning() =>
        _loop.Statuses.Select(s => s.Account.InstallPath).Distinct(StringComparer.OrdinalIgnoreCase).Any(_games.IsRunning);

    private void Restart(bool quiet)
    {
        lock (_gate)
        {
            if (_restartAsked)
            {
                return;
            }

            _restartAsked = true;
        }

        _safeMoment?.Dispose();
        _safeMoment = null;
        HandOver(restart: true);
        RestartRequested?.Invoke(this, quiet);
    }

    private void HandOver(bool restart)
    {
        if (NewVersion is { } version)
        {
            _memory.Save(_memory.Load() with { HandedOver = version });
        }

        _updates.ApplyAfterExit(restart);
    }

    private void Remember(DateTimeOffset at)
    {
        LastCheck = at;
        _memory.Save(_memory.Load() with { LastCheck = at });
    }

    private void Set(UpdateState state)
    {
        State = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Progress(UpdateChecker checker) : IProgress<long>
    {
        public void Report(long value)
        {
            checker.DownloadedBytes = value;
            checker.Changed?.Invoke(checker, EventArgs.Empty);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Update {Version} downloaded; it goes in at a safe moment")]
    private partial void LogReady(string version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated to {Version}")]
    private partial void LogInstalled(string version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update to {Version} didn't install; the old version runs on")]
    private partial void LogInstallFailed(string version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update thrown away: {Reason}")]
    private partial void LogRejected(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update check failed ({Error}); again in an hour")]
    private partial void LogCouldntCheck(string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The site needs version {Version} or newer; uploads wait for the update")]
    private partial void LogRequired(string version);
}
