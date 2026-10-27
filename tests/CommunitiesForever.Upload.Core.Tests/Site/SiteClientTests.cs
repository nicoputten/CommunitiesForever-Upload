using System.Net;
using CommunitiesForever.Upload.Core.Site;

namespace CommunitiesForever.Upload.Core.Tests.Site;

public sealed class SiteClientTests
{
    private static readonly AccessToken Token = new("cfu_3q2-7wEXAMPLEexampleEXAMPLEexampleEXAMPLExy");
    private static readonly SiteOptions Options = new(new Uri("https://wow.vestras.nl/"), "1.2.3");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void ContractHasTheExamplesThesTestsUse()
    {
        string[] used = ["error", "device-authorization", "token", "upload-ok", "upload-rejected", "me"];

        Assert.All(used, name => Assert.Contains(name, ContractExamples.Names));
    }

    [Fact]
    public async Task DeviceCodeRequestSendsTheFormAndReadsTheAnswer()
    {
        var http = new FakeHttp(_ => FakeHttp.Json(HttpStatusCode.OK, ContractExamples.Get("device-authorization")));
        var client = Client(http);

        var result = await client.RequestDeviceCodeAsync("DESKTOP-NICO", Ct);

        var (request, body) = Assert.Single(http.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://wow.vestras.nl/api/uploader/v1/device_authorization", request.RequestUri!.AbsoluteUri);
        Assert.Contains("client_id=communitiesforever-uploader", body, StringComparison.Ordinal);
        Assert.Contains("device_name=DESKTOP-NICO", body, StringComparison.Ordinal);
        Assert.Contains("app_version=1.2.3", body, StringComparison.Ordinal);

        Assert.True(result.IsOk);
        var code = result.Value!;
        Assert.Equal("KXRT-7QMN", code.UserCode);
        Assert.Equal(new Uri("https://wow.vestras.nl/uploader?code=KXRT-7QMN"), code.VerificationUriComplete);
        Assert.Equal(TimeSpan.FromSeconds(600), code.ExpiresIn);
        Assert.Equal(TimeSpan.FromSeconds(5), code.Interval);
        Assert.Equal(new DeviceCode("kq3V0dXyPZ8m1nR2c9WbTg"), code.DeviceCode);
    }

    [Fact]
    public async Task LongDeviceNamesAreCutTo64Characters()
    {
        var http = new FakeHttp(_ => FakeHttp.Json(HttpStatusCode.OK, ContractExamples.Get("device-authorization")));

        await Client(http).RequestDeviceCodeAsync(new string('X', 80), Ct);

        Assert.Contains("device_name=" + new string('X', 64) + "&", http.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GrantedPollReturnsTheToken()
    {
        var http = new FakeHttp(_ => FakeHttp.Json(HttpStatusCode.OK, ContractExamples.Get("token")));

        var result = await Client(http).PollTokenAsync(new DeviceCode("kq3V0dXyPZ8m1nR2c9WbTg"), Ct);

        Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code", http.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("device_code=kq3V0dXyPZ8m1nR2c9WbTg", http.Requests[0].Body, StringComparison.Ordinal);
        Assert.Equal(PollStatus.Granted, result.Value!.Status);
        Assert.Equal(Token, result.Value.Token);
    }

    [Theory]
    [InlineData("authorization_pending", PollStatus.Pending)]
    [InlineData("slow_down", PollStatus.SlowDown)]
    [InlineData("access_denied", PollStatus.Denied)]
    [InlineData("expired_token", PollStatus.Expired)]
    public async Task PollErrorsBecomeStatuses(string error, PollStatus expected)
    {
        var http = new FakeHttp(_ => FakeHttp.Error(HttpStatusCode.BadRequest, error));

        var result = await Client(http).PollTokenAsync(new DeviceCode("x"), Ct);

        Assert.Equal(expected, result.Value!.Status);
        Assert.Null(result.Value.Token);
    }

    [Fact]
    public async Task IdentitySendsTheBearerTokenAndReadsMe()
    {
        var http = new FakeHttp(_ => FakeHttp.Json(HttpStatusCode.OK, ContractExamples.Get("me")));

        var result = await Client(http).GetIdentityAsync(Token, Ct);

        var request = http.Requests[0].Request;
        Assert.Equal("https://wow.vestras.nl/api/uploader/v1/me", request.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal(Token.Reveal(), request.Headers.Authorization.Parameter);
        var me = result.Value!;
        Assert.Equal("Nico#2201", me.BattleTag);
        Assert.Equal("Doombringers", me.SiteName);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793000000), me.CreatedAt);
        Assert.Null(me.LastUploadAt);
    }

    [Fact]
    public async Task UploadSendsTheBytesAsTheyAreAndReadsTheReceipt()
    {
        var http = new FakeHttp(_ => FakeHttp.Json(HttpStatusCode.OK, ContractExamples.Get("upload-ok")));
        using var file = new MemoryStream("CommunitiesForeverDB = {}"u8.ToArray());

        var result = await Client(http).UploadAsync(Token, file, DateTimeOffset.FromUnixTimeSeconds(1792999000), Ct);

        var (request, body) = http.Requests[0];
        Assert.Equal("https://wow.vestras.nl/api/uploader/v1/uploads", request.RequestUri!.AbsoluteUri);
        Assert.Equal("application/octet-stream", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("CommunitiesForeverDB = {}", body);
        Assert.Equal("1.2.3", Assert.Single(request.Headers.GetValues("X-Uploader-Version")));
        Assert.Equal("1792999000", Assert.Single(request.Headers.GetValues("X-File-Saved-At")));

        var receipt = result.Value!;
        Assert.Equal(UploadStatus.Ok, receipt.Status);
        Assert.Equal(42, receipt.UploadId);
        Assert.Null(receipt.Rejection);
        Assert.Equal("5 levels gained · 4 skill gains · 1 new character", receipt.SummaryLine);
        Assert.Equal(3, receipt.Highlights.Count);
        Assert.Equal(new UploadCounts(10, 5), receipt.Counts);
        Assert.Equal(new Uri("https://wow.vestras.nl/upload/42"), receipt.ReportUrl);
    }

    [Fact]
    public async Task RejectedUploadKeepsTheReason()
    {
        var http = new FakeHttp(_ => FakeHttp.Json(HttpStatusCode.OK, ContractExamples.Get("upload-rejected")));
        using var file = new MemoryStream([1]);

        var receipt = (await Client(http).UploadAsync(Token, file, null, Ct)).Value!;

        Assert.Equal(UploadStatus.Rejected, receipt.Status);
        Assert.Equal(UploadReceipt.Rejections.NewerFormat, receipt.Rejection);
        Assert.Equal(3, receipt.SchemaVersion);
        Assert.Null(receipt.Counts);
        Assert.False(http.Requests[0].Request.Headers.Contains("X-File-Saved-At"));
    }

    [Fact]
    public async Task UnknownUploadStatusCountsAsNotUploaded()
    {
        string json = ContractExamples.Get("upload-ok").Replace("\"status\": \"ok\"", "\"status\": \"quarantined\"", StringComparison.Ordinal);
        var http = new FakeHttp(_ => FakeHttp.Json(HttpStatusCode.OK, json));
        using var file = new MemoryStream([1]);

        var receipt = (await Client(http).UploadAsync(Token, file, null, Ct)).Value!;

        Assert.Equal(UploadStatus.Rejected, receipt.Status);
        Assert.Equal("quarantined", receipt.Rejection);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "invalid_token", SiteError.InvalidToken)]
    [InlineData(HttpStatusCode.Forbidden, "may_not_upload", SiteError.MayNotUpload)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, "too_large", SiteError.TooLarge)]
    [InlineData(HttpStatusCode.BadRequest, "empty_file", SiteError.EmptyFile)]
    [InlineData(HttpStatusCode.InternalServerError, "internal", SiteError.ServerError)]
    [InlineData(HttpStatusCode.Unauthorized, "invalid_request", SiteError.Unexpected)]
    [InlineData(HttpStatusCode.Conflict, "something_new", SiteError.Unexpected)]
    public async Task ErrorsAreMappedOnce(HttpStatusCode status, string code, SiteError expected)
    {
        var http = new FakeHttp(_ => FakeHttp.Error(status, code));
        using var file = new MemoryStream([1]);

        var result = await Client(http).UploadAsync(Token, file, null, Ct);

        Assert.False(result.IsOk);
        Assert.Equal(expected, result.Failure!.Kind);
        Assert.Equal(code, result.Failure.Code);
        Assert.Equal((int)status, result.Failure.Status);
    }

    [Fact]
    public async Task ContractErrorExampleIsReadable()
    {
        var http = new FakeHttp(_ => FakeHttp.Json(HttpStatusCode.Unauthorized, ContractExamples.Get("error")));

        var result = await Client(http).GetIdentityAsync(Token, Ct);

        Assert.Equal(SiteError.InvalidToken, result.Failure!.Kind);
    }

    [Fact]
    public async Task RateLimitCarriesRetryAfter()
    {
        var http = new FakeHttp(_ =>
        {
            var response = FakeHttp.Error(HttpStatusCode.TooManyRequests, "rate_limited");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
            return response;
        });
        using var file = new MemoryStream([1]);

        var result = await Client(http).UploadAsync(Token, file, null, Ct);

        Assert.Equal(SiteError.RateLimited, result.Failure!.Kind);
        Assert.Equal(TimeSpan.FromSeconds(120), result.Failure.RetryAfter);
    }

    [Fact]
    public async Task NoNetworkIsOfflineForEveryCall()
    {
        var client = new SiteClient(new HttpClient(new OfflineHttp()), Options);
        using var file = new MemoryStream([1]);

        Assert.Equal(SiteError.Offline, (await client.RequestDeviceCodeAsync("PC", Ct)).Failure!.Kind);
        Assert.Equal(SiteError.Offline, (await client.PollTokenAsync(new DeviceCode("x"), Ct)).Failure!.Kind);
        Assert.Equal(SiteError.Offline, (await client.GetIdentityAsync(Token, Ct)).Failure!.Kind);
        Assert.Equal(SiteError.Offline, (await client.UploadAsync(Token, file, null, Ct)).Failure!.Kind);
        Assert.Equal(SiteError.Offline, (await client.RevokeAsync(Token, Ct)).Failure!.Kind);
    }

    [Fact]
    public async Task RevokeSendsTheTokenAsAForm()
    {
        var http = new FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var result = await Client(http).RevokeAsync(Token, Ct);

        var (request, body) = http.Requests[0];
        Assert.Equal("https://wow.vestras.nl/api/uploader/v1/revoke", request.RequestUri!.AbsoluteUri);
        Assert.Contains("token=" + Token.Reveal(), body, StringComparison.Ordinal);
        Assert.Contains("token_type_hint=access_token", body, StringComparison.Ordinal);
        Assert.Contains("client_id=communitiesforever-uploader", body, StringComparison.Ordinal);
        Assert.Null(request.Headers.Authorization);
        Assert.True(result.IsOk);
    }

    [Fact]
    public async Task SiteUnderAPathKeepsThePath()
    {
        var http = new FakeHttp(_ => FakeHttp.Json(HttpStatusCode.OK, ContractExamples.Get("me")));
        var client = new SiteClient(new HttpClient(http), Options with { BaseUri = new Uri("http://localhost:3000/") });

        await client.GetIdentityAsync(Token, Ct);

        Assert.Equal("http://localhost:3000/api/uploader/v1/me", http.Requests[0].Request.RequestUri!.AbsoluteUri);
    }

    private static SiteClient Client(HttpMessageHandler handler) => new(new HttpClient(handler), Options);
}
