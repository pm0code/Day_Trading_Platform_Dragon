using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using TradingPlatform.Core.Models;
using TradingPlatform.GoldenRules.Engine;
using TradingPlatform.GoldenRules.Models;
using TradingPlatform.TimeSeries.Interfaces;
using TradingPlatform.UnitTests.Framework;
using TradingPlatform.UnitTests.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.UnitTests.GoldenRules.Engine
{
    public class CanonicalGoldenRulesEngineTests : CanonicalServiceTestBase<CanonicalGoldenRulesEngine>
    {
        private readonly Mock<ITimeSeriesService> _mockTimeSeriesService;
        private readonly IOptions<GoldenRulesEngineConfig> _config;

        public CanonicalGoldenRulesEngineTests(ITestOutputHelper output) : base(output)
        {
            _mockTimeSeriesService = new Mock<ITimeSeriesService>();
            _mockTimeSeriesService.Setup(x => x.WritePointAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult.Success());

            _config = Options.Create(new GoldenRulesEngineConfig
            {
                Enabled = true,
                StrictMode = true,
                EnableRealTimeAlerts = true,
                MinimumComplianceScore = 0.8m,
                RuleConfigs = new List<GoldenRuleConfiguration>()
            });
        }

        protected override CanonicalGoldenRulesEngine CreateService()
        {
            return new CanonicalGoldenRulesEngine(MockLogger.Object, _mockTimeSeriesService.Object, _config);
        }

        [Fact]
        public async Task EvaluateTradeAsync_CompliantTrade_PassesAllRules()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var positionContext = CreateCompliantPositionContext();
            var marketConditions = CreateFavorableMarketConditions();

            // Act
            var result = await Service.EvaluateTradeAsync(
                "AAPL",
                OrderType.Market,
                OrderSide.Buy,
                100,
                150m,
                positionContext,
                marketConditions,
                TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var assessment = result.Value;
            assessment.OverallCompliance.Should().BeTrue();
            assessment.ComplianceScore.Should().BeGreaterOrEqualTo(0.8m);
            assessment.BlockingViolations.Should().Be(0);
            assessment.Recommendation.Should().Contain("PROCEED");
        }

        [Fact]
        public async Task EvaluateTradeAsync_ExceedsRiskLimit_ViolatesRule1()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var positionContext = new PositionContext
            {
                Symbol = "TSLA",
                AccountBalance = 10000m, // Small account
                BuyingPower = 40000m,
                DayTradeCount = 0
            };

            var marketConditions = CreateFavorableMarketConditions();

            // Act - Try to buy $5000 worth (50% of account!)
            var result = await Service.EvaluateTradeAsync(
                "TSLA",
                OrderType.Market,
                OrderSide.Buy,
                25, // 25 shares at $200 = $5000
                200m,
                positionContext,
                marketConditions,
                TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var assessment = result.Value;
            assessment.OverallCompliance.Should().BeFalse();
            assessment.RuleResults.Should().Contain(r => 
                r.RuleNumber == 1 && !r.IsCompliant);
            assessment.Recommendation.Should().Contain("DO NOT TRADE");
        }

        [Fact]
        public async Task EvaluateTradeAsync_ExceedsDailyLoss_ViolatesRule2()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var positionContext = new PositionContext
            {
                Symbol = "AAPL",
                AccountBalance = 50000m,
                BuyingPower = 200000m,
                DailyPnL = -3500m, // Already down 7%!
                DayTradeCount = 5
            };

            var marketConditions = CreateFavorableMarketConditions();

            // Act
            var result = await Service.EvaluateTradeAsync(
                "AAPL",
                OrderType.Market,
                OrderSide.Buy,
                100,
                150m,
                positionContext,
                marketConditions,
                TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var assessment = result.Value;
            assessment.OverallCompliance.Should().BeFalse();
            assessment.RuleResults.Should().Contain(r => 
                r.RuleNumber == 2 && !r.IsCompliant);
            assessment.BlockingViolations.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task EvaluateTradeAsync_NoExitStrategy_ViolatesRule3()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var positionContext = CreateCompliantPositionContext();
            positionContext.StopLoss = null; // No stop loss set
            positionContext.TakeProfit = null; // No take profit set

            var marketConditions = CreateFavorableMarketConditions();

            // Act
            var result = await Service.EvaluateTradeAsync(
                "GOOGL",
                OrderType.Market,
                OrderSide.Buy,
                10,
                2800m,
                positionContext,
                marketConditions,
                TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var assessment = result.Value;
            var rule3Result = assessment.RuleResults.First(r => r.RuleNumber == 3);
            rule3Result.IsCompliant.Should().BeFalse();
            rule3Result.Message.Should().Contain("exit strategy");
        }

        [Fact]
        public async Task EvaluateTradeAsync_MultipleViolations_AggregatesCorrectly()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var positionContext = new PositionContext
            {
                Symbol = "MEME",
                AccountBalance = 25000m,
                BuyingPower = 100000m,
                DayTradeCount = 15, // Too many trades (Rule 9)
                DailyPnL = -2000m, // Down 8% (Rule 2)
                ConsecutiveLosses = 5, // Losing streak (Rule 6)
                OpenPositions = 8, // Too many positions
                CurrentDrawdown = 0.25m // 25% drawdown
            };

            var marketConditions = new MarketConditions
            {
                Symbol = "MEME",
                Price = 50m,
                Volume = 500000, // Low volume (Rule 7)
                Volatility = 0.15m, // Very high volatility
                Session = MarketSession.PreMarket, // Wrong session
                Trend = TrendDirection.Down
            };

            // Act
            var result = await Service.EvaluateTradeAsync(
                "MEME",
                OrderType.Market,
                OrderSide.Buy,
                1000, // Large position
                50m,
                positionContext,
                marketConditions,
                TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var assessment = result.Value;
            assessment.OverallCompliance.Should().BeFalse();
            assessment.ComplianceScore.Should().BeLessThan(0.5m);
            assessment.CriticalViolations.Should().BeGreaterThan(2);
            assessment.RuleResults.Count(r => !r.IsCompliant).Should().BeGreaterThan(3);
        }

        [Fact]
        public async Task ValidateTradeAsync_SimplifiedValidation_Works()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            // Act
            var result = await Service.ValidateTradeAsync(
                "AAPL",
                OrderType.Market,
                OrderSide.Buy,
                50,
                150m,
                TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            result.Value.Should().BeTrue(); // Simple validation with default context
        }

        [Theory]
        [InlineData(1, true)]   // Rule 1: 2% risk
        [InlineData(2, true)]   // Rule 2: 6% daily loss
        [InlineData(3, true)]   // Rule 3: Exit strategy
        [InlineData(4, true)]   // Rule 4: Cut losses
        [InlineData(5, true)]   // Rule 5: Let winners run
        [InlineData(6, true)]   // Rule 6: No revenge trading
        [InlineData(7, true)]   // Rule 7: Liquid stocks
        [InlineData(8, true)]   // Rule 8: Pre-market prep
        [InlineData(9, true)]   // Rule 9: No overtrading
        [InlineData(10, true)]  // Rule 10: Review trades
        [InlineData(11, true)]  // Rule 11: Capital preservation
        [InlineData(12, true)]  // Rule 12: Risk/reward ratio
        public async Task EvaluateTradeAsync_IndividualRules_EvaluateCorrectly(int ruleNumber, bool shouldExist)
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var positionContext = CreateCompliantPositionContext();
            var marketConditions = CreateFavorableMarketConditions();

            // Act
            var result = await Service.EvaluateTradeAsync(
                "TEST",
                OrderType.Market,
                OrderSide.Buy,
                10,
                100m,
                positionContext,
                marketConditions,
                TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var assessment = result.Value;
            
            if (shouldExist)
            {
                assessment.RuleResults.Should().Contain(r => r.RuleNumber == ruleNumber);
            }
        }

        [Fact]
        public async Task GetSessionReportAsync_ReturnsComprehensiveReport()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            // Perform some evaluations
            var context = CreateCompliantPositionContext();
            var conditions = CreateFavorableMarketConditions();

            for (int i = 0; i < 5; i++)
            {
                await Service.EvaluateTradeAsync(
                    $"SYM{i}",
                    OrderType.Market,
                    OrderSide.Buy,
                    100,
                    100m + i * 10,
                    context,
                    conditions,
                    TestCts.Token);
            }

            // Act
            var result = await Service.GetSessionReportAsync(TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var report = result.Value;
            report.TotalEvaluations.Should().Be(5);
            report.ComplianceRate.Should().BeGreaterThan(0);
            report.RuleViolationCounts.Should().NotBeNull();
            report.CriticalViolations.Should().BeGreaterOrEqualTo(0);
            report.Recommendations.Should().NotBeEmpty();
        }

        [Fact]
        public async Task UpdateRuleConfigAsync_ModifiesRuleBehavior()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var newConfig = new GoldenRuleConfiguration
            {
                RuleNumber = 1,
                Enabled = false, // Disable rule 1
                Threshold = 0.05m, // Change from 2% to 5%
                Severity = RuleSeverity.Warning
            };

            // Act
            var updateResult = await Service.UpdateRuleConfigurationAsync(1, newConfig, TestCts.Token);

            // Assert
            updateResult.Should().BeSuccess();
            updateResult.Value.Should().BeTrue();

            // Verify rule behavior changed
            var context = CreateCompliantPositionContext();
            var conditions = CreateFavorableMarketConditions();

            var evalResult = await Service.EvaluateTradeAsync(
                "TEST",
                OrderType.Market,
                OrderSide.Buy,
                1000, // Large position that would normally violate 2% rule
                100m,
                context,
                conditions,
                TestCts.Token);

            evalResult.Should().BeSuccess();
            var assessment = evalResult.Value;
            
            // Rule 1 should be disabled or have different threshold
            var rule1Result = assessment.RuleResults.FirstOrDefault(r => r.RuleNumber == 1);
            if (rule1Result != null)
            {
                rule1Result.Severity.Should().Be(RuleSeverity.Warning);
            }
        }

        [Fact]
        public async Task PerformanceMetrics_TracksEvaluations()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            // Perform evaluations with different outcomes
            var goodContext = CreateCompliantPositionContext();
            var badContext = CreateCompliantPositionContext();
            badContext.DailyPnL = -5000m; // Big loss

            var conditions = CreateFavorableMarketConditions();

            await Service.EvaluateTradeAsync("GOOD1", OrderType.Market, OrderSide.Buy, 
                100, 150m, goodContext, conditions, TestCts.Token);
            
            await Service.EvaluateTradeAsync("BAD1", OrderType.Market, OrderSide.Buy, 
                1000, 150m, badContext, conditions, TestCts.Token);

            // Act
            var metricsResult = await Service.GetPerformanceMetricsAsync(TestCts.Token);

            // Assert
            metricsResult.Should().BeSuccess();
            var metrics = metricsResult.Value;
            metrics.Should().ContainKey("TotalEvaluations");
            metrics.Should().ContainKey("ComplianceRate");
            metrics.Should().ContainKey("AverageComplianceScore");
            
            Convert.ToInt32(metrics["TotalEvaluations"]).Should().Be(2);
        }

        private PositionContext CreateCompliantPositionContext()
        {
            return new PositionContext
            {
                Symbol = "AAPL",
                AccountBalance = 100000m,
                BuyingPower = 400000m,
                DayTradeCount = 2,
                DailyPnL = 500m,
                OpenPositions = 3,
                MaxPositionsAllowed = 10,
                TotalRiskExposure = 2000m,
                AverageWinRate = 0.65m,
                CurrentDrawdown = 0.02m,
                ConsecutiveLosses = 0,
                StopLoss = 148m,
                TakeProfit = 155m
            };
        }

        private MarketConditions CreateFavorableMarketConditions()
        {
            return new MarketConditions
            {
                Symbol = "AAPL",
                Price = 150m,
                Bid = 149.95m,
                Ask = 150.05m,
                Volume = 50000000,
                AverageVolume = 45000000,
                Volatility = 0.02m,
                Trend = TrendDirection.Uptrend,
                RelativeVolume = 1.1m,
                Session = MarketSession.RegularHours,
                HasNewsCatalyst = false
            };
        }
    }
}