using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace CommunitiesForever.Upload.App.DesignChecks;

/// <summary>
/// Compares a rendered window with its baseline in Baselines/. Baselines are made on CI (windows-latest) and
/// committed only after Nico approved the look (CLAUDE.md rule 7). On a difference the actual, expected and diff
/// images go to TestResults/design-checks, which CI uploads as an artifact.
/// UPDATE_BASELINES=1 writes the actual image as the new baseline (local iteration only).
/// </summary>
internal static class Screenshot
{
    // A pixel differs when a channel is off by more than this (0..255) ...
    private const int ChannelTolerance = 8;

    // ... and an image differs when more than this share of its pixels differ.
    private const double MaxDifferentShare = 0.001;

    public static void Matches(Window window, string name)
    {
        byte[] actualPng = Capture(window);
        string baseline = Path.Combine(Paths.Baselines, name + ".png");

        if (Environment.GetEnvironmentVariable("UPDATE_BASELINES") == "1")
        {
            File.WriteAllBytes(baseline, actualPng);
            return;
        }

        if (!File.Exists(baseline))
        {
            string written = Save(name + ".actual.png", actualPng);
            Assert.Fail($"No baseline for '{name}'. The rendered image is at {written}; commit it as {baseline} after Nico approved it.");
        }

        using var expected = SKBitmap.Decode(baseline);
        using var actual = SKBitmap.Decode(actualPng);
        string? problem = Compare(expected, actual, out SKBitmap? diff);
        if (problem is null)
        {
            return;
        }

        Save(name + ".actual.png", actualPng);
        Save(name + ".expected.png", File.ReadAllBytes(baseline));
        if (diff is not null)
        {
            using (diff)
            {
                Save(name + ".diff.png", Encode(diff));
            }
        }

        Assert.Fail($"'{name}' differs from its baseline: {problem}. Images in {Paths.Output}.");
    }

    private static byte[] Capture(Window window)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Nothing was rendered");
        using var stream = new MemoryStream();
        frame.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    private static string? Compare(SKBitmap expected, SKBitmap actual, out SKBitmap? diff)
    {
        diff = null;
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            return $"size {actual.Width} x {actual.Height}, baseline {expected.Width} x {expected.Height}";
        }

        SKColor[] a = expected.Pixels;
        SKColor[] b = actual.Pixels;
        var marks = new SKColor[a.Length];
        int different = 0;
        for (int i = 0; i < a.Length; i++)
        {
            bool differs = Math.Abs(a[i].Red - b[i].Red) > ChannelTolerance
                || Math.Abs(a[i].Green - b[i].Green) > ChannelTolerance
                || Math.Abs(a[i].Blue - b[i].Blue) > ChannelTolerance
                || Math.Abs(a[i].Alpha - b[i].Alpha) > ChannelTolerance;
            if (differs)
            {
                different++;
            }

            // Diff image: changed pixels in red over a faded copy of the baseline.
            marks[i] = differs ? SKColors.Red : a[i].WithAlpha(60);
        }

        double share = (double)different / a.Length;
        if (share <= MaxDifferentShare)
        {
            return null;
        }

        diff = new SKBitmap(expected.Width, expected.Height) { Pixels = marks };
        return $"{different} pixels ({share:P2}) differ";
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string Save(string fileName, byte[] png)
    {
        Directory.CreateDirectory(Paths.Output);
        string path = Path.Combine(Paths.Output, fileName);
        File.WriteAllBytes(path, png);
        return path;
    }
}

internal static class Paths
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string Baselines => Path.Combine(RepoRoot, "tests", "CommunitiesForever.Upload.App.DesignChecks", "Baselines");

    public static string Output => Path.Combine(RepoRoot, "TestResults", "design-checks");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CommunitiesForever.Upload.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }
}
