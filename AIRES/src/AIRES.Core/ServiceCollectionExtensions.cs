using Microsoft.Extensions.DependencyInjection;

namespace AIRES.Core;

/// <summary>
/// Extension methods for registering AIRES Core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds AIRES Core domain services to the service collection.
    /// </summary>
    public static IServiceCollection AddAIRESCore(this IServiceCollection services)
    {
        // Register domain services here
        
        return services;
    }
}