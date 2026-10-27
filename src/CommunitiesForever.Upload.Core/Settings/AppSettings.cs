namespace CommunitiesForever.Upload.Core.Settings;

public enum NotificationLevel
{
    EveryUpload,
    OnlyProblems,
    Off,
}

/// <summary>One WoW account folder the app watches: <c>&lt;InstallPath&gt;\WTF\Account\&lt;Account&gt;</c>.</summary>
public sealed record WatchedAccount(string InstallPath, string Account);

/// <summary>
/// The user's settings, saved as settings.json (docs/ARCHITECTURE.md "Storage"). Versioned: every change gets a new
/// <see cref="CurrentVersion"/> and a migration step in <see cref="SettingsStore"/> (CLAUDE.md rule 10).
/// </summary>
public sealed record AppSettings
{
    public const int CurrentVersion = 2;

    public IReadOnlyList<WatchedAccount> Accounts { get; init; } = [];

    public bool UploadAutomatically { get; init; } = true;

    public bool StartWithWindows { get; init; } = true;

    public bool CloseHidesToTray { get; init; } = true;

    public NotificationLevel Notifications { get; init; } = NotificationLevel.EveryUpload;

    /// <summary>Version 2 (UP-10): restart quietly into a downloaded update when WoW is closed and nothing waits to upload.</summary>
    public bool InstallUpdatesWhenWowClosed { get; init; } = true;

    /// <summary>Only set when the user points the app at another site than wow.vestras.nl.</summary>
    public Uri? SiteUri { get; init; }

    public bool Equals(AppSettings? other) =>
        other is not null
        && Accounts.SequenceEqual(other.Accounts)
        && UploadAutomatically == other.UploadAutomatically
        && StartWithWindows == other.StartWithWindows
        && CloseHidesToTray == other.CloseHidesToTray
        && Notifications == other.Notifications
        && InstallUpdatesWhenWowClosed == other.InstallUpdatesWhenWowClosed
        && SiteUri == other.SiteUri;

    public override int GetHashCode() => HashCode.Combine(Accounts.Count, UploadAutomatically, StartWithWindows, CloseHidesToTray, Notifications, InstallUpdatesWhenWowClosed, SiteUri);
}
