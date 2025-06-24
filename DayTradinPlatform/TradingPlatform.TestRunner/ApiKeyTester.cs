// File: TradingPlatform.TestRunner\ApiKeyTester.cs

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Configuration;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Utilities;

namespace TradingPlatform.TestRunner
{
    /// <summary>
    /// Simple API key tester to validate which AlphaVantage key works
    /// </summary>
    public class ApiKeyTester
    {
        public static async Task TestApiKeys()
        {
            Console.WriteLine("=== API Key Validation Test ===\n");

            // Setup
            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            var logger = serviceProvider.GetRequiredService<ILogger<ApiKeyTester>>();
            var validator = serviceProvider.GetRequiredService<ApiKeyValidatorCanonical>();
            var settings = serviceProvider.GetRequiredService<ISettingsService>();

            // Initialize
            await validator.InitializeAsync();

            // Test Finnhub
            Console.WriteLine("Testing Finnhub API key...");
            var finnhubKey = "d10vmq9r01qse6lecfrgd10vmq9r01qse6lecfs0";
            var finnhubResult = await validator.ValidateFinnhubKeyAsync(finnhubKey);
            PrintResult("Finnhub", finnhubResult);

            // Test AlphaVantage - First key
            Console.WriteLine("\nTesting AlphaVantage API key #1...");
            var alphaKey1 = "MNB5O9E1S3G9WFDL";
            var alphaResult1 = await validator.ValidateAlphaVantageKeyAsync(alphaKey1);
            PrintResult("AlphaVantage Key #1", alphaResult1);

            // Wait a bit to avoid rate limiting
            await Task.Delay(2000);

            // Test AlphaVantage - Second key (same as first in this case)
            Console.WriteLine("\nTesting AlphaVantage API key #2...");
            var alphaKey2 = "MNB5O9E1S3G9WFDL"; // Note: Both keys provided were identical
            var alphaResult2 = await validator.ValidateAlphaVantageKeyAsync(alphaKey2);
            PrintResult("AlphaVantage Key #2", alphaResult2);

            // Summary
            Console.WriteLine("\n=== SUMMARY ===");
            Console.WriteLine($"Finnhub API Key: {(finnhubResult.IsValid ? "VALID ✓" : "INVALID ✗")}");
            Console.WriteLine($"AlphaVantage API Key: {(alphaResult1.IsValid ? "VALID ✓" : "INVALID ✗")}");
            
            if (alphaResult1.IsRateLimited)
            {
                Console.WriteLine("Note: AlphaVantage key is valid but currently rate limited");
            }

            // Update settings with working keys
            if (finnhubResult.IsValid || alphaResult1.IsValid)
            {
                Console.WriteLine("\nUpdating settings with validated API keys...");
                var currentSettings = await settings.GetSettingsAsync();
                
                if (finnhubResult.IsValid)
                {
                    currentSettings.Api.Finnhub.ApiKey = finnhubKey;
                }
                
                if (alphaResult1.IsValid)
                {
                    currentSettings.Api.AlphaVantage.ApiKey = alphaKey1;
                }

                var updateResult = await settings.UpdateSettingsAsync(currentSettings);
                Console.WriteLine($"Settings update: {(updateResult.IsSuccess ? "SUCCESS" : "FAILED")}");
            }

            // Get metrics
            var metrics = validator.GetMetrics();
            Console.WriteLine($"\nValidation Metrics:");
            Console.WriteLine($"- Total Validations: {metrics["TotalValidations"]}");
            Console.WriteLine($"- Successful: {metrics["SuccessfulValidations"]}");
            Console.WriteLine($"- Failed: {metrics["FailedValidations"]}");
        }

        private static void PrintResult(string apiName, ApiValidationDetail result)
        {
            Console.WriteLine($"\n{apiName} Results:");
            Console.WriteLine($"- Valid: {(result.IsValid ? "YES" : "NO")}");
            Console.WriteLine($"- Message: {result.Message}");
            
            if (result.IsRateLimited)
                Console.WriteLine($"- Rate Limited: YES");
            
            if (!string.IsNullOrEmpty(result.Error))
                Console.WriteLine($"- Error: {result.Error}");
            
            if (!string.IsNullOrEmpty(result.ResponseData))
                Console.WriteLine($"- Response: {result.ResponseData}");
            
            if (result.StatusCode.HasValue)
                Console.WriteLine($"- HTTP Status: {result.StatusCode}");
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.Configure<TradingPlatformSettings>(configuration.GetSection("TradingPlatformSettings"));

            // Logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Core services
            services.AddSingleton<ITradingLogger, TradingLogger>();
            services.AddSingleton<ISettingsService, CanonicalSettingsService>();
            
            // HTTP client
            services.AddHttpClient<ApiKeyValidatorCanonical>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "TradingPlatform/1.0");
            });

            // API key validator
            services.AddSingleton<ApiKeyValidatorCanonical>();
        }
    }
}