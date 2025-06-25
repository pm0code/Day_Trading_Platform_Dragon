using System;
using System.Threading.Tasks;
using FluentAssertions;
using TradingPlatform.Core.Models;
using TradingPlatform.PaperTrading.Services;
using TradingPlatform.SecurityTests.Framework;
using TradingPlatform.Messaging.Services;
using Xunit;
using Xunit.Abstractions;
using Moq;

namespace TradingPlatform.SecurityTests.InputValidation
{
    public class OrderValidationSecurityTests : SecurityTestBase
    {
        private readonly OrderExecutionEngineCanonical _executionEngine;
        private readonly Mock<ICanonicalMessageQueue> _mockMessageQueue;

        public OrderValidationSecurityTests(ITestOutputHelper output) : base(output)
        {
            _mockMessageQueue = new Mock<ICanonicalMessageQueue>();
            _executionEngine = new OrderExecutionEngineCanonical(MockLogger.Object, _mockMessageQueue.Object);
            _executionEngine.InitializeAsync(TestCts.Token).Wait();
            _executionEngine.StartAsync(TestCts.Token).Wait();
        }

        [Theory]
        [MemberData(nameof(GetSqlInjectionTestData))]
        public async Task ValidateOrder_WithSqlInjection_ShouldReject(string maliciousInput)
        {
            // Arrange
            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = maliciousInput, // Inject in symbol
                OrderType = OrderType.Market,
                Side = OrderSide.Buy,
                Quantity = 100,
                Price = 150m,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var result = await _executionEngine.ValidateOrderAsync(order, TestCts.Token);

            // Assert
            if (result.IsSuccess)
            {
                // If validation passes, ensure the input is properly sanitized
                IsSqlInjectionSafe(order.Symbol).Should().BeTrue(
                    $"Order validation should reject or sanitize SQL injection: {maliciousInput}");
            }
            else
            {
                // Validation correctly rejected the malicious input
                result.Error!.Code.Should().Be("VALIDATION_ERROR");
            }
        }

        [Theory]
        [MemberData(nameof(GetXssTestData))]
        public async Task ValidateOrder_WithXssPayload_ShouldReject(string xssPayload)
        {
            // Arrange
            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = "AAPL",
                OrderType = OrderType.Limit,
                Side = OrderSide.Buy,
                Quantity = 100,
                Price = 150m,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                ClientOrderId = xssPayload // Inject in client order ID
            };

            // Act
            var result = await _executionEngine.ValidateOrderAsync(order, TestCts.Token);

            // Assert
            if (result.IsSuccess)
            {
                IsXssSafe(order.ClientOrderId).Should().BeTrue(
                    $"Order validation should reject or sanitize XSS: {xssPayload}");
            }
        }

        [Fact]
        public async Task ValidateOrder_WithNegativeQuantity_ShouldReject()
        {
            // Arrange
            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = "AAPL",
                OrderType = OrderType.Market,
                Side = OrderSide.Buy,
                Quantity = -100, // Negative quantity
                Price = 150m,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var result = await _executionEngine.ValidateOrderAsync(order, TestCts.Token);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error!.Message.Should().Contain("quantity");
        }

        [Fact]
        public async Task ValidateOrder_WithZeroPrice_ForLimitOrder_ShouldReject()
        {
            // Arrange
            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = "AAPL",
                OrderType = OrderType.Limit,
                Side = OrderSide.Buy,
                Quantity = 100,
                Price = 0m, // Zero price for limit order
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var result = await _executionEngine.ValidateOrderAsync(order, TestCts.Token);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error!.Message.Should().Contain("price");
        }

        [Fact]
        public async Task ValidateOrder_WithExcessiveQuantity_ShouldReject()
        {
            // Arrange
            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = "AAPL",
                OrderType = OrderType.Market,
                Side = OrderSide.Buy,
                Quantity = int.MaxValue, // Excessive quantity
                Price = 150m,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var result = await _executionEngine.ValidateOrderAsync(order, TestCts.Token);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error!.Message.Should().Contain("quantity");
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public async Task ValidateOrder_WithInvalidSymbol_ShouldReject(string? invalidSymbol)
        {
            // Arrange
            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = invalidSymbol!,
                OrderType = OrderType.Market,
                Side = OrderSide.Buy,
                Quantity = 100,
                Price = 150m,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var result = await _executionEngine.ValidateOrderAsync(order, TestCts.Token);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error!.Message.Should().Contain("symbol");
        }

        [Fact]
        public async Task ValidateOrder_WithFutureDatetime_ShouldReject()
        {
            // Arrange
            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = "AAPL",
                OrderType = OrderType.Market,
                Side = OrderSide.Buy,
                Quantity = 100,
                Price = 150m,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddDays(1) // Future date
            };

            // Act
            var result = await _executionEngine.ValidateOrderAsync(order, TestCts.Token);

            // Assert
            // Should either reject or adjust the timestamp
            if (result.IsSuccess)
            {
                order.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
            }
        }

        [Theory]
        [MemberData(nameof(GetPathTraversalTestData))]
        public async Task ValidateOrder_WithPathTraversal_InAccountId_ShouldReject(string pathTraversal)
        {
            // Arrange
            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = "AAPL",
                OrderType = OrderType.Market,
                Side = OrderSide.Buy,
                Quantity = 100,
                Price = 150m,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                AccountId = pathTraversal // Path traversal in account ID
            };

            // Act
            var result = await _executionEngine.ValidateOrderAsync(order, TestCts.Token);

            // Assert
            if (result.IsSuccess)
            {
                IsPathTraversalSafe(order.AccountId).Should().BeTrue(
                    $"Order validation should reject path traversal: {pathTraversal}");
            }
        }

        [Fact]
        public async Task ValidateOrder_WithUnicodeCharacters_ShouldHandleProperly()
        {
            // Arrange
            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = "AAPL",
                OrderType = OrderType.Market,
                Side = OrderSide.Buy,
                Quantity = 100,
                Price = 150m,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                ClientOrderId = "Test_🚀_Order_123" // Unicode characters
            };

            // Act
            var result = await _executionEngine.ValidateOrderAsync(order, TestCts.Token);

            // Assert
            // Should either accept or properly sanitize unicode
            result.IsSuccess.Should().BeTrue();
        }

        public static TheoryData<string> GetSqlInjectionTestData()
        {
            var data = new TheoryData<string>();
            foreach (var pattern in SqlInjectionPatterns)
            {
                data.Add(pattern);
            }
            return data;
        }

        public static TheoryData<string> GetXssTestData()
        {
            var data = new TheoryData<string>();
            foreach (var pattern in XssPatterns)
            {
                data.Add(pattern);
            }
            return data;
        }

        public static TheoryData<string> GetPathTraversalTestData()
        {
            var data = new TheoryData<string>();
            foreach (var pattern in PathTraversalPatterns)
            {
                data.Add(pattern);
            }
            return data;
        }

        public override void Dispose()
        {
            _executionEngine?.StopAsync(TestCts.Token).Wait();
            _executionEngine?.Dispose();
            base.Dispose();
        }
    }
}