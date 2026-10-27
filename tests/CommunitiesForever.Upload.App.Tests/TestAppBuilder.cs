using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(CommunitiesForever.Upload.App.Tests.TestAppBuilder))]

namespace CommunitiesForever.Upload.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
