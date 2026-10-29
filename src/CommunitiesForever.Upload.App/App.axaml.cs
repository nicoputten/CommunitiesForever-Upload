using System.Reflection;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunitiesForever.Upload.App.Tray;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.App.Views;
using CommunitiesForever.Upload.Core;
using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Notifications;
using CommunitiesForever.Upload.Core.Runs;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Updates;
using CommunitiesForever.Upload.Core.Uploading;
using CommunitiesForever.Upload.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CommunitiesForever.Upload.App;

internal sealed partial class App : Application
{
    internal const string DefaultName = "CommunitiesForever Uploader";

    // Added to the application styles while the user has animations switched off: no transitions anywhere.
    private readonly Style _noMotion = new(x => x.Is<Control>())
    {
        Setters = { new Setter(Animatable.TransitionsProperty, null) },
    };

    // Looping animations (Styles/Motion.axaml), added only while animations are on.
    private readonly StyleInclude _motion = new(new Uri("avares://CommunitiesForever.Upload.App/"))
    {
        Source = new Uri("/Styles/Motion.axaml", UriKind.Relative),
    };

    private ServiceProvider? _services;
    private TrayController? _tray;
    private Window? _mainWindow;
    private bool _quitting;
    private SetupViewModel? _setup;
    private MainViewModel? _main;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The app lives in the tray: it ends by "Quit", not by closing a window (UP-08).
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            string[] args = desktop.Args ?? [];
            _services = ConfigureServices(args);
            RegisterWithWindows();
            _tray = CreateTray(_services);
            // A tray icon only shows once it is registered on the Application.
            TrayIcon.SetIcons(this, [_tray.Icon]);
            desktop.Exit += OnExit;

            var first = FirstWindow(args, _services);
            if (args.Contains(RegistryAutostart.TrayArgument) && first is MainWindow)
            {
                // Started with Windows: only the tray; the window opens from there.
                _mainWindow = first;
                Watch(first);
            }
            else
            {
                SetMainWindow(desktop, first);
            }

            ApplyMotion();
            SyncAutostart();
            _tray.Start();
            var updates = _services.GetRequiredService<UpdateChecker>();
            updates.RestartRequested += OnUpdateRestart;
            updates.Start();
            if (Instance is { } instance)
            {
                instance.ArgumentsReceived += OnArgumentsReceived;
                instance.Listen();
            }

            // Started by a notification's link while the app wasn't running.
            Dispatcher.UIThread.Post(() => HandleLinks(args));
        }

        base.OnFrameworkInitializationCompleted();
    }

    internal static ServiceProvider ConfigureServices(string[]? args = null)
    {
        string version = AppVersion();
        var folder = GameFolder(args ?? []);
        var services = new ServiceCollection()
            .AddCore()
            .AddWindowsAdapters(folder.IsRetailTest ? AppPaths.RetailTestDirectory : null);
        services.AddSingleton(new SiteOptions(SiteUri(args ?? []), version));
        services.AddSingleton(folder);
        // Rolling files in the app's logs folder; disposed with the container.
        services.AddLogging(logging => logging
            .SetMinimumLevel(LogLevel.Information)
            .AddProvider(new RollingFileLoggerProvider(AppPaths.LogDirectory)));
        // One HttpClient for the app's lifetime, disposed with the container.
        services.AddSingleton(_ => SiteHttp.Create(version));
        services.AddTransient<MainWindow>();
        services.AddTransient<GalleryWindow>();
        services.AddTransient<SetupViewModel>();
        services.AddTransient<MainViewModel>();
        // Not resolved as a transient: the container would keep every settings page it ever made (CLAUDE.md rule 11).
        services.AddSingleton<Func<SettingsViewModel>>(sp => () => Configure(ActivatorUtilities.CreateInstance<SettingsViewModel>(sp)));
        services.AddTransient<ConnectWindowViewModel>();
        services.AddSingleton<TrayController>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>Follows Windows' "Animation effects": off removes every transition and looping animation.</summary>
    internal void ApplyMotion(bool animationsEnabled)
    {
        Toggle(_noMotion, !animationsEnabled);
        Toggle(_motion, animationsEnabled);
    }

    private void Toggle(IStyle style, bool on)
    {
        bool applied = Styles.Contains(style);
        if (on && !applied)
        {
            Styles.Add(style);
        }
        else if (!on && applied)
        {
            Styles.Remove(style);
        }
    }

    /// <summary>The single-instance lock Program took; later starts send their arguments here.</summary>
    internal static SingleInstance? Instance { get; set; }

    // Development switches (--gallery, --site) work in Debug builds only.
    private static readonly bool DevSwitchesAllowed = IsDebugBuild();

    private static bool IsDebugBuild()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    /// <summary>The site: wow.vestras.nl; Debug builds accept <c>--site http://localhost:3000</c> for the web dev server.</summary>
    internal static Uri SiteUri(string[] args)
    {
        int at = Array.IndexOf(args, "--site");
        return DevSwitchesAllowed && at >= 0 && at + 1 < args.Length && Uri.TryCreate(args[at + 1], UriKind.Absolute, out var uri)
            ? new Uri(uri.AbsoluteUri.TrimEnd('/') + "/")
            : SiteOptions.DefaultBaseUri;
    }

    /// <summary>
    /// Retail test mode: <c>--retail</c> watches and writes the <c>_retail_</c> folder instead of <c>_forever_</c>, with its
    /// own settings and history, for testing the addon on the retail client (docs/RETAIL-TEST-MODE.md). Works in
    /// release builds too; off unless started with the flag.
    /// </summary>
    internal static GameFolderOptions GameFolder(string[] args) =>
        args.Contains(RetailArgument, StringComparer.OrdinalIgnoreCase) ? GameFolderOptions.RetailTest : GameFolderOptions.Forever;

    internal const string RetailArgument = "--retail";

    private static string AppVersion()
    {
        string? informational = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        // "1.0.0+<commit>": the site only needs the version.
        return informational?.Split('+')[0] ?? "0.0.0";
    }

    /// <summary>
    /// Setup without a token or without a watched account (UP-05), else the main window.
    /// A settings file from a newer version is left alone: the main window, nothing is overwritten.
    /// </summary>
    private Window FirstWindow(string[] args, ServiceProvider services)
    {
        if (DevSwitchesAllowed && args.Contains("--gallery"))
        {
            return services.GetRequiredService<GalleryWindow>();
        }

        var load = services.GetRequiredService<SettingsStore>().Load();
        var settings = (load as SettingsLoad.Loaded)?.Settings;
        bool connected = services.GetRequiredService<ITokenStore>().Load() is not null;
        if (load is SettingsLoad.FromNewerVersion || (connected && settings is { Accounts.Count: > 0 }))
        {
            if (settings is not null)
            {
                StartLoop(services, settings);
            }

            return CreateMain(services);
        }

        return CreateSetup(services, settings);
    }

    private MainWindow CreateMain(ServiceProvider services)
    {
        ForgetMain();
        _main = services.GetRequiredService<MainViewModel>();
        _main.RetailTestMode = services.GetRequiredService<GameFolderOptions>().IsRetailTest;
        _main.ReconnectRequested += OnReconnectRequested;
        _main.AccountsChanged += OnAccountsChanged;
        _main.SettingsChanged += OnSettingsChanged;
        var window = services.GetRequiredService<MainWindow>();
        window.DataContext = _main;
        window.HidesToTray = Settings(services)?.CloseHidesToTray ?? true;
        return window;
    }

    // The wizard is for the first run only (docs/DESIGN-NOTES.md).
    private SetupWindow CreateSetup(ServiceProvider services, AppSettings? settings)
    {
        _setup = services.GetRequiredService<SetupViewModel>();
        _setup.RetailTestMode = services.GetRequiredService<GameFolderOptions>().IsRetailTest;
        _setup.Existing = settings;
        _setup.Finished += OnSetupFinished;
        _setup.Cancelled += OnSetupCancelled;
        return new SetupWindow { DataContext = _setup };
    }

    // "Reconnect": the Connect window on top of the main window; once connected, watching restarts with the new token.
    private async void OnReconnectRequested(object? sender, EventArgs e)
    {
        if (_services is null || _mainWindow is null)
        {
            return;
        }

        using var vm = _services.GetRequiredService<ConnectWindowViewModel>();
        var window = new ConnectWindow { DataContext = vm };
        void Close(object? s, EventArgs a) => window.Close();
        vm.Connected += Close;
        vm.Cancelled += Close;
        await window.ShowDialog(_mainWindow);
        vm.Connected -= Close;
        vm.Cancelled -= Close;
        if (_services.GetRequiredService<ITokenStore>().Load() is not null)
        {
            RestartWatching();
            _tray?.Reload();
            _main?.Settings?.TestConnectionCommand.Execute(null);
        }
    }

    private void OnAccountsChanged(object? sender, EventArgs e)
    {
        RestartWatching();
        _tray?.Reload();
    }

    // Settings saved (UP-09): autostart and the close button follow right away.
    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        SyncAutostart();
        if (_mainWindow is MainWindow window)
        {
            window.HidesToTray = settings.CloseHidesToTray;
        }
    }

    // The checker handed the update to the installer, which starts the app again: quit (UP-10).
    private void OnUpdateRestart(object? sender, bool quiet) => Dispatcher.UIThread.Post(Quit);

    private static SettingsViewModel Configure(SettingsViewModel vm)
    {
        vm.LogFolder = AppPaths.LogDirectory;
        vm.LicencesFile = Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.txt");
        return vm;
    }

    // New accounts or a new token: restart the loop with the saved settings and refresh the main window.
    private void RestartWatching()
    {
        if (_services?.GetRequiredService<SettingsStore>().Load() is SettingsLoad.Loaded { Settings: var settings })
        {
            StartLoop(_services, settings);
        }

        _main?.Reload();
    }

    private void SwitchTo(IClassicDesktopStyleApplicationLifetime desktop, Window next)
    {
        var previous = _mainWindow;
        SetMainWindow(desktop, next);
        next.Show();
        // No longer the app's window: closing it doesn't quit.
        previous?.Close();
    }

    private void ForgetMain()
    {
        if (_main is { } main)
        {
            main.ReconnectRequested -= OnReconnectRequested;
            main.AccountsChanged -= OnAccountsChanged;
            main.SettingsChanged -= OnSettingsChanged;
            _main = null;
        }
    }

    private void OnSetupFinished(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || _services is null)
        {
            return;
        }

        ForgetSetup();
        if (_services.GetRequiredService<SettingsStore>().Load() is SettingsLoad.Loaded { Settings: var settings })
        {
            StartLoop(_services, settings);
        }

        SyncAutostart();
        _tray?.Reload();
        SwitchTo(desktop, CreateMain(_services));
    }

    // The watch-and-upload loop (UP-06) and the runs to the game (UP-12); the container stops them on exit.
    private static void StartLoop(ServiceProvider services, AppSettings settings)
    {
        _ = services.GetRequiredService<UploadLoop>().StartAsync(settings);
        services.GetRequiredService<RunsSync>().Start(settings);
    }

    private void OnSetupCancelled(object? sender, EventArgs e)
    {
        // First run only: without a connection there is nothing to do.
        ForgetSetup();
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    private void ForgetSetup()
    {
        if (_setup is { } setup)
        {
            setup.Finished -= OnSetupFinished;
            setup.Cancelled -= OnSetupCancelled;
            _setup = null;
        }
    }

    // The window the app lives in; moves its handlers along (CLAUDE.md rule 11).
    private void SetMainWindow(IClassicDesktopStyleApplicationLifetime desktop, Window window)
    {
        if (_mainWindow is not null)
        {
            Unwatch(_mainWindow);
        }

        _mainWindow = window;
        Watch(window);
        desktop.MainWindow = window;
    }

    private void Watch(Window window)
    {
        window.Activated += OnMainWindowActivated;
        window.Closing += OnMainWindowClosing;
        window.Closed += OnMainWindowClosed;
    }

    private void Unwatch(Window window)
    {
        window.Activated -= OnMainWindowActivated;
        window.Closing -= OnMainWindowClosing;
        window.Closed -= OnMainWindowClosed;
    }

    // "Close hides to the tray" (setup step 3): the main window hides instead; Quit is in the tray menu.
    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_quitting && e.CloseReason != WindowCloseReason.ApplicationShutdown && sender is MainWindow { HidesToTray: true } window)
        {
            e.Cancel = true;
            window.Hide();
            _services?.GetRequiredService<TrayMemory>().Hidden();
        }
    }

    // Closing the window the app lives in (setup, or the main window without "hide to tray") quits.
    private void OnMainWindowClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _mainWindow))
        {
            Quit();
        }
    }

    private void Quit()
    {
        _quitting = true;
        // A downloaded update goes in once the app has quit (UP-10).
        _services?.GetRequiredService<UpdateChecker>().ApplyIfReady();
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    private TrayController CreateTray(ServiceProvider services)
    {
        var tray = services.GetRequiredService<TrayController>();
        tray.IsWindowInFront = () => _mainWindow is { IsVisible: true, IsActive: true };
        tray.ShowRequested += OnShowRequested;
        tray.QuitRequested += OnQuitRequested;
        return tray;
    }

    private void OnShowRequested(object? sender, EventArgs e) => ShowMainWindow();

    private void OnQuitRequested(object? sender, EventArgs e) => Quit();

    // A later start (Start menu, a notification's link) hands its arguments over (Program, SingleInstance).
    private void OnArgumentsReceived(object? sender, string[] args) => Dispatcher.UIThread.Post(() =>
    {
        if (!args.Contains(RegistryAutostart.TrayArgument))
        {
            ShowMainWindow();
        }

        HandleLinks(args);
    });

    // Notification links: …://open shows the window, …://reconnect also opens Connect.
    private void HandleLinks(string[] args)
    {
        foreach (string arg in args)
        {
            if (!Uri.TryCreate(arg, UriKind.Absolute, out var link) || link.Scheme != AppIdentity.Scheme)
            {
                continue;
            }

            ShowMainWindow();
            if (link.Host == AppIdentity.Reconnect.Host && _mainWindow is MainWindow)
            {
                OnReconnectRequested(this, EventArgs.Empty);
            }
        }
    }

    private void ShowMainWindow()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || _mainWindow is not { } window)
        {
            return;
        }

        desktop.MainWindow = window;
        _services?.GetRequiredService<TrayMemory>().Shown();
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    // The notification sender and the app link scheme, for this Windows user (UP-10 removes them on uninstall).
    private static void RegisterWithWindows()
    {
        try
        {
            ShellRegistration.Register(Environment.ProcessPath ?? "", Path.Combine(AppContext.BaseDirectory, "app-icon.png"));
            // Windows takes a notification's header (name, icon) from the Start menu shortcut.
            StartMenuShortcut.Ensure(Environment.ProcessPath ?? "");
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException or System.Runtime.InteropServices.COMException)
        {
            // Without it there are no notifications; the app itself works.
        }
    }

    // "Start with Windows" follows the setting. Debug builds leave the Run key alone (it would start a dev build).
    private void SyncAutostart()
    {
        // Retail test mode leaves the Run entry to the normal settings: Windows starts the normal mode.
        if (!IsDebugBuild() && _services is not null && !_services.GetRequiredService<GameFolderOptions>().IsRetailTest
            && Settings(_services) is { } settings)
        {
            _services.GetRequiredService<IAutostart>().Apply(settings.StartWithWindows);
        }
    }

    private static AppSettings? Settings(ServiceProvider services) =>
        (services.GetRequiredService<SettingsStore>().Load() as SettingsLoad.Loaded)?.Settings;

    private void ApplyMotion()
    {
        if (_services is not null)
        {
            ApplyMotion(_services.GetRequiredService<IMotionPreference>().AnimationsEnabled);
        }
    }

    // The setting can change while the app runs; checking on activation is enough and needs no system hook.
    private void OnMainWindowActivated(object? sender, EventArgs e) => ApplyMotion();

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        // Long-running app: release everything we created (CLAUDE.md rule 11).
        if (sender is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit -= OnExit;
        }

        if (_mainWindow is not null)
        {
            Unwatch(_mainWindow);
            _mainWindow = null;
        }

        ForgetSetup();
        ForgetMain();

        if (Instance is { } instance)
        {
            instance.ArgumentsReceived -= OnArgumentsReceived;
        }

        if (_tray is { } tray)
        {
            tray.ShowRequested -= OnShowRequested;
            tray.QuitRequested -= OnQuitRequested;
            _tray = null; // disposed with the container
        }

        if (_services is not null)
        {
            _services.GetRequiredService<UpdateChecker>().RestartRequested -= OnUpdateRestart;
        }

        _services?.Dispose();
        _services = null;
    }
}
