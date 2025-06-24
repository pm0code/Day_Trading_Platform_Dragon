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
    /// Canonical test harness for validating Finnhub and AlphaVantage data providers
    /// with comprehensive logging, error handling, and progress reporting
    /// </summary>
    public class DataProviderTestHarness_Canonical : CanonicalBase, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly CanonicalProgressReporter _progressReporter;

        public DataProviderTestHarness_Canonical() : base(new TradingLogger())
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
                        .AddUserSecrets<DataProviderTestHarness_Canonical>(optional: true)
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
                            
                            var quote = await TrackPerformanceAsync(
                                async () => await finnhub.GetQuoteAsync("AAPL"),
                                "Finnhub GetQuoteAsync",
                                TimeSpan.FromSeconds(2)
                            );
                            
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
                            
                            var profile = await TrackPerformanceAsync(
                                async () => await finnhub.GetCompanyProfileAsync("MSFT"),
                                "Finnhub GetCompanyProfileAsync",
                                TimeSpan.FromSeconds(2)
                            );
                            
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
                            
                            var news = await TrackPerformanceAsync(
                                async () => await finnhub.GetMarketNewsAsync(),
                                "Finnhub GetMarketNewsAsync",
                                TimeSpan.FromSeconds(3)
                            );
                            
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
                            
                            var isOpen = await TrackPerformanceAsync(
                                async () => await finnhub.IsMarketOpenAsync(),
                                "Finnhub IsMarketOpenAsync",
                                TimeSpan.FromSeconds(1)
                            );
                            
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
                            
                            var batchQuotes = await TrackPerformanceAsync(
                                async () => await finnhub.GetBatchQuotesAsync(symbols),
                                "Finnhub GetBatchQuotesAsync",
                                TimeSpan.FromSeconds(3)
                            );
                            
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
                    
                    _progressReporter.ReportProgress(
                        (successCount * 100.0 / totalCount),
                        $"Finnhub: {successCount}/{totalCount} tests passed"
                    );
                    
                    return Task.CompletedTask;
                },
                "Testing Finnhub provider",
                "Finnhub provider tests failed",
                "Check Finnhub API key and service availability"
            );
        }

        private async Task TestAlphaVantageProvider()
        {
            await ExecuteWithLoggingAsync(
                async () =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var alphaVantage = scope.ServiceProvider.GetRequiredService<IAlphaVantageProvider>();
                    
                    var testResults = new List<(string TestName, bool Success, string Details)>();
                    
                    // Test 1: Get Real-Time Data
                    await ExecuteTestOperationAsync(
                        async () =>
                        {
                            Console.WriteLine("  - Testing GetRealTimeDataAsync for AAPL...");
                            LogDebug("Testing AlphaVantage GetRealTimeDataAsync", new { Symbol = "AAPL" });
                            
                            var realtimeData = await TrackPerformanceAsync(
                                async () => await alphaVantage.GetRealTimeDataAsync("AAPL"),
                                "AlphaVantage GetRealTimeDataAsync",
                                TimeSpan.FromSeconds(3)
                            );
                            
                            if (realtimeData != null)
                            {
                                var details = $"AAPL Price=${realtimeData.Price:F2}, Change={realtimeData.ChangePercent:F2}%";
                                Console.WriteLine($"    ✓ Success: {details}");
                                LogInfo("AlphaVantage real-time data test passed", realtimeData);
                                testResults.Add(("GetRealTimeDataAsync", true, details));
                            }
                            else
                            {
                                Console.WriteLine("    ✗ Failed: No real-time data returned");
                                LogWarning("AlphaVantage real-time data test failed - no data returned");
                                testResults.Add(("GetRealTimeDataAsync", false, "No data returned"));
                            }
                        },
                        "GetRealTimeDataAsync test"
                    );

                    // Test 2: Get Historical Data
                    await ExecuteTestOperationAsync(
                        async () =>
                        {
                            Console.WriteLine("  - Testing GetHistoricalDataAsync for MSFT (last 5 days)...");
                            var endDate = DateTime.Today;
                            var startDate = endDate.AddDays(-5);
                            LogDebug("Testing AlphaVantage GetHistoricalDataAsync", new { Symbol = "MSFT", StartDate = startDate, EndDate = endDate });
                            
                            var historicalData = await TrackPerformanceAsync(
                                async () => await alphaVantage.GetHistoricalDataAsync("MSFT", startDate, endDate),
                                "AlphaVantage GetHistoricalDataAsync",
                                TimeSpan.FromSeconds(5)
                            );
                            
                            if (historicalData != null && historicalData.Any())
                            {
                                var details = $"Retrieved {historicalData.Count()} days of data";
                                Console.WriteLine($"    ✓ Success: {details}");
                                var latestDay = historicalData.OrderByDescending(h => h.Date).First();
                                Console.WriteLine($"      Latest: {latestDay.Date:yyyy-MM-dd} Close=${latestDay.Close:F2}");
                                LogInfo("AlphaVantage historical data test passed", new { DayCount = historicalData.Count(), LatestDay = latestDay });
                                testResults.Add(("GetHistoricalDataAsync", true, details));
                            }
                            else
                            {
                                Console.WriteLine("    ✗ Failed: No historical data returned");
                                LogWarning("AlphaVantage historical data test failed - no data returned");
                                testResults.Add(("GetHistoricalDataAsync", false, "No data returned"));
                            }
                        },
                        "GetHistoricalDataAsync test"
                    );

                    // Test 3: Get Company Overview
                    await ExecuteTestOperationAsync(
                        async () =>
                        {
                            Console.WriteLine("  - Testing GetCompanyOverviewAsync for GOOGL...");
                            LogDebug("Testing AlphaVantage GetCompanyOverviewAsync", new { Symbol = "GOOGL" });
                            
                            var overview = await TrackPerformanceAsync(
                                async () => await alphaVantage.GetCompanyOverviewAsync("GOOGL"),
                                "AlphaVantage GetCompanyOverviewAsync",
                                TimeSpan.FromSeconds(3)
                            );
                            
                            if (overview != null)
                            {
                                var details = $"{overview.Name} - Market Cap: ${overview.MarketCapitalization:N0}";
                                Console.WriteLine($"    ✓ Success: {details}");
                                LogInfo("AlphaVantage company overview test passed", overview);
                                testResults.Add(("GetCompanyOverviewAsync", true, details));
                            }
                            else
                            {
                                Console.WriteLine("    ✗ Failed: No company overview returned");
                                LogWarning("AlphaVantage company overview test failed - no data returned");
                                testResults.Add(("GetCompanyOverviewAsync", false, "No data returned"));
                            }
                        },
                        "GetCompanyOverviewAsync test"
                    );

                    // Test 4: Subscribe to Updates (test observable pattern)
                    await ExecuteTestOperationAsync(
                        async () =>
                        {
                            Console.WriteLine("  - Testing SubscribeToUpdates for TSLA (5-second test)...");
                            LogDebug("Testing AlphaVantage SubscribeToUpdates", new { Symbol = "TSLA", Duration = "5 seconds" });
                            
                            var updateCount = 0;
                            var subscription = alphaVantage.SubscribeToUpdates("TSLA", TimeSpan.FromSeconds(5))
                                .Subscribe(
                                    data =>
                                    {
                                        updateCount++;
                                        Console.WriteLine($"      Update {updateCount}: ${data.Price:F2} at {data.Timestamp:HH:mm:ss}");
                                        LogDebug($"AlphaVantage update received", new { UpdateNumber = updateCount, Data = data });
                                    },
                                    error =>
                                    {
                                        Console.WriteLine($"    ✗ Subscription error: {error.Message}");
                                        LogError("AlphaVantage subscription error", error, "SubscribeToUpdates", "Subscription failed", "Check API limits");
                                    },
                                    () =>
                                    {
                                        Console.WriteLine($"    ✓ Subscription completed with {updateCount} updates");
                                        LogInfo("AlphaVantage subscription completed", new { TotalUpdates = updateCount });
                                    }
                                );

                            // Wait for a few updates
                            await Task.Delay(TimeSpan.FromSeconds(12));
                            subscription.Dispose();
                            
                            var details = $"Received {updateCount} updates";
                            testResults.Add(("SubscribeToUpdates", updateCount > 0, details));
                        },
                        "SubscribeToUpdates test"
                    );

                    // Log summary
                    var successCount = testResults.Count(r => r.Success);
                    var totalCount = testResults.Count;
                    LogInfo($"AlphaVantage provider tests completed: {successCount}/{totalCount} passed", testResults);
                    
                    _progressReporter.ReportProgress(
                        (successCount * 100.0 / totalCount),
                        $"AlphaVantage: {successCount}/{totalCount} tests passed"
                    );
                    
                    return Task.CompletedTask;
                },
                "Testing AlphaVantage provider",
                "AlphaVantage provider tests failed",
                "Check AlphaVantage API key and rate limits (5 calls/minute for free tier)"
            );
        }

        private async Task TestMarketDataAggregator()
        {
            await ExecuteWithLoggingAsync(
                async () =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var aggregator = scope.ServiceProvider.GetRequiredService<IMarketDataAggregator>();
                    
                    var testResults = new List<(string TestName, bool Success, string Details)>();
                    
                    // Test 1: Get Aggregated Market Data
                    await ExecuteTestOperationAsync(
                        async () =>
                        {
                            Console.WriteLine("  - Testing GetAggregatedMarketDataAsync for AAPL...");
                            LogDebug("Testing Market Data Aggregator GetAggregatedMarketDataAsync", new { Symbol = "AAPL" });
                            
                            var marketData = await TrackPerformanceAsync(
                                async () => await aggregator.GetAggregatedMarketDataAsync("AAPL"),
                                "Aggregator GetAggregatedMarketDataAsync",
                                TimeSpan.FromSeconds(5)
                            );
                            
                            if (marketData != null)
                            {
                                var details = $"AAPL aggregated data - Price: ${marketData.Price:F2}, Volume: {marketData.Volume:N0}";
                                Console.WriteLine($"    ✓ Success: {details}");
                                Console.WriteLine($"      Day Range: ${marketData.DayLow:F2} - ${marketData.DayHigh:F2}");
                                Console.WriteLine($"      Data Source: {marketData.Source}");
                                LogInfo("Market data aggregator test passed", marketData);
                                testResults.Add(("GetAggregatedMarketDataAsync", true, details));
                            }
                            else
                            {
                                Console.WriteLine("    ✗ Failed: No aggregated data returned");
                                LogWarning("Market data aggregator test failed - no data returned");
                                testResults.Add(("GetAggregatedMarketDataAsync", false, "No data returned"));
                            }
                        },
                        "GetAggregatedMarketDataAsync test"
                    );

                    // Test 2: Test Fallback Mechanism
                    await ExecuteTestOperationAsync(
                        async () =>
                        {
                            Console.WriteLine("  - Testing fallback mechanism with invalid symbol...");
                            LogDebug("Testing aggregator fallback mechanism", new { Symbol = "INVALID_SYMBOL_XYZ" });
                            
                            var fallbackData = await TrackPerformanceAsync(
                                async () => await aggregator.GetAggregatedMarketDataAsync("INVALID_SYMBOL_XYZ"),
                                "Aggregator fallback test",
                                TimeSpan.FromSeconds(5)
                            );
                            
                            if (fallbackData == null)
                            {
                                Console.WriteLine("    ✓ Success: Properly handled invalid symbol");
                                LogInfo("Aggregator fallback test passed - invalid symbol handled correctly");
                                testResults.Add(("Fallback mechanism", true, "Invalid symbol handled correctly"));
                            }
                            else
                            {
                                Console.WriteLine("    ✗ Warning: Returned data for invalid symbol");
                                LogWarning("Aggregator fallback test warning - data returned for invalid symbol", fallbackData);
                                testResults.Add(("Fallback mechanism", false, "Data returned for invalid symbol"));
                            }
                        },
                        "Fallback mechanism test"
                    );

                    // Log summary
                    var successCount = testResults.Count(r => r.Success);
                    var totalCount = testResults.Count;
                    LogInfo($"Market data aggregator tests completed: {successCount}/{totalCount} passed", testResults);
                    
                    _progressReporter.ReportProgress(
                        (successCount * 100.0 / totalCount),
                        $"Aggregator: {successCount}/{totalCount} tests passed"
                    );
                    
                    return Task.CompletedTask;
                },
                "Testing market data aggregator",
                "Market data aggregator tests failed",
                "Check data provider configurations"
            );
        }
        
        private async Task ExecuteTestOperationAsync(Func<Task> operation, string operationName)
        {
            try
            {
                await ExecuteWithRetryAsync(
                    async () =>
                    {
                        await operation();
                        return Task.CompletedTask;
                    },
                    operationName,
                    maxRetries: 2,
                    retryDelay: TimeSpan.FromSeconds(2)
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ✗ Error in {operationName}: {ex.Message}");
                LogError($"{operationName} failed", ex, operationName, "Test operation failed", "Check API connectivity and credentials");
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

            DataProviderTestHarness_Canonical? harness = null;
            
            try
            {
                harness = new DataProviderTestHarness_Canonical();
                await harness.RunAllTests();
            }
            catch (Exception ex)
            {
                CanonicalErrorHandler.HandleError(
                    ex,
                    "DataProviderTestHarness",
                    "Test harness execution",
                    ErrorSeverity.Critical,
                    "Test harness failed to execute",
                    "Check configuration and error details"
                );
                
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                harness?.Dispose();
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        public void Dispose()
        {
            LogMethodEntry();
            
            try
            {
                _progressReporter?.Dispose();
                
                if (_serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                
                LogInfo("DataProviderTestHarness disposed successfully");
            }
            catch (Exception ex)
            {
                LogError("Error during disposal", ex, "Dispose", "Resources may not be fully released", "Check for resource leaks");
            }
            finally
            {
                LogMethodExit();
            }
        }
    }
}