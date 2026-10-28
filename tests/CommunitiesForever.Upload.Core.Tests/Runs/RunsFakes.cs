using System.Text.Json;
using CommunitiesForever.Upload.Core.Runs;
using CommunitiesForever.Upload.Core.Site;

namespace CommunitiesForever.Upload.Core.Tests.Runs;

// Fakes for the runs to the game (UP-12); linked into the app's tests and design checks.
internal sealed class RunsSite : ISiteClient
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public string Feed { get; set; } = """{ "v": 1, "updatedAt": 1, "runs": [ { "id": "r-1" }, { "id": "r-2" }, { "id": "r-3" } ] }""";

    public bool Fail { get; set; }

    public Task<SiteResult<RunsFeed>> GetRunsAsync(AccessToken token, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult<SiteResult<RunsFeed>>(Fail
            ? new SiteFailure(SiteError.Offline)
            : SiteResult.Ok(new RunsFeed(JsonDocument.Parse(Feed).RootElement.Clone())));
    }

    public Task<SiteResult<DeviceAuthorization>> RequestDeviceCodeAsync(string deviceName, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<TokenPoll>> PollTokenAsync(DeviceCode deviceCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<SiteIdentity>> GetIdentityAsync(AccessToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<UploadReceipt>> UploadAsync(AccessToken token, Stream content, DateTimeOffset? savedAt, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<bool>> RevokeAsync(AccessToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SiteResult<UpdateStatus>> GetUpdateStatusAsync(AccessToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class FakeRunsFiles : IRunsFiles
{
    private int _writes;

    public AddonFolder Folder { get; set; } = AddonFolder.Ready;

    public byte[]? Content { get; set; } = "CF_RUNS = nil\n"u8.ToArray();

    public int Writes => Volatile.Read(ref _writes);

    public string PathFor(string foreverPath) => foreverPath + @"\Interface\AddOns\CommunitiesForever\Data\Runs.lua";

    public AddonFolder Inspect(string foreverPath) => Folder;

    public byte[]? Read(string foreverPath) => Content;

    public void Write(string foreverPath, byte[] content)
    {
        Content = content;
        if (Folder == AddonFolder.NoFile)
        {
            Folder = AddonFolder.Ready;
        }

        Interlocked.Increment(ref _writes);
    }
}
