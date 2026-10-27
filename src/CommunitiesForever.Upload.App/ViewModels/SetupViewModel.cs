using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommunitiesForever.Upload.App.ViewModels;

/// <summary>Setup step 3 (board <c>setup</c>): how the app runs. Stored now; UP-08 makes the switches work.</summary>
public sealed partial class HowItRunsStepViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool UploadAutomatically { get; set; } = true;

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; } = true;

    [ObservableProperty]
    public partial bool CloseHidesToTray { get; set; } = true;

    /// <summary>0 every upload, 1 only problems, 2 off (the segmented control's order).</summary>
    [ObservableProperty]
    public partial int Notifications { get; set; }

    public void Load(AppSettings settings)
    {
        UploadAutomatically = settings.UploadAutomatically;
        StartWithWindows = settings.StartWithWindows;
        CloseHidesToTray = settings.CloseHidesToTray;
        Notifications = (int)settings.Notifications;
    }
}

/// <summary>The first-run setup (UP-05): connect, find World of Warcraft, how it runs; saves settings v1 at the end.</summary>
public sealed partial class SetupViewModel : ObservableObject, IDisposable
{
    private readonly ISiteClient _site;
    private readonly ITokenStore _tokens;
    private readonly SettingsStore _settings;
    private readonly SiteOptions _options;
    private AppSettings _existing = new();

    public SetupViewModel(ConnectFlow connect, Core.Game.GameScanner scanner, ISiteClient site, ITokenStore tokens, SettingsStore settings, SiteOptions options, TimeProvider time)
    {
        _site = site;
        _tokens = tokens;
        _settings = settings;
        _options = options;
        Connect = new ConnectStepViewModel(connect, options);
        Find = new FindGameStepViewModel(scanner, time);
        Connect.Connected += OnConnected;
    }

    /// <summary>Settings were saved; the app switches to the main window.</summary>
    public event EventHandler? Finished;

    /// <summary>"Cancel" in step 1: nothing to do without a connection, so the app quits.</summary>
    public event EventHandler? Cancelled;

    public ConnectStepViewModel Connect { get; }

    public FindGameStepViewModel Find { get; }

    public HowItRunsStepViewModel HowItRuns { get; } = new();

    public IUiServices? Ui
    {
        get => Connect.Ui;
        set
        {
            Connect.Ui = value;
            Find.Ui = value;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep1), nameof(IsStep2), nameof(IsStep3), nameof(Step1Done), nameof(Step2Done), nameof(Step2Text))]
    public partial int Step { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Step1Text), nameof(Title))]
    public partial SiteIdentity? Identity { get; set; }

    public bool IsStep1 => Step == 1;

    public bool IsStep2 => Step == 2;

    public bool IsStep3 => Step == 3;

    public bool Step1Done => Step > 1;

    public bool Step2Done => Step > 2;

    public string Step1Text => Identity is { } me ? $"Connected as {me.BattleTag}" : "Connect";

    public string Step2Text => Step > 2 ? WatchedText(Find.Chosen.Count) : "Find World of Warcraft";

    /// <summary>"CommunitiesForever Uploader" until the site's name is known (docs/DESIGN-NOTES.md).</summary>
    public string Title => Identity is { SiteName.Length: > 0 } me ? $"{me.SiteName} Uploader" : App.DefaultName;

    /// <summary>"Reconnect": start at step 1 even with a (revoked) token.</summary>
    public bool ConnectFirst { get; set; }

    /// <summary>Settings from an earlier run (setup reopened because no account is watched), or null.</summary>
    public AppSettings? Existing { get; set; }

    /// <summary>Opens at step 1 without a token, else at step 2 (no watched account yet). Called when the window opens.</summary>
    public async Task StartAsync()
    {
        _existing = Existing ?? new AppSettings();
        HowItRuns.Load(_existing);
        if (!ConnectFirst && _tokens.Load() is { } token)
        {
            await ShowFindStepAsync();
            // Best effort: show who is connected; offline leaves the plain title.
            var identity = await _site.GetIdentityAsync(token);
            Identity ??= identity.Value;
        }
        else
        {
            Step = 1;
            await Connect.StartAsync();
        }
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Back() => Step = Math.Max(1, Step - 1);

    [RelayCommand]
    private void Next() => Step = Math.Min(3, Step + 1);

    [RelayCommand]
    private void StartWatching()
    {
        _settings.Save(_existing with
        {
            Accounts = Find.Chosen,
            UploadAutomatically = HowItRuns.UploadAutomatically,
            StartWithWindows = HowItRuns.StartWithWindows,
            CloseHidesToTray = HowItRuns.CloseHidesToTray,
            Notifications = (NotificationLevel)HowItRuns.Notifications,
            SiteUri = _options.BaseUri == SiteOptions.DefaultBaseUri ? null : _options.BaseUri,
        });
        Finished?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Connect.Connected -= OnConnected;
        Connect.Dispose();
        Ui = null;
    }

    private async void OnConnected(object? sender, SiteIdentity identity)
    {
        Identity = identity;
        await ShowFindStepAsync();
    }

    private async Task ShowFindStepAsync()
    {
        Step = 2;
        await Find.ScanAsync();
        if (_existing.Accounts.Count > 0)
        {
            Find.Choose(_existing.Accounts);
        }
    }

    private static string WatchedText(int count) => count == 1 ? "1 account watched" : $"{count} accounts watched";
}
