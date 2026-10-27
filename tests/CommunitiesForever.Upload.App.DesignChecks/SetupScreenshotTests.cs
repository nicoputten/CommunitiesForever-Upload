using Avalonia.Headless.XUnit;
using CommunitiesForever.Upload.App.Tests.Setup;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.App.Views;
using CommunitiesForever.Upload.Core.Site;

namespace CommunitiesForever.Upload.App.DesignChecks;

/// <summary>The setup window (board <c>setup</c>, 760 × 560) in every state, over the setup fakes.</summary>
public sealed class SetupScreenshotTests
{
    private readonly SetupHarness _harness = new();

    [AvaloniaFact]
    public async Task ConnectWaiting()
    {
        var (window, vm) = Open();
        await SetupHarness.Until(() => vm.Connect.IsWaiting);

        Screenshot.Matches(window, "setup-connect");
        window.Close();
    }

    [AvaloniaFact]
    public async Task ConnectPasteToken()
    {
        var (window, vm) = Open();
        await SetupHarness.Until(() => vm.Connect.IsWaiting);
        vm.Connect.IsPasteOpen = true;
        vm.Connect.PasteText = "hello";
        await vm.Connect.UsePastedTokenCommand.ExecuteAsync(null);
        await SetupHarness.Until(() => vm.Connect.PasteError is not null);

        Screenshot.Matches(window, "setup-connect-paste");
        window.Close();
    }

    [AvaloniaFact]
    public async Task ConnectUnreachable()
    {
        _harness.Site.DeviceCode = new SiteFailure(SiteError.Offline);
        var (window, vm) = Open();
        await SetupHarness.Until(() => vm.Connect.State == ConnectState.Unreachable);

        Screenshot.Matches(window, "setup-connect-unreachable");
        window.Close();
    }

    [AvaloniaFact]
    public async Task FindFound()
    {
        _harness.Tokens.Token = SetupHarness.Token;
        var (window, vm) = Open();
        await SetupHarness.Until(() => vm.Find.Found && vm.Identity is not null);

        Screenshot.Matches(window, "setup-find");
        window.Close();
    }

    [AvaloniaFact]
    public async Task FindNothing()
    {
        _harness.Tokens.Token = SetupHarness.Token;
        _harness.Disk.Clear();
        var (window, vm) = Open();
        await SetupHarness.Until(() => vm.Find.NothingFound && vm.Identity is not null);

        Screenshot.Matches(window, "setup-find-nothing");
        window.Close();
    }

    [AvaloniaFact]
    public async Task HowItRuns()
    {
        _harness.Tokens.Token = SetupHarness.Token;
        var (window, vm) = Open();
        await SetupHarness.Until(() => vm.Find.Found && vm.Identity is not null);
        vm.NextCommand.Execute(null);
        await SetupHarness.Until(() => vm.IsStep3);

        Screenshot.Matches(window, "setup-run");
        window.Close();
    }

    private (SetupWindow Window, SetupViewModel ViewModel) Open()
    {
        var vm = _harness.Create();
        var window = new SetupWindow { DataContext = vm };
        window.Show();
        return (window, vm);
    }
}
