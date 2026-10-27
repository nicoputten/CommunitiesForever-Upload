namespace CommunitiesForever.Upload.Core.Site;

/// <summary>Why a call to the site did not work (UPLOADER-API.md errors, mapped once here).</summary>
public enum SiteError
{
    /// <summary>No answer: no network, DNS, timeout or the site is down.</summary>
    Offline,

    /// <summary>401 <c>invalid_token</c>: unknown or revoked; the app shows "Reconnect".</summary>
    InvalidToken,

    /// <summary>403 <c>may_not_upload</c>: the user lost upload rights; the token stays.</summary>
    MayNotUpload,

    /// <summary>413 <c>too_large</c>.</summary>
    TooLarge,

    /// <summary>400 <c>empty_file</c>.</summary>
    EmptyFile,

    /// <summary>429 <c>rate_limited</c>; see <see cref="SiteFailure.RetryAfter"/>.</summary>
    RateLimited,

    /// <summary>5xx.</summary>
    ServerError,

    /// <summary>Anything else, e.g. <c>invalid_client</c> or a new error code (contract changes are additive).</summary>
    Unexpected,
}

public sealed record SiteFailure(SiteError Kind, string? Code = null, int? Status = null, TimeSpan? RetryAfter = null);

/// <summary>The outcome of a site call: a value or a <see cref="SiteFailure"/>, never an exception (except cancellation).</summary>
public sealed class SiteResult<T>
{
    internal SiteResult(T? value, SiteFailure? failure)
    {
        Value = value;
        Failure = failure;
    }

    public T? Value { get; }

    public SiteFailure? Failure { get; }

    public bool IsOk => Failure is null;

    public static implicit operator SiteResult<T>(SiteFailure failure) => new(default, failure);
}

public static class SiteResult
{
    public static SiteResult<T> Ok<T>(T value) => new(value, null);
}
