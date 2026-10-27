using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CommunitiesForever.Upload.App.Tests.Main;
using CommunitiesForever.Upload.App.Tests.Setup;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.Core.Site;

namespace CommunitiesForever.Upload.App.DesignChecks;

/// <summary>Settings (board <c>settings</c>, 900 wide), tall so every section shows in one shot.</summary>
public sealed class SettingsScreenshotTests
{
    private const double TallHeight = 1080;

    [AvaloniaFact]
    public Task Connected() => Shot("settings", _ => { });

    [AvaloniaFact]
    public Task DisconnectConfirm() => Shot("settings-disconnect", vm => vm.AskDisconnectCommand.Execute(null));

    [AvaloniaFact]
    public Task NotConnected() => Shot("settings-not-connected", _ => { }, connected: false);

    private static async Task Shot(string name, Action<SettingsViewModel> arrange, bool connected = true)
    {
        // The board's two accounts: one watched, one found and switched off.
        var h = new MainHarness();
        h.Disk.AddAccount(MainHarness.Forever, MainHarness.First.Account, h.Now);
        h.Disk.AddAccount(MainHarness.Forever, MainHarness.Second.Account, null);
        h.Site.Identity = SiteResult.Ok(SetupHarness.Nico with { CreatedAt = new DateTimeOffset(2026, 10, 27, 20, 0, 0, TimeSpan.Zero) });
        if (!connected)
        {
            h.Tokens.Token = null;
        }

        var main = h.Create();
        var window = new MainWindow { DataContext = main, Height = TallHeight };
        window.Show();
        if (connected)
        {
            await SetupHarness.Until(() => main.Identity is not null);
        }

        main.OpenSettingsCommand.Execute(null);
        var vm = main.Settings!;
        await SetupHarness.Until(() => vm.Badge.Length > 0 && vm.Folders.Count == 2);
        arrange(vm);
        Dispatcher.UIThread.RunJobs();

        Screenshot.Matches(window, name);
        window.Close();
    }
}
