using Avalonia.Controls;

namespace CommunitiesForever.Upload.App;

/// <summary>Every base component in every state, for the design checks (UP-03). Debug builds: start with --gallery.</summary>
internal sealed partial class GalleryWindow : Window
{
    public GalleryWindow() => InitializeComponent();
}
