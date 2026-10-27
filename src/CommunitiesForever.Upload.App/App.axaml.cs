using System.Reflection;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using CommunitiesForever.Upload.Core;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace CommunitiesForever.Upload.App;

internal sealed partial class App : Application
{
    internal const string DefaultName = "CommunitiesForever Uploader";

    // Added to the application styles while the user has animations switched off: no transitions anywhere.
    private readonly Style _noMotion = new(x => x.Is<Control>())
    {
        Setters = { new Setter(Animatable.TransitionsProperty, null) },
    };

    private ServiceProvider? _services;
    private TrayIcon? _trayIcon;
    private Window? _mainWindow;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _services = ConfigureServices(desktop.Args ?? []);
            _mainWindow = CreateMainWindow(desktop.Args ?? [], _services);
            desktop.MainWindow = _mainWindow;
            _trayIcon = CreateTrayIcon(desktop);
            // A tray icon only shows once it is registered on the Application.
            TrayIcon.SetIcons(this, [_trayIcon]);

            ApplyMotion();
            _mainWindow.Activated += OnMainWindowActivated;
            desktop.Exit += OnExit;
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
        // One HttpClient for the app's lifetime, disposed with the container.
        services.AddSingleton(_ => SiteHttp.Create(version));
        services.AddTransient<MainWindow>();
        services.AddTransient<GalleryWindow>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>Follows Windows' "Animation effects": switches every transition off while it is off.</summary>
    internal void ApplyMotion(bool animationsEnabled)
    {
        bool applied = Styles.Contains(_noMotion);
        if (!animationsEnabled && !applied)
        {
            Styles.Add(_noMotion);
        }
        else if (animationsEnabled && applied)
        {
            Styles.Remove(_noMotion);
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

    private static Window CreateMainWindow(string[] args, ServiceProvider services) =>
        DevSwitchesAllowed && args.Contains("--gallery")
            ? services.GetRequiredService<GalleryWindow>()
            : services.GetRequiredService<MainWindow>();

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

        _trayIcon?.Dispose();
        _trayIcon = null;
        _services?.Dispose();
        _services = null;
    }
}
