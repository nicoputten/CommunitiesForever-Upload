using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Notifications;
using CommunitiesForever.Upload.Core.Runs;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Updates;
using CommunitiesForever.Upload.Core.Uploading;
using Microsoft.Extensions.DependencyInjection;

namespace CommunitiesForever.Upload.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Core services. Core never builds a container itself; App does, and provides the shared
    /// <see cref="HttpClient"/> and the <see cref="SiteOptions"/>.
    /// </summary>
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISiteClient, SiteClient>();
        services.AddTransient<ConnectFlow>();
        services.AddSingleton<SettingsStore>();
        services.AddTransient<GameScanner>();
        services.AddSingleton<UploadHistory>();
        services.AddSingleton<NotificationPolicy>();
        services.AddSingleton<UploadLoop>();
        services.AddSingleton<IUploadLoop>(sp => sp.GetRequiredService<UploadLoop>());
        services.AddSingleton<IUpdateMemoryFile, UpdateMemoryStore>();
        services.AddSingleton<UpdateChecker>();
        services.AddSingleton<RunsSync>();
        services.AddSingleton<TrayMemory>();
        return services;
    }
}
