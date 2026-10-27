using System.Threading.Channels;
using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using Microsoft.Extensions.Logging;

namespace CommunitiesForever.Upload.Core.Uploading;

/// <summary>
/// The watch-and-upload loop (docs/ARCHITECTURE.md "The loop", UP-06): one worker per watched account, uploads one at a
/// time. Everything with a lifetime is owned here and released by <see cref="StopAsync"/> (CLAUDE.md rule 11).
/// </summary>
public sealed partial class UploadLoop : IUploadLoop, IAsyncDisposable, IDisposable
{
    public static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan GameCheckInterval = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan FirstRetry = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan MaxRetry = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan NotAllowedRetry = TimeSpan.FromHours(1);
    public static readonly TimeSpan ReadRetry = TimeSpan.FromSeconds(1);
    public const int ReadAttempts = 3;

    private readonly ISiteClient _site;
    private readonly ITokenStore _tokens;
    private readonly UploadHistory _history;
    private readonly IAddonFileReader _files;
    private readonly IFileWatcherFactory _watchers;
    private readonly IGameProcesses _games;
    private readonly TimeProvider _time;
    private readonly ILogger<UploadLoop> _logger;
    private readonly SemaphoreSlim _uploadGate = new(1, 1);
    private readonly Lock _lock = new();
    private readonly List<AccountWorker> _workers = [];
    private readonly Dictionary<string, bool> _playing = new(StringComparer.OrdinalIgnoreCase);

    private ITimer? _gameTimer;
    private AppSettings _settings = new();
    private DateTimeOffset _startedAt;
    private DateTimeOffset? _pausedUntil;
    private volatile bool _notConnected;
    private volatile bool _held;

    public UploadLoop(
        ISiteClient site,
        ITokenStore tokens,
        UploadHistory history,
        IAddonFileReader files,
        IFileWatcherFactory watchers,
        IGameProcesses games,
        TimeProvider time,
        ILogger<UploadLoop> logger)
    {
        _site = site;
        _tokens = tokens;
        _history = history;
        _files = files;
        _watchers = watchers;
        _games = games;
        _time = time;
        _logger = logger;
    }

    /// <summary>Raised (on a background thread) whenever an account's status changes.</summary>
    public event EventHandler? StatusChanged;

    public IReadOnlyList<AccountStatus> Statuses
    {
        get
        {
            lock (_lock)
            {
                return [.. _workers.Select(w => w.Status)];
            }
        }
    }

    /// <summary>The most urgent state of all accounts (the tray icon).</summary>
    public AccountState Overall => Statuses.Select(s => s.State).DefaultIfEmpty(AccountState.Watching).Max();

    public bool IsPaused => _pausedUntil is { } until && _time.GetUtcNow() < until;

    /// <summary>When the pause ends, while paused.</summary>
    public DateTimeOffset? PausedUntil => IsPaused ? _pausedUntil : null;

    /// <summary>(Re)starts watching the settings' accounts.</summary>
    public async Task StartAsync(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await StopAsync().ConfigureAwait(false);
        _settings = settings;
        _startedAt = _time.GetUtcNow();
        _notConnected = _tokens.Load() is null;
        lock (_lock)
        {
            foreach (var account in settings.Accounts)
            {
                _workers.Add(new AccountWorker(this, account));
            }
        }

        _gameTimer = _time.CreateTimer(_ => CheckGames(), null, TimeSpan.Zero, GameCheckInterval);
        foreach (var worker in Workers())
        {
            worker.Start();
        }
    }

    /// <summary>No uploads until <paramref name="until"/>; saves are noted and the newest goes up after.</summary>
    public void Pause(DateTimeOffset until)
    {
        _pausedUntil = until;
        WakeAll();
    }

    public bool IsHeld => _held;

    public void Hold(bool held)
    {
        _held = held;
        WakeAll();
    }

    public void EndPause()
    {
        _pausedUntil = null;
        WakeAll();
    }

    /// <summary>"Upload now" / "Try now": sends the account's current file without waiting (all accounts when null).</summary>
    public void UploadNow(WatchedAccount? account = null)
    {
        foreach (var worker in Workers().Where(w => account is null || w.Account == account))
        {
            worker.RequestUploadNow();
        }
    }

    /// <summary>"Skip this one": the waiting file is never sent.</summary>
    public void Skip(WatchedAccount account)
    {
        foreach (var worker in Workers().Where(w => w.Account == account))
        {
            worker.RequestSkip();
        }
    }

    /// <summary>A new token was stored after "Reconnect".</summary>
    public void Reconnected()
    {
        _notConnected = _tokens.Load() is null;
        WakeAll();
    }

    public async Task StopAsync()
    {
        _gameTimer?.Dispose();
        _gameTimer = null;
        List<AccountWorker> workers;
        lock (_lock)
        {
            workers = [.. _workers];
            _workers.Clear();
        }

        foreach (var worker in workers)
        {
            await worker.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _uploadGate.Dispose();
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private List<AccountWorker> Workers()
    {
        lock (_lock)
        {
            return [.. _workers];
        }
    }

    private void WakeAll()
    {
        foreach (var worker in Workers())
        {
            worker.Wake();
        }
    }

    private void CheckGames()
    {
        foreach (string install in Workers().Select(w => w.Account.InstallPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            bool running = _games.IsRunning(install);
            bool changed;
            lock (_lock)
            {
                changed = !_playing.TryGetValue(install, out bool was) || was != running;
                _playing[install] = running;
            }

            if (changed)
            {
                WakeAll();
            }
        }
    }

    private bool IsPlaying(string install)
    {
        lock (_lock)
        {
            return _playing.TryGetValue(install, out bool running) && running;
        }
    }

    private void OnStatusChanged() => StatusChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>One watched account: watch, settle, hash, check the ledger, upload, handle the answer.</summary>
    private sealed class AccountWorker : IAsyncDisposable
    {
        private readonly UploadLoop _loop;
        private readonly string _folder;
        private readonly string _file;
        private readonly CancellationTokenSource _stop = new();
        private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

        private Task _run = Task.CompletedTask;
        private IDisposable? _watcher;
        private FileStamp? _seen;
        private FileStamp? _handled;
        private (FileStamp Stamp, string Hash)? _pending;
        private DateTimeOffset? _nextTryAt;
        private TimeSpan _retryDelay = FirstRetry;
        private bool _uploadNow;
        private bool _skip;
        private bool _notUploaded;
        private string? _rejection;
        private int? _schemaVersion;

        public AccountWorker(UploadLoop loop, WatchedAccount account)
        {
            _loop = loop;
            Account = account;
            _folder = Path.Combine(account.InstallPath, "WTF", "Account", account.Account, "SavedVariables");
            _file = Path.Combine(_folder, GameScanner.AddonFileName);
            Status = new AccountStatus(account, AccountState.Watching, LastUpload: loop._history.LastUpload(account));
        }

        public WatchedAccount Account { get; }

        public AccountStatus Status { get; private set; }

        private DateTimeOffset Now => _loop._time.GetUtcNow();

        public void Start() => _run = Task.Run(RunAsync);

        public void Wake() => _wake.Writer.TryWrite(true);

        public void RequestUploadNow()
        {
            _uploadNow = true;
            Wake();
        }

        public void RequestSkip()
        {
            _skip = true;
            Wake();
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync().ConfigureAwait(false);
            try
            {
                await _run.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Stopped.
            }

            _watcher?.Dispose();
            _watcher = null;
            _stop.Dispose();
        }

        private async Task RunAsync()
        {
            var ct = _stop.Token;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await StepAsync(ct).ConfigureAwait(false);
                    await WaitAsync(NextWait(), ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Stopped.
            }
        }

        private async Task StepAsync(CancellationToken ct)
        {
            if (_loop._notConnected)
            {
                Set(AccountState.NotConnected);
                return;
            }

            if (!_loop._files.FolderExists(_folder) || _loop._files.GetStamp(_file) is not { } stamp)
            {
                _seen = null;
                StopWatching();
                Set(AccountState.NotWatching);
                return;
            }

            _watcher ??= _loop._watchers.Watch(_folder, GameScanner.AddonFileName, Wake);
            _seen = stamp;

            if (stamp != _handled && stamp != _pending?.Stamp)
            {
                if (await SettleAndHashAsync(stamp, ct).ConfigureAwait(false) is not { } settled)
                {
                    return;
                }

                if (!_uploadNow && _loop._history.IsHandled(Account, settled.Hash, Now, _loop._startedAt))
                {
                    _handled = settled.Stamp;
                    _pending = null;
                    SetIdle();
                    return;
                }

                _pending = settled;
                _nextTryAt = null;
                _retryDelay = FirstRetry;
            }

            if (_pending is not { } pending)
            {
                // Nothing waiting: an "Upload now" must not carry over to the next save (it would skip the ledger).
                _uploadNow = false;
                SetIdle();
                return;
            }

            if (_skip)
            {
                _skip = false;
                _loop._history.Record(Account, new LedgerEntry(pending.Hash, LedgerOutcome.Skipped, null, Now));
                Handled(pending.Stamp, notUploaded: false);
                return;
            }

            if (_loop._held)
            {
                Set(AccountState.Paused);
                return;
            }

            bool forced = _uploadNow;
            if (!forced)
            {
                if (_loop.IsPaused)
                {
                    Set(AccountState.Paused);
                    return;
                }

                if (!_loop._settings.UploadAutomatically)
                {
                    Set(AccountState.ReadyToUpload);
                    return;
                }

                if (_nextTryAt is { } next && Now < next)
                {
                    return; // still WaitingToSend or NotAllowed
                }
            }

            _uploadNow = false;
            await UploadAsync(pending, ct).ConfigureAwait(false);
        }

        // "Unchanged for 5 s", then read (retried while the game holds the file) and hash.
        private async Task<(FileStamp Stamp, string Hash)?> SettleAndHashAsync(FileStamp stamp, CancellationToken ct)
        {
            var settlesAt = Now + SettleTime;
            while (true)
            {
                Set(AccountState.Settling, settlesAt: settlesAt);
                if (!_uploadNow)
                {
                    // "Upload now" already asked: its wake was used up getting here, so don't wait for another.
                    await WaitAsync(settlesAt - Now, ct).ConfigureAwait(false);
                }
                if (_loop._files.GetStamp(_file) is not { } now)
                {
                    Set(AccountState.NotWatching);
                    return null;
                }

                if (now != stamp)
                {
                    stamp = now;
                    _seen = now;
                    settlesAt = Now + SettleTime;
                    continue;
                }

                if (_uploadNow || Now >= settlesAt)
                {
                    break;
                }
            }

            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    byte[] bytes = await _loop._files.ReadAllAsync(_file, ct).ConfigureAwait(false);
                    using var content = new MemoryStream(bytes, writable: false);
                    return (stamp, await ContentHash.ComputeAsync(content, ct).ConfigureAwait(false));
                }
                catch (IOException e) when (attempt < ReadAttempts)
                {
                    Log.ReadRetry(_loop._logger, Account.Account, e.Message);
                    await Task.Delay(ReadRetry, _loop._time, ct).ConfigureAwait(false);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // Still held: try again at the next wake or poll.
                    Log.ReadFailed(_loop._logger, Account.Account, e.Message);
                    SetIdle();
                    return null;
                }
            }
        }

        private async Task UploadAsync((FileStamp Stamp, string Hash) pending, CancellationToken ct)
        {
            if (_loop._tokens.Load() is not { } token)
            {
                _loop._notConnected = true;
                Set(AccountState.NotConnected);
                return;
            }

            await _loop._uploadGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                Set(AccountState.Uploading);
                byte[] bytes;
                try
                {
                    bytes = await _loop._files.ReadAllAsync(_file, ct).ConfigureAwait(false);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Log.ReadFailed(_loop._logger, Account.Account, e.Message);
                    RetryLater(SiteError.Offline);
                    return;
                }

                using var content = new MemoryStream(bytes, writable: false);
                if (await ContentHash.ComputeAsync(content, ct).ConfigureAwait(false) != pending.Hash)
                {
                    // Changed since it settled: settle the new one first.
                    _pending = null;
                    Wake();
                    return;
                }

                content.Position = 0;
                var result = await _loop._site.UploadAsync(token, content, pending.Stamp.WrittenAt, ct).ConfigureAwait(false);
                Handle(pending, result);
            }
            finally
            {
                _loop._uploadGate.Release();
            }
        }

        private void Handle((FileStamp Stamp, string Hash) pending, SiteResult<UploadReceipt> result)
        {
            if (result.IsOk)
            {
                var receipt = result.Value!;
                var entry = new HistoryEntry(receipt.UploadId, receipt.UploadedAt, receipt.Status, receipt.Rejection, receipt.SchemaVersion, receipt.SummaryLine, receipt.Highlights, receipt.ReportUrl);
                var outcome = receipt.Status switch
                {
                    UploadStatus.Ok => LedgerOutcome.Ok,
                    UploadStatus.NothingNew => LedgerOutcome.NothingNew,
                    _ => LedgerOutcome.Rejected,
                };
                _loop._history.Record(Account, new LedgerEntry(pending.Hash, outcome, receipt.Rejection, Now), entry);
                Log.Uploaded(_loop._logger, Account.Account, receipt.UploadId, receipt.Status, receipt.Rejection);
                _rejection = receipt.Rejection;
                _schemaVersion = receipt.SchemaVersion;
                Handled(pending.Stamp, notUploaded: receipt.Status == UploadStatus.Rejected);
                return;
            }

            var failure = result.Failure!;
            switch (failure.Kind)
            {
                case SiteError.TooLarge:
                case SiteError.EmptyFile:
                    var final = failure.Kind == SiteError.TooLarge ? LedgerOutcome.TooLarge : LedgerOutcome.EmptyFile;
                    _loop._history.Record(Account, new LedgerEntry(pending.Hash, final, failure.Code, Now));
                    Log.Refused(_loop._logger, Account.Account, failure.Kind);
                    _rejection = failure.Code;
                    _schemaVersion = null;
                    Handled(pending.Stamp, notUploaded: true);
                    break;
                case SiteError.InvalidToken:
                    Log.NotConnected(_loop._logger);
                    _loop._notConnected = true;
                    Set(AccountState.NotConnected);
                    _loop.WakeAll();
                    break;
                case SiteError.MayNotUpload:
                    _nextTryAt = Now + NotAllowedRetry;
                    Log.Refused(_loop._logger, Account.Account, failure.Kind);
                    Set(AccountState.NotAllowed, nextTryAt: _nextTryAt);
                    break;
                case SiteError.RateLimited:
                    _nextTryAt = Now + (failure.RetryAfter ?? FirstRetry);
                    Set(AccountState.WaitingToSend, nextTryAt: _nextTryAt);
                    break;
                default:
                    RetryLater(failure.Kind);
                    break;
            }
        }

        // Offline and the like: 1 min, doubling up to 30 min.
        private void RetryLater(SiteError kind)
        {
            _nextTryAt = Now + _retryDelay;
            Log.WaitingToSend(_loop._logger, Account.Account, kind, _retryDelay);
            _retryDelay = TimeSpan.FromTicks(Math.Min(_retryDelay.Ticks * 2, MaxRetry.Ticks));
            Set(AccountState.WaitingToSend, nextTryAt: _nextTryAt);
        }

        private void Handled(FileStamp stamp, bool notUploaded)
        {
            _handled = stamp;
            _pending = null;
            _nextTryAt = null;
            _retryDelay = FirstRetry;
            _notUploaded = notUploaded;
            SetIdle();
        }

        private void SetIdle()
        {
            if (_notUploaded)
            {
                Set(AccountState.NotUploaded, rejection: _rejection, schemaVersion: _schemaVersion);
            }
            else
            {
                Set(_loop.IsPlaying(Account.InstallPath) ? AccountState.Playing : AccountState.Watching);
            }
        }

        private void Set(AccountState state, DateTimeOffset? settlesAt = null, DateTimeOffset? nextTryAt = null, string? rejection = null, int? schemaVersion = null)
        {
            var status = new AccountStatus(Account, state, settlesAt, nextTryAt, rejection, schemaVersion, _loop._history.LastUpload(Account), _seen);
            if (status == Status)
            {
                return;
            }

            Status = status;
            _loop.OnStatusChanged();
        }

        private void StopWatching()
        {
            _watcher?.Dispose();
            _watcher = null;
        }

        private TimeSpan NextWait()
        {
            var wait = PollInterval;
            if (_pending is not null && _nextTryAt is { } next)
            {
                wait = Min(wait, next - Now);
            }

            if (_pending is not null && _loop._pausedUntil is { } until && until > Now)
            {
                wait = Min(wait, until - Now);
            }

            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        // Waits for a wake (watcher, command) or the timeout; the losing wait is cancelled, so nothing piles up.
        private async Task WaitAsync(TimeSpan timeout, CancellationToken ct)
        {
            if (_wake.Reader.TryRead(out _))
            {
                return;
            }

            if (timeout <= TimeSpan.Zero)
            {
                return;
            }

            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var woken = _wake.Reader.WaitToReadAsync(waiting.Token).AsTask();
            var delay = Task.Delay(timeout, _loop._time, waiting.Token);
            await Task.WhenAny(woken, delay).ConfigureAwait(false);
            await waiting.CancelAsync().ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            _wake.Reader.TryRead(out _);
        }

        private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Uploaded {Account}: #{UploadId} {Status} {Rejection}")]
        public static partial void Uploaded(ILogger logger, string account, long uploadId, UploadStatus status, string? rejection);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Not uploaded {Account}: {Kind}")]
        public static partial void Refused(ILogger logger, string account, SiteError kind);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Waiting to send {Account} ({Kind}); next try in {Delay}")]
        public static partial void WaitingToSend(ILogger logger, string account, SiteError kind, TimeSpan delay);

        [LoggerMessage(Level = LogLevel.Warning, Message = "The site no longer accepts the token: not connected")]
        public static partial void NotConnected(ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "File of {Account} still held, retrying: {Reason}")]
        public static partial void ReadRetry(ILogger logger, string account, string reason);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the file of {Account}: {Reason}")]
        public static partial void ReadFailed(ILogger logger, string account, string reason);
    }
}
