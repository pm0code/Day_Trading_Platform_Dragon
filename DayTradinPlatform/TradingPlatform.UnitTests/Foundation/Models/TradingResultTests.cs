using System;
using FluentAssertions;
using TradingPlatform.Core.Models;
using TradingPlatform.UnitTests.Extensions;
using Xunit;

namespace TradingPlatform.UnitTests.Foundation.Models
{
    public class TradingResultTests
    {
        [Fact]
        public void Success_WithValue_CreatesSuccessResult()
        {
            // Arrange
            const string expectedValue = "test value";

            // Act
            var result = TradingResult<string>.Success(expectedValue);

            // Assert
            result.Should().BeSuccess();
            result.Should().HaveValue(expectedValue);
            result.Error.Should().BeNull();
        }

        [Fact]
        public void Success_WithoutValue_CreatesSuccessResult()
        {
            // Act
            var result = TradingResult.Success();

            // Assert
            result.Should().BeSuccess();
            result.Error.Should().BeNull();
        }

        [Fact]
        public void Failure_WithError_CreatesFailureResult()
        {
            // Arrange
            var error = new TradingError("TEST001", "Test error message");

            // Act
            var result = TradingResult<string>.Failure(error);

            // Assert
            result.Should().BeFailure();
            result.Should().HaveError("Test error message");
            result.Should().HaveErrorCode("TEST001");
            result.Invoking(r => r.Value).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Failure_WithMessage_CreatesFailureResult()
        {
            // Arrange
            const string message = "Operation failed";

            // Act
            var result = TradingResult.Failure(message);

            // Assert
            result.Should().BeFailure();
            result.Should().HaveError(message);
            result.Error!.Code.Should().Be("GENERAL_ERROR");
        }

        [Fact]
        public void Map_OnSuccess_TransformsValue()
        {
            // Arrange
            var result = TradingResult<int>.Success(42);

            // Act
            var mappedResult = result.Map(x => x.ToString());

            // Assert
            mappedResult.Should().BeSuccess();
            mappedResult.Should().HaveValue("42");
        }

        [Fact]
        public void Map_OnFailure_PropagatesError()
        {
            // Arrange
            var error = new TradingError("ERR001", "Original error");
            var result = TradingResult<int>.Failure(error);

            // Act
            var mappedResult = result.Map(x => x.ToString());

            // Assert
            mappedResult.Should().BeFailure();
            mappedResult.Should().HaveError("Original error");
            mappedResult.Should().HaveErrorCode("ERR001");
        }

        [Fact]
        public void Bind_OnSuccess_ChainsOperations()
        {
            // Arrange
            var result = TradingResult<int>.Success(10);

            // Act
            var bindResult = result.Bind(x => 
                x > 5 
                    ? TradingResult<string>.Success($"Value {x} is valid") 
                    : TradingResult<string>.Failure("Value too small"));

            // Assert
            bindResult.Should().BeSuccess();
            bindResult.Should().HaveValue("Value 10 is valid");
        }

        [Fact]
        public void Bind_OnFailure_PropagatesError()
        {
            // Arrange
            var error = new TradingError("ERR001", "Original error");
            var result = TradingResult<int>.Failure(error);

            // Act
            var bindResult = result.Bind(x => TradingResult<string>.Success(x.ToString()));

            // Assert
            bindResult.Should().BeFailure();
            bindResult.Should().HaveError("Original error");
        }

        [Fact]
        public void Match_ExecutesCorrectBranch()
        {
            // Arrange
            var successResult = TradingResult<int>.Success(42);
            var failureResult = TradingResult<int>.Failure("Error");

            // Act
            var successValue = successResult.Match(
                onSuccess: value => value * 2,
                onFailure: error => -1);

            var failureValue = failureResult.Match(
                onSuccess: value => value * 2,
                onFailure: error => -1);

            // Assert
            successValue.Should().Be(84);
            failureValue.Should().Be(-1);
        }

        [Fact]
        public void OnSuccess_ExecutesActionOnSuccess()
        {
            // Arrange
            var result = TradingResult<int>.Success(42);
            var actionExecuted = false;
            var capturedValue = 0;

            // Act
            result.OnSuccess(value =>
            {
                actionExecuted = true;
                capturedValue = value;
            });

            // Assert
            actionExecuted.Should().BeTrue();
            capturedValue.Should().Be(42);
        }

        [Fact]
        public void OnFailure_ExecutesActionOnFailure()
        {
            // Arrange
            var error = new TradingError("ERR001", "Test error");
            var result = TradingResult<int>.Failure(error);
            var actionExecuted = false;
            TradingError? capturedError = null;

            // Act
            result.OnFailure(err =>
            {
                actionExecuted = true;
                capturedError = err;
            });

            // Assert
            actionExecuted.Should().BeTrue();
            capturedError.Should().NotBeNull();
            capturedError!.Code.Should().Be("ERR001");
        }

        [Fact]
        public void ImplicitConversion_FromValue_CreatesSuccess()
        {
            // Act
            TradingResult<string> result = "test value";

            // Assert
            result.Should().BeSuccess();
            result.Should().HaveValue("test value");
        }

        [Fact]
        public void ImplicitConversion_FromError_CreatesFailure()
        {
            // Arrange
            var error = new TradingError("ERR001", "Test error");

            // Act
            TradingResult<string> result = error;

            // Assert
            result.Should().BeFailure();
            result.Should().HaveErrorCode("ERR001");
        }

        [Fact]
        public void Value_OnFailure_ThrowsInvalidOperationException()
        {
            // Arrange
            var result = TradingResult<string>.Failure("Error");

            // Act & Assert
            result.Invoking(r => r.Value)
                .Should().Throw<InvalidOperationException>()
                .WithMessage("Cannot access Value on a failed result*");
        }

        [Fact]
        public void GetValueOrDefault_ReturnsValueOnSuccess()
        {
            // Arrange
            var result = TradingResult<int>.Success(42);

            // Act
            var value = result.GetValueOrDefault(0);

            // Assert
            value.Should().Be(42);
        }

        [Fact]
        public void GetValueOrDefault_ReturnsDefaultOnFailure()
        {
            // Arrange
            var result = TradingResult<int>.Failure("Error");

            // Act
            var value = result.GetValueOrDefault(99);

            // Assert
            value.Should().Be(99);
        }
    }
}