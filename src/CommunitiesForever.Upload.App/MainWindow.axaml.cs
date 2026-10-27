using Avalonia.Controls;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.App.Views;

namespace CommunitiesForever.Upload.App;

/// <summary>The main window (board <c>main-window</c>, UP-07). Its view model follows the loop only while it is open.</summary>
internal sealed partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MainViewModel vm)
        {
            vm.Ui = new WindowUi(this);
            vm.Attach();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // Long-running app: let go of the loop, so neither window nor view model stays alive (CLAUDE.md rule 11).
        (DataContext as MainViewModel)?.Dispose();
        base.OnClosed(e);
    }
}
