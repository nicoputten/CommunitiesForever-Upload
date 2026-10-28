using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Uploading;
using Microsoft.Extensions.Logging;

namespace CommunitiesForever.Upload.Core.Runs;

/// <summary>Where the addon of one install stands for Runs.lua.</summary>
public enum AddonFolder
{
    /// <summary><c>Interface\AddOns\CommunitiesForever</c> is missing: the addon isn't installed.</summary>
    Missing,

    /// <summary>The addon has no <c>Data</c> folder: a version before CF-45.</summary>
    TooOld,

    /// <summary><c>Data</c> exists but Runs.lua doesn't: the game loads it only after a restart (addon V67).</summary>
    NoFile,

    Ready,
}

/// <summary>The addon's Runs.lua per install (Windows: the file system). Never touches the addon's saved file.</summary>
public interface IRunsFiles
{
    /// <summary><c>&lt;foreverPath&gt;\Interface\AddOns\CommunitiesForever\Data\Runs.lua</c>.</summary>
    string PathFor(string foreverPath);

    AddonFolder Inspect(string foreverPath);

    /// <summary>Runs.lua's bytes (only to compare with what would be written), or null when missing.</summary>
    byte[]? Read(string foreverPath);

    /// <summary>Writes a temp file next to Runs.lua and moves it over, so a /reload never reads half a file.</summary>
    void Write(string foreverPath, byte[] content);
}

/// <summary>What the main window's Runs block needs to say (most urgent first).</summary>
public enum RunsHint
{
    None,

    /// <summary>Runs.lua changed while the game runs: "/reload to see them".</summary>
    Reload,

    /// <summary>Runs.lua was new: the game reads it only after a restart.</summary>
    RestartWow,
    UpdateAddon,
    InstallAddon,
}

/// <summary>
/// The Runs block: <see cref="RunCount"/> and <see cref="FetchedAt"/> after a fetch; <see cref="NextAt"/> while WoW runs;
/// <see cref="Failed"/> when the last fetch didn't work.
/// </summary>
public sealed record RunsStatus(
    int? RunCount = null,
    DateTimeOffset? FetchedAt = null,
    DateTimeOffset? NextAt = null,
    bool Failed = false,
    bool GameRunning = false,
    RunsHint Hint = RunsHint.None,
    string? FilePath = null);

/// <summary>
/// Planned runs to the game (UP-12, board <c>runs-flow</c>): fetches the site's upcoming runs when WoW starts, every
/// <see cref="Interval"/> while it runs and right after each upload, and writes them into each watched install's
/// Runs.lua when they changed. Nothing while WoW is closed. Owns its timer and cancellation (CLAUDE.md rule 11); nothing
/// is stored, the status lives in memory.
/// </summary>
public sealed partial class RunsSync : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly ISiteClient _site;
    private readonly ITokenStore _tokens;
    private readonly IUploadLoop _loop;
    private readonly IGameProcesses _games;
    private readonly IRunsFiles _files;
    private readonly TimeProvider _time;
    private readonly ILogger<RunsSync> _logger;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _gameStarted = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _written = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _restart = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource _stop = new();
    private IReadOnlyList<string> _installs = [];
    private ITimer? _timer;
    private DateTimeOffset? _lastUpload;
    private DateTimeOffset? _nextFetch;
    private int? _runCount;
    private DateTimeOffset? _fetchedAt;
    private bool _failed;
    private int _fetching;

    public RunsSync(ISiteClient site, ITokenStore tokens, IUploadLoop loop, IGameProcesses games, IRunsFiles files, TimeProvider time, ILogger<RunsSync> logger)
    {
        _site = site;
        _tokens = tokens;
        _loop = loop;
        _games = games;
        _files = files;
        _time = time;
        _logger = logger;
    }

    /// <summary>Raised (on a background thread) when <see cref="Status"/> changes.</summary>
    public event EventHandler? Changed;

    public RunsStatus Status { get; private set; } = new();

    internal bool IsFetching => Volatile.Read(ref _fetching) == 1;

    /// <summary>(Re)starts for the settings' installs.</summary>
    public void Start(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Stop();
        lock (_gate)
        {
            _installs = [.. settings.Accounts.Select(a => a.InstallPath).Distinct(StringComparer.OrdinalIgnoreCase)];
            _lastUpload = LastUpload();
        }

        _loop.StatusChanged += OnLoopChanged;
        _timer = _time.CreateTimer(_ => Tick(), null, TimeSpan.Zero, UploadLoop.GameCheckInterval);
    }

    public void Stop()
    {
        _loop.StatusChanged -= OnLoopChanged;
        _timer?.Dispose();
        _timer = null;
        lock (_gate)
        {
            _stop.Cancel();
            _stop.Dispose();
            _stop = new CancellationTokenSource();
            _gameStarted.Clear();
            _written.Clear();
            _restart.Clear();
            _nextFetch = null;
            _runCount = null;
            _fetchedAt = null;
            _failed = false;
        }
    }

    public void Dispose()
    {
        Stop();
        _stop.Dispose();
    }

    // Every 10 s: a game that started fetches now; while any runs, every Interval.
    private void Tick()
    {
        var now = _time.GetUtcNow();
        bool started = false;
        bool running = false;
        lock (_gate)
        {
            foreach (string install in _installs)
            {
                if (_games.IsRunning(install))
                {
                    running = true;
                    if (_gameStarted.TryAdd(install, now))
                    {
                        started = true;
                    }
                }
                else if (_gameStarted.Remove(install))
                {
                    _restart.Remove(install);
                }
            }
        }

        if (started || (running && _nextFetch is { } next && now >= next))
        {
            _ = FetchAsync();
        }
        else
        {
            Publish();
        }
    }

    // After an upload that reached the site (a newer last upload on any account).
    private void OnLoopChanged(object? sender, EventArgs e)
    {
        var last = LastUpload();
        bool newer;
        lock (_gate)
        {
            newer = last is { } at && (_lastUpload is null || at > _lastUpload);
            if (newer)
            {
                _lastUpload = last;
            }
        }

        if (newer)
        {
            _ = FetchAsync();
        }
    }

    private DateTimeOffset? LastUpload() =>
        _loop.Statuses.Select(s => s.LastUpload?.UploadedAt).Where(at => at is not null).DefaultIfEmpty(null).Max();

    /// <summary>One fetch and write; a fetch already running makes this one a no-op.</summary>
    internal async Task FetchAsync()
    {
        if (Interlocked.Exchange(ref _fetching, 1) == 1)
        {
            return;
        }

        try
        {
            CancellationToken cancel;
            lock (_gate)
            {
                cancel = _stop.Token;
                _nextFetch = _time.GetUtcNow() + Interval;
            }

            if (_tokens.Load() is not { } token)
            {
                return;
            }

            var result = await _site.GetRunsAsync(token, cancel).ConfigureAwait(false);
            if (!result.IsOk)
            {
                LogFetchFailed(result.Failure!.Kind);
                Fetched(failed: true);
                return;
            }

            var lua = RunsLua.Write(result.Value!.Root);
            if (lua.Trimmed > 0)
            {
                LogTrimmed(lua.Trimmed);
            }

            if (lua.Content is null)
            {
                LogTooLarge();
                Fetched(failed: true);
                return;
            }

            WriteAll(lua.Content);
            Fetched(failed: false, result.Value.RunCount);
        }
        catch (OperationCanceledException)
        {
            // Stopped.
        }
        finally
        {
            Volatile.Write(ref _fetching, 0);
            Publish();
        }
    }

    private void Fetched(bool failed, int? runCount = null)
    {
        lock (_gate)
        {
            _failed = failed;
            if (!failed)
            {
                _runCount = runCount;
                _fetchedAt = _time.GetUtcNow();
            }
        }
    }

    private void WriteAll(byte[] content)
    {
        string[] installs;
        lock (_gate)
        {
            installs = [.. _installs];
        }

        foreach (string install in installs)
        {
            var folder = _files.Inspect(install);
            if (folder is AddonFolder.Missing or AddonFolder.TooOld)
            {
                continue;
            }

            try
            {
                if (folder == AddonFolder.Ready && _files.Read(install) is { } old && old.AsSpan().SequenceEqual(content))
                {
                    continue;
                }

                _files.Write(install, content);
                lock (_gate)
                {
                    _written[install] = _time.GetUtcNow();
                    if (folder == AddonFolder.NoFile && _gameStarted.ContainsKey(install))
                    {
                        _restart.Add(install);
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                LogWriteFailed(e.GetType().Name);
            }
        }
    }

    // The status from what is known now; raises Changed when it differs.
    private void Publish()
    {
        RunsStatus next;
        lock (_gate)
        {
            bool running = _gameStarted.Count > 0;
            var hint = RunsHint.None;
            string? path = null;
            foreach (string install in _installs)
            {
                var folder = _files.Inspect(install);
                var here = folder switch
                {
                    AddonFolder.Missing => RunsHint.InstallAddon,
                    AddonFolder.TooOld => RunsHint.UpdateAddon,
                    _ when _restart.Contains(install) => RunsHint.RestartWow,
                    _ when _gameStarted.TryGetValue(install, out var since) && _written.TryGetValue(install, out var at) && at >= since => RunsHint.Reload,
                    _ => RunsHint.None,
                };
                if (folder is AddonFolder.Ready or AddonFolder.NoFile)
                {
                    path ??= _files.PathFor(install);
                }

                if (here > hint)
                {
                    hint = here;
                }
            }

            next = new RunsStatus(_runCount, _fetchedAt, running ? _nextFetch : null, _failed, running, hint, path);
        }

        if (next != Status)
        {
            Status = next;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Runs: fetch failed ({Kind})")]
    private partial void LogFetchFailed(SiteError kind);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Runs: {Count} values over the caps left out or cut")]
    private partial void LogTrimmed(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Runs: the answer is over the file size cap; nothing written")]
    private partial void LogTooLarge();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Runs: writing Runs.lua failed ({Error})")]
    private partial void LogWriteFailed(string error);
}
