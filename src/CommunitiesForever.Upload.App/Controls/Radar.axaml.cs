using Avalonia;
using Avalonia.Controls;

namespace CommunitiesForever.Upload.App.Controls;

/// <summary>The setup board's radar; the sweep turns while <see cref="Scanning"/> (with motion on).</summary>
public sealed partial class Radar : UserControl
{
    public static readonly StyledProperty<bool> ScanningProperty = AvaloniaProperty.Register<Radar, bool>(nameof(Scanning));

    public Radar() => InitializeComponent();

    public bool Scanning
    {
        get => GetValue(ScanningProperty);
        set => SetValue(ScanningProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScanningProperty)
        {
            Sweep.Classes.Set("sweep", change.GetNewValue<bool>());
        }
    }
}
