using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.DataIngestion.RateLimiting;
using TradingPlatform.DataIngestion.Models;
using TradingPlatform.Tests.Core.Canonical;
using TradingPlatform.DataIngestion.Interfaces;
using Moq;

namespace TradingPlatform.Tests.Integration.DataIngestion
{
    /// <summary>
    /// Integration tests for ApiRateLimiter with real dependencies
    /// Tests interaction with cache, configuration, and concurrent scenarios
    /// </summary>
    public class ApiRateLimiterIntegrationTests : CanonicalIntegrationTestBase<ApiRateLimiter>
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ApiConfiguration _config;
        private readonly Mock<IMarketDataProvider> _mockDataProvider;
        
        public ApiRateLimiterIntegrationTests(ITestOutputHelper output) : base(output)
        {
            _memoryCache = ServiceProvider.GetRequiredService<IMemoryCache>();
            _config = ServiceProvider.GetRequiredService<ApiConfiguration>();
            _mockDataProvider = new Mock<IMarketDataProvider>();
        }
        
        protected override void ConfigureIntegrationServices(IServiceCollection services)
        {
            // Configure real API configuration from config
            services.AddSingleton(provider =>
            {
                var config = new ApiConfiguration
                {
                    AlphaVantageApiKey = Configuration["ApiKeys:AlphaVantage"],
                    FinnhubApiKey = Configuration["ApiKeys:Finnhub"],
                    AlphaVantageBaseUrl = "https://www.alphavantage.co/query",
                    FinnhubBaseUrl = "https://finnhub.io/api/v1"
                };
                
                // Set rate limits from configuration
                config.UpdateRateLimits("alphavantage", 
                    int.Parse(Configuration["RateLimits:AlphaVantage:CallsPerMinute"]));
                config.UpdateRateLimits("finnhub", 
                    int.Parse(Configuration["RateLimits:Finnhub:CallsPerMinute"]));
                    
                return config;
            });
            
            services.AddSingleton<ApiConfiguration>();
        }
        
        protected override ApiRateLimiter CreateSystemUnderTest()
        {
            return new ApiRateLimiter(_memoryCache, MockLogger.Object, _config);
        }
        
        #region Multi-Provider Integration Tests
        
        [Fact]
        public async Task MultipleProviders_IndependentRateLimiting()
        {
            // Arrange
            var alphaVantageProvider = "alphavantage";
            var finnhubProvider = "finnhub";
            var alphaVantageLimit = 5;
            var finnhubLimit = 60;
            
            // Act - Make requests to both providers concurrently
            var alphaTasks = Enumerable.Range(0, alphaVantageLimit + 2)
                .Select(i => Task.Run(async () =>
                {
                    var canMake = await SystemUnderTest.CanMakeRequestAsync(alphaVantageProvider);
                    if (canMake)
                    {
                        await SystemUnderTest.RecordRequestAsync(alphaVantageProvider);
                        return true;
                    }
                    return false;
                }));
                
            var finnhubTasks = Enumerable.Range(0, 10)
                .Select(i => Task.Run(async () =>
                {
                    var canMake = await SystemUnderTest.CanMakeRequestAsync(finnhubProvider);
                    if (canMake)
                    {
                        await SystemUnderTest.RecordRequestAsync(finnhubProvider);
                        return true;
                    }
                    return false;
                }));
                
            var alphaResults = await Task.WhenAll(alphaTasks);
            var finnhubResults = await Task.WhenAll(finnhubTasks);
            
            // Assert
            var alphaSuccesses = alphaResults.Count(r => r);
            var finnhubSuccesses = finnhubResults.Count(r => r);
            
            Assert.Equal(alphaVantageLimit, alphaSuccesses);
            Assert.Equal(10, finnhubSuccesses); // All Finnhub requests should succeed
            
            // Verify statistics
            var stats = SystemUnderTest.GetStatistics();
            Assert.Equal(alphaVantageLimit + 10, stats.TotalRequests);
            Assert.Equal(2, stats.RateLimitedRequests); // 2 AlphaVantage requests were limited
        }
        
        #endregion
        
        #region Cache Integration Tests
        
        [Fact]
        public async Task CacheIntegration_PersistsAcrossInstances()
        {
            // Arrange
            const string provider = "alphavantage";
            
            // Act - Use first instance
            for (int i = 0; i < 3; i++)
            {
                await SystemUnderTest.RecordRequestAsync(provider);
            }
            
            // Create new instance with same cache
            var newRateLimiter = new ApiRateLimiter(_memoryCache, MockLogger.Object, _config);
            
            // Assert - New instance should see existing requests
            var remaining = await newRateLimiter.GetRemainingRequestsAsync(provider, TimeSpan.FromMinutes(1));
            Assert.Equal(2, remaining); // 5 - 3
            
            // Can still make 2 more requests
            var canMake = await newRateLimiter.CanMakeRequestAsync(provider);
            Assert.True(canMake);
        }
        
        [Fact]
        public async Task CacheIntegration_SlidingWindowCleanup()
        {
            // Arrange
            const string provider = "test";
            SystemUnderTest.UpdateLimits(10);
            var cacheKey = $"rate_limit_{provider}_sliding_window";
            
            // Act - Fill rate limit
            for (int i = 0; i < 10; i++)
            {
                await SystemUnderTest.RecordRequestAsync(provider);
            }
            
            // Verify cache has entries
            Assert.True(_memoryCache.TryGetValue(cacheKey, out _));
            
            // Simulate time passing (would need time abstraction in real code)
            // For now, just verify the sliding window exists
            var canMake = await SystemUnderTest.CanMakeRequestAsync(provider);
            Assert.False(canMake);
            
            // Reset should clear cache
            await SystemUnderTest.ResetLimitsAsync(provider);
            
            // Should be able to make requests again
            canMake = await SystemUnderTest.CanMakeRequestAsync(provider);
            Assert.True(canMake);
        }
        
        #endregion
        
        #region Real-World Scenario Tests
        
        [Fact]
        public async Task RealWorldScenario_BurstTrafficWithBackoff()
        {
            // Arrange
            const string provider = "alphavantage";
            var requestResults = new List<(bool success, TimeSpan waitTime)>();
            
            // Act - Simulate burst traffic
            for (int i = 0; i < 10; i++)
            {
                var canMake = await SystemUnderTest.CanMakeRequestAsync(provider);
                if (canMake)
                {
                    await SystemUnderTest.RecordRequestAsync(provider);
                    requestResults.Add((true, TimeSpan.Zero));
                }
                else
                {
                    var waitTime = await SystemUnderTest.GetWaitTimeAsync(provider);
                    requestResults.Add((false, waitTime));
                    
                    // Log for debugging
                    Output.WriteLine($"Request {i + 1}: Rate limited, wait time: {waitTime.TotalMilliseconds}ms");
                }
            }
            
            // Assert
            var successCount = requestResults.Count(r => r.success);
            var limitedCount = requestResults.Count(r => !r.success);
            
            Assert.Equal(5, successCount); // AlphaVantage limit
            Assert.Equal(5, limitedCount);
            
            // Verify wait times have jitter
            var waitTimes = requestResults.Where(r => !r.success).Select(r => r.waitTime).ToList();
            var distinctWaitTimes = waitTimes.Distinct().Count();
            Assert.True(distinctWaitTimes > 1, "Wait times should vary due to jitter");
        }
        
        [Fact]
        public async Task RealWorldScenario_AdaptiveRateLimiting()
        {
            // Arrange
            const string provider = "adaptive_test";
            SystemUnderTest.UpdateLimits(20); // Start with 20 rpm
            
            var successRates = new List<double>();
            
            // Act - Simulate adaptive rate limiting over multiple windows
            for (int window = 0; window < 3; window++)
            {
                var successes = 0;
                var attempts = 25;
                
                for (int i = 0; i < attempts; i++)
                {
                    if (await SystemUnderTest.CanMakeRequestAsync(provider))
                    {
                        await SystemUnderTest.RecordRequestAsync(provider);
                        successes++;
                    }
                }
                
                var successRate = (double)successes / attempts;
                successRates.Add(successRate);
                
                Output.WriteLine($"Window {window + 1}: Success rate = {successRate:P}");
                
                // Reset for next window
                await SystemUnderTest.ResetLimitsAsync(provider);
                
                // Simulate adjusting limits based on success rate
                if (successRate < 0.9)
                {
                    SystemUnderTest.UpdateLimits((int)(20 * 0.8)); // Reduce by 20%
                }
            }
            
            // Assert - Success rate should stabilize
            Assert.All(successRates, rate => Assert.True(rate > 0.5));
        }
        
        #endregion
        
        #region Performance Integration Tests
        
        [Fact]
        public async Task Performance_HighConcurrency()
        {
            // Arrange
            const string provider = "perf_test";
            SystemUnderTest.UpdateLimits(1000); // High limit for perf testing
            const int concurrentClients = 50;
            const int requestsPerClient = 20;
            
            // Act - Simulate high concurrency
            var elapsed = await MeasureExecutionTimeAsync(async () =>
            {
                var tasks = Enumerable.Range(0, concurrentClients)
                    .Select(clientId => Task.Run(async () =>
                    {
                        for (int i = 0; i < requestsPerClient; i++)
                        {
                            if (await SystemUnderTest.CanMakeRequestAsync(provider))
                            {
                                await SystemUnderTest.RecordRequestAsync(provider);
                            }
                        }
                    }));
                    
                await Task.WhenAll(tasks);
            });
            
            // Assert
            Output.WriteLine($"High concurrency test completed in {elapsed}ms");
            Output.WriteLine($"Throughput: {(concurrentClients * requestsPerClient * 1000.0 / elapsed):F2} requests/second");
            
            // Should handle 1000 requests efficiently
            Assert.True(elapsed < 1000, $"High concurrency test took too long: {elapsed}ms");
            
            // Verify correct count
            var usedCalls = SystemUnderTest.GetUsedCalls();
            Assert.Equal(concurrentClients * requestsPerClient, usedCalls);
        }
        
        [Fact]
        public async Task Performance_LatencyUnderLoad()
        {
            // Arrange
            const string provider = "latency_test";
            SystemUnderTest.UpdateLimits(100);
            var latencies = new List<long>();
            
            // Warm up
            for (int i = 0; i < 10; i++)
            {
                await SystemUnderTest.CanMakeRequestAsync(provider);
            }
            
            // Act - Measure individual operation latency
            for (int i = 0; i < 100; i++)
            {
                var latency = await MeasureExecutionTimeAsync(async () =>
                {
                    await SystemUnderTest.CanMakeRequestAsync(provider);
                });
                latencies.Add(latency);
            }
            
            // Assert
            var avgLatency = latencies.Average();
            var p99Latency = latencies.OrderBy(l => l).Skip(98).First();
            
            Output.WriteLine($"CanMakeRequest latency - Avg: {avgLatency}ms, P99: {p99Latency}ms");
            
            // Rate limiter operations should be very fast
            Assert.True(avgLatency < 1, $"Average latency {avgLatency}ms too high");
            Assert.True(p99Latency < 5, $"P99 latency {p99Latency}ms too high");
        }
        
        #endregion
        
        #region Error Recovery Tests
        
        [Fact]
        public async Task ErrorRecovery_HandlesProviderFailures()
        {
            // Arrange
            const string provider = "error_test";
            var failureException = new HttpRequestException("Provider unavailable");
            
            // Act - Simulate provider failures
            for (int i = 0; i < 5; i++)
            {
                SystemUnderTest.RecordFailure(failureException);
            }
            
            // Provider should still be available for rate limiting
            var isAvailable = await SystemUnderTest.IsProviderAvailableAsync(provider);
            Assert.True(isAvailable);
            
            // Can still make requests (rate limiter doesn't block on failures)
            var canMake = await SystemUnderTest.CanMakeRequestAsync(provider);
            Assert.True(canMake);
            
            // Verify failure statistics
            var stats = SystemUnderTest.GetStatistics();
            Assert.Equal(5, stats.FailedRequests);
        }
        
        [Fact]
        public async Task ErrorRecovery_CacheCorruption()
        {
            // Arrange
            const string provider = "cache_test";
            
            // Corrupt cache entry
            var cacheKey = $"rate_limit_{provider}_sliding_window";
            _memoryCache.Set(cacheKey, "invalid_data");
            
            // Act - Should handle gracefully
            var canMake = await SystemUnderTest.CanMakeRequestAsync(provider);
            
            // Assert - Should recover and allow requests
            Assert.True(canMake);
            
            // Should be able to record request
            await AssertNoExceptionsAsync(async () =>
            {
                await SystemUnderTest.RecordRequestAsync(provider);
            });
        }
        
        #endregion
        
        #region Configuration Integration Tests
        
        [Fact]
        public async Task Configuration_DynamicLimitUpdates()
        {
            // Arrange
            const string provider = "dynamic_test";
            var initialLimit = 10;
            var newLimit = 20;
            
            SystemUnderTest.UpdateLimits(initialLimit);
            
            // Fill initial limit
            for (int i = 0; i < initialLimit; i++)
            {
                await SystemUnderTest.RecordRequestAsync(provider);
            }
            
            // Should be at limit
            Assert.False(await SystemUnderTest.CanMakeRequestAsync(provider));
            
            // Act - Update limits dynamically
            SystemUnderTest.UpdateLimits(newLimit);
            
            // Assert - Should be able to make more requests
            var additionalRequests = 0;
            for (int i = 0; i < newLimit; i++)
            {
                if (await SystemUnderTest.CanMakeRequestAsync(provider))
                {
                    await SystemUnderTest.RecordRequestAsync(provider);
                    additionalRequests++;
                }
            }
            
            Assert.Equal(newLimit - initialLimit, additionalRequests);
        }
        
        [Fact]
        public async Task Configuration_ProviderSpecificSettings()
        {
            // Arrange & Act - Test each provider's specific configuration
            var providers = new[]
            {
                ("alphavantage", 5),
                ("finnhub", 60),
                ("custom", 60) // Default
            };
            
            foreach (var (provider, expectedLimit) in providers)
            {
                // Reset to ensure clean state
                await SystemUnderTest.ResetLimitsAsync(provider);
                
                var remaining = await SystemUnderTest.GetRemainingRequestsAsync(provider, TimeSpan.FromMinutes(1));
                Assert.Equal(expectedLimit, remaining);
                
                Output.WriteLine($"Provider {provider}: Limit = {expectedLimit}");
            }
        }
        
        #endregion
    }
}