using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using TradingPlatform.ChaosTests.Framework;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.DataIngestion.Services;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.ChaosTests.Scenarios
{
    [Collection("Chaos Tests")]
    public class DataIngestionChaosTests : ChaosTestBase
    {
        private readonly IDataIngestionService _dataIngestionService;
        private readonly ITradingLogger _logger;

        public DataIngestionChaosTests(ITestOutputHelper output) : base(output)
        {
            _dataIngestionService = GetRequiredService<IDataIngestionService>();
            _logger = GetRequiredService<ITradingLogger>();
        }

        [Fact]
        public async Task DataProviders_WithIntermittentFailures_RecoverAndContinue()
        {
            // Arrange
            var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA" };
            var successfulIngestions = new List<string>();
            var failedIngestions = new List<string>();
            var totalAttempts = 50;

            // Create chaos policy for API failures
            var apiFailurePolicy = CreateExceptionChaosPolicy<TradingResult<MarketData>>(
                injectionRate: 0.3, // 30% failure rate
                exceptionFactory: (ctx, ct) => new System.Net.Http.HttpRequestException("API timeout"));

            // Act - Ingest data with chaos
            var tasks = new List<Task>();
            for (int i = 0; i < totalAttempts; i++)
            {
                var symbol = symbols[i % symbols.Length];
                var attemptIndex = i;

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // Wrap data ingestion with chaos policy
                        var result = await apiFailurePolicy.ExecuteAsync(async () =>
                        {
                            return await _dataIngestionService.IngestMarketDataAsync(
                                symbol,
                                TimeInterval.FiveMinutes,
                                CancellationToken.None);
                        });

                        if (result.IsSuccess)
                        {
                            lock (successfulIngestions)
                            {
                                successfulIngestions.Add($"{symbol}-{attemptIndex}");
                            }
                        }
                        else
                        {
                            lock (failedIngestions)
                            {
                                failedIngestions.Add($"{symbol}-{attemptIndex}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Output.WriteLine($"Ingestion {attemptIndex} failed: {ex.Message}");
                        lock (failedIngestions)
                        {
                            failedIngestions.Add($"{symbol}-{attemptIndex}");
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            var successRate = (double)successfulIngestions.Count / totalAttempts;
            successRate.Should().BeGreaterThan(0.6); // At least 60% success despite 30% chaos
            Output.WriteLine($"Data ingestion success rate: {successRate:P2} ({successfulIngestions.Count}/{totalAttempts})");
        }

        [Fact]
        public async Task DataProviders_WithRateLimitExhaustion_ThrottleGracefully()
        {
            // Arrange
            var burstSize = 100;
            var rateLimitExceeded = 0;
            var successfulRequests = 0;
            var symbol = "AAPL";

            // Act - Send burst of requests
            var tasks = new Task[burstSize];
            for (int i = 0; i < burstSize; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        var result = await _dataIngestionService.IngestMarketDataAsync(
                            symbol,
                            TimeInterval.OneMinute,
                            CancellationToken.None);

                        if (result.IsSuccess)
                        {
                            Interlocked.Increment(ref successfulRequests);
                        }
                        else if (result.Error?.Contains("rate limit", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            Interlocked.Increment(ref rateLimitExceeded);
                        }
                    }
                    catch (Exception ex) when (ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
                    {
                        Interlocked.Increment(ref rateLimitExceeded);
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Assert - Should handle rate limiting gracefully
            rateLimitExceeded.Should().BeGreaterThan(0);
            successfulRequests.Should().BeGreaterThan(0);
            Output.WriteLine($"Rate limit handling: {successfulRequests} successful, {rateLimitExceeded} rate limited");
        }

        [Fact]
        public async Task MarketDataAggregator_WithConflictingData_ResolvesConsistently()
        {
            // Arrange
            var aggregator = GetRequiredService<IMarketDataAggregator>();
            var symbol = "AAPL";
            var conflictingDataSets = new List<MarketData>();

            // Create conflicting data with chaos
            var dataCorruptionPolicy = CreateResultChaosPolicy<MarketData>(
                injectionRate: 0.2,
                resultFactory: (ctx, ct) => new MarketData
                {
                    Symbol = symbol,
                    Timestamp = DateTime.UtcNow,
                    Open = -999m, // Invalid price
                    High = 0m,
                    Low = 1000000m,
                    Close = decimal.MaxValue,
                    Volume = -1000
                });

            // Act - Generate and aggregate data
            for (int i = 0; i < 20; i++)
            {
                var data = await dataCorruptionPolicy.ExecuteAsync(async () =>
                {
                    return new MarketData
                    {
                        Symbol = symbol,
                        Timestamp = DateTime.UtcNow,
                        Open = 150m + ChaosRandom.Next(-5, 5),
                        High = 155m + ChaosRandom.Next(-3, 3),
                        Low = 145m + ChaosRandom.Next(-3, 3),
                        Close = 152m + ChaosRandom.Next(-5, 5),
                        Volume = 10000000 + ChaosRandom.Next(-1000000, 1000000)
                    };
                });

                conflictingDataSets.Add(data);
            }

            // Aggregate data
            var aggregatedResult = await aggregator.AggregateMarketDataAsync(
                conflictingDataSets,
                CancellationToken.None);

            // Assert - Aggregator should handle conflicts
            aggregatedResult.Should().NotBeNull();
            aggregatedResult.Open.Should().BeGreaterThan(0);
            aggregatedResult.Close.Should().BeGreaterThan(0);
            aggregatedResult.High.Should().BeGreaterThanOrEqualTo(aggregatedResult.Low);
            aggregatedResult.Volume.Should().BeGreaterThan(0);

            Output.WriteLine($"Aggregated {conflictingDataSets.Count} data points with conflicts");
        }

        [Fact]
        public async Task DataIngestion_WithProviderFailover_MaintainsAvailability()
        {
            // Arrange
            var primaryFailures = 0;
            var fallbackUsed = 0;
            var totalRequests = 30;

            // Simulate primary provider failures
            var primaryFailurePolicy = CreateExceptionChaosPolicy<TradingResult<MarketData>>(
                injectionRate: 0.5, // 50% primary failure
                exceptionFactory: (ctx, ct) =>
                {
                    Interlocked.Increment(ref primaryFailures);
                    return new InvalidOperationException("Primary provider unavailable");
                });

            // Act - Request data with failover
            var results = new List<bool>();
            for (int i = 0; i < totalRequests; i++)
            {
                try
                {
                    // First try primary provider with chaos
                    var primaryResult = await primaryFailurePolicy.ExecuteAsync(async () =>
                    {
                        // Simulate primary provider call
                        return TradingResult<MarketData>.Success(new MarketData
                        {
                            Symbol = "AAPL",
                            Timestamp = DateTime.UtcNow,
                            Source = "Primary"
                        });
                    });

                    results.Add(true);
                }
                catch (InvalidOperationException)
                {
                    // Failover to secondary provider
                    var fallbackResult = await _dataIngestionService.IngestMarketDataAsync(
                        "AAPL",
                        TimeInterval.FiveMinutes,
                        CancellationToken.None);

                    if (fallbackResult.IsSuccess)
                    {
                        Interlocked.Increment(ref fallbackUsed);
                        results.Add(true);
                    }
                    else
                    {
                        results.Add(false);
                    }
                }
            }

            // Assert
            var availability = results.Count(r => r) / (double)totalRequests;
            availability.Should().BeGreaterThan(0.95); // 95% availability with failover
            fallbackUsed.Should().BeGreaterThan(0);
            
            Output.WriteLine($"Provider failover: {primaryFailures} primary failures, {fallbackUsed} fallback used");
            Output.WriteLine($"Overall availability: {availability:P2}");
        }

        [Fact]
        public async Task DataIngestion_UnderMemoryPressure_HandlesBackpressure()
        {
            // Arrange
            var processedCount = 0;
            var droppedCount = 0;
            var memoryPressureDuration = TimeSpan.FromSeconds(10);

            // Start memory pressure simulation
            var memoryTask = SimulateResourceExhaustion(
                cpuStressThreads: 2,
                memoryMB: 800,
                duration: memoryPressureDuration);

            // Act - Ingest data under memory pressure
            var ingestionTask = Task.Run(async () =>
            {
                var startTime = DateTime.UtcNow;
                while (DateTime.UtcNow - startTime < memoryPressureDuration)
                {
                    try
                    {
                        var result = await _dataIngestionService.IngestMarketDataAsync(
                            $"SYM{processedCount % 10}",
                            TimeInterval.OneMinute,
                            CancellationToken.None);

                        if (result.IsSuccess)
                        {
                            Interlocked.Increment(ref processedCount);
                        }
                        else
                        {
                            Interlocked.Increment(ref droppedCount);
                        }
                    }
                    catch
                    {
                        Interlocked.Increment(ref droppedCount);
                    }

                    await Task.Delay(10); // Small delay between requests
                }
            });

            await Task.WhenAll(memoryTask, ingestionTask);

            // Assert - System should handle backpressure
            processedCount.Should().BeGreaterThan(0);
            var totalAttempts = processedCount + droppedCount;
            var successRate = processedCount / (double)totalAttempts;
            
            Output.WriteLine($"Under memory pressure: {processedCount} processed, {droppedCount} dropped");
            Output.WriteLine($"Success rate: {successRate:P2}");
        }

        [Fact]
        public async Task DataIngestion_WithCorruptedResponses_ValidatesAndRejects()
        {
            // Arrange
            var validData = 0;
            var corruptedData = 0;
            var totalRequests = 50;

            // Create corruption scenarios
            var corruptionScenarios = new Func<MarketData>[]
            {
                () => new MarketData { Symbol = null!, Timestamp = DateTime.UtcNow }, // Null symbol
                () => new MarketData { Symbol = "AAPL", Timestamp = DateTime.MinValue }, // Invalid timestamp
                () => new MarketData { Symbol = "AAPL", Timestamp = DateTime.UtcNow, Close = -100m }, // Negative price
                () => new MarketData { Symbol = "AAPL", Timestamp = DateTime.UtcNow, Volume = -1000 }, // Negative volume
                () => new MarketData { Symbol = "AAPL", Timestamp = DateTime.UtcNow, High = 100m, Low = 200m } // High < Low
            };

            // Act - Process mix of valid and corrupted data
            for (int i = 0; i < totalRequests; i++)
            {
                var isCorrupted = ChaosRandom.NextDouble() < 0.3; // 30% corruption rate
                
                MarketData data;
                if (isCorrupted)
                {
                    var scenario = corruptionScenarios[ChaosRandom.Next(corruptionScenarios.Length)];
                    data = scenario();
                }
                else
                {
                    data = new MarketData
                    {
                        Symbol = "AAPL",
                        Timestamp = DateTime.UtcNow,
                        Open = 150m,
                        High = 155m,
                        Low = 145m,
                        Close = 152m,
                        Volume = 10000000
                    };
                }

                // Validate data
                var isValid = ValidateMarketData(data);
                if (isValid)
                {
                    validData++;
                }
                else
                {
                    corruptedData++;
                }
            }

            // Assert
            corruptedData.Should().BeGreaterThan(0);
            validData.Should().BeGreaterThan(0);
            (validData + corruptedData).Should().Be(totalRequests);
            
            Output.WriteLine($"Data validation: {validData} valid, {corruptedData} corrupted/rejected");
        }

        private bool ValidateMarketData(MarketData data)
        {
            if (data == null) return false;
            if (string.IsNullOrEmpty(data.Symbol)) return false;
            if (data.Timestamp == DateTime.MinValue || data.Timestamp > DateTime.UtcNow.AddMinutes(1)) return false;
            if (data.Open < 0 || data.High < 0 || data.Low < 0 || data.Close < 0) return false;
            if (data.Volume < 0) return false;
            if (data.High < data.Low) return false;
            if (data.Open > data.High || data.Open < data.Low) return false;
            if (data.Close > data.High || data.Close < data.Low) return false;
            
            return true;
        }
    }
}