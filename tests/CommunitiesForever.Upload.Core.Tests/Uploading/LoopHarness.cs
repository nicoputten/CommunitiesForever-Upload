using System.Diagnostics;
using System.Text;
using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Uploading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace CommunitiesForever.Upload.Core.Tests.Uploading;

/// <summary>An <see cref="UploadLoop"/> over fakes: disk, watcher, game, site, tokens and history file, on a fake clock.</summary>
internal sealed class LoopHarness : IAsyncDisposable
{
    public const string Forever = @"C:\WoW\_forever_";

    public static readonly AccessToken Token = new("cfu_3q2-7wEXAMPLEexampleEXAMPLEexampleEXAMPLExy");

    public LoopHarness(params string[] accounts)
    {
        Accounts = [.. accounts.DefaultIfEmpty("NICO").Select(a => new WatchedAccount(Forever, a))];
        Tokens.Token = Token;
        History = new UploadHistory(HistoryFile);
        Loop = NewLoop();
    }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 27, 20, 0, 0, TimeSpan.Zero));

    public FakeFiles Files { get; } = new();

    public FakeWatchers Watchers { get; } = new();

    public FakeGames Games { get; } = new();

    public FakeUploads Site { get; } = new();

    public MemoryTokens Tokens { get; } = new();

    public MemoryHistoryFile HistoryFile { get; } = new();

    public CapturingLogger Logger { get; } = new();

    public UploadHistory History { get; private set; }

    public IReadOnlyList<WatchedAccount> Accounts { get; }

    public UploadLoop Loop { get; private set; }

    public AppSettings Settings { get; set; } = new();

    public WatchedAccount First => Accounts[0];

    public static string FilePath(WatchedAccount account) =>
        Path.Combine(account.InstallPath, "WTF", "Account", account.Account, "SavedVariables", GameScanner.AddonFileName);

    public Task StartAsync() => Loop.StartAsync(Settings with { Accounts = Accounts });

    /// <summary>A new loop over the same history file, as after an app restart.</summary>
    public async Task RestartAsync()
    {
        await Loop.DisposeAsync();
        History = new UploadHistory(HistoryFile);
        Loop = NewLoop();
        await StartAsync();
    }

    /// <summary>The game saves the account's file.</summary>
    public void Save(WatchedAccount account, string content)
    {
        // Each real save has its own write time.
        Time.Advance(TimeSpan.FromMilliseconds(1));
        Files.Write(FilePath(account), Encoding.UTF8.GetBytes(content), Time.GetUtcNow());
        Watchers.Fire(Path.GetDirectoryName(FilePath(account))!);
    }

    public AccountStatus Status(WatchedAccount account) => Loop.Statuses.Single(s => s.Account == account);

    /// <summary>Moves the clock in steps until <paramref name="done"/> (or fails after <paramref name="max"/> of fake time).</summary>
    public async Task AdvanceUntil(Func<bool> done, TimeSpan? step = null, TimeSpan? max = null)
    {
        var limit = Time.GetUtcNow() + (max ?? TimeSpan.FromMinutes(5));
        await Settle(done, TimeSpan.FromMilliseconds(50));
        while (!done())
        {
            Assert.True(Time.GetUtcNow() < limit, "The loop did not get there in time");
            Time.Advance(step ?? TimeSpan.FromSeconds(1));
            await Settle(done, TimeSpan.FromMilliseconds(20));
        }
    }

    /// <summary>Lets the workers run (real time, briefly) without moving the clock.</summary>
    public static async Task Settle(Func<bool>? done = null, TimeSpan? max = null)
    {
        var watch = Stopwatch.StartNew();
        var limit = max ?? TimeSpan.FromMilliseconds(100);
        while (watch.Elapsed < limit && done?.Invoke() != true)
        {
            await Task.Yield();
            Thread.Yield();
        }
    }

    public async ValueTask DisposeAsync() => await Loop.DisposeAsync();

    private UploadLoop NewLoop() => new(Site, Tokens, History, Files, Watchers, Games, Time, Logger);
}

internal sealed class FakeFiles : IAddonFileReader
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, (byte[] Bytes, FileStamp Stamp)> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How many more reads fail as if the game still held the file.</summary>
    public int HeldReads { get; set; }

    public void Write(string path, byte[] bytes, DateTimeOffset at)
    {
        lock (_lock)
        {
            _files[path] = (bytes, new FileStamp(bytes.Length, at));
        }
    }

    public void Delete(string path)
    {
        lock (_lock)
        {
            _files.Remove(path);
        }
    }

    public FileStamp? GetStamp(string path)
    {
        lock (_lock)
        {
            return _files.TryGetValue(path, out var file) ? file.Stamp : null;
        }
    }

    public Task<byte[]> ReadAllAsync(string path, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (HeldReads > 0)
            {
                HeldReads--;
                throw new IOException("The process cannot access the file because it is being used by another process.");
            }

            return _files.TryGetValue(path, out var file) ? Task.FromResult(file.Bytes) : throw new FileNotFoundException(path);
        }
    }

    public bool FolderExists(string path)
    {
        lock (_lock)
        {
            return _files.Keys.Any(f => string.Equals(Path.GetDirectoryName(f), path, StringComparison.OrdinalIgnoreCase));
        }
    }
}

internal sealed class FakeWatchers : IFileWatcherFactory
{
    private readonly Lock _lock = new();
    private readonly List<(string Folder, Action Changed, Handle Handle)> _active = [];

    public int Active
    {
        get
        {
            lock (_lock)
            {
                return _active.Count;
            }
        }
    }

    public IDisposable Watch(string folder, string fileName, Action changed)
    {
        var handle = new Handle(this);
        lock (_lock)
        {
            _active.Add((folder, changed, handle));
        }

        return handle;
    }

    public void Fire(string folder)
    {
        List<Action> callbacks;
        lock (_lock)
        {
            callbacks = [.. _active.Where(a => string.Equals(a.Folder, folder, StringComparison.OrdinalIgnoreCase)).Select(a => a.Changed)];
        }

        callbacks.ForEach(c => c());
    }

    private void Remove(Handle handle)
    {
        lock (_lock)
        {
            _active.RemoveAll(a => a.Handle == handle);
        }
    }

    internal sealed class Handle(FakeWatchers owner) : IDisposable
    {
        public void Dispose() => owner.Remove(this);
    }
}

internal sealed class FakeGames : IGameProcesses
{
    public bool Running { get; set; }

    public bool IsRunning(string foreverPath) => Running;
}

internal sealed class FakeUploads : ISiteClient
{
    private readonly Lock _lock = new();
    private readonly Queue<SiteResult<UploadReceipt>> _answers = new();
    private long _nextId = 1;

    public List<string> Uploaded { get; } = [];

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return Uploaded.Count;
            }
        }
    }

    public void Answer(SiteResult<UploadReceipt> answer)
    {
        lock (_lock)
        {
            _answers.Enqueue(answer);
        }
    }

    public static SiteResult<UploadReceipt> Receipt(UploadStatus status = UploadStatus.Ok, string? rejection = null, long id = 0) =>
        SiteResult.Ok(new UploadReceipt(
            id,
            DateTimeOffset.UnixEpoch,
            status,
            rejection,
            rejection == UploadReceipt.Rejections.NewerFormat ? 3 : null,
            status == UploadStatus.Ok ? "5 levels gained" : "Nothing new",
            status == UploadStatus.Ok ? ["Ves Doombringer 37 → 39"] : [],
            null,
            new Uri($"https://wow.vestras.nl/upload/{id}")));

    public async Task<SiteResult<UploadReceipt>> UploadAsync(AccessToken token, Stream content, DateTimeOffset? savedAt, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(content);
        string text = await reader.ReadToEndAsync(cancellationToken);
        lock (_lock)
        {
            Uploaded.Add(text);
            return _answers.Count > 0 ? _answers.Dequeue() : Receipt(id: _nextId++);
        }
    }

    public Task<SiteResult<DeviceAuthorization>> RequestDeviceCodeAsync(string deviceName, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<TokenPoll>> PollTokenAsync(DeviceCode deviceCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<SiteIdentity>> GetIdentityAsync(AccessToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<bool>> RevokeAsync(AccessToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<UpdateStatus>> GetUpdateStatusAsync(AccessToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class MemoryTokens : ITokenStore
{
    public AccessToken? Token { get; set; }

    public AccessToken? Load() => Token;

    public void Save(AccessToken token) => Token = token;

    public void Delete() => Token = null;
}

internal sealed class MemoryHistoryFile : IHistoryFile
{
    public string? Text { get; set; }

    public string? Read() => Text;

    public void Write(string text) => Text = text;
}

internal sealed class CapturingLogger : ILogger<UploadLoop>
{
    private readonly Lock _lock = new();
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lock)
            {
                return [.. _lines];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_lock)
        {
            _lines.Add(formatter(state, exception) + " " + exception);
        }
    }
}
