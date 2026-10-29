using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.Core.Runs;

namespace CommunitiesForever.Upload.App.Tests.Main;

/// <summary>The main window's Runs block (UP-12, board <c>main-window-runs</c>).</summary>
public sealed class RunsBlockTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 2, 17, 34, 0, TimeSpan.Zero);
    private const string Path = @"C:\WoW\_forever_\Interface\AddOns\CommunitiesForever\Data\Runs.lua";

    [Fact]
    public void SaysWhatWasSavedAndWhenItChecksAgain_AsOnTheBoard()
    {
        var block = RunsBlock.For(new RunsStatus(3, At, At.AddMinutes(5), false, true, RunsHint.Reload, Path), TimeZoneInfo.Utc);

        Assert.Equal("3 runs saved for the game · 17:34", block.Title);
        Assert.Equal("Checks every 5 min while WoW runs, and after each upload · next 17:39", block.Line);
        Assert.Equal("Type /reload to load them", block.Badge);
        Assert.Equal(RunsBadgeKind.Reload, block.BadgeKind);
        Assert.Equal(Path, block.FullPath);
    }

    [Theory]
    [InlineData(null, null, false, false, "Runs reach the game when WoW is running", "Checks when WoW starts, and after each upload")]
    [InlineData(0, 0, false, false, "No upcoming runs · 17:34", "Checks again when WoW starts")]
    [InlineData(1, 1, false, false, "1 run saved for the game · 17:34", "Checks again when WoW starts")]
    [InlineData(2, 2, true, true, "2 runs saved for the game · 17:34", "Couldn't get runs from the site · tries again 17:39")]
    [InlineData(2, 2, true, false, "2 runs saved for the game · 17:34", "Couldn't get runs from the site · tries again when WoW starts")]
    public void EveryStateHasItsLines(int? fetched, int? count, bool failed, bool running, string title, string line)
    {
        var status = new RunsStatus(count, fetched is null ? null : At, running ? At.AddMinutes(5) : null, failed, running);
        var block = RunsBlock.For(status, TimeZoneInfo.Utc);

        Assert.Equal(title, block.Title);
        Assert.Equal(line, block.Line);
        Assert.False(block.HasBadge);
        Assert.Null(block.FullPath);
    }

    [Theory]
    [InlineData(RunsHint.RestartWow, "Restart WoW to load them", RunsBadgeKind.Reload, null)]
    [InlineData(RunsHint.UpdateAddon, "Update the addon", RunsBadgeKind.Warn, "Update the addon to see runs in game")]
    [InlineData(RunsHint.InstallAddon, "Addon not installed", RunsBadgeKind.Warn, "Install the CommunitiesForever addon to see runs in game")]
    public void SaysWhatTheGameOrTheAddonNeeds(RunsHint hint, string badge, RunsBadgeKind kind, string? line)
    {
        var block = RunsBlock.For(new RunsStatus(3, At, null, false, false, hint), TimeZoneInfo.Utc);

        Assert.Equal(badge, block.Badge);
        Assert.Equal(kind, block.BadgeKind);
        Assert.Equal(line ?? "Checks again when WoW starts", block.Line);
    }

    [Fact]
    public void MainWindowShowsTheBlockWhileConnected_AndOpensRunsOnTheSite()
    {
        var h = new MainHarness();
        h.RunsSent();
        var vm = h.Create();
        vm.Reload();

        Assert.True(vm.ShowRuns);
        Assert.Equal("3 runs saved for the game · 21:30", vm.Runs.Title);
        Assert.Equal("Type /reload to load them", vm.Runs.Badge);
        vm.OpenRunsCommand.Execute(null);
        Assert.Equal(new Uri("https://wow.vestras.nl/runs"), h.Opened[^1]);

        h.Status(MainHarness.First, Core.Uploading.AccountState.NotConnected);
        vm.Refresh();
        Assert.False(vm.ShowRuns);
        h.Runs.Dispose();
    }
}
