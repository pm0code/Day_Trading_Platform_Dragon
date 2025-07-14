using Microsoft.Extensions.DependencyInjection;
using AIRES.Watchdog.Services;

namespace AIRES.Watchdog;

/// <summary>
/// Extension methods for registering AIRES Watchdog services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds AIRES Watchdog services to the service collection.
    /// </summary>
    public static IServiceCollection AddAIRESWatchdog(this IServiceCollection services)
    {
        // Register watchdog services
        services.AddSingleton<FileWatchdogService>();
        services.AddSingleton<IFileWatchdogService>(provider => provider.GetRequiredService<FileWatchdogService>());
        
        return services;
    }
}