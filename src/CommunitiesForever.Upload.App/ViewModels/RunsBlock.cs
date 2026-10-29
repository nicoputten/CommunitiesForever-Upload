using CommunitiesForever.Upload.Core.Runs;

namespace CommunitiesForever.Upload.App.ViewModels;

/// <summary>How the Runs block's badge looks: the board's "/reload" pill, or a warning.</summary>
public enum RunsBadgeKind
{
    None,
    Reload,
    Warn,
}

/// <summary>
/// The main window's Runs block (UP-12, board <c>main-window-runs</c>): what was saved for the game and when it checks
/// again, and a badge when the game or the addon needs something. The file's path is a tooltip only (docs/DESIGN-NOTES.md).
/// </summary>
public sealed record RunsBlock(string Title, string Line, string? Badge, RunsBadgeKind BadgeKind, string? FullPath)
{
    public bool HasBadge => Badge is not null;

    public static RunsBlock For(RunsStatus status, TimeZoneInfo zone)
    {
        string Clock(DateTimeOffset at) => Formats.Clock(TimeZoneInfo.ConvertTime(at, zone));

        string title = status.FetchedAt is not { } fetched
            ? "Runs reach the game when WoW is running"
            : status.RunCount switch
            {
                0 or null => $"No upcoming runs · {Clock(fetched)}",
                1 => $"1 run saved for the game · {Clock(fetched)}",
                var n => $"{n} runs saved for the game · {Clock(fetched)}",
            };

        string line = (status.Failed, status.NextAt) switch
        {
            (true, { } next) => $"Couldn't get runs from the site · tries again {Clock(next)}",
            (true, null) => "Couldn't get runs from the site · tries again when WoW starts",
            (false, { } next) => $"Checks every 5 min while WoW runs, and after each upload · next {Clock(next)}",
            _ when status.FetchedAt is null => "Checks when WoW starts, and after each upload",
            _ => "Checks again when WoW starts",
        };

        (string? badge, var kind) = status.Hint switch
        {
            // The game reads the file only when it loads: say what loads them, not that they are missing.
            RunsHint.Reload => ("Type /reload to load them", RunsBadgeKind.Reload),
            RunsHint.RestartWow => ("Restart WoW to load them", RunsBadgeKind.Reload),
            RunsHint.UpdateAddon => ("Update the addon", RunsBadgeKind.Warn),
            RunsHint.InstallAddon => ("Addon not installed", RunsBadgeKind.Warn),
            _ => ((string?)null, RunsBadgeKind.None),
        };

        line = status.Hint switch
        {
            RunsHint.UpdateAddon => "Update the addon to see runs in game",
            RunsHint.InstallAddon => "Install the CommunitiesForever addon to see runs in game",
            _ => line,
        };

        return new RunsBlock(title, line, badge, kind, status.FilePath);
    }
}
