using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CommunitiesForever.Upload.App.Tests.Main;
using CommunitiesForever.Upload.App.Tests.Setup;
using CommunitiesForever.Upload.App.Views;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Updates;
using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.App.DesignChecks;

/// <summary>Updates (UP-10): every state of board <c>update-states</c>, plus the main window after and before an update.</summary>
public sealed class UpdateScreenshotTests
{
    private const double PanelWidth = 640;
    private static readonly Uri Notes = new("https://wow.vestras.nl/uploader/changes#1.1.0");
    private static readonly UpdateOffer Newer = new("1.1.0", 6_600_000, "Fixes a missed upload after long sessions · shows planned runs");

    [AvaloniaFact]
    public Task UpToDate() => Panel("update-up-to-date", async h =>
    {
        h.Updates.Site.Status = SiteResult.Ok(new UpdateStatus("1.0.3", new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero), null, null));
        await h.Updates.Checker.CheckNowAsync();
    });

    [AvaloniaFact]
    public Task Checking() => Panel("update-checking", h =>
    {
        h.Updates.Updates.CheckGate = new TaskCompletionSource();
        _ = h.Updates.Checker.CheckNowAsync();
        return SetupHarness.Until(() => h.Updates.Checker.State == UpdateState.Checking);
    });

    [AvaloniaFact]
    public Task Downloading() => Panel("update-downloading", h =>
    {
        h.Updates.Updates.Newer = Newer;
        h.Updates.Updates.DownloadGate = new TaskCompletionSource();
        _ = h.Updates.Checker.CheckNowAsync();
        return SetupHarness.Until(() => h.Updates.Checker.State == UpdateState.Downloading);
    });

    [AvaloniaFact]
    public Task Ready() => Panel("update-ready", Download);

    [AvaloniaFact]
    public Task WaitingForUpload() => Panel("update-waiting", async h =>
    {
        await Download(h);
        h.Status(MainHarness.First, AccountState.Uploading);
        h.Updates.Checker.RestartNow();
    });

    [AvaloniaFact]
    public Task CouldntCheck() => Panel("update-couldnt-check", async h =>
    {
        h.Updates.Updates.CheckError = new HttpRequestException("offline");
        await h.Updates.Checker.CheckNowAsync();
    });

    [AvaloniaFact]
    public Task Failed() => Panel("update-failed", h =>
    {
        h.Updates.Memory.Saved = new UpdateMemory(HandedOver: "1.1.0");
        h.Updates.Checker.Start();
        return Task.CompletedTask;
    });

    [AvaloniaFact]
    public Task DevBuild() => Panel("update-dev-build", _ => Task.CompletedTask, installed: false);

    [AvaloniaFact]
    public async Task UpdateNeeded()
    {
        var h = new MainHarness();
        h.BoardHistory(MainHarness.First);
        h.Updates.Site.Status = SiteResult.Ok(new UpdateStatus("1.1.0", null, "1.1.0", Notes));
        h.Updates.Updates.Newer = Newer;
        await h.Updates.Checker.CheckNowAsync();
        h.Status(MainHarness.First, AccountState.Paused);

        await MainShot(h, "main-update-needed");
    }

    [AvaloniaFact]
    public async Task Updated()
    {
        var h = new MainHarness();
        h.BoardHistory(MainHarness.First);
        h.Time.Advance(TimeSpan.FromHours(1));
        h.Status(MainHarness.First, AccountState.Watching);
        h.Updates.Updates.CurrentVersion = "1.1.0";
        h.Updates.Memory.Saved = new UpdateMemory(HandedOver: "1.1.0");
        h.Updates.Checker.Start();

        await MainShot(h, "main-updated");
        h.Updates.Checker.Dispose();
    }

    private static async Task Download(MainHarness h)
    {
        h.Updates.Site.Status = SiteResult.Ok(new UpdateStatus("1.1.0", null, null, Notes));
        h.Updates.Updates.Newer = Newer;
        await h.Updates.Checker.CheckNowAsync();
    }

    private static async Task Panel(string name, Func<MainHarness, Task> arrange, bool installed = true)
    {
        var h = new MainHarness(installed);
        await arrange(h);
        var vm = h.CreateSettings();
        vm.AppName = "Doombringers Uploader";
        await vm.OpenAsync();
        var window = new Window
        {
            Width = PanelWidth,
            SizeToContent = SizeToContent.Height,
            Content = new Border
            {
                Padding = (Thickness)Application.Current!.FindResource("S4Thickness")!,
                Child = new UpdatePanelView { DataContext = vm },
            },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Screenshot.Matches(window, name);
        window.Close();
        vm.Dispose();
    }

    private static async Task MainShot(MainHarness h, string name)
    {
        var vm = h.Create();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        await SetupHarness.Until(() => vm.Identity is not null);
        vm.Refresh();
        Dispatcher.UIThread.RunJobs();

        Screenshot.Matches(window, name);
        window.Close();
    }
}
