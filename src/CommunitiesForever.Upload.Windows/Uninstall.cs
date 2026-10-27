namespace CommunitiesForever.Upload.Windows;

/// <summary>
/// What the app leaves in Windows, removed when it is uninstalled (UP-10, Velopack's before-uninstall hook): the Run
/// entry, the notification sender and link scheme, the Start menu shortcut and the upload token. Settings, history and
/// logs stay, so a reinstall picks up where it left off.
/// </summary>
public static class Uninstall
{
    public static void CleanUp() => CleanUp(AppPaths.DataDirectory, StartMenuShortcut.DefaultPath);

    internal static void CleanUp(string dataDirectory, string shortcutPath, string classesRoot = @"Software\Classes", string runKey = RegistryAutostart.RunKey)
    {
        // Each step on its own: one that fails must not keep the others from running.
        Try(() => new RegistryAutostart("", runKey).Apply(false));
        Try(() => ShellRegistration.Unregister(classesRoot));
        Try(() => StartMenuShortcut.Remove(shortcutPath));
        Try(() => new DpapiTokenStore(dataDirectory).Delete());
    }

    private static void Try(Action step)
    {
        try
        {
            step();
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Left behind; harmless without the app.
        }
    }
}
