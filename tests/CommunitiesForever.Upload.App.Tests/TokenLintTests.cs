using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CommunitiesForever.Upload.App.Tests;

/// <summary>
/// No raw colours, sizes, spacing, radii or durations outside Tokens.axaml (CLAUDE.md rule 6).
/// Checks every .axaml file of the App and the App's C# code.
/// </summary>
public sealed partial class TokenLintTests
{
    private static readonly HashSet<string> SizedProperties =
    [
        "FontSize", "Margin", "Padding", "Spacing", "ItemSpacing", "LineSpacing",
        "Width", "Height", "MinWidth", "MinHeight", "MaxWidth", "MaxHeight",
        "CornerRadius", "BorderThickness", "StrokeThickness", "Opacity", "LetterSpacing", "LineHeight",
        "LetterSpacingEm", "LineHeightEm", "Duration", "Delay", "Canvas.Left", "Canvas.Top",
    ];

    private static readonly HashSet<string> ColourProperties =
    [
        "Background", "Foreground", "Fill", "Stroke", "BorderBrush", "Color", "Brush",
    ];

    [Fact]
    public void AxamlUsesOnlyTokens()
    {
        string app = Path.Combine(TokenDriftTests.RepoRoot(), "src", "CommunitiesForever.Upload.App");
        var problems = Directory.EnumerateFiles(app, "*.axaml", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(f) && Path.GetFileName(f) != "Tokens.axaml")
            .SelectMany(f => LintAxaml(XDocument.Load(f)).Select(p => $"{Path.GetRelativePath(app, f)}: {p}"))
            .ToList();

        Assert.Empty(problems);
    }

    [Fact]
    public void CodeUsesOnlyTokens()
    {
        string app = Path.Combine(TokenDriftTests.RepoRoot(), "src", "CommunitiesForever.Upload.App");
        var problems = Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(f))
            .SelectMany(f => LintCode(File.ReadAllText(f)).Select(p => $"{Path.GetRelativePath(app, f)}: {p}"))
            .ToList();

        Assert.Empty(problems);
    }

    [Fact]
    public void LintCatchesRawValues()
    {
        var xaml = XDocument.Parse("""
            <Styles xmlns="https://github.com/avaloniaui">
              <Border Background="#FF0000" Padding="12" Margin="0" CornerRadius="{StaticResource RCard}" />
              <TextBlock Foreground="Red" FontSize="13" Classes="meta" />
              <Style><Setter Property="Opacity" Value="0.5" /><Setter Property="Width" Value="{StaticResource S4}" /></Style>
              <BrushTransition Duration="0:0:0.2" />
            </Styles>
            """);

        var problems = LintAxaml(xaml).ToList();

        Assert.Equal(6, problems.Count);
        Assert.Equal(4, LintCode("Color.Parse(\"#fff\"); Colors.Red; new Thickness(4); TimeSpan.FromMilliseconds(120); new Thickness(0);").Count());
    }

    private static IEnumerable<string> LintAxaml(XDocument xaml)
    {
        foreach (var element in xaml.Descendants())
        {
            if (element.Name.LocalName == "Setter"
                && element.Attribute("Property")?.Value is { } property
                && element.Attribute("Value")?.Value is { } setterValue
                && Problem(property, setterValue) is { } setterProblem)
            {
                yield return $"<Setter Property=\"{property}\"> {setterProblem}";
            }

            foreach (var attribute in element.Attributes())
            {
                if (Problem(attribute.Name.LocalName, attribute.Value) is { } problem)
                {
                    yield return $"<{element.Name.LocalName} {attribute.Name.LocalName}> {problem}";
                }
            }
        }
    }

    private static string? Problem(string property, string value)
    {
        value = value.Trim();
        if (value.StartsWith('{'))
        {
            return null;
        }

        string name = property.Contains("Typography.", StringComparison.Ordinal) ? property[(property.LastIndexOf('.') + 1)..] : property;

        if (HexColour().IsMatch(value))
        {
            return $"raw colour {value}";
        }

        if (ColourProperties.Contains(name) && value != "Transparent")
        {
            return $"raw colour {value}";
        }

        if (SizedProperties.Contains(name) && RawNumber().IsMatch(value) && !ZeroOnly().IsMatch(value))
        {
            return $"raw value {value}";
        }

        return null;
    }

    private static IEnumerable<string> LintCode(string code) =>
        RawCode().Matches(code).Select(m => $"raw value {m.Value}");

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    [GeneratedRegex("^#[0-9a-fA-F]{3,8}$")]
    private static partial Regex HexColour();

    // Numbers, number lists (thickness) and time spans.
    [GeneratedRegex(@"^(-?[\d.]+(\s*,\s*-?[\d.]+)*|\d+:\d+:[\d.]+)$")]
    private static partial Regex RawNumber();

    [GeneratedRegex(@"^[0.,\s]+$")]
    private static partial Regex ZeroOnly();

    [GeneratedRegex(@"Color\.(Parse|FromRgb|FromArgb|FromUInt32)\(|Colors\.\w+|new (Thickness|CornerRadius)\(\s*[1-9]|TimeSpan\.From\w+\(\s*[1-9]")]
    private static partial Regex RawCode();
}
