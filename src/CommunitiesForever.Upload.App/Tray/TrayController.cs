using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.Core;
using CommunitiesForever.Upload.Core.Notifications;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Updates;
using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.App.Tray;

/// <summary>
/// The tray (board <c>tray</c>, UP-08): icon per state (uploading animates), tooltip, right-click menu, and the Windows
/// notifications the <see cref="NotificationPolicy"/> asks for. Lives as long as the app; <see cref="Dispose"/> lets
/// go of the loop, the timer and the icon (CLAUDE.md rule 11).
/// </summary>
public sealed class TrayController : IDisposable
{
    private const string IconFolder = "avares://CommunitiesForever.Upload.App/Assets/Icons/";

    private readonly IUploadLoop _loop;
    private readonly NotificationPolicy _policy;
    private readonly INotifier _notifier;
    private readonly SettingsStore _settings;
    private readonly ISiteClient _site;
    private readonly ITokenStore _tokens;
    private readonly SiteOptions _options;
    private readonly IBrowser _browser;
    private readonly IMotionPreference _motion;
    private readonly TimeProvider _time;
    private readonly Dictionary<TrayIconKind, WindowIcon> _icons = [];
    private readonly List<WindowIcon> _frames = [];
    private readonly DispatcherTimer _animation;
    private readonly TrayIcon _trayIcon;
    private readonly NativeMenuItem _restartToUpdate = new() { IsVisible = false };
    private readonly NativeMenuItemSeparator _restartSeparator = new() { IsVisible = false };
    private readonly NativeMenuItem _siteLine = new() { IsEnabled = false };
    private readonly NativeMenuItem _statusLine = new() { IsEnabled = false };
    private readonly NativeMenuItem _uploadNow = new("Upload now");
    private readonly NativeMenuItem _pause = new("Pause uploads");
    private readonly NativeMenuItem _pauseHour = new("For 1 hour");
    private readonly NativeMenuItem _pauseTomorrow = new("Until tomorrow");
    private readonly NativeMenuItem _resume = new("Resume uploads");
    private readonly NativeMenuItem _openSite = new();
    private readonly NativeMenuItem _show = new("Show uploader");
    private readonly NativeMenuItem _quit = new("Quit");
    private readonly UpdateChecker _updates;
    private int _frame;
    private string _siteName = App.DefaultName;

    public TrayController(
        IUploadLoop loop,
        NotificationPolicy policy,
        INotifier notifier,
        SettingsStore settings,
        ISiteClient site,
        ITokenStore tokens,
        SiteOptions options,
        IBrowser browser,
        IMotionPreference motion,
        TimeProvider time,
        UpdateChecker updates)
    {
        _updates = updates;
        _loop = loop;
        _policy = policy;
        _notifier = notifier;
        _settings = settings;
        _site = site;
        _tokens = tokens;
        _options = options;
        _browser = browser;
        _motion = motion;
        _time = time;

        _icons[TrayIconKind.Watching] = LoadIcon("tray.ico");
        _icons[TrayIconKind.Paused] = LoadIcon("tray-paused.ico");
        _icons[TrayIconKind.Attention] = LoadIcon("tray-attention.ico");
        for (int i = 1; i <= 4; i++)
        {
            _frames.Add(LoadIcon($"tray-uploading-{i}.ico"));
        }

        _icons[TrayIconKind.Uploading] = _frames[0];
        _animation = new DispatcherTimer { Interval = FrameTime() };
        _animation.Tick += OnFrame;

        _pause.Menu = new NativeMenu { Items = { _pauseHour, _pauseTomorrow } };
        _restartToUpdate.Click += OnRestartToUpdate;
        _uploadNow.Click += OnUploadNow;
        _pauseHour.Click += OnPauseHour;
        _pauseTomorrow.Click += OnPauseTomorrow;
        _resume.Click += OnResume;
        _openSite.Click += OnOpenSite;
        _show.Click += OnShow;
        _quit.Click += OnQuit;
        _trayIcon = new TrayIcon
        {
            Icon = _icons[TrayIconKind.Watching],
            ToolTipText = App.DefaultName,
            Menu = new NativeMenu
            {
                Items =
                {
                    _restartToUpdate, _restartSeparator,
                    _siteLine, _statusLine, new NativeMenuItemSeparator(),
                    _uploadNow, _pause, _resume, new NativeMenuItemSeparator(),
                    _openSite, _show, new NativeMenuItemSeparator(),
                    _quit,
                },
            },
            IsVisible = true,
        };
        _trayIcon.Clicked += OnShow;
    }

    /// <summary>Left click, "Show uploader" and notification clicks: the app shows its window.</summary>
    public event EventHandler? ShowRequested;

    public event EventHandler? QuitRequested;

    /// <summary>The app's Avalonia tray icon (registered on the Application by App).</summary>
    public TrayIcon Icon => _trayIcon;

    /// <summary>True while the main window is in front: no notifications then (docs/DESIGN-NOTES.md).</summary>
    public Func<bool> IsWindowInFront { get; set; } = () => false;

    public void Start()
    {
        _loop.StatusChanged += OnStatusChanged;
        _updates.Changed += OnUpdatesChanged;
        Refresh();
        _ = LoadSiteNameAsync();
    }

    /// <summary>After "Reconnect": the site's name may be known now.</summary>
    public void Reload()
    {
        Refresh();
        _ = LoadSiteNameAsync();
    }

    public void Dispose()
    {
        _loop.StatusChanged -= OnStatusChanged;
        _updates.Changed -= OnUpdatesChanged;
        _restartToUpdate.Click -= OnRestartToUpdate;
        _animation.Stop();
        _animation.Tick -= OnFrame;
        _trayIcon.Clicked -= OnShow;
        _uploadNow.Click -= OnUploadNow;
        _pauseHour.Click -= OnPauseHour;
        _pauseTomorrow.Click -= OnPauseTomorrow;
        _resume.Click -= OnResume;
        _openSite.Click -= OnOpenSite;
        _show.Click -= OnShow;
        _quit.Click -= OnQuit;
        _trayIcon.Dispose();
    }

    private static WindowIcon LoadIcon(string file) => new(AssetLoader.Open(new Uri(IconFolder + file)));

    private static TimeSpan FrameTime() =>
        Application.Current?.TryFindResource("AppDurTrayFrame", out object? value) == true && value is TimeSpan frame ? frame : TimeSpan.FromSeconds(0.25);

    // The loop reports from its own threads.
    private void OnStatusChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        Refresh();
        Notify();
    });

    private void OnUpdatesChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(Refresh);

    private void OnRestartToUpdate(object? sender, EventArgs e) => _updates.RestartNow();

    private void Refresh()
    {
        bool updateNeeded = _updates.IsRequired;
        var model = TrayModel.For(_loop.Statuses, _siteName, _options.BaseUri.Host, _time.GetUtcNow(), _time.LocalTimeZone, _loop.PausedUntil, updateNeeded);

        // Normal updates leave the tray as it is; a required one leads the menu (update-states board).
        bool canRestart = updateNeeded && _updates.State == UpdateState.Ready;
        _restartToUpdate.Header = $"Restart to update to {UpdatePanel.ShortVersion(_updates.NewVersion)}";
        _restartToUpdate.IsVisible = canRestart;
        _restartSeparator.IsVisible = canRestart;
        _uploadNow.Header = updateNeeded ? "Upload now · paused until updated" : "Upload now";
        _uploadNow.IsEnabled = !updateNeeded;
        _trayIcon.ToolTipText = model.Tooltip;
        _siteLine.Header = _siteName;
        _statusLine.Header = model.StatusLine;
        _openSite.Header = $"Open {_siteName.Replace(" Uploader", "", StringComparison.Ordinal)} site";
        bool paused = _loop.PausedUntil is not null;
        _pause.IsVisible = !paused;
        _resume.IsVisible = paused;

        // Uploading animates its four frames; with animations off it stays on the first (reduced motion).
        if (model.Icon == TrayIconKind.Uploading && _motion.AnimationsEnabled)
        {
            if (!_animation.IsEnabled)
            {
                _frame = 0;
                _animation.Start();
            }
        }
        else
        {
            _animation.Stop();
            _trayIcon.Icon = _icons[model.Icon];
        }
    }

    private void Notify()
    {
        var level = _settings.Load() is SettingsLoad.Loaded { Settings: var s } ? s.Notifications : NotificationLevel.EveryUpload;
        var events = _policy.Update(_loop.Statuses, level);
        if (IsWindowInFront())
        {
            return;
        }

        foreach (var e in events)
        {
            _notifier.Show(NotificationTexts.For(e, _options.BaseUri.Host));
        }
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        _frame = (_frame + 1) % _frames.Count;
        _trayIcon.Icon = _frames[_frame];
    }

    private async Task LoadSiteNameAsync()
    {
        if (_tokens.Load() is { } token && (await _site.GetIdentityAsync(token)).Value is { SiteName.Length: > 0 } me)
        {
            _siteName = $"{me.SiteName} Uploader";
            Dispatcher.UIThread.Post(Refresh);
        }
    }

    private void OnUploadNow(object? sender, EventArgs e) => _loop.UploadNow();

    private void OnPauseHour(object? sender, EventArgs e) => _loop.Pause(_time.GetUtcNow().AddHours(1));

    // "Until tomorrow": until 06:00 the next morning, local time.
    private void OnPauseTomorrow(object? sender, EventArgs e)
    {
        var local = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _time.LocalTimeZone);
        var morning = new DateTimeOffset(local.Date.AddDays(1).AddHours(6), _time.LocalTimeZone.GetUtcOffset(local.Date.AddDays(1).AddHours(6)));
        _loop.Pause(morning);
    }

    private void OnResume(object? sender, EventArgs e) => _loop.EndPause();

    private void OnOpenSite(object? sender, EventArgs e) => _browser.Open(_options.BaseUri);

    private void OnShow(object? sender, EventArgs e) => ShowRequested?.Invoke(this, EventArgs.Empty);

    private void OnQuit(object? sender, EventArgs e) => QuitRequested?.Invoke(this, EventArgs.Empty);
}
