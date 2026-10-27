using System.Collections.ObjectModel;
using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Updates;
using CommunitiesForever.Upload.Core.Uploading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommunitiesForever.Upload.App.ViewModels;

/// <summary>
/// A watched account in the "Watched" box: name, one status line and a badge; the path, save time and size are in
/// <see cref="Details"/> (a tooltip). Less text than the board (docs/DESIGN-NOTES.md).
/// </summary>
public sealed record AccountRow(string Name, string Line, string Badge, Tone BadgeTone, string Details);

/// <summary>A line in "Recent uploads": the site's summary line; click opens the session report.</summary>
public sealed record RecentRow(string When, string Summary, Tone Tone, Uri? Report);

/// <summary>
/// The main window (UP-07): the loop's status as header, watched accounts, recent uploads, connection.
/// Attached while the window is open (CLAUDE.md rule 11): <see cref="Attach"/> subscribes, <see cref="Dispose"/> lets go.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    public const int RecentCount = 7;
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    private readonly IUploadLoop _loop;
    private readonly UploadHistory _history;
    private readonly SettingsStore _settings;
    private readonly ISiteClient _site;
    private readonly ITokenStore _tokens;
    private readonly SiteOptions _options;
    private readonly IBrowser _browser;
    private readonly TimeProvider _time;
    private readonly GameScanner _scanner;
    private readonly Func<SettingsViewModel> _newSettings;
    private readonly UpdateChecker _updates;
    private SynchronizationContext? _ui;
    private ITimer? _ticker;
    private CancellationTokenSource? _identityLoad;
    private AppSettings _current = new();
    private WatchedAccount? _heroAccount;

    public MainViewModel(IUploadLoop loop, UploadHistory history, SettingsStore settings, ISiteClient site, ITokenStore tokens, SiteOptions options, IBrowser browser, TimeProvider time, GameScanner scanner, Func<SettingsViewModel> newSettings, UpdateChecker updates)
    {
        _updates = updates;
        _scanner = scanner;
        _newSettings = newSettings;
        _loop = loop;
        _history = history;
        _settings = settings;
        _site = site;
        _tokens = tokens;
        _options = options;
        _browser = browser;
        _time = time;
        Hero = Hero.For(null, null, time.GetUtcNow(), time.LocalTimeZone, Host, null);
    }

    /// <summary>"Reconnect": the app opens the Connect window.</summary>
    public event EventHandler? ReconnectRequested;

    /// <summary>
    /// The accounts, "upload automatically" or the connection changed (Scan again, Choose folder…, Settings);
    /// the settings are saved, the app restarts watching.
    /// </summary>
    public event EventHandler? AccountsChanged;

    /// <summary>Settings saved: the app applies "Start with Windows" and the close button.</summary>
    public event EventHandler<AppSettings>? SettingsChanged;

    /// <summary>The window's clipboard and folder picker, while it is open.</summary>
    public IUiServices? Ui { get; set; }

    public string Host => _options.BaseUri.Host;

    public ObservableCollection<AccountRow> Accounts { get; } = [];

    public ObservableCollection<RecentRow> Recent { get; } = [];

    [ObservableProperty]
    public partial Hero Hero { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(ConnectedText))]
    public partial SiteIdentity? Identity { get; set; }

    [ObservableProperty]
    public partial bool StartsWithWindows { get; set; }

    [ObservableProperty]
    public partial bool HasRecent { get; set; }

    /// <summary>"Updated to 1.1 · What's new", once in the footer after an update (update-states board).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdatedTo))]
    public partial string? UpdatedTo { get; set; }

    public bool HasUpdatedTo => UpdatedTo is not null;

    /// <summary>The settings page, while it is open (UP-09); the window shows it instead of the dashboard.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(IsSettingsOpen))]
    public partial SettingsViewModel? Settings { get; set; }

    public bool IsSettingsOpen => Settings is not null;

    public string Title => Settings is null ? Name : $"{Name} · Settings";

    private string Name => Identity is { SiteName.Length: > 0 } me ? $"{me.SiteName} Uploader" : App.DefaultName;

    public string ConnectedText => !IsConnected
        ? $"Not connected · {Host}"
        : Identity is { } me ? $"Connected as {me.BattleTag} · {Host}" : $"Connected · {Host}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectedText))]
    public partial bool IsConnected { get; set; } = true;

    /// <summary>Starts following the loop; call on the UI thread when the window opens.</summary>
    public void Attach()
    {
        _ui = SynchronizationContext.Current;
        _loop.StatusChanged += OnStatusChanged;
        _updates.Changed += OnStatusChanged;
        _ticker = _time.CreateTimer(_ => Post(RefreshIfCounting), null, Tick, Tick);
        Reload();
    }

    /// <summary>Reads the settings and who is connected again (after Settings or Reconnect).</summary>
    public void Reload()
    {
        _current = _settings.Load() is SettingsLoad.Loaded { Settings: var settings } ? settings : new AppSettings();
        StartsWithWindows = _current.StartWithWindows;
        Refresh();
        _ = LoadIdentityAsync();
    }

    /// <summary>Opens the settings page at a section ("Change…" opens Game folders).</summary>
    public void ShowSettings(SettingsSection section)
    {
        if (Settings is not null)
        {
            return;
        }

        var settings = _newSettings();
        settings.Section = section;
        settings.AppName = Name;
        settings.Ui = Ui;
        settings.Changed += OnSettingsSaved;
        settings.Disconnected += OnDisconnected;
        settings.ConnectRequested += OnConnectRequested;
        settings.BackRequested += OnSettingsBack;
        Settings = settings;
        _ = settings.OpenAsync();
    }

    /// <summary>"← Back": the dashboard again.</summary>
    public void CloseSettings()
    {
        if (Settings is not { } settings)
        {
            return;
        }

        settings.Changed -= OnSettingsSaved;
        settings.Disconnected -= OnDisconnected;
        settings.ConnectRequested -= OnConnectRequested;
        settings.BackRequested -= OnSettingsBack;
        settings.Dispose();
        Settings = null;
    }

    public void Dispose()
    {
        CloseSettings();
        _loop.StatusChanged -= OnStatusChanged;
        _updates.Changed -= OnStatusChanged;
        _ticker?.Dispose();
        _ticker = null;
        _identityLoad?.Cancel();
        _identityLoad?.Dispose();
        _identityLoad = null;
        _ui = null;
        Ui = null;
    }

    /// <summary>Rebuilds everything from the loop's statuses and the history.</summary>
    public void Refresh()
    {
        var statuses = _loop.Statuses;
        var urgent = statuses.OrderByDescending(s => s.State).FirstOrDefault();
        _heroAccount = urgent?.Account;
        IsConnected = urgent?.State != AccountState.NotConnected;
        var now = _time.GetUtcNow();
        // A required update is the one loud update case: it holds every upload (update-states board).
        Hero = _updates.IsRequired
            ? Hero.UpdateNeeded(_updates.CurrentVersion, _updates.State is UpdateState.Ready or UpdateState.WaitingForUpload ? _updates.NewVersion : null)
            : Hero.For(urgent, statuses.Count > 1 ? urgent?.Account.Account : null, now, _time.LocalTimeZone, Host, _loop.PausedUntil);
        UpdatedTo = _updates.UpdatedTo is { } to ? $"Updated to {UpdatePanel.ShortVersion(to)} · " : null;

        var localNow = TimeZoneInfo.ConvertTime(now, _time.LocalTimeZone);
        Accounts.Clear();
        foreach (var status in statuses)
        {
            Accounts.Add(Row(status, localNow));
        }

        Recent.Clear();
        var uploads = _current.Accounts
            .SelectMany(_history.Uploads)
            .OrderByDescending(u => u.UploadedAt)
            .Take(RecentCount);
        foreach (var upload in uploads)
        {
            Recent.Add(new RecentRow(
                Formats.Day(TimeZoneInfo.ConvertTime(upload.UploadedAt, _time.LocalTimeZone), localNow),
                upload.SummaryLine,
                upload.Status switch { UploadStatus.Ok => Tone.Fresh, UploadStatus.NothingNew => Tone.Dim, _ => Tone.Warn },
                upload.ReportUrl));
        }

        HasRecent = Recent.Count > 0;
    }

    [RelayCommand]
    private void UploadNow() => _loop.UploadNow(Hero.Actions.HasFlag(HeroActions.Skip) ? _heroAccount : null);

    [RelayCommand]
    private void Skip()
    {
        if (_heroAccount is { } account)
        {
            _loop.Skip(account);
        }
    }

    [RelayCommand]
    private void TryNow() => _loop.UploadNow(_heroAccount);

    [RelayCommand]
    private void EndPause() => _loop.EndPause();

    [RelayCommand]
    private void Reconnect() => ReconnectRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenSettings() => ShowSettings(SettingsSection.Behaviour);

    [RelayCommand]
    private void ChangeAccounts() => ShowSettings(SettingsSection.GameFolders);

    /// <summary>
    /// "Scan again" (not watching): finds the accounts in their new place and saves that, in place.
    /// When an account is nowhere to be found, Settings › Game folders opens.
    /// </summary>
    [RelayCommand]
    private async Task ScanAgainAsync()
    {
        var installs = await Task.Run(_scanner.Scan);
        if (!Relocate(installs))
        {
            ShowSettings(SettingsSection.GameFolders);
        }
    }

    /// <summary>"Choose folder…" (not watching): the user points at the game; the watched accounts move there.</summary>
    [RelayCommand]
    private async Task ChooseFolderAsync()
    {
        if (Ui is null || await Ui.PickFolderAsync("Choose the World of Warcraft folder") is not { } folder)
        {
            return;
        }

        var install = await Task.Run(() => _scanner.Inspect(folder));
        if (install is null || !Relocate([install]))
        {
            ShowSettings(SettingsSection.GameFolders);
        }
    }

    // Moves every watched account that isn't watched any more to an install that has it. False when one is nowhere.
    private bool Relocate(IReadOnlyList<GameInstall> installs)
    {
        var lost = _loop.Statuses.Where(s => s.State == AccountState.NotWatching).Select(s => s.Account).ToHashSet();
        bool changed = false, allFound = true;
        var accounts = _current.Accounts.Select(account =>
        {
            if (!lost.Contains(account))
            {
                return account;
            }

            var home = installs.FirstOrDefault(i => i.Accounts.Any(a => string.Equals(a.Name, account.Account, StringComparison.OrdinalIgnoreCase)));
            if (home is null)
            {
                allFound = false;
                return account;
            }

            changed |= !string.Equals(home.ForeverPath, account.InstallPath, StringComparison.OrdinalIgnoreCase);
            return account with { InstallPath = home.ForeverPath };
        }).ToList();

        if (changed)
        {
            _current = _current with { Accounts = accounts };
            _settings.Save(_current);
            AccountsChanged?.Invoke(this, EventArgs.Empty);
        }

        return allFound;
    }

    [RelayCommand]
    private void OpenSite() => _browser.Open(_options.BaseUri);

    [RelayCommand]
    private void RestartToUpdate() => _updates.RestartNow();

    [RelayCommand]
    private void OpenWhatsNew()
    {
        if (_updates.NotesUrl is { } notes)
        {
            _browser.Open(notes);
        }

        _updates.DismissUpdated();
    }

    [RelayCommand]
    private void DismissUpdated() => _updates.DismissUpdated();

    [RelayCommand]
    private void OpenReport()
    {
        if (Hero.Report is { } report)
        {
            _browser.Open(report);
        }
    }

    [RelayCommand]
    private void OpenHistory() => _browser.Open(new Uri(_options.BaseUri, "upload"));

    [RelayCommand]
    private void OpenRecent(RecentRow? row)
    {
        if (row?.Report is { } report)
        {
            _browser.Open(report);
        }
    }

    private AccountRow Row(AccountStatus status, DateTimeOffset localNow)
    {
        var account = status.Account;
        string line = status.LastUpload is { } last
            ? $"Uploaded {Formats.Moment(TimeZoneInfo.ConvertTime(last.UploadedAt, _time.LocalTimeZone), localNow)}"
            : "Not uploaded yet";
        var details = new List<string> { Path.Combine(account.InstallPath, "WTF", "Account", account.Account, "SavedVariables", GameScanner.AddonFileName) };
        if (status.File is { } file)
        {
            details.Add($"Saved by the game {Formats.Moment(TimeZoneInfo.ConvertTime(file.WrittenAt, _time.LocalTimeZone), localNow)} · {Formats.Size(file.Size)}");
        }

        var (badge, tone) = status.State switch
        {
            AccountState.Watching or AccountState.Playing when status.LastUpload is not null => ("Up to date", Tone.Fresh),
            AccountState.WaitingToSend => ("Waiting to send", Tone.Warn),
            AccountState.NotUploaded => ("Not uploaded", Tone.Warn),
            AccountState.NotWatching => ("Not watching", Tone.Warn),
            AccountState.NotConnected => ("Not connected", Tone.Warn),
            AccountState.NotAllowed => ("Not allowed", Tone.Warn),
            _ => ("Waiting", Tone.Gold),
        };
        return new AccountRow(account.Account, line, badge, tone, string.Join(Environment.NewLine, details));
    }

    private async Task LoadIdentityAsync()
    {
        if (_tokens.Load() is not { } token)
        {
            return;
        }

        _identityLoad = new CancellationTokenSource();
        var result = await _site.GetIdentityAsync(token, _identityLoad.Token);
        if (result.IsOk)
        {
            Identity = result.Value;
        }
    }

    // Watching restarts only for what the loop uses; the rest the app applies (or reads when needed, like notifications).
    private void OnSettingsSaved(object? sender, AppSettings saved)
    {
        bool restart = saved.UploadAutomatically != _current.UploadAutomatically || !saved.Accounts.SequenceEqual(_current.Accounts);
        _current = saved;
        StartsWithWindows = saved.StartWithWindows;
        SettingsChanged?.Invoke(this, saved);
        if (restart)
        {
            AccountsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        Identity = null;
        AccountsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnConnectRequested(object? sender, EventArgs e) => ReconnectRequested?.Invoke(this, EventArgs.Empty);

    private void OnSettingsBack(object? sender, EventArgs e) => CloseSettings();

    // The loop reports from its own threads; the window updates on its own.
    private void OnStatusChanged(object? sender, EventArgs e) => Post(Refresh);

    // The countdown and "Next try in" change with time alone.
    private void RefreshIfCounting()
    {
        if (_loop.Statuses.Any(s => s.State is AccountState.Settling or AccountState.WaitingToSend or AccountState.NotAllowed)
            || Hero.Actions.HasFlag(HeroActions.Report))
        {
            Refresh();
        }
    }

    private void Post(Action action)
    {
        if (_ui is { } ui)
        {
            ui.Post(_ => action(), null);
        }
    }
}
