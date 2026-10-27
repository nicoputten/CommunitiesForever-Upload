using System.Reflection;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Platform;
using Avalonia.Styling;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.App.Views;
using CommunitiesForever.Upload.Core;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
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
    private TrayIcon? _trayIcon;
    private Window? _mainWindow;
    private SetupViewModel? _setup;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string[] args = desktop.Args ?? [];
            _services = ConfigureServices(args);
            _trayIcon = CreateTrayIcon(desktop);
            // A tray icon only shows once it is registered on the Application.
            TrayIcon.SetIcons(this, [_trayIcon]);
            desktop.Exit += OnExit;

            SetMainWindow(desktop, FirstWindow(args, _services));
            ApplyMotion();
        }

        base.OnFrameworkInitializationCompleted();
    }

    internal static ServiceProvider ConfigureServices(string[]? args = null)
    {
        string version = AppVersion();
        var services = new ServiceCollection()
            .AddCore()
            .AddWindowsAdapters();
        services.AddSingleton(new SiteOptions(SiteUri(args ?? []), version));
        // Rolling files in the app's logs folder; disposed with the container.
        services.AddLogging(logging => logging
            .SetMinimumLevel(LogLevel.Information)
            .AddProvider(new RollingFileLoggerProvider(AppPaths.LogDirectory)));
        // One HttpClient for the app's lifetime, disposed with the container.
        services.AddSingleton(_ => SiteHttp.Create(version));
        services.AddTransient<MainWindow>();
        services.AddTransient<GalleryWindow>();
        services.AddTransient<SetupViewModel>();
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

            return services.GetRequiredService<MainWindow>();
        }

        _setup = services.GetRequiredService<SetupViewModel>();
        _setup.Existing = settings;
        _setup.Finished += OnSetupFinished;
        _setup.Cancelled += OnSetupCancelled;
        return new SetupWindow { DataContext = _setup };
    }

    private void OnSetupFinished(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || _services is null)
        {
            return;
        }

        var setupWindow = _mainWindow;
        ForgetSetup();
        if (_services.GetRequiredService<SettingsStore>().Load() is SettingsLoad.Loaded { Settings: var settings })
        {
            StartLoop(_services, settings);
        }

        var main = _services.GetRequiredService<MainWindow>();
        SetMainWindow(desktop, main);
        main.Show();
        setupWindow?.Close();
    }

    // The watch-and-upload loop (UP-06); the container stops it on exit.
    private static void StartLoop(ServiceProvider services, AppSettings settings) =>
        _ = services.GetRequiredService<UploadLoop>().StartAsync(settings);

    private void OnSetupCancelled(object? sender, EventArgs e)
    {
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

    // The window the app lives in; moves the activation handler along (CLAUDE.md rule 11).
    private void SetMainWindow(IClassicDesktopStyleApplicationLifetime desktop, Window window)
    {
        if (_mainWindow is not null)
        {
            _mainWindow.Activated -= OnMainWindowActivated;
        }

        _mainWindow = window;
        _mainWindow.Activated += OnMainWindowActivated;
        desktop.MainWindow = window;
    }

    private void ApplyMotion()
    {
        if (_services is not null)
        {
            ApplyMotion(_services.GetRequiredService<IMotionPreference>().AnimationsEnabled);
        }
    }

    // The setting can change while the app runs; checking on activation is enough and needs no system hook.
    private void OnMainWindowActivated(object? sender, EventArgs e) => ApplyMotion();

    private static TrayIcon CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var open = new NativeMenuItem("Open");
        open.Click += (_, _) => ShowMainWindow(desktop);
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => desktop.Shutdown();

        var trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://CommunitiesForever.Upload.App/Assets/Icons/tray.ico"))),
            ToolTipText = DefaultName,
            Menu = new NativeMenu { Items = { open, quit } },
        };
        trayIcon.Clicked += (_, _) => ShowMainWindow(desktop);
        trayIcon.IsVisible = true;
        return trayIcon;
    }

    private static void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (desktop.MainWindow is not { } window)
        {
            return;
        }

        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        // Long-running app: release everything we created (CLAUDE.md rule 11).
        if (sender is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit -= OnExit;
        }

        if (_mainWindow is not null)
        {
            _mainWindow.Activated -= OnMainWindowActivated;
            _mainWindow = null;
        }

        ForgetSetup();

        _trayIcon?.Dispose();
        _trayIcon = null;
        _services?.Dispose();
        _services = null;
    }
}
