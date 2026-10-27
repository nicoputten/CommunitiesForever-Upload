using CommunitiesForever.Upload.Core.Settings;

namespace CommunitiesForever.Upload.Core.Uploading;

/// <summary>What the screens see of the loop: statuses and the user's actions.</summary>
public interface IUploadLoop
{
    /// <summary>Raised (on a background thread) whenever an account's status changes.</summary>
    event EventHandler? StatusChanged;

    IReadOnlyList<AccountStatus> Statuses { get; }

    DateTimeOffset? PausedUntil { get; }

    void UploadNow(WatchedAccount? account = null);

    void Skip(WatchedAccount account);

    void Pause(DateTimeOffset until);

    void EndPause();

    /// <summary>Uploads wait for a required update (UP-10); also "Upload now". Not a pause: Resume doesn't end it.</summary>
    bool IsHeld { get; }

    void Hold(bool held);
}
