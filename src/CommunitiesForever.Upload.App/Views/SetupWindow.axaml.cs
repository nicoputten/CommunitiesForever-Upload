using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using CommunitiesForever.Upload.App.ViewModels;

namespace CommunitiesForever.Upload.App.Views;

/// <summary>The first-run setup window (board <c>setup</c>). Gives its view model the clipboard and the folder picker.</summary>
public sealed partial class SetupWindow : Window, IUiServices
{
    public SetupWindow() => InitializeComponent();

    public async Task SetClipboardTextAsync(string text)
    {
        if (Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is SetupViewModel vm)
        {
            vm.Ui = this;
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
