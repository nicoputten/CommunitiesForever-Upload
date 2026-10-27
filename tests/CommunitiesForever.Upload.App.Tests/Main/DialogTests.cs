using Avalonia.Headless.XUnit;
using CommunitiesForever.Upload.App.Tests.Setup;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Site;

namespace CommunitiesForever.Upload.App.Tests.Main;

/// <summary>The Connect window of the main window (UP-07).</summary>
public sealed class DialogTests
{
    private readonly SetupHarness _harness = new();

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
