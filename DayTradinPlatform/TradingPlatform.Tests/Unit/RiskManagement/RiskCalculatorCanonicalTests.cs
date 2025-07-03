using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.RiskManagement.Services;
using TradingPlatform.RiskManagement.Models;
using TradingPlatform.Tests.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using Moq;

namespace TradingPlatform.Tests.Unit.RiskManagement
{
    /// <summary>
    /// Comprehensive unit tests for RiskCalculatorCanonical
    /// Tests VaR, CVaR, Sharpe Ratio, Beta, and risk assessment calculations
    /// </summary>
    public class RiskCalculatorCanonicalTests : CanonicalTestBase<RiskCalculatorCanonical>
    {
        private const decimal PRECISION_TOLERANCE = 0.0001m;
        
        public RiskCalculatorCanonicalTests(ITestOutputHelper output) : base(output)
        {
        }
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ITradingLogger>(MockLogger.Object);
            services.AddSingleton<IServiceProvider>(ServiceProvider);
        }
        
        protected override RiskCalculatorCanonical CreateSystemUnderTest()
        {
            return new RiskCalculatorCanonical(ServiceProvider);
        }
        
        #region VaR Calculation Tests
        
        [Fact]
        public async Task CalculateVaRAsync_NormalDistribution_Returns95PercentileValue()
        {
            // Arrange - Returns representing daily percentage losses/gains
            var returns = GenerateNormalReturns(1000, 0, 0.02m); // 0% mean, 2% std dev
            
            // Act
            var result = await SystemUnderTest.CalculateVaRAsync(returns, 0.95m);
            
            // Assert
            // For normal distribution with 2% std dev, 95% VaR should be ~1.645 * 2% = 3.29%
            Assert.True(result > 0.025m && result < 0.04m);
            Output.WriteLine($"95% VaR: {result:P2}");
            
            // Verify logging
            VerifyLoggerCalled("info", Times.AtLeastOnce());
        }
        
        [Fact]
        public async Task CalculateVaRAsync_HistoricalMethod_HandlesSmallDataset()
        {
            // Arrange - Small dataset with known values
            var returns = new List<decimal> 
            { 
                -0.05m, -0.03m, -0.02m, -0.01m, 0m, 
                0.01m, 0.02m, 0.03m, 0.04m, 0.05m 
            };
            
            // Act
            var result = await SystemUnderTest.CalculateVaRAsync(returns, 0.90m);
            
            // Assert
            // At 90% confidence, we look at the 10th percentile (worst 10%)
            // With 10 values, that's position 1, which is -0.05
            Assert.Equal(0.05m, result); // VaR is reported as positive
        }
        
        [Fact]
        public async Task CalculateVaRAsync_EmptyReturns_ReturnsZero()
        {
            // Act
            var result = await SystemUnderTest.CalculateVaRAsync(new List<decimal>(), 0.95m);
            
            // Assert
            Assert.Equal(0m, result);
        }
        
        [Fact]
        public async Task CalculateVaRAsync_ExtremeRisk_LogsWarning()
        {
            // Arrange - Extreme losses
            var returns = new List<decimal> 
            { 
                -0.30m, -0.25m, -0.20m, -0.15m, -0.10m 
            };
            
            // Act
            var result = await SystemUnderTest.CalculateVaRAsync(returns, 0.95m);
            
            // Assert
            Assert.True(result > 0.25m); // Extreme risk threshold
            VerifyLoggerCalled("warning", Times.AtLeastOnce());
        }
        
        #endregion
        
        #region Expected Shortfall (CVaR) Tests
        
        [Fact]
        public async Task CalculateExpectedShortfallAsync_AlwaysGreaterThanVaR()
        {
            // Arrange
            var returns = GenerateNormalReturns(1000, 0, 0.02m);
            
            // Act
            var var = await SystemUnderTest.CalculateVaRAsync(returns, 0.95m);
            var es = await SystemUnderTest.CalculateExpectedShortfallAsync(returns, 0.95m);
            
            // Assert
            Assert.True(es >= var, $"Expected Shortfall ({es}) should be >= VaR ({var})");
            Output.WriteLine($"VaR: {var:P2}, ES: {es:P2}, ES/VaR Ratio: {es/var:F2}");
        }
        
        [Fact]
        public async Task CalculateExpectedShortfallAsync_TailRisk_CalculatesCorrectly()
        {
            // Arrange - Returns with fat tail
            var returns = new List<decimal>();
            // Normal returns
            returns.AddRange(GenerateNormalReturns(900, 0, 0.01m));
            // Tail events
            returns.AddRange(new[] { -0.10m, -0.15m, -0.20m, -0.25m, -0.30m });
            
            // Act
            var es = await SystemUnderTest.CalculateExpectedShortfallAsync(returns, 0.95m);
            
            // Assert
            // ES should capture the average of the worst 5% (50 values)
            Assert.True(es > 0.05m); // Should be significantly higher due to tail events
        }
        
        #endregion
        
        #region Max Drawdown Tests
        
        [Fact]
        public async Task CalculateMaxDrawdownAsync_CumulativeValues_FindsLargestDrop()
        {
            // Arrange - Portfolio values with clear drawdown
            var portfolioValues = new List<decimal> 
            { 
                100000, 110000, 120000, 115000, 105000, 
                95000, 100000, 110000, 105000, 115000 
            };
            
            // Act
            var result = await SystemUnderTest.CalculateMaxDrawdownAsync(portfolioValues);
            
            // Assert
            // Max drawdown from 120,000 to 95,000 = 25,000 / 120,000 = 20.83%
            AssertFinancialPrecision(0.2083m, result, 4);
        }
        
        [Fact]
        public async Task CalculateMaxDrawdownAsync_NoDrawdown_ReturnsZero()
        {
            // Arrange - Monotonically increasing values
            var portfolioValues = Enumerable.Range(1, 10).Select(i => i * 10000m).ToList();
            
            // Act
            var result = await SystemUnderTest.CalculateMaxDrawdownAsync(portfolioValues);
            
            // Assert
            Assert.Equal(0m, result);
        }
        
        [Fact]
        public async Task CalculateMaxDrawdownAsync_RecoveryAfterDrawdown_StillReportsMaximum()
        {
            // Arrange - Values that recover after drawdown
            var portfolioValues = new List<decimal> 
            { 
                100000, 120000, 80000, 90000, 130000, 140000 
            };
            
            // Act
            var result = await SystemUnderTest.CalculateMaxDrawdownAsync(portfolioValues);
            
            // Assert
            // Max drawdown from 120,000 to 80,000 = 40,000 / 120,000 = 33.33%
            AssertFinancialPrecision(0.3333m, result, 4);
        }
        
        #endregion
        
        #region Sharpe Ratio Tests
        
        [Fact]
        public async Task CalculateSharpeRatioAsync_PositiveReturns_ReturnsPositiveRatio()
        {
            // Arrange - Consistent positive returns above risk-free rate
            var returns = GenerateNormalReturns(252, 0.0005m, 0.01m); // 0.05% daily return, 1% volatility
            decimal annualRiskFreeRate = 0.02m; // 2% annual
            
            // Act
            var result = await SystemUnderTest.CalculateSharpeRatioAsync(returns, annualRiskFreeRate);
            
            // Assert
            Assert.True(result > 0, "Sharpe ratio should be positive for returns above risk-free rate");
            Assert.True(result < 5, "Sharpe ratio should be reasonable");
            Output.WriteLine($"Sharpe Ratio: {result:F2}");
        }
        
        [Fact]
        public async Task CalculateSharpeRatioAsync_NegativeReturns_ReturnsNegativeRatio()
        {
            // Arrange - Consistent negative returns
            var returns = GenerateNormalReturns(252, -0.0005m, 0.01m);
            
            // Act
            var result = await SystemUnderTest.CalculateSharpeRatioAsync(returns, 0.02m);
            
            // Assert
            Assert.True(result < 0, "Sharpe ratio should be negative for returns below risk-free rate");
        }
        
        [Fact]
        public async Task CalculateSharpeRatioAsync_ZeroVolatility_HandlesGracefully()
        {
            // Arrange - Constant returns (no volatility)
            var returns = Enumerable.Repeat(0.001m, 252).ToList();
            
            // Act
            var result = await SystemUnderTest.CalculateSharpeRatioAsync(returns, 0.02m);
            
            // Assert
            // Should handle division by zero case
            Assert.True(!double.IsNaN((double)result) && !double.IsInfinity((double)result));
        }
        
        #endregion
        
        #region Beta Calculation Tests
        
        [Fact]
        public async Task CalculateBetaAsync_MarketNeutral_ReturnsOne()
        {
            // Arrange - Asset returns perfectly match market
            var marketReturns = GenerateNormalReturns(100, 0.0003m, 0.015m);
            var assetReturns = marketReturns.ToList(); // Same as market
            
            // Act
            var result = await SystemUnderTest.CalculateBetaAsync(assetReturns, marketReturns);
            
            // Assert
            AssertFinancialPrecision(1m, result, 2);
        }
        
        [Fact]
        public async Task CalculateBetaAsync_HighBetaStock_ReturnsGreaterThanOne()
        {
            // Arrange - Asset is 1.5x more volatile than market
            var marketReturns = GenerateNormalReturns(100, 0m, 0.01m);
            var assetReturns = marketReturns.Select(r => r * 1.5m).ToList();
            
            // Act
            var result = await SystemUnderTest.CalculateBetaAsync(assetReturns, marketReturns);
            
            // Assert
            AssertFinancialPrecision(1.5m, result, 2);
        }
        
        [Fact]
        public async Task CalculateBetaAsync_DefensiveStock_ReturnsLessThanOne()
        {
            // Arrange - Asset is 0.5x less volatile than market
            var marketReturns = GenerateNormalReturns(100, 0m, 0.01m);
            var assetReturns = marketReturns.Select(r => r * 0.5m).ToList();
            
            // Act
            var result = await SystemUnderTest.CalculateBetaAsync(assetReturns, marketReturns);
            
            // Assert
            AssertFinancialPrecision(0.5m, result, 2);
        }
        
        [Fact]
        public async Task CalculateBetaAsync_InverseCorrelation_ReturnsNegative()
        {
            // Arrange - Asset moves opposite to market
            var marketReturns = GenerateNormalReturns(100, 0m, 0.01m);
            var assetReturns = marketReturns.Select(r => -r).ToList();
            
            // Act
            var result = await SystemUnderTest.CalculateBetaAsync(assetReturns, marketReturns);
            
            // Assert
            AssertFinancialPrecision(-1m, result, 2);
        }
        
        [Fact]
        public async Task CalculateBetaAsync_MismatchedData_ReturnsFailure()
        {
            // Arrange
            var marketReturns = new List<decimal> { 0.01m, 0.02m, -0.01m };
            var assetReturns = new List<decimal> { 0.01m, 0.02m }; // Different length
            
            // Act
            var result = await SystemUnderTest.CalculateBetaAsync(assetReturns, marketReturns);
            
            // Assert
            Assert.Equal(0m, result); // Should handle error gracefully
        }
        
        #endregion
        
        #region Risk Assessment Integration Tests
        
        [Fact]
        public async Task EvaluateRiskAsync_LowRiskPortfolio_PassesAssessment()
        {
            // Arrange
            var context = new RiskCalculationContext
            {
                Returns = GenerateNormalReturns(252, 0.0003m, 0.005m), // Low volatility
                PortfolioValues = GenerateGrowingPortfolio(252, 100000, 0.0003m),
                ConfidenceLevel = 0.95m,
                RiskFreeRate = 0.02m
            };
            
            // Act
            var assessment = await SystemUnderTest.EvaluateRiskAsync(context);
            
            // Assert
            Assert.True(assessment.IsAcceptable);
            Assert.True(assessment.RiskScore < 0.5m);
            Output.WriteLine($"Risk Score: {assessment.RiskScore:F2}");
            Output.WriteLine($"Assessment: {assessment.Reason}");
        }
        
        [Fact]
        public async Task EvaluateRiskAsync_HighRiskPortfolio_FailsAssessment()
        {
            // Arrange
            var context = new RiskCalculationContext
            {
                Returns = GenerateNormalReturns(252, -0.001m, 0.05m), // High volatility, negative drift
                PortfolioValues = GenerateDecliningPortfolio(252, 100000, -0.001m),
                ConfidenceLevel = 0.95m,
                RiskFreeRate = 0.02m
            };
            
            // Act
            var assessment = await SystemUnderTest.EvaluateRiskAsync(context);
            
            // Assert
            Assert.False(assessment.IsAcceptable);
            Assert.True(assessment.RiskScore > 0.7m);
            Assert.Contains("High VaR", assessment.Reason);
        }
        
        [Fact]
        public async Task EvaluateRiskAsync_AllMetricsCalculated()
        {
            // Arrange
            var context = new RiskCalculationContext
            {
                Returns = GenerateNormalReturns(252, 0.0002m, 0.015m),
                PortfolioValues = GenerateGrowingPortfolio(252, 100000, 0.0002m),
                ConfidenceLevel = 0.95m,
                RiskFreeRate = 0.02m
            };
            
            // Act
            var assessment = await SystemUnderTest.EvaluateRiskAsync(context);
            
            // Assert
            Assert.Contains("VaR", assessment.Metrics.Keys);
            Assert.Contains("ExpectedShortfall", assessment.Metrics.Keys);
            Assert.Contains("MaxDrawdown", assessment.Metrics.Keys);
            Assert.Contains("SharpeRatio", assessment.Metrics.Keys);
            
            // All metrics should be calculated
            Assert.True(assessment.Metrics["VaR"] > 0);
            Assert.True(assessment.Metrics["ExpectedShortfall"] >= assessment.Metrics["VaR"]);
            
            Output.WriteLine("Risk Metrics:");
            foreach (var metric in assessment.Metrics)
            {
                Output.WriteLine($"  {metric.Key}: {metric.Value:F4}");
            }
        }
        
        #endregion
        
        #region Kelly Criterion Tests
        
        [Fact]
        public async Task CalculateKellyCriterionAsync_ProfitableStrategy_ReturnsPositiveFraction()
        {
            // Arrange
            decimal winRate = 0.60m; // 60% win rate
            decimal avgWin = 150m;
            decimal avgLoss = 100m;
            
            // Act
            var result = await SystemUnderTest.CalculateKellyCriterionAsync(winRate, avgWin, avgLoss);
            
            // Assert
            Assert.True(result > 0 && result <= 0.25m); // Should be positive but capped at 25%
            Output.WriteLine($"Kelly Fraction: {result:P2}");
        }
        
        [Fact]
        public async Task CalculateKellyCriterionAsync_UnprofitableStrategy_ReturnsZero()
        {
            // Arrange
            decimal winRate = 0.40m; // 40% win rate
            decimal avgWin = 100m;
            decimal avgLoss = 100m; // 1:1 risk/reward with <50% win rate
            
            // Act
            var result = await SystemUnderTest.CalculateKellyCriterionAsync(winRate, avgWin, avgLoss);
            
            // Assert
            Assert.Equal(0m, result); // Should not recommend any position size
        }
        
        #endregion
        
        #region Helper Methods
        
        private List<decimal> GenerateNormalReturns(int count, decimal mean, decimal stdDev)
        {
            var random = new Random(42); // Fixed seed for reproducibility
            var returns = new List<decimal>();
            
            for (int i = 0; i < count; i++)
            {
                // Box-Muller transform for normal distribution
                double u1 = 1.0 - random.NextDouble();
                double u2 = 1.0 - random.NextDouble();
                double normalValue = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                
                decimal returnValue = mean + stdDev * (decimal)normalValue;
                returns.Add(returnValue);
            }
            
            return returns;
        }
        
        private List<decimal> GenerateGrowingPortfolio(int days, decimal startValue, decimal dailyReturn)
        {
            var values = new List<decimal> { startValue };
            
            for (int i = 1; i < days; i++)
            {
                var newValue = values[i - 1] * (1 + dailyReturn + (decimal)(new Random(i).NextDouble() - 0.5) * 0.01m);
                values.Add(newValue);
            }
            
            return values;
        }
        
        private List<decimal> GenerateDecliningPortfolio(int days, decimal startValue, decimal dailyReturn)
        {
            return GenerateGrowingPortfolio(days, startValue, -Math.Abs(dailyReturn));
        }
        
        #endregion
    }
}