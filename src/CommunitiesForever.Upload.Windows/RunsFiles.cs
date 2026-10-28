using CommunitiesForever.Upload.Core.Runs;

namespace CommunitiesForever.Upload.Windows;

/// <summary>
/// The addon's Runs.lua on disk (UP-12): the only file the app writes into the game (CLAUDE.md rule 1's exception).
/// Atomic, like settings.json: a temp file next to it, then a move over it.
/// </summary>
internal sealed class RunsFiles : IRunsFiles
{
    private static string AddonFolderOf(string foreverPath) =>
        Path.Combine(foreverPath, "Interface", "AddOns", "CommunitiesForever");

    public string PathFor(string foreverPath) => Path.Combine(AddonFolderOf(foreverPath), "Data", "Runs.lua");

    public AddonFolder Inspect(string foreverPath)
    {
        string addon = AddonFolderOf(foreverPath);
        if (!Directory.Exists(addon))
        {
            return AddonFolder.Missing;
        }

        if (!Directory.Exists(Path.Combine(addon, "Data")))
        {
            return AddonFolder.TooOld;
        }

        return File.Exists(PathFor(foreverPath)) ? AddonFolder.Ready : AddonFolder.NoFile;
    }

    public byte[]? Read(string foreverPath)
    {
        string path = PathFor(foreverPath);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public void Write(string foreverPath, byte[] content)
    {
        string path = PathFor(foreverPath);
        string temp = path + ".tmp";
        File.WriteAllBytes(temp, content);
        File.Move(temp, path, overwrite: true);
    }
}
