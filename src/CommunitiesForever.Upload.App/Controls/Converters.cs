using Avalonia.Data.Converters;
using CommunitiesForever.Upload.App.ViewModels;

namespace CommunitiesForever.Upload.App.Controls;

/// <summary>The step rail's mark: the number, or a check once the step is done.</summary>
public static class StepMark
{
    public static readonly IValueConverter One = new FuncValueConverter<bool, string>(done => done ? "✓" : "1");

    public static readonly IValueConverter Two = new FuncValueConverter<bool, string>(done => done ? "✓" : "2");
}

/// <summary>Texts that switch with a flag.</summary>
public static class Choose
{
    public static readonly IValueConverter CopyLink =
        new FuncValueConverter<bool, string>(copied => copied ? "Link copied" : "Browser didn't open? Copy the link");

    public static readonly IValueConverter ScanTitle =
        new FuncValueConverter<bool, string>(scanning => scanning ? "Looking for your game…" : "Found your game");
}

/// <summary>A <see cref="Tone"/> as style classes (fresh, gold, playing, dim, warn).</summary>
/// <summary>The Runs block's badge classes (UP-12).</summary>
public static class RunsBadges
{
    public static readonly IValueConverter Reload = new FuncValueConverter<RunsBadgeKind, bool>(k => k == RunsBadgeKind.Reload);

    public static readonly IValueConverter Warn = new FuncValueConverter<RunsBadgeKind, bool>(k => k == RunsBadgeKind.Warn);
}

public static class Tones
{
    public static readonly IValueConverter Fresh = new FuncValueConverter<Tone, bool>(t => t == Tone.Fresh);

    public static readonly IValueConverter Gold = new FuncValueConverter<Tone, bool>(t => t == Tone.Gold);

    public static readonly IValueConverter Playing = new FuncValueConverter<Tone, bool>(t => t == Tone.Playing);

    public static readonly IValueConverter Dim = new FuncValueConverter<Tone, bool>(t => t == Tone.Dim);

    public static readonly IValueConverter Warn = new FuncValueConverter<Tone, bool>(t => t == Tone.Warn);
}
