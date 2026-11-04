using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CommunitiesForever.Upload.App.Tests.Main;
using CommunitiesForever.Upload.App.Tests.Setup;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.App.DesignChecks;

/// <summary>The main window (board <c>main-window</c>, 900 × 620) in the states of board <c>states</c>.</summary>
public sealed class MainScreenshotTests
{
    [AvaloniaFact]
    public Task CaughtUp() => Shot("main-caught-up", h =>
    {
        h.BoardHistory(MainHarness.First);
        h.Time.Advance(TimeSpan.FromHours(1));
        h.Status(MainHarness.First, AccountState.Watching);
    });

    [AvaloniaFact]
    public Task Uploaded() => Shot("main-uploaded", h =>
    {
        h.BoardHistory(MainHarness.First);
        h.Status(MainHarness.First, AccountState.Watching);
    });

    [AvaloniaFact]
    public Task Playing() => Shot("main-playing", h =>
    {
        h.BoardHistory(MainHarness.First);
        h.Time.Advance(TimeSpan.FromHours(1));
        h.Status(MainHarness.First, AccountState.Playing);
    });

    [AvaloniaFact]
    public Task FileChanged() => Shot("main-file-changed", h =>
    {
        h.BoardHistory(MainHarness.First);
        h.Status(MainHarness.First, AccountState.Settling, settlesAt: h.Now.AddSeconds(4));
    });

    [AvaloniaFact]
    public Task Uploading() => Shot("main-uploading", h =>
    {
        h.BoardHistory(MainHarness.First);
        h.Status(MainHarness.First, AccountState.Uploading);
    });

    [AvaloniaFact]
    public Task WaitingToSend() => Shot("main-waiting-to-send", h =>
    {
        h.BoardHistory(MainHarness.First);
        h.Status(MainHarness.First, AccountState.WaitingToSend, nextTryAt: h.Now.AddMinutes(1));
    });

    [AvaloniaFact]
    public Task NotConnected() => Shot("main-not-connected", h =>
    {
        h.BoardHistory(MainHarness.First);
        h.Status(MainHarness.First, AccountState.NotConnected);
    });

    [AvaloniaFact]
    public Task NewerFormat() => Shot("main-newer-format", h =>
    {
        h.BoardHistory(MainHarness.First);
        h.Record(MainHarness.First, MainHarness.Upload(43, h.Now.AddMinutes(-3), UploadStatus.Rejected, "Newer addon: format 3", rejection: "newer_format", schema: 3));
        h.Status(MainHarness.First, AccountState.NotUploaded, rejection: "newer_format", schema: 3);
    });

    [AvaloniaFact]
    public Task NotWatching() => Shot("main-not-watching", h =>
    {
        h.BoardHistory(MainHarness.First);
        h.Status(MainHarness.First, AccountState.NotWatching);
    });

    [AvaloniaFact]
    public Task TwoAccounts() => Shot(
        "main-two-accounts",
        h =>
        {
            h.BoardHistory(MainHarness.First);
            h.Time.Advance(TimeSpan.FromHours(1));
            h.Status(MainHarness.First, AccountState.Watching);
            h.Status(MainHarness.Second, AccountState.NotWatching);
        },
        MainHarness.First,
        MainHarness.Second);

    // UP-12, board main-window-runs: three runs sent while WoW runs.
    [AvaloniaFact]
    public Task RunsSent() => Shot("main-runs", h =>
    {
        h.BoardHistory(MainHarness.First);
        h.Status(MainHarness.First, AccountState.Playing);
        h.RunsSent();
    });

    // Smaller windows (not on the boards): at the minimum size Recent uploads goes under the left column and
    // the content scrolls; at a medium width the hero's buttons go under its text.
    [AvaloniaFact]
    public Task NotWatchingNarrow() => Shot("main-narrow-not-watching", NotWatchingState, size: Narrow);

    [AvaloniaFact]
    public Task NotWatchingMedium() => Shot("main-medium-not-watching", NotWatchingState, size: Medium);

    [AvaloniaFact]
    public Task UploadedNarrow() => Shot("main-narrow-uploaded", UploadedState, size: Narrow);

    [AvaloniaFact]
    public Task UploadedMedium() => Shot("main-medium-uploaded", UploadedState, size: Medium);

    private static readonly Size Narrow = new(640, 480);
    private static readonly Size Medium = new(780, 620);

    private static void NotWatchingState(MainHarness h)
    {
        h.BoardHistory(MainHarness.First);
        h.Status(MainHarness.First, AccountState.NotWatching);
    }

    private static void UploadedState(MainHarness h)
    {
        h.BoardHistory(MainHarness.First);
        h.Status(MainHarness.First, AccountState.Watching);
    }

    private static Task Shot(string name, Action<MainHarness> arrange, params WatchedAccount[] accounts) =>
        Shot(name, arrange, null, accounts);

    private static async Task Shot(string name, Action<MainHarness> arrange, Size? size, params WatchedAccount[] accounts)
    {
        var h = new MainHarness(accounts);
        arrange(h);
        var vm = h.Create();
        var window = new MainWindow { DataContext = vm };
        if (size is { } s)
        {
            window.Width = s.Width;
            window.Height = s.Height;
        }

        window.Show();
        await SetupHarness.Until(() => vm.Identity is not null);
        vm.Refresh();
        Dispatcher.UIThread.RunJobs();

        Screenshot.Matches(window, name);
        window.Close();
    }
}
