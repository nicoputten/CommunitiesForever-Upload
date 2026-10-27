using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CommunitiesForever.Upload.Windows;

/// <summary>
/// Registers the app for this Windows user, at every start (idempotent): the notification sender (AppUserModelId, name
/// and icon) and the app link scheme that notification buttons use. UP-10 removes both on uninstall.
/// </summary>
public static partial class ShellRegistration
{
    public static void Register(string exePath, string iconPath, string classesRoot = @"Software\Classes")
    {
        using (var sender = Registry.CurrentUser.CreateSubKey($@"{classesRoot}\AppUserModelId\{AppIdentity.AppId}"))
        {
            sender.SetValue("DisplayName", AppIdentity.DisplayName);
            sender.SetValue("IconUri", iconPath);
        }

        using (var scheme = Registry.CurrentUser.CreateSubKey($@"{classesRoot}\{AppIdentity.Scheme}"))
        {
            scheme.SetValue("", $"URL:{AppIdentity.DisplayName}");
            scheme.SetValue("URL Protocol", "");
            using var command = scheme.CreateSubKey(@"shell\open\command");
            command.SetValue("", $"\"{exePath}\" \"%1\"");
        }

        // Toasts and the taskbar group under the same id.
        _ = SetCurrentProcessExplicitAppUserModelID(AppIdentity.AppId);
    }

    /// <summary>Removes the sender and the link scheme again (uninstall, UP-10).</summary>
    public static void Unregister(string classesRoot = @"Software\Classes")
    {
        Registry.CurrentUser.DeleteSubKeyTree($@"{classesRoot}\AppUserModelId\{AppIdentity.AppId}", throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree($@"{classesRoot}\{AppIdentity.Scheme}", throwOnMissingSubKey: false);
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SetCurrentProcessExplicitAppUserModelID(string appId);
}
