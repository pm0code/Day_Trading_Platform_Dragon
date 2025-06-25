using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TradingPlatform.Core.Models;
using TradingPlatform.PaperTrading.Services;
using TradingPlatform.PaperTrading.Models;
using TradingPlatform.Messaging.Services;
using TradingPlatform.UnitTests.Framework;
using TradingPlatform.UnitTests.Extensions;
using TradingPlatform.UnitTests.Builders;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.UnitTests.PaperTrading.Services
{
    public class OrderExecutionEngineCanonicalTests : CanonicalServiceTestBase<OrderExecutionEngineCanonical>
    {
        private readonly Mock<ICanonicalMessageQueue> _mockMessageQueue;

        public OrderExecutionEngineCanonicalTests(ITestOutputHelper output) : base(output)
        {
            _mockMessageQueue = new Mock<ICanonicalMessageQueue>();
            _mockMessageQueue.Setup(x => x.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<MessagePriority>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult<string>.Success("msg-123"));
        }

        protected override OrderExecutionEngineCanonical CreateService()
        {
            return new OrderExecutionEngineCanonical(MockLogger.Object, _mockMessageQueue.Object);
        }

        protected override void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
        {
            base.ConfigureServices(services);
            services.AddSingleton(_mockMessageQueue.Object);
        }

        [Fact]
        public async Task ExecuteOrderAsync_MarketOrder_ExecutesImmediately()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var order = new OrderBuilder()
                .WithSymbol("AAPL")
                .WithOrderType(OrderType.Market)
                .WithSide(OrderSide.Buy)
                .WithQuantity(100)
                .WithPrice(150m)
                .Build();

            // Act
            var result = await Service.ExecuteOrderAsync(order, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var execution = result.Value;
            execution.Should().NotBeNull();
            execution.OrderId.Should().Be(order.Id);
            execution.Symbol.Should().Be("AAPL");
            execution.Quantity.Should().Be(100);
            execution.Price.Should().BeApproximately(150m, 1m); // Allow for slippage
            execution.Commission.Should().BeGreaterThan(0);
            execution.ExecutedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            // Verify message was published
            _mockMessageQueue.Verify(x => x.PublishAsync(
                "order-executions",
                It.IsAny<OrderExecution>(),
                MessagePriority.High,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteOrderAsync_LimitOrder_ChecksPriceBeforeExecution()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var order = new OrderBuilder()
                .WithLimitOrder(145m) // Limit price below market
                .WithSide(OrderSide.Buy)
                .WithQuantity(50)
                .Build();

            // Act
            var result = await Service.ExecuteOrderAsync(order, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var execution = result.Value;
            
            // For simulation, limit orders execute at limit price or better
            execution.Price.Should().BeLessOrEqualTo(145m);
        }

        [Fact]
        public async Task ValidateOrderAsync_ValidOrder_ReturnsTrue()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var order = new OrderBuilder()
                .WithSymbol("TSLA")
                .WithQuantity(10)
                .WithPrice(200m)
                .Build();

            // Act
            var result = await Service.ValidateOrderAsync(order, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            result.Value.Should().BeTrue();
        }

        [Theory]
        [InlineData("", 100, 150, "Symbol is required")]
        [InlineData("AAPL", -10, 150, "Quantity must be positive")]
        [InlineData("AAPL", 0, 150, "Quantity must be positive")]
        [InlineData("AAPL", 100, -50, "Price must be positive")]
        [InlineData("AAPL", int.MaxValue, 150, "Quantity exceeds maximum")]
        public async Task ValidateOrderAsync_InvalidOrder_ReturnsFalse(
            string symbol, decimal quantity, decimal price, string expectedError)
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = symbol,
                OrderType = OrderType.Market,
                Side = OrderSide.Buy,
                Quantity = quantity,
                Price = price,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var result = await Service.ValidateOrderAsync(order, TestCts.Token);

            // Assert
            result.Should().BeFailure();
            result.Error!.Message.Should().Contain(expectedError);
        }

        [Fact]
        public async Task ExecuteOrderAsync_CalculatesSlippage()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var order = new OrderBuilder()
                .WithSymbol("NVDA")
                .WithOrderType(OrderType.Market)
                .WithQuantity(1000) // Large order should have more slippage
                .WithPrice(500m)
                .Build();

            // Act
            var result = await Service.ExecuteOrderAsync(order, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var execution = result.Value;
            execution.Slippage.Should().BeGreaterThan(0);
            execution.MarketImpact.Should().BeGreaterThan(0);
            
            // Slippage should affect execution price
            if (order.Side == OrderSide.Buy)
            {
                execution.Price.Should().BeGreaterThan(order.Price); // Pay more when buying
            }
            else
            {
                execution.Price.Should().BeLessThan(order.Price); // Receive less when selling
            }
        }

        [Fact]
        public async Task ExecuteOrderAsync_CalculatesCommission()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var order = new OrderBuilder()
                .WithQuantity(100)
                .WithPrice(150m)
                .Build();

            var expectedValue = 100m * 150m; // $15,000
            var expectedCommission = expectedValue * 0.001m; // 0.1% = $15

            // Act
            var result = await Service.ExecuteOrderAsync(order, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            var execution = result.Value;
            execution.Commission.Should().Be(expectedCommission);
        }

        [Fact]
        public async Task ExecuteOrderAsync_StopOrder_RequiresStopPrice()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var order = new OrderBuilder()
                .WithStopOrder(145m) // Stop at $145
                .Build();

            // Act
            var result = await Service.ExecuteOrderAsync(order, TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            // In paper trading, stop orders execute immediately for simplicity
            var execution = result.Value;
            execution.Should().NotBeNull();
        }

        [Fact]
        public async Task CancelOrderAsync_PendingOrder_Cancels()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var orderId = Guid.NewGuid().ToString();

            // Act
            var result = await Service.CancelOrderAsync(orderId, "User requested", TestCts.Token);

            // Assert
            result.Should().BeSuccess();
            result.Value.Should().BeTrue();

            // Verify cancellation event was published
            _mockMessageQueue.Verify(x => x.PublishAsync(
                "order-events",
                It.Is<object>(o => o.ToString()!.Contains("Cancelled")),
                It.IsAny<MessagePriority>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetMetrics_TracksExecutions()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            // Execute multiple orders
            for (int i = 0; i < 5; i++)
            {
                var order = new OrderBuilder()
                    .WithSymbol($"SYM{i}")
                    .WithQuantity(100)
                    .WithPrice(100 + i * 10)
                    .Build();
                
                await Service.ExecuteOrderAsync(order, TestCts.Token);
            }

            // Act
            var metricsResult = await Service.GetPerformanceMetricsAsync(TestCts.Token);

            // Assert
            metricsResult.Should().BeSuccess();
            var metrics = metricsResult.Value;
            metrics.Should().ContainKey("TotalOrdersExecuted");
            metrics["TotalOrdersExecuted"].Should().Be(5L);
            metrics.Should().ContainKey("TotalCommissionCollected");
            metrics.Should().ContainKey("TotalSlippage");
        }

        [Fact]
        public async Task ExecuteOrderAsync_HighFrequency_HandlesCorrectly()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var tasks = new Task<TradingResult<OrderExecution>>[100];

            // Act - Execute 100 orders concurrently
            for (int i = 0; i < tasks.Length; i++)
            {
                var order = new OrderBuilder()
                    .WithSymbol($"SYM{i % 10}")
                    .WithQuantity(10 + i % 50)
                    .WithPrice(50 + i % 100)
                    .Build();
                
                tasks[i] = Service.ExecuteOrderAsync(order, TestCts.Token);
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().AllSatisfy(r => r.Should().BeSuccess());
            results.Select(r => r.Value.OrderId).Distinct().Should().HaveCount(100);
        }

        [Fact]
        public async Task ExecuteOrderAsync_DifferentTimeInForce_HandledCorrectly()
        {
            // Arrange
            Service = CreateService();
            await Service.InitializeAsync(TestCts.Token);
            await Service.StartAsync(TestCts.Token);

            var orders = new[]
            {
                new OrderBuilder().WithTimeInForce(TimeInForce.Day).Build(),
                new OrderBuilder().WithTimeInForce(TimeInForce.GTC).Build(),
                new OrderBuilder().WithTimeInForce(TimeInForce.IOC).Build(),
                new OrderBuilder().WithTimeInForce(TimeInForce.FOK).Build()
            };

            // Act & Assert
            foreach (var order in orders)
            {
                var result = await Service.ExecuteOrderAsync(order, TestCts.Token);
                result.Should().BeSuccess();
                
                // In paper trading, all TIF orders execute for simplicity
                result.Value.Should().NotBeNull();
            }
        }
    }
}