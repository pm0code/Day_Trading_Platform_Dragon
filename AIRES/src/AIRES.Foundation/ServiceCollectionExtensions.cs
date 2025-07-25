using AIRES.Foundation.Alerting;
using AIRES.Foundation.Logging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AIRES.Foundation;

/// <summary>
/// Extension methods for registering AIRES Foundation services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds AIRES Foundation services to the service collection.
    /// </summary>
    public static IServiceCollection AddAIRESFoundation(this IServiceCollection services)
    {
        // Register IAIRESLogger
        services.AddSingleton<IAIRESLogger>(provider =>
        {
            var serilogLogger = provider.GetRequiredService<ILogger>();
            return new SerilogAIRESLogger(serilogLogger);
        });
        
        // Register alerting components
        services.AddSingleton<IAlertChannelFactory, AlertChannelFactory>();
        services.AddSingleton<IAlertThrottler, SimpleAlertThrottler>();
        services.AddSingleton<IAlertPersistence, InMemoryAlertPersistence>();
        services.AddSingleton<IAIRESAlertingService, AIRESAlertingService>();
        
        return services;
    }
}