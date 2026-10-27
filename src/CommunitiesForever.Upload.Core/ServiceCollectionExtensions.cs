using Microsoft.Extensions.DependencyInjection;

namespace CommunitiesForever.Upload.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the Core services. Core never builds a container itself; App does.</summary>
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
