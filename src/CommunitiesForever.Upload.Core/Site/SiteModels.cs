namespace CommunitiesForever.Upload.Core.Site;

/// <summary>RFC 8628 device authorization response (UPLOADER-API.md "Connecting" step 1).</summary>
public sealed record DeviceAuthorization(
    DeviceCode DeviceCode,
    string UserCode,
    Uri VerificationUri,
    Uri? VerificationUriComplete,
    TimeSpan ExpiresIn,
    TimeSpan Interval);

public enum PollStatus
{
    /// <summary>Allowed: <see cref="TokenPoll.Token"/> is set.</summary>
    Granted,

    /// <summary><c>authorization_pending</c>: keep polling.</summary>
    Pending,

    /// <summary><c>slow_down</c>: add 5 seconds to the interval.</summary>
    SlowDown,

    /// <summary><c>access_denied</c>.</summary>
    Denied,

    /// <summary><c>expired_token</c>: expired, already collected or unknown.</summary>
    Expired,
}

public sealed record TokenPoll(PollStatus Status, AccessToken? Token = null);

/// <summary><c>GET /me</c>: who the token belongs to.</summary>
public sealed record SiteIdentity(
    string BattleTag,
    string SiteName,
    string DeviceName,
    string? AppVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUploadAt);

/// <summary>
/// <c>GET updates/status</c> (UP-10): the newest uploader, when it came out, the oldest one the site still accepts
/// (null: any) and its "What's new" page.
/// </summary>
public sealed record UpdateStatus(string Latest, DateTimeOffset? ReleasedAt, string? MinimumVersion, Uri? NotesUrl);

/// <summary>
/// <c>GET runs</c> (UP-12): the site's answer as it came, turned into Runs.lua as data only. <see cref="RunCount"/> is
/// what the main window shows; the app reads nothing else from it.
/// </summary>
public sealed record RunsFeed(System.Text.Json.JsonElement Root)
{
    public int RunCount =>
        Root.ValueKind == System.Text.Json.JsonValueKind.Object
        && Root.TryGetProperty("runs", out var runs)
        && runs.ValueKind == System.Text.Json.JsonValueKind.Array
            ? runs.GetArrayLength()
            : 0;
}

public enum UploadStatus
{
    Ok,
    NothingNew,
    Rejected,
}

public sealed record UploadCounts(int Characters, int Friends);

/// <summary>
/// The site's answer to an upload. Everything the app shows about the file's content comes from here (CLAUDE.md rule 1).
/// </summary>
public sealed record UploadReceipt(
    long UploadId,
    DateTimeOffset UploadedAt,
    UploadStatus Status,
    string? Rejection,
    int? SchemaVersion,
    string SummaryLine,
    IReadOnlyList<string> Highlights,
    UploadCounts? Counts,
    Uri? ReportUrl)
{
    /// <summary>Rejection codes of UPLOADER-API.md; any other code means "not uploaded".</summary>
    public static class Rejections
    {
        public const string WrongFile = "wrong_file";
        public const string Damaged = "damaged";
        public const string NewerFormat = "newer_format";
        public const string NotYourFile = "not_your_file";
        public const string OtherCommunity = "other_community";
        public const string NoCommunityYet = "no_community_yet";
    }
}
