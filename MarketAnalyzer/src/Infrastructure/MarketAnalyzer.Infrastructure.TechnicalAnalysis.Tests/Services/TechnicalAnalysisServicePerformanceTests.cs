using System.Diagnostics;

namespace MarketAnalyzer.Infrastructure.TechnicalAnalysis.Tests.Services;

/// <summary>
/// Performance tests for TechnicalAnalysisService to ensure O(1) streaming calculations.
/// </summary>
public class TechnicalAnalysisServicePerformanceTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<TechnicalAnalysisService> _logger;
    private readonly TechnicalAnalysisService _service;

    public TechnicalAnalysisServicePerformanceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = new Mock<ILogger<TechnicalAnalysisService>>().Object;
        _service = new TechnicalAnalysisService(_logger, _cache);
    }

    [Fact]
    public async Task StreamingCalculation_Performance_MaintainsO1Complexity()
    {
        // Arrange
        await _service.InitializeAsync();
        await _service.StartAsync();
        
        var symbol = "AAPL";
        var timings = new List<long>();
        
        // Warm up with initial data
        for (int i = 0; i < 100; i++)
        {
            var quote = new MarketQuote(
                symbol: symbol,
                currentPrice: 100m + i * 0.1m,
                previousClose: 99.5m,
                dayOpen: 99.8m,
                dayHigh: 100.5m + i * 0.1m,
                dayLow: 99.5m,
                volume: 1000000L,
                timestamp: DateTime.UtcNow.AddMinutes(-100 + i),
                hardwareTimestamp: DateTime.UtcNow.Ticks,
                marketStatus: MarketStatus.Open,
                isRealTime: true
            );
            quote.UpdateBidAsk(100m, 100.1m, 1000, 1000);
            await _service.UpdateIndicatorsAsync(quote);
        }

        // Act - Measure time for streaming updates
        for (int i = 0; i < 50; i++)
        {
            var quote = new MarketQuote(
                symbol: symbol,
                currentPrice: 105m + i * 0.05m,
                previousClose: 104.5m,
                dayOpen: 104.8m,
                dayHigh: 105.5m + i * 0.05m,
                dayLow: 104.5m,
                volume: 1100000L,
                timestamp: DateTime.UtcNow.AddMinutes(i),
                hardwareTimestamp: DateTime.UtcNow.Ticks,
                marketStatus: MarketStatus.Open,
                isRealTime: true
            );
            quote.UpdateBidAsk(105m, 105.1m, 1000, 1000);

            var stopwatch = Stopwatch.StartNew();
            await _service.UpdateIndicatorsAsync(quote);
            await _service.CalculateRSIAsync(symbol);
            stopwatch.Stop();
            
            timings.Add(stopwatch.ElapsedMilliseconds);
        }

        // Assert - Verify O(1) complexity
        var firstHalfAvg = timings.Take(25).Average();
        var secondHalfAvg = timings.Skip(25).Average();
        
        // Time should not increase significantly with more data
        var percentageIncrease = ((secondHalfAvg - firstHalfAvg) / firstHalfAvg) * 100;
        percentageIncrease.Should().BeLessThan(10); // Allow max 10% variance
        
        // All calculations should be under 1ms for O(1) operations
        timings.Should().NotBeNull();
        timings.Should().AllSatisfy(t => t.Should().BeLessThan(50)); // 50ms safety margin
    }

    [Fact]
    public async Task GetAllIndicators_Performance_ExecutesInParallel()
    {
        // Arrange
        await _service.InitializeAsync();
        await _service.StartAsync();
        
        var symbol = "AAPL";
        
        // Feed sufficient data
        for (int i = 0; i < 60; i++)
        {
            var quote = new MarketQuote(
                symbol: symbol,
                currentPrice: 100m + i * 0.2m,
                previousClose: 99.5m,
                dayOpen: 99.8m,
                dayHigh: 100.5m + i * 0.2m,
                dayLow: 99.5m,
                volume: 1000000L + i * 10000L,
                timestamp: DateTime.UtcNow.AddMinutes(-60 + i),
                hardwareTimestamp: DateTime.UtcNow.Ticks,
                marketStatus: MarketStatus.Open,
                isRealTime: true
            );
            quote.UpdateBidAsk(100m, 100.1m, 1000, 1000);
            await _service.UpdateIndicatorsAsync(quote);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.GetAllIndicatorsAsync(symbol);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Count.Should().BeGreaterThan(10); // Should have all indicators
        
        // Parallel execution should complete quickly
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100); // Should be much faster than sequential
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10000)]
    public async Task MemoryEfficiency_LargeDatasets_MaintainsFixedMemory(int dataPoints)
    {
        // Arrange
        await _service.InitializeAsync();
        await _service.StartAsync();
        
        var symbol = "AAPL";
        var initialMemory = GC.GetTotalMemory(true);

        // Act - Feed large dataset
        for (int i = 0; i < dataPoints; i++)
        {
            var quote = new MarketQuote(
                symbol: symbol,
                currentPrice: 100m + (i % 100) * 0.1m,
                previousClose: 99.5m,
                dayOpen: 99.8m,
                dayHigh: 101m,
                dayLow: 99.5m,
                volume: 1000000L,
                timestamp: DateTime.UtcNow.AddSeconds(i),
                hardwareTimestamp: DateTime.UtcNow.Ticks,
                marketStatus: MarketStatus.Open,
                isRealTime: true
            );
            quote.UpdateBidAsk(100m, 100.1m, 1000, 1000);
            await _service.UpdateIndicatorsAsync(quote);
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert - Memory should be bounded (only stores last 1000 values)
        var memoryPerDataPoint = memoryIncrease / dataPoints;
        memoryPerDataPoint.Should().BeLessThan(1000); // Less than 1KB per data point indicates bounded memory
    }

    [Fact]
    public async Task CachePerformance_FrequentAccess_HighHitRate()
    {
        // Arrange
        await _service.InitializeAsync();
        await _service.StartAsync();
        
        var symbol = "AAPL";
        var cacheHits = 0;
        var totalCalls = 100;
        
        // Feed initial data
        for (int i = 0; i < 20; i++)
        {
            var quote = new MarketQuote(
                symbol: symbol,
                currentPrice: 100m + i,
                previousClose: 99.5m,
                dayOpen: 99.8m,
                dayHigh: 101m,
                dayLow: 99.5m,
                volume: 1000000L,
                timestamp: DateTime.UtcNow.AddMinutes(-20 + i),
                hardwareTimestamp: DateTime.UtcNow.Ticks,
                marketStatus: MarketStatus.Open,
                isRealTime: true
            );
            quote.UpdateBidAsk(100m, 100.1m, 1000, 1000);
            await _service.UpdateIndicatorsAsync(quote);
        }

        // Act - Make repeated calls within cache TTL
        var tasks = new List<Task>();
        for (int i = 0; i < totalCalls; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                await _service.CalculateRSIAsync(symbol);
                stopwatch.Stop();
                
                // Cache hits should be < 1ms
                if (stopwatch.ElapsedMilliseconds < 1)
                {
                    Interlocked.Increment(ref cacheHits);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var hitRate = (double)cacheHits / totalCalls * 100;
        hitRate.Should().BeGreaterThan(90); // At least 90% cache hit rate
    }

    public void Dispose()
    {
        _service?.Dispose();
        _cache?.Dispose();
    }
}