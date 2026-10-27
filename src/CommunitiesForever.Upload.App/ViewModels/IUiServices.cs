namespace CommunitiesForever.Upload.App.ViewModels;

/// <summary>What view models need from the window they are shown in. Set by the window while it is open.</summary>
public interface IUiServices
{
    Task SetClipboardTextAsync(string text);

    /// <summary>The folder the user picked, or null when cancelled.</summary>
    Task<string?> PickFolderAsync(string title);
}
