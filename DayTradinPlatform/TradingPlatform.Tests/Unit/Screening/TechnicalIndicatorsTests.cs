using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Screening.Indicators;
using TradingPlatform.Core.Models;
using TradingPlatform.Tests.Core.Canonical;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Tests.Unit.Screening
{
    /// <summary>
    /// Comprehensive unit tests for TechnicalIndicators
    /// Tests RSI, Moving Averages, Bollinger Bands, Candlestick Patterns, and Trend Analysis
    /// </summary>
    public class TechnicalIndicatorsTests : CanonicalTestBase<TechnicalIndicators>
    {
        private const decimal PRICE_PRECISION = 0.01m;
        
        public TechnicalIndicatorsTests(ITestOutputHelper output) : base(output)
        {
        }
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ITradingLogger>(MockLogger.Object);
        }
        
        protected override TechnicalIndicators CreateSystemUnderTest()
        {
            return new TechnicalIndicators(MockLogger.Object);
        }
        
        #region RSI Tests
        
        [Fact]
        public async Task CalculateRSIAsync_StandardPeriod_ReturnsCorrectValue()
        {
            // Arrange - Generate price data with known pattern
            var priceData = GenerateTrendingPriceData(20, 100m, 0.01m); // Uptrend
            
            // Act
            var result = await SystemUnderTest.CalculateRSIAsync(priceData, 14);
            
            // Assert
            Assert.True(result > 50m, "RSI should be above 50 for uptrend");
            Assert.True(result < 100m, "RSI should be below 100");
            Output.WriteLine($"RSI: {result:F2}");
        }
        
        [Fact]
        public async Task CalculateRSIAsync_AllGains_Returns100()
        {
            // Arrange - Only upward price movements
            var priceData = new List<DailyData>();
            for (int i = 0; i < 20; i++)
            {
                priceData.Add(CreateDailyData(100m + i, DateTime.Today.AddDays(-20 + i)));
            }
            
            // Act
            var result = await SystemUnderTest.CalculateRSIAsync(priceData, 14);
            
            // Assert
            Assert.Equal(100m, result);
        }
        
        [Fact]
        public async Task CalculateRSIAsync_InsufficientData_Returns50()
        {
            // Arrange
            var priceData = GenerateTrendingPriceData(10, 100m, 0.01m); // Less than period + 1
            
            // Act
            var result = await SystemUnderTest.CalculateRSIAsync(priceData, 14);
            
            // Assert
            Assert.Equal(50m, result); // Default neutral value
        }
        
        [Fact]
        public async Task CalculateRSIAsync_OversoldCondition_ReturnsBelow30()
        {
            // Arrange - Strong downtrend
            var priceData = GenerateTrendingPriceData(20, 100m, -0.02m);
            
            // Act
            var result = await SystemUnderTest.CalculateRSIAsync(priceData, 14);
            
            // Assert
            Assert.True(result < 30m, $"RSI {result} should indicate oversold condition");
        }
        
        #endregion
        
        #region Moving Average Tests
        
        [Fact]
        public async Task CalculateMovingAveragesAsync_SufficientData_ReturnsCorrectAverages()
        {
            // Arrange - 60 days of data
            var priceData = new List<DailyData>();
            for (int i = 0; i < 60; i++)
            {
                priceData.Add(CreateDailyData(100m + i * 0.1m, DateTime.Today.AddDays(-60 + i)));
            }
            
            // Act
            var (sma20, sma50) = await SystemUnderTest.CalculateMovingAveragesAsync(priceData);
            
            // Assert
            // SMA20 should be average of last 20 prices (104.05)
            var expectedSma20 = priceData.TakeLast(20).Average(d => d.Close);
            AssertFinancialPrecision(expectedSma20, sma20, 2);
            
            // SMA50 should be average of last 50 prices (102.45)
            var expectedSma50 = priceData.TakeLast(50).Average(d => d.Close);
            AssertFinancialPrecision(expectedSma50, sma50, 2);
            
            // In uptrend, SMA20 > SMA50
            Assert.True(sma20 > sma50, "SMA20 should be above SMA50 in uptrend");
        }
        
        [Fact]
        public async Task CalculateMovingAveragesAsync_InsufficientData_UsesAvailableData()
        {
            // Arrange - Only 10 days
            var priceData = GenerateConstantPriceData(10, 100m);
            
            // Act
            var (sma20, sma50) = await SystemUnderTest.CalculateMovingAveragesAsync(priceData);
            
            // Assert
            Assert.Equal(100m, sma20);
            Assert.Equal(100m, sma50);
        }
        
        #endregion
        
        #region Bollinger Band Tests
        
        [Fact]
        public async Task CalculateBollingerBandPositionAsync_PriceAtMiddle_Returns50Percent()
        {
            // Arrange - Constant prices
            var priceData = GenerateConstantPriceData(20, 100m);
            var marketData = new MarketData { Price = 100m };
            
            // Act
            var result = await SystemUnderTest.CalculateBollingerBandPositionAsync(marketData, priceData);
            
            // Assert
            AssertFinancialPrecision(0.5m, result, 2); // Middle of bands
        }
        
        [Fact]
        public async Task CalculateBollingerBandPositionAsync_PriceAtUpperBand_Returns100Percent()
        {
            // Arrange - Price data with known standard deviation
            var priceData = new List<DailyData>();
            for (int i = 0; i < 20; i++)
            {
                var price = 100m + (i % 2 == 0 ? 2m : -2m); // Oscillating prices
                priceData.Add(CreateDailyData(price, DateTime.Today.AddDays(-20 + i)));
            }
            
            var avg = priceData.Select(d => d.Close).Average();
            var stdDev = CalculateStdDev(priceData.Select(d => d.Close));
            var upperBand = avg + 2 * stdDev;
            
            var marketData = new MarketData { Price = upperBand };
            
            // Act
            var result = await SystemUnderTest.CalculateBollingerBandPositionAsync(marketData, priceData);
            
            // Assert
            AssertFinancialPrecision(1m, result, 2); // At upper band
        }
        
        [Fact]
        public async Task CalculateBollingerBandPositionAsync_InsufficientData_ReturnsDefault()
        {
            // Arrange
            var priceData = GenerateConstantPriceData(10, 100m);
            var marketData = new MarketData { Price = 100m };
            
            // Act
            var result = await SystemUnderTest.CalculateBollingerBandPositionAsync(marketData, priceData, 20);
            
            // Assert
            Assert.Equal(0.5m, result);
        }
        
        #endregion
        
        #region Candlestick Pattern Tests
        
        [Fact]
        public async Task DetectCandlestickPatternAsync_Doji_RecognizesPattern()
        {
            // Arrange - Doji pattern (open ≈ close)
            var priceData = new List<DailyData>
            {
                CreateCandlestick(100m, 105m, 95m, 99m), // Previous
                CreateCandlestick(99m, 104m, 96m, 98m),  // Previous
                CreateCandlestick(100m, 110m, 90m, 100.5m) // Doji (small body, large range)
            };
            
            // Act
            var result = await SystemUnderTest.DetectCandlestickPatternAsync(priceData);
            
            // Assert
            Assert.Equal("Doji", result);
        }
        
        [Fact]
        public async Task DetectCandlestickPatternAsync_Hammer_RecognizesPattern()
        {
            // Arrange - Hammer pattern (small body at top, long lower shadow)
            var priceData = new List<DailyData>
            {
                CreateCandlestick(100m, 102m, 98m, 99m),  // Downtrend
                CreateCandlestick(99m, 100m, 97m, 98m),   // Downtrend
                CreateCandlestick(98m, 99m, 94m, 98.5m)  // Hammer
            };
            
            // Act
            var result = await SystemUnderTest.DetectCandlestickPatternAsync(priceData);
            
            // Assert
            Assert.Equal("Hammer", result);
        }
        
        [Fact]
        public async Task DetectCandlestickPatternAsync_InsufficientData_ReturnsMessage()
        {
            // Arrange
            var priceData = new List<DailyData>
            {
                CreateDailyData(100m, DateTime.Today)
            };
            
            // Act
            var result = await SystemUnderTest.DetectCandlestickPatternAsync(priceData);
            
            // Assert
            Assert.Equal("Insufficient Data", result);
        }
        
        #endregion
        
        #region Trend Analysis Tests
        
        [Fact]
        public async Task AnalyzeTrendAsync_StrongUptrend_IdentifiesCorrectly()
        {
            // Arrange - Strong upward movement
            var priceData = GenerateTrendingPriceData(25, 100m, 0.005m); // 0.5% daily gain
            
            // Act
            var result = await SystemUnderTest.AnalyzeTrendAsync(priceData, 20);
            
            // Assert
            Assert.Equal(TrendDirection.StrongUptrend, result);
        }
        
        [Fact]
        public async Task AnalyzeTrendAsync_Downtrend_IdentifiesCorrectly()
        {
            // Arrange - Downward movement
            var priceData = GenerateTrendingPriceData(25, 100m, -0.003m); // 0.3% daily loss
            
            // Act
            var result = await SystemUnderTest.AnalyzeTrendAsync(priceData, 20);
            
            // Assert
            Assert.Equal(TrendDirection.Downtrend, result);
        }
        
        [Fact]
        public async Task AnalyzeTrendAsync_Sideways_IdentifiesCorrectly()
        {
            // Arrange - Oscillating prices
            var priceData = new List<DailyData>();
            for (int i = 0; i < 25; i++)
            {
                var price = 100m + (decimal)Math.Sin(i * 0.5) * 2m;
                priceData.Add(CreateDailyData(price, DateTime.Today.AddDays(-25 + i)));
            }
            
            // Act
            var result = await SystemUnderTest.AnalyzeTrendAsync(priceData, 20);
            
            // Assert
            Assert.Equal(TrendDirection.Sideways, result);
        }
        
        #endregion
        
        #region Breakout Setup Tests
        
        [Fact]
        public async Task IsBreakoutSetupAsync_NearResistanceWithVolume_ReturnsTrue()
        {
            // Arrange - Price near recent high with volume spike
            var priceData = new List<DailyData>();
            for (int i = 0; i < 25; i++)
            {
                var price = 95m + i * 0.2m; // Trending up to 100
                priceData.Add(new DailyData
                {
                    Open = price - 0.5m,
                    High = price + 0.5m,
                    Low = price - 0.5m,
                    Close = price,
                    Volume = 1000000,
                    Date = DateTime.Today.AddDays(-25 + i)
                });
            }
            
            var marketData = new MarketData 
            { 
                Price = 99.5m, // Near high of 99.5
                Volume = 2000000 // Volume spike
            };
            
            // Act
            var result = await SystemUnderTest.IsBreakoutSetupAsync(marketData, priceData);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public async Task IsBreakoutSetupAsync_MiddleOfRangeNoVolume_ReturnsFalse()
        {
            // Arrange
            var priceData = GenerateConstantPriceData(25, 100m);
            var marketData = new MarketData 
            { 
                Price = 100m,
                Volume = 1000000
            };
            
            // Act
            var result = await SystemUnderTest.IsBreakoutSetupAsync(marketData, priceData);
            
            // Assert
            Assert.False(result);
        }
        
        #endregion
        
        #region Helper Methods
        
        private DailyData CreateDailyData(decimal price, DateTime date)
        {
            return new DailyData
            {
                Open = price - 0.5m,
                High = price + 1m,
                Low = price - 1m,
                Close = price,
                Volume = 1000000,
                Date = date
            };
        }
        
        private DailyData CreateCandlestick(decimal open, decimal high, decimal low, decimal close)
        {
            return new DailyData
            {
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = 1000000,
                Date = DateTime.Today
            };
        }
        
        private List<DailyData> GenerateTrendingPriceData(int days, decimal startPrice, decimal dailyReturn)
        {
            var data = new List<DailyData>();
            var price = startPrice;
            
            for (int i = 0; i < days; i++)
            {
                price *= (1 + dailyReturn);
                data.Add(CreateDailyData(price, DateTime.Today.AddDays(-days + i)));
            }
            
            return data;
        }
        
        private List<DailyData> GenerateConstantPriceData(int days, decimal price)
        {
            var data = new List<DailyData>();
            
            for (int i = 0; i < days; i++)
            {
                data.Add(CreateDailyData(price, DateTime.Today.AddDays(-days + i)));
            }
            
            return data;
        }
        
        private decimal CalculateStdDev(IEnumerable<decimal> values)
        {
            var valuesList = values.ToList();
            var avg = valuesList.Average();
            var sumOfSquares = valuesList.Sum(v => (v - avg) * (v - avg));
            var variance = sumOfSquares / valuesList.Count;
            return (decimal)Math.Sqrt((double)variance);
        }
        
        #endregion
    }
    
    public enum TrendDirection
    {
        StrongDowntrend,
        Downtrend,
        Sideways,
        Uptrend,
        StrongUptrend
    }
}