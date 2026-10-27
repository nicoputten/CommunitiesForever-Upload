using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using CommunitiesForever.Upload.Core;
using CommunitiesForever.Upload.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace CommunitiesForever.Upload.App;

internal sealed partial class App : Application
{
    internal const string DefaultName = "CommunitiesForever Uploader";

    private ServiceProvider? _services;
    private TrayIcon? _trayIcon;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _services = ConfigureServices();
            desktop.MainWindow = _services.GetRequiredService<MainWindow>();
            _trayIcon = CreateTrayIcon(desktop);
            // A tray icon only shows once it is registered on the Application.
            TrayIcon.SetIcons(this, [_trayIcon]);
            desktop.Exit += OnExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    internal static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection()
            .AddCore()
            .AddWindowsAdapters();
        services.AddTransient<MainWindow>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static TrayIcon CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var open = new NativeMenuItem("Open");
        open.Click += (_, _) => ShowMainWindow(desktop);
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => desktop.Shutdown();

        var trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://CommunitiesForever.Upload.App/Assets/icon.png"))),
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

        _trayIcon?.Dispose();
        _trayIcon = null;
        _services?.Dispose();
        _services = null;
    }
}
