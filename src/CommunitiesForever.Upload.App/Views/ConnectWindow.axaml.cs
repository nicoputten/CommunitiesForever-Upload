using Avalonia.Controls;
using CommunitiesForever.Upload.App.ViewModels;

namespace CommunitiesForever.Upload.App.Views;

/// <summary>"Connect" ("Reconnect" in the main window).</summary>
public sealed partial class ConnectWindow : Window
{
    public ConnectWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is ConnectWindowViewModel vm)
        {
            vm.Ui = new WindowUi(this);
            _ = vm.StartAsync();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // Stops connecting and lets go of the window (CLAUDE.md rule 11).
        (DataContext as ConnectWindowViewModel)?.Dispose();
        base.OnClosed(e);
    }
}
