using System.ComponentModel;
using System.Diagnostics;
using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.Windows;

/// <summary>
/// WoW: Forever runs as Wow.exe inside its _forever_ folder; retail uses the same name, so the path decides.
/// Every Process object is disposed (CLAUDE.md rule 11).
/// </summary>
internal sealed class GameProcesses : IGameProcesses
{
    public bool IsRunning(string foreverPath)
    {
        string folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(foreverPath)) + Path.DirectorySeparatorChar;
        var processes = Process.GetProcessesByName("Wow");
        try
        {
            return processes.Any(p => PathOf(p)?.StartsWith(folder, StringComparison.OrdinalIgnoreCase) == true);
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static string? PathOf(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            // Exited, or not ours to inspect.
            return null;
        }
    }
}
