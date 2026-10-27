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
