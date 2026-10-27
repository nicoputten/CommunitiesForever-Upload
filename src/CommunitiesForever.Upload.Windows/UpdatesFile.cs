using CommunitiesForever.Upload.Core.Updates;

namespace CommunitiesForever.Upload.Windows;

/// <summary>updates.json next to the settings (UP-10).</summary>
internal sealed class UpdatesFile(string directory) : IUpdatesFile
{
    private string Path => System.IO.Path.Combine(directory, "updates.json");

    public string? Read() => File.Exists(Path) ? File.ReadAllText(Path) : null;

    public void Write(string text)
    {
        Directory.CreateDirectory(directory);
        string temporary = Path + ".tmp";
        File.WriteAllText(temporary, text);
        File.Move(temporary, Path, overwrite: true);
    }
}
