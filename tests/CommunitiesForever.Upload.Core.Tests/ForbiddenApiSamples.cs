using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CommunitiesForever.Upload.Core.Tests;

/// <summary>Never called: gives ArchitectureTests.RulesCatchForbiddenApis something to find.</summary>
internal static class ForbiddenApiSamples
{
    public static bool UsesFile(string path) => File.Exists(path);

    public static int UsesProcess() => Process.GetCurrentProcess().Id;

    [DllImport("kernel32.dll")]
    internal static extern uint GetTickCount();
}
