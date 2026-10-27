using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CommunitiesForever.Upload.App.Tests.Main;
using CommunitiesForever.Upload.App.Tests.Setup;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Updates;

namespace CommunitiesForever.Upload.App.DesignChecks;

/// <summary>Settings (board <c>settings</c>, 900 wide), tall so every section shows in one shot.</summary>
public sealed class SettingsScreenshotTests
{
    private const double TallHeight = 1400;

    [AvaloniaFact]
    public Task Connected() => Shot("settings", _ => { });

    [AvaloniaFact]
    public Task DisconnectConfirm() => Shot("settings-disconnect", vm => vm.AskDisconnectCommand.Execute(null));

    [AvaloniaFact]
    public Task NotConnected() => Shot("settings-not-connected", _ => { }, connected: false);

    /// <summary>Board settings-updates: a new version downloaded and waiting.</summary>
    [AvaloniaFact]
    public Task UpdateReady() => Shot("settings-updates", _ => { }, ready: true);

    private static async Task Shot(string name, Action<SettingsViewModel> arrange, bool connected = true, bool ready = false)
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

        h.Updates.Site.Status = ready
            ? SiteResult.Ok(new UpdateStatus("1.1.0", null, null, new Uri("https://wow.vestras.nl/uploader/changes#1.1.0")))
            : SiteResult.Ok(new UpdateStatus("1.0.3", new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero), null, null));
        if (ready)
        {
            h.Updates.Updates.Newer = new UpdateOffer("1.1.0", 6_600_000, "Fixes a missed upload after long sessions · shows planned runs");
        }

        await h.Updates.Checker.CheckNowAsync();

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
