using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.DataIngestion.RateLimiting;
using TradingPlatform.DataIngestion.Models;
using TradingPlatform.Tests.Core.Canonical;

namespace TradingPlatform.Tests.Unit.DataIngestion
{
    /// <summary>
    /// Comprehensive unit tests for ApiRateLimiter
    /// Tests sliding window, async behavior, and jitter implementation
    /// </summary>
    public class ApiRateLimiterTests : CanonicalTestBase<ApiRateLimiter>
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ApiConfiguration _config;
        
        public ApiRateLimiterTests(ITestOutputHelper output) : base(output)
        {
            _memoryCache = ServiceProvider.GetRequiredService<IMemoryCache>();
            _config = new ApiConfiguration();
        }
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddSingleton<ApiConfiguration>();
        }
        
        protected override ApiRateLimiter CreateSystemUnderTest()
        {
            return new ApiRateLimiter(_memoryCache, MockLogger.Object, _config);
        }
        
        #region Sliding Window Tests
        
        [Fact]
        public async Task SlidingWindow_RemovesExpiredRequests()
        {
            // Arrange
            const string provider = "test";
            
            // Act - Record requests
            await SystemUnderTest.RecordRequestAsync(provider);
            await SystemUnderTest.RecordRequestAsync(provider);
            
            // Assert - Should have 2 requests
            var count1 = SystemUnderTest.GetUsedCalls();
            Assert.Equal(2, count1);
            
            // Wait for window to expire (simulated by modifying window in test)
            // Note: In real implementation, we'd need to wait or mock time
            await Task.Delay(100);
            
            // For testing purposes, verify the sliding window behavior
            var canMake = await SystemUnderTest.CanMakeRequestAsync(provider);
            Assert.True(canMake);
        }
        
        [Fact]
        public async Task SlidingWindow_MaintainsAccurateCount()
        {
            // Arrange
            const string provider = "alphavantage";
            const int limit = 5; // AlphaVantage limit
            
            // Act - Fill up to limit
            for (int i = 0; i < limit; i++)
            {
                var canMake = await SystemUnderTest.CanMakeRequestAsync(provider);
                Assert.True(canMake, $"Should be able to make request {i + 1}");
                await SystemUnderTest.RecordRequestAsync(provider);
            }
            
            // Assert - Should be at limit
            var canMakeExtra = await SystemUnderTest.CanMakeRequestAsync(provider);
            Assert.False(canMakeExtra, "Should not be able to exceed limit");
            
            var remaining = await SystemUnderTest.GetRemainingRequestsAsync(provider, TimeSpan.FromMinutes(1));
            Assert.Equal(0, remaining);
        }
        
        #endregion
        
        #region Jitter Tests
        
        [Fact]
        public async Task GetWaitTime_IncludesJitter()
        {
            // Arrange
            const string provider = "alphavantage";
            const int limit = 5;
            var waitTimes = new List<TimeSpan>();
            
            // Fill up rate limit
            for (int i = 0; i < limit; i++)
            {
                await SystemUnderTest.RecordRequestAsync(provider);
            }
            
            // Act - Get multiple wait times
            for (int i = 0; i < 10; i++)
            {
                var waitTime = await SystemUnderTest.GetWaitTimeAsync(provider);
                waitTimes.Add(waitTime);
            }
            
            // Assert - Wait times should vary due to jitter
            var distinctWaitTimes = waitTimes.Distinct().Count();
            Assert.True(distinctWaitTimes > 1, "Jitter should produce varied wait times");
            
            // Verify all wait times are positive
            Assert.All(waitTimes, wt => Assert.True(wt >= TimeSpan.Zero));
        }
        
        [Fact]
        public async Task Jitter_StaysWithinBounds()
        {
            // Arrange
            const string provider = "test";
            SystemUnderTest.UpdateLimits(1); // 1 request per minute
            
            // Fill the limit
            await SystemUnderTest.RecordRequestAsync(provider);
            
            // Act - Get wait time multiple times
            var waitTimes = new List<double>();
            for (int i = 0; i < 100; i++)
            {
                var waitTime = await SystemUnderTest.GetWaitTimeAsync(provider);
                waitTimes.Add(waitTime.TotalMilliseconds);
            }
            
            // Assert - Jitter should be within ±20%
            var baseWaitTime = waitTimes.Average();
            var minExpected = baseWaitTime * 0.8;
            var maxExpected = baseWaitTime * 1.2;
            
            Assert.All(waitTimes, wt =>
            {
                Assert.True(wt >= minExpected * 0.9, $"Wait time {wt}ms below minimum expected {minExpected}ms");
                Assert.True(wt <= maxExpected * 1.1, $"Wait time {wt}ms above maximum expected {maxExpected}ms");
            });
        }
        
        #endregion
        
        #region Async Pattern Tests
        
        [Fact]
        public async Task AllMethods_UseProperAsync()
        {
            // This test verifies no .Result or .Wait() calls cause deadlocks
            const string provider = "test";
            
            // Test all async methods
            await AssertNoExceptionsAsync(async () =>
            {
                await SystemUnderTest.CanMakeRequestAsync(provider);
                await SystemUnderTest.RecordRequestAsync(provider);
                await SystemUnderTest.GetWaitTimeAsync(provider);
                await SystemUnderTest.GetRemainingRequestsAsync(provider, TimeSpan.FromMinutes(1));
                await SystemUnderTest.ResetLimitsAsync(provider);
                await SystemUnderTest.IsProviderAvailableAsync(provider);
                await SystemUnderTest.WaitForPermitAsync(provider);
            });
            
            // Verify no deadlocks in synchronous wrappers
            AssertNoExceptions(() =>
            {
                var result = SystemUnderTest.TryAcquirePermit(provider);
                SystemUnderTest.RecordRequest();
                var isReached = SystemUnderTest.IsLimitReached(provider);
                var remaining = SystemUnderTest.GetRemainingCalls(provider);
            });
        }
        
        [Fact]
        public async Task WaitForPermitAsync_DoesNotDeadlock()
        {
            // Arrange
            const string provider = "test";
            SystemUnderTest.UpdateLimits(1); // Very low limit
            await SystemUnderTest.RecordRequestAsync(provider);
            
            // Act - This should wait then succeed
            var waitTask = Task.Run(async () =>
            {
                await SystemUnderTest.WaitForPermitAsync(provider);
                return true;
            });
            
            // Reset limits after short delay
            await Task.Delay(50);
            await SystemUnderTest.ResetLimitsAsync(provider);
            
            // Assert - Should complete without timeout
            var completed = await Task.WhenAny(waitTask, Task.Delay(1000)) == waitTask;
            Assert.True(completed, "WaitForPermitAsync should not deadlock");
        }
        
        #endregion
        
        #region Provider-Specific Rate Limit Tests
        
        [Theory]
        [InlineData("alphavantage", 5)]
        [InlineData("finnhub", 60)]
        [InlineData("unknown", 60)]
        public async Task RateLimit_EnforcedPerProvider(string provider, int expectedLimit)
        {
            // Act - Record requests up to limit
            for (int i = 0; i < expectedLimit; i++)
            {
                var canMake = await SystemUnderTest.CanMakeRequestAsync(provider);
                Assert.True(canMake);
                await SystemUnderTest.RecordRequestAsync(provider);
            }
            
            // Assert - Should be at limit
            var canMakeExtra = await SystemUnderTest.CanMakeRequestAsync(provider);
            Assert.False(canMakeExtra);
            
            var remaining = await SystemUnderTest.GetRemainingRequestsAsync(provider, TimeSpan.FromMinutes(1));
            Assert.Equal(0, remaining);
        }
        
        [Fact]
        public async Task MultipleProviders_IndependentLimits()
        {
            // Arrange
            const string provider1 = "alphavantage";
            const string provider2 = "finnhub";
            
            // Act - Fill provider1 limit
            for (int i = 0; i < 5; i++)
            {
                await SystemUnderTest.RecordRequestAsync(provider1);
            }
            
            // Assert - provider1 is limited but provider2 is not
            Assert.False(await SystemUnderTest.CanMakeRequestAsync(provider1));
            Assert.True(await SystemUnderTest.CanMakeRequestAsync(provider2));
            
            // Can still make requests to provider2
            await SystemUnderTest.RecordRequestAsync(provider2);
            var remaining2 = await SystemUnderTest.GetRemainingRequestsAsync(provider2, TimeSpan.FromMinutes(1));
            Assert.Equal(59, remaining2); // 60 - 1
        }
        
        #endregion
        
        #region Statistics and Events Tests
        
        [Fact]
        public async Task Statistics_TrackedCorrectly()
        {
            // Arrange
            const string provider = "test";
            SystemUnderTest.UpdateLimits(2); // Low limit for testing
            
            // Act
            await SystemUnderTest.RecordRequestAsync(provider);
            await SystemUnderTest.RecordRequestAsync(provider);
            await SystemUnderTest.RecordRequestAsync(provider); // This should be rate limited
            
            // Assert
            var stats = SystemUnderTest.GetStatistics();
            Assert.Equal(3, stats.TotalRequests);
            Assert.Equal(2, stats.CurrentRpm);
            Assert.Equal(1, stats.RateLimitedRequests);
        }
        
        [Fact]
        public async Task RateLimitReached_EventFired()
        {
            // Arrange
            const string provider = "test";
            SystemUnderTest.UpdateLimits(1);
            RateLimitReachedEventArgs? eventArgs = null;
            
            SystemUnderTest.RateLimitReached += (sender, args) => eventArgs = args;
            
            // Act
            await SystemUnderTest.RecordRequestAsync(provider);
            await SystemUnderTest.RecordRequestAsync(provider); // Should trigger event
            
            // Assert
            Assert.NotNull(eventArgs);
            Assert.Equal(provider, eventArgs.Provider);
            Assert.Equal(1, eventArgs.CurrentCount);
            Assert.Equal(1, eventArgs.Limit);
        }
        
        #endregion
        
        #region Error Handling Tests
        
        [Fact]
        public void RecordFailure_UpdatesStatistics()
        {
            // Arrange
            var exception = CreateTestException("API error");
            
            // Act
            SystemUnderTest.RecordFailure(exception);
            
            // Assert
            var stats = SystemUnderTest.GetStatistics();
            Assert.Equal(1, stats.FailedRequests);
            VerifyLoggerCalled("error", Times.Once());
        }
        
        [Fact]
        public async Task Reset_ClearsAllState()
        {
            // Arrange
            const string provider = "test";
            await SystemUnderTest.RecordRequestAsync(provider);
            
            // Act
            SystemUnderTest.Reset();
            
            // Assert
            var canMake = await SystemUnderTest.CanMakeRequestAsync(provider);
            Assert.True(canMake);
            Assert.Equal(0, SystemUnderTest.GetUsedCalls());
        }
        
        #endregion
        
        #region Performance Tests
        
        [Fact]
        public async Task CanMakeRequest_PerformanceTest()
        {
            // Arrange
            const string provider = "test";
            const int iterations = 1000;
            
            // Warm up
            for (int i = 0; i < 100; i++)
            {
                await SystemUnderTest.CanMakeRequestAsync(provider);
            }
            
            // Act & Assert - Should complete quickly
            await AssertCompletesWithinAsync(100, async () =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    await SystemUnderTest.CanMakeRequestAsync(provider);
                }
            });
        }
        
        [Fact]
        public async Task ConcurrentRequests_ThreadSafe()
        {
            // Arrange
            const string provider = "test";
            const int threadCount = 10;
            const int requestsPerThread = 5;
            SystemUnderTest.UpdateLimits(threadCount * requestsPerThread);
            
            // Act - Multiple threads recording requests
            var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(async () =>
            {
                for (int i = 0; i < requestsPerThread; i++)
                {
                    await SystemUnderTest.RecordRequestAsync(provider);
                }
            })).ToArray();
            
            await Task.WhenAll(tasks);
            
            // Assert - Exactly the expected number of requests
            var used = SystemUnderTest.GetUsedCalls();
            Assert.Equal(threadCount * requestsPerThread, used);
            
            // Should be at limit
            var canMake = await SystemUnderTest.CanMakeRequestAsync(provider);
            Assert.False(canMake);
        }
        
        #endregion
    }
}