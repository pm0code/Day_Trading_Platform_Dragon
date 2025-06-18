// File: DayTradinPlatform\Program.cs

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Interfaces; // Example: Add necessary using statements
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.DataIngestion.Providers;
using TradingPlatform.DataIngestion.RateLimiting;
using TradingPlatform.DataIngestion.Services;
using TradingPlatform.Screening.Alerts;
using TradingPlatform.Screening.Criteria;
using TradingPlatform.Screening.Engines;
using TradingPlatform.Screening.Interfaces;
using TradingPlatform.Screening.Services;

namespace DayTradinPlatform
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // Register your services here

                    // Example: Logging
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole(); // Add other logging providers as needed
                    });

                    // Example: Core Services
                    services.AddSingleton<IMarketDataProvider, MarketDataProvider>(); // Replace with your actual implementation
                    services.AddSingleton<IMarketService, MarketService>(); // Example: Replace with actual implementation
                    services.AddSingleton<TradingCriteria>(); // Example

                    // Example: Data Ingestion Services
                    services.AddSingleton<IAlphaVantageProvider, AlphaVantageProvider>();
                    services.AddSingleton<IFinnhubProvider, FinnhubProvider>();
                    services.AddSingleton<IRateLimiter, ApiRateLimiter>();
                    services.AddSingleton<ICacheService, CacheService>(); // If you have a cache service
                    services.AddSingleton<IMarketDataAggregator, MarketDataAggregator>(); // Ensure this interface is defined
                    services.AddSingleton<IDataIngestionService, DataIngestionService>();
                    services.AddSingleton<ApiConfiguration>(); // Your API configuration

                    // Example: Screening Services
                    services.AddSingleton<IScreeningEngine, RealTimeScreeningEngine>();
                    services.AddSingleton<VolumeCriteria>();
                    services.AddSingleton<PriceCriteria>();
                    services.AddSingleton<VolatilityCriteria>();
                    services.AddSingleton<GapCriteria>();
                    services.AddSingleton<NewsCriteria>();
                    services.AddSingleton<ScreeningOrchestrator>();
                    services.AddSingleton<ICriteriaEvaluator, CriteriaEvaluator>(); // Example: Replace with actual implementation
                    services.AddSingleton<IAlertService, AlertService>();
                    services.AddSingleton<NotificationService>();
                    services.AddSingleton<CriteriaConfigurationService>();
                    services.AddSingleton<ScreeningHistoryService>();

                    // ... Add other services ...

                    // Example: Background service (if needed)
                    // services.AddHostedService<MarketDataWorker>(); // Example
                });
    }
}

// Total Lines: 79
