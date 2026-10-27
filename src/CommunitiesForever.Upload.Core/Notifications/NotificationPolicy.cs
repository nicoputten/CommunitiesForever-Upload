using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.Core.Notifications;

public enum NotificationKind
{
    /// <summary>An upload with something new (board <c>tray</c>: "Uploaded · 5 levels gained").</summary>
    Uploaded,

    /// <summary>"No connection: will try again".</summary>
    Offline,

    /// <summary>"Upload token no longer works", with Reconnect / Later.</summary>
    NotConnected,

    /// <summary>The site didn't take the file (approved addition: one line per reason).</summary>
    NotUploaded,
}

/// <summary>Something worth a Windows notification, with what the text needs.</summary>
public sealed record NotificationEvent(NotificationKind Kind, WatchedAccount Account, HistoryEntry? Upload = null, string? Rejection = null, int? SchemaVersion = null)
{
    public bool IsProblem => Kind != NotificationKind.Uploaded;
}

/// <summary>
/// Decides which status changes become a notification (UP-08): a new upload with something in it, and each problem
/// once when it starts, not on every retry. "Only problems" drops the uploads, "Off" everything. "Nothing new" never
/// notifies.
/// </summary>
public sealed class NotificationPolicy
{
    private readonly Dictionary<WatchedAccount, AccountStatus> _previous = [];

    public IReadOnlyList<NotificationEvent> Update(IReadOnlyList<AccountStatus> statuses, NotificationLevel level)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        var events = new List<NotificationEvent>();
        foreach (var status in statuses)
        {
            // The first time an account is seen sets the baseline: earlier uploads and states don't notify.
            if (_previous.TryGetValue(status.Account, out var before))
            {
                events.AddRange(Changes(before, status));
            }

            _previous[status.Account] = status;
        }

        // Accounts no longer watched are forgotten (rule 11: no growing state).
        foreach (var gone in _previous.Keys.Where(k => statuses.All(s => s.Account != k)).ToList())
        {
            _previous.Remove(gone);
        }

        return level switch
        {
            NotificationLevel.Off => [],
            NotificationLevel.OnlyProblems => [.. events.Where(e => e.IsProblem)],
            _ => events,
        };
    }

    private static IEnumerable<NotificationEvent> Changes(AccountStatus before, AccountStatus now)
    {
        if (now.LastUpload is { Status: UploadStatus.Ok } upload && upload.UploadId != before.LastUpload?.UploadId)
        {
            yield return new NotificationEvent(NotificationKind.Uploaded, now.Account, upload);
        }

        if (now.State == before.State)
        {
            yield break;
        }

        switch (now.State)
        {
            case AccountState.WaitingToSend:
                yield return new NotificationEvent(NotificationKind.Offline, now.Account);
                break;
            case AccountState.NotConnected:
                yield return new NotificationEvent(NotificationKind.NotConnected, now.Account);
                break;
            case AccountState.NotUploaded:
                yield return new NotificationEvent(NotificationKind.NotUploaded, now.Account, now.LastUpload, now.Rejection, now.SchemaVersion);
                break;
        }
    }
}
