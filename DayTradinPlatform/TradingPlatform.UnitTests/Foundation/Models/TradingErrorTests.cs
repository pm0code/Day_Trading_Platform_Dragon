using System;
using FluentAssertions;
using TradingPlatform.Core.Models;
using Xunit;

namespace TradingPlatform.UnitTests.Foundation.Models
{
    public class TradingErrorTests
    {
        [Fact]
        public void Constructor_WithCodeAndMessage_CreatesError()
        {
            // Arrange
            const string code = "TEST001";
            const string message = "Test error message";

            // Act
            var error = new TradingError(code, message);

            // Assert
            error.Code.Should().Be(code);
            error.Message.Should().Be(message);
            error.Details.Should().BeNull();
            error.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void Constructor_WithDetails_CreatesErrorWithDetails()
        {
            // Arrange
            const string code = "TEST001";
            const string message = "Test error message";
            var details = new { Field = "Price", Value = 100m };

            // Act
            var error = new TradingError(code, message, details);

            // Assert
            error.Code.Should().Be(code);
            error.Message.Should().Be(message);
            error.Details.Should().NotBeNull();
            error.Details.Should().BeEquivalentTo(details);
        }

        [Fact]
        public void General_CreatesGeneralError()
        {
            // Arrange
            const string message = "General error occurred";

            // Act
            var error = TradingError.General(message);

            // Assert
            error.Code.Should().Be("GENERAL_ERROR");
            error.Message.Should().Be(message);
        }

        [Fact]
        public void Validation_CreatesValidationError()
        {
            // Arrange
            const string message = "Validation failed";
            var details = new { Field = "Quantity", Error = "Must be positive" };

            // Act
            var error = TradingError.Validation(message, details);

            // Assert
            error.Code.Should().Be("VALIDATION_ERROR");
            error.Message.Should().Be(message);
            error.Details.Should().BeEquivalentTo(details);
        }

        [Fact]
        public void NotFound_CreatesNotFoundError()
        {
            // Arrange
            const string resource = "Order";
            const string id = "12345";

            // Act
            var error = TradingError.NotFound(resource, id);

            // Assert
            error.Code.Should().Be("NOT_FOUND");
            error.Message.Should().Be($"{resource} with ID '{id}' not found");
        }

        [Fact]
        public void Unauthorized_CreatesUnauthorizedError()
        {
            // Arrange
            const string message = "Access denied";

            // Act
            var error = TradingError.Unauthorized(message);

            // Assert
            error.Code.Should().Be("UNAUTHORIZED");
            error.Message.Should().Be(message);
        }

        [Fact]
        public void RateLimited_CreatesRateLimitError()
        {
            // Arrange
            const string resource = "AlphaVantage API";
            var retryAfter = TimeSpan.FromSeconds(60);

            // Act
            var error = TradingError.RateLimited(resource, retryAfter);

            // Assert
            error.Code.Should().Be("RATE_LIMITED");
            error.Message.Should().Contain(resource);
            error.Message.Should().Contain("60 seconds");
            error.Details.Should().NotBeNull();
        }

        [Fact]
        public void Timeout_CreatesTimeoutError()
        {
            // Arrange
            const string operation = "Market data fetch";
            var timeout = TimeSpan.FromSeconds(30);

            // Act
            var error = TradingError.Timeout(operation, timeout);

            // Assert
            error.Code.Should().Be("TIMEOUT");
            error.Message.Should().Be($"{operation} timed out after 30 seconds");
        }

        [Fact]
        public void ServiceUnavailable_CreatesServiceError()
        {
            // Arrange
            const string service = "Market Data Service";
            const string reason = "Connection failed";

            // Act
            var error = TradingError.ServiceUnavailable(service, reason);

            // Assert
            error.Code.Should().Be("SERVICE_UNAVAILABLE");
            error.Message.Should().Be($"{service} is unavailable: {reason}");
        }

        [Fact]
        public void InvalidOperation_CreatesInvalidOperationError()
        {
            // Arrange
            const string operation = "Place order";
            const string reason = "Market is closed";

            // Act
            var error = TradingError.InvalidOperation(operation, reason);

            // Assert
            error.Code.Should().Be("INVALID_OPERATION");
            error.Message.Should().Be($"Cannot {operation}: {reason}");
        }

        [Fact]
        public void Configuration_CreatesConfigurationError()
        {
            // Arrange
            const string setting = "API_KEY";
            const string issue = "Missing or invalid";

            // Act
            var error = TradingError.Configuration(setting, issue);

            // Assert
            error.Code.Should().Be("CONFIGURATION_ERROR");
            error.Message.Should().Be($"Configuration error for {setting}: {issue}");
        }

        [Fact]
        public void ToString_ReturnsFormattedError()
        {
            // Arrange
            var error = new TradingError("TEST001", "Test error", new { Debug = true });

            // Act
            var result = error.ToString();

            // Assert
            result.Should().Contain("TEST001");
            result.Should().Contain("Test error");
            result.Should().Contain(error.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        [Theory]
        [InlineData("", "Message", "Code cannot be empty")]
        [InlineData(null, "Message", "Code cannot be empty")]
        [InlineData("CODE", "", "Message cannot be empty")]
        [InlineData("CODE", null, "Message cannot be empty")]
        public void Constructor_WithInvalidParameters_ThrowsException(string code, string message, string expectedError)
        {
            // Act & Assert
            var action = () => new TradingError(code!, message!);
            action.Should().Throw<ArgumentException>()
                .WithMessage($"*{expectedError}*");
        }

        [Fact]
        public void WithDetails_AddsDetailsToError()
        {
            // Arrange
            var error = new TradingError("TEST001", "Test error");
            var details = new { Field = "Symbol", Value = "AAPL" };

            // Act
            var errorWithDetails = error.WithDetails(details);

            // Assert
            errorWithDetails.Should().NotBeSameAs(error);
            errorWithDetails.Code.Should().Be(error.Code);
            errorWithDetails.Message.Should().Be(error.Message);
            errorWithDetails.Details.Should().BeEquivalentTo(details);
        }

        [Fact]
        public void Equals_WithSameCodeAndMessage_ReturnsTrue()
        {
            // Arrange
            var error1 = new TradingError("TEST001", "Test message");
            var error2 = new TradingError("TEST001", "Test message");

            // Act & Assert
            error1.Should().Be(error2);
            error1.GetHashCode().Should().Be(error2.GetHashCode());
        }

        [Fact]
        public void Equals_WithDifferentCode_ReturnsFalse()
        {
            // Arrange
            var error1 = new TradingError("TEST001", "Test message");
            var error2 = new TradingError("TEST002", "Test message");

            // Act & Assert
            error1.Should().NotBe(error2);
        }
    }
}