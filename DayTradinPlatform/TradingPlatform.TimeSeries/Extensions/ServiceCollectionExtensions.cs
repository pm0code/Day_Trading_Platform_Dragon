using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.TimeSeries.Configuration;
using TradingPlatform.TimeSeries.Interfaces;
using TradingPlatform.TimeSeries.Services;

namespace TradingPlatform.TimeSeries.Extensions
{
    /// <summary>
    /// Extension methods for registering time-series services
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds InfluxDB time-series database services
        /// </summary>
        public static IServiceCollection AddInfluxDbTimeSeries(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Bind configuration
            services.Configure<InfluxDbOptions>(configuration.GetSection("InfluxDb"));
            
            // Register time-series service
            services.AddSingleton<ITimeSeriesService, CanonicalInfluxDbService>();
            
            return services;
        }

        /// <summary>
        /// Adds InfluxDB time-series database services with custom options
        /// </summary>
        public static IServiceCollection AddInfluxDbTimeSeries(
            this IServiceCollection services,
            Action<InfluxDbOptions> configureOptions)
        {
            services.Configure(configureOptions);
            services.AddSingleton<ITimeSeriesService, CanonicalInfluxDbService>();
            
            return services;
        }

        /// <summary>
        /// Adds InfluxDB for development with local defaults
        /// </summary>
        public static IServiceCollection AddInfluxDbTimeSeriesForDevelopment(
            this IServiceCollection services,
            string token = "development-token")
        {
            return services.AddInfluxDbTimeSeries(options =>
            {
                options.Url = "http://localhost:8086";
                options.Token = token;
                options.Organization = "dev-org";
                options.DefaultBucket = "dev-trading-data";
                options.MarketDataBucket = "dev-market-data";
                options.OrderDataBucket = "dev-order-data";
                options.MetricsBucket = "dev-metrics";
                options.EnableDebugLogging = true;
                options.RetentionDays = 30; // Keep dev data for 30 days
            });
        }

        /// <summary>
        /// Adds InfluxDB for production with optimized settings
        /// </summary>
        public static IServiceCollection AddInfluxDbTimeSeriesForProduction(
            this IServiceCollection services,
            string url,
            string token,
            string organization)
        {
            return services.AddInfluxDbTimeSeries(options =>
            {
                options.Url = url;
                options.Token = token;
                options.Organization = organization;
                options.DefaultBucket = "trading-data";
                options.MarketDataBucket = "market-data";
                options.OrderDataBucket = "order-data";
                options.MetricsBucket = "performance-metrics";
                options.BatchSize = 5000; // Larger batches for production
                options.FlushInterval = 1000; // 1 second flush
                options.EnableGzip = true;
                options.EnableDebugLogging = false;
                options.RetentionDays = 365; // 1 year retention
            });
        }
    }
}