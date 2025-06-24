using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Testing;

namespace TradingPlatform.Tests.Canonical
{
    /// <summary>
    /// Comprehensive unit tests for CanonicalProvider following canonical test patterns
    /// </summary>
    public class CanonicalProviderTests : CanonicalTestBase
    {
        private readonly Mock<ITradingLogger> _mockLogger;
        private readonly IMemoryCache _memoryCache;
        private readonly TestProvider _provider;

        public CanonicalProviderTests(ITestOutputHelper output) : base(output)
        {
            _mockLogger = new Mock<ITradingLogger>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _provider = new TestProvider(_mockLogger.Object, _memoryCache);
        }

        #region FetchDataAsync Tests

        [Fact]
        public async Task FetchDataAsync_Should_ReturnDataSuccessfully()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing successful data fetch");

                // Arrange
                var dataKey = "test-key";
                var expectedData = new TestData { Id = 1, Value = "Test Value" };
                _provider.SetupDataFetcher(() => Task.FromResult(expectedData));

                // Act
                var result = await _provider.TestFetchDataAsync(dataKey);

                // Assert
                AssertWithLogging(result.IsSuccess, true, "Fetch should succeed");
                AssertNotNull(result.Value, "Result should have value");
                AssertWithLogging(result.Value.Id, expectedData.Id, "Data should match");
            });
        }

        [Fact]
        public async Task FetchDataAsync_Should_UseCacheWhenEnabled()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing cache usage");

                // Arrange
                var dataKey = "cache-test-key";
                var testData = new TestData { Id = 1, Value = "Cached Value" };
                var fetchCount = 0;
                
                _provider.SetupDataFetcher(() =>
                {
                    fetchCount++;
                    return Task.FromResult(testData);
                });

                // Act - First fetch should hit the data source
                var result1 = await _provider.TestFetchDataAsync(dataKey);
                
                // Act - Second fetch should hit the cache
                var result2 = await _provider.TestFetchDataAsync(dataKey);

                // Assert
                AssertWithLogging(fetchCount, 1, "Data fetcher should only be called once");
                AssertWithLogging(result1.IsSuccess, true, "First fetch should succeed");
                AssertWithLogging(result2.IsSuccess, true, "Second fetch should succeed");
                AssertWithLogging(result2.Value?.Id, testData.Id, "Cached data should match");
            });
        }

        [Fact]
        public async Task FetchDataAsync_Should_ApplyRateLimiting()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing rate limiting");

                // Arrange
                _provider.SetRateLimitPerMinute(2); // Very low limit for testing
                _provider.SetupDataFetcher(() => Task.FromResult(new TestData { Id = 1 }));

                // Act - Fire multiple requests rapidly
                var tasks = new List<Task<TradingResult<TestData>>>();
                for (int i = 0; i < 5; i++)
                {
                    tasks.Add(_provider.TestFetchDataAsync($"key-{i}"));
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                await Task.WhenAll(tasks);
                stopwatch.Stop();

                // Assert - Should take some time due to rate limiting
                AssertConditionWithLogging(
                    stopwatch.ElapsedMilliseconds > 100,
                    "Rate limiting should introduce delays");
                
                AssertConditionWithLogging(
                    tasks.All(t => t.Result.IsSuccess),
                    "All requests should eventually succeed");
            });
        }

        [Fact]
        public async Task FetchDataAsync_Should_RetryOnFailure()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing retry logic");

                // Arrange
                var attempts = 0;
                var dataKey = "retry-test";
                var expectedData = new TestData { Id = 1, Value = "Success after retry" };
                
                _provider.SetupDataFetcher(() =>
                {
                    attempts++;
                    if (attempts < 3)
                    {
                        throw new InvalidOperationException($"Simulated failure {attempts}");
                    }
                    return Task.FromResult(expectedData);
                });

                // Act
                var result = await _provider.TestFetchDataAsync(dataKey);

                // Assert
                AssertWithLogging(result.IsSuccess, true, "Should succeed after retries");
                AssertWithLogging(attempts, 3, "Should have made 3 attempts");
                AssertWithLogging(result.Value?.Id, expectedData.Id, "Should return correct data");
            });
        }

        [Fact]
        public async Task FetchDataAsync_Should_FailAfterMaxRetries()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing retry exhaustion");

                // Arrange
                _provider.SetupDataFetcher(() =>
                {
                    throw new InvalidOperationException("Persistent failure");
                });

                // Act
                var result = await _provider.TestFetchDataAsync("fail-key");

                // Assert
                AssertWithLogging(result.IsSuccess, false, "Should fail after max retries");
                AssertNotNull(result.Error, "Should have error details");
                AssertConditionWithLogging(
                    result.Error.ErrorCode == TradingError.ErrorCodes.ExternalServiceError,
                    "Should have correct error code");
            });
        }

        #endregion

        #region FetchBatchDataAsync Tests

        [Fact]
        public async Task FetchBatchDataAsync_Should_ProcessBatchesSuccessfully()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing batch processing");

                // Arrange
                var keys = Enumerable.Range(1, 250).Select(i => $"key-{i}").ToList();
                var batchesReceived = new List<int>();
                
                _provider.SetupBatchFetcher((batchKeys) =>
                {
                    var keyList = batchKeys.ToList();
                    batchesReceived.Add(keyList.Count);
                    
                    var results = keyList.Select(k => new TestData 
                    { 
                        Id = int.Parse(k.Split('-')[1]), 
                        Value = k 
                    });
                    
                    return Task.FromResult(results);
                });

                // Act
                var result = await _provider.TestFetchBatchDataAsync(keys, maxBatchSize: 100);

                // Assert
                AssertWithLogging(result.IsSuccess, true, "Batch fetch should succeed");
                AssertWithLogging(result.Value?.Count(), 250, "Should return all items");
                AssertWithLogging(batchesReceived.Count, 3, "Should process in 3 batches (250/100)");
                AssertWithLogging(batchesReceived[0], 100, "First batch should be 100");
                AssertWithLogging(batchesReceived[1], 100, "Second batch should be 100");
                AssertWithLogging(batchesReceived[2], 50, "Third batch should be 50");
            });
        }

        [Fact]
        public async Task FetchBatchDataAsync_Should_HandlePartialFailures()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing partial batch failures");

                // Arrange
                var keys = Enumerable.Range(1, 30).Select(i => $"key-{i}").ToList();
                var batchCount = 0;
                
                _provider.SetupBatchFetcher((batchKeys) =>
                {
                    batchCount++;
                    if (batchCount == 2)
                    {
                        throw new InvalidOperationException("Batch 2 failed");
                    }
                    
                    var results = batchKeys.Select(k => new TestData 
                    { 
                        Id = batchCount * 100 + int.Parse(k.Split('-')[1]), 
                        Value = k 
                    });
                    
                    return Task.FromResult(results);
                });

                // Act
                var result = await _provider.TestFetchBatchDataAsync(keys, maxBatchSize: 10);

                // Assert
                AssertWithLogging(result.IsSuccess, true, "Should return partial success");
                var resultCount = result.Value?.Count() ?? 0;
                AssertConditionWithLogging(
                    resultCount == 20,
                    $"Should return 20 items from successful batches, got {resultCount}");
            });
        }

        #endregion

        #region ValidateProviderAsync Tests

        [Fact]
        public async Task ValidateProviderAsync_Should_PassAllValidations()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing provider validation - all pass");

                // Arrange
                _provider.SetupValidation(
                    configValid: true,
                    connectivityValid: true,
                    authValid: true);

                // Act
                var result = await _provider.ValidateProviderAsync();

                // Assert
                AssertWithLogging(result.IsSuccess, true, "Validation should pass");
            });
        }

        [Fact]
        public async Task ValidateProviderAsync_Should_FailOnConfigError()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing provider validation - config failure");

                // Arrange
                _provider.SetupValidation(
                    configValid: false,
                    connectivityValid: true,
                    authValid: true);

                // Act
                var result = await _provider.ValidateProviderAsync();

                // Assert
                AssertWithLogging(result.IsSuccess, false, "Validation should fail");
                AssertConditionWithLogging(
                    result.Error?.Message.Contains("Configuration") ?? false,
                    "Error should mention configuration");
            });
        }

        [Fact]
        public async Task ValidateProviderAsync_Should_FailOnConnectivityError()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing provider validation - connectivity failure");

                // Arrange
                _provider.SetupValidation(
                    configValid: true,
                    connectivityValid: false,
                    authValid: true);

                // Act
                var result = await _provider.ValidateProviderAsync();

                // Assert
                AssertWithLogging(result.IsSuccess, false, "Validation should fail");
                AssertConditionWithLogging(
                    result.Error?.Message.Contains("Connectivity") ?? false,
                    "Error should mention connectivity");
            });
        }

        #endregion

        #region Metrics Tests

        [Fact]
        public async Task Provider_Should_TrackMetrics()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing metrics tracking");

                // Arrange
                _provider.SetupDataFetcher(() => Task.FromResult(new TestData { Id = 1 }));
                
                // Act - Perform various operations
                await _provider.TestFetchDataAsync("metrics-key-1");
                await _provider.TestFetchDataAsync("metrics-key-1"); // Cache hit
                
                _provider.SetupDataFetcher(() => throw new Exception("Failure"));
                await _provider.TestFetchDataAsync("fail-key");

                // Get metrics
                var metrics = _provider.GetMetrics();

                // Assert
                AssertConditionWithLogging(
                    metrics.ContainsKey("Provider.CacheEnabled"),
                    "Should have cache enabled metric");
                AssertConditionWithLogging(
                    metrics.ContainsKey("CacheHitRate"),
                    "Should have cache hit rate metric");
                AssertConditionWithLogging(
                    metrics.ContainsKey("SuccessRate"),
                    "Should have success rate metric");
            });
        }

        #endregion

        #region Test Helper Classes

        private class TestData
        {
            public int Id { get; set; }
            public string Value { get; set; } = string.Empty;
        }

        private class TestProvider : CanonicalProvider<TestData>
        {
            private Func<Task<TestData>>? _dataFetcher;
            private Func<IEnumerable<string>, Task<IEnumerable<TestData>>>? _batchFetcher;
            private bool _configValid = true;
            private bool _connectivityValid = true;
            private bool _authValid = true;

            protected override int RateLimitRequestsPerMinute { get; set; } = 60;

            public TestProvider(ITradingLogger logger, IMemoryCache cache)
                : base(logger, "TestProvider", cache)
            {
            }

            public void SetupDataFetcher(Func<Task<TestData>> fetcher)
            {
                _dataFetcher = fetcher;
            }

            public void SetupBatchFetcher(Func<IEnumerable<string>, Task<IEnumerable<TestData>>> fetcher)
            {
                _batchFetcher = fetcher;
            }

            public void SetupValidation(bool configValid, bool connectivityValid, bool authValid)
            {
                _configValid = configValid;
                _connectivityValid = connectivityValid;
                _authValid = authValid;
            }

            public void SetRateLimitPerMinute(int limit)
            {
                RateLimitRequestsPerMinute = limit;
            }

            public Task<TradingResult<TestData>> TestFetchDataAsync(string key)
            {
                return FetchDataAsync(key, _dataFetcher ?? (() => Task.FromResult(new TestData())));
            }

            public Task<TradingResult<IEnumerable<TestData>>> TestFetchBatchDataAsync(
                IEnumerable<string> keys, 
                int maxBatchSize = 100)
            {
                return FetchBatchDataAsync(
                    keys, 
                    _batchFetcher ?? (k => Task.FromResult(Enumerable.Empty<TestData>())),
                    maxBatchSize);
            }

            protected override Task<TradingResult> ValidateConfigurationAsync()
            {
                return Task.FromResult(
                    _configValid 
                        ? TradingResult.Success() 
                        : TradingResult.Failure("CONFIG_ERROR", "Configuration validation failed"));
            }

            protected override Task<TradingResult> TestConnectivityAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    _connectivityValid 
                        ? TradingResult.Success() 
                        : TradingResult.Failure("CONNECTIVITY_ERROR", "Connectivity test failed"));
            }

            protected override Task<TradingResult> ValidateAuthenticationAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    _authValid 
                        ? TradingResult.Success() 
                        : TradingResult.Failure("AUTH_ERROR", "Authentication validation failed"));
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _memoryCache?.Dispose();
                _provider?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}