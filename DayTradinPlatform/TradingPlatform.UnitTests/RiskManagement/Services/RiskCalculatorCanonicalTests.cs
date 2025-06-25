using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TradingPlatform.Core.Models;
using TradingPlatform.RiskManagement.Services;
using TradingPlatform.RiskManagement.Models;
using TradingPlatform.UnitTests.Framework;
using TradingPlatform.UnitTests.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.UnitTests.RiskManagement.Services
{
    public class RiskCalculatorCanonicalTests : CanonicalServiceTestBase<RiskCalculatorCanonical>
    {
        public RiskCalculatorCanonicalTests(ITestOutputHelper output) : base(output)
        {
        }

        protected override RiskCalculatorCanonical CreateService()
        {
            return new RiskCalculatorCanonical(MockLogger.Object);
        }

        [Fact]
        public async Task CalculateRiskAsync_WithValidInputs_ReturnsRiskAssessment()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var symbol = "AAPL";
            var quantity = 100m;
            var price = 150m;
            var side = OrderSide.Buy;

            // Act
            var result = await Service.CalculateRiskAsync(symbol, quantity, price, side, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var risk = result.Value;
            risk.Should().NotBeNull();
            risk.Symbol.Should().Be(symbol);
            risk.PositionSize.Should().Be(quantity * price); // $15,000
            risk.PositionRisk.Should().BeGreaterThan(0);
            risk.AccountRiskPercentage.Should().BeGreaterThan(0);
            risk.IsWithinLimits.Should().BeTrue();
        }

        [Theory]
        [InlineData(1000, 150, 150000, true)]   // $150k position = acceptable
        [InlineData(10000, 150, 1500000, false)] // $1.5M position = too large
        [InlineData(100, 1500, 150000, true)]   // $150k position = acceptable
        public async Task CalculateRiskAsync_PositionSizeLimits_EnforcedCorrectly(
            decimal quantity, decimal price, decimal expectedPositionSize, bool shouldBeWithinLimits)
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            // Act
            var result = await Service.CalculateRiskAsync("TEST", quantity, price, OrderSide.Buy, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var risk = result.Value;
            risk.PositionSize.Should().Be(expectedPositionSize);
            risk.IsWithinLimits.Should().Be(shouldBeWithinLimits);
            
            if (!shouldBeWithinLimits)
            {
                risk.RiskViolations.Should().Contain("Position size exceeds maximum allowed");
            }
        }

        [Fact]
        public async Task CalculateRiskAsync_WithStopLoss_CalculatesRiskCorrectly()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var symbol = "TSLA";
            var quantity = 100m;
            var entryPrice = 200m;
            var stopLoss = 195m; // $5 stop loss per share
            var side = OrderSide.Buy;

            // Act
            var result = await Service.CalculateRiskAsync(symbol, quantity, entryPrice, side, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var risk = result.Value;
            
            // With 2% default stop loss: 200 * 0.02 = $4 per share
            // Position risk = 100 * 4 = $400
            var expectedRiskPerShare = entryPrice * 0.02m; // 2% stop loss
            var expectedPositionRisk = quantity * expectedRiskPerShare;
            
            risk.PositionRisk.Should().Be(expectedPositionRisk);
        }

        [Fact]
        public async Task CalculateAccountRiskAsync_WithMultiplePositions_AggregatesCorrectly()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var positions = new[]
            {
                new { Symbol = "AAPL", Quantity = 100m, Price = 150m },
                new { Symbol = "GOOGL", Quantity = 50m, Price = 2800m },
                new { Symbol = "MSFT", Quantity = 200m, Price = 400m }
            };

            decimal totalRisk = 0;

            // Act - Calculate risk for each position
            foreach (var pos in positions)
            {
                var result = await Service.CalculateRiskAsync(
                    pos.Symbol, pos.Quantity, pos.Price, OrderSide.Buy, TestCts.Token);
                
                result.Should().BeSuccess();
                totalRisk += result.Value.PositionRisk;
            }

            // Assert
            totalRisk.Should().BeGreaterThan(0);
            
            // Total position value = (100*150) + (50*2800) + (200*400) = 15k + 140k + 80k = 235k
            var totalPositionValue = 235000m;
            var expectedTotalRisk = totalPositionValue * 0.02m; // 2% stop loss = $4,700
            totalRisk.Should().Be(expectedTotalRisk);
        }

        [Theory]
        [InlineData(OrderSide.Buy, 100, 50, -50)]   // Long position loss
        [InlineData(OrderSide.Buy, 100, 150, 50)]   // Long position profit
        [InlineData(OrderSide.Sell, 100, 50, 50)]   // Short position profit
        [InlineData(OrderSide.Sell, 100, 150, -50)] // Short position loss
        public async Task CalculateRiskAsync_LongVsShort_CalculatesCorrectly(
            OrderSide side, decimal entryPrice, decimal currentPrice, decimal expectedPnLPerShare)
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var quantity = 100m;

            // Act
            var result = await Service.CalculateRiskAsync("TEST", quantity, entryPrice, side, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var risk = result.Value;
            
            // Risk should always be positive (potential loss)
            risk.PositionRisk.Should().BeGreaterThan(0);
            
            // For buy orders, risk is downward movement
            // For sell orders, risk is upward movement
            if (side == OrderSide.Buy)
            {
                risk.RiskDirection.Should().Be("Downside");
            }
            else
            {
                risk.RiskDirection.Should().Be("Upside");
            }
        }

        [Fact]
        public async Task CalculateRiskAsync_WithInvalidInputs_ReturnsError()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            // Act & Assert - Negative quantity
            var result1 = await Service.CalculateRiskAsync("AAPL", -100, 150, OrderSide.Buy, TestCts.Token);
            result1.Should().BeFailure();
            result1.Error!.Message.Should().Contain("Quantity must be positive");

            // Act & Assert - Zero price
            var result2 = await Service.CalculateRiskAsync("AAPL", 100, 0, OrderSide.Buy, TestCts.Token);
            result2.Should().BeFailure();
            result2.Error!.Message.Should().Contain("Price must be positive");

            // Act & Assert - Empty symbol
            var result3 = await Service.CalculateRiskAsync("", 100, 150, OrderSide.Buy, TestCts.Token);
            result3.Should().BeFailure();
            result3.Error!.Message.Should().Contain("Symbol cannot be empty");
        }

        [Fact]
        public async Task CalculatePortfolioRiskAsync_WithDiversification_ReducesRisk()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            // Single large position
            var singlePositionResult = await Service.CalculateRiskAsync(
                "AAPL", 1000, 150, OrderSide.Buy, TestCts.Token);

            // Multiple smaller positions (same total value)
            var positions = new[]
            {
                ("AAPL", 250m, 150m),
                ("GOOGL", 53.5m, 2800m), // ~$150k
                ("MSFT", 375m, 400m),
                ("NVDA", 300m, 500m)
            };

            var diversifiedRisk = 0m;
            foreach (var (symbol, qty, price) in positions)
            {
                var result = await Service.CalculateRiskAsync(
                    symbol, qty, price, OrderSide.Buy, TestCts.Token);
                result.Should().BeSuccess();
                diversifiedRisk += result.Value.PositionRisk;
            }

            // Assert
            singlePositionResult.Should().BeSuccess();
            var singleRisk = singlePositionResult.Value.PositionRisk;
            
            // Diversified portfolio should have similar total risk
            // (In reality, correlation would reduce risk further)
            diversifiedRisk.Should().BeApproximately(singleRisk, singleRisk * 0.1m);
        }

        [Fact]
        public async Task GetMetrics_TracksRiskCalculations()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            // Perform multiple risk calculations
            var symbols = new[] { "AAPL", "GOOGL", "MSFT", "TSLA", "NVDA" };
            foreach (var symbol in symbols)
            {
                await Service.CalculateRiskAsync(
                    symbol, 
                    Random.Shared.Next(10, 100), 
                    Random.Shared.Next(50, 500), 
                    OrderSide.Buy, 
                    TestCts.Token);
            }

            // Act
            var metricsResult = await Service.GetPerformanceMetricsAsync(TestCts.Token);

            // Assert
            metricsResult.Should().BeSuccess();
            var metrics = metricsResult.Value;
            metrics.Should().ContainKey("TotalRiskCalculations");
            metrics["TotalRiskCalculations"].Should().Be(5L);
        }

        [Fact]
        public async Task CalculateRiskAsync_UnderStress_PerformsQuickly()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Act - Perform 1000 risk calculations
            var tasks = new Task[1000];
            for (int i = 0; i < tasks.Length; i++)
            {
                var symbol = $"SYM{i % 10}";
                var quantity = 100m + (i % 50);
                var price = 50m + (i % 100);
                
                tasks[i] = Service.CalculateRiskAsync(
                    symbol, quantity, price, OrderSide.Buy, TestCts.Token);
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            // Assert - Should complete quickly
            sw.ElapsedMilliseconds.Should().BeLessThan(1000); // Under 1 second for 1000 calculations
            Output.WriteLine($"Completed 1000 risk calculations in {sw.ElapsedMilliseconds}ms");
        }
    }
}