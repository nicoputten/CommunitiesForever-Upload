using System.Collections.ObjectModel;
using System.Globalization;
using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Updates;
using CommunitiesForever.Upload.Core.Uploading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommunitiesForever.Upload.App.ViewModels;

public enum SettingsSection
{
    Behaviour,
    GameFolders,
    Connection,
    About,
}

/// <summary>An account in Settings › Game folders: name, folder, a switch to watch it.</summary>
public sealed partial class GameFolderRow(WatchedAccount account, string folder, bool isMissing, bool isWatched, Action<GameFolderRow> changed) : ObservableObject
{
    public WatchedAccount Account { get; } = account;

    public string Name => Account.Account;

    public string Folder { get; } = folder;

    /// <summary>Watched, but its folder is gone ("not found" in the warning colour).</summary>
    public bool IsMissing { get; } = isMissing;

    [ObservableProperty]
    public partial bool IsWatched { get; set; } = isWatched;

    partial void OnIsWatchedChanged(bool value) => changed(this);
}

/// <summary>
/// Settings (board <c>settings</c>, UP-09): a page in the main window. Everything saves and takes effect right away;
/// <see cref="Changed"/> tells the app to apply it (autostart, close to tray, watching).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly SettingsStore _store;
    private readonly IUploadLoop _loop;
    private readonly GameScanner _scanner;
    private readonly ConnectFlow _connect;
    private readonly ISiteClient _site;
    private readonly ITokenStore _tokens;
    private readonly SiteOptions _options;
    private readonly IBrowser _browser;
    private readonly TimeProvider _time;
    private readonly UpdateChecker _updates;
    private readonly List<GameInstall> _installs = [];
    private SynchronizationContext? _ui;
    private AppSettings _settings = new();
    private bool _loading;

    public SettingsViewModel(SettingsStore store, IUploadLoop loop, GameScanner scanner, ConnectFlow connect, ISiteClient site, ITokenStore tokens, SiteOptions options, IBrowser browser, TimeProvider time, UpdateChecker updates)
    {
        _updates = updates;
        _store = store;
        _loop = loop;
        _scanner = scanner;
        _connect = connect;
        _site = site;
        _tokens = tokens;
        _options = options;
        _browser = browser;
        _time = time;
    }

    /// <summary>The settings were saved; the app applies them.</summary>
    public event EventHandler<AppSettings>? Changed;

    /// <summary>Disconnected: the app stops uploading and shows "Not connected".</summary>
    public event EventHandler? Disconnected;

    /// <summary>"Connect" (after a disconnect): the app opens the Connect window.</summary>
    public event EventHandler? ConnectRequested;

    /// <summary>"← Back".</summary>
    public event EventHandler? BackRequested;

    public IUiServices? Ui { get; set; }

    /// <summary>Where the page opens ("Change…" opens Game folders).</summary>
    public SettingsSection Section { get; set; }

    /// <summary>"Doombringers Uploader", for About.</summary>
    public string AppName { get; set; } = App.DefaultName;

    /// <summary>The app's version, for About.</summary>
    public string Version => _options.AppVersion;

    /// <summary>The logs folder, for "Open log folder".</summary>
    public string LogFolder { get; set; } = "";

    /// <summary>THIRD-PARTY-NOTICES.txt next to the app, for "Licences".</summary>
    public string LicencesFile { get; set; } = "";

    /// <summary>"Updates come from wow.vestras.nl with your upload token and are signed; …" (settings-updates board).</summary>
    public string UpdatesNote => $"Updates come from {SiteHost} with your upload token and are signed; an unsigned file is thrown away. No pop-ups for updates.";

    public string SiteHost => _options.BaseUri.Host;

    public ObservableCollection<GameFolderRow> Folders { get; } = [];

    [ObservableProperty]
    public partial bool UploadAutomatically { get; set; }

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial bool CloseHidesToTray { get; set; }

    /// <summary>0 every upload, 1 only problems, 2 off.</summary>
    [ObservableProperty]
    public partial int Notifications { get; set; }

    /// <summary>0 not paused, 1 for 1 hour, 2 until tomorrow (06:00); the same pause as the tray menu.</summary>
    [ObservableProperty]
    public partial int Pause { get; set; }

    [ObservableProperty]
    public partial bool InstallUpdatesWhenWowClosed { get; set; }

    /// <summary>Settings › About &amp; updates: the update state.</summary>
    [ObservableProperty]
    public partial UpdatePanel? Updates { get; set; }

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    public partial string? FolderProblem { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotConnected))]
    public partial bool IsConnected { get; set; }

    public bool IsNotConnected => !IsConnected;

    [ObservableProperty]
    public partial string ConnectionLine { get; set; } = "";

    [ObservableProperty]
    public partial string Badge { get; set; } = "";

    [ObservableProperty]
    public partial Tone BadgeTone { get; set; } = Tone.Dim;

    [ObservableProperty]
    public partial bool IsConfirmingDisconnect { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Reads the settings, the pause and the connection; scans for accounts. Called when the page opens.</summary>
    public async Task OpenAsync()
    {
        _loading = true;
        _settings = _store.Load() is SettingsLoad.Loaded { Settings: var s } ? s : new AppSettings();
        UploadAutomatically = _settings.UploadAutomatically;
        StartWithWindows = _settings.StartWithWindows;
        CloseHidesToTray = _settings.CloseHidesToTray;
        Notifications = (int)_settings.Notifications;
        InstallUpdatesWhenWowClosed = _settings.InstallUpdatesWhenWowClosed;
        // The tray may have paused already: an hour or less left reads as "1 hour".
        Pause = _loop.PausedUntil is not { } until ? 0 : until - _time.GetUtcNow() <= TimeSpan.FromHours(1) ? 1 : 2;
        _loading = false;
        _ui = SynchronizationContext.Current;
        _updates.Changed += OnUpdatesChanged;
        RefreshUpdates();

        await Task.WhenAll(ScanAsync(), TestConnectionAsync());
    }

    public void Dispose()
    {
        _updates.Changed -= OnUpdatesChanged;
        _ui = null;
        Ui = null;
    }

    partial void OnUploadAutomaticallyChanged(bool value) => Save(_settings with { UploadAutomatically = value });

    partial void OnStartWithWindowsChanged(bool value) => Save(_settings with { StartWithWindows = value });

    partial void OnCloseHidesToTrayChanged(bool value) => Save(_settings with { CloseHidesToTray = value });

    partial void OnNotificationsChanged(int value) => Save(_settings with { Notifications = (NotificationLevel)value });

    partial void OnInstallUpdatesWhenWowClosedChanged(bool value)
    {
        Save(_settings with { InstallUpdatesWhenWowClosed = value });
        RefreshUpdates();
    }

    partial void OnPauseChanged(int value)
    {
        if (_loading)
        {
            return;
        }

        var now = _time.GetUtcNow();
        switch (value)
        {
            case 1:
                _loop.Pause(now.AddHours(1));
                break;
            case 2:
                var local = TimeZoneInfo.ConvertTime(now, _time.LocalTimeZone);
                var morning = local.Date.AddDays(1).AddHours(6);
                _loop.Pause(new DateTimeOffset(morning, _time.LocalTimeZone.GetUtcOffset(morning)));
                break;
            default:
                _loop.EndPause();
                break;
        }
    }

    [RelayCommand]
    private void Back() => BackRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Adds the accounts it finds (switched off); watched ones whose folder is gone show "not found".</summary>
    [RelayCommand]
    private async Task ScanAsync()
    {
        IsScanning = true;
        FolderProblem = null;
        var installs = await Task.Run(_scanner.Scan);
        IsScanning = false;
        _installs.Clear();
        _installs.AddRange(installs);
        Rebuild(switchOn: []);
    }

    /// <summary>"Add folder…": the accounts in the chosen folder; the ones with an addon file are switched on.</summary>
    [RelayCommand]
    private async Task AddFolderAsync()
    {
        if (Ui is null || await Ui.PickFolderAsync("Choose the World of Warcraft folder") is not { } folder)
        {
            return;
        }

        var install = await Task.Run(() => _scanner.Inspect(folder));
        if (install is null)
        {
            FolderProblem = "There is no World of Warcraft: Forever in that folder. Choose the World of Warcraft folder or its _forever_ folder.";
            return;
        }

        FolderProblem = null;
        var ready = install.Accounts.Where(a => a.HasAddonFile).Select(a => new WatchedAccount(install.ForeverPath, a.Name)).ToList();
        _installs.RemoveAll(i => string.Equals(i.ForeverPath, install.ForeverPath, StringComparison.OrdinalIgnoreCase));
        _installs.Add(install);
        Rebuild(ready);
        if (ready.Count > 0)
        {
            Save(_settings with { Accounts = [.. _settings.Accounts.Union(ready)] });
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (_tokens.Load() is not { } token)
        {
            IsConnected = false;
            ConnectionLine = "This PC doesn't upload to the site.";
            Badge = "Not connected";
            BadgeTone = Tone.Warn;
            return;
        }

        IsConnected = true;
        IsBusy = true;
        var me = await _site.GetIdentityAsync(token);
        IsBusy = false;
        if (me.IsOk)
        {
            var created = TimeZoneInfo.ConvertTime(me.Value!.CreatedAt, _time.LocalTimeZone);
            ConnectionLine = $"Connected as {me.Value.BattleTag} · upload-only token · created {created.ToString("d MMM", CultureInfo.InvariantCulture)}";
            Badge = "Working";
            BadgeTone = Tone.Fresh;
        }
        else if (me.Failure!.Kind == SiteError.InvalidToken)
        {
            ConnectionLine = "The site doesn't accept this uploader's token anymore.";
            Badge = "Token revoked";
            BadgeTone = Tone.Warn;
        }
        else
        {
            ConnectionLine = $"Can't reach {SiteHost} right now.";
            Badge = "Can't reach";
            BadgeTone = Tone.Warn;
        }
    }

    [RelayCommand]
    private void AskDisconnect() => IsConfirmingDisconnect = true;

    [RelayCommand]
    private void CancelDisconnect() => IsConfirmingDisconnect = false;

    /// <summary>Revokes the token on the site and deletes it here (also when the site can't be reached).</summary>
    [RelayCommand]
    private async Task DisconnectAsync()
    {
        IsConfirmingDisconnect = false;
        IsBusy = true;
        await _connect.DisconnectAsync();
        IsBusy = false;
        await TestConnectionAsync();
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Connect() => ConnectRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private Task CheckForUpdatesAsync() => _updates.CheckNowAsync();

    [RelayCommand]
    private void RestartNow() => _updates.RestartNow();

    [RelayCommand]
    private void CancelRestart() => _updates.CancelRestart();

    [RelayCommand]
    private void OpenWhatsNew()
    {
        if (_updates.NotesUrl is { } notes)
        {
            _browser.Open(notes);
        }
    }

    [RelayCommand]
    private void OpenLicences()
    {
        if (File.Exists(LicencesFile))
        {
            _browser.Open(new Uri(LicencesFile));
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        if (LogFolder.Length > 0)
        {
            Directory.CreateDirectory(LogFolder);
            _browser.Open(new Uri(LogFolder));
        }
    }

    // Watched accounts first (missing when their folder is gone), then accounts found but not watched.
    private void Rebuild(IReadOnlyList<WatchedAccount> switchOn)
    {
        var found = _installs.SelectMany(i => i.Accounts.Select(a => new WatchedAccount(i.ForeverPath, a.Name))).ToList();
        var watched = _settings.Accounts.Union(switchOn).ToList();
        Folders.Clear();
        foreach (var account in watched)
        {
            Folders.Add(Row(account, isMissing: !found.Contains(account), isWatched: true));
        }

        foreach (var account in found.Where(a => !watched.Contains(a)))
        {
            Folders.Add(Row(account, isMissing: false, isWatched: false));
        }
    }

    private GameFolderRow Row(WatchedAccount account, bool isMissing, bool isWatched) =>
        new(account, Path.Combine(account.InstallPath, "WTF", "Account", account.Account, "SavedVariables"), isMissing, isWatched, OnFolderSwitched);

    private void OnFolderSwitched(GameFolderRow row) => Save(_settings with
    {
        Accounts = row.IsWatched
            ? [.. _settings.Accounts.Append(row.Account).Distinct()]
            : [.. _settings.Accounts.Where(a => a != row.Account)],
    });

    // The checker reports from its own thread.
    private void OnUpdatesChanged(object? sender, EventArgs e)
    {
        if (_ui is { } ui)
        {
            ui.Post(_ => RefreshUpdates(), null);
        }
    }

    private void RefreshUpdates() =>
        Updates = UpdatePanel.For(_updates, _time.GetUtcNow(), _time.LocalTimeZone, SiteHost, InstallUpdatesWhenWowClosed);

    private void Save(AppSettings settings)
    {
        if (_loading)
        {
            return;
        }

        _settings = settings;
        _store.Save(settings);
        Changed?.Invoke(this, settings);
    }
}
