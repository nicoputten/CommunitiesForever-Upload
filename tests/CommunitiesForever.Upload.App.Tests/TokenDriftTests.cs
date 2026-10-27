using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CommunitiesForever.Upload.App.Tests;

/// <summary>
/// Tokens.axaml mirrors the web's tokens.css (CLAUDE.md rule 6). Compares it with the committed copy
/// design/tokens/web-tokens.css (refresh: scripts/refresh-tokens.ps1), so the two can't drift silently.
/// </summary>
public sealed partial class TokenDriftTests
{
    // Mirrored: Part 1 and the general blocks of Part 2, up to the first page-specific block.
    private const string MirrorStart = "/* ── Part 1";
    private const string MirrorEnd = "/* roster (WEB-21)";

    // Not mirrored: fonts are embedded (FontDisplay, FontName, FontUi), blur and glow are an approved deviation.
    private static readonly Regex NotMirrored = NotMirroredRegex();

    private static readonly HashSet<string> AppOnlyKeys = ["FontDisplay", "FontName", "FontUi", "FontMono"];

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void EveryWebTokenIsMirroredWithTheSameValue()
    {
        var tokens = AppTokens();
        var problems = new List<string>();

        foreach (var (name, value) in WebTokens())
        {
            string key = KeyFor(name);
            if (!tokens.TryGetValue(key, out var element))
            {
                problems.Add($"{name}: missing {key}");
                continue;
            }

            string? problem = Compare(name, value, element, tokens);
            if (problem is not null)
            {
                problems.Add($"{name} ({key}): {problem}");
            }
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void EveryAppTokenIsAWebTokenOrAppOnly()
    {
        var webKeys = WebTokens().Select(t => KeyFor(t.Name)).ToHashSet();

        var unknown = AppTokens().Keys
            .Where(key => !AppOnlyKeys.Contains(key) && !key.StartsWith("App", StringComparison.Ordinal))
            .Where(key => !webKeys.Contains(key)
                && !webKeys.Contains(TrimSuffix(key, "Brush"))
                && !webKeys.Contains(TrimSuffix(key, "Thickness")))
            .ToList();

        Assert.Empty(unknown);
    }

    [Theory]
    [InlineData("--text-muted", "TextMuted")]
    [InlineData("--s-0-5", "S0_5")]
    [InlineData("--fs-display-xl", "FsDisplayXl")]
    [InlineData("--s-12", "S12")]
    public void KeysAreTheCssNameInPascalCase(string css, string key) => Assert.Equal(key, KeyFor(css));

    internal static string KeyFor(string cssName)
    {
        string[] parts = cssName.TrimStart('-').Split('-');
        var key = new System.Text.StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0 && char.IsDigit(parts[i][0]) && char.IsDigit(parts[i - 1][0]))
            {
                key.Append('_');
            }

            key.Append(char.ToUpperInvariant(parts[i][0])).Append(parts[i].AsSpan(1));
        }

        return key.ToString();
    }

    private static string? Compare(string name, string css, XElement element, Dictionary<string, XElement> tokens)
    {
        string kind = element.Name.LocalName;
        string text = element.Value.Trim();

        if (ParseCssColour(css) is { } colour)
        {
            if (kind != "Color" || !SameColour(colour, ParseHex(text)))
            {
                return $"expected colour {colour:X8}, found {kind} {text}";
            }

            string? brush = tokens.GetValueOrDefault(KeyFor(name) + "Brush")?.Attribute("Color")?.Value;
            return brush is not null && SameColour(colour, ParseHex(brush)) ? null : "brush missing or different";
        }

        if (name.StartsWith("--shadow-", StringComparison.Ordinal))
        {
            return kind == "BoxShadows" && text == CssShadowToAvalonia(css) ? null : $"expected {CssShadowToAvalonia(css)}, found {text}";
        }

        if (css.StartsWith("cubic-bezier(", StringComparison.Ordinal))
        {
            string[] p = css["cubic-bezier(".Length..^1].Split(',', StringSplitOptions.TrimEntries);
            string[] found = ["X1", "Y1", "X2", "Y2"];
            return kind == "SplineEasing" && found.Select(a => element.Attribute(a)?.Value).SequenceEqual(p) ? null : "easing differs";
        }

        if (name.StartsWith("--fw-", StringComparison.Ordinal))
        {
            string expected = css switch { "400" => "Normal", "500" => "Medium", "600" => "SemiBold", _ => css };
            return kind == "FontWeight" && text == expected ? null : $"expected {expected}, found {text}";
        }

        if (css.EndsWith("ms", StringComparison.Ordinal) || Regex.IsMatch(css, @"^[\d.]+s$"))
        {
            double ms = css.EndsWith("ms", StringComparison.Ordinal) ? Number(css[..^2]) : Number(css[..^1]) * 1000;
            return kind == "TimeSpan" && TimeSpan.Parse(text, CultureInfo.InvariantCulture).TotalMilliseconds == ms
                ? null : $"expected {ms} ms, found {text}";
        }

        double value = Number(css.Replace("px", "", StringComparison.Ordinal).Replace("em", "", StringComparison.Ordinal));
        if (name.StartsWith("--r-", StringComparison.Ordinal))
        {
            return kind == "CornerRadius" && Number(text) == value ? null : $"expected radius {value}, found {kind} {text}";
        }

        if (kind != "Double" || Number(text) != value)
        {
            return $"expected {value}, found {kind} {text}";
        }

        // Spacing always has a Thickness twin; other sizes may have one (Border, FocusWidth).
        string? thickness = tokens.GetValueOrDefault(KeyFor(name) + "Thickness")?.Value;
        if (thickness is null)
        {
            return name.StartsWith("--s-", StringComparison.Ordinal) ? "thickness missing" : null;
        }

        return Number(thickness) == value ? null : $"thickness {thickness} differs";
    }

    private static List<(string Name, string Value)> WebTokens()
    {
        string css = File.ReadAllText(Path.Combine(RepoRoot(), "design", "tokens", "web-tokens.css"));
        int start = css.IndexOf(MirrorStart, StringComparison.Ordinal);
        int end = css.IndexOf(MirrorEnd, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "web-tokens.css no longer has the expected Part 1 / roster markers");

        return CssToken().Matches(css[start..end])
            .Select(m => (Name: m.Groups[1].Value, Value: m.Groups[2].Value.Trim()))
            .Where(t => !NotMirrored.IsMatch(t.Name))
            .ToList();
    }

    private static Dictionary<string, XElement> AppTokens()
    {
        var xaml = XDocument.Load(Path.Combine(RepoRoot(), "src", "CommunitiesForever.Upload.App", "Styles", "Tokens.axaml"));
        return xaml.Root!.Elements()
            .Where(e => e.Attribute(X + "Key") is not null)
            .ToDictionary(e => e.Attribute(X + "Key")!.Value);
    }

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CommunitiesForever.Upload.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }

    private static uint? ParseCssColour(string css)
    {
        if (Regex.IsMatch(css, "^#[0-9a-fA-F]{6}$"))
        {
            return 0xFF000000 | uint.Parse(css[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        var m = Regex.Match(css, @"^rgba\(\s*(\d+),\s*(\d+),\s*(\d+),\s*([\d.]+)\s*\)$");
        if (!m.Success)
        {
            return null;
        }

        uint a = (uint)Math.Round(Number(m.Groups[4].Value) * 255);
        return (a << 24) | (uint.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) << 16)
            | (uint.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) << 8) | uint.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
    }

    private static uint ParseHex(string hex) => uint.Parse(hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    // Alpha may differ by one step from rounding 0..1 to 0..255.
    private static bool SameColour(uint expected, uint found) =>
        (expected & 0xFFFFFF) == (found & 0xFFFFFF) && Math.Abs((int)(expected >> 24) - (int)(found >> 24)) <= 1;

    private static string CssShadowToAvalonia(string css) => string.Join(", ",
        ShadowSplit().Split(css).Select(part =>
        {
            bool inset = part.Trim().StartsWith("inset", StringComparison.Ordinal);
            var m = Regex.Match(part.Replace("inset", "", StringComparison.Ordinal).Trim(), @"^(-?[\d.]+)(?:px)?\s+(-?[\d.]+)(?:px)?\s+(-?[\d.]+)(?:px)?\s+(rgba\([^)]*\))$");
            return $"{(inset ? "inset " : "")}{m.Groups[1].Value} {m.Groups[2].Value} {m.Groups[3].Value} 0 #{ParseCssColour(m.Groups[4].Value):X8}";
        }));

    private static double Number(string s) => double.Parse(s.Trim(), CultureInfo.InvariantCulture);

    private static string TrimSuffix(string key, string suffix) =>
        key.EndsWith(suffix, StringComparison.Ordinal) ? key[..^suffix.Length] : key;

    [GeneratedRegex(@"(--[\w-]+):\s*([^;]+);")]
    private static partial Regex CssToken();

    [GeneratedRegex(@"^--(font-|blur-|glow-|shadow-current$|text-glow$)")]
    private static partial Regex NotMirroredRegex();

    [GeneratedRegex(@",\s*(?![^()]*\))")]
    private static partial Regex ShadowSplit();
}
