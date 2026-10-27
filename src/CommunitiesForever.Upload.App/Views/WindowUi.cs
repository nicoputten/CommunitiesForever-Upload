using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using CommunitiesForever.Upload.App.ViewModels;

namespace CommunitiesForever.Upload.App.Views;

/// <summary>The clipboard and folder picker of a window, for its view model.</summary>
internal sealed class WindowUi(Window window) : IUiServices
{
    public async Task SetClipboardTextAsync(string text)
    {
        if (window.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
