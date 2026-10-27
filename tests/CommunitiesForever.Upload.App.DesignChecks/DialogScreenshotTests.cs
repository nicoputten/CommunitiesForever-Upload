using Avalonia.Headless.XUnit;
using CommunitiesForever.Upload.App.Tests.Setup;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.App.Views;
using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;

namespace CommunitiesForever.Upload.App.DesignChecks;

/// <summary>The main window's Watched accounts and Connect windows (setup board steps 2 and 1 on their own).</summary>
public sealed class DialogScreenshotTests
{
    private readonly SetupHarness _harness = new();

    [AvaloniaFact]
    public async Task WatchedAccounts()
    {
        var store = new SettingsStore(_harness.SettingsFile);
        store.Save(new AppSettings { Accounts = [new WatchedAccount(SetupHarness.Forever, "123456789#1")] });
        var vm = new WatchedAccountsViewModel(new GameScanner(_harness.Disk, new Roots(_harness.Disk)), store, _harness.Time);
        var window = new WatchedAccountsWindow { DataContext = vm };
        window.Show();
        await SetupHarness.Until(() => vm.Find.Found);

        Screenshot.Matches(window, "dialog-watched-accounts");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Connect()
    {
        var vm = new ConnectWindowViewModel(
            new ConnectFlow(_harness.Site, _harness.Tokens, new NoBrowser(), new Device(), _harness.Time),
            new SiteOptions(new Uri("https://wow.vestras.nl/"), "1.0.0"));
        var window = new ConnectWindow { DataContext = vm };
        window.Show();
        await SetupHarness.Until(() => vm.Connect.IsWaiting);

        Screenshot.Matches(window, "dialog-connect");
        window.Close();
    }

    private sealed class Roots(MemoryDisk disk) : IInstallHints
    {
        public IEnumerable<string> CandidateFolders() => disk.Roots;
    }

    private sealed class NoBrowser : IBrowser
    {
        public void Open(Uri uri)
        {
        }
    }

    private sealed class Device : IDeviceInfo
    {
        public string DeviceName => "DESKTOP-NICO";
    }
}
