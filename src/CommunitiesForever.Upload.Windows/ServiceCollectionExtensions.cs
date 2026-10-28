using CommunitiesForever.Upload.Core;
using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Notifications;
using CommunitiesForever.Upload.Core.Runs;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Updates;
using CommunitiesForever.Upload.Core.Uploading;
using Microsoft.Extensions.DependencyInjection;

namespace CommunitiesForever.Upload.Windows;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the Windows adapters for Core's interfaces.</summary>
    public static IServiceCollection AddWindowsAdapters(this IServiceCollection services)
    {
        services.AddSingleton<IMotionPreference, WindowsMotionPreference>();
        services.AddSingleton<ITokenStore>(_ => new DpapiTokenStore(AppPaths.DataDirectory));
        services.AddSingleton<IBrowser, WindowsBrowser>();
        services.AddSingleton<IDeviceInfo, WindowsDeviceInfo>();
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IInstallHints, WindowsInstallHints>();
        services.AddSingleton<ISettingsFile>(_ => new SettingsFile(AppPaths.DataDirectory));
        services.AddSingleton<IHistoryFile>(_ => new HistoryFile(AppPaths.DataDirectory));
        services.AddSingleton<IAddonFileReader, AddonFileReader>();
        services.AddSingleton<IFileWatcherFactory, FileWatcherFactory>();
        services.AddSingleton<IGameProcesses, GameProcesses>();
        services.AddSingleton<IRunsFiles, RunsFiles>();
        services.AddSingleton<INotifier, WindowsNotifier>();
        services.AddSingleton<IAutostart>(_ => new RegistryAutostart(Environment.ProcessPath ?? ""));
        services.AddSingleton<IAppUpdates, VelopackUpdates>();
        services.AddSingleton<IUpdatesFile>(_ => new UpdatesFile(AppPaths.DataDirectory));
        return services;
    }
}
