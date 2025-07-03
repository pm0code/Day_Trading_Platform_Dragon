using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.PaperTrading.Services;
using TradingPlatform.PaperTrading.Models;
using TradingPlatform.Tests.Core.Canonical;
using TradingPlatform.Core.Interfaces;

namespace TradingPlatform.Tests.Unit.PaperTrading
{
    /// <summary>
    /// Comprehensive unit tests for SlippageCalculatorCanonical
    /// Tests slippage calculation, market impact estimation, and microstructure models
    /// </summary>
    public class SlippageCalculatorCanonicalTests : CanonicalTestBase<SlippageCalculatorCanonical>
    {
        private const decimal PRECISION_TOLERANCE = 0.0001m;
        
        public SlippageCalculatorCanonicalTests(ITestOutputHelper output) : base(output)
        {
        }
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ITradingLogger>(MockLogger.Object);
        }
        
        protected override SlippageCalculatorCanonical CreateSystemUnderTest()
        {
            return new SlippageCalculatorCanonical(MockLogger.Object);
        }
        
        #region Basic Slippage Calculation Tests
        
        [Theory]
        [InlineData(100, 100.10, OrderSide.Buy, 0.001)] // Buy slippage: (100.10-100)/100 = 0.1%
        [InlineData(100, 99.90, OrderSide.Sell, 0.001)] // Sell slippage: (100-99.90)/100 = 0.1%
        [InlineData(50, 50.25, OrderSide.Buy, 0.005)]   // 0.5% slippage
        [InlineData(50, 49.75, OrderSide.Sell, 0.005)]  // 0.5% slippage
        public void CalculateSlippage_BasicScenarios_ReturnsCorrectValue(
            decimal requestedPrice, decimal executedPrice, OrderSide side, decimal expectedSlippage)
        {
            // Act
            var result = SystemUnderTest.CalculateSlippage(requestedPrice, executedPrice, side);
            
            // Assert
            AssertFinancialPrecision(expectedSlippage, result, 4);
        }
        
        [Fact]
        public void CalculateSlippage_NoSlippage_ReturnsZero()
        {
            // Arrange
            decimal price = 100m;
            
            // Act
            var buySlippage = SystemUnderTest.CalculateSlippage(price, price, OrderSide.Buy);
            var sellSlippage = SystemUnderTest.CalculateSlippage(price, price, OrderSide.Sell);
            
            // Assert
            Assert.Equal(0m, buySlippage);
            Assert.Equal(0m, sellSlippage);
        }
        
        [Fact]
        public void CalculateSlippage_FavorableExecution_ReturnsZero()
        {
            // Arrange - Better execution than requested
            decimal requestedPrice = 100m;
            decimal favorableBuyPrice = 99.50m;  // Better for buy
            decimal favorableSellPrice = 100.50m; // Better for sell
            
            // Act
            var buySlippage = SystemUnderTest.CalculateSlippage(requestedPrice, favorableBuyPrice, OrderSide.Buy);
            var sellSlippage = SystemUnderTest.CalculateSlippage(requestedPrice, favorableSellPrice, OrderSide.Sell);
            
            // Assert
            Assert.Equal(0m, buySlippage); // No negative slippage reported
            Assert.Equal(0m, sellSlippage);
        }
        
        [Theory]
        [InlineData(0, 100, OrderSide.Buy)]   // Zero requested price
        [InlineData(100, 0, OrderSide.Sell)]  // Zero executed price
        [InlineData(-100, 100, OrderSide.Buy)] // Negative price
        public void CalculateSlippage_InvalidPrices_HandlesGracefully(
            decimal requestedPrice, decimal executedPrice, OrderSide side)
        {
            if (requestedPrice <= 0 || executedPrice <= 0)
            {
                // Act & Assert
                var result = SystemUnderTest.CalculateSlippage(requestedPrice, executedPrice, side);
                Assert.True(result >= 0); // Should handle gracefully
            }
        }
        
        #endregion
        
        #region Slippage Estimation Tests
        
        [Fact]
        public async Task EstimateSlippageAsync_SmallOrder_ReturnsMinimalSlippage()
        {
            // Arrange - Small order relative to ADV
            string symbol = "AAPL";
            decimal quantity = 100; // Small quantity
            
            // Act
            var slippage = await SystemUnderTest.EstimateSlippageAsync(symbol, OrderSide.Buy, quantity);
            
            // Assert
            Assert.True(slippage > 0, "Slippage should be positive");
            Assert.True(slippage < 0.001m, "Small orders should have minimal slippage");
            Output.WriteLine($"Estimated slippage for {quantity} shares: {slippage:P4}");
        }
        
        [Fact]
        public async Task EstimateSlippageAsync_LargeOrder_ReturnsHigherSlippage()
        {
            // Arrange - Large order relative to ADV
            string symbol = "AAPL";
            decimal smallQuantity = 100;
            decimal largeQuantity = 100000;
            
            // Act
            var smallSlippage = await SystemUnderTest.EstimateSlippageAsync(symbol, OrderSide.Buy, smallQuantity);
            var largeSlippage = await SystemUnderTest.EstimateSlippageAsync(symbol, OrderSide.Buy, largeQuantity);
            
            // Assert
            Assert.True(largeSlippage > smallSlippage, 
                $"Large order slippage ({largeSlippage:P4}) should exceed small order ({smallSlippage:P4})");
            Output.WriteLine($"Small order: {smallSlippage:P4}, Large order: {largeSlippage:P4}");
        }
        
        [Fact]
        public async Task EstimateSlippageAsync_DifferentSides_MayDiffer()
        {
            // Arrange
            string symbol = "AAPL";
            decimal quantity = 10000;
            
            // Act
            var buySlippage = await SystemUnderTest.EstimateSlippageAsync(symbol, OrderSide.Buy, quantity);
            var sellSlippage = await SystemUnderTest.EstimateSlippageAsync(symbol, OrderSide.Sell, quantity);
            
            // Assert
            // Buy and sell slippage may differ due to market conditions
            Assert.True(buySlippage > 0);
            Assert.True(sellSlippage > 0);
            Output.WriteLine($"Buy slippage: {buySlippage:P4}, Sell slippage: {sellSlippage:P4}");
        }
        
        [Fact]
        public async Task EstimateSlippageAsync_MaximumCap_DoesNotExceedLimit()
        {
            // Arrange - Extremely large order
            string symbol = "AAPL";
            decimal hugeQuantity = 10000000; // 10 million shares
            
            // Act
            var slippage = await SystemUnderTest.EstimateSlippageAsync(symbol, OrderSide.Buy, hugeQuantity);
            
            // Assert
            Assert.True(slippage <= 0.10m, "Slippage should be capped at 10%");
            Output.WriteLine($"Capped slippage for huge order: {slippage:P4}");
        }
        
        #endregion
        
        #region Market Impact Tests
        
        [Fact]
        public async Task CalculateMarketImpactAsync_StandardOrder_ReturnsReasonableImpact()
        {
            // Arrange
            string symbol = "AAPL";
            decimal quantity = 10000;
            TimeSpan duration = TimeSpan.FromMinutes(30);
            
            // Act
            var impact = await SystemUnderTest.CalculateMarketImpactAsync(symbol, quantity, duration);
            
            // Assert
            Assert.True(impact > 0, "Market impact should be positive");
            Assert.True(impact < 0.01m, "Standard order should have < 1% impact");
            Output.WriteLine($"Market impact for {quantity} shares over {duration}: {impact:P4}");
        }
        
        [Fact]
        public async Task CalculateMarketImpactAsync_LongerDuration_ReducesImpact()
        {
            // Arrange
            string symbol = "AAPL";
            decimal quantity = 50000;
            TimeSpan shortDuration = TimeSpan.FromMinutes(5);
            TimeSpan longDuration = TimeSpan.FromHours(2);
            
            // Act
            var shortImpact = await SystemUnderTest.CalculateMarketImpactAsync(symbol, quantity, shortDuration);
            var longImpact = await SystemUnderTest.CalculateMarketImpactAsync(symbol, quantity, longDuration);
            
            // Assert
            Assert.True(longImpact < shortImpact, 
                $"Longer duration ({longImpact:P4}) should reduce impact vs short ({shortImpact:P4})");
            Output.WriteLine($"5-min impact: {shortImpact:P4}, 2-hour impact: {longImpact:P4}");
        }
        
        [Fact]
        public async Task CalculateMarketImpactAsync_TemporaryVsPermanent_BothCalculated()
        {
            // Arrange
            string symbol = "AAPL";
            decimal quantity = 25000;
            TimeSpan duration = TimeSpan.FromMinutes(15);
            
            // Act
            var totalImpact = await SystemUnderTest.CalculateMarketImpactAsync(symbol, quantity, duration);
            
            // Assert
            // Total impact should include both temporary and permanent components
            Assert.True(totalImpact > 0);
            
            // Verify metrics were recorded (check logging)
            VerifyLoggerCalled("info", Moq.Times.AtLeastOnce());
        }
        
        #endregion
        
        #region Square Root Impact Model Tests
        
        [Fact]
        public async Task EstimateSlippageAsync_SquareRootModel_ScalesCorrectly()
        {
            // Arrange - Test square root scaling
            string symbol = "AAPL";
            decimal baseQuantity = 1000;
            decimal quadrupleQuantity = 4000; // 4x quantity
            
            // Act
            var baseSlippage = await SystemUnderTest.EstimateSlippageAsync(symbol, OrderSide.Buy, baseQuantity);
            var quadSlippage = await SystemUnderTest.EstimateSlippageAsync(symbol, OrderSide.Buy, quadrupleQuantity);
            
            // Assert
            // Due to square root model, 4x quantity should give ~2x slippage (plus base)
            var ratio = quadSlippage / baseSlippage;
            Assert.True(ratio < 4, $"Square root model: 4x quantity gives {ratio:F2}x slippage, not 4x");
            Assert.True(ratio > 1.5 && ratio < 2.5, $"Ratio {ratio:F2} should be close to 2");
        }
        
        #endregion
        
        #region Symbol-Specific Tests
        
        [Theory]
        [InlineData("AAPL", 10000)]  // Liquid stock
        [InlineData("MSFT", 10000)]  // Another liquid stock
        [InlineData("SPY", 10000)]   // ETF
        [InlineData("XYZ", 10000)]   // Unknown symbol
        public async Task EstimateSlippageAsync_DifferentSymbols_HandlesAll(string symbol, decimal quantity)
        {
            // Act
            var slippage = await SystemUnderTest.EstimateSlippageAsync(symbol, OrderSide.Buy, quantity);
            
            // Assert
            Assert.True(slippage >= 0, $"Slippage for {symbol} should be non-negative");
            Assert.True(slippage <= 0.10m, $"Slippage for {symbol} should not exceed cap");
            Output.WriteLine($"{symbol}: {slippage:P4}");
        }
        
        #endregion
        
        #region Edge Case Tests
        
        [Theory]
        [InlineData("", OrderSide.Buy, 100)]     // Empty symbol
        [InlineData("AAPL", OrderSide.Buy, 0)]  // Zero quantity
        [InlineData("AAPL", OrderSide.Buy, -100)] // Negative quantity
        public async Task EstimateSlippageAsync_InvalidInputs_HandlesGracefully(
            string symbol, OrderSide side, decimal quantity)
        {
            // Act & Assert
            if (string.IsNullOrWhiteSpace(symbol) || quantity <= 0)
            {
                // Should handle invalid inputs gracefully
                var slippage = await SystemUnderTest.EstimateSlippageAsync(symbol, side, quantity);
                Assert.True(slippage >= 0 || slippage == 0); // May return 0 or handle error
            }
        }
        
        [Fact]
        public async Task CalculateMarketImpactAsync_ZeroDuration_HandlesGracefully()
        {
            // Arrange
            string symbol = "AAPL";
            decimal quantity = 10000;
            TimeSpan zeroDuration = TimeSpan.Zero;
            
            // Act
            var impact = await SystemUnderTest.CalculateMarketImpactAsync(symbol, quantity, zeroDuration);
            
            // Assert
            // Should handle zero duration without crashing
            Assert.True(impact >= 0);
        }
        
        #endregion
        
        #region Performance Tests
        
        [Fact]
        public async Task EstimateSlippageAsync_Performance_CompletesQuickly()
        {
            // Arrange
            string symbol = "AAPL";
            decimal quantity = 10000;
            
            // Act & Assert - Should complete 1000 calculations in under 100ms
            await AssertCompletesWithinAsync(100, async () =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    await SystemUnderTest.EstimateSlippageAsync(symbol, OrderSide.Buy, quantity);
                }
            });
        }
        
        [Fact]
        public void CalculateSlippage_Performance_HighThroughput()
        {
            // Act & Assert - Should handle 10,000 calculations quickly
            AssertCompletesWithinAsync(50, async () =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    var price = 100m + (i % 10) * 0.01m;
                    SystemUnderTest.CalculateSlippage(100m, price, OrderSide.Buy);
                }
                await Task.CompletedTask;
            }).GetAwaiter().GetResult();
        }
        
        #endregion
    }
    
    public enum OrderSide
    {
        Buy,
        Sell
    }
}