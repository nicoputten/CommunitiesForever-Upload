using CommunitiesForever.Upload.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CommunitiesForever.Upload.Windows;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the Windows adapters for Core's interfaces.</summary>
    public static IServiceCollection AddWindowsAdapters(this IServiceCollection services)
    {
        services.AddSingleton<IMotionPreference, WindowsMotionPreference>();
        return services;
    }
}
