using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingPlatform.SecureConfiguration.Builders;
using TradingPlatform.SecureConfiguration.Core;

namespace TradingPlatform.SecureConfiguration.Extensions
{
    /// <summary>
    /// Extension methods for dependency injection
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add secure configuration with a builder pattern
        /// </summary>
        public static IServiceCollection AddSecureConfiguration(
            this IServiceCollection services,
            Action<SecureConfigurationBuilder> configure)
        {
            var builder = SecureConfigurationBuilder.Create();
            configure(builder);
            
            var configuration = builder.Build();
            services.AddSingleton<ISecureConfiguration>(configuration);
            
            return services;
        }

        /// <summary>
        /// Add secure configuration for a trading platform
        /// </summary>
        public static IServiceCollection AddTradingPlatformConfiguration(
            this IServiceCollection services,
            string platformName = "TradingPlatform")
        {
            return services.AddSecureConfiguration(builder => builder
                .ForApplication(platformName, $"{platformName} Trading System")
                .WithApiKey("AlphaVantage", environmentVariable: "ALPHAVANTAGE_API_KEY")
                .WithApiKey("Finnhub", environmentVariable: "FINNHUB_API_KEY")
                .WithApiKey("IexCloud", required: false, environmentVariable: "IEXCLOUD_API_KEY")
                .WithApiKey("Polygon", required: false, environmentVariable: "POLYGON_API_KEY")
                .WithApiKey("TwelveData", required: false, environmentVariable: "TWELVEDATA_API_KEY")
                .WithDatabaseConnection("TradingDb", required: false, environmentVariable: "TRADING_DATABASE_URL")
                .WithMode(ConfigurationMode.Interactive));
        }

        /// <summary>
        /// Add secure configuration for a financial data provider service
        /// </summary>
        public static IServiceCollection AddFinancialProviderConfiguration(
            this IServiceCollection services,
            string providerName)
        {
            return services.AddSecureConfiguration(builder => builder
                .ForFinancialDataProvider(providerName));
        }

        /// <summary>
        /// Add secure configuration for a banking system
        /// </summary>
        public static IServiceCollection AddBankingConfiguration(
            this IServiceCollection services,
            string bankName)
        {
            return services.AddSecureConfiguration(builder => builder
                .ForBankingSystem(bankName));
        }

        /// <summary>
        /// Add secure configuration for a crypto exchange
        /// </summary>
        public static IServiceCollection AddCryptoExchangeConfiguration(
            this IServiceCollection services,
            string exchangeName)
        {
            return services.AddSecureConfiguration(builder => builder
                .ForCryptoExchange(exchangeName));
        }

        /// <summary>
        /// Add secure configuration with custom setup
        /// </summary>
        public static IServiceCollection AddCustomSecureConfiguration(
            this IServiceCollection services,
            string applicationName,
            string displayName,
            Action<SecureConfigurationBuilder> configure)
        {
            return services.AddSecureConfiguration(builder =>
            {
                builder.ForApplication(applicationName, displayName);
                configure(builder);
            });
        }

        /// <summary>
        /// Initialize secure configuration on startup
        /// </summary>
        public static void InitializeSecureConfiguration(this IServiceProvider serviceProvider)
        {
            var configuration = serviceProvider.GetService<ISecureConfiguration>();
            if (configuration != null)
            {
                var logger = serviceProvider.GetService<ILogger<ISecureConfiguration>>();
                logger?.LogInformation("Initializing secure configuration...");
                
                var result = configuration.InitializeAsync().GetAwaiter().GetResult();
                
                if (!result.IsSuccess)
                {
                    logger?.LogError("Failed to initialize secure configuration: {Error}", result.ErrorMessage);
                    throw new InvalidOperationException($"Configuration initialization failed: {result.ErrorMessage}");
                }
                
                logger?.LogInformation("Secure configuration initialized successfully");
            }
        }
    }
}