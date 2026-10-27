using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(CommunitiesForever.Upload.App.DesignChecks.TestAppBuilder))]

namespace CommunitiesForever.Upload.App.DesignChecks;

public static class TestAppBuilder
{
    // Real Skia rendering (not the headless stub) so frames can be captured as images. Motion off: a screenshot must
    // show the final look, never a transition halfway (a button fading in as it gets enabled).
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .AfterSetup(builder => ((App)builder.Instance!).ApplyMotion(animationsEnabled: false));
}
