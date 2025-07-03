using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Extensions;
using TradingPlatform.Core.Configuration;

namespace TradingPlatform.Console
{
    /// <summary>
    /// Console application entry point with first-run configuration setup
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            
            // Initialize configuration (will run first-time setup if needed)
            var configService = host.Services.GetRequiredService<IConfigurationService>();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            
            try
            {
                logger.LogInformation("Starting Trading Platform...");
                
                // Initialize configuration - this will trigger first-run setup if needed
                var result = await ((ConfigurationService)configService).InitializeAsync();
                
                if (!result.IsSuccess)
                {
                    logger.LogError("Failed to initialize configuration: {Error}", result.ErrorMessage);
                    Environment.Exit(1);
                }
                
                logger.LogInformation("Configuration initialized successfully");
                
                // Start the application
                await host.RunAsync();
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Application failed to start");
                Environment.Exit(1);
            }
        }
        
        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    // Add core services with encrypted configuration
                    services.AddTradingPlatformCore(context.Configuration);
                    
                    // Add other services as needed
                    // services.AddDataIngestion();
                    // services.AddScreening();
                    // services.AddRiskManagement();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Information);
                });
    }
}