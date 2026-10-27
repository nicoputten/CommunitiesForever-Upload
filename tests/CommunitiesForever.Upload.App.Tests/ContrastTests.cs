using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace CommunitiesForever.Upload.App.Tests;

/// <summary>
/// WCAG contrast of the token pairs the app uses (web DESIGN.md: AA). Text needs 4.5:1; faint text is only used
/// for secondary or large text and needs 3:1. See-through surfaces are measured over the window background.
/// </summary>
public sealed class ContrastTests
{
    private static readonly string[] Backgrounds = ["BgNight", "BgAbyss", "PageGlow", "SurfaceAside", "SurfaceRaised"];

    private static readonly string[] Text =
        ["Text", "TextStrong", "TextMuted", "TextDim", "TextChip", "Gold", "GoldBright", "FreshText", "StaleText"];

    [AvaloniaFact]
    public void TextIsReadableOnEveryBackground()
    {
        var failures = new List<string>();
        foreach (string background in Backgrounds)
        {
            foreach (string text in Text)
            {
                Check(text, background, 4.5, failures);
            }

            Check("TextFaint", background, 3, failures);
        }

        Check("OnGold", "Gold", 4.5, failures);
        Check("OnGold", "GoldBright", 4.5, failures);

        Assert.Empty(failures);
    }

    [Theory]
    [InlineData("#000000", "#FFFFFF", 21)]
    [InlineData("#777777", "#FFFFFF", 4.48)]
    public void ContrastFollowsWcag(string a, string b, double expected) =>
        Assert.Equal(expected, Ratio(Color.Parse(a), Color.Parse(b)), 2);

    private static void Check(string text, string background, double minimum, List<string> failures)
    {
        Color night = Token("BgNight");
        Color back = Over(Token(background), night);
        double ratio = Ratio(Over(Token(text), back), back);
        if (ratio < minimum)
        {
            failures.Add($"{text} on {background}: {ratio:F2}:1, needs {minimum}:1");
        }
    }

    private static Color Token(string key) =>
        Application.Current!.TryFindResource(key, out object? value) && value is Color colour
            ? colour
            : throw new InvalidOperationException($"No colour token {key}");

    // Alpha-composite a colour over an opaque background.
    private static Color Over(Color top, Color bottom)
    {
        double a = top.A / 255d;
        return Color.FromRgb(
            (byte)Math.Round(top.R * a + bottom.R * (1 - a)),
            (byte)Math.Round(top.G * a + bottom.G * (1 - a)),
            (byte)Math.Round(top.B * a + bottom.B * (1 - a)));
    }

    private static double Ratio(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(Color c) =>
        0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

    private static double Channel(byte value)
    {
        double s = value / 255d;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}
