using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace CommunitiesForever.Upload.App.DesignChecks;

/// <summary>Screenshots of every screen at its board size (STORIES.md "Every UI story"). New screens add theirs here.</summary>
public sealed class ScreenshotTests
{
    [AvaloniaFact]
    public void MainWindow()
    {
        var window = new MainWindow();
        window.Show();

        Screenshot.Matches(window, "main-window");
        window.Close();
    }

    [AvaloniaFact]
    public void Gallery()
    {
        var window = OpenGallery();

        Screenshot.Matches(window, "gallery");
        window.Close();
    }

    [AvaloniaFact]
    public void GalleryKeyboardFocus()
    {
        var window = OpenGallery();
        var primary = window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("primary"));

        primary.Focus(NavigationMethod.Tab);

        Screenshot.Matches(window, "gallery-focus");
        window.Close();
    }

    // The gallery as one tall image: board width, as high as its content.
    private static GalleryWindow OpenGallery()
    {
        // The XAML height (board size) would win over SizeToContent, so clear it.
        var window = new GalleryWindow { Height = double.NaN, SizeToContent = SizeToContent.Height };
        window.Show();
        return window;
    }
}
