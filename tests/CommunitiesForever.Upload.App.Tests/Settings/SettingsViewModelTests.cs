using System.Runtime.CompilerServices;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CommunitiesForever.Upload.App.Tests.Main;
using CommunitiesForever.Upload.App.Tests.Setup;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;

namespace CommunitiesForever.Upload.App.Tests.Settings;

/// <summary>Settings (UP-09): everything saves right away and the app hears about it.</summary>
public sealed class SettingsViewModelTests
{
    private static readonly WatchedAccount Found = new(MainHarness.Forever, "123456789#2");

    [AvaloniaFact]
    public async Task OpensWithTheSavedSettings()
    {
        var h = new MainHarness();
        new SettingsStore(h.SettingsFile).Save(new AppSettings
        {
            Accounts = [MainHarness.First],
            StartWithWindows = false,
            CloseHidesToTray = false,
            Notifications = NotificationLevel.OnlyProblems,
        });
        var vm = h.CreateSettings();

        await vm.OpenAsync();

        Assert.True(vm.UploadAutomatically);
        Assert.False(vm.StartWithWindows);
        Assert.False(vm.CloseHidesToTray);
        Assert.Equal(1, vm.Notifications);
        Assert.Equal(0, vm.Pause);
        Assert.Equal("1.0.0", vm.Version);
    }

    [AvaloniaFact]
    public async Task SwitchesSaveRightAway()
    {
        var h = new MainHarness();
        var vm = h.CreateSettings();
        await vm.OpenAsync();
        var heard = new List<AppSettings>();
        vm.Changed += (_, s) => heard.Add(s);

        vm.StartWithWindows = false;
        vm.CloseHidesToTray = false;
        vm.UploadAutomatically = false;
        vm.Notifications = 2;

        var saved = Saved(h);
        Assert.False(saved.StartWithWindows);
        Assert.False(saved.CloseHidesToTray);
        Assert.False(saved.UploadAutomatically);
        Assert.Equal(NotificationLevel.Off, saved.Notifications);
        Assert.Equal([MainHarness.First], saved.Accounts);
        Assert.Equal(4, heard.Count);
        Assert.Equal(saved, heard[^1]);
    }

    [AvaloniaFact]
    public async Task PauseIsTheTraysPause()
    {
        var h = new MainHarness();
        var vm = h.CreateSettings();
        await vm.OpenAsync();

        vm.Pause = 1;
        vm.Pause = 2;
        vm.Pause = 0;

        // 27 Oct 21:30 UTC: an hour, then until 06:00 the next morning.
        Assert.Equal(["Pause 2026-10-27 22:30", "Pause 2026-10-28 06:00", "EndPause"], h.Loop.Calls);
    }

    [AvaloniaFact]
    public async Task OpensShowingAPauseFromTheTray()
    {
        var h = new MainHarness();
        h.Loop.PausedUntil = h.Now.AddMinutes(40);
        var vm = h.CreateSettings();

        await vm.OpenAsync();

        Assert.Equal(1, vm.Pause);
        Assert.Empty(h.Loop.Calls);
    }

    [AvaloniaFact]
    public async Task GameFoldersListWatchedThenFound()
    {
        var h = new MainHarness();
        h.Disk.AddAccount(MainHarness.Forever, MainHarness.First.Account, h.Now);
        h.Disk.AddAccount(MainHarness.Forever, Found.Account, null);
        var vm = h.CreateSettings();

        await vm.OpenAsync();

        Assert.Equal([(MainHarness.First.Account, true), (Found.Account, false)], vm.Folders.Select(f => (f.Name, f.IsWatched)));
        Assert.All(vm.Folders, f => Assert.False(f.IsMissing));
        Assert.EndsWith(@"WTF\Account\123456789#1\SavedVariables", vm.Folders[0].Folder, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task SwitchingAFolderSavesTheAccounts()
    {
        var h = new MainHarness();
        h.Disk.AddAccount(MainHarness.Forever, MainHarness.First.Account, h.Now);
        h.Disk.AddAccount(MainHarness.Forever, Found.Account, null);
        var vm = h.CreateSettings();
        await vm.OpenAsync();

        vm.Folders[1].IsWatched = true;
        Assert.Equal([MainHarness.First, Found], Saved(h).Accounts);

        vm.Folders[0].IsWatched = false;
        Assert.Equal([Found], Saved(h).Accounts);
    }

    [AvaloniaFact]
    public async Task AWatchedAccountThatIsGoneIsNotFound()
    {
        var h = new MainHarness();
        var vm = h.CreateSettings();

        await vm.OpenAsync();

        var row = Assert.Single(vm.Folders);
        Assert.True(row.IsMissing);
        Assert.True(row.IsWatched);
    }

    [AvaloniaFact]
    public async Task ConnectionSaysWhoAndWhen()
    {
        var h = new MainHarness();
        h.Site.Identity = SiteResult.Ok(SetupHarness.Nico with { CreatedAt = new DateTimeOffset(2026, 10, 27, 20, 0, 0, TimeSpan.Zero) });
        var vm = h.CreateSettings();

        await vm.OpenAsync();

        Assert.True(vm.IsConnected);
        Assert.Equal("wow.vestras.nl", vm.SiteHost);
        Assert.Equal("Connected as Nico#2201 · upload-only token · created 27 Oct", vm.ConnectionLine);
        Assert.Equal(("Working", Tone.Fresh), (vm.Badge, vm.BadgeTone));
    }

    [AvaloniaTheory]
    [InlineData(SiteError.InvalidToken, "Token revoked")]
    [InlineData(SiteError.Offline, "Can't reach")]
    public async Task ConnectionProblemsShowInTheBadge(SiteError error, string badge)
    {
        var h = new MainHarness();
        h.Site.Identity = new SiteFailure(error);
        var vm = h.CreateSettings();

        await vm.OpenAsync();

        Assert.True(vm.IsConnected);
        Assert.Equal((badge, Tone.Warn), (vm.Badge, vm.BadgeTone));
    }

    [AvaloniaFact]
    public async Task DisconnectAsksFirstThenForgetsTheToken()
    {
        var h = new MainHarness();
        var vm = h.CreateSettings();
        await vm.OpenAsync();
        bool disconnected = false;
        vm.Disconnected += (_, _) => disconnected = true;

        vm.AskDisconnectCommand.Execute(null);
        Assert.True(vm.IsConfirmingDisconnect);
        vm.CancelDisconnectCommand.Execute(null);
        Assert.False(vm.IsConfirmingDisconnect);
        Assert.NotNull(h.Tokens.Token);

        vm.AskDisconnectCommand.Execute(null);
        await vm.DisconnectCommand.ExecuteAsync(null);

        Assert.True(disconnected);
        Assert.Null(h.Tokens.Token);
        Assert.True(vm.IsNotConnected);
        Assert.Equal("Not connected", vm.Badge);
    }

    [AvaloniaFact]
    public async Task MainWindowAppliesSettingsAndRestartsWatchingOnlyWhenNeeded()
    {
        var h = new MainHarness();
        var main = h.Create();
        main.Attach();
        var heard = new List<string>();
        main.SettingsChanged += (_, _) => heard.Add("settings");
        main.AccountsChanged += (_, _) => heard.Add("restart");
        main.OpenSettingsCommand.Execute(null);
        var settings = Assert.IsType<SettingsViewModel>(main.Settings);
        await SetupHarness.Until(() => settings.Badge.Length > 0);

        settings.StartWithWindows = false;
        Assert.False(main.StartsWithWindows);
        settings.Notifications = 1;
        settings.UploadAutomatically = false;

        Assert.Equal(["settings", "settings", "settings", "restart"], heard);
        settings.BackCommand.Execute(null);
        Assert.False(main.IsSettingsOpen);
        main.Dispose();
    }

    [AvaloniaFact]
    public void ClosedSettingsPageIsCollected()
    {
        // Long-running app (CLAUDE.md rule 11): opening and leaving the settings page leaves nothing behind.
        var h = new MainHarness();
        var main = h.Create();
        var window = new MainWindow { DataContext = main };
        window.Show();
        var page = OpenAndGoBack(main);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        for (int i = 0; i < 10 && page.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Dispatcher.UIThread.RunJobs();
        }

        Assert.False(page.IsAlive);
        window.Close();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference OpenAndGoBack(MainViewModel main)
    {
        main.OpenSettingsCommand.Execute(null);
        var page = main.Settings!;
        Dispatcher.UIThread.RunJobs();
        page.BackCommand.Execute(null);
        return new WeakReference(page);
    }

    private static AppSettings Saved(MainHarness h) =>
        Assert.IsType<SettingsLoad.Loaded>(new SettingsStore(h.SettingsFile).Load()).Settings;
}
