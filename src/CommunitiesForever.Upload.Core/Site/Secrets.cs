using System.Text.RegularExpressions;

namespace CommunitiesForever.Upload.Core.Site;

/// <summary>
/// A value that must never end up in logs, exceptions or crash reports (CLAUDE.md rule 4):
/// <see cref="ToString"/> hides it, <see cref="Reveal"/> hands it to the one place that sends it.
/// </summary>
public abstract class Secret : IEquatable<Secret>
{
    private readonly string _value;

    protected Secret(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _value = value;
    }

    public string Reveal() => _value;

    public override string ToString() => $"{GetType().Name}(hidden)";

    public bool Equals(Secret? other) => other is not null && other.GetType() == GetType() && other._value == _value;

    public override bool Equals(object? obj) => Equals(obj as Secret);

    public override int GetHashCode() => _value.GetHashCode(StringComparison.Ordinal);
}

/// <summary>The upload token (UPLOADER-API.md "Access tokens"): <c>cfu_</c> and 43 base64url characters.</summary>
public sealed partial class AccessToken(string value) : Secret(value)
{
    /// <summary>Checks a pasted token's format before it is tried against the site.</summary>
    public static bool TryParse(string? text, out AccessToken? token)
    {
        string trimmed = text?.Trim() ?? "";
        token = Format().IsMatch(trimmed) ? new AccessToken(trimmed) : null;
        return token is not null;
    }

    [GeneratedRegex("^cfu_[A-Za-z0-9_-]{43}$")]
    private static partial Regex Format();
}

/// <summary>RFC 8628 device code: whoever holds it collects the token, so it stays in memory only.</summary>
public sealed class DeviceCode(string value) : Secret(value);
