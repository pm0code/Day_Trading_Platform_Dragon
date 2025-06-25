using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using TradingPlatform.IntegrationTests.Fixtures;
using TradingPlatform.GoldenRules.Engine;
using TradingPlatform.GoldenRules.Interfaces;
using TradingPlatform.GoldenRules.Models;
using TradingPlatform.Core.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.TimeSeries.Interfaces;
using TradingPlatform.TimeSeries.Services;
using Moq;

namespace TradingPlatform.IntegrationTests.GoldenRules
{
    [Collection("Integration Tests")]
    public class GoldenRulesEngineIntegrationTests : IClassFixture<GoldenRulesTestFixture>
    {
        private readonly GoldenRulesTestFixture _fixture;
        private readonly IGoldenRulesEngine _goldenRulesEngine;

        public GoldenRulesEngineIntegrationTests(GoldenRulesTestFixture fixture)
        {
            _fixture = fixture;
            _goldenRulesEngine = _fixture.GetRequiredService<IGoldenRulesEngine>();
        }

        [Fact]
        public async Task EvaluateTrade_ValidTrade_ShouldPass()
        {
            // Arrange
            var positionContext = new PositionContext
            {
                Symbol = "AAPL",
                AccountBalance = 100000m,
                BuyingPower = 400000m,
                DayTradeCount = 2,
                DailyPnL = 500m,
                Quantity = 0, // No existing position
                EntryPrice = 0,
                CurrentPrice = 150m
            };

            var marketConditions = new MarketConditions
            {
                Symbol = "AAPL",
                Price = 150m,
                Bid = 149.95m,
                Ask = 150.05m,
                Volume = 50000000,
                DayHigh = 152m,
                DayLow = 148m,
                OpenPrice = 149m,
                PreviousClose = 148.50m,
                ATR = 2.5m,
                Volatility = 0.02m,
                Trend = TrendDirection.Uptrend,
                Momentum = 0.7m,
                RelativeVolume = 1.2m,
                Session = MarketSession.RegularHours,
                HasNewsCatalyst = false,
                TechnicalIndicators = new Dictionary<string, decimal>
                {
                    ["RSI"] = 55m,
                    ["MACD"] = 0.5m
                }
            };

            // Act
            var result = await _goldenRulesEngine.EvaluateTradeAsync(
                "AAPL",
                OrderType.Market,
                OrderSide.Buy,
                100, // 100 shares
                150m, // $150 per share
                positionContext,
                marketConditions);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var assessment = result.Value;
            assessment.Should().NotBeNull();
            assessment.OverallCompliance.Should().BeTrue();
            assessment.BlockingViolations.Should().Be(0);
            assessment.ConfidenceScore.Should().BeGreaterThan(0.5m);
        }

        [Fact]
        public async Task EvaluateTrade_ExceedsDailyLossLimit_ShouldBlock()
        {
            // Arrange
            var positionContext = new PositionContext
            {
                Symbol = "TSLA",
                AccountBalance = 100000m,
                BuyingPower = 400000m,
                DayTradeCount = 5,
                DailyPnL = -2100m, // Already exceeded 2% daily loss
                Quantity = 0,
                EntryPrice = 0,
                CurrentPrice = 250m
            };

            var marketConditions = CreateDefaultMarketConditions("TSLA", 250m);

            // Act
            var result = await _goldenRulesEngine.EvaluateTradeAsync(
                "TSLA",
                OrderType.Market,
                OrderSide.Buy,
                100,
                250m,
                positionContext,
                marketConditions);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var assessment = result.Value;
            assessment.OverallCompliance.Should().BeFalse();
            assessment.BlockingViolations.Should().BeGreaterThan(0);
            assessment.RuleResults.Should().Contain(r => r.RuleNumber == 8 && !r.IsPassing);
            assessment.Recommendation.Should().Contain("DO NOT TRADE");
        }

        [Fact]
        public async Task EvaluateTrade_PositionSizeTooLarge_ShouldBlock()
        {
            // Arrange
            var positionContext = new PositionContext
            {
                Symbol = "NVDA",
                AccountBalance = 50000m,
                BuyingPower = 200000m,
                DayTradeCount = 1,
                DailyPnL = 0m,
                Quantity = 0,
                EntryPrice = 0,
                CurrentPrice = 500m
            };

            var marketConditions = CreateDefaultMarketConditions("NVDA", 500m);

            // Act - Try to buy 100 shares at $500 = $50,000 (100% of account)
            var result = await _goldenRulesEngine.EvaluateTradeAsync(
                "NVDA",
                OrderType.Market,
                OrderSide.Buy,
                100,
                500m,
                positionContext,
                marketConditions);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var assessment = result.Value;
            assessment.OverallCompliance.Should().BeFalse();
            assessment.RuleResults.Should().Contain(r => r.RuleNumber == 7 && !r.IsPassing); // Position sizing rule
        }

        [Fact]
        public async Task ValidateTrade_QuickValidation_ShouldWork()
        {
            // Arrange
            var marketConditions = CreateDefaultMarketConditions("SPY", 450m);

            // Act
            var result = await _goldenRulesEngine.ValidateTradeAsync(
                "SPY",
                OrderType.Market,
                OrderSide.Buy,
                50,
                450m);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue(); // Should pass basic validation
        }

        [Fact]
        public async Task GetComplianceStatus_AfterMultipleTrades_ShouldTrackStats()
        {
            // Arrange & Act - Evaluate multiple trades
            var trades = new[]
            {
                ("AAPL", 100, 150m, true),
                ("MSFT", 50, 350m, true),
                ("TSLA", 200, 250m, false), // Should fail position size
            };

            foreach (var (symbol, quantity, price, _) in trades)
            {
                var context = CreatePositionContext(symbol);
                var conditions = CreateDefaultMarketConditions(symbol, price);
                
                await _goldenRulesEngine.EvaluateTradeAsync(
                    symbol,
                    OrderType.Market,
                    OrderSide.Buy,
                    quantity,
                    price,
                    context,
                    conditions);
            }

            // Get compliance status
            var statusResult = await _goldenRulesEngine.GetComplianceStatusAsync();

            // Assert
            statusResult.IsSuccess.Should().BeTrue();
            var status = statusResult.Value;
            status.Should().NotBeEmpty();
            
            // Check that evaluations were recorded
            var totalEvaluations = 0;
            foreach (var ruleStats in status.Values)
            {
                totalEvaluations += ruleStats.EvaluationCount;
                ruleStats.ComplianceRate.Should().BeGreaterOrEqualTo(0).And.BeLessThanOrEqualTo(1);
            }
            totalEvaluations.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetSessionViolations_AfterViolations_ShouldReturnViolations()
        {
            // Arrange - Create a position that violates multiple rules
            var positionContext = new PositionContext
            {
                Symbol = "MEME",
                AccountBalance = 25000m, // Small account
                BuyingPower = 100000m,
                DayTradeCount = 15, // Overtrading
                DailyPnL = -1000m, // Already down 4%
                Quantity = 0,
                EntryPrice = 0,
                CurrentPrice = 100m
            };

            var marketConditions = CreateDefaultMarketConditions("MEME", 100m);
            marketConditions.Volatility = 0.10m; // High volatility

            // Act - Evaluate trade that should violate rules
            await _goldenRulesEngine.EvaluateTradeAsync(
                "MEME",
                OrderType.Market,
                OrderSide.Buy,
                1000, // Way too many shares
                100m,
                positionContext,
                marketConditions);

            // Get violations
            var violationsResult = await _goldenRulesEngine.GetSessionViolationsAsync();

            // Assert
            violationsResult.IsSuccess.Should().BeTrue();
            var violations = violationsResult.Value;
            violations.Should().NotBeEmpty();
            violations.Should().Contain(v => v.Symbol == "MEME");
        }

        [Fact]
        public async Task GenerateSessionReport_ShouldProvideComprehensiveReport()
        {
            // Arrange - Perform some evaluations
            var symbols = new[] { "AAPL", "GOOGL", "AMZN" };
            
            foreach (var symbol in symbols)
            {
                var context = CreatePositionContext(symbol);
                var conditions = CreateDefaultMarketConditions(symbol, 100m + Random.Shared.Next(50, 500));
                
                await _goldenRulesEngine.EvaluateTradeAsync(
                    symbol,
                    OrderType.Market,
                    OrderSide.Buy,
                    Random.Shared.Next(10, 100),
                    conditions.Price,
                    context,
                    conditions);
            }

            // Act
            var reportResult = await _goldenRulesEngine.GenerateSessionReportAsync(
                DateTime.UtcNow.AddHours(-1),
                DateTime.UtcNow);

            // Assert
            reportResult.IsSuccess.Should().BeTrue();
            var report = reportResult.Value;
            report.Should().NotBeNull();
            report.TotalTradesEvaluated.Should().BeGreaterOrEqualTo(symbols.Length);
            report.OverallComplianceRate.Should().BeGreaterOrEqualTo(0).And.BeLessThanOrEqualTo(1);
            report.RuleStats.Should().NotBeEmpty();
        }

        [Fact]
        public async Task UpdateRuleConfiguration_DisableRule_ShouldNotEvaluate()
        {
            // Arrange
            var config = new GoldenRuleConfiguration
            {
                RuleNumber = 2, // Trading Discipline
                Enabled = false,
                Severity = RuleSeverity.Info,
                Parameters = new Dictionary<string, object>()
            };

            // Act - Disable rule
            var updateResult = await _goldenRulesEngine.UpdateRuleConfigurationAsync(2, config);
            updateResult.IsSuccess.Should().BeTrue();

            // Evaluate a trade
            var context = CreatePositionContext("TEST");
            var conditions = CreateDefaultMarketConditions("TEST", 50m);
            
            var evalResult = await _goldenRulesEngine.EvaluateTradeAsync(
                "TEST",
                OrderType.Market,
                OrderSide.Buy,
                100,
                50m,
                context,
                conditions);

            // Assert - Rule 2 should not be in results
            evalResult.IsSuccess.Should().BeTrue();
            var assessment = evalResult.Value;
            assessment.RuleResults.Should().NotContain(r => r.RuleNumber == 2);

            // Re-enable the rule
            config.Enabled = true;
            await _goldenRulesEngine.UpdateRuleConfigurationAsync(2, config);
        }

        [Fact]
        public async Task GetRecommendations_BasedOnConditions_ShouldProvideGuidance()
        {
            // Arrange
            var positionContext = new PositionContext
            {
                Symbol = "SPY",
                AccountBalance = 100000m,
                BuyingPower = 400000m,
                DayTradeCount = 8, // High activity
                DailyPnL = -500m, // Small loss
                Quantity = 100,
                EntryPrice = 440m,
                CurrentPrice = 445m,
                UnrealizedPnL = 500m // Small profit
            };

            var marketConditions = CreateDefaultMarketConditions("SPY", 445m);
            marketConditions.Volatility = 0.015m; // Normal volatility

            // Act
            var recommendationsResult = await _goldenRulesEngine.GetRecommendationsAsync(
                "SPY",
                positionContext,
                marketConditions);

            // Assert
            recommendationsResult.IsSuccess.Should().BeTrue();
            var recommendations = recommendationsResult.Value;
            recommendations.Should().NotBeEmpty();
            recommendations.Should().Contain(r => r.Contains("trade", StringComparison.OrdinalIgnoreCase));
        }

        // Helper methods
        private MarketConditions CreateDefaultMarketConditions(string symbol, decimal price)
        {
            return new MarketConditions
            {
                Symbol = symbol,
                Price = price,
                Bid = price - 0.05m,
                Ask = price + 0.05m,
                Volume = 10000000,
                DayHigh = price * 1.02m,
                DayLow = price * 0.98m,
                OpenPrice = price * 0.99m,
                PreviousClose = price * 0.995m,
                ATR = price * 0.02m,
                Volatility = 0.02m,
                Trend = TrendDirection.Sideways,
                Momentum = 0.5m,
                RelativeVolume = 1.0m,
                Session = MarketSession.RegularHours,
                HasNewsCatalyst = false,
                TechnicalIndicators = new Dictionary<string, decimal>
                {
                    ["RSI"] = 50m,
                    ["MACD"] = 0m,
                    ["MA20"] = price,
                    ["MA50"] = price * 0.98m
                }
            };
        }

        private PositionContext CreatePositionContext(string symbol)
        {
            return new PositionContext
            {
                Symbol = symbol,
                AccountBalance = 100000m,
                BuyingPower = 400000m,
                DayTradeCount = 0,
                DailyPnL = 0m,
                Quantity = 0,
                EntryPrice = 0,
                CurrentPrice = 100m,
                UnrealizedPnL = 0,
                RealizedPnL = 0,
                HoldingPeriod = TimeSpan.Zero
            };
        }
    }

    public class GoldenRulesTestFixture : IntegrationTestFixture
    {
        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            // Add Golden Rules configuration
            services.Configure<GoldenRulesEngineConfig>(options =>
            {
                options.Enabled = true;
                options.StrictMode = true;
                options.EnableRealTimeAlerts = true;
                options.MinimumComplianceScore = 0.6m;
                options.RuleConfigs = new List<GoldenRuleConfiguration>();
            });

            // Mock time series service for testing
            var mockTimeSeriesService = new Mock<ITimeSeriesService>();
            mockTimeSeriesService.Setup(x => x.WritePointAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult.Success());
            mockTimeSeriesService.Setup(x => x.IsHealthyAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult<bool>.Success(true));

            services.AddSingleton(mockTimeSeriesService.Object);

            // Add Golden Rules Engine
            services.AddSingleton<IGoldenRulesEngine, CanonicalGoldenRulesEngine>();
        }
    }
}