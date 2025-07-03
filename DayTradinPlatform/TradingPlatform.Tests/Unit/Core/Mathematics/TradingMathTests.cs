using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Common.Mathematics;
using TradingPlatform.Tests.Core.Canonical;

namespace TradingPlatform.Tests.Unit.Core.Mathematics
{
    /// <summary>
    /// Comprehensive unit tests for TradingMath financial calculations
    /// Tests P&L, returns, risk metrics, and technical indicators
    /// </summary>
    public class TradingMathTests : CanonicalTestBase<TradingMath>
    {
        // Financial precision tolerance (8 decimal places for critical calculations)
        private const decimal FINANCIAL_PRECISION = 0.00000001m;
        // Less precision for percentage calculations
        private const decimal PERCENTAGE_PRECISION = 0.0001m;
        
        public TradingMathTests(ITestOutputHelper output) : base(output)
        {
        }
        
        protected override void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
        {
            // TradingMath is static, no services needed
        }
        
        protected override TradingMath CreateSystemUnderTest()
        {
            // Static class, return null
            return null;
        }
        
        #region P&L Calculation Tests
        
        [Theory]
        [InlineData(100, 110, 100, true, 0, 1000)] // Long profit: (110-100)*100
        [InlineData(100, 90, 100, true, 0, -1000)] // Long loss: (90-100)*100
        [InlineData(100, 90, 100, false, 0, 1000)] // Short profit: (100-90)*100
        [InlineData(100, 110, 100, false, 0, -1000)] // Short loss: (100-110)*100
        public void CalculatePnL_BasicScenarios_ReturnsCorrectPnL(
            decimal entryPrice, decimal exitPrice, decimal quantity, bool isLong, 
            decimal commission, decimal expectedPnL)
        {
            // Act
            var result = TradingMath.CalculatePnL(entryPrice, exitPrice, quantity, isLong, commission);
            
            // Assert
            Assert.Equal(expectedPnL, result);
        }
        
        [Theory]
        [InlineData(100, 110, 100, true, 0.01, 998)] // $2 commission total
        [InlineData(100, 110, 100, true, 0.05, 990)] // $10 commission total
        [InlineData(100, 101, 100, true, 0.01, 98)]  // Small profit eaten by commission
        [InlineData(100, 100.5, 100, true, 0.01, 48)] // Break-even after commission
        public void CalculatePnL_WithCommission_DeductsCorrectly(
            decimal entryPrice, decimal exitPrice, decimal quantity, bool isLong, 
            decimal commissionPerShare, decimal expectedPnL)
        {
            // Act
            var result = TradingMath.CalculatePnL(entryPrice, exitPrice, quantity, isLong, commissionPerShare);
            
            // Assert
            Assert.Equal(expectedPnL, result);
        }
        
        [Theory]
        [InlineData(0, 100, 100, true)] // Zero entry price
        [InlineData(100, 0, 100, true)] // Zero exit price
        [InlineData(100, 100, 0, true)] // Zero quantity
        [InlineData(-100, 100, 100, true)] // Negative entry price
        public void CalculatePnL_InvalidInputs_ThrowsArgumentException(
            decimal entryPrice, decimal exitPrice, decimal quantity, bool isLong)
        {
            // Assert
            Assert.Throws<ArgumentException>(() => 
                TradingMath.CalculatePnL(entryPrice, exitPrice, quantity, isLong));
        }
        
        [Fact]
        public void CalculatePnL_LargePositions_MaintainsPrecision()
        {
            // Arrange - Large institutional trade
            decimal entryPrice = 150.2575m;
            decimal exitPrice = 150.3825m;
            decimal quantity = 100000m;
            decimal commissionPerShare = 0.0035m;
            
            // Act
            var result = TradingMath.CalculatePnL(entryPrice, exitPrice, quantity, true, commissionPerShare);
            
            // Assert
            // Gross P&L: (150.3825 - 150.2575) * 100000 = 1250
            // Commission: 0.0035 * 100000 * 2 = 700
            // Net P&L: 1250 - 700 = 550
            Assert.Equal(550m, result);
        }
        
        #endregion
        
        #region Return Calculation Tests
        
        [Theory]
        [InlineData(100, 110, true, 10)] // 10% gain long
        [InlineData(100, 90, true, -10)] // 10% loss long
        [InlineData(100, 90, false, 10)] // 10% gain short
        [InlineData(100, 110, false, -10)] // 10% loss short
        [InlineData(50, 75, true, 50)] // 50% gain
        [InlineData(200, 100, true, -50)] // 50% loss
        public void CalculateReturn_BasicScenarios_ReturnsCorrectPercentage(
            decimal entryPrice, decimal exitPrice, bool isLong, decimal expectedReturn)
        {
            // Act
            var result = TradingMath.CalculateReturn(entryPrice, exitPrice, isLong);
            
            // Assert
            AssertFinancialPrecision(expectedReturn, result, 4);
        }
        
        [Theory]
        [InlineData(100.50, 101.25, true, 0.7463)] // Small gain with precision
        [InlineData(45.123, 45.678, true, 1.2305)] // Fractional prices
        [InlineData(0.0001, 0.0002, true, 100)] // Penny stock double
        public void CalculateReturn_PrecisionScenarios_MaintainsAccuracy(
            decimal entryPrice, decimal exitPrice, bool isLong, decimal expectedReturn)
        {
            // Act
            var result = TradingMath.CalculateReturn(entryPrice, exitPrice, isLong);
            
            // Assert
            AssertFinancialPrecision(expectedReturn, result, 4);
        }
        
        #endregion
        
        #region Max Drawdown Tests
        
        [Fact]
        public void CalculateMaxDrawdown_CumulativePnL_ReturnsCorrectDrawdown()
        {
            // Arrange - Cumulative P&L series with drawdown
            var pnlValues = new List<decimal> 
            { 
                0, 100, 200, 150, 50, 100, 300, 250, 200, 400 
            };
            
            // Act
            var result = TradingMath.CalculateMaxDrawdown(pnlValues, true);
            
            // Assert
            // Max drawdown from 300 to 200 = 100
            Assert.Equal(150m, result); // From 200 to 50
        }
        
        [Fact]
        public void CalculateMaxDrawdown_IndividualPnL_CalculatesCumulative()
        {
            // Arrange - Individual P&L values
            var pnlValues = new List<decimal> 
            { 
                100, 100, -50, -100, 50, 200, -50, -50, 200 
            };
            
            // Act
            var result = TradingMath.CalculateMaxDrawdown(pnlValues, false);
            
            // Assert
            // Cumulative: 100, 200, 150, 50, 100, 300, 250, 200, 400
            // Max drawdown from 200 to 50 = 150
            Assert.Equal(150m, result);
        }
        
        [Fact]
        public void CalculateMaxDrawdown_NoDrawdown_ReturnsZero()
        {
            // Arrange - Monotonically increasing P&L
            var pnlValues = new List<decimal> { 0, 100, 200, 300, 400, 500 };
            
            // Act
            var result = TradingMath.CalculateMaxDrawdown(pnlValues, true);
            
            // Assert
            Assert.Equal(0m, result);
        }
        
        [Fact]
        public void CalculateMaxDrawdown_EmptyList_ReturnsZero()
        {
            // Act
            var result = TradingMath.CalculateMaxDrawdown(new List<decimal>(), true);
            
            // Assert
            Assert.Equal(0m, result);
        }
        
        #endregion
        
        #region Sharpe Ratio Tests
        
        [Fact]
        public void CalculateSharpeRatio_PositiveReturns_ReturnsCorrectRatio()
        {
            // Arrange - Daily returns in percentage
            var returns = new List<decimal> { 0.5m, 1.0m, -0.5m, 0.8m, 0.2m, -0.3m, 0.6m };
            decimal riskFreeRate = 2m; // 2% annual
            
            // Act
            var result = TradingMath.CalculateSharpeRatio(returns, riskFreeRate);
            
            // Assert
            // This is a complex calculation, verify it's positive and reasonable
            Assert.True(result > 0);
            Assert.True(result < 10); // Reasonable Sharpe ratio range
        }
        
        [Fact]
        public void CalculateSharpeRatio_ZeroVolatility_ReturnsZero()
        {
            // Arrange - Constant returns
            var returns = new List<decimal> { 1m, 1m, 1m, 1m, 1m };
            
            // Act
            var result = TradingMath.CalculateSharpeRatio(returns);
            
            // Assert
            Assert.Equal(0m, result);
        }
        
        [Fact]
        public void CalculateSharpeRatio_EmptyReturns_ReturnsZero()
        {
            // Act
            var result = TradingMath.CalculateSharpeRatio(new List<decimal>());
            
            // Assert
            Assert.Equal(0m, result);
        }
        
        #endregion
        
        #region VaR Calculation Tests
        
        [Fact]
        public void CalculateVaR_NormalDistribution_Returns95PercentileValue()
        {
            // Arrange - Returns sorted: -5, -3, -2, -1, 0, 1, 2, 3, 4, 5
            var returns = new List<decimal> { 1, -2, 3, -5, 0, 2, -1, 5, -3, 4 };
            decimal portfolioValue = 100000m;
            
            // Act
            var result = TradingMath.CalculateVaR(returns, 0.95m, portfolioValue);
            
            // Assert
            // At 95% confidence, we look at the 5th percentile (worst 5%)
            // With 10 values, that's the 0.5th position, so we take the 1st value = -5
            // VaR = 5% of 100,000 = 5,000
            Assert.Equal(5000m, result);
        }
        
        [Fact]
        public void CalculateVaR_EmptyReturns_ReturnsZero()
        {
            // Act
            var result = TradingMath.CalculateVaR(new List<decimal>(), 0.95m, 100000m);
            
            // Assert
            Assert.Equal(0m, result);
        }
        
        #endregion
        
        #region Technical Indicator Tests
        
        [Fact]
        public void CalculateVWAP_StandardData_ReturnsCorrectValue()
        {
            // Arrange
            var priceVolumeData = new List<(decimal price, decimal volume)>
            {
                (100m, 1000m),
                (101m, 2000m),
                (99m, 1500m),
                (100.5m, 500m)
            };
            
            // Act
            var result = TradingMath.CalculateVWAP(priceVolumeData);
            
            // Assert
            // VWAP = (100*1000 + 101*2000 + 99*1500 + 100.5*500) / (1000+2000+1500+500)
            // = (100000 + 202000 + 148500 + 50250) / 5000
            // = 500750 / 5000 = 100.15
            Assert.Equal(100.15m, result);
        }
        
        [Fact]
        public void CalculateVWAP_ZeroVolume_ThrowsException()
        {
            // Arrange
            var priceVolumeData = new List<(decimal price, decimal volume)>
            {
                (100m, 0m),
                (101m, 0m)
            };
            
            // Assert
            Assert.Throws<ArgumentException>(() => 
                TradingMath.CalculateVWAP(priceVolumeData));
        }
        
        [Fact]
        public void CalculateTWAP_MultipleTimestamps_ReturnsTimeWeightedAverage()
        {
            // Arrange
            var pricesWithTimestamps = new List<(decimal price, DateTime timestamp)>
            {
                (100m, DateTime.Parse("2025-01-30 09:30:00")),
                (101m, DateTime.Parse("2025-01-30 09:31:00")), // 1 minute = 60 seconds
                (102m, DateTime.Parse("2025-01-30 09:33:00")), // 2 minutes = 120 seconds
                (100m, DateTime.Parse("2025-01-30 09:34:00"))  // 1 minute = 60 seconds
            };
            
            // Act
            var result = TradingMath.CalculateTWAP(pricesWithTimestamps);
            
            // Assert
            // Weights: 60, 120, 60 (total 240)
            // TWAP = (100*60 + 101*120 + 102*60) / 240
            // = (6000 + 12120 + 6120) / 240
            // = 24240 / 240 = 101
            Assert.Equal(101m, result);
        }
        
        [Fact]
        public void CalculateRSI_StandardPeriod_ReturnsCorrectValue()
        {
            // Arrange - Price series that should give RSI around 70 (overbought)
            var prices = new List<decimal>
            {
                100, 101, 102, 103, 104, 105, 106, 107, 108, 109,
                110, 111, 112, 113, 114, 115, 114, 113, 112, 111
            };
            
            // Act
            var result = TradingMath.CalculateRSI(prices, 14);
            
            // Assert
            // RSI should be high (>70) due to strong uptrend
            Assert.True(result > 60);
            Assert.True(result < 100);
            Output.WriteLine($"RSI: {result}");
        }
        
        [Fact]
        public void CalculateRSI_AllGains_Returns100()
        {
            // Arrange - Only upward moves
            var prices = Enumerable.Range(100, 20).Select(x => (decimal)x).ToList();
            
            // Act
            var result = TradingMath.CalculateRSI(prices, 14);
            
            // Assert
            Assert.Equal(100m, result);
        }
        
        [Fact]
        public void CalculateBollingerBands_StandardData_ReturnsCorrectBands()
        {
            // Arrange - 20 prices with some volatility
            var prices = new List<decimal>
            {
                100, 101, 99, 102, 98, 103, 97, 104, 96, 105,
                95, 106, 94, 107, 93, 108, 92, 109, 91, 110
            };
            
            // Act
            var (upper, middle, lower) = TradingMath.CalculateBollingerBands(prices, 20, 2);
            
            // Assert
            Assert.True(upper > middle);
            Assert.True(middle > lower);
            Assert.True(upper - lower > 10); // Should have significant band width
            
            // Middle band should be average of all 20 prices
            var expectedMiddle = prices.Average();
            AssertFinancialPrecision(expectedMiddle, middle, 2);
        }
        
        #endregion
        
        #region Risk Management Calculations
        
        [Theory]
        [InlineData(0.6, 150, 100, 0.002)] // 60% win rate, 1.5:1 risk/reward
        [InlineData(0.5, 200, 100, 0)]     // 50% win rate, 2:1 risk/reward (breakeven)
        [InlineData(0.4, 300, 100, 0.002)] // 40% win rate, 3:1 risk/reward
        public void CalculateKellyPercent_VariousScenarios_ReturnsOptimalFraction(
            decimal winRate, decimal avgWin, decimal avgLoss, decimal expectedKelly)
        {
            // Act
            var result = TradingMath.CalculateKellyPercent(winRate, avgWin, avgLoss);
            
            // Assert
            AssertFinancialPrecision(expectedKelly, result, 4);
        }
        
        [Fact]
        public void CalculateKellyPercent_ExtremelyProfitable_CapsAt25Percent()
        {
            // Arrange - 90% win rate with 5:1 risk/reward
            decimal winRate = 0.9m;
            decimal avgWin = 500m;
            decimal avgLoss = 100m;
            
            // Act
            var result = TradingMath.CalculateKellyPercent(winRate, avgWin, avgLoss);
            
            // Assert - Should be capped at 25%
            Assert.Equal(0.25m, result);
        }
        
        [Fact]
        public void CalculateCorrelation_PerfectPositive_ReturnsOne()
        {
            // Arrange
            var series1 = new List<decimal> { 1, 2, 3, 4, 5 };
            var series2 = new List<decimal> { 2, 4, 6, 8, 10 };
            
            // Act
            var result = TradingMath.CalculateCorrelation(series1, series2);
            
            // Assert
            AssertFinancialPrecision(1m, result, 4);
        }
        
        [Fact]
        public void CalculateCorrelation_PerfectNegative_ReturnsNegativeOne()
        {
            // Arrange
            var series1 = new List<decimal> { 1, 2, 3, 4, 5 };
            var series2 = new List<decimal> { 5, 4, 3, 2, 1 };
            
            // Act
            var result = TradingMath.CalculateCorrelation(series1, series2);
            
            // Assert
            AssertFinancialPrecision(-1m, result, 4);
        }
        
        [Fact]
        public void CalculateCorrelation_NoCorrelation_ReturnsNearZero()
        {
            // Arrange
            var series1 = new List<decimal> { 1, 2, 3, 4, 5 };
            var series2 = new List<decimal> { 3, 1, 4, 2, 5 };
            
            // Act
            var result = TradingMath.CalculateCorrelation(series1, series2);
            
            // Assert
            Assert.True(Math.Abs(result) < 0.5m);
        }
        
        #endregion
        
        #region Statistical Function Tests
        
        [Fact]
        public void StandardDeviation_SampleData_ReturnsCorrectValue()
        {
            // Arrange
            var values = new List<decimal> { 2, 4, 4, 4, 5, 5, 7, 9 };
            
            // Act
            var result = TradingMath.StandardDeviation(values, false);
            
            // Assert
            // Sample standard deviation should be approximately 2.0
            AssertFinancialPrecision(2m, result, 1);
        }
        
        [Fact]
        public void MovingAverage_SimpleSeries_ReturnsCorrectAverages()
        {
            // Arrange
            var values = new List<decimal> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int period = 3;
            
            // Act
            var result = TradingMath.MovingAverage(values, period).ToList();
            
            // Assert
            var expected = new List<decimal> { 2, 3, 4, 5, 6, 7, 8, 9 };
            Assert.Equal(expected.Count, result.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i], result[i]);
            }
        }
        
        #endregion
        
        #region Utility Function Tests
        
        [Theory]
        [InlineData(123.456789, 2, 123.46)]
        [InlineData(123.454, 2, 123.45)]
        [InlineData(123.455, 2, 123.46)] // Banker's rounding to even
        [InlineData(123.465, 2, 123.46)] // Banker's rounding to even
        public void RoundFinancial_BankersRounding_RoundsCorrectly(
            decimal value, int decimals, decimal expected)
        {
            // Act
            var result = TradingMath.RoundFinancial(value, decimals);
            
            // Assert
            Assert.Equal(expected, result);
        }
        
        [Theory]
        [InlineData(25, 100, 4, 25)]
        [InlineData(33.333333, 100, 2, 33.33)]
        [InlineData(0, 100, 4, 0)]
        public void CalculatePercentage_VariousInputs_ReturnsCorrectPercentage(
            decimal value, decimal total, int decimals, decimal expected)
        {
            // Act
            var result = TradingMath.CalculatePercentage(value, total, decimals);
            
            // Assert
            Assert.Equal(expected, result);
        }
        
        [Fact]
        public void CalculatePercentage_ZeroTotal_ReturnsZero()
        {
            // Act
            var result = TradingMath.CalculatePercentage(100, 0);
            
            // Assert
            Assert.Equal(0m, result);
        }
        
        [Theory]
        [InlineData(100, 110, 10)]
        [InlineData(100, 90, -10)]
        [InlineData(50, 100, 100)]
        [InlineData(100, 50, -50)]
        public void CalculatePercentageChange_VariousChanges_ReturnsCorrectPercentage(
            decimal oldValue, decimal newValue, decimal expected)
        {
            // Act
            var result = TradingMath.CalculatePercentageChange(oldValue, newValue);
            
            // Assert
            AssertFinancialPrecision(expected, result, 4);
        }
        
        [Theory]
        [InlineData(5, 0, 10, 5)]
        [InlineData(-5, 0, 10, 0)]
        [InlineData(15, 0, 10, 10)]
        [InlineData(5, 10, 0, 5)] // Should throw
        public void Clamp_VariousBounds_ClampsCorrectly(
            decimal value, decimal min, decimal max, decimal expected)
        {
            if (min > max)
            {
                // Assert
                Assert.Throws<ArgumentException>(() => TradingMath.Clamp(value, min, max));
            }
            else
            {
                // Act
                var result = TradingMath.Clamp(value, min, max);
                
                // Assert
                Assert.Equal(expected, result);
            }
        }
        
        #endregion
        
        #region Performance Tests
        
        [Fact]
        public void AllCalculations_LargeDataSet_CompletesQuickly()
        {
            // Arrange - 10,000 data points
            var random = new Random(42);
            var prices = Enumerable.Range(1, 10000)
                .Select(i => 100m + (decimal)(random.NextDouble() * 10 - 5))
                .ToList();
            
            // Act & Assert - Should complete all calculations within 1 second
            AssertCompletesWithinAsync(1000, async () =>
            {
                // P&L calculations
                for (int i = 0; i < 100; i++)
                {
                    TradingMath.CalculatePnL(100, 110, 100, true, 0.01m);
                }
                
                // Technical indicators
                TradingMath.CalculateRSI(prices.Take(100).ToList(), 14);
                TradingMath.CalculateBollingerBands(prices.Take(20).ToList());
                TradingMath.MovingAverage(prices.Take(100).ToList(), 20);
                
                // Risk metrics
                TradingMath.CalculateMaxDrawdown(prices.Take(1000).ToList());
                TradingMath.CalculateSharpeRatio(prices.Take(252).ToList());
                TradingMath.CalculateVaR(prices.Take(100).ToList());
                
                await Task.CompletedTask;
            }).GetAwaiter().GetResult();
        }
        
        #endregion
    }
}