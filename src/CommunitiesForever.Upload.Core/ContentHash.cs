using System.Security.Cryptography;

namespace CommunitiesForever.Upload.Core;

/// <summary>
/// SHA-256 of the addon file's bytes, used to skip uploading an unchanged file.
/// The bytes are never interpreted (CLAUDE.md rule 1).
/// </summary>
public static class ContentHash
{
    public static async Task<string> ComputeAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var hash = await SHA256.HashDataAsync(content, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }
}
