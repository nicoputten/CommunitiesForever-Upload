namespace CommunitiesForever.Upload.Core.Site;

/// <summary>The site's uploader API (UPLOADER-API.md, owned by the web repo). Calls never throw except on cancellation.</summary>
public interface ISiteClient
{
    /// <summary>RFC 8628 device authorization request.</summary>
    Task<SiteResult<DeviceAuthorization>> RequestDeviceCodeAsync(string deviceName, CancellationToken cancellationToken = default);

    /// <summary>RFC 8628 device access token request; call every <see cref="DeviceAuthorization.Interval"/>.</summary>
    Task<SiteResult<TokenPoll>> PollTokenAsync(DeviceCode deviceCode, CancellationToken cancellationToken = default);

    Task<SiteResult<SiteIdentity>> GetIdentityAsync(AccessToken token, CancellationToken cancellationToken = default);

    /// <summary>Uploads the file's bytes as they are. <paramref name="savedAt"/> is the file's write time.</summary>
    Task<SiteResult<UploadReceipt>> UploadAsync(AccessToken token, Stream content, DateTimeOffset? savedAt, CancellationToken cancellationToken = default);

    /// <summary>RFC 7009 revocation; succeeds for unknown or already revoked tokens too.</summary>
    Task<SiteResult<bool>> RevokeAsync(AccessToken token, CancellationToken cancellationToken = default);

    /// <summary>The newest version, the minimum the site still accepts and its "What's new" page (UP-10).</summary>
    Task<SiteResult<UpdateStatus>> GetUpdateStatusAsync(AccessToken token, CancellationToken cancellationToken = default);
}

/// <summary>Where the site is and what the app sends about itself.</summary>
public sealed record SiteOptions(Uri BaseUri, string AppVersion)
{
    public const string ClientId = "communitiesforever-uploader";

    public static readonly Uri DefaultBaseUri = new("https://wow.vestras.nl/");
}
