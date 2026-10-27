using System.Collections.ObjectModel;
using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Uploading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommunitiesForever.Upload.App.ViewModels;

/// <summary>A watched account in the "Watched" box (board <c>main-window</c>).</summary>
public sealed record AccountRow(string FileLine, string Detail, string Badge, Tone BadgeTone, string Folder);

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
    private SynchronizationContext? _ui;
    private ITimer? _ticker;
    private CancellationTokenSource? _identityLoad;
    private AppSettings _current = new();
    private WatchedAccount? _heroAccount;

    public MainViewModel(IUploadLoop loop, UploadHistory history, SettingsStore settings, ISiteClient site, ITokenStore tokens, SiteOptions options, IBrowser browser, TimeProvider time)
    {
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

    /// <summary>"Reconnect": the app opens setup at step 1.</summary>
    public event EventHandler? ReconnectRequested;

    /// <summary>"Change…", "Scan again", "Choose folder…": the app opens setup at step 2.</summary>
    public event EventHandler? FindGameRequested;

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

    public string Title => Identity is { SiteName.Length: > 0 } me ? $"{me.SiteName} Uploader" : App.DefaultName;

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
        _current = _settings.Load() is SettingsLoad.Loaded { Settings: var settings } ? settings : new AppSettings();
        StartsWithWindows = _current.StartWithWindows;
        _loop.StatusChanged += OnStatusChanged;
        _ticker = _time.CreateTimer(_ => Post(RefreshIfCounting), null, Tick, Tick);
        Refresh();
        _ = LoadIdentityAsync();
    }

    public void Dispose()
    {
        _loop.StatusChanged -= OnStatusChanged;
        _ticker?.Dispose();
        _ticker = null;
        _identityLoad?.Cancel();
        _identityLoad?.Dispose();
        _identityLoad = null;
        _ui = null;
    }

    /// <summary>Rebuilds everything from the loop's statuses and the history.</summary>
    public void Refresh()
    {
        var statuses = _loop.Statuses;
        var urgent = statuses.OrderByDescending(s => s.State).FirstOrDefault();
        _heroAccount = urgent?.Account;
        IsConnected = urgent?.State != AccountState.NotConnected;
        var now = _time.GetUtcNow();
        Hero = Hero.For(urgent, statuses.Count > 1 ? urgent?.Account.Account : null, now, _time.LocalTimeZone, Host, _loop.PausedUntil);

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
    private void FindGame() => FindGameRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenSite() => _browser.Open(_options.BaseUri);

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
        string folder = Path.Combine(account.InstallPath, "WTF", "Account", account.Account, "SavedVariables");
        var parts = new List<string>();
        if (status.File is { } file)
        {
            var saved = TimeZoneInfo.ConvertTime(file.WrittenAt, _time.LocalTimeZone);
            parts.Add($"Saved by the game {(saved.Date == localNow.Date ? Formats.Clock(saved) : Formats.Moment(saved, localNow))}");
        }

        if (status.LastUpload is { } last)
        {
            var uploaded = TimeZoneInfo.ConvertTime(last.UploadedAt, _time.LocalTimeZone);
            parts.Add($"uploaded {(uploaded.Date == localNow.Date ? Formats.Clock(uploaded) : Formats.Moment(uploaded, localNow))}");
        }
        else
        {
            parts.Add("not uploaded yet");
        }

        if (status.File is { } size)
        {
            parts.Add(Formats.Size(size.Size));
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
        return new AccountRow($"{account.Account} · {GameScanner.AddonFileName}", string.Join(" · ", parts), badge, tone, folder);
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
