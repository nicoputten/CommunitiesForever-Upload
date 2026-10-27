using System.Security.Cryptography;
using System.Text;
using CommunitiesForever.Upload.Core.Site;
using Microsoft.Win32;

namespace CommunitiesForever.Upload.Windows.Tests;

/// <summary>Updates and uninstall (UP-10), on test keys, a temp folder and a fake token.</summary>
public sealed class PackagingAdapterTests : IDisposable
{
    private static readonly Uri Feed = new("https://wow.vestras.nl/api/uploader/v1/updates/");

    private readonly string _root = @"Software\CommunitiesForever.Upload.Tests\" + Guid.NewGuid().ToString("N");
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "cfu-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    [Fact]
    public void OnlyAFeedSignedWithTheKeyHolds()
    {
        using var http = new HttpClient();
        var downloader = new VelopackUpdates.BearerDownloader(http, new Tokens(new AccessToken("cfu_test")), Feed, _key);
        byte[] feed = Encoding.UTF8.GetBytes("""{"Assets":[]}""");
        // As the release workflow signs: openssl dgst -sha256 -sign (DER).
        byte[] signature = _key.SignData(feed, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] forged = other.SignData(feed, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        Assert.True(downloader.IsSigned(feed, signature));
        Assert.False(downloader.IsSigned(feed, forged));
        Assert.False(downloader.IsSigned(Encoding.UTF8.GetBytes("""{"Assets":[1]}"""), signature));
    }

    [Fact]
    public void FeedRequestsCarryTheToken()
    {
        using var http = new HttpClient();
        var downloader = new VelopackUpdates.BearerDownloader(http, new Tokens(new AccessToken("cfu_test")), Feed, _key);

        using var request = downloader.Request("https://wow.vestras.nl/api/uploader/v1/updates/releases.win.json");

        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("cfu_test", request.Headers.Authorization?.Parameter);
    }

    [Theory]
    [InlineData("https://example.com/api/uploader/v1/updates/releases.win.json")]
    [InlineData("https://wow.vestras.nl/api/uploader/v1/upload")]
    public void TheTokenNeverLeavesTheFeed(string url)
    {
        using var http = new HttpClient();
        var downloader = new VelopackUpdates.BearerDownloader(http, new Tokens(new AccessToken("cfu_test")), Feed, _key);

        Assert.Throws<InvalidOperationException>(() => downloader.Request(url));
    }

    [Fact]
    public void UninstallRemovesWhatTheAppLeftInWindows()
    {
        string classes = _root + @"\Classes";
        string run = _root + @"\Run";
        string shortcut = Path.Combine(_folder, "Uploader.lnk");
        new RegistryAutostart(@"C:\Apps\Uploader.exe", run).Apply(true);
        ShellRegistration.Register(@"C:\Apps\Uploader.exe", @"C:\Apps\app-icon.png", classes);
        StartMenuShortcut.Ensure(Path.Combine(AppContext.BaseDirectory, "testhost.exe"), shortcut);
        var tokens = new DpapiTokenStore(_folder);
        tokens.Save(new AccessToken("cfu_test"));
        File.WriteAllText(Path.Combine(_folder, "settings.json"), "{}");

        Uninstall.CleanUp(_folder, shortcut, classes, run);

        Assert.False(new RegistryAutostart(@"C:\Apps\Uploader.exe", run).IsEnabled);
        Assert.Null(Registry.CurrentUser.OpenSubKey($@"{classes}\AppUserModelId\{AppIdentity.AppId}"));
        Assert.Null(Registry.CurrentUser.OpenSubKey($@"{classes}\{AppIdentity.Scheme}"));
        Assert.False(File.Exists(shortcut));
        Assert.Null(tokens.Load());
        // Settings stay for a reinstall.
        Assert.True(File.Exists(Path.Combine(_folder, "settings.json")));

        Uninstall.CleanUp(_folder, shortcut, classes, run);
    }

    public void Dispose()
    {
        _key.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_root, throwOnMissingSubKey: false);
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private sealed class Tokens(AccessToken token) : ITokenStore
    {
        public AccessToken? Load() => token;

        public void Save(AccessToken value)
        {
        }

        public void Delete()
        {
        }
    }
}
