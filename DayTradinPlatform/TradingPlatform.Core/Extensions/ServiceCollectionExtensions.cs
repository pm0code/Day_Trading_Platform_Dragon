using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Configuration;

namespace TradingPlatform.Core.Extensions
{
    /// <summary>
    /// Extension methods for service registration
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds encrypted configuration services
        /// </summary>
        public static IServiceCollection AddEncryptedConfiguration(this IServiceCollection services)
        {
            // Register encrypted configuration
            services.AddSingleton<EncryptedConfiguration>();
            
            // Register configuration service
            services.AddSingleton<ConfigurationService>();
            services.AddSingleton<IConfigurationService>(provider => 
                provider.GetRequiredService<ConfigurationService>());
            
            return services;
        }
        
        /// <summary>
        /// Adds core trading platform services
        /// </summary>
        public static IServiceCollection AddTradingPlatformCore(this IServiceCollection services, IConfiguration configuration)
        {
            // Add encrypted configuration
            services.AddEncryptedConfiguration();
            
            // Add memory cache for market data
            services.AddMemoryCache();
            
            // Add HTTP clients
            services.AddHttpClient();
            
            // Add logging
            services.AddLogging();
            
            return services;
        }
    }
}