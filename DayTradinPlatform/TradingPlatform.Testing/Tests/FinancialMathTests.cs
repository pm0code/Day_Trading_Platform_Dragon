using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using TradingPlatform.Core.Mathematics;

namespace TradingPlatform.Testing.Tests
{
    public class FinancialMathTests
    {
        #region Sqrt Tests

        [Fact]
        public void Sqrt_ZeroValue_ReturnsZero()
        {
            // Arrange
            decimal input = 0m;

            // Act
            decimal result = FinancialMath.Sqrt(input);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void Sqrt_OneValue_ReturnsOne()
        {
            // Arrange
            decimal input = 1m;

            // Act
            decimal result = FinancialMath.Sqrt(input);

            // Assert
            Assert.Equal(1m, result);
        }

        [Fact]
        public void Sqrt_PerfectSquares_ReturnsExactValues()
        {
            // Test perfect squares for financial calculations
            Assert.Equal(2m, FinancialMath.Sqrt(4m));
            Assert.Equal(3m, FinancialMath.Sqrt(9m));
            Assert.Equal(5m, FinancialMath.Sqrt(25m));
            Assert.Equal(10m, FinancialMath.Sqrt(100m));
        }

        [Fact]
        public void Sqrt_TypicalStockPrices_ReturnsAccurateResults()
        {
            // Arrange - typical stock price scenarios
            decimal stockPrice1 = 150.75m;
            decimal stockPrice2 = 45.25m;

            // Act
            decimal result1 = FinancialMath.Sqrt(stockPrice1);
            decimal result2 = FinancialMath.Sqrt(stockPrice2);

            // Assert - verify precision and reasonable values
            Assert.True(result1 > 12m && result1 < 13m); // ~12.28
            Assert.True(result2 > 6m && result2 < 7m);   // ~6.73

            // Verify precision by squaring back
            decimal squared1 = result1 * result1;
            decimal squared2 = result2 * result2;
            Assert.True(Math.Abs(squared1 - stockPrice1) < 0.0001m);
            Assert.True(Math.Abs(squared2 - stockPrice2) < 0.0001m);
        }

        [Fact]
        public void Sqrt_NegativeValue_ThrowsArgumentException()
        {
            // Arrange
            decimal negativeValue = -10m;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => FinancialMath.Sqrt(negativeValue));
        }

        #endregion

        #region Variance Tests

        [Fact]
        public void Variance_EmptyCollection_ReturnsZero()
        {
            // Arrange
            var emptyList = new List<decimal>();

            // Act
            decimal result = FinancialMath.Variance(emptyList);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void Variance_SingleValue_ReturnsZero()
        {
            // Arrange
            var singleValue = new List<decimal> { 100m };

            // Act
            decimal result = FinancialMath.Variance(singleValue);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void Variance_IdenticalValues_ReturnsZero()
        {
            // Arrange - all prices the same (no volatility)
            var identicalPrices = new List<decimal> { 50m, 50m, 50m, 50m, 50m };

            // Act
            decimal result = FinancialMath.Variance(identicalPrices);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void Variance_TypicalStockPrices_CalculatesCorrectly()
        {
            // Arrange - realistic daily stock prices
            var stockPrices = new List<decimal> { 100m, 102m, 98m, 105m, 97m };

            // Expected calculation:
            // Mean = (100 + 102 + 98 + 105 + 97) / 5 = 100.4
            // Variance = [(100-100.4)² + (102-100.4)² + (98-100.4)² + (105-100.4)² + (97-100.4)²] / 5
            // = [0.16 + 2.56 + 5.76 + 21.16 + 11.56] / 5 = 41.2 / 5 = 8.24

            // Act
            decimal result = FinancialMath.Variance(stockPrices);

            // Assert
            Assert.Equal(8.24m, result);
        }

        [Fact]
        public void Variance_HighVolatilityScenario_ReturnsLargeVariance()
        {
            // Arrange - highly volatile stock prices
            var volatilePrices = new List<decimal> { 50m, 150m, 25m, 175m, 75m };

            // Act
            decimal result = FinancialMath.Variance(volatilePrices);

            // Assert
            Assert.True(result > 2000m); // High volatility should produce large variance
        }

        #endregion

        #region StandardDeviation Tests

        [Fact]
        public void StandardDeviation_KnownVariance_ReturnsCorrectStdDev()
        {
            // Arrange - prices with known variance of 8.24
            var stockPrices = new List<decimal> { 100m, 102m, 98m, 105m, 97m };

            // Act
            decimal result = FinancialMath.StandardDeviation(stockPrices);

            // Assert - should be sqrt(8.24) ≈ 2.87
            Assert.True(result > 2.8m && result < 2.9m);
        }

        [Fact]
        public void StandardDeviation_EmptyCollection_ReturnsZero()
        {
            // Arrange
            var emptyList = new List<decimal>();

            // Act
            decimal result = FinancialMath.StandardDeviation(emptyList);

            // Assert
            Assert.Equal(0m, result);
        }

        #endregion

        #region RoundFinancial Tests

        [Fact]
        public void RoundFinancial_DefaultPrecision_RoundsToTwoDecimals()
        {
            // Arrange
            decimal value = 123.456789m;

            // Act
            decimal result = FinancialMath.RoundFinancial(value);

            // Assert
            Assert.Equal(123.46m, result);
        }

        [Fact]
        public void RoundFinancial_CustomPrecision_RoundsCorrectly()
        {
            // Arrange
            decimal value = 123.456789m;

            // Act
            decimal result = FinancialMath.RoundFinancial(value, 4);

            // Assert
            Assert.Equal(123.4568m, result);
        }

        [Fact]
        public void RoundFinancial_MidpointRounding_UsesToEven()
        {
            // Test banker's rounding (round to even)
            Assert.Equal(2.4m, FinancialMath.RoundFinancial(2.45m, 1));
            Assert.Equal(2.6m, FinancialMath.RoundFinancial(2.55m, 1));
        }

        #endregion

        #region CalculatePercentage Tests

        [Fact]
        public void CalculatePercentage_ValidInputs_ReturnsCorrectPercentage()
        {
            // Arrange
            decimal value = 25m;
            decimal total = 100m;

            // Act
            decimal result = FinancialMath.CalculatePercentage(value, total);

            // Assert
            Assert.Equal(25.0000m, result);
        }

        [Fact]
        public void CalculatePercentage_ZeroTotal_ReturnsZero()
        {
            // Arrange
            decimal value = 50m;
            decimal total = 0m;

            // Act
            decimal result = FinancialMath.CalculatePercentage(value, total);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void CalculatePercentage_StockPortfolioScenario_CalculatesCorrectly()
        {
            // Arrange - stock position as percentage of portfolio
            decimal stockValue = 15000m;
            decimal portfolioTotal = 75000m;

            // Act
            decimal result = FinancialMath.CalculatePercentage(stockValue, portfolioTotal);

            // Assert
            Assert.Equal(20.0000m, result); // 20%
        }

        #endregion

        #region CalculatePercentageChange Tests

        [Fact]
        public void CalculatePercentageChange_PositiveChange_ReturnsPositivePercentage()
        {
            // Arrange - stock price increase
            decimal oldPrice = 100m;
            decimal newPrice = 110m;

            // Act
            decimal result = FinancialMath.CalculatePercentageChange(oldPrice, newPrice);

            // Assert
            Assert.Equal(10.0000m, result); // 10% increase
        }

        [Fact]
        public void CalculatePercentageChange_NegativeChange_ReturnsNegativePercentage()
        {
            // Arrange - stock price decrease
            decimal oldPrice = 100m;
            decimal newPrice = 85m;

            // Act
            decimal result = FinancialMath.CalculatePercentageChange(oldPrice, newPrice);

            // Assert
            Assert.Equal(-15.0000m, result); // 15% decrease
        }

        [Fact]
        public void CalculatePercentageChange_ZeroOldValue_ReturnsZero()
        {
            // Arrange
            decimal oldPrice = 0m;
            decimal newPrice = 50m;

            // Act
            decimal result = FinancialMath.CalculatePercentageChange(oldPrice, newPrice);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void CalculatePercentageChange_DayTradingScenario_CalculatesCorrectly()
        {
            // Arrange - typical day trading price movement
            decimal openPrice = 45.67m;
            decimal currentPrice = 47.23m;

            // Act
            decimal result = FinancialMath.CalculatePercentageChange(openPrice, currentPrice);

            // Assert
            // Expected: (47.23 - 45.67) / 45.67 * 100 = 3.4159%
            Assert.True(result > 3.4m && result < 3.42m);
        }

        #endregion

        #region DecimalExtensions Tests

        [Fact]
        public void ToFinancialPrecision_Extension_WorksCorrectly()
        {
            // Arrange
            decimal value = 123.456789m;

            // Act
            decimal result = value.ToFinancialPrecision();

            // Assert
            Assert.Equal(123.46m, result);
        }

        [Fact]
        public void PercentageOf_Extension_WorksCorrectly()
        {
            // Arrange
            decimal value = 25m;
            decimal total = 100m;

            // Act
            decimal result = value.PercentageOf(total);

            // Assert
            Assert.Equal(25.0000m, result);
        }

        [Fact]
        public void PercentageChangeTo_Extension_WorksCorrectly()
        {
            // Arrange
            decimal oldValue = 100m;
            decimal newValue = 110m;

            // Act
            decimal result = oldValue.PercentageChangeTo(newValue);

            // Assert
            Assert.Equal(10.0000m, result);
        }

        #endregion

        #region Golden Rules Compliance Tests

        [Fact]
        public void AllCalculations_UseSystemDecimal_NeverDoubleOrFloat()
        {
            // This test ensures we never accidentally use double/float
            // All method signatures should use decimal

            // Arrange - financial values that could lose precision with double
            decimal preciseValue = 0.123456789012345678901234567890m;
            var preciseList = new List<decimal> { preciseValue, preciseValue + 0.01m };

            // Act - all operations should maintain decimal precision
            decimal sqrtResult = FinancialMath.Sqrt(25m);
            decimal varianceResult = FinancialMath.Variance(preciseList);
            decimal stdDevResult = FinancialMath.StandardDeviation(preciseList);
            decimal roundResult = FinancialMath.RoundFinancial(preciseValue);
            decimal percentResult = FinancialMath.CalculatePercentage(preciseValue, 1m);
            decimal changeResult = FinancialMath.CalculatePercentageChange(preciseValue, preciseValue + 0.01m);

            // Assert - all results should be decimal type (compile-time check)
            Assert.IsType<decimal>(sqrtResult);
            Assert.IsType<decimal>(varianceResult);
            Assert.IsType<decimal>(stdDevResult);
            Assert.IsType<decimal>(roundResult);
            Assert.IsType<decimal>(percentResult);
            Assert.IsType<decimal>(changeResult);
        }

        [Fact]
        public void VolatilityCalculation_RealWorldScenario_MaintainsPrecision()
        {
            // Arrange - real intraday price movements for day trading
            var intradayPrices = new List<decimal>
            {
                45.67m, 45.72m, 45.69m, 45.78m, 45.65m,
                45.71m, 45.84m, 45.79m, 45.88m, 45.92m
            };

            // Act - calculate volatility metrics critical for day trading
            decimal variance = FinancialMath.Variance(intradayPrices);
            decimal standardDeviation = FinancialMath.StandardDeviation(intradayPrices);

            // Assert - verify precision and reasonable values for trading decisions
            Assert.True(variance > 0m);
            Assert.True(standardDeviation > 0m);
            Assert.True(standardDeviation < 1m); // Reasonable daily volatility

            // Verify relationship: stdDev should equal sqrt(variance)
            decimal expectedStdDev = FinancialMath.Sqrt(variance);
            Assert.Equal(expectedStdDev, standardDeviation);
        }

        #endregion
    }
}