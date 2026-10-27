using System.Text.Json;
using CommunitiesForever.Upload.Core.Game;
using Microsoft.Win32;

namespace CommunitiesForever.Upload.Windows;

/// <summary>
/// Where World of Warcraft may be: Blizzard's registry keys, Battle.net's default install folder, then the usual folders
/// on every fixed drive. The scanner checks each for a <c>_forever_</c> folder.
/// </summary>
internal sealed class WindowsInstallHints : IInstallHints
{
    private const string GameFolder = "World of Warcraft";

    public IEnumerable<string> CandidateFolders()
    {
        foreach (string folder in Registry())
        {
            yield return folder;
        }

        foreach (string folder in BattleNet())
        {
            yield return folder;
        }

        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), GameFolder);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), GameFolder);
        foreach (string drive in FixedDrives())
        {
            yield return Path.Combine(drive, GameFolder);
            yield return Path.Combine(drive, "Games", GameFolder);
        }
    }

    private static List<string> Registry()
    {
        var folders = new List<string>();
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            try
            {
                using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                // InstallPath points at a version folder such as _retail_; the scanner looks next to it too.
                AddValue(folders, machine, @"SOFTWARE\Blizzard Entertainment\World of Warcraft", "InstallPath");
                AddValue(folders, machine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\World of Warcraft", "InstallLocation");
            }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                // No access to the registry: the other hints remain.
            }
        }

        return folders;
    }

    private static void AddValue(List<string> folders, RegistryKey root, string key, string name)
    {
        using var sub = root.OpenSubKey(key);
        if (sub?.GetValue(name) is string { Length: > 0 } value)
        {
            folders.Add(value);
        }
    }

    // Battle.net.config is Battle.net's own JSON settings; "DefaultInstallPath" is where it installs games.
    private static List<string> BattleNet()
    {
        string config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Battle.net", "Battle.net.config");
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(config));
            return [.. FindValues(json.RootElement, "DefaultInstallPath").Select(path => Path.Combine(path, GameFolder))];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    private static IEnumerable<string> FindValues(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(name) && property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() is { Length: > 0 } value)
                {
                    yield return value;
                }

                foreach (string nested in FindValues(property.Value, name))
                {
                    yield return nested;
                }
            }
        }
    }

    private static List<string> FixedDrives()
    {
        try
        {
            return DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).Select(d => d.RootDirectory.FullName).ToList();
        }
        catch (IOException)
        {
            return [];
        }
    }
}
