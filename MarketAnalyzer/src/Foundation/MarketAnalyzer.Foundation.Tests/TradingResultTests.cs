using FluentAssertions;
using MarketAnalyzer.Foundation;

namespace MarketAnalyzer.Foundation.Tests;

public class TradingResultTests
{
    [Fact]
    public void Success_Should_Create_Successful_Result()
    {
        // Arrange
        const decimal price = 123.45m;

        // Act
        var result = TradingResult<decimal>.Success(price);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(price);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_Should_Create_Failed_Result()
    {
        // Arrange
        const string errorCode = "MARKET_DATA_UNAVAILABLE";
        const string message = "Market data service is down";

        // Act
        var result = TradingResult<decimal>.Failure(errorCode, message);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Value.Should().Be(default(decimal));
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(errorCode);
        result.Error.Message.Should().Be(message);
    }

    [Fact]
    public void Implicit_Conversion_Should_Create_Successful_Result()
    {
        // Arrange
        const decimal price = 456.78m;

        // Act
        TradingResult<decimal> result = price;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(price);
    }

    [Fact]
    public void FromT_Should_Create_Successful_Result()
    {
        // Arrange
        const decimal price = 789.01m;

        // Act
        var result = TradingResult<decimal>.FromT(price);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(price);
    }

    [Fact]
    public void Map_Should_Transform_Successful_Result()
    {
        // Arrange
        var result = TradingResult<decimal>.Success(100.0m);

        // Act
        var mappedResult = result.Map(x => x * 2);

        // Assert
        mappedResult.IsSuccess.Should().BeTrue();
        mappedResult.Value.Should().Be(200.0m);
    }

    [Fact]
    public void Map_Should_Preserve_Failure()
    {
        // Arrange
        var result = TradingResult<decimal>.Failure("ERROR", "Test error");

        // Act
        var mappedResult = result.Map(x => x * 2);

        // Assert
        mappedResult.IsFailure.Should().BeTrue();
        mappedResult.Error!.Code.Should().Be("ERROR");
    }

    [Fact]
    public void OnSuccess_Should_Execute_Action_For_Successful_Result()
    {
        // Arrange
        var result = TradingResult<decimal>.Success(100.0m);
        var executed = false;

        // Act
        result.OnSuccess(_ => executed = true);

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public void OnSuccess_Should_Not_Execute_Action_For_Failed_Result()
    {
        // Arrange
        var result = TradingResult<decimal>.Failure("ERROR", "Test error");
        var executed = false;

        // Act
        result.OnSuccess(_ => executed = true);

        // Assert
        executed.Should().BeFalse();
    }

    [Fact]
    public void OnFailure_Should_Execute_Action_For_Failed_Result()
    {
        // Arrange
        var result = TradingResult<decimal>.Failure("ERROR", "Test error");
        var executed = false;

        // Act
        result.OnFailure(_ => executed = true);

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public void OnFailure_Should_Not_Execute_Action_For_Successful_Result()
    {
        // Arrange
        var result = TradingResult<decimal>.Success(100.0m);
        var executed = false;

        // Act
        result.OnFailure(_ => executed = true);

        // Assert
        executed.Should().BeFalse();
    }
}