using System.Collections.ObjectModel;
using System.Globalization;
using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommunitiesForever.Upload.App.ViewModels;

/// <summary>Setup step 2 (board <c>setup</c>): find WoW: Forever and choose the accounts to watch.</summary>
public sealed partial class FindGameStepViewModel(GameScanner scanner, TimeProvider time) : ObservableObject
{
    public IUiServices? Ui { get; set; }

    public ObservableCollection<InstallItem> Installs { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NothingFound), nameof(Found))]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = "";

    [ObservableProperty]
    public partial string? FolderProblem { get; set; }

    [ObservableProperty]
    public partial bool CanContinue { get; set; }

    public bool NothingFound => !IsScanning && Installs.Count == 0;

    public bool Found => !IsScanning && Installs.Count > 0;

    public IReadOnlyList<WatchedAccount> Chosen =>
        [.. Installs.SelectMany(i => i.Accounts.Where(a => a.IsChecked).Select(a => new WatchedAccount(i.ForeverPath, a.Name)))];

    /// <summary>Scans (off the UI thread: it touches the disk), keeping the user's earlier choices.</summary>
    [RelayCommand]
    public async Task ScanAsync()
    {
        var chosen = Chosen;
        IsScanning = true;
        FolderProblem = null;
        var found = await Task.Run(scanner.Scan);
        Installs.Clear();
        foreach (var install in found)
        {
            Add(install, chosen);
        }

        IsScanning = false;
        Update();
    }

    /// <summary>Selects the given accounts (setup reopened with earlier settings).</summary>
    public void Choose(IReadOnlyList<WatchedAccount> accounts)
    {
        foreach (var install in Installs)
        {
            foreach (var account in install.Accounts)
            {
                account.IsChecked = accounts.Contains(new WatchedAccount(install.ForeverPath, account.Name));
            }
        }
    }

    [RelayCommand]
    private async Task ChooseFolderAsync()
    {
        if (Ui is null || await Ui.PickFolderAsync("Choose the World of Warcraft folder") is not { } folder)
        {
            return;
        }

        var install = await Task.Run(() => scanner.Inspect(folder));
        if (install is null)
        {
            FolderProblem = "There is no World of Warcraft: Forever in that folder. Choose the World of Warcraft folder or its _forever_ folder.";
            return;
        }

        FolderProblem = null;
        if (Installs.FirstOrDefault(i => string.Equals(i.ForeverPath, install.ForeverPath, StringComparison.OrdinalIgnoreCase)) is { } known)
        {
            Installs.Remove(known);
        }

        Add(install, Chosen);
        Update();
    }

    private void Add(GameInstall install, IReadOnlyList<WatchedAccount> chosen)
    {
        var now = time.GetLocalNow();
        var accounts = install.Accounts.Select(a => new AccountItem(
            a.Name,
            a.HasAddonFile,
            a.SavedAt is { } saved ? $"Saved {SavedAt(TimeZoneInfo.ConvertTime(saved, time.LocalTimeZone), now)}" : "No addon file yet: log in once with the addon enabled",
            // Ready accounts are ticked; earlier choices win after "Scan again".
            chosen.Count > 0 ? chosen.Contains(new WatchedAccount(install.ForeverPath, a.Name)) : a.HasAddonFile,
            Update));
        Installs.Add(new InstallItem(install.ForeverPath, [.. accounts]));
    }

    private void Update()
    {
        int installs = Installs.Count, accounts = Installs.Sum(i => i.Accounts.Count);
        Summary = $"Scan complete · {Count(installs, "install")} · {Count(accounts, "account")}";
        CanContinue = Installs.Any(i => i.Accounts.Any(a => a.IsChecked));
        OnPropertyChanged(nameof(NothingFound));
        OnPropertyChanged(nameof(Found));
    }

    private static string Count(int n, string noun) => $"{n} {noun}{(n == 1 ? "" : "s")}";

    internal static string SavedAt(DateTimeOffset saved, DateTimeOffset now)
    {
        string clock = saved.ToString("HH:mm", CultureInfo.InvariantCulture);
        return (now.Date - saved.Date).Days switch
        {
            0 => $"today {clock}",
            1 => $"yesterday {clock}",
            _ => saved.ToString("d MMM HH:mm", CultureInfo.InvariantCulture),
        };
    }
}

public sealed record InstallItem(string ForeverPath, IReadOnlyList<AccountItem> Accounts);

public sealed partial class AccountItem(string name, bool isReady, string detail, bool isChecked, Action changed) : ObservableObject
{
    public string Name { get; } = name;

    public bool IsReady { get; } = isReady;

    public string Detail { get; } = detail;

    public string Status => IsReady ? "Ready" : "Not found";

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = isChecked;

    partial void OnIsCheckedChanged(bool value) => changed();
}
