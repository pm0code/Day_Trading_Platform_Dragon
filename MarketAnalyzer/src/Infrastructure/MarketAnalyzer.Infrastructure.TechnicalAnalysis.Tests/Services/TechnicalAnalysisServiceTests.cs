using System.Collections.Concurrent;

namespace MarketAnalyzer.Infrastructure.TechnicalAnalysis.Tests.Services;

/// <summary>
/// Comprehensive unit tests for TechnicalAnalysisService.
/// Tests streaming calculations, financial precision, and all technical indicators.
/// </summary>
public class TechnicalAnalysisServiceTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<TechnicalAnalysisService> _logger;
    private readonly TechnicalAnalysisService _service;
    private readonly Mock<ILogger<TechnicalAnalysisService>> _mockLogger;

    public TechnicalAnalysisServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<TechnicalAnalysisService>>();
        _logger = _mockLogger.Object;
        _service = new TechnicalAnalysisService(_logger, _cache);
    }

    #region Setup and Teardown

    private async Task InitializeServiceAsync()
    {
        await _service.InitializeAsync();
        await _service.StartAsync();
    }

    public void Dispose()
    {
        _service?.Dispose();
        _cache?.Dispose();
    }

    #endregion

    #region Helper Methods

    private static MarketQuote CreateMarketQuote(string symbol, decimal price, long volume = 1000000, 
        decimal? high = null, decimal? low = null, decimal? open = null)
    {
        var quote = new MarketQuote(
            symbol: symbol,
            currentPrice: price,
            previousClose: price - 0.5m,
            dayOpen: open ?? price - 0.3m,
            dayHigh: high ?? price + 0.5m,
            dayLow: low ?? price - 0.5m,
            volume: volume,
            timestamp: DateTime.UtcNow,
            hardwareTimestamp: DateTime.UtcNow.Ticks,
            marketStatus: MarketStatus.Open,
            isRealTime: true
        );
        
        // Update bid/ask separately
        quote.UpdateBidAsk(price - 0.01m, price + 0.01m, 1000, 1000);
        
        return quote;
    }

    private async Task FeedHistoricalDataAsync(string symbol, decimal[] prices, long[] volumes = null!)
    {
        volumes ??= Enumerable.Repeat(1000000L, prices.Length).ToArray();

        for (int i = 0; i < prices.Length; i++)
        {
            var quote = CreateMarketQuote(symbol, prices[i], volumes[i],
                high: prices[i] + 0.5m, 
                low: prices[i] - 0.5m,
                open: prices[i] - 0.2m);
            await _service.UpdateIndicatorsAsync(quote);
        }
    }

    #endregion

    #region RSI Tests

    [Fact]
    public async Task CalculateRSIAsync_EmptySymbol_ReturnsFailure()
    {
        // Arrange
        await InitializeServiceAsync();

        // Act
        var result = await _service.CalculateRSIAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error?.Code.Should().Be("INVALID_SYMBOL");
    }

    [Theory]
    [InlineData("AAPL", 14)]
    [InlineData("GOOGL", 21)]
    [InlineData("MSFT", 7)]
    public async Task CalculateRSIAsync_ValidSymbol_ReturnsRSIValue(string symbol, int period)
    {
        // Arrange
        await InitializeServiceAsync();
        
        // Feed enough data for RSI calculation
        var prices = new decimal[period + 5];
        for (int i = 0; i < prices.Length; i++)
        {
            // Create price movements for RSI calculation
            prices[i] = 100m + (i % 2 == 0 ? i * 0.5m : -i * 0.3m);
        }
        await FeedHistoricalDataAsync(symbol, prices);

        // Act
        var result = await _service.CalculateRSIAsync(symbol, period);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThanOrEqualTo(0m).And.BeLessThanOrEqualTo(100m);
    }

    [Fact]
    public async Task CalculateRSIAsync_StreamingUpdate_RecalculatesCorrectly()
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Initial data
        var prices = Enumerable.Range(1, 20).Select(i => 100m + i).ToArray();
        await FeedHistoricalDataAsync(symbol, prices);
        
        var firstResult = await _service.CalculateRSIAsync(symbol);

        // Act - Add new streaming data
        await _service.UpdateIndicatorsAsync(CreateMarketQuote(symbol, 125m));
        var secondResult = await _service.CalculateRSIAsync(symbol);

        // Assert
        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsSuccess.Should().BeTrue();
        secondResult.Value.Should().NotBe(firstResult.Value);
    }

    #endregion

    #region SMA Tests

    [Theory]
    [InlineData(10, 100.5)]
    [InlineData(20, 105.0)]
    [InlineData(50, 110.0)]
    public async Task CalculateSMAAsync_VariousPeriods_ReturnsCorrectAverage(int period, decimal basePrice)
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Create consistent prices for easy SMA validation
        var prices = Enumerable.Range(1, period).Select(i => basePrice).ToArray();
        await FeedHistoricalDataAsync(symbol, prices);

        // Act
        var result = await _service.CalculateSMAAsync(symbol, period);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(basePrice); // All prices same, so SMA should equal the price
    }

    [Fact]
    public async Task CalculateSMAAsync_FinancialPrecision_MaintainsDecimalAccuracy()
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Use prices that test decimal precision
        var prices = new[] { 100.123456m, 100.234567m, 100.345678m, 100.456789m, 100.567890m };
        await FeedHistoricalDataAsync(symbol, prices);

        // Act
        var result = await _service.CalculateSMAAsync(symbol, 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var expectedSma = prices.Average();
        result.Value.Should().Be(expectedSma);
    }

    #endregion

    #region EMA Tests

    [Fact]
    public async Task CalculateEMAAsync_ValidData_ReturnsExponentialAverage()
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        var period = 12;
        
        // Feed trending data for EMA
        var prices = Enumerable.Range(1, 20).Select(i => 100m + i * 2m).ToArray();
        await FeedHistoricalDataAsync(symbol, prices);

        // Act
        var result = await _service.CalculateEMAAsync(symbol, period);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0m);
        // EMA should be closer to recent prices than SMA
        var smaResult = await _service.CalculateSMAAsync(symbol, period);
        result.Value.Should().BeGreaterThan(smaResult.Value); // In uptrend, EMA > SMA
    }

    #endregion

    #region Bollinger Bands Tests

    [Theory]
    [InlineData(20, 2.0)]
    [InlineData(10, 1.5)]
    [InlineData(30, 2.5)]
    public async Task CalculateBollingerBandsAsync_ValidData_ReturnsThreeBands(int period, double stdDev)
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Create volatile data for band calculation
        var prices = new decimal[period + 10];
        for (int i = 0; i < prices.Length; i++)
        {
            prices[i] = 100m + (decimal)(Math.Sin(i * 0.5) * 10);
        }
        await FeedHistoricalDataAsync(symbol, prices);

        // Act
        var result = await _service.CalculateBollingerBandsAsync(symbol, period, (decimal)stdDev);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Upper.Should().BeGreaterThan(result.Value.Middle);
        result.Value.Lower.Should().BeLessThan(result.Value.Middle);
        result.Value.Middle.Should().BeGreaterThan(0m);
    }

    #endregion

    #region MACD Tests

    [Fact]
    public async Task CalculateMACDAsync_ValidData_ReturnsThreeComponents()
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Feed sufficient data for MACD (26 + 9 periods minimum)
        var prices = Enumerable.Range(1, 40).Select(i => 100m + i * 0.5m).ToArray();
        await FeedHistoricalDataAsync(symbol, prices);

        // Act
        var result = await _service.CalculateMACDAsync(symbol);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MACD.Should().NotBe(0m);
        result.Value.Signal.Should().NotBe(0m);
        result.Value.Histogram.Should().Be(result.Value.MACD - result.Value.Signal);
    }

    #endregion

    #region ATR Tests

    [Fact]
    public async Task CalculateATRAsync_ValidOHLCData_ReturnsAverageTrueRange()
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Create OHLC data with volatility
        var prices = new decimal[20];
        for (int i = 0; i < prices.Length; i++)
        {
            prices[i] = 100m + (i % 3) * 2m;
        }
        await FeedHistoricalDataAsync(symbol, prices);

        // Act
        var result = await _service.CalculateATRAsync(symbol);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0m);
    }

    #endregion

    #region Stochastic Tests

    [Theory]
    [InlineData(14, 3)]
    [InlineData(21, 5)]
    [InlineData(7, 3)]
    public async Task CalculateStochasticAsync_ValidData_ReturnsKAndD(int kPeriod, int dPeriod)
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Create oscillating price data
        var prices = new decimal[kPeriod + dPeriod + 5];
        for (int i = 0; i < prices.Length; i++)
        {
            prices[i] = 100m + (decimal)(Math.Sin(i * 0.3) * 5);
        }
        await FeedHistoricalDataAsync(symbol, prices);

        // Act
        var result = await _service.CalculateStochasticAsync(symbol, kPeriod, dPeriod);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.K.Should().BeInRange(0m, 100m);
        result.Value.D.Should().BeInRange(0m, 100m);
    }

    #endregion

    #region OBV Tests

    [Fact]
    public async Task CalculateOBVAsync_ValidPriceVolume_ReturnsOnBalanceVolume()
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Create price and volume data
        var prices = new[] { 100m, 101m, 100.5m, 102m, 101.5m };
        var volumes = new[] { 1000000L, 1500000L, 800000L, 2000000L, 1200000L };
        await FeedHistoricalDataAsync(symbol, prices, volumes);

        // Act
        var result = await _service.CalculateOBVAsync(symbol);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(0m);
    }

    [Fact]
    public async Task CalculateOBVAsync_StreamingUpdates_AccumulatesCorrectly()
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Initial data
        await _service.UpdateIndicatorsAsync(CreateMarketQuote(symbol, 100m, 1000000));
        await _service.UpdateIndicatorsAsync(CreateMarketQuote(symbol, 101m, 1500000)); // Price up, volume added
        
        var firstObv = await _service.CalculateOBVAsync(symbol);

        // Act - Price down, volume subtracted
        await _service.UpdateIndicatorsAsync(CreateMarketQuote(symbol, 100.5m, 2000000));
        var secondObv = await _service.CalculateOBVAsync(symbol);

        // Assert
        firstObv.IsSuccess.Should().BeTrue();
        secondObv.IsSuccess.Should().BeTrue();
        secondObv.Value.Should().BeLessThan(firstObv.Value);
    }

    #endregion

    #region Volume Profile Tests

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public async Task CalculateVolumeProfileAsync_ValidData_ReturnsPriceLevelVolumes(int priceLevels)
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Create distributed price data
        var prices = new decimal[30];
        var volumes = new long[30];
        for (int i = 0; i < prices.Length; i++)
        {
            prices[i] = 95m + (i % 10) * 1m; // Prices between 95-105
            volumes[i] = 100000L * (i % 5 + 1); // Varying volumes
        }
        await FeedHistoricalDataAsync(symbol, prices, volumes);

        // Act
        var result = await _service.CalculateVolumeProfileAsync(symbol, priceLevels);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().NotBeEmpty();
        result.Value!.Count.Should().BeLessThanOrEqualTo(priceLevels);
        result.Value!.Values.Should().AllSatisfy(v => v.Should().BeGreaterThan(0));
    }

    #endregion

    #region VWAP Tests

    [Fact]
    public async Task CalculateVWAPAsync_ValidData_ReturnsVolumeWeightedPrice()
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Create price/volume data where VWAP should differ from simple average
        var prices = new[] { 100m, 101m, 102m, 103m, 104m };
        var volumes = new[] { 5000000L, 1000000L, 1000000L, 1000000L, 2000000L };
        await FeedHistoricalDataAsync(symbol, prices, volumes);

        // Act
        var result = await _service.CalculateVWAPAsync(symbol);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0m);
        
        // VWAP should be weighted toward high-volume prices
        var simpleAverage = prices.Average();
        result.Value.Should().BeLessThan(simpleAverage); // Because highest volume at lowest price
    }

    #endregion

    #region Ichimoku Tests

    [Fact]
    public async Task CalculateIchimokuAsync_ValidData_ReturnsFiveComponents()
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Feed sufficient data for Ichimoku (52 periods minimum)
        var prices = new decimal[60];
        for (int i = 0; i < prices.Length; i++)
        {
            prices[i] = 100m + (decimal)(Math.Sin(i * 0.1) * 10);
        }
        await FeedHistoricalDataAsync(symbol, prices);

        // Act
        var result = await _service.CalculateIchimokuAsync(symbol);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Tenkan.Should().BeGreaterThan(0m);
        result.Value.Kijun.Should().BeGreaterThan(0m);
        result.Value.SpanA.Should().BeGreaterThan(0m);
        result.Value.SpanB.Should().BeGreaterThan(0m);
        result.Value.Chikou.Should().BeGreaterThan(0m);
    }

    [Theory]
    [InlineData(9, 26, 52, 26)]
    [InlineData(7, 22, 44, 22)]
    public async Task CalculateIchimokuAsync_CustomPeriods_CalculatesCorrectly(
        int tenkanPeriod, int kijunPeriod, int spanBPeriod, int displacement)
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Feed data based on maximum period needed
        var dataPoints = Math.Max(spanBPeriod, displacement) + 10;
        var prices = Enumerable.Range(1, dataPoints).Select(i => 100m + i * 0.5m).ToArray();
        await FeedHistoricalDataAsync(symbol, prices);

        // Act
        var result = await _service.CalculateIchimokuAsync(symbol, tenkanPeriod, kijunPeriod, spanBPeriod, displacement);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SpanA.Should().Be((result.Value.Tenkan + result.Value.Kijun) / 2m);
    }

    #endregion

    #region GetAllIndicators Tests

    [Fact]
    public async Task GetAllIndicatorsAsync_WithSufficientData_ReturnsAllIndicators()
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Feed enough data for all indicators
        var prices = new decimal[60];
        var volumes = new long[60];
        for (int i = 0; i < prices.Length; i++)
        {
            prices[i] = 100m + (decimal)(Math.Sin(i * 0.1) * 5);
            volumes[i] = 1000000L + (i * 10000L);
        }
        await FeedHistoricalDataAsync(symbol, prices, volumes);

        // Act
        var result = await _service.GetAllIndicatorsAsync(symbol);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().ContainKey("RSI");
        result.Value.Should().ContainKey("SMA_20");
        result.Value.Should().ContainKey("EMA_12");
        result.Value.Should().ContainKey("BollingerBands");
        result.Value.Should().ContainKey("MACD");
        result.Value.Should().ContainKey("ATR");
        result.Value.Should().ContainKey("Stochastic");
        result.Value.Should().ContainKey("OBV");
        result.Value.Should().ContainKey("VolumeProfile");
        result.Value.Should().ContainKey("VWAP");
        result.Value.Should().ContainKey("Ichimoku");
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task CalculateRSIAsync_CalledTwice_UsesCachedValue()
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Feed data
        var prices = Enumerable.Range(1, 20).Select(i => 100m + i).ToArray();
        await FeedHistoricalDataAsync(symbol, prices);

        // Act
        var result1 = await _service.CalculateRSIAsync(symbol);
        var result2 = await _service.CalculateRSIAsync(symbol);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.Should().Be(result2.Value);
        
        // Verify cache was used (no additional calculations)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("cache")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task CalculateIndicator_InsufficientData_ReturnsGracefulError()
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Feed insufficient data
        await _service.UpdateIndicatorsAsync(CreateMarketQuote(symbol, 100m));

        // Act
        var result = await _service.CalculateRSIAsync(symbol);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error?.Code.Should().Be("INSUFFICIENT_DATA");
    }

    [Fact]
    public async Task UpdateIndicatorsAsync_NullQuote_ReturnsFailure()
    {
        // Arrange
        await InitializeServiceAsync();

        // Act
        var result = await _service.UpdateIndicatorsAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error?.Code.Should().Be("NULL_QUOTE");
    }

    #endregion

    #region Decimal Precision Tests

    [Fact]
    public async Task AllCalculations_MaintainDecimalPrecision_NoFloatingPointErrors()
    {
        // Arrange
        await InitializeServiceAsync();
        var symbol = "AAPL";
        
        // Use prices that could cause floating point issues
        var prices = new[] { 
            100.333333m, 100.666666m, 100.999999m, 
            101.111111m, 101.444444m, 101.777777m 
        };
        await FeedHistoricalDataAsync(symbol, prices);

        // Act & Assert - All calculations should maintain decimal precision
        var sma = await _service.CalculateSMAAsync(symbol, 5);
        sma.IsSuccess.Should().BeTrue();
        sma.Value.ToString().Should().NotContain("E"); // No scientific notation
        
        var ema = await _service.CalculateEMAAsync(symbol, 5);
        ema.IsSuccess.Should().BeTrue();
        ema.Value.ToString().Should().NotContain("E");
    }

    #endregion
}