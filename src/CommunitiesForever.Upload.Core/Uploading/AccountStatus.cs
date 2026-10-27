using CommunitiesForever.Upload.Core.Settings;

namespace CommunitiesForever.Upload.Core.Uploading;

/// <summary>
/// An account's state (board <c>states</c>). Ordered by urgency: the app's overall status is the highest of its accounts.
/// </summary>
public enum AccountState
{
    Watching,
    Playing,
    Settling,
    ReadyToUpload,
    Paused,
    Uploading,
    WaitingToSend,
    NotAllowed,
    NotUploaded,
    NotWatching,
    NotConnected,
}

/// <summary>What the main window and the tray show for one watched account.</summary>
public sealed record AccountStatus(
    WatchedAccount Account,
    AccountState State,
    DateTimeOffset? SettlesAt = null,
    DateTimeOffset? NextTryAt = null,
    string? Rejection = null,
    int? SchemaVersion = null,
    HistoryEntry? LastUpload = null,
    FileStamp? File = null);
