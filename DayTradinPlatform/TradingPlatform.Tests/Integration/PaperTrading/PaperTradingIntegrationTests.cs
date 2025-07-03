using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.PaperTrading.Services;
using TradingPlatform.PaperTrading.Models;
using TradingPlatform.Tests.Core.Canonical;
using Moq;

namespace TradingPlatform.Tests.Integration.PaperTrading
{
    /// <summary>
    /// Integration tests for PaperTrading module
    /// Verifies that canonical services work together correctly
    /// </summary>
    public class PaperTradingIntegrationTests : IntegrationTestBase
    {
        private IOrderExecutionEngine _executionEngine;
        private IPortfolioManager _portfolioManager;
        private IOrderBookSimulator _orderBookSimulator;
        private ISlippageCalculator _slippageCalculator;
        private PaperTradingServiceCanonical _paperTradingService;
        
        public PaperTradingIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            // Register all PaperTrading canonical services
            services.AddPaperTradingServices();
            
            // Register mock dependencies
            services.AddSingleton(MockLogger.Object);
            services.AddSingleton(Mock.Of<IMarketDataService>());
            services.AddSingleton(Mock.Of<IRiskManagementService>());
        }
        
        protected override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            
            // Get services
            _executionEngine = ServiceProvider.GetRequiredService<IOrderExecutionEngine>();
            _portfolioManager = ServiceProvider.GetRequiredService<IPortfolioManager>();
            _orderBookSimulator = ServiceProvider.GetRequiredService<IOrderBookSimulator>();
            _slippageCalculator = ServiceProvider.GetRequiredService<ISlippageCalculator>();
            _paperTradingService = ServiceProvider.GetRequiredService<PaperTradingServiceCanonical>();
            
            // Initialize all services
            await _paperTradingService.InitializeAsync();
        }
        
        #region Order Execution Flow Tests
        
        [Fact]
        public async Task ExecuteOrder_CompleteFlow_UpdatesPortfolioCorrectly()
        {
            // Arrange
            var initialCash = 100000m;
            await _portfolioManager.InitializePortfolioAsync(initialCash);
            
            var order = new Order
            {
                OrderId = Guid.NewGuid().ToString(),
                Symbol = "AAPL",
                Quantity = 100,
                OrderType = OrderType.Market,
                Side = OrderSide.Buy,
                CreatedAt = DateTime.UtcNow
            };
            
            var marketPrice = 175.50m;
            
            // Act
            var execution = await _executionEngine.ExecuteOrderAsync(order, marketPrice);
            
            // Assert
            Assert.NotNull(execution);
            Assert.Equal(OrderStatus.Filled, execution.Status);
            
            // Verify portfolio was updated
            var portfolio = await _portfolioManager.GetPortfolioAsync();
            Assert.NotNull(portfolio);
            Assert.Contains("AAPL", portfolio.Holdings.Keys);
            Assert.Equal(100, portfolio.Holdings["AAPL"].Quantity);
            
            // Verify cash was reduced
            var expectedCashReduction = execution.ExecutedPrice * execution.ExecutedQuantity;
            Assert.True(portfolio.CashBalance < initialCash);
            
            Output.WriteLine($"Order executed at {execution.ExecutedPrice:C}");
            Output.WriteLine($"Portfolio cash: {portfolio.CashBalance:C}");
            Output.WriteLine($"AAPL position: {portfolio.Holdings["AAPL"].Quantity} shares");
        }
        
        [Fact]
        public async Task ExecuteMultipleOrders_WithSlippage_CalculatesCorrectly()
        {
            // Arrange
            await _portfolioManager.InitializePortfolioAsync(200000m);
            
            var orders = new[]
            {
                CreateOrder("AAPL", 100, OrderSide.Buy),
                CreateOrder("MSFT", 50, OrderSide.Buy),
                CreateOrder("GOOGL", 25, OrderSide.Buy)
            };
            
            var marketPrices = new Dictionary<string, decimal>
            {
                ["AAPL"] = 175m,
                ["MSFT"] = 350m,
                ["GOOGL"] = 140m
            };
            
            // Act
            var executions = new List<Execution>();
            foreach (var order in orders)
            {
                var execution = await _executionEngine.ExecuteOrderAsync(
                    order, marketPrices[order.Symbol]);
                executions.Add(execution);
            }
            
            // Assert
            Assert.All(executions, e => Assert.Equal(OrderStatus.Filled, e.Status));
            
            // Verify slippage was applied
            foreach (var execution in executions)
            {
                var expectedSlippage = await _slippageCalculator.EstimateSlippageAsync(
                    execution.Symbol, execution.Side, execution.ExecutedQuantity);
                
                Assert.True(expectedSlippage > 0);
                
                // For buy orders, executed price should be higher than market price
                Assert.True(execution.ExecutedPrice > marketPrices[execution.Symbol]);
                
                Output.WriteLine($"{execution.Symbol}: Market={marketPrices[execution.Symbol]:C}, " +
                               $"Executed={execution.ExecutedPrice:C}, " +
                               $"Slippage={expectedSlippage:P4}");
            }
        }
        
        [Fact]
        public async Task ExecuteSellOrder_WithExistingPosition_UpdatesCorrectly()
        {
            // Arrange
            await _portfolioManager.InitializePortfolioAsync(100000m);
            
            // First, buy some shares
            var buyOrder = CreateOrder("AAPL", 200, OrderSide.Buy);
            await _executionEngine.ExecuteOrderAsync(buyOrder, 170m);
            
            // Then sell half
            var sellOrder = CreateOrder("AAPL", 100, OrderSide.Sell);
            
            // Act
            var sellExecution = await _executionEngine.ExecuteOrderAsync(sellOrder, 175m);
            
            // Assert
            Assert.NotNull(sellExecution);
            Assert.Equal(OrderStatus.Filled, sellExecution.Status);
            
            var portfolio = await _portfolioManager.GetPortfolioAsync();
            Assert.Equal(100, portfolio.Holdings["AAPL"].Quantity); // Should have 100 left
            
            // Calculate realized P&L
            var buyPrice = 170m; // Approximate, ignoring slippage for simplicity
            var sellPrice = sellExecution.ExecutedPrice;
            var realizedPnL = (sellPrice - buyPrice) * 100;
            
            Output.WriteLine($"Realized P&L: {realizedPnL:C}");
            Output.WriteLine($"Remaining position: {portfolio.Holdings["AAPL"].Quantity} shares");
        }
        
        #endregion
        
        #region Order Book Simulation Tests
        
        [Fact]
        public async Task OrderBookSimulation_LimitOrders_ExecuteAtCorrectPrices()
        {
            // Arrange
            await _portfolioManager.InitializePortfolioAsync(100000m);
            
            // Create order book with spread
            var orderBook = new OrderBook
            {
                Symbol = "AAPL",
                Timestamp = DateTime.UtcNow,
                Bids = new List<OrderBookLevel>
                {
                    new() { Price = 174.95m, Size = 1000 },
                    new() { Price = 174.90m, Size = 2000 },
                    new() { Price = 174.85m, Size = 3000 }
                },
                Asks = new List<OrderBookLevel>
                {
                    new() { Price = 175.05m, Size = 1000 },
                    new() { Price = 175.10m, Size = 2000 },
                    new() { Price = 175.15m, Size = 3000 }
                }
            };
            
            await _orderBookSimulator.UpdateOrderBookAsync(orderBook);
            
            // Create limit order
            var limitOrder = new Order
            {
                OrderId = Guid.NewGuid().ToString(),
                Symbol = "AAPL",
                Quantity = 100,
                OrderType = OrderType.Limit,
                Side = OrderSide.Buy,
                LimitPrice = 175.00m,
                CreatedAt = DateTime.UtcNow
            };
            
            // Act
            var canExecute = await _orderBookSimulator.CanExecuteLimitOrderAsync(limitOrder);
            var executionPrice = await _executionEngine.CalculateExecutionPriceAsync(
                limitOrder, orderBook);
            
            // Assert
            Assert.False(canExecute); // Best ask is 175.05, limit is 175.00
            Assert.Equal(0m, executionPrice); // Should not execute
            
            // Update limit price to cross the spread
            limitOrder.LimitPrice = 175.10m;
            canExecute = await _orderBookSimulator.CanExecuteLimitOrderAsync(limitOrder);
            executionPrice = await _executionEngine.CalculateExecutionPriceAsync(
                limitOrder, orderBook);
            
            Assert.True(canExecute);
            Assert.Equal(175.05m, executionPrice); // Should execute at best ask
        }
        
        #endregion
        
        #region Portfolio Management Integration Tests
        
        [Fact]
        public async Task PortfolioTracking_MultiplePositions_CalculatesMetricsCorrectly()
        {
            // Arrange
            await _portfolioManager.InitializePortfolioAsync(250000m);
            
            // Execute multiple trades
            var trades = new[]
            {
                (Symbol: "AAPL", Quantity: 100, Price: 175m),
                (Symbol: "MSFT", Quantity: 50, Price: 350m),
                (Symbol: "GOOGL", Quantity: 25, Price: 140m),
                (Symbol: "TSLA", Quantity: 75, Price: 250m)
            };
            
            foreach (var trade in trades)
            {
                var order = CreateOrder(trade.Symbol, trade.Quantity, OrderSide.Buy);
                await _executionEngine.ExecuteOrderAsync(order, trade.Price);
            }
            
            // Update current prices (simulate market movement)
            var currentPrices = new Dictionary<string, decimal>
            {
                ["AAPL"] = 180m,   // +$5
                ["MSFT"] = 345m,   // -$5
                ["GOOGL"] = 145m,  // +$5
                ["TSLA"] = 260m    // +$10
            };
            
            foreach (var kvp in currentPrices)
            {
                await _portfolioManager.UpdatePositionPriceAsync(kvp.Key, kvp.Value);
            }
            
            // Act
            var portfolio = await _portfolioManager.GetPortfolioAsync();
            var totalValue = await _portfolioManager.GetTotalPortfolioValueAsync();
            var positions = await _portfolioManager.GetPositionsAsync();
            
            // Assert
            Assert.Equal(4, positions.Count);
            
            // Calculate expected unrealized P&L
            var expectedUnrealizedPnL = 
                (180m - 175m) * 100 +  // AAPL: +$500
                (345m - 350m) * 50 +   // MSFT: -$250
                (145m - 140m) * 25 +   // GOOGL: +$125
                (260m - 250m) * 75;    // TSLA: +$750
                                       // Total: +$1,125
            
            var actualUnrealizedPnL = portfolio.Holdings.Values.Sum(h => h.UnrealizedPnL);
            AssertFinancialPrecision(expectedUnrealizedPnL, actualUnrealizedPnL);
            
            Output.WriteLine($"Total Portfolio Value: {totalValue:C}");
            Output.WriteLine($"Unrealized P&L: {actualUnrealizedPnL:C}");
            Output.WriteLine("\nPositions:");
            foreach (var position in positions)
            {
                var holding = portfolio.Holdings[position.Symbol];
                Output.WriteLine($"  {position.Symbol}: {position.Quantity} shares, " +
                               $"Avg Cost: {position.AverageCost:C}, " +
                               $"Current: {holding.CurrentPrice:C}, " +
                               $"P&L: {holding.UnrealizedPnL:C}");
            }
        }
        
        [Fact]
        public async Task RiskLimits_PreventOverExposure()
        {
            // Arrange
            await _portfolioManager.InitializePortfolioAsync(50000m);
            
            // Try to buy more than we can afford
            var largeOrder = CreateOrder("AAPL", 1000, OrderSide.Buy); // ~$175,000 worth
            
            // Act
            var execution = await _executionEngine.ExecuteOrderAsync(largeOrder, 175m);
            
            // Assert
            Assert.NotNull(execution);
            Assert.Equal(OrderStatus.Rejected, execution.Status);
            Assert.Contains("Insufficient", execution.RejectionReason);
            
            Output.WriteLine($"Order rejected: {execution.RejectionReason}");
        }
        
        #endregion
        
        #region Performance and Concurrency Tests
        
        [Fact]
        public async Task ConcurrentOrders_ExecuteCorrectly()
        {
            // Arrange
            await _portfolioManager.InitializePortfolioAsync(500000m);
            
            var symbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA" };
            var orders = symbols.Select(s => CreateOrder(s, 50, OrderSide.Buy)).ToList();
            
            // Act - Execute orders concurrently
            var executionTasks = orders.Select(order => 
                _executionEngine.ExecuteOrderAsync(order, 100m + Random.Shared.Next(50, 200))
            ).ToList();
            
            var executions = await Task.WhenAll(executionTasks);
            
            // Assert
            Assert.All(executions, e => Assert.Equal(OrderStatus.Filled, e.Status));
            
            var portfolio = await _portfolioManager.GetPortfolioAsync();
            Assert.Equal(5, portfolio.Holdings.Count);
            Assert.All(symbols, s => Assert.Equal(50, portfolio.Holdings[s].Quantity));
            
            Output.WriteLine($"Executed {executions.Length} orders concurrently");
            Output.WriteLine($"Final cash balance: {portfolio.CashBalance:C}");
        }
        
        #endregion
        
        #region Helper Methods
        
        private Order CreateOrder(string symbol, decimal quantity, OrderSide side)
        {
            return new Order
            {
                OrderId = Guid.NewGuid().ToString(),
                Symbol = symbol,
                Quantity = quantity,
                OrderType = OrderType.Market,
                Side = side,
                CreatedAt = DateTime.UtcNow
            };
        }
        
        private void AssertFinancialPrecision(decimal expected, decimal actual, int decimalPlaces = 2)
        {
            var tolerance = (decimal)Math.Pow(10, -decimalPlaces);
            Assert.True(Math.Abs(expected - actual) <= tolerance,
                $"Expected {expected} but got {actual}. Difference: {Math.Abs(expected - actual)}");
        }
        
        #endregion
    }
}