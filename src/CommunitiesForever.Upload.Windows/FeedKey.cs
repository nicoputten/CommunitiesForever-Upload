using System.Security.Cryptography;

namespace CommunitiesForever.Upload.Windows;

/// <summary>
/// The public half of the key that signs the update feed (ECDSA P-256, UP-10). The private half lives only in the
/// <c>release</c> environment's secret <c>UPLOADER_FEED_SIGNING_KEY</c>; the release workflow signs
/// <c>releases.win.json</c> with it (README "Releasing").
/// </summary>
public static class FeedKey
{
    public const string PublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEpeEWJ+Mo854B2iNl5PMQH5s7nBh/
        eAUOHxyThVzLmV0TUXnf/pC4VsfeuUuCTfDGBEh5Fx89X1DaOGLhP3IGUQ==
        -----END PUBLIC KEY-----
        """;

    public static ECDsa Load()
    {
        var key = ECDsa.Create();
        key.ImportFromPem(PublicKeyPem);
        return key;
    }
}
