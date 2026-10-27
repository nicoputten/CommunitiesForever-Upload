using CommunitiesForever.Upload.Core.Notifications;
using Microsoft.Win32;

namespace CommunitiesForever.Upload.Windows;

/// <summary>Start with Windows: a value in the user's Run key that starts the app hidden in the tray (<c>--tray</c>).</summary>
public sealed class RegistryAutostart(string exePath, string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run") : IAutostart
{
    public const string TrayArgument = "--tray";

    private string Command => $"\"{exePath}\" {TrayArgument}";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKey);
            return key?.GetValue(AppIdentity.DisplayName) is string value && value == Command;
        }
    }

    public void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(runKey);
        if (enabled)
        {
            key.SetValue(AppIdentity.DisplayName, Command);
        }
        else
        {
            key.DeleteValue(AppIdentity.DisplayName, throwOnMissingValue: false);
        }
    }
}
