using Avalonia.Headless.XUnit;
using CommunitiesForever.Upload.App.Tests.Setup;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;

namespace CommunitiesForever.Upload.App.Tests.Main;

/// <summary>The Watched accounts and Connect windows of the main window (UP-07).</summary>
public sealed class DialogTests
{
    private readonly SetupHarness _harness = new();

    [AvaloniaFact]
    public async Task WatchedAccountsStartsWithTheCurrentChoiceAndSaves()
    {
        var store = new SettingsStore(_harness.SettingsFile);
        store.Save(new AppSettings { StartWithWindows = false, Accounts = [new WatchedAccount(SetupHarness.Forever, "123456789#2")] });
        var vm = new WatchedAccountsViewModel(new GameScanner(_harness.Disk, new Roots(_harness.Disk)), store, _harness.Time);
        bool saved = false;
        vm.Saved += (_, _) => saved = true;

        await vm.StartAsync();
        Assert.False(vm.Find.Installs[0].Accounts[0].IsChecked);
        Assert.True(vm.Find.Installs[0].Accounts[1].IsChecked);
        vm.Find.Installs[0].Accounts[0].IsChecked = true;
        vm.SaveCommand.Execute(null);

        Assert.True(saved);
        var settings = Assert.IsType<SettingsLoad.Loaded>(store.Load()).Settings;
        Assert.Equal(2, settings.Accounts.Count);
        Assert.False(settings.StartWithWindows);
    }

    [AvaloniaFact]
    public async Task ConnectWindowSaysWhenConnected()
    {
        _harness.Site.Polls.Enqueue(SiteResult.Ok(new TokenPoll(PollStatus.Granted, SetupHarness.Token)));
        using var vm = new ConnectWindowViewModel(
            new ConnectFlow(_harness.Site, _harness.Tokens, new NoBrowser(), new Device(), _harness.Time),
            new SiteOptions(new Uri("https://wow.vestras.nl/"), "1.0.0"));
        bool connected = false;
        vm.Connected += (_, _) => connected = true;

        _ = vm.StartAsync();
        await SetupHarness.Until(() => vm.Connect.IsWaiting);
        _harness.Time.Advance(TimeSpan.FromSeconds(5));
        await SetupHarness.Until(() => connected);

        Assert.Equal(SetupHarness.Token, _harness.Tokens.Token);
        Assert.Equal("Connected as Nico#2201", vm.Connect.ConnectedText);
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
