namespace CommunitiesForever.Upload.Core.Uploading;

/// <summary>Size and write time: what "unchanged for 5 s" compares.</summary>
public readonly record struct FileStamp(long Size, DateTimeOffset WrittenAt);

/// <summary>Reads the addon file as bytes, to hash and send. Never parsed (CLAUDE.md rule 1).</summary>
public interface IAddonFileReader
{
    /// <summary>The file's size and write time, or null when it doesn't exist.</summary>
    FileStamp? GetStamp(string path);

    /// <summary>All bytes; throws <see cref="IOException"/> while the game still holds the file.</summary>
    Task<byte[]> ReadAllAsync(string path, CancellationToken cancellationToken);

    bool FolderExists(string path);
}

/// <summary>Tells when a file in a folder changes (Windows: FileSystemWatcher). Disposing stops watching.</summary>
public interface IFileWatcherFactory
{
    IDisposable Watch(string folder, string fileName, Action changed);
}

/// <summary>Whether WoW: Forever runs from an install (only to show "Playing").</summary>
public interface IGameProcesses
{
    bool IsRunning(string foreverPath);
}

/// <summary>Reads and writes history.json (ledger and upload history).</summary>
public interface IHistoryFile
{
    string? Read();

    void Write(string text);
}
