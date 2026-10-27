using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(CommunitiesForever.Upload.App.DesignChecks.TestAppBuilder))]

namespace CommunitiesForever.Upload.App.DesignChecks;

public static class TestAppBuilder
{
    // Real Skia rendering (not the headless stub) so frames can be captured as images.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
