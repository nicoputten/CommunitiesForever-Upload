using CommunitiesForever.Upload.Core.Site;

namespace CommunitiesForever.Upload.Windows;

/// <summary>The computer name, as the site lists the uploader ("DESKTOP-NICO").</summary>
internal sealed class WindowsDeviceInfo : IDeviceInfo
{
    public string DeviceName => Environment.MachineName;
}
