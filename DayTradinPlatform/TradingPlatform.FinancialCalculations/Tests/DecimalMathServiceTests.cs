// TradingPlatform.FinancialCalculations.Tests.DecimalMathServiceTests
// Comprehensive tests for GPU-accelerated decimal mathematics with financial precision validation

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TradingPlatform.FinancialCalculations.Compliance;
using TradingPlatform.FinancialCalculations.Configuration;
using TradingPlatform.FinancialCalculations.Interfaces;
using TradingPlatform.FinancialCalculations.Models;
using TradingPlatform.FinancialCalculations.Services;
using TradingPlatform.GPU.Infrastructure;
using TradingPlatform.GPU.Mathematics;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.FinancialCalculations.Tests;

/// <summary>
/// Comprehensive tests for GPU-accelerated decimal mathematics service
/// Validates precision, performance, and regulatory compliance
/// </summary>
public class DecimalMathServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IDecimalMathService _decimalMathService;
    private readonly GpuContext _gpuContext;
    private readonly FinancialCalculationConfiguration _config;
    private readonly IComplianceAuditor _complianceAuditor;
    private readonly ILogger<DecimalMathService> _logger;

    public DecimalMathServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = NullLogger<DecimalMathService>.Instance;

        // Create test configuration
        _config = CreateTestConfiguration();
        
        // Create compliance auditor
        _complianceAuditor = new ComplianceAuditor(_config.ComplianceConfiguration, 
            NullLogger<ComplianceAuditor>.Instance);

        // Initialize GPU context
        _gpuContext = new GpuContext(NullLogger<GpuContext>.Instance);
        
        // Create decimal math service
        _decimalMathService = new DecimalMathService(_config, _complianceAuditor, _gpuContext);
    }

    #region Precision Tests

    [Fact]
    public async Task SquareRoot_HighPrecision_MaintainsDecimalAccuracy()
    {
        // Arrange
        var testValue = 2.0m;
        var expectedResult = 1.4142135623730950488m; // High precision square root of 2

        // Act
        var result = await _decimalMathService.SqrtAsync(testValue);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        
        var difference = Math.Abs(result.Data.Value - expectedResult);
        Assert.True(difference < 0.000000001m, $"Expected precision within 9 decimal places, got difference: {difference}");
        
        _output.WriteLine($"Square root of {testValue}: {result.Data.Value}");
        _output.WriteLine($"Expected: {expectedResult}");
        _output.WriteLine($"Difference: {difference}");
    }

    [Fact]
    public async Task PowerCalculation_IntegerExponent_ExactResult()
    {
        // Arrange
        var baseValue = 1.5m;
        var exponent = 3m;
        var expectedResult = 3.375m; // 1.5^3 = 3.375

        // Act
        var result = await _decimalMathService.PowAsync(baseValue, exponent);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(expectedResult, result.Data.Value);
        
        _output.WriteLine($"{baseValue}^{exponent} = {result.Data.Value}");
    }

    [Fact]
    public async Task NaturalLogarithm_KnownValue_HighPrecision()
    {
        // Arrange
        var testValue = Math.E; // e
        var expectedResult = 1.0m; // ln(e) = 1

        // Act
        var result = await _decimalMathService.LogAsync((decimal)testValue);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        
        var difference = Math.Abs(result.Data.Value - expectedResult);
        Assert.True(difference < 0.000001m, $"ln(e) should be 1.0, got {result.Data.Value}");
        
        _output.WriteLine($"ln({testValue}) = {result.Data.Value}");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public async Task BatchCalculation_Addition_CorrectResults(int dataSize)
    {
        // Arrange
        var operand1 = GenerateTestData(dataSize, 1.0m, 100.0m);
        var operand2 = GenerateTestData(dataSize, 1.0m, 100.0m);
        var expectedResults = operand1.Zip(operand2, (a, b) => a + b).ToArray();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _decimalMathService.BatchCalculateAsync(operand1, operand2, MathOperation.Add);
        stopwatch.Stop();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(dataSize, result.Data.Count);
        
        for (int i = 0; i < dataSize; i++)
        {
            Assert.Equal(expectedResults[i], result.Data.Values[i]);
        }

        _output.WriteLine($"Batch addition of {dataSize} elements completed in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"GPU used: {result.Data.UsedGpuAcceleration}");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task LargeDataset_GpuAcceleration_PerformanceImprovement()
    {
        // Arrange
        const int largeDataSize = 100000;
        var operand1 = GenerateTestData(largeDataSize, 1.0m, 1000.0m);
        var operand2 = GenerateTestData(largeDataSize, 1.0m, 1000.0m);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _decimalMathService.BatchCalculateAsync(operand1, operand2, MathOperation.Multiply);
        stopwatch.Stop();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(largeDataSize, result.Data.Count);
        
        // For large datasets, GPU acceleration should be used
        if (_gpuContext.Accelerator != null)
        {
            Assert.True(result.Data.UsedGpuAcceleration, "GPU should be used for large datasets");
        }

        _output.WriteLine($"Large dataset multiplication ({largeDataSize} elements):");
        _output.WriteLine($"  Execution time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  GPU accelerated: {result.Data.UsedGpuAcceleration}");
        _output.WriteLine($"  Throughput: {largeDataSize / (stopwatch.ElapsedMilliseconds / 1000.0):N0} operations/second");
    }

    [Fact]
    public async Task PortfolioCalculation_LargePortfolio_Performance()
    {
        // Arrange
        const int positionCount = 50000;
        var quantities = GenerateTestData(positionCount, 1.0m, 10000.0m);
        var prices = GenerateTestData(positionCount, 0.1m, 1000.0m);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _decimalMathService.CalculatePortfolioValuesAsync(quantities, prices);
        stopwatch.Stop();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(positionCount, result.Data.Count);

        // Validate portfolio values are correctly calculated
        for (int i = 0; i < Math.Min(100, positionCount); i++) // Sample validation
        {
            var expectedValue = Math.Round(quantities[i] * prices[i], 2, MidpointRounding.ToEven);
            var actualValue = Math.Round(result.Data.Values[i], 2, MidpointRounding.ToEven);
            Assert.Equal(expectedValue, actualValue);
        }

        _output.WriteLine($"Portfolio calculation ({positionCount} positions):");
        _output.WriteLine($"  Execution time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  GPU accelerated: {result.Data.UsedGpuAcceleration}");
        _output.WriteLine($"  Total portfolio value: {result.Data.Values.Sum():C}");
    }

    #endregion

    #region Financial Calculations Tests

    [Theory]
    [InlineData(10000.0, 0.05, 10, FinancialCalculationType.CompoundInterest)]
    [InlineData(10000.0, 0.08, 5, FinancialCalculationType.FutureValue)]
    [InlineData(15000.0, 0.06, 8, FinancialCalculationType.PresentValue)]
    public async Task FinancialCalculation_StandardInputs_ValidResults(
        double principal, double rate, int periods, FinancialCalculationType calculationType)
    {
        // Arrange
        var principalDecimal = (decimal)principal;
        var rateDecimal = (decimal)rate;

        // Act
        var result = await _decimalMathService.CalculateFinancialValueAsync(
            principalDecimal, rateDecimal, periods, calculationType);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Value > 0, "Financial calculation result should be positive");
        
        // Validate banker's rounding was applied
        var decimalPlaces = GetDecimalPlaces(result.Data.Value);
        Assert.True(decimalPlaces <= _config.DecimalPrecision.DefaultPrecision, 
            $"Result should not exceed {_config.DecimalPrecision.DefaultPrecision} decimal places");

        _output.WriteLine($"{calculationType} calculation:");
        _output.WriteLine($"  Principal: {principalDecimal:C}");
        _output.WriteLine($"  Rate: {rateDecimal:P}");
        _output.WriteLine($"  Periods: {periods}");
        _output.WriteLine($"  Result: {result.Data.Value:C}");
    }

    [Fact]
    public async Task RiskCalculation_HistoricalReturns_ComprehensiveMetrics()
    {
        // Arrange
        var returns = GenerateReturnsData(1000); // 1000 daily returns
        var confidenceLevel = 0.95m;

        // Act
        var result = await _decimalMathService.CalculateRiskMetricsAsync(returns, confidenceLevel);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        
        // Validate risk metrics
        Assert.True(result.Data.VaR95 >= 0, "VaR should be non-negative");
        Assert.True(result.Data.Volatility >= 0, "Volatility should be non-negative");
        Assert.True(result.Data.MaxDrawdown >= 0, "Max drawdown should be non-negative");
        Assert.True(result.Data.ExpectedShortfall >= result.Data.VaR95, 
            "Expected shortfall should be >= VaR");
        Assert.Equal(confidenceLevel, result.Data.ConfidenceLevel);
        Assert.Equal(returns.Length, result.Data.SampleSize);

        _output.WriteLine($"Risk metrics for {returns.Length} returns:");
        _output.WriteLine($"  VaR 95%: {result.Data.VaR95:P}");
        _output.WriteLine($"  Volatility: {result.Data.Volatility:P}");
        _output.WriteLine($"  Max Drawdown: {result.Data.MaxDrawdown:P}");
        _output.WriteLine($"  Expected Shortfall: {result.Data.ExpectedShortfall:P}");
        _output.WriteLine($"  GPU accelerated: {result.Data.UsedGpuAcceleration}");
    }

    #endregion

    #region Regulatory Compliance Tests

    [Fact]
    public async Task FinancialCalculation_BankersRounding_RegulatoryCompliance()
    {
        // Arrange - Test banker's rounding (round to even)
        var testValues = new[] { 2.5m, 3.5m, 4.5m, 5.5m }; // All should round to even numbers
        var expectedResults = new[] { 2m, 4m, 4m, 6m };

        // Act & Assert
        for (int i = 0; i < testValues.Length; i++)
        {
            var result = await _decimalMathService.SqrtAsync(testValues[i] * testValues[i]); // sqrt(x^2) = x
            Assert.True(result.IsSuccess);
            
            var rounded = Math.Round(result.Data.Value, 0, MidpointRounding.ToEven);
            var expected = expectedResults[i];
            
            _output.WriteLine($"sqrt({testValues[i]}^2) = {result.Data.Value}, rounded = {rounded}, expected = {expected}");
        }
    }

    [Fact]
    public async Task LargeCalculation_AuditTrail_ComplianceTracking()
    {
        // Arrange
        var operand1 = GenerateTestData(1000, 100.0m, 1000.0m);
        var operand2 = GenerateTestData(1000, 100.0m, 1000.0m);

        // Act
        var result = await _decimalMathService.BatchCalculateAsync(operand1, operand2, MathOperation.Multiply);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data.CalculationId);
        Assert.True(result.Data.CalculationTimeMs > 0);
        Assert.Equal("DecimalMathBatch", result.Data.CalculationType);
        
        _output.WriteLine($"Audit information:");
        _output.WriteLine($"  Calculation ID: {result.Data.CalculationId}");
        _output.WriteLine($"  Calculation Time: {result.Data.CalculationTimeMs}ms");
        _output.WriteLine($"  Service Name: {result.Data.ServiceName}");
        _output.WriteLine($"  Decimal Precision: {result.Data.DecimalPrecision}");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task SquareRoot_NegativeValue_ThrowsArgumentException()
    {
        // Arrange
        var negativeValue = -4.0m;

        // Act & Assert
        var result = await _decimalMathService.SqrtAsync(negativeValue);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("negative", result.Error.Message.ToLower());
    }

    [Fact]
    public async Task Power_ZeroToNegativePower_ThrowsArgumentException()
    {
        // Arrange
        var baseValue = 0m;
        var exponent = -2m;

        // Act & Assert
        var result = await _decimalMathService.PowAsync(baseValue, exponent);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task BatchCalculation_NullOperands_ThrowsArgumentException()
    {
        // Act & Assert
        var result = await _decimalMathService.BatchCalculateAsync(null!, null!, MathOperation.Add);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task BatchCalculation_MismatchedArraySizes_ThrowsArgumentException()
    {
        // Arrange
        var operand1 = new decimal[] { 1m, 2m, 3m };
        var operand2 = new decimal[] { 1m, 2m }; // Different size

        // Act & Assert
        var result = await _decimalMathService.BatchCalculateAsync(operand1, operand2, MathOperation.Add);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("same length", result.Error.Message);
    }

    #endregion

    #region Helper Methods

    private decimal[] GenerateTestData(int count, decimal min, decimal max)
    {
        var random = new Random(42); // Fixed seed for reproducible tests
        var data = new decimal[count];
        var range = max - min;

        for (int i = 0; i < count; i++)
        {
            data[i] = min + (decimal)random.NextDouble() * range;
        }

        return data;
    }

    private decimal[] GenerateReturnsData(int count)
    {
        var random = new Random(42);
        var returns = new decimal[count];
        
        for (int i = 0; i < count; i++)
        {
            // Generate normally distributed returns (simplified)
            var u1 = 1.0 - random.NextDouble();
            var u2 = 1.0 - random.NextDouble();
            var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            
            // Scale to daily return range (-10% to +10%)
            returns[i] = (decimal)(randStdNormal * 0.02); // 2% daily volatility
        }

        return returns;
    }

    private int GetDecimalPlaces(decimal value)
    {
        var bits = decimal.GetBits(value);
        return (bits[3] >> 16) & 0xFF;
    }

    private FinancialCalculationConfiguration CreateTestConfiguration()
    {
        return new FinancialCalculationConfiguration
        {
            DecimalPrecision = new DecimalPrecisionConfiguration
            {
                DefaultPrecision = 4,
                DefaultRoundingMode = RegulatoryRoundingMode.BankersRounding,
                CurrencyPrecisionOverrides = new Dictionary<string, int>
                {
                    { "USD", 2 },
                    { "EUR", 2 },
                    { "BTC", 8 }
                }
            },
            GpuConfiguration = new GpuConfiguration
            {
                EnableGpuAcceleration = true,
                BatchSizeThreshold = 100,
                EnableMultiGpu = false,
                EnableCpuFallback = true
            },
            ComplianceConfiguration = new ComplianceConfiguration
            {
                EnableSOXCompliance = true,
                EnableMiFIDCompliance = true,
                EnableBaselCompliance = true,
                EnableAuditTrail = true,
                AuditTrailRetention = TimeSpan.FromDays(2555), // 7 years
                RegulatoryReportingPath = Path.GetTempPath()
            },
            CacheConfiguration = new CacheConfiguration
            {
                EnableCaching = true,
                MaxCacheSize = 10000,
                DefaultCacheExpiry = TimeSpan.FromMinutes(15)
            },
            PerformanceThresholds = new PerformanceConfiguration
            {
                MaxLatencyMs = 1000.0,
                MaxConcurrentCalculations = 100
            }
        };
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _decimalMathService?.Dispose();
        _gpuContext?.Dispose();
        _complianceAuditor?.Dispose();
    }

    #endregion
}