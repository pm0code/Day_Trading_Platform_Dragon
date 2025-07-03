using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Configuration;

namespace TradingPlatform.Core.Extensions
{
    /// <summary>
    /// Extension methods for service registration
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds API configuration services
        /// </summary>
        public static IServiceCollection AddApiConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            // Register configuration as singleton
            services.AddSingleton<ApiConfiguration>();
            
            // Bind configuration section if needed
            services.Configure<ApiConfiguration>(configuration.GetSection("ApiConfiguration"));
            
            return services;
        }
        
        /// <summary>
        /// Adds core trading platform services
        /// </summary>
        public static IServiceCollection AddTradingPlatformCore(this IServiceCollection services, IConfiguration configuration)
        {
            // Add API configuration
            services.AddApiConfiguration(configuration);
            
            // Add memory cache for market data
            services.AddMemoryCache();
            
            // Add HTTP clients
            services.AddHttpClient();
            
            return services;
        }
    }
}