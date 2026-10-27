using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.Windows;

/// <summary>history.json in the app's data folder, replaced in one step.</summary>
internal sealed class HistoryFile(string directory) : IHistoryFile
{
    private string FilePath => Path.Combine(directory, "history.json");

    public string? Read() => File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;

    public void Write(string text)
    {
        Directory.CreateDirectory(directory);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, FilePath, overwrite: true);
    }
}
