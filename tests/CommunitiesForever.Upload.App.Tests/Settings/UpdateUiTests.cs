using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CommunitiesForever.Upload.App.Tests.Main;
using CommunitiesForever.Upload.App.Tray;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Updates;
using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.App.Tests.Settings;

/// <summary>Updates in the app (UP-10): Settings › About &amp; updates, the footer, the required-update banner and tray.</summary>
public sealed class UpdateUiTests
{
    private static readonly Uri Notes = new("https://wow.vestras.nl/uploader/changes#1.1.0");

    [AvaloniaFact]
    public async Task PanelFollowsTheBoardStates()
    {
        var h = new MainHarness();
        var updates = h.Updates;
        string host = "wow.vestras.nl";
        UpdatePanel Panel() => UpdatePanel.For(updates.Checker, h.Now, TimeZoneInfo.Utc, host, installWhenWowClosed: true);

        updates.Site.Status = SiteResult.Ok(new UpdateStatus("1.0.3", new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero), null, null));
        await updates.Checker.CheckNowAsync();
        Assert.Equal(("Version 1.0.3 · released 20 Oct", "● Up to date", Tone.Fresh), (Panel().VersionLine, Panel().Pill, Panel().PillTone));
        Assert.Equal("Automatic at start and every 24 hours · last check today 21:30", Panel().CheckLine);

        updates.Updates.CheckError = new HttpRequestException("offline");
        await updates.Checker.CheckNowAsync();
        Assert.Equal(("Couldn't check", "No connection to wow.vestras.nl. Tries again in 1 hour by itself.", UpdateButtons.TryAgain), (Panel().Pill, Panel().Line, Panel().Buttons));

        updates.Updates.CheckError = null;
        updates.Site.Status = SiteResult.Ok(new UpdateStatus("1.1.0", null, null, Notes));
        updates.Updates.Newer = new UpdateOffer("1.1.0", 6_600_000, "Fixes a missed upload after long sessions · shows planned runs");
        updates.Updates.DownloadGate = new TaskCompletionSource();
        var check = updates.Checker.CheckNowAsync();
        await UpdateHarnessUntil(() => updates.Checker.State == UpdateState.Downloading);
        Assert.Equal(("Downloading 1.1", "62% · 4.1 of 6.6 MB · uploads keep working"), (Panel().Pill, Panel().Line));
        Assert.Equal(0.62, Panel().Progress!.Value, 2);
        updates.Updates.DownloadGate.SetResult();
        await check;

        Assert.Equal("Version 1.1 ready", Panel().Pill);
        Assert.Equal("Downloaded in the background today at 21:30. It installs by itself when World of Warcraft is closed and nothing is waiting to upload, or now:", Panel().Line);
        Assert.Equal(("What's new in 1.1", "Fixes a missed upload after long sessions · shows planned runs"), (Panel().WhatsNew, Panel().Notes));
        Assert.True(Panel().ShowRestartNow);
        Assert.False(Panel().CanCheck);

        h.Status(MainHarness.First, AccountState.Uploading);
        updates.Checker.RestartNow();
        Assert.Equal(("Waiting for upload", "Restarting after this upload, a few seconds", UpdateButtons.Cancel), (Panel().Pill, Panel().Line, Panel().Buttons));
    }

    [AvaloniaFact]
    public void DevBuildAndFailedInstall()
    {
        var dev = new MainHarness(installed: false);
        var panel = UpdatePanel.For(dev.Updates.Checker, dev.Now, TimeZoneInfo.Utc, "wow.vestras.nl", true);
        Assert.Equal(("Version 1.0.3 · local build", "Dev build", "Updates are off for builds that aren't installed releases."), (panel.VersionLine, panel.Pill, panel.Line));
        Assert.True(panel.IsDevBuild);

        var h = new MainHarness();
        h.Updates.Memory.Saved = new UpdateMemory(HandedOver: "1.1.0");
        h.Updates.Checker.Start();
        panel = UpdatePanel.For(h.Updates.Checker, h.Now, TimeZoneInfo.Utc, "wow.vestras.nl", true);
        Assert.Equal(("Update failed", Tone.Warn, "Still on 1.0.3, nothing lost. The download will be fetched again."), (panel.Pill, panel.PillTone, panel.Line));
        Assert.Equal(UpdateButtons.TryAgain | UpdateButtons.OpenLog, panel.Buttons);
        h.Updates.Checker.Dispose();
    }

    [AvaloniaFact]
    public async Task SettingsSavesInstallWhenWowClosedAndChecks()
    {
        var h = new MainHarness();
        var vm = h.CreateSettings();
        await vm.OpenAsync();
        Assert.True(vm.InstallUpdatesWhenWowClosed);

        vm.InstallUpdatesWhenWowClosed = false;
        await vm.CheckForUpdatesCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Assert.IsType<SettingsLoad.Loaded>(new SettingsStore(h.SettingsFile).Load()).Settings.InstallUpdatesWhenWowClosed);
        Assert.Equal("● Up to date", vm.Updates?.Pill);
        Assert.Equal(1, h.Updates.Updates.Checks);
        vm.Dispose();
    }

    [AvaloniaFact]
    public async Task ARequiredUpdateTakesOverTheHeader()
    {
        var h = new MainHarness();
        var vm = h.Create();
        vm.Attach();
        h.Updates.Site.Status = SiteResult.Ok(new UpdateStatus("1.1.0", null, "1.1.0", Notes));
        h.Updates.Updates.Newer = new UpdateOffer("1.1.0", 6_600_000);

        await h.Updates.Checker.CheckNowAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(("● Uploads paused", "Update needed to keep uploading", Tone.Warn), (vm.Hero.Status, vm.Hero.Title, vm.Hero.Tone));
        Assert.Equal("The site changed and 1.0.3 can't upload to it anymore. Version 1.1 is downloaded. Nothing is lost: your saved data uploads right after.", vm.Hero.Line);
        Assert.True(vm.Hero.ShowRestartToUpdate);
        Assert.True(vm.Hero.IsUpdateNeeded);
        Assert.True(h.Loop.IsHeld);
        vm.Dispose();
    }

    [AvaloniaFact]
    public void AfterAnUpdateTheFooterSaysSoOnce()
    {
        var h = new MainHarness();
        h.Updates.Updates.CurrentVersion = "1.1.0";
        h.Updates.Memory.Saved = new UpdateMemory(HandedOver: "1.1.0");
        h.Updates.Site.Status = SiteResult.Ok(new UpdateStatus("1.1.0", null, null, Notes));
        h.Updates.Checker.Start();
        var vm = h.Create();
        vm.Attach();

        Assert.Equal("Updated to 1.1 · ", vm.UpdatedTo);
        Dispatcher.UIThread.RunJobs();
        vm.OpenWhatsNewCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.HasUpdatedTo);
        Assert.Contains(Notes, h.Opened);
        vm.Dispose();
        h.Updates.Checker.Dispose();
    }

    [Fact]
    public void TrayShowsTheOrangeDotOnlyForARequiredUpdate()
    {
        var status = new AccountStatus(MainHarness.First, AccountState.Paused);
        var now = new DateTimeOffset(2026, 10, 27, 21, 30, 0, TimeSpan.Zero);

        var model = TrayModel.For([status], "Doombringers", "wow.vestras.nl", now, TimeZoneInfo.Utc, null, updateNeeded: true);

        Assert.Equal(TrayIconKind.Attention, model.Icon);
        Assert.Equal("Update needed to keep uploading", model.Tooltip);
    }

    private static async Task UpdateHarnessUntil(Func<bool> condition)
    {
        for (int i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }
}
