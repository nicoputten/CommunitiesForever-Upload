using CommunitiesForever.Upload.Core.Settings;

namespace CommunitiesForever.Upload.Windows;

/// <summary>settings.json in the app's data folder, replaced in one step.</summary>
internal sealed class SettingsFile(string directory) : ISettingsFile
{
    private string FilePath => Path.Combine(directory, "settings.json");

    public string? Read() => File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;

    public void Write(string text)
    {
        Directory.CreateDirectory(directory);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, FilePath, overwrite: true);
    }

    public void MoveAsideAsDamaged()
    {
        if (File.Exists(FilePath))
        {
            File.Move(FilePath, FilePath + ".bad", overwrite: true);
        }
    }
}
