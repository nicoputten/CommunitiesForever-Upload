using Avalonia.Controls;
using CommunitiesForever.Upload.App.ViewModels;

namespace CommunitiesForever.Upload.App.Views;

/// <summary>The first-run setup window (board <c>setup</c>).</summary>
public sealed partial class SetupWindow : Window
{
    public SetupWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is SetupViewModel vm)
        {
            vm.Ui = new WindowUi(this);
            _ = vm.StartAsync();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // Long-running app: the view model must not keep this window alive (CLAUDE.md rule 11).
        if (DataContext is SetupViewModel vm)
        {
            vm.Dispose();
        }

        base.OnClosed(e);
    }
}
