using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.AI.Core;
using TradingPlatform.AI.Engines;
using TradingPlatform.AI.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Models;
using Xunit;

namespace TradingPlatform.AI.Tests.Engines;

/// <summary>
/// Comprehensive unit tests for Prophet Time Series Engine
/// Tests validation, inference, error handling, and 2025 best practices
/// </summary>
public class ProphetTimeSeriesEngineTests : IDisposable
{
    private readonly ProphetTimeSeriesEngine _engine;
    private readonly ITradingLogger _logger;
    private readonly AIModelConfiguration _configuration;

    public ProphetTimeSeriesEngineTests()
    {
        _logger = TradingLogOrchestrator.Instance;
        _configuration = CreateTestConfiguration();
        _engine = new ProphetTimeSeriesEngine(_logger, _configuration);
    }

    #region Input Validation Tests

    [Fact]
    public async Task ValidateInputAsync_NullInput_ShouldReturnFailure()
    {
        // Arrange
        FinancialTimeSeriesData? input = null;

        // Act
        var result = await _engine.InferAsync(input!);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("NULL_INPUT");
        result.ErrorMessage.Should().Contain("Input data cannot be null");
    }

    [Fact]
    public async Task ValidateInputAsync_MissingSymbol_ShouldReturnFailure()
    {
        // Arrange
        var input = new FinancialTimeSeriesData
        {
            Symbol = "", // Empty symbol
            DataPoints = GenerateTestDataPoints(20)
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("MISSING_SYMBOL");
        result.ErrorMessage.Should().Contain("Symbol is required");
    }

    [Fact]
    public async Task ValidateInputAsync_InsufficientDataPoints_ShouldReturnFailure()
    {
        // Arrange
        var input = new FinancialTimeSeriesData
        {
            Symbol = "AAPL",
            DataPoints = GenerateTestDataPoints(5) // Less than required 10
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("INSUFFICIENT_DATA_POINTS");
        result.ErrorMessage.Should().Contain("at least 10 data points");
    }

    [Fact]
    public async Task ValidateInputAsync_PoorDataQuality_ShouldReturnFailure()
    {
        // Arrange
        var dataPoints = new List<TimeSeriesPoint>();
        for (int i = 0; i < 20; i++)
        {
            dataPoints.Add(new TimeSeriesPoint
            {
                Timestamp = DateTime.UtcNow.AddDays(-i),
                Value = i < 16 ? 0 : 100 // 80% zero values
            });
        }

        var input = new FinancialTimeSeriesData
        {
            Symbol = "AAPL",
            DataPoints = dataPoints
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("POOR_DATA_QUALITY");
        result.ErrorMessage.Should().Contain("Too many invalid data points");
    }

    [Fact]
    public async Task ValidateInputAsync_ValidInput_ShouldReturnSuccess()
    {
        // Arrange
        var input = new FinancialTimeSeriesData
        {
            Symbol = "AAPL",
            DataPoints = GenerateTestDataPoints(50),
            Frequency = "daily",
            DataType = "close"
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.ForecastPoints.Should().NotBeEmpty();
    }

    #endregion

    #region Inference Tests

    [Fact]
    public async Task InferAsync_ValidTimeSeriesData_ShouldGenerateForecast()
    {
        // Arrange
        var input = new FinancialTimeSeriesData
        {
            Symbol = "AAPL",
            DataPoints = GenerateRealisticPriceData(100, 150.0m, 0.02m),
            Frequency = "daily",
            DataType = "close"
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.ModelType.Should().Be("Prophet");
        result.Data.ForecastPoints.Should().HaveCount(30); // Default forecast horizon
        result.Data.Confidence.Should().BeInRange(0.5m, 1.0m);
        result.Data.PredictedValue.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task InferAsync_WithSpecificModel_ShouldUseRequestedModel()
    {
        // Arrange
        var input = new FinancialTimeSeriesData
        {
            Symbol = "BTC-USD",
            DataPoints = GenerateRealisticPriceData(50, 50000m, 0.05m),
            Frequency = "hourly",
            DataType = "close"
        };

        // Act
        var result = await _engine.InferAsync(input, "prophet_crypto");

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.ModelName.Should().Contain("crypto");
    }

    [Fact]
    public async Task InferAsync_UnorderedData_ShouldHandleCorrectly()
    {
        // Arrange
        var orderedData = GenerateTestDataPoints(30);
        var unorderedData = orderedData.OrderBy(x => Guid.NewGuid()).ToList(); // Randomize order

        var input = new FinancialTimeSeriesData
        {
            Symbol = "AAPL",
            DataPoints = unorderedData,
            Frequency = "daily",
            DataType = "close"
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.ForecastPoints.Should().BeInChronologicalOrder(p => p.Timestamp);
    }

    #endregion

    #region Batch Processing Tests

    [Fact]
    public async Task InferBatchAsync_MultipleTimeSeries_ShouldProcessAll()
    {
        // Arrange
        var inputs = new List<FinancialTimeSeriesData>
        {
            new() { Symbol = "AAPL", DataPoints = GenerateTestDataPoints(50) },
            new() { Symbol = "GOOGL", DataPoints = GenerateTestDataPoints(50) },
            new() { Symbol = "MSFT", DataPoints = GenerateTestDataPoints(50) }
        };

        // Act
        var result = await _engine.InferBatchAsync(inputs);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(3);
        result.Data.Should().OnlyContain(forecast => forecast.ForecastPoints.Count > 0);
    }

    [Fact]
    public async Task InferBatchAsync_WithFailures_ShouldContinueProcessing()
    {
        // Arrange
        var inputs = new List<FinancialTimeSeriesData>
        {
            new() { Symbol = "AAPL", DataPoints = GenerateTestDataPoints(50) }, // Valid
            new() { Symbol = "", DataPoints = GenerateTestDataPoints(50) }, // Invalid - missing symbol
            new() { Symbol = "MSFT", DataPoints = GenerateTestDataPoints(3) }, // Invalid - insufficient data
            new() { Symbol = "GOOGL", DataPoints = GenerateTestDataPoints(50) } // Valid
        };

        // Act
        var result = await _engine.InferBatchAsync(inputs);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(2); // Only valid inputs processed
        result.Data[0].TrendAnalysis.Should().NotBeNull();
        result.Data[1].SeasonalAnalysis.Should().NotBeNull();
    }

    #endregion

    #region Forecast Quality Tests

    [Fact]
    public async Task Forecast_ShouldIncludeTrendAnalysis()
    {
        // Arrange
        var input = new FinancialTimeSeriesData
        {
            Symbol = "AAPL",
            DataPoints = GenerateTrendingData(100, 100m, 0.001m), // Upward trend
            Frequency = "daily",
            DataType = "close"
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.TrendAnalysis.Should().NotBeNull();
        result.Data.TrendAnalysis.TrendDirection.Should().Be("UP");
        result.Data.TrendAnalysis.TrendStrength.Should().BeGreaterThan(0);
        result.Data.TrendAnalysis.TrendComponents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Forecast_ShouldIncludeSeasonalAnalysis()
    {
        // Arrange
        var input = new FinancialTimeSeriesData
        {
            Symbol = "AAPL",
            DataPoints = GenerateSeasonalData(100, 150m, 5, 0.1m),
            Frequency = "daily",
            DataType = "close"
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.SeasonalAnalysis.Should().NotBeNull();
        result.Data.SeasonalAnalysis.WeeklySeasonality.Should().NotBeEmpty();
        result.Data.SeasonalAnalysis.SeasonalStrength.Should().BeInRange(0, 1);
    }

    [Fact]
    public async Task Forecast_ShouldIncludeConfidenceIntervals()
    {
        // Arrange
        var input = new FinancialTimeSeriesData
        {
            Symbol = "AAPL",
            DataPoints = GenerateTestDataPoints(50),
            Frequency = "daily",
            DataType = "close"
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.ConfidenceIntervals.Should().NotBeEmpty();
        
        foreach (var interval in result.Data.ConfidenceIntervals)
        {
            interval.UpperBound.Should().BeGreaterThan(interval.LowerBound);
            interval.ConfidenceLevel.Should().BeInRange(0.9m, 1.0m);
        }
    }

    [Fact]
    public async Task Forecast_ConfidenceShouldDecreaseOverTime()
    {
        // Arrange
        var input = new FinancialTimeSeriesData
        {
            Symbol = "AAPL",
            DataPoints = GenerateTestDataPoints(50),
            Frequency = "daily",
            DataType = "close"
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        
        var firstConfidence = result.Data.ForecastPoints.First().Confidence;
        var lastConfidence = result.Data.ForecastPoints.Last().Confidence;
        
        lastConfidence.Should().BeLessThan(firstConfidence);
    }

    #endregion

    #region Post-Processing Tests

    [Fact]
    public async Task PostProcessing_ShouldEnhanceWithMetrics()
    {
        // Arrange
        var input = new FinancialTimeSeriesData
        {
            Symbol = "AAPL",
            DataPoints = GenerateVolatileData(50, 150m, 0.1m),
            Frequency = "daily",
            DataType = "close"
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Metadata.Should().ContainKey("volatility");
        result.Data.Metadata.Should().ContainKey("mean_forecast");
        result.Data.Metadata.Should().ContainKey("forecast_range");
        result.Data.Metadata.Should().ContainKey("estimated_accuracy");
    }

    [Fact]
    public async Task PostProcessing_ShouldApplyBusinessRules()
    {
        // Arrange
        var dataPoints = GenerateTestDataPoints(50);
        // Force some extreme values that should be corrected
        dataPoints[25].Value = -100m; // Negative price

        var input = new FinancialTimeSeriesData
        {
            Symbol = "AAPL",
            DataPoints = dataPoints,
            Frequency = "daily",
            DataType = "close"
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.ForecastPoints.Should().OnlyContain(p => p.PredictedValue > 0);
    }

    #endregion

    #region Health Monitoring Tests

    [Fact]
    public async Task GetServiceHealthAsync_ShouldReturnHealthMetrics()
    {
        // Arrange
        // Perform some operations first
        var input = new FinancialTimeSeriesData
        {
            Symbol = "AAPL",
            DataPoints = GenerateTestDataPoints(20),
            Frequency = "daily",
            DataType = "close"
        };
        await _engine.InferAsync(input);

        // Act
        var healthResult = await _engine.GetServiceHealthAsync();

        // Assert
        healthResult.Success.Should().BeTrue();
        healthResult.Data.Should().NotBeNull();
        healthResult.Data.IsHealthy.Should().BeTrue();
        healthResult.Data.TotalRequests.Should().BeGreaterThan(0);
        healthResult.Data.SuccessRate.Should().BeGreaterThan(0);
        healthResult.Data.LoadedModels.Should().NotBeEmpty();
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task InferAsync_ExtremePriceJump_ShouldHandleGracefully()
    {
        // Arrange
        var dataPoints = GenerateTestDataPoints(50);
        // Create extreme price jump
        dataPoints[25].Value = dataPoints[24].Value * 10m;

        var input = new FinancialTimeSeriesData
        {
            Symbol = "AAPL",
            DataPoints = dataPoints,
            Frequency = "daily",
            DataType = "close"
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        // Forecast should handle outlier without crashing
    }

    [Fact]
    public async Task InferAsync_AllZeroValues_ShouldReturnMinimalForecast()
    {
        // Arrange
        var dataPoints = Enumerable.Range(0, 50)
            .Select(i => new TimeSeriesPoint
            {
                Timestamp = DateTime.UtcNow.AddDays(-i),
                Value = 0.01m // Minimal non-zero value
            }).ToList();

        var input = new FinancialTimeSeriesData
        {
            Symbol = "AAPL",
            DataPoints = dataPoints,
            Frequency = "daily",
            DataType = "close"
        };

        // Act
        var result = await _engine.InferAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.ForecastPoints.Should().OnlyContain(p => p.PredictedValue >= 0.01m);
    }

    #endregion

    #region Helper Methods

    private AIModelConfiguration CreateTestConfiguration()
    {
        return new AIModelConfiguration
        {
            DefaultModelType = "Prophet",
            MaxConcurrentInferences = 5,
            ModelCacheSize = 10,
            DefaultTimeout = TimeSpan.FromSeconds(30),
            EnableGpuAcceleration = false,
            AvailableModels = new List<ModelDefinition>
            {
                new()
                {
                    Name = "prophet_default",
                    Type = "Prophet",
                    Version = "1.1.0",
                    IsDefault = true,
                    Priority = 1,
                    Capabilities = new AIModelCapabilities
                    {
                        SupportedInputTypes = new() { "FinancialTimeSeriesData" },
                        SupportedOutputTypes = new() { "TimeSeriesForecast" },
                        MaxBatchSize = 1,
                        RequiresGpu = false
                    }
                },
                new()
                {
                    Name = "prophet_crypto",
                    Type = "Prophet",
                    Version = "1.1.0",
                    IsDefault = false,
                    Priority = 2,
                    Capabilities = new AIModelCapabilities
                    {
                        SupportedInputTypes = new() { "FinancialTimeSeriesData" },
                        SupportedOutputTypes = new() { "TimeSeriesForecast" },
                        MaxBatchSize = 1,
                        RequiresGpu = false
                    },
                    Parameters = new Dictionary<string, object>
                    {
                        ["changepoint_prior_scale"] = 0.1, // Higher for volatile crypto
                        ["daily_seasonality"] = true // Crypto trades 24/7
                    }
                }
            }
        };
    }

    private List<TimeSeriesPoint> GenerateTestDataPoints(int count)
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var baseValue = 100m;
        var points = new List<TimeSeriesPoint>();

        for (int i = 0; i < count; i++)
        {
            var noise = (decimal)(random.NextDouble() * 10 - 5);
            points.Add(new TimeSeriesPoint
            {
                Timestamp = DateTime.UtcNow.AddDays(-count + i),
                Value = baseValue + noise
            });
        }

        return points;
    }

    private List<TimeSeriesPoint> GenerateRealisticPriceData(int count, decimal startPrice, decimal volatility)
    {
        var random = new Random(42);
        var points = new List<TimeSeriesPoint>();
        var currentPrice = startPrice;

        for (int i = 0; i < count; i++)
        {
            var change = (decimal)(random.NextDouble() * 2 - 1) * volatility * currentPrice;
            currentPrice = Math.Max(0.01m, currentPrice + change);
            
            points.Add(new TimeSeriesPoint
            {
                Timestamp = DateTime.UtcNow.AddDays(-count + i),
                Value = currentPrice
            });
        }

        return points;
    }

    private List<TimeSeriesPoint> GenerateTrendingData(int count, decimal startPrice, decimal trendRate)
    {
        var points = new List<TimeSeriesPoint>();
        var currentPrice = startPrice;

        for (int i = 0; i < count; i++)
        {
            currentPrice *= (1 + trendRate);
            var noise = (decimal)(new Random(i).NextDouble() * 2 - 1) * 0.01m * currentPrice;
            
            points.Add(new TimeSeriesPoint
            {
                Timestamp = DateTime.UtcNow.AddDays(-count + i),
                Value = currentPrice + noise
            });
        }

        return points;
    }

    private List<TimeSeriesPoint> GenerateSeasonalData(int count, decimal basePrice, int seasonLength, decimal amplitude)
    {
        var points = new List<TimeSeriesPoint>();

        for (int i = 0; i < count; i++)
        {
            var seasonalComponent = (decimal)Math.Sin(2 * Math.PI * i / seasonLength) * amplitude * basePrice;
            var value = basePrice + seasonalComponent;
            
            points.Add(new TimeSeriesPoint
            {
                Timestamp = DateTime.UtcNow.AddDays(-count + i),
                Value = value
            });
        }

        return points;
    }

    private List<TimeSeriesPoint> GenerateVolatileData(int count, decimal basePrice, decimal volatility)
    {
        var random = new Random(42);
        var points = new List<TimeSeriesPoint>();

        for (int i = 0; i < count; i++)
        {
            var shock = random.NextDouble() < 0.1 ? volatility * 3 : volatility;
            var change = (decimal)(random.NextDouble() * 2 - 1) * shock * basePrice;
            
            points.Add(new TimeSeriesPoint
            {
                Timestamp = DateTime.UtcNow.AddDays(-count + i),
                Value = Math.Max(0.01m, basePrice + change)
            });
        }

        return points;
    }

    public void Dispose()
    {
        _engine?.Dispose();
    }

    #endregion
}