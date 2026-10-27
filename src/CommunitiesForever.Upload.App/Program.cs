using Avalonia;
using CommunitiesForever.Upload.Windows;
using Velopack;

namespace CommunitiesForever.Upload.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // First: the installer runs the app for its hooks (install, update, uninstall) and it ends here then;
        // a downloaded update goes in before the app starts (UP-10).
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => Uninstall.CleanUp())
            .Run();

        // One app per Windows user: a second start hands its arguments over and ends (UP-08).
        using var instance = new SingleInstance();
        if (!instance.IsFirst)
        {
            instance.Forward(args);
            return;
        }

        App.Instance = instance;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the designer previewer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
