using System.Runtime.CompilerServices;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.App.Views;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;

namespace CommunitiesForever.Upload.App.Tests.Setup;

public sealed class SetupViewModelTests
{
    private readonly SetupHarness _harness = new();

    [AvaloniaFact]
    public async Task OpensTheBrowserAndShowsTheCode()
    {
        var vm = _harness.Create();

        _ = vm.StartAsync();
        await SetupHarness.Until(() => vm.Connect.IsWaiting);

        Assert.Equal(1, vm.Step);
        Assert.Equal("KXRT-7QMN", vm.Connect.UserCode);
        Assert.Equal(new Uri("https://wow.vestras.nl/uploader?code=KXRT-7QMN"), Assert.Single(_harness.Opened));
        Assert.Equal("Connect to wow.vestras.nl", vm.Connect.Heading);
        Assert.Equal(App.DefaultName, vm.Title);
        vm.Dispose();
    }

    [AvaloniaFact]
    public async Task AllowingOnTheSiteMovesOnAndScans()
    {
        _harness.Site.Polls.Enqueue(SiteResult.Ok(new TokenPoll(PollStatus.Granted, SetupHarness.Token)));
        var vm = _harness.Create();

        _ = vm.StartAsync();
        await SetupHarness.Until(() => vm.Connect.IsWaiting);
        _harness.Time.Advance(TimeSpan.FromSeconds(5));
        await SetupHarness.Until(() => vm.Step == 2 && vm.Find.Found);

        Assert.Equal(SetupHarness.Token, _harness.Tokens.Token);
        Assert.Equal("Connected as Nico#2201", vm.Step1Text);
        Assert.Equal("Doombringers Uploader", vm.Title);
        Assert.Equal("Scan complete · 1 install · 2 accounts", vm.Find.Summary);
        var accounts = Assert.Single(vm.Find.Installs).Accounts;
        Assert.True(accounts[0].IsChecked);
        Assert.Equal("CommunitiesForever.lua · saved today 21:12", accounts[0].Detail);
        Assert.False(accounts[1].IsChecked);
        Assert.Equal("Not found", accounts[1].Status);
        Assert.True(vm.Find.CanContinue);
        vm.Dispose();
    }

    [AvaloniaFact]
    public async Task WithATokenSetupStartsAtFindingTheGame()
    {
        _harness.Tokens.Token = SetupHarness.Token;
        var vm = _harness.Create();

        await vm.StartAsync();

        Assert.Equal(2, vm.Step);
        Assert.Empty(_harness.Opened);
        Assert.Equal(SetupHarness.Nico, vm.Identity);
        vm.Dispose();
    }

    [AvaloniaFact]
    public async Task StartWatchingSavesSettingsV1()
    {
        _harness.Tokens.Token = SetupHarness.Token;
        var vm = _harness.Create();
        bool finished = false;
        vm.Finished += (_, _) => finished = true;
        await vm.StartAsync();

        vm.Find.Installs[0].Accounts[1].IsChecked = true;
        vm.NextCommand.Execute(null);
        Assert.Equal("2 accounts watched", vm.Step2Text);
        vm.HowItRuns.StartWithWindows = false;
        vm.HowItRuns.Notifications = 1;
        vm.StartWatchingCommand.Execute(null);

        Assert.True(finished);
        var saved = Assert.IsType<SettingsLoad.Loaded>(new SettingsStore(_harness.SettingsFile).Load()).Settings;
        Assert.Equal(
            [new WatchedAccount(SetupHarness.Forever, "123456789#1"), new WatchedAccount(SetupHarness.Forever, "123456789#2")],
            saved.Accounts);
        Assert.False(saved.StartWithWindows);
        Assert.Equal(NotificationLevel.OnlyProblems, saved.Notifications);
        Assert.Null(saved.SiteUri);
        vm.Dispose();
    }

    [AvaloniaFact]
    public async Task NoAccountChosenBlocksNext()
    {
        _harness.Tokens.Token = SetupHarness.Token;
        var vm = _harness.Create();
        await vm.StartAsync();

        vm.Find.Installs[0].Accounts[0].IsChecked = false;

        Assert.False(vm.Find.CanContinue);
        vm.Dispose();
    }

    [AvaloniaFact]
    public async Task NothingOnDiskSaysSo()
    {
        _harness.Tokens.Token = SetupHarness.Token;
        _harness.Disk.Clear();
        var vm = _harness.Create();

        await vm.StartAsync();

        Assert.True(vm.Find.NothingFound);
        Assert.False(vm.Find.CanContinue);
        vm.Dispose();
    }

    [AvaloniaFact]
    public async Task UnreachableSiteOffersTryAgain()
    {
        _harness.Site.DeviceCode = new SiteFailure(SiteError.Offline);
        var vm = _harness.Create();

        await vm.StartAsync();

        Assert.Equal(ConnectState.Unreachable, vm.Connect.State);
        Assert.Equal("Can't reach wow.vestras.nl.", vm.Connect.Problem);
        vm.Dispose();
    }

    [AvaloniaFact]
    public async Task PastedTokenErrorsShowInline()
    {
        var vm = _harness.Create();
        vm.Connect.PasteText = "hello";

        await vm.Connect.UsePastedTokenCommand.ExecuteAsync(null);
        Assert.Equal("That isn't an upload token. It starts with cfu_.", vm.Connect.PasteError);

        _harness.Site.Identity = new SiteFailure(SiteError.InvalidToken, "invalid_token", 401);
        vm.Connect.PasteText = SetupHarness.Token.Reveal();
        await vm.Connect.UsePastedTokenCommand.ExecuteAsync(null);
        Assert.StartsWith("The site doesn't know this token", vm.Connect.PasteError, StringComparison.Ordinal);
        Assert.Null(_harness.Tokens.Token);
        vm.Dispose();
    }

    [AvaloniaFact]
    public async Task ReopenedSetupKeepsEarlierChoices()
    {
        _harness.Tokens.Token = SetupHarness.Token;
        var vm = _harness.Create();
        vm.Existing = new AppSettings { CloseHidesToTray = false, Accounts = [new WatchedAccount(SetupHarness.Forever, "123456789#2")] };

        await vm.StartAsync();

        Assert.False(vm.HowItRuns.CloseHidesToTray);
        Assert.False(vm.Find.Installs[0].Accounts[0].IsChecked);
        Assert.True(vm.Find.Installs[0].Accounts[1].IsChecked);
        vm.Dispose();
    }

    [Theory]
    [InlineData("2026-10-27T21:12:00Z", "today 21:12")]
    [InlineData("2026-10-26T08:05:00Z", "yesterday 08:05")]
    [InlineData("2026-10-20T19:40:00Z", "20 Oct 19:40")]
    public void SaveTimesReadNaturally(string saved, string expected) =>
        Assert.Equal(expected, FindGameStepViewModel.SavedAt(DateTimeOffset.Parse(saved, System.Globalization.CultureInfo.InvariantCulture), new DateTimeOffset(2026, 10, 27, 21, 30, 0, TimeSpan.Zero)));

    [AvaloniaFact]
    public async Task ClosedSetupWindowIsCollected()
    {
        // Long-running app (CLAUDE.md rule 11): neither the window nor its view model may stay alive.
        var (window, vm) = OpenAndClose(_harness);

        // Closing cancels connecting; that unwinds asynchronously (on slow machines it takes a moment), so collect
        // until it has, for at most two seconds. A real leak stays alive the whole time.
        for (int i = 0; i < 100 && (window.IsAlive || vm.IsAlive); i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.False(window.IsAlive);
        Assert.False(vm.IsAlive);
    }

    [AvaloniaFact]
    public void MotionStylesLoad()
    {
        var app = (App)Avalonia.Application.Current!;

        app.ApplyMotion(animationsEnabled: true);
        var window = new SetupWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Close();
        app.ApplyMotion(animationsEnabled: false);
        app.ApplyMotion(animationsEnabled: true);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Window, WeakReference ViewModel) OpenAndClose(SetupHarness harness)
    {
        var vm = harness.Create();
        var window = new SetupWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Close();
        return (new WeakReference(window), new WeakReference(vm));
    }
}
