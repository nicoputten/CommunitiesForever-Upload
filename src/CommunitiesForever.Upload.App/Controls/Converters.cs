using Avalonia.Data.Converters;

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
