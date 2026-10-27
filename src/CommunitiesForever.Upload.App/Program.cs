using Avalonia;
using CommunitiesForever.Upload.Windows;

namespace CommunitiesForever.Upload.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
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
