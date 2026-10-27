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
        PLACEHOLDER: Nico generates the key pair and pastes the public key here (README "Releasing").
        -----END PUBLIC KEY-----
        """;

    public static ECDsa Load()
    {
        var key = ECDsa.Create();
        try
        {
            key.ImportFromPem(PublicKeyPem);
        }
        catch (ArgumentException)
        {
            // No key yet: nothing verifies, so no update is ever used.
        }

        return key;
    }
}
