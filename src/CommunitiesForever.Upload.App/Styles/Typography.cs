using Avalonia;
using Avalonia.Controls;

namespace CommunitiesForever.Upload.App.Styles;

/// <summary>
/// Letter spacing and line height in em, as the web tokens give them (<c>LsDisplay</c>, <c>LhBody</c>).
/// Avalonia wants pixels, so these follow the TextBlock's font size.
/// </summary>
public static class Typography
{
    public static readonly AttachedProperty<double> LetterSpacingEmProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, double>("LetterSpacingEm", typeof(Typography));

    public static readonly AttachedProperty<double> LineHeightEmProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, double>("LineHeightEm", typeof(Typography));

    static Typography()
    {
        // Class handlers are static: nothing is held per TextBlock (CLAUDE.md rule 11).
        LetterSpacingEmProperty.Changed.AddClassHandler<TextBlock>((t, _) => Apply(t));
        LineHeightEmProperty.Changed.AddClassHandler<TextBlock>((t, _) => Apply(t));
        TextBlock.FontSizeProperty.Changed.AddClassHandler<TextBlock>((t, _) => Apply(t));
    }

    public static double GetLetterSpacingEm(TextBlock element) => element.GetValue(LetterSpacingEmProperty);

    public static void SetLetterSpacingEm(TextBlock element, double value) => element.SetValue(LetterSpacingEmProperty, value);

    public static double GetLineHeightEm(TextBlock element) => element.GetValue(LineHeightEmProperty);

    public static void SetLineHeightEm(TextBlock element, double value) => element.SetValue(LineHeightEmProperty, value);

    private static void Apply(TextBlock text)
    {
        if (text.IsSet(LetterSpacingEmProperty))
        {
            text.LetterSpacing = GetLetterSpacingEm(text) * text.FontSize;
        }

        if (text.IsSet(LineHeightEmProperty))
        {
            text.LineHeight = GetLineHeightEm(text) * text.FontSize;
        }
    }
}
