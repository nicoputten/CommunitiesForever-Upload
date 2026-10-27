using System.Runtime.CompilerServices;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.App.Tests.Main;

public sealed class MainViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 21, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(AccountState.Watching, "● Watching", "All caught up", HeroActions.UploadNow | HeroActions.OpenSite)]
    [InlineData(AccountState.Playing, "● Playing", "Waiting for the game to save", HeroActions.UploadNow | HeroActions.OpenSite)]
    [InlineData(AccountState.ReadyToUpload, "● Ready to upload", "The game saved the file", HeroActions.UploadNow | HeroActions.Skip)]
    [InlineData(AccountState.Uploading, "● Uploading", "Sending to wow.vestras.nl", HeroActions.None)]
    [InlineData(AccountState.NotAllowed, "● Not allowed", "The site doesn't take uploads from you right now", HeroActions.TryNow)]
    [InlineData(AccountState.NotConnected, "● Not connected", "The site doesn't accept this uploader anymore", HeroActions.Reconnect)]
    [InlineData(AccountState.NotWatching, "● Not watching", "Where is World of Warcraft?", HeroActions.FindGame)]
    public void EveryStateHasItsHeader(AccountState state, string status, string title, HeroActions actions)
    {
        var hero = Hero.For(new AccountStatus(MainHarness.First, state), null, Now, TimeZoneInfo.Utc, "wow.vestras.nl", null);

        Assert.Equal(status, hero.Status);
        Assert.Equal(title, hero.Title);
        Assert.Equal(actions, hero.Actions);
    }

    [Fact]
    public void SettlingCountsDown()
    {
        var hero = Hero.For(new AccountStatus(MainHarness.First, AccountState.Settling, SettlesAt: Now.AddSeconds(3.2)), null, Now, TimeZoneInfo.Utc, "wow.vestras.nl", null);

        Assert.Equal("Game saved: uploading in 4 s", hero.Title);
        Assert.Equal(HeroActions.UploadNow | HeroActions.Skip, hero.Actions);
    }

    [Fact]
    public void OfflineSaysWhenItTriesAgain()
    {
        var hero = Hero.For(new AccountStatus(MainHarness.First, AccountState.WaitingToSend, NextTryAt: Now.AddMinutes(1)), null, Now, TimeZoneInfo.Utc, "wow.vestras.nl", null);

        Assert.Equal("Can't reach wow.vestras.nl", hero.Title);
        Assert.EndsWith("Next try in 1 min.", hero.Line, StringComparison.Ordinal);
        Assert.True(hero.IsWarn);
    }

    [Fact]
    public void NewerFormatNamesTheFormat()
    {
        var last = MainHarness.Upload(43, Now.AddMinutes(-5), UploadStatus.Rejected, "Newer addon: format 3", rejection: "newer_format", schema: 3);
        var hero = Hero.For(new AccountStatus(MainHarness.First, AccountState.NotUploaded, Rejection: "newer_format", SchemaVersion: 3, LastUpload: last), null, Now, TimeZoneInfo.Utc, "wow.vestras.nl", null);

        Assert.Equal("The site can't read this file yet", hero.Title);
        Assert.StartsWith("Your addon writes data format 3; the site can't read it yet.", hero.Line, StringComparison.Ordinal);
        Assert.Equal(last.ReportUrl, hero.Report);
    }

    [Fact]
    public void RecentUploadShowsForAnHour()
    {
        var last = MainHarness.Upload(42, Now.AddMinutes(-16), UploadStatus.Ok, "5 levels gained · 4 skill gains", ["Ves 37 → 39", "Lia 35 → 37"]);

        var recent = Hero.For(new AccountStatus(MainHarness.First, AccountState.Watching, LastUpload: last), null, Now, TimeZoneInfo.Utc, "wow.vestras.nl", null);
        var later = Hero.For(new AccountStatus(MainHarness.First, AccountState.Watching, LastUpload: last), null, Now.AddHours(1), TimeZoneInfo.Utc, "wow.vestras.nl", null);

        Assert.Equal("● Uploaded 21:14", recent.Status);
        Assert.Equal("5 levels gained", recent.Title);
        Assert.Equal("Ves 37 → 39 · Lia 35 → 37", recent.Line);
        Assert.True(recent.ShowReport);
        Assert.Equal("All caught up", later.Title);
        Assert.Equal("Last upload today at 21:14", later.Line);
    }

    [AvaloniaFact]
    public void ShowsTheMostUrgentAccountAndEveryRow()
    {
        var h = new MainHarness(MainHarness.First, MainHarness.Second);
        h.BoardHistory(MainHarness.First);
        var vm = h.Create();
        vm.Attach();

        h.Status(MainHarness.First, AccountState.Watching);
        h.Status(MainHarness.Second, AccountState.WaitingToSend, nextTryAt: h.Now.AddMinutes(1));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Can't reach wow.vestras.nl", vm.Hero.Title);
        Assert.StartsWith("123456789#2 · ", vm.Hero.Line, StringComparison.Ordinal);
        Assert.Equal(2, vm.Accounts.Count);
        Assert.Equal("123456789#1", vm.Accounts[0].Name);
        Assert.Equal("Uploaded today 21:14", vm.Accounts[0].Line);
        Assert.EndsWith("Saved by the game today 21:12 · 48 KB", vm.Accounts[0].Details, StringComparison.Ordinal);
        Assert.StartsWith(@"C:\Program Files (x86)\World of Warcraft\_forever_\WTF\Account\123456789#1\SavedVariables\CommunitiesForever.lua", vm.Accounts[0].Details, StringComparison.Ordinal);
        Assert.Equal("Up to date", vm.Accounts[0].Badge);
        Assert.Equal("Waiting to send", vm.Accounts[1].Badge);
        Assert.Equal(
            ["Today 21:14", "Today 19:02", "Fri 23 Oct, 23:40", "Wed 21 Oct, 22:05"],
            vm.Recent.Select(r => r.When));
        Assert.Equal(Tone.Dim, vm.Recent[1].Tone);
        vm.Dispose();
    }

    [AvaloniaFact]
    public async Task ShowsWhoIsConnected()
    {
        var h = new MainHarness();
        var vm = h.Create();

        vm.Attach();
        await Tests.Setup.SetupHarness.Until(() => vm.Identity is not null);

        Assert.Equal("Doombringers Uploader", vm.Title);
        Assert.Equal("Connected as Nico#2201 · wow.vestras.nl", vm.ConnectedText);
        Assert.True(vm.StartsWithWindows);
        vm.Dispose();
    }

    [AvaloniaFact]
    public void ActionsReachTheLoopAndTheBrowser()
    {
        var h = new MainHarness();
        h.BoardHistory(MainHarness.First);
        var vm = h.Create();
        vm.Attach();
        h.Status(MainHarness.First, AccountState.Settling, settlesAt: h.Now.AddSeconds(4));
        Dispatcher.UIThread.RunJobs();

        vm.UploadNowCommand.Execute(null);
        vm.SkipCommand.Execute(null);
        vm.OpenSiteCommand.Execute(null);
        vm.OpenHistoryCommand.Execute(null);
        vm.OpenRecentCommand.Execute(vm.Recent[0]);

        Assert.Equal(["UploadNow 123456789#1", "Skip 123456789#1"], h.Loop.Calls);
        Assert.Equal(
            [new Uri("https://wow.vestras.nl/"), new Uri("https://wow.vestras.nl/upload"), new Uri("https://wow.vestras.nl/upload/42")],
            h.Opened);
        vm.Dispose();
    }

    [AvaloniaFact]
    public void ReconnectAndChangeAskTheApp()
    {
        var h = new MainHarness();
        var vm = h.Create();
        var asked = new List<string>();
        vm.ReconnectRequested += (_, _) => asked.Add("reconnect");

        vm.ReconnectCommand.Execute(null);
        vm.ChangeAccountsCommand.Execute(null);

        Assert.Equal(["reconnect"], asked);
        Assert.Equal(SettingsSection.GameFolders, vm.Settings?.Section);
        Assert.Equal("CommunitiesForever Uploader · Settings", vm.Title);
        vm.Dispose();
    }

    [AvaloniaFact]
    public async Task ScanAgainFollowsTheGameToItsNewFolder()
    {
        var h = new MainHarness();
        h.Disk.AddAccount(@"D:\Games\World of Warcraft\_forever_", "123456789#1", h.Now);
        var vm = h.Create();
        vm.Attach();
        h.Status(MainHarness.First, AccountState.NotWatching);
        Dispatcher.UIThread.RunJobs();
        bool changed = false;
        vm.AccountsChanged += (_, _) => changed = true;

        await vm.ScanAgainCommand.ExecuteAsync(null);

        Assert.True(changed);
        Assert.False(vm.IsSettingsOpen);
        var saved = Assert.IsType<SettingsLoad.Loaded>(new SettingsStore(h.SettingsFile).Load()).Settings;
        Assert.Equal(@"D:\Games\World of Warcraft\_forever_", Assert.Single(saved.Accounts).InstallPath);
        vm.Dispose();
    }

    [AvaloniaFact]
    public async Task ScanAgainFindingNothingOpensGameFolders()
    {
        var h = new MainHarness();
        var vm = h.Create();
        vm.Attach();
        h.Status(MainHarness.First, AccountState.NotWatching);
        Dispatcher.UIThread.RunJobs();

        await vm.ScanAgainCommand.ExecuteAsync(null);

        Assert.Equal(SettingsSection.GameFolders, vm.Settings?.Section);
        vm.Dispose();
    }

    [AvaloniaFact]
    public void ClosedMainWindowIsCollected()
    {
        // Long-running app (CLAUDE.md rule 11): the loop outlives the window and must not keep it alive.
        var h = new MainHarness();
        var (window, vm) = OpenAndClose(h);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(window.IsAlive);
        Assert.False(vm.IsAlive);
        h.Loop.Changed();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Window, WeakReference ViewModel) OpenAndClose(MainHarness h)
    {
        var vm = h.Create();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Close();
        return (new WeakReference(window), new WeakReference(vm));
    }
}
