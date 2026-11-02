using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunitiesForever.Upload.Core.Uploading;
using Microsoft.Win32.SafeHandles;

namespace CommunitiesForever.Upload.Windows;

/// <summary>
/// WoW runs as Wow.exe inside its version folder (_forever_, _retail_); other clients use names such as WowClassic.exe,
/// so any process whose name starts with "Wow" counts, and the path decides which install it is. The path is read with
/// the limited query right, which also works for a game started as administrator. Every Process object is disposed
/// (CLAUDE.md rule 11).
/// </summary>
internal sealed partial class GameProcesses : IGameProcesses
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    public bool IsRunning(string foreverPath)
    {
        string folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(foreverPath)) + Path.DirectorySeparatorChar;
        var processes = Process.GetProcesses();
        try
        {
            return processes.Any(p => IsGameName(p.ProcessName) && ImagePath(p.Id)?.StartsWith(folder, StringComparison.OrdinalIgnoreCase) == true);
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    internal static bool IsGameName(string processName) => processName.StartsWith("Wow", StringComparison.OrdinalIgnoreCase);

    /// <summary>The process's exe, or null when it has exited or Windows won't say.</summary>
    internal static string? ImagePath(int processId)
    {
        using var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle.IsInvalid)
        {
            return null;
        }

        unsafe
        {
            char* buffer = stackalloc char[1024];
            int size = 1024;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? new string(buffer, 0, size) : null;
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, char* buffer, ref int size);
}
