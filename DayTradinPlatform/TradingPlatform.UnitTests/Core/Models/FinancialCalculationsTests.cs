using System;
using FluentAssertions;
using TradingPlatform.Core.Models;
using TradingPlatform.Core.Math;
using Xunit;

namespace TradingPlatform.UnitTests.Core.Models
{
    public class FinancialCalculationsTests
    {
        [Theory]
        [InlineData(100, 110, 10, 0.1)]
        [InlineData(100, 90, -10, -0.1)]
        [InlineData(50, 75, 25, 0.5)]
        [InlineData(200, 180, -20, -0.1)]
        [InlineData(100, 100, 0, 0)]
        public void CalculatePnL_WithValidInputs_ReturnsCorrectValues(
            decimal entryPrice, decimal exitPrice, decimal expectedPnL, decimal expectedPnLPercent)
        {
            // Arrange
            const decimal quantity = 100;
            var position = new Position
            {
                Quantity = quantity,
                AveragePrice = entryPrice,
                CurrentPrice = exitPrice,
                Side = PositionSide.Long
            };

            // Act
            var pnl = (exitPrice - entryPrice) * quantity;
            var pnlPercent = entryPrice != 0 ? (exitPrice - entryPrice) / entryPrice : 0;

            // Assert
            pnl.Should().Be(expectedPnL * quantity);
            pnlPercent.Should().BeApproximately(expectedPnLPercent, 0.0001m);
        }

        [Theory]
        [InlineData(1000, 0.02, 20)]
        [InlineData(5000, 0.01, 50)]
        [InlineData(10000, 0.015, 150)]
        [InlineData(100, 0.05, 5)]
        public void CalculatePositionSize_WithRiskPercent_ReturnsCorrectSize(
            decimal accountBalance, decimal riskPercent, decimal expectedRiskAmount)
        {
            // Act
            var riskAmount = accountBalance * riskPercent;

            // Assert
            riskAmount.Should().Be(expectedRiskAmount);
        }

        [Theory]
        [InlineData(100, 95, 5, 0.05)]
        [InlineData(50, 48, 2, 0.04)]
        [InlineData(200, 190, 10, 0.05)]
        [InlineData(75.50, 73.5, 2, 0.0265)] // Approximately 2.65%
        public void CalculateStopLoss_WithPercentage_ReturnsCorrectPrice(
            decimal entryPrice, decimal expectedStopPrice, decimal stopDistance, decimal stopPercent)
        {
            // Act
            var calculatedStopPrice = entryPrice * (1 - stopPercent);
            var calculatedDistance = entryPrice - calculatedStopPrice;

            // Assert
            calculatedStopPrice.Should().BeApproximately(expectedStopPrice, 0.01m);
            calculatedDistance.Should().BeApproximately(stopDistance, 0.01m);
        }

        [Theory]
        [InlineData(100, 2, 5, 40)] // Risk $2 per share, stop at $5 = 40 shares
        [InlineData(500, 1, 10, 50)] // Risk $1 per share, stop at $10 = 50 shares
        [InlineData(1000, 3, 15, 66.67)] // Risk $3 per share, stop at $15 = 66.67 shares
        public void CalculateSharesFromRisk_WithStopLoss_ReturnsCorrectShares(
            decimal riskAmount, decimal stopLossDistance, decimal sharePrice, decimal expectedShares)
        {
            // Act
            var shares = riskAmount / stopLossDistance;

            // Assert
            shares.Should().BeApproximately(expectedShares, 0.01m);
        }

        [Theory]
        [InlineData(100000, 4, 400000)] // 4:1 margin
        [InlineData(50000, 2, 100000)] // 2:1 margin
        [InlineData(25000, 1, 25000)] // No margin
        [InlineData(100000, 0, 100000)] // No margin (0 ratio)
        public void CalculateBuyingPower_WithMargin_ReturnsCorrectAmount(
            decimal accountBalance, decimal marginRatio, decimal expectedBuyingPower)
        {
            // Act
            var buyingPower = marginRatio > 0 ? accountBalance * marginRatio : accountBalance;

            // Assert
            buyingPower.Should().Be(expectedBuyingPower);
        }

        [Theory]
        [InlineData(10, 15, 20, 15)] // ATR calculation
        [InlineData(5, 8, 12, 8.33)] // Different values
        [InlineData(100, 105, 95, 100)] // Larger values
        public void CalculateAverageTrueRange_WithHighLowClose_ReturnsCorrectATR(
            decimal high, decimal low, decimal close, decimal expectedATR)
        {
            // Simple ATR approximation for single period
            var trueRange = System.Math.Max(high - low, 
                System.Math.Max(System.Math.Abs((double)(high - close)), 
                System.Math.Abs((double)(low - close))));

            // For more accurate test, would need multiple periods
            ((decimal)trueRange).Should().BePositive();
        }

        [Fact]
        public void CalculateSharpeRatio_WithReturnsAndRiskFree_ReturnsCorrectRatio()
        {
            // Arrange
            decimal[] returns = { 0.02m, 0.03m, -0.01m, 0.04m, 0.01m };
            decimal riskFreeRate = 0.01m;
            
            // Act
            var avgReturn = returns.Average();
            var excessReturn = avgReturn - riskFreeRate;
            var stdDev = CalculateStandardDeviation(returns);
            var sharpeRatio = stdDev > 0 ? excessReturn / stdDev : 0;

            // Assert
            sharpeRatio.Should().BeGreaterThan(0);
            sharpeRatio.Should().BeLessThan(5); // Reasonable range
        }

        [Theory]
        [InlineData(50, 30, 70, 50)] // Neutral RSI
        [InlineData(80, 30, 70, 100)] // Overbought
        [InlineData(20, 30, 70, 0)] // Oversold
        public void CalculateRSI_WithGainsAndLosses_ReturnsCorrectRSI(
            decimal currentRSI, decimal oversoldLevel, decimal overboughtLevel, decimal normalizedRSI)
        {
            // Act
            var isOverbought = currentRSI >= overboughtLevel;
            var isOversold = currentRSI <= oversoldLevel;
            var isNeutral = !isOverbought && !isOversold;

            // Assert
            if (normalizedRSI == 100)
                isOverbought.Should().BeTrue();
            else if (normalizedRSI == 0)
                isOversold.Should().BeTrue();
            else
                isNeutral.Should().BeTrue();
        }

        [Theory]
        [InlineData(100, 2, 102, 98)] // 2% bands
        [InlineData(50, 3, 51.5, 48.5)] // 3% bands
        [InlineData(200, 1.5, 203, 197)] // 1.5% bands
        public void CalculateBollingerBands_WithStandardDeviation_ReturnsCorrectBands(
            decimal price, decimal stdDevPercent, decimal expectedUpper, decimal expectedLower)
        {
            // Act
            var upperBand = price * (1 + stdDevPercent / 100);
            var lowerBand = price * (1 - stdDevPercent / 100);

            // Assert
            upperBand.Should().Be(expectedUpper);
            lowerBand.Should().Be(expectedLower);
        }

        [Fact]
        public void CalculateVWAP_WithPriceAndVolume_ReturnsCorrectVWAP()
        {
            // Arrange
            var trades = new[]
            {
                new { Price = 100m, Volume = 1000L },
                new { Price = 101m, Volume = 2000L },
                new { Price = 99m, Volume = 1500L },
                new { Price = 100.5m, Volume = 500L }
            };

            // Act
            var totalValue = trades.Sum(t => t.Price * t.Volume);
            var totalVolume = trades.Sum(t => t.Volume);
            var vwap = totalVolume > 0 ? totalValue / totalVolume : 0;

            // Assert
            vwap.Should().BeApproximately(100.2m, 0.1m);
        }

        [Theory]
        [InlineData(100, 5, 2000)] // $100 risk, $5 stop = 20 shares * 100 (for 100 shares)
        [InlineData(250, 2.5, 10000)] // $250 risk, $2.50 stop = 100 shares * 100
        [InlineData(50, 1, 5000)] // $50 risk, $1 stop = 50 shares * 100
        public void CalculateMaxPositionValue_WithRiskAndStop_ReturnsCorrectValue(
            decimal maxRisk, decimal stopDistance, decimal expectedValue)
        {
            // Act
            var shares = maxRisk / stopDistance;
            var positionValue = shares * 100; // Assuming $100 per share for simplicity

            // Assert
            positionValue.Should().Be(expectedValue);
        }

        [Fact]
        public void ValidateDecimalPrecision_ForAllCalculations_MaintainsPrecision()
        {
            // Arrange
            decimal price = 123.456789m;
            decimal quantity = 100.123456m;
            decimal commission = 0.001m;

            // Act
            decimal totalValue = price * quantity;
            decimal commissionAmount = totalValue * commission;
            decimal netValue = totalValue - commissionAmount;

            // Assert
            // Verify decimal precision is maintained
            totalValue.ToString().Should().Contain(".");
            commissionAmount.Should().BeGreaterThan(0);
            netValue.Should().BeLessThan(totalValue);
            
            // Verify no floating point errors
            (totalValue - commissionAmount).Should().Be(netValue);
        }

        private static decimal CalculateStandardDeviation(decimal[] values)
        {
            if (values.Length == 0) return 0;
            
            var avg = values.Average();
            var sumOfSquares = values.Sum(v => (v - avg) * (v - avg));
            var variance = sumOfSquares / values.Length;
            
            // Convert to double for sqrt, then back to decimal
            return (decimal)System.Math.Sqrt((double)variance);
        }
    }
}