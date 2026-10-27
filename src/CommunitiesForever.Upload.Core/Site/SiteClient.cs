using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duende.IdentityModel.Client;

namespace CommunitiesForever.Upload.Core.Site;

/// <summary>
/// <see cref="ISiteClient"/> over one shared <see cref="HttpClient"/> (created and owned by App).
/// Connecting and revoking use Duende.IdentityModel's standard OAuth calls, as UPLOADER-API.md "Standards" asks.
/// </summary>
public sealed class SiteClient(HttpClient http, SiteOptions options) : ISiteClient
{
    private const string ApiPath = "api/uploader/v1/";

    public async Task<SiteResult<DeviceAuthorization>> RequestDeviceCodeAsync(string deviceName, CancellationToken cancellationToken = default)
    {
        var request = new DeviceAuthorizationRequest
        {
            Address = Endpoint("device_authorization"),
            ClientId = SiteOptions.ClientId,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
        };
        request.Parameters.Add("device_name", deviceName.Length > 64 ? deviceName[..64] : deviceName);
        request.Parameters.Add("app_version", options.AppVersion);

        var response = await http.RequestDeviceAuthorizationAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.IsError)
        {
            return Failure(response, cancellationToken);
        }

        if (response.DeviceCode is null || response.UserCode is null || !Uri.TryCreate(response.VerificationUri, UriKind.Absolute, out var verification))
        {
            return new SiteFailure(SiteError.Unexpected, "invalid_response", (int)response.HttpStatusCode);
        }

        Uri.TryCreate(response.VerificationUriComplete, UriKind.Absolute, out var complete);
        return SiteResult.Ok(new DeviceAuthorization(
            new DeviceCode(response.DeviceCode),
            response.UserCode,
            verification,
            complete,
            TimeSpan.FromSeconds(response.ExpiresIn ?? 0),
            TimeSpan.FromSeconds(response.Interval)));
    }

    public async Task<SiteResult<TokenPoll>> PollTokenAsync(DeviceCode deviceCode, CancellationToken cancellationToken = default)
    {
        var response = await http.RequestDeviceTokenAsync(
            new DeviceTokenRequest
            {
                Address = Endpoint("token"),
                ClientId = SiteOptions.ClientId,
                ClientCredentialStyle = ClientCredentialStyle.PostBody,
                DeviceCode = deviceCode.Reveal(),
            },
            cancellationToken).ConfigureAwait(false);

        if (!response.IsError)
        {
            return response.AccessToken is { Length: > 0 } token
                ? SiteResult.Ok(new TokenPoll(PollStatus.Granted, new AccessToken(token)))
                : new SiteFailure(SiteError.Unexpected, "invalid_response", (int)response.HttpStatusCode);
        }

        PollStatus? status = response.Error switch
        {
            "authorization_pending" => PollStatus.Pending,
            "slow_down" => PollStatus.SlowDown,
            "access_denied" => PollStatus.Denied,
            "expired_token" => PollStatus.Expired,
            _ => null,
        };
        return status is { } known ? SiteResult.Ok(new TokenPoll(known)) : Failure(response, cancellationToken);
    }

    public async Task<SiteResult<SiteIdentity>> GetIdentityAsync(AccessToken token, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint("me"));
        var result = await SendAsync(request, token, SiteJson.Default.MeJson, cancellationToken).ConfigureAwait(false);
        if (!result.IsOk)
        {
            return result.Failure!;
        }

        var me = result.Value!;
        return SiteResult.Ok(new SiteIdentity(
            me.BattleTag ?? "",
            me.SiteName ?? "",
            me.DeviceName ?? "",
            me.AppVersion,
            DateTimeOffset.FromUnixTimeSeconds(me.CreatedAt),
            me.LastUploadAt is { } last ? DateTimeOffset.FromUnixTimeSeconds(last) : null));
    }

    public async Task<SiteResult<UploadReceipt>> UploadAsync(AccessToken token, Stream content, DateTimeOffset? savedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint("uploads"));
        var body = new StreamContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content = body;
        request.Headers.Add("X-Uploader-Version", options.AppVersion);
        if (savedAt is { } saved)
        {
            request.Headers.Add("X-File-Saved-At", saved.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        }

        var result = await SendAsync(request, token, SiteJson.Default.UploadJson, cancellationToken).ConfigureAwait(false);
        if (!result.IsOk)
        {
            return result.Failure!;
        }

        var upload = result.Value!;
        var status = upload.Status switch
        {
            "ok" => UploadStatus.Ok,
            "nothing_new" => UploadStatus.NothingNew,
            _ => UploadStatus.Rejected,
        };
        return SiteResult.Ok(new UploadReceipt(
            upload.UploadId,
            DateTimeOffset.FromUnixTimeSeconds(upload.UploadedAt),
            status,
            // An unknown status is treated as a rejection with that status as its reason ("not uploaded").
            status == UploadStatus.Rejected ? upload.Rejection ?? upload.Status : null,
            upload.SchemaVersion,
            upload.SummaryLine ?? "",
            upload.Highlights ?? [],
            upload.Counts is { } c ? new UploadCounts(c.Characters, c.Friends) : null,
            Uri.TryCreate(upload.ReportUrl, UriKind.Absolute, out var report) ? report : null));
    }

    public async Task<SiteResult<bool>> RevokeAsync(AccessToken token, CancellationToken cancellationToken = default)
    {
        var response = await http.RevokeTokenAsync(
            new TokenRevocationRequest
            {
                Address = Endpoint("revoke"),
                ClientId = SiteOptions.ClientId,
                ClientCredentialStyle = ClientCredentialStyle.PostBody,
                Token = token.Reveal(),
                TokenTypeHint = "access_token",
            },
            cancellationToken).ConfigureAwait(false);

        return response.IsError ? Failure(response, cancellationToken) : SiteResult.Ok(true);
    }

    private string Endpoint(string name) => new Uri(options.BaseUri, ApiPath + name).AbsoluteUri;

    private async Task<SiteResult<T>> SendAsync<T>(
        HttpRequestMessage request,
        AccessToken token,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> json,
        CancellationToken cancellationToken)
        where T : class
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Reveal());
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (IsOffline(e, cancellationToken))
        {
            return new SiteFailure(SiteError.Offline);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string? code = await ReadErrorCodeAsync(response, cancellationToken).ConfigureAwait(false);
                return Map(response.StatusCode, code, response.Headers.RetryAfter);
            }

            try
            {
                var value = await response.Content.ReadFromJsonAsync(json, cancellationToken).ConfigureAwait(false);
                return value is null ? new SiteFailure(SiteError.Unexpected, "invalid_response", (int)response.StatusCode) : SiteResult.Ok(value);
            }
            catch (JsonException)
            {
                return new SiteFailure(SiteError.Unexpected, "invalid_response", (int)response.StatusCode);
            }
            catch (Exception e) when (IsOffline(e, cancellationToken))
            {
                return new SiteFailure(SiteError.Offline);
            }
        }
    }

    private static SiteFailure Failure(ProtocolResponse response, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (response.ErrorType == ResponseErrorType.Exception)
        {
            return new SiteFailure(SiteError.Offline);
        }

        return Map(response.HttpStatusCode, response.Error, response.HttpResponse?.Headers.RetryAfter);
    }

    // The one place where status codes and error codes become SiteErrors (UPLOADER-API.md "Bearer errors", "Uploading").
    private static SiteFailure Map(HttpStatusCode status, string? code, RetryConditionHeaderValue? retryAfter)
    {
        int number = (int)status;
        var kind = (number, code) switch
        {
            (401, "invalid_token") => SiteError.InvalidToken,
            (403, "may_not_upload") => SiteError.MayNotUpload,
            (413, _) => SiteError.TooLarge,
            (400, "empty_file") => SiteError.EmptyFile,
            (429, _) => SiteError.RateLimited,
            ( >= 500, _) => SiteError.ServerError,
            _ => SiteError.Unexpected,
        };
        return new SiteFailure(kind, code, number, kind == SiteError.RateLimited ? RetryDelay(retryAfter) : null);
    }

    private static TimeSpan? RetryDelay(RetryConditionHeaderValue? retryAfter) =>
        retryAfter?.Delta ?? (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync(OAuthJson.Default.ErrorJson, cancellationToken).ConfigureAwait(false);
            return error?.Error;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or HttpRequestException)
        {
            return null;
        }
    }

    private static bool IsOffline(Exception e, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested && e is HttpRequestException or TaskCanceledException or IOException;
}

internal sealed record ErrorJson(string? Error, string? ErrorDescription);

internal sealed record MeJson(string? BattleTag, string? SiteName, string? DeviceName, string? AppVersion, long CreatedAt, long? LastUploadAt);

internal sealed record CountsJson(int Characters, int Friends);

internal sealed record UploadJson(
    long UploadId,
    long UploadedAt,
    string? Status,
    string? Rejection,
    int? SchemaVersion,
    string? SummaryLine,
    string[]? Highlights,
    CountsJson? Counts,
    string? ReportUrl);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(MeJson))]
[JsonSerializable(typeof(UploadJson))]
internal sealed partial class SiteJson : JsonSerializerContext;

// OAuth errors use snake_case keys (RFC 6749 §5.2).
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(ErrorJson))]
internal sealed partial class OAuthJson : JsonSerializerContext;
