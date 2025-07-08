namespace MarketAnalyzer.Infrastructure.TechnicalAnalysis.Tests.Services;

/// <summary>
/// Integration tests for TechnicalAnalysisService focusing on service lifecycle,
/// error recovery, and high availability scenarios.
/// </summary>
public class TechnicalAnalysisServiceIntegrationTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<TechnicalAnalysisService> _logger;
    private readonly Mock<ILogger<TechnicalAnalysisService>> _mockLogger;
    private TechnicalAnalysisService _service;

    public TechnicalAnalysisServiceIntegrationTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<TechnicalAnalysisService>>();
        _logger = _mockLogger.Object;
        _service = new TechnicalAnalysisService(_logger, _cache);
    }

    #region Service Lifecycle Tests

    [Fact]
    public async Task ServiceLifecycle_InitializeStartStop_TransitionsCorrectly()
    {
        // Act & Assert - Initialize
        _service.Health.Should().Be(ServiceHealth.Unknown);
        
        var initResult = await _service.InitializeAsync();
        initResult.IsSuccess.Should().BeTrue();
        _service.Health.Should().Be(ServiceHealth.Initialized);

        // Act & Assert - Start
        var startResult = await _service.StartAsync();
        startResult.IsSuccess.Should().BeTrue();
        _service.Health.Should().Be(ServiceHealth.Running);

        // Act & Assert - Stop
        var stopResult = await _service.StopAsync();
        stopResult.IsSuccess.Should().BeTrue();
        _service.Health.Should().Be(ServiceHealth.Stopped);
    }

    [Fact]
    public async Task ServiceRestart_AfterStop_WorksCorrectly()
    {
        // Arrange - Initial lifecycle
        await _service.InitializeAsync();
        await _service.StartAsync();
        
        // Feed some data
        var symbol = "AAPL";
        for (int i = 0; i < 20; i++)
        {
            var quote = CreateMarketQuote(symbol, 100m + i);
            await _service.UpdateIndicatorsAsync(quote);
        }
        
        // Stop service
        await _service.StopAsync();
        _service.Dispose();

        // Act - Create new service instance and restart
        _service = new TechnicalAnalysisService(_logger, _cache);
        await _service.InitializeAsync();
        await _service.StartAsync();
        
        // Feed new data
        var newQuote = CreateMarketQuote(symbol, 120m);
        var updateResult = await _service.UpdateIndicatorsAsync(newQuote);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        _service.Health.Should().Be(ServiceHealth.Running);
    }

    #endregion

    #region Error Recovery Tests

    [Fact]
    public async Task ErrorRecovery_AfterCalculationError_ServiceRemainsHealthy()
    {
        // Arrange
        await _service.InitializeAsync();
        await _service.StartAsync();

        // Act - Trigger error with invalid symbol
        var errorResult = await _service.CalculateRSIAsync("");
        
        // Service should remain healthy
        _service.Health.Should().Be(ServiceHealth.Running);
        
        // Should still process valid requests
        var symbol = "AAPL";
        for (int i = 0; i < 20; i++)
        {
            await _service.UpdateIndicatorsAsync(CreateMarketQuote(symbol, 100m + i));
        }
        
        var validResult = await _service.CalculateRSIAsync(symbol);

        // Assert
        errorResult.IsSuccess.Should().BeFalse();
        validResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentOperations_MultipleSymbols_HandlesCorrectly()
    {
        // Arrange
        await _service.InitializeAsync();
        await _service.StartAsync();
        
        var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA" };
        var tasks = new List<Task>();

        // Act - Update multiple symbols concurrently
        foreach (var symbol in symbols)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < 30; i++)
                {
                    var quote = CreateMarketQuote(symbol, 100m + i + symbol.GetHashCode() % 10);
                    await _service.UpdateIndicatorsAsync(quote);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Calculate indicators for all symbols concurrently
        var indicatorTasks = symbols.Select(async symbol =>
        {
            var result = await _service.CalculateRSIAsync(symbol);
            return (symbol, result);
        }).ToList();

        var results = await Task.WhenAll(indicatorTasks);

        // Assert
        results.Should().HaveCount(5);
        results.Should().AllSatisfy(r => r.result.IsSuccess.Should().BeTrue());
        
        // Each symbol should have different values
        var rsiValues = results.Select(r => r.result.Value).ToList();
        rsiValues.Distinct().Count().Should().BeGreaterThan(1);
    }

    #endregion

    #region High Availability Tests

    [Fact]
    public async Task HighAvailability_ContinuousDataFeed_NoDataLoss()
    {
        // Arrange
        await _service.InitializeAsync();
        await _service.StartAsync();
        
        var symbol = "AAPL";
        var processedCount = 0;
        var totalQuotes = 1000;

        // Act - Continuous data feed
        var feedTask = Task.Run(async () =>
        {
            for (int i = 0; i < totalQuotes; i++)
            {
                var quote = CreateMarketQuote(symbol, 100m + i * 0.01m);
                var result = await _service.UpdateIndicatorsAsync(quote);
                if (result.IsSuccess)
                {
                    Interlocked.Increment(ref processedCount);
                }
                
                // Simulate real-time feed
                if (i % 10 == 0)
                {
                    await Task.Delay(1);
                }
            }
        });

        // Concurrent indicator calculations
        var calcTask = Task.Run(async () =>
        {
            var calculations = 0;
            while (processedCount < totalQuotes && calculations < 100)
            {
                await Task.Delay(10);
                var result = await _service.CalculateRSIAsync(symbol);
                if (result.IsSuccess)
                {
                    calculations++;
                }
            }
            return calculations;
        });

        await feedTask;
        var calculationCount = await calcTask;

        // Assert
        processedCount.Should().Be(totalQuotes);
        calculationCount.Should().BeGreaterThan(0);
        _service.Health.Should().Be(ServiceHealth.Running);
    }

    [Fact]
    public async Task GracefulShutdown_WithPendingOperations_CompletesCleanly()
    {
        // Arrange
        await _service.InitializeAsync();
        await _service.StartAsync();
        
        var symbol = "AAPL";
        var updateCount = 0;

        // Act - Start continuous updates
        var updateTask = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                var quote = CreateMarketQuote(symbol, 100m + i);
                await _service.UpdateIndicatorsAsync(quote);
                Interlocked.Increment(ref updateCount);
                await Task.Delay(5);
            }
        });

        // Wait for some updates
        await Task.Delay(50);
        
        // Initiate shutdown while updates are running
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var stopResult = await _service.StopAsync(cts.Token);

        // Assert
        stopResult.IsSuccess.Should().BeTrue();
        _service.Health.Should().Be(ServiceHealth.Stopped);
        updateCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region Logging and Debugging Tests

    [Fact]
    public async Task Logging_AllOperations_ProperlyLogged()
    {
        // Arrange
        await _service.InitializeAsync();
        await _service.StartAsync();

        // Act
        var symbol = "AAPL";
        await _service.UpdateIndicatorsAsync(CreateMarketQuote(symbol, 100m));
        await _service.CalculateRSIAsync(symbol);

        // Assert - Verify proper logging patterns
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Method entry")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Method exit")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ErrorControl_ExceptionInCalculation_LoggedAndHandled()
    {
        // Arrange
        await _service.InitializeAsync();
        await _service.StartAsync();

        // Act - Force an error condition
        var result = await _service.CalculateRSIAsync("INVALID_SYMBOL_WITH_NO_DATA");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        
        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Helper Methods

    private static MarketQuote CreateMarketQuote(string symbol, decimal price)
    {
        var quote = new MarketQuote(
            symbol: symbol,
            currentPrice: price,
            previousClose: price - 0.5m,
            dayOpen: price - 0.3m,
            dayHigh: price + 0.5m,
            dayLow: price - 0.5m,
            volume: 1000000L,
            timestamp: DateTime.UtcNow,
            hardwareTimestamp: DateTime.UtcNow.Ticks,
            marketStatus: MarketStatus.Open,
            isRealTime: true
        );
        quote.UpdateBidAsk(price - 0.01m, price + 0.01m, 1000, 1000);
        return quote;
    }

    #endregion

    public void Dispose()
    {
        _service?.Dispose();
        _cache?.Dispose();
    }
}