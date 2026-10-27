using Avalonia.Controls;
using CommunitiesForever.Upload.App.ViewModels;

namespace CommunitiesForever.Upload.App.Views;

/// <summary>"Watched accounts" ("Change…" in the main window).</summary>
public sealed partial class WatchedAccountsWindow : Window
{
    public WatchedAccountsWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is WatchedAccountsViewModel vm)
        {
            vm.Ui = new WindowUi(this);
            _ = vm.StartAsync();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // The view model must not keep this window alive (CLAUDE.md rule 11).
        if (DataContext is WatchedAccountsViewModel vm)
        {
            vm.Ui = null;
        }

        base.OnClosed(e);
    }
}
