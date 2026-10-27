using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommunitiesForever.Upload.App.ViewModels;

/// <summary>
/// The "Watched accounts" window ("Change…" in the main window): setup step 2 on its own, with Save and Cancel.
/// UP-09 shows the same view in Settings.
/// </summary>
public sealed partial class WatchedAccountsViewModel(GameScanner scanner, SettingsStore settings, TimeProvider time) : ObservableObject
{
    private AppSettings _current = new();

    /// <summary>The new accounts are saved; the app restarts watching.</summary>
    public event EventHandler? Saved;

    public event EventHandler? Cancelled;

    public FindGameStepViewModel Find { get; } = new(scanner, time);

    public IUiServices? Ui
    {
        get => Find.Ui;
        set => Find.Ui = value;
    }

    /// <summary>Scans, with the accounts watched now ticked. Called when the window opens.</summary>
    public async Task StartAsync()
    {
        _current = settings.Load() is SettingsLoad.Loaded { Settings: var loaded } ? loaded : new AppSettings();
        await Find.ScanAsync();
        if (_current.Accounts.Count > 0)
        {
            Find.Choose(_current.Accounts);
        }
    }

    [RelayCommand]
    private void Save()
    {
        settings.Save(_current with { Accounts = Find.Chosen });
        Saved?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);
}
