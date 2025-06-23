using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using TradingPlatform.DisplayManagement.Models;
using TradingPlatform.DisplayManagement.Services;

namespace TradingPlatform.DisplayManagement.Extensions;

/// <summary>
/// Service registration extensions for TradingPlatform.DisplayManagement
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register all display management services for DRAGON trading platform
    /// </summary>
    public static IServiceCollection AddDisplayManagement(this IServiceCollection services, IConfiguration configuration)
    {
        // Register display session management services
        services.Configure<DisplaySessionOptions>(configuration.GetSection("DisplaySession"));
        services.AddSingleton<IDisplaySessionService, DisplaySessionService>();
        services.AddHostedService<DisplaySessionService>(provider =>
            (DisplaySessionService)provider.GetRequiredService<IDisplaySessionService>());

        // Register GPU detection services
        RegisterGpuDetectionServices(services);

        // Register monitor detection services  
        RegisterMonitorDetectionServices(services);

        return services;
    }

    /// <summary>
    /// Register GPU detection services with mock fallback for RDP/testing
    /// </summary>
    private static void RegisterGpuDetectionServices(IServiceCollection services)
    {
        // Determine if running in RDP session for mock services
        var isRdpSession = IsRunningViaRdp();

        if (isRdpSession)
        {
            // Use mock services for RDP testing
            services.AddScoped<IGpuDetectionService, MockGpuDetectionService>();
        }
        else
        {
            // Use real hardware detection for direct console access
            services.AddScoped<IGpuDetectionService, GpuDetectionService>();
        }
    }

    /// <summary>
    /// Register monitor detection services with mock fallback for RDP/testing
    /// </summary>
    private static void RegisterMonitorDetectionServices(IServiceCollection services)
    {
        // Determine if running in RDP session for mock services
        var isRdpSession = IsRunningViaRdp();

        if (isRdpSession)
        {
            // Use mock services for RDP testing
            services.AddScoped<IMonitorDetectionService, MockMonitorDetectionService>();
        }
        else
        {
            // Use real hardware detection for direct console access
            services.AddScoped<IMonitorDetectionService, MonitorDetectionService>();
        }
    }

    /// <summary>
    /// Detect if running via RDP session
    /// </summary>
    private static bool IsRunningViaRdp()
    {
        try
        {
            var sessionName = Environment.GetEnvironmentVariable("SESSIONNAME");
            return !string.IsNullOrEmpty(sessionName) &&
                   (sessionName.StartsWith("RDP-", StringComparison.OrdinalIgnoreCase) ||
                    !sessionName.Equals("Console", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
