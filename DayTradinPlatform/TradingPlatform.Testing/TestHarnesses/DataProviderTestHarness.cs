using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Canonical;
using TradingPlatform.DataIngestion.Providers;
using TradingPlatform.DataIngestion.Services;
using TradingPlatform.DataIngestion.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace TradingPlatform.Testing.TestHarnesses
{
    /// <summary>
    /// Test harness for validating Finnhub and AlphaVantage data providers
    /// with real API calls (requires valid API keys in configuration)
    /// </summary>
    public class DataProviderTestHarness : CanonicalBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly CanonicalProgressReporter _progressReporter;

        public DataProviderTestHarness() : base(new TradingLogger())
        {
            LogMethodEntry();
            
            try
            {
                // Build configuration
                _configuration = ExecuteWithLogging(
                    () => new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .AddUserSecrets<DataProviderTestHarness>(optional: true)
                        .Build(),
                    "Building configuration",
                    "Test harness cannot be configured",
                    "Ensure configuration files exist and are valid"
                );

                // Setup DI container
                var services = new ServiceCollection();
                ConfigureServices(services);
                _serviceProvider = ExecuteWithLogging(
                    () => services.BuildServiceProvider(),
                    "Building service provider",
                    "Dependency injection setup failed",
                    "Check service registrations"
                );

                _progressReporter = new CanonicalProgressReporter("Data Provider Test Harness", true);
                
                LogInfo("DataProviderTestHarness initialized successfully");
            }
            catch (Exception ex)
            {
                LogError(
                    "Failed to initialize DataProviderTestHarness",
                    ex,
                    "Constructor",
                    "Test harness is not operational",
                    "Check configuration and dependencies"
                );
                throw;
            }
            finally
            {
                LogMethodExit();
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            LogMethodEntry();
            
            try
            {
                LogDebug("Configuring test harness services");
                
                // Configuration
                services.AddSingleton(_configuration);
                
                // Logging
                services.AddSingleton<ITradingLogger>(_logger);
                
                // Caching
                services.AddMemoryCache();
                LogDebug("Memory cache configured");
                
                // Rate Limiting
                services.AddSingleton<IRateLimiter, ApiRateLimiter>();
                LogDebug("Rate limiter configured");
                
                // API Configuration
                services.Configure<ApiConfiguration>(options =>
                {
                    _configuration.GetSection("ApiConfiguration").Bind(options);
                    
                    // Override with environment variables if present
                    var finnhubKey = Environment.GetEnvironmentVariable("FINNHUB_API_KEY") 
                        ?? _configuration["ApiConfiguration:Finnhub:ApiKey"];
                    var alphaVantageKey = Environment.GetEnvironmentVariable("ALPHAVANTAGE_API_KEY") 
                        ?? _configuration["ApiConfiguration:AlphaVantage:ApiKey"];
                    
                    if (!string.IsNullOrEmpty(finnhubKey))
                    {
                        options.Finnhub.ApiKey = finnhubKey;
                        LogDebug("Finnhub API key configured from environment/config");
                    }
                    else
                    {
                        LogWarning("No Finnhub API key found - using default");
                    }
                    
                    if (!string.IsNullOrEmpty(alphaVantageKey))
                    {
                        options.AlphaVantage.ApiKey = alphaVantageKey;
                        LogDebug("AlphaVantage API key configured from environment/config");
                    }
                    else
                    {
                        LogWarning("No AlphaVantage API key found - using default");
                    }
                });
                
                // Data Providers
                services.AddScoped<IFinnhubProvider, FinnhubProvider>();
                services.AddScoped<IAlphaVantageProvider, AlphaVantageProvider>();
                services.AddScoped<IMarketDataAggregator, MarketDataAggregator>();
                LogDebug("Data providers registered");
                
                LogInfo("Service configuration completed successfully");
            }
            catch (Exception ex)
            {
                LogError(
                    "Failed to configure services",
                    ex,
                    "Service configuration",
                    "Services may not be available",
                    "Check service registrations and dependencies"
                );
                throw;
            }
            finally
            {
                LogMethodExit();
            }
        }

        public async Task RunAllTests()
        {
            await ExecuteWithLoggingAsync(
                async () =>
                {
                    Console.WriteLine("=== Day Trading Platform - Data Provider Test Harness ===");
                    Console.WriteLine($"Starting tests at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"Correlation ID: {_correlationId}");
                    Console.WriteLine();

                    _progressReporter.ReportStageProgress("Initialization", 100, "Test harness initialized");

                    // Test Finnhub
                    _progressReporter.ReportStageProgress("Finnhub Tests", 0, "Starting Finnhub provider tests");
                    Console.WriteLine("1. Testing Finnhub Provider...");
                    await TestFinnhubProvider();
                    _progressReporter.CompleteStage("Finnhub Tests");
                    Console.WriteLine();

                    // Test AlphaVantage
                    _progressReporter.ReportStageProgress("AlphaVantage Tests", 0, "Starting AlphaVantage provider tests");
                    Console.WriteLine("2. Testing AlphaVantage Provider...");
                    await TestAlphaVantageProvider();
                    _progressReporter.CompleteStage("AlphaVantage Tests");
                    Console.WriteLine();

                    // Test Market Data Aggregator
                    _progressReporter.ReportStageProgress("Aggregator Tests", 0, "Starting market data aggregator tests");
                    Console.WriteLine("3. Testing Market Data Aggregator...");
                    await TestMarketDataAggregator();
                    _progressReporter.CompleteStage("Aggregator Tests");
                    Console.WriteLine();

                    _progressReporter.Complete("All tests completed successfully");
                    Console.WriteLine("=== Test Harness Complete ===");
                    
                    return Task.CompletedTask;
                },
                "Running all data provider tests",
                "Test harness execution failed",
                "Check API keys and network connectivity"
            );
        }

        private async Task TestFinnhubProvider()
        {
            await ExecuteWithLoggingAsync(
                async () =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var finnhub = scope.ServiceProvider.GetRequiredService<IFinnhubProvider>();
                    
                    var testResults = new List<(string TestName, bool Success, string Details)>();
                    
                    // Test 1: Get Quote
                    await ExecuteTestOperationAsync(
                        async () =>
                        {
                            Console.WriteLine("  - Testing GetQuoteAsync for AAPL...");
                            LogDebug("Testing Finnhub GetQuoteAsync", new { Symbol = "AAPL" });
                            
                            var quote = await finnhub.GetQuoteAsync("AAPL");
                            if (quote != null)
                            {
                                var details = $"AAPL Price=${quote.Price:F2}, Volume={quote.Volume:N0}";
                                Console.WriteLine($"    ✓ Success: {details}");
                                LogInfo("Finnhub quote test passed", quote);
                                testResults.Add(("GetQuoteAsync", true, details));
                            }
                            else
                            {
                                Console.WriteLine("    ✗ Failed: No quote data returned");
                                LogWarning("Finnhub quote test failed - no data returned");
                                testResults.Add(("GetQuoteAsync", false, "No data returned"));
                            }
                        },
                        "GetQuoteAsync test"
                    );

                    // Test 2: Get Company Profile
                    await ExecuteTestOperationAsync(
                        async () =>
                        {
                            Console.WriteLine("  - Testing GetCompanyProfileAsync for MSFT...");
                            LogDebug("Testing Finnhub GetCompanyProfileAsync", new { Symbol = "MSFT" });
                            
                            var profile = await finnhub.GetCompanyProfileAsync("MSFT");
                            if (profile != null)
                            {
                                var details = $"{profile.Name} ({profile.Ticker})";
                                Console.WriteLine($"    ✓ Success: {details}");
                                LogInfo("Finnhub company profile test passed", profile);
                                testResults.Add(("GetCompanyProfileAsync", true, details));
                            }
                            else
                            {
                                Console.WriteLine("    ✗ Failed: No company profile returned");
                                LogWarning("Finnhub company profile test failed - no data returned");
                                testResults.Add(("GetCompanyProfileAsync", false, "No data returned"));
                            }
                        },
                        "GetCompanyProfileAsync test"
                    );

                    // Test 3: Get Market News
                    await ExecuteTestOperationAsync(
                        async () =>
                        {
                            Console.WriteLine("  - Testing GetMarketNewsAsync...");
                            LogDebug("Testing Finnhub GetMarketNewsAsync");
                            
                            var news = await finnhub.GetMarketNewsAsync();
                            if (news != null && news.Any())
                            {
                                var details = $"Retrieved {news.Count()} news items";
                                Console.WriteLine($"    ✓ Success: {details}");
                                var latestNews = news.First();
                                Console.WriteLine($"      Latest: {latestNews.Headline?.Substring(0, Math.Min(60, latestNews.Headline?.Length ?? 0))}...");
                                LogInfo("Finnhub market news test passed", new { NewsCount = news.Count(), LatestHeadline = latestNews.Headline });
                                testResults.Add(("GetMarketNewsAsync", true, details));
                            }
                            else
                            {
                                Console.WriteLine("    ✗ Failed: No market news returned");
                                LogWarning("Finnhub market news test failed - no data returned");
                                testResults.Add(("GetMarketNewsAsync", false, "No data returned"));
                            }
                        },
                        "GetMarketNewsAsync test"
                    );

                    // Test 4: Check Market Status
                    await ExecuteTestOperationAsync(
                        async () =>
                        {
                            Console.WriteLine("  - Testing IsMarketOpenAsync...");
                            LogDebug("Testing Finnhub IsMarketOpenAsync");
                            
                            var isOpen = await finnhub.IsMarketOpenAsync();
                            var details = $"Market is {(isOpen ? "OPEN" : "CLOSED")}";
                            Console.WriteLine($"    ✓ {details}");
                            LogInfo("Finnhub market status test completed", new { IsOpen = isOpen });
                            testResults.Add(("IsMarketOpenAsync", true, details));
                        },
                        "IsMarketOpenAsync test"
                    );

                    // Test 5: Get Batch Quotes
                    await ExecuteTestOperationAsync(
                        async () =>
                        {
                            Console.WriteLine("  - Testing GetBatchQuotesAsync for multiple symbols...");
                            var symbols = new[] { "AAPL", "MSFT", "GOOGL" };
                            LogDebug("Testing Finnhub GetBatchQuotesAsync", new { Symbols = symbols });
                            
                            var batchQuotes = await finnhub.GetBatchQuotesAsync(symbols);
                            if (batchQuotes != null && batchQuotes.Any())
                            {
                                var details = $"Retrieved quotes for {batchQuotes.Count()} symbols";
                                Console.WriteLine($"    ✓ Success: {details}");
                                foreach (var bq in batchQuotes)
                                {
                                    Console.WriteLine($"      {bq.Symbol}: ${bq.Price:F2}");
                                }
                                LogInfo("Finnhub batch quotes test passed", new { QuoteCount = batchQuotes.Count(), Quotes = batchQuotes });
                                testResults.Add(("GetBatchQuotesAsync", true, details));
                            }
                            else
                            {
                                Console.WriteLine("    ✗ Failed: No batch quotes returned");
                                LogWarning("Finnhub batch quotes test failed - no data returned");
                                testResults.Add(("GetBatchQuotesAsync", false, "No data returned"));
                            }
                        },
                        "GetBatchQuotesAsync test"
                    );

                    // Log summary
                    var successCount = testResults.Count(r => r.Success);
                    var totalCount = testResults.Count;
                    LogInfo($"Finnhub provider tests completed: {successCount}/{totalCount} passed", testResults);
                    
                    return Task.CompletedTask;
                },
                "Testing Finnhub provider",
                "Finnhub provider tests failed",
                "Check Finnhub API key and service availability"
            );
        }
        
        private async Task ExecuteTestOperationAsync(Func<Task> operation, string operationName)
        {
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ✗ Error in {operationName}: {ex.Message}");
                LogError($"{operationName} failed", ex, operationName, "Test operation failed", "Check API connectivity and credentials");
            }
        }

        private async Task TestAlphaVantageProvider()
        {
            using var scope = _serviceProvider.CreateScope();
            var alphaVantage = scope.ServiceProvider.GetRequiredService<IAlphaVantageProvider>();
            
            try
            {
                // Test 1: Get Real-Time Data
                Console.WriteLine("  - Testing GetRealTimeDataAsync for AAPL...");
                var realtimeData = await alphaVantage.GetRealTimeDataAsync("AAPL");
                if (realtimeData != null)
                {
                    Console.WriteLine($"    ✓ Success: AAPL Price=${realtimeData.Price:F2}, Change={realtimeData.ChangePercent:F2}%");
                }
                else
                {
                    Console.WriteLine("    ✗ Failed: No real-time data returned");
                }

                // Test 2: Get Historical Data
                Console.WriteLine("  - Testing GetHistoricalDataAsync for MSFT (last 5 days)...");
                var endDate = DateTime.Today;
                var startDate = endDate.AddDays(-5);
                var historicalData = await alphaVantage.GetHistoricalDataAsync("MSFT", startDate, endDate);
                if (historicalData != null && historicalData.Any())
                {
                    Console.WriteLine($"    ✓ Success: Retrieved {historicalData.Count()} days of data");
                    var latestDay = historicalData.OrderByDescending(h => h.Date).First();
                    Console.WriteLine($"      Latest: {latestDay.Date:yyyy-MM-dd} Close=${latestDay.Close:F2}");
                }
                else
                {
                    Console.WriteLine("    ✗ Failed: No historical data returned");
                }

                // Test 3: Get Company Overview
                Console.WriteLine("  - Testing GetCompanyOverviewAsync for GOOGL...");
                var overview = await alphaVantage.GetCompanyOverviewAsync("GOOGL");
                if (overview != null)
                {
                    Console.WriteLine($"    ✓ Success: {overview.Name} - Market Cap: ${overview.MarketCapitalization:N0}");
                }
                else
                {
                    Console.WriteLine("    ✗ Failed: No company overview returned");
                }

                // Test 4: Subscribe to Updates (test observable pattern)
                Console.WriteLine("  - Testing SubscribeToUpdates for TSLA (5-second test)...");
                var updateCount = 0;
                var subscription = alphaVantage.SubscribeToUpdates("TSLA", TimeSpan.FromSeconds(5))
                    .Subscribe(
                        data =>
                        {
                            updateCount++;
                            Console.WriteLine($"      Update {updateCount}: ${data.Price:F2} at {data.Timestamp:HH:mm:ss}");
                        },
                        error => Console.WriteLine($"    ✗ Subscription error: {error.Message}"),
                        () => Console.WriteLine($"    ✓ Subscription completed with {updateCount} updates")
                    );

                // Wait for a few updates
                await Task.Delay(TimeSpan.FromSeconds(12));
                subscription.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ✗ Error: {ex.Message}");
                _logger.LogError($"AlphaVantage test failed: {ex.Message}", ex);
            }
        }

        private async Task TestMarketDataAggregator()
        {
            using var scope = _serviceProvider.CreateScope();
            var aggregator = scope.ServiceProvider.GetRequiredService<IMarketDataAggregator>();
            
            try
            {
                // Test 1: Get Aggregated Market Data
                Console.WriteLine("  - Testing GetAggregatedMarketDataAsync for AAPL...");
                var marketData = await aggregator.GetAggregatedMarketDataAsync("AAPL");
                if (marketData != null)
                {
                    Console.WriteLine($"    ✓ Success: AAPL aggregated data");
                    Console.WriteLine($"      Price: ${marketData.Price:F2}");
                    Console.WriteLine($"      Volume: {marketData.Volume:N0}");
                    Console.WriteLine($"      Day Range: ${marketData.DayLow:F2} - ${marketData.DayHigh:F2}");
                    Console.WriteLine($"      Data Source: {marketData.Source}");
                }
                else
                {
                    Console.WriteLine("    ✗ Failed: No aggregated data returned");
                }

                // Test 2: Test Fallback Mechanism
                Console.WriteLine("  - Testing fallback mechanism with invalid symbol...");
                var fallbackData = await aggregator.GetAggregatedMarketDataAsync("INVALID_SYMBOL_XYZ");
                if (fallbackData == null)
                {
                    Console.WriteLine("    ✓ Success: Properly handled invalid symbol");
                }
                else
                {
                    Console.WriteLine("    ✗ Warning: Returned data for invalid symbol");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ✗ Error: {ex.Message}");
                _logger.LogError($"Market Data Aggregator test failed: {ex.Message}", ex);
            }
        }

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Data Provider Test Harness...");
            Console.WriteLine();
            Console.WriteLine("NOTE: This test harness requires valid API keys.");
            Console.WriteLine("Please ensure you have set either:");
            Console.WriteLine("  1. Environment variables: FINNHUB_API_KEY and ALPHAVANTAGE_API_KEY");
            Console.WriteLine("  2. User secrets (dotnet user-secrets)");
            Console.WriteLine("  3. Updated appsettings.json with real API keys");
            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            Console.WriteLine();

            try
            {
                var harness = new DataProviderTestHarness();
                await harness.RunAllTests();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}