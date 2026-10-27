using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.App.Tray;

/// <summary>Which of Nico's tray icons (board <c>tray</c>).</summary>
public enum TrayIconKind
{
    Watching,
    Uploading,
    Paused,
    Attention,
}

/// <summary>What the tray shows: icon, tooltip line and the menu's status line (board <c>tray</c>).</summary>
public sealed record TrayModel(TrayIconKind Icon, string Tooltip, string StatusLine)
{
    /// <summary>Windows cuts tray tooltips longer than this.</summary>
    public const int TooltipMax = 127;

    public static TrayModel For(IReadOnlyList<AccountStatus> statuses, string siteName, string host, DateTimeOffset now, TimeZoneInfo zone, DateTimeOffset? pausedUntil)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        var urgent = statuses.OrderByDescending(s => s.State).FirstOrDefault();
        var state = urgent?.State ?? AccountState.Watching;
        var last = statuses.Select(s => s.LastUpload).Where(u => u is not null).MaxBy(u => u!.UploadedAt);
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        string lastUpload = last is null
            ? "no upload yet"
            : $"last upload {When(TimeZoneInfo.ConvertTime(last.UploadedAt, zone), localNow)}";
        string until = pausedUntil is { } p ? Formats.Clock(TimeZoneInfo.ConvertTime(p, zone)) : "";

        var (icon, tooltip, status) = state switch
        {
            AccountState.Uploading => (TrayIconKind.Uploading, "Uploading CommunitiesForever.lua…", "● Uploading"),
            AccountState.Paused => (TrayIconKind.Paused, $"Paused until {until}", $"● Paused until {until}"),
            AccountState.NotConnected => (TrayIconKind.Attention, "Upload token no longer works", "● Not connected"),
            AccountState.WaitingToSend => (TrayIconKind.Attention, $"Can't reach {host}", "● Waiting to send"),
            AccountState.NotAllowed => (TrayIconKind.Attention, "Not allowed to upload right now", "● Not allowed"),
            AccountState.NotWatching => (TrayIconKind.Attention, "Where is World of Warcraft?", "● Not watching"),
            AccountState.NotUploaded => (TrayIconKind.Attention, Hero.Rejected(urgent!.Rejection, urgent.SchemaVersion).Title, "● Not uploaded"),
            AccountState.Playing => (TrayIconKind.Watching, $"{siteName} · {lastUpload}", $"● Playing · {lastUpload}"),
            _ => (TrayIconKind.Watching, $"{siteName} · {lastUpload}", $"● Watching · {lastUpload}"),
        };

        return new TrayModel(icon, tooltip.Length > TooltipMax ? tooltip[..(TooltipMax - 1)] + "…" : tooltip, status);
    }

    // Board: "last upload 21:14"; another day says which.
    private static string When(DateTimeOffset at, DateTimeOffset now) => at.Date == now.Date ? Formats.Clock(at) : Formats.Moment(at, now);
}
