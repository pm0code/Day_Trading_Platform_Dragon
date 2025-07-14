using Microsoft.Extensions.DependencyInjection;

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
        // Register watchdog services here
        
        return services;
    }
}