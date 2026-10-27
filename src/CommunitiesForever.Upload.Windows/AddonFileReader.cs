using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.Windows;

/// <summary>The addon file's bytes and stamp. Opened with sharing, so the game can keep writing; never parsed.</summary>
internal sealed class AddonFileReader : IAddonFileReader
{
    public FileStamp? GetStamp(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? new FileStamp(info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)) : null;
    }

    public async Task<byte[]> ReadAllAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, useAsync: true);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        return copy.ToArray();
    }

    public bool FolderExists(string path) => Directory.Exists(path);
}
