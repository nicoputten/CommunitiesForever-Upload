using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.Core.Notifications;
using CommunitiesForever.Upload.Windows;

namespace CommunitiesForever.Upload.App.Tray;

/// <summary>The notifications of board <c>tray</c>, plus one per rejection reason (docs/DESIGN-NOTES.md).</summary>
public static class NotificationTexts
{
    public static AppNotification For(NotificationEvent e, string host)
    {
        ArgumentNullException.ThrowIfNull(e);
        switch (e.Kind)
        {
            case NotificationKind.Uploaded:
                var upload = e.Upload!;
                string[] counts = upload.SummaryLine.Split(" · ");
                // Board: "Uploaded · 5 levels gained", then what changed.
                string line = upload.Highlights.Count > 0 ? string.Join(" · ", upload.Highlights) : string.Join(" · ", counts.Skip(1));
                return new AppNotification(
                    $"Uploaded · {counts[0]}",
                    line,
                    AppIdentity.Open,
                    upload.ReportUrl is { } report ? [new NotificationButton("See what changed", report)] : []);

            case NotificationKind.Offline:
                return new AppNotification(
                    "No connection: will try again",
                    $"Your file is saved and goes up as soon as {host} is reachable.",
                    AppIdentity.Open,
                    []);

            case NotificationKind.NotConnected:
                return new AppNotification(
                    "Upload token no longer works",
                    "It was revoked on the site. Connect again to keep uploading.",
                    AppIdentity.Open,
                    [new NotificationButton("Reconnect", AppIdentity.Reconnect), new NotificationButton("Later", null)]);

            default:
                var (title, why) = Hero.Rejected(e.Rejection, e.SchemaVersion);
                return new AppNotification(
                    $"Not uploaded · {title}",
                    why,
                    AppIdentity.Open,
                    e.Upload?.ReportUrl is { } reason ? [new NotificationButton("See why", reason)] : []);
        }
    }
}
