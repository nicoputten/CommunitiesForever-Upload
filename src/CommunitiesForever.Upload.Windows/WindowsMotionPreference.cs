using System.Runtime.InteropServices;
using CommunitiesForever.Upload.Core;

namespace CommunitiesForever.Upload.Windows;

/// <summary>Reads Windows' "Animation effects" setting.</summary>
internal sealed partial class WindowsMotionPreference : IMotionPreference
{
    private const uint SpiGetClientAreaAnimation = 0x1042;

    public bool AnimationsEnabled =>
        !SystemParametersInfo(SpiGetClientAreaAnimation, 0, out int enabled, 0) || enabled != 0;

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint param, out int value, uint winIni);
}
