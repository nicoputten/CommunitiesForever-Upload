using Avalonia.Headless.XUnit;
using CommunitiesForever.Upload.App.Tests.Main;
using CommunitiesForever.Upload.Core.Game;
using Microsoft.Extensions.DependencyInjection;

namespace CommunitiesForever.Upload.App.Tests;

public sealed class RetailTestModeTests
{
    [Theory]
    [InlineData("")]
    [InlineData("--tray")]
    [InlineData("--site http://localhost:3000")]
    public void ForeverByDefault(string args) => Assert.Same(GameFolderOptions.Forever, App.GameFolder(Split(args)));

    [Theory]
    [InlineData("--retail")]
    [InlineData("--tray --RETAIL")]
    public void RetailWithTheFlag(string args) => Assert.Same(GameFolderOptions.RetailTest, App.GameFolder(Split(args)));

    [Fact]
    public void TheContainerCarriesTheChoice()
    {
        using var normal = App.ConfigureServices();
        using var retail = App.ConfigureServices(["--retail"]);

        Assert.False(normal.GetRequiredService<GameFolderOptions>().IsRetailTest);
        Assert.True(retail.GetRequiredService<GameFolderOptions>().IsRetailTest);
    }

    [AvaloniaFact]
    public void TitleSaysRetailTestMode()
    {
        var vm = new MainHarness().Create();

        Assert.DoesNotContain("Retail", vm.Title, StringComparison.Ordinal);
        vm.RetailTestMode = true;
        Assert.Equal(App.DefaultName + " · Retail test mode", vm.Title);
        vm.Dispose();
    }

    private static string[] Split(string args) => args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
