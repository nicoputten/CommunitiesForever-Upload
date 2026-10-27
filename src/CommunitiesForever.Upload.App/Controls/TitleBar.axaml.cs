using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CommunitiesForever.Upload.App.Controls;

/// <summary>The app's own title bar (main-window board). Closing quits for now; UP-08 makes it hide to the tray.</summary>
internal sealed partial class TitleBar : UserControl
{
    private Window? _window;

    public TitleBar()
    {
        InitializeComponent();
        MinimiseButton.Click += OnMinimise;
        CloseButton.Click += OnClose;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null)
        {
            ShowCloseLabel();
            TitleText.Text = _window.Title;
            _window.PropertyChanged += OnWindowPropertyChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Long-running app: never leave a handler on the window behind (CLAUDE.md rule 11).
        if (_window is not null)
        {
            _window.PropertyChanged -= OnWindowPropertyChanged;
            _window = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Board main-window: "Hide to tray" when closing only hides (UP-08); follows the setting (UP-09).</summary>
    public void ShowCloseLabel()
    {
        if (_window is not null)
        {
            string close = _window is MainWindow { HidesToTray: true } ? "Hide to tray" : "Close";
            Avalonia.Automation.AutomationProperties.SetName(CloseButton, close);
            ToolTip.SetTip(CloseButton, close);
        }
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.TitleProperty)
        {
            TitleText.Text = _window?.Title;
        }
    }

    private void OnMinimise(object? sender, RoutedEventArgs e)
    {
        if (_window is not null)
        {
            _window.WindowState = WindowState.Minimized;
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => _window?.Close();
}
