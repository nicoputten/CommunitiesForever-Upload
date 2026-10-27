using CommunitiesForever.Upload.Core;
using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
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
        return services;
    }
}
