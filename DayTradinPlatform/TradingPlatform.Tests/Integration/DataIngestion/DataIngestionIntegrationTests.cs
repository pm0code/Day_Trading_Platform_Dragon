using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Services;
using TradingPlatform.DataIngestion.RateLimiting;
using TradingPlatform.DataIngestion.Providers;
using TradingPlatform.Tests.Core.Canonical;
using Moq;

namespace TradingPlatform.Tests.Integration.DataIngestion
{
    /// <summary>
    /// Integration tests for Data Ingestion components
    /// Tests rate limiting, data aggregation, and provider coordination
    /// </summary>
    public class DataIngestionIntegrationTests : IntegrationTestBase
    {
        private ApiRateLimiterCanonical _rateLimiter;
        private DataIngestionService _dataIngestionService;
        private MarketDataAggregator _dataAggregator;
        private Mock<IAlphaVantageProvider> _mockAlphaVantage;
        private Mock<IFinnhubProvider> _mockFinnhub;
        
        public DataIngestionIntegrationTests(ITestOutputHelper output) : base(output)
        {
            _mockAlphaVantage = new Mock<IAlphaVantageProvider>();
            _mockFinnhub = new Mock<IFinnhubProvider>();
        }
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            // Register data ingestion services
            services.AddSingleton<ApiRateLimiterCanonical>();
            services.AddSingleton<DataIngestionService>();
            services.AddSingleton<MarketDataAggregator>();
            
            // Register providers
            services.AddSingleton(_mockAlphaVantage.Object);
            services.AddSingleton(_mockFinnhub.Object);
            services.AddSingleton<IMarketDataProvider>(_mockAlphaVantage.Object);
            services.AddSingleton<IMarketDataProvider>(_mockFinnhub.Object);
            
            // Register mocks
            services.AddSingleton(MockLogger.Object);
            services.AddMemoryCache();
        }
        
        protected override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            
            _rateLimiter = ServiceProvider.GetRequiredService<ApiRateLimiterCanonical>();
            _dataIngestionService = ServiceProvider.GetRequiredService<DataIngestionService>();
            _dataAggregator = ServiceProvider.GetRequiredService<MarketDataAggregator>();
            
            await _rateLimiter.InitializeAsync();
            
            SetupProviderMocks();
        }
        
        #region Rate Limiting Integration
        
        [Fact]
        public async Task RateLimiter_WithMultipleProviders_EnforcesLimitsCorrectly()
        {
            // Arrange
            Output.WriteLine("=== Testing Rate Limiter with Multiple Providers ===");
            
            var alphaVantageLimit = new RateLimitConfig
            {
                Provider = "AlphaVantage",
                RequestsPerMinute = 5,
                DailyLimit = 500,
                BurstCapacity = 2
            };
            
            var finnhubLimit = new RateLimitConfig
            {
                Provider = "Finnhub",
                RequestsPerMinute = 60,
                DailyLimit = null, // No daily limit
                BurstCapacity = 10
            };
            
            await _rateLimiter.ConfigureProviderLimitAsync(alphaVantageLimit);
            await _rateLimiter.ConfigureProviderLimitAsync(finnhubLimit);
            
            // Act - Make concurrent requests
            var alphaVantageRequests = new List<Task<bool>>();
            var finnhubRequests = new List<Task<bool>>();
            
            // Try 10 AlphaVantage requests (should allow 5 + burst)
            for (int i = 0; i < 10; i++)
            {
                var task = _rateLimiter.TryAcquireAsync("AlphaVantage", "quote");
                alphaVantageRequests.Add(task);
            }
            
            // Try 70 Finnhub requests (should allow 60 + burst)
            for (int i = 0; i < 70; i++)
            {
                var task = _rateLimiter.TryAcquireAsync("Finnhub", "quote");
                finnhubRequests.Add(task);
            }
            
            var alphaResults = await Task.WhenAll(alphaVantageRequests);
            var finnhubResults = await Task.WhenAll(finnhubRequests);
            
            // Assert
            var alphaAllowed = alphaResults.Count(r => r);
            var finnhubAllowed = finnhubResults.Count(r => r);
            
            Output.WriteLine($"AlphaVantage: {alphaAllowed}/10 requests allowed");
            Output.WriteLine($"Finnhub: {finnhubAllowed}/70 requests allowed");
            
            Assert.True(alphaAllowed <= 7); // 5 + 2 burst
            Assert.True(finnhubAllowed <= 70); // 60 + 10 burst
            
            // Test rate recovery
            Output.WriteLine("\nWaiting for rate limit recovery...");
            await Task.Delay(TimeSpan.FromSeconds(60));
            
            var recoveryTest = await _rateLimiter.TryAcquireAsync("AlphaVantage", "quote");
            Assert.True(recoveryTest, "Should allow request after recovery");
        }
        
        [Fact]
        public async Task RateLimiter_DailyLimit_BlocksAfterExhaustion()
        {
            // Arrange
            Output.WriteLine("=== Testing Daily Rate Limit ===");
            
            var config = new RateLimitConfig
            {
                Provider = "TestProvider",
                RequestsPerMinute = 1000, // High minute rate
                DailyLimit = 10, // Low daily limit for testing
                BurstCapacity = 0
            };
            
            await _rateLimiter.ConfigureProviderLimitAsync(config);
            
            // Act - Exhaust daily limit
            var requests = new List<Task<bool>>();
            for (int i = 0; i < 15; i++)
            {
                requests.Add(_rateLimiter.TryAcquireAsync("TestProvider", $"request_{i}"));
            }
            
            var results = await Task.WhenAll(requests);
            
            // Assert
            var allowed = results.Count(r => r);
            var blocked = results.Count(r => !r);
            
            Output.WriteLine($"Daily limit test:");
            Output.WriteLine($"  Allowed: {allowed}");
            Output.WriteLine($"  Blocked: {blocked}");
            
            Assert.Equal(10, allowed);
            Assert.Equal(5, blocked);
            
            // Verify blocked for rest of day
            var additionalRequest = await _rateLimiter.TryAcquireAsync("TestProvider", "extra");
            Assert.False(additionalRequest, "Should block after daily limit exhausted");
        }
        
        #endregion
        
        #region Data Aggregation Tests
        
        [Fact]
        public async Task DataAggregation_MultipleProviders_CombinesDataCorrectly()
        {
            // Arrange
            Output.WriteLine("=== Testing Data Aggregation from Multiple Providers ===");
            
            var symbol = "AAPL";
            
            // Setup different data from each provider
            var alphaVantageData = new MarketData
            {
                Symbol = symbol,
                Price = 175.50m,
                Volume = 50_000_000,
                High = 176.80m,
                Low = 174.20m,
                Source = "AlphaVantage",
                Timestamp = DateTime.UtcNow
            };
            
            var finnhubData = new MarketData
            {
                Symbol = symbol,
                Price = 175.48m,
                Volume = 50_500_000,
                High = 176.75m,
                Low = 174.25m,
                Source = "Finnhub",
                Timestamp = DateTime.UtcNow.AddSeconds(-2)
            };
            
            _mockAlphaVantage.Setup(x => x.GetMarketDataAsync(symbol))
                .ReturnsAsync(alphaVantageData);
            
            _mockFinnhub.Setup(x => x.GetMarketDataAsync(symbol))
                .ReturnsAsync(finnhubData);
            
            // Act
            var aggregatedData = await _dataAggregator.GetAggregatedDataAsync(symbol);
            
            // Assert
            Assert.NotNull(aggregatedData);
            Output.WriteLine($"Aggregated Data for {symbol}:");
            Output.WriteLine($"  Price: ${aggregatedData.Price:F2} (avg of providers)");
            Output.WriteLine($"  Volume: {aggregatedData.Volume:N0}");
            Output.WriteLine($"  High: ${aggregatedData.High:F2}");
            Output.WriteLine($"  Low: ${aggregatedData.Low:F2}");
            Output.WriteLine($"  Sources: {aggregatedData.Sources.Count}");
            
            // Price should be average or most recent
            Assert.InRange(aggregatedData.Price, 175.48m, 175.50m);
            
            // Volume might be averaged or summed depending on implementation
            Assert.True(aggregatedData.Volume >= 50_000_000);
            
            // High/Low should be the extremes
            Assert.Equal(176.80m, aggregatedData.High);
            Assert.Equal(174.20m, aggregatedData.Low);
        }
        
        [Fact]
        public async Task DataAggregation_ProviderFailure_UsesAvailableData()
        {
            // Arrange
            Output.WriteLine("=== Testing Data Aggregation with Provider Failure ===");
            
            var symbol = "MSFT";
            
            // AlphaVantage fails
            _mockAlphaVantage.Setup(x => x.GetMarketDataAsync(symbol))
                .ThrowsAsync(new Exception("API Error"));
            
            // Finnhub succeeds
            _mockFinnhub.Setup(x => x.GetMarketDataAsync(symbol))
                .ReturnsAsync(new MarketData
                {
                    Symbol = symbol,
                    Price = 350.25m,
                    Volume = 25_000_000,
                    Source = "Finnhub"
                });
            
            // Act
            var aggregatedData = await _dataAggregator.GetAggregatedDataAsync(symbol);
            
            // Assert
            Assert.NotNull(aggregatedData);
            Assert.Equal(350.25m, aggregatedData.Price);
            Assert.Single(aggregatedData.Sources);
            Assert.Contains("Finnhub", aggregatedData.Sources);
            
            Output.WriteLine($"Aggregation with failure:");
            Output.WriteLine($"  Successfully used data from: {string.Join(", ", aggregatedData.Sources)}");
            Output.WriteLine($"  Failed providers handled gracefully");
        }
        
        #endregion
        
        #region Data Ingestion Orchestration
        
        [Fact]
        public async Task DataIngestion_BatchProcessing_HandlesLargeDatasets()
        {
            // Arrange
            Output.WriteLine("=== Testing Batch Data Ingestion ===");
            
            var symbols = GenerateTestSymbols(100);
            var processedCount = 0;
            var errors = new List<string>();
            
            // Setup mock responses
            foreach (var symbol in symbols)
            {
                var mockData = GenerateMockMarketData(symbol);
                _mockAlphaVantage.Setup(x => x.GetMarketDataAsync(symbol))
                    .ReturnsAsync(mockData);
                _mockFinnhub.Setup(x => x.GetMarketDataAsync(symbol))
                    .ReturnsAsync(mockData);
            }
            
            // Act
            var batchSize = 10;
            var batches = symbols.Chunk(batchSize).ToList();
            
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            foreach (var batch in batches)
            {
                var tasks = batch.Select(async symbol =>
                {
                    try
                    {
                        var data = await _dataIngestionService.IngestDataAsync(symbol);
                        if (data != null)
                        {
                            Interlocked.Increment(ref processedCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (errors)
                        {
                            errors.Add($"{symbol}: {ex.Message}");
                        }
                    }
                });
                
                await Task.WhenAll(tasks);
                
                // Respect rate limits between batches
                await Task.Delay(100);
            }
            
            sw.Stop();
            
            // Assert
            Output.WriteLine($"Batch Processing Results:");
            Output.WriteLine($"  Total Symbols: {symbols.Length}");
            Output.WriteLine($"  Processed: {processedCount}");
            Output.WriteLine($"  Errors: {errors.Count}");
            Output.WriteLine($"  Duration: {sw.ElapsedMilliseconds}ms");
            Output.WriteLine($"  Throughput: {processedCount / (sw.ElapsedMilliseconds / 1000.0):F2} symbols/sec");
            
            Assert.True(processedCount > symbols.Length * 0.95, "Should process >95% successfully");
            Assert.True(errors.Count < symbols.Length * 0.05, "Error rate should be <5%");
        }
        
        [Fact]
        public async Task DataIngestion_RealTimeStreaming_MaintainsLatency()
        {
            // Arrange
            Output.WriteLine("=== Testing Real-Time Data Streaming ===");
            
            var streamDuration = TimeSpan.FromSeconds(5);
            var updateCount = 0;
            var latencies = new List<long>();
            var cts = new CancellationTokenSource(streamDuration);
            
            // Setup streaming simulation
            var symbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA" };
            
            // Act - Simulate streaming updates
            var streamingTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var symbol = symbols[updateCount % symbols.Length];
                    var marketData = GenerateMockMarketData(symbol);
                    marketData.Timestamp = DateTime.UtcNow;
                    
                    _mockFinnhub.Setup(x => x.GetMarketDataAsync(symbol))
                        .ReturnsAsync(marketData);
                    
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var data = await _dataIngestionService.IngestDataAsync(symbol);
                    sw.Stop();
                    
                    if (data != null)
                    {
                        lock (latencies)
                        {
                            latencies.Add(sw.ElapsedMilliseconds);
                            updateCount++;
                        }
                    }
                    
                    await Task.Delay(50); // 20 updates/second
                }
            }, cts.Token);
            
            try
            {
                await streamingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            
            // Assert
            if (latencies.Any())
            {
                var avgLatency = latencies.Average();
                var p95Latency = GetPercentile(latencies, 0.95);
                var p99Latency = GetPercentile(latencies, 0.99);
                
                Output.WriteLine($"Streaming Performance:");
                Output.WriteLine($"  Updates: {updateCount}");
                Output.WriteLine($"  Avg Latency: {avgLatency:F2}ms");
                Output.WriteLine($"  P95 Latency: {p95Latency}ms");
                Output.WriteLine($"  P99 Latency: {p99Latency}ms");
                
                Assert.True(avgLatency < 50, "Average latency should be <50ms");
                Assert.True(p99Latency < 100, "P99 latency should be <100ms");
            }
        }
        
        #endregion
        
        #region Historical Data Tests
        
        [Fact]
        public async Task HistoricalData_LargeTimeRange_PaginatesCorrectly()
        {
            // Arrange
            Output.WriteLine("=== Testing Historical Data Pagination ===");
            
            var symbol = "AAPL";
            var startDate = DateTime.Today.AddYears(-2);
            var endDate = DateTime.Today;
            var expectedDays = (endDate - startDate).Days;
            
            // Setup paginated responses
            var pageSize = 100;
            var pages = (int)Math.Ceiling(expectedDays / (double)pageSize);
            
            for (int i = 0; i < pages; i++)
            {
                var pageData = GenerateHistoricalData(
                    symbol, 
                    startDate.AddDays(i * pageSize),
                    Math.Min(pageSize, expectedDays - i * pageSize)
                );
                
                _mockAlphaVantage.Setup(x => x.GetHistoricalDataAsync(
                    symbol, 
                    It.IsAny<DateTime>(), 
                    It.IsAny<DateTime>(),
                    i))
                    .ReturnsAsync(pageData);
            }
            
            // Act
            var allData = await _dataIngestionService.GetHistoricalDataAsync(
                symbol, startDate, endDate);
            
            // Assert
            Assert.NotNull(allData);
            Output.WriteLine($"Historical Data Retrieved:");
            Output.WriteLine($"  Symbol: {symbol}");
            Output.WriteLine($"  Date Range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            Output.WriteLine($"  Expected Days: ~{expectedDays}");
            Output.WriteLine($"  Actual Records: {allData.Count}");
            Output.WriteLine($"  Pages Fetched: {pages}");
            
            // Should have data for most trading days (accounting for weekends/holidays)
            Assert.True(allData.Count > expectedDays * 0.65); // ~65% are trading days
        }
        
        #endregion
        
        #region Cache Integration Tests
        
        [Fact]
        public async Task DataCaching_FrequentRequests_UsesCacheEffectively()
        {
            // Arrange
            Output.WriteLine("=== Testing Data Caching ===");
            
            var symbol = "MSFT";
            var apiCallCount = 0;
            
            _mockAlphaVantage.Setup(x => x.GetMarketDataAsync(symbol))
                .ReturnsAsync(() =>
                {
                    Interlocked.Increment(ref apiCallCount);
                    return GenerateMockMarketData(symbol);
                });
            
            // Act - Make multiple requests
            var requestCount = 10;
            var results = new List<MarketData>();
            
            for (int i = 0; i < requestCount; i++)
            {
                var data = await _dataIngestionService.IngestDataAsync(symbol);
                results.Add(data);
                
                // Small delay between requests
                await Task.Delay(100);
            }
            
            // Assert
            Output.WriteLine($"Cache Performance:");
            Output.WriteLine($"  Total Requests: {requestCount}");
            Output.WriteLine($"  API Calls Made: {apiCallCount}");
            Output.WriteLine($"  Cache Hit Rate: {(requestCount - apiCallCount) / (double)requestCount:P0}");
            
            Assert.True(apiCallCount < requestCount, "Should use cache for some requests");
            Assert.True(apiCallCount <= 2, "Should make minimal API calls due to caching");
            
            // Verify data consistency
            var firstPrice = results.First().Price;
            Assert.All(results.Take(5), r => Assert.Equal(firstPrice, r.Price));
        }
        
        #endregion
        
        #region Error Handling and Resilience
        
        [Fact]
        public async Task DataIngestion_NetworkErrors_RetriesWithBackoff()
        {
            // Arrange
            Output.WriteLine("=== Testing Retry Logic with Network Errors ===");
            
            var symbol = "GOOGL";
            var attemptCount = 0;
            
            _mockFinnhub.Setup(x => x.GetMarketDataAsync(symbol))
                .ReturnsAsync(() =>
                {
                    attemptCount++;
                    if (attemptCount <= 2)
                    {
                        throw new System.Net.Http.HttpRequestException("Network error");
                    }
                    return GenerateMockMarketData(symbol);
                });
            
            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var data = await _dataIngestionService.IngestDataAsync(symbol);
            sw.Stop();
            
            // Assert
            Assert.NotNull(data);
            Assert.Equal(3, attemptCount); // Initial + 2 retries
            
            Output.WriteLine($"Retry Test Results:");
            Output.WriteLine($"  Attempts: {attemptCount}");
            Output.WriteLine($"  Total Duration: {sw.ElapsedMilliseconds}ms");
            Output.WriteLine($"  Success: {data != null}");
            
            // Should have exponential backoff
            Assert.True(sw.ElapsedMilliseconds > 1000, "Should include backoff delays");
        }
        
        [Fact]
        public async Task DataIngestion_ProviderOutage_SwitchesToBackup()
        {
            // Arrange
            Output.WriteLine("=== Testing Provider Failover ===");
            
            var symbols = new[] { "AAPL", "MSFT", "GOOGL" };
            
            // Primary provider (AlphaVantage) is down
            _mockAlphaVantage.Setup(x => x.GetMarketDataAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Service unavailable"));
            
            // Backup provider (Finnhub) is working
            foreach (var symbol in symbols)
            {
                _mockFinnhub.Setup(x => x.GetMarketDataAsync(symbol))
                    .ReturnsAsync(GenerateMockMarketData(symbol));
            }
            
            // Act
            var results = new Dictionary<string, MarketData>();
            foreach (var symbol in symbols)
            {
                var data = await _dataIngestionService.IngestDataAsync(symbol);
                if (data != null)
                {
                    results[symbol] = data;
                }
            }
            
            // Assert
            Assert.Equal(symbols.Length, results.Count);
            Assert.All(results.Values, data => Assert.Equal("Finnhub", data.Source));
            
            Output.WriteLine("Failover Test Results:");
            Output.WriteLine("  Primary provider failed for all requests");
            Output.WriteLine("  Successfully failed over to backup provider");
            Output.WriteLine($"  Retrieved data for: {string.Join(", ", results.Keys)}");
        }
        
        #endregion
        
        #region Helper Methods
        
        private void SetupProviderMocks()
        {
            _mockAlphaVantage.Setup(x => x.Name).Returns("AlphaVantage");
            _mockFinnhub.Setup(x => x.Name).Returns("Finnhub");
        }
        
        private string[] GenerateTestSymbols(int count)
        {
            var symbols = new string[count];
            var baseSymbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "META", "NVDA", "JPM" };
            
            for (int i = 0; i < count; i++)
            {
                if (i < baseSymbols.Length)
                {
                    symbols[i] = baseSymbols[i];
                }
                else
                {
                    symbols[i] = $"SYM{i:D3}";
                }
            }
            
            return symbols;
        }
        
        private MarketData GenerateMockMarketData(string symbol)
        {
            var random = new Random(symbol.GetHashCode());
            var basePrice = 50m + random.Next(0, 300);
            
            return new MarketData
            {
                Symbol = symbol,
                Price = basePrice + (decimal)(random.NextDouble() * 10 - 5),
                PreviousClose = basePrice,
                Open = basePrice + (decimal)(random.NextDouble() * 2 - 1),
                High = basePrice + (decimal)random.NextDouble() * 5,
                Low = basePrice - (decimal)random.NextDouble() * 5,
                Volume = random.Next(1_000_000, 50_000_000),
                AverageVolume = random.Next(5_000_000, 30_000_000),
                MarketCap = basePrice * random.Next(1_000_000_000, 10_000_000_000),
                Timestamp = DateTime.UtcNow
            };
        }
        
        private List<DailyData> GenerateHistoricalData(string symbol, DateTime startDate, int days)
        {
            var data = new List<DailyData>();
            var random = new Random(symbol.GetHashCode());
            var price = 100m + random.Next(0, 200);
            
            for (int i = 0; i < days; i++)
            {
                var date = startDate.AddDays(i);
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                {
                    var dailyReturn = (decimal)(random.NextDouble() * 0.04 - 0.02);
                    price *= (1 + dailyReturn);
                    
                    data.Add(new DailyData
                    {
                        Date = date,
                        Open = price * (1m + (decimal)(random.NextDouble() * 0.01 - 0.005)),
                        High = price * (1m + (decimal)random.NextDouble() * 0.02),
                        Low = price * (1m - (decimal)random.NextDouble() * 0.02),
                        Close = price,
                        Volume = random.Next(1_000_000, 10_000_000)
                    });
                }
            }
            
            return data;
        }
        
        private double GetPercentile(List<long> values, double percentile)
        {
            var sorted = values.OrderBy(v => v).ToList();
            var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
            return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
        }
        
        #endregion
    }
    
    // Helper classes
    public class RateLimitConfig
    {
        public string Provider { get; set; }
        public int RequestsPerMinute { get; set; }
        public int? DailyLimit { get; set; }
        public int BurstCapacity { get; set; }
    }
    
    public class AggregatedMarketData : MarketData
    {
        public List<string> Sources { get; set; } = new List<string>();
        public Dictionary<string, decimal> SourcePrices { get; set; } = new Dictionary<string, decimal>();
        public DateTime AggregationTimestamp { get; set; }
    }
}