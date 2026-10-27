using CommunitiesForever.Upload.Core.Game;

namespace CommunitiesForever.Upload.Windows;

/// <summary>The real disk, for the game scanner: folders and file times only.</summary>
internal sealed class PhysicalFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> GetDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public DateTimeOffset? GetLastWriteTime(string path) =>
        File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : null;
}
