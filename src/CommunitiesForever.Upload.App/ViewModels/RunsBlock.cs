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
/// The main window's Runs block (UP-12, board <c>main-window-runs</c>): what was sent to the game and when it checks
/// again, a badge when the game or the addon needs something, and the file's path (in full as a tooltip).
/// </summary>
public sealed record RunsBlock(string Title, string Line, string? Badge, RunsBadgeKind BadgeKind, string? Path, string? FullPath)
{
    public const string ShortPath = @"…\Interface\AddOns\CommunitiesForever\Data\Runs.lua";

    public bool HasBadge => Badge is not null;

    public bool HasPath => Path is not null;

    public static RunsBlock For(RunsStatus status, TimeZoneInfo zone)
    {
        string Clock(DateTimeOffset at) => Formats.Clock(TimeZoneInfo.ConvertTime(at, zone));

        string title = status.FetchedAt is not { } fetched
            ? "Runs reach the game when WoW is running"
            : status.RunCount switch
            {
                0 or null => $"No upcoming runs · {Clock(fetched)}",
                1 => $"1 upcoming run sent to the game · {Clock(fetched)}",
                var n => $"{n} upcoming runs sent to the game · {Clock(fetched)}",
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
            RunsHint.Reload => ("/reload to see them", RunsBadgeKind.Reload),
            RunsHint.RestartWow => ("Restart WoW to see them", RunsBadgeKind.Reload),
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

        return new RunsBlock(title, line, badge, kind, status.FilePath is null ? null : ShortPath, status.FilePath);
    }
}
