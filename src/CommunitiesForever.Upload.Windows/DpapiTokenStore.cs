using System.Security.Cryptography;
using System.Text;
using CommunitiesForever.Upload.Core.Site;

namespace CommunitiesForever.Upload.Windows;

/// <summary>
/// The upload token, encrypted with DPAPI for the current Windows user (CLAUDE.md rule 4), in token.bin.
/// Only this user on this PC can read it; it never appears in settings or logs.
/// </summary>
internal sealed class DpapiTokenStore(string directory) : ITokenStore
{
    // Extra entropy: other apps of the same user can't decrypt the file with a plain DPAPI call.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CommunitiesForever.Upload/token/v1");

    private string FilePath => Path.Combine(directory, "token.bin");

    public AccessToken? Load()
    {
        try
        {
            byte[] encrypted = File.ReadAllBytes(FilePath);
            byte[] plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return new AccessToken(Encoding.UTF8.GetString(plain));
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or CryptographicException or ArgumentException)
        {
            // Missing, or unreadable (another user, another PC, damaged): not connected.
            return null;
        }
    }

    public void Save(AccessToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        Directory.CreateDirectory(directory);
        byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(token.Reveal()), Entropy, DataProtectionScope.CurrentUser);
        string temp = FilePath + ".tmp";
        File.WriteAllBytes(temp, encrypted);
        File.Move(temp, FilePath, overwrite: true);
    }

    public void Delete()
    {
        // File.Delete throws when the folder is missing; nothing to delete then.
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }
}
