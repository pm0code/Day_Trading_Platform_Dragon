// TradingPlatform.FinancialCalculations.Services.DecimalMathService
// Comprehensive decimal mathematics service with GPU acceleration and regulatory compliance

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Models;
using TradingPlatform.FinancialCalculations.Canonical;
using TradingPlatform.FinancialCalculations.Compliance;
using TradingPlatform.FinancialCalculations.Configuration;
using TradingPlatform.FinancialCalculations.Interfaces;
using TradingPlatform.FinancialCalculations.Models;
using TradingPlatform.GPU.Infrastructure;
using TradingPlatform.GPU.Mathematics;

namespace TradingPlatform.FinancialCalculations.Services;

/// <summary>
/// Enterprise-grade decimal mathematics service with GPU acceleration
/// Provides high-precision financial calculations with regulatory compliance
/// Automatically selects optimal execution path (GPU vs CPU) based on data size
/// </summary>
public class DecimalMathService : CanonicalFinancialCalculatorBase, IDecimalMathService
{
    #region Private Fields

    private readonly GpuDecimalMath? _gpuMath;
    private readonly ConcurrentDictionary<string, CalculationCache> _calculationCache;
    private readonly Timer _performanceMonitorTimer;
    private readonly object _cacheLock = new();

    // Performance tracking
    private long _totalCalculations = 0;
    private long _gpuCalculations = 0;
    private long _cpuCalculations = 0;
    private readonly ConcurrentDictionary<string, PerformanceMetrics> _operationMetrics;

    // Configuration
    private readonly int _gpuThreshold;
    private readonly bool _enableCaching;
    private readonly TimeSpan _cacheExpiry;

    #endregion

    #region Constructor

    public DecimalMathService(
        FinancialCalculationConfiguration configuration,
        IComplianceAuditor complianceAuditor,
        GpuContext? gpuContext = null,
        Dictionary<string, string>? metadata = null)
        : base("DecimalMathService", configuration, complianceAuditor, null, metadata)
    {
        _calculationCache = new ConcurrentDictionary<string, CalculationCache>();
        _operationMetrics = new ConcurrentDictionary<string, PerformanceMetrics>();
        
        // Initialize GPU math if available
        if (gpuContext != null && configuration.GpuConfiguration.EnableGpuAcceleration)
        {
            try
            {
                _gpuMath = new GpuDecimalMath(gpuContext, Logger);
                Logger.LogInfo("GPU acceleration initialized for decimal mathematics");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Failed to initialize GPU acceleration, using CPU only", ex);
            }
        }

        // Configuration
        _gpuThreshold = configuration.GpuConfiguration.BatchSizeThreshold;
        _enableCaching = configuration.CacheConfiguration.EnableCaching;
        _cacheExpiry = configuration.CacheConfiguration.DefaultCacheExpiry;

        // Performance monitoring timer
        _performanceMonitorTimer = new Timer(LogPerformanceMetrics, null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        Logger.LogInfo("DecimalMathService initialized successfully", new
        {
            GpuAvailable = _gpuMath != null,
            GpuThreshold = _gpuThreshold,
            CachingEnabled = _enableCaching
        });
    }

    #endregion

    #region IDecimalMathService Implementation

    /// <summary>
    /// Calculates square root with high precision and GPU acceleration
    /// </summary>
    public async Task<TradingResult<decimal>> SqrtAsync(decimal value)
    {
        return await ExecuteCalculationAsync(
            "SquareRoot",
            new { Value = value },
            async (request) =>
            {
                if (value < 0m)
                {
                    throw new ArgumentException("Square root of negative number is not supported");
                }

                if (value == 0m || value == 1m)
                {
                    return CreateResult(value, false);
                }

                // Use Newton's method for high precision
                var result = await CalculateDecimalSqrtAsync(value);
                return CreateResult(result, false);
            });
    }

    /// <summary>
    /// Calculates power with decimal precision
    /// </summary>
    public async Task<TradingResult<decimal>> PowAsync(decimal baseValue, decimal exponent)
    {
        return await ExecuteCalculationAsync(
            "Power",
            new { BaseValue = baseValue, Exponent = exponent },
            async (request) =>
            {
                if (baseValue == 0m && exponent <= 0m)
                {
                    throw new ArgumentException("Zero to negative or zero power is undefined");
                }

                if (exponent == 0m) return CreateResult(1m, false);
                if (exponent == 1m) return CreateResult(baseValue, false);
                if (baseValue == 1m) return CreateResult(1m, false);

                var result = await CalculateDecimalPowerAsync(baseValue, exponent);
                return CreateResult(result, false);
            });
    }

    /// <summary>
    /// Calculates natural logarithm with decimal precision
    /// </summary>
    public async Task<TradingResult<decimal>> LogAsync(decimal value)
    {
        return await ExecuteCalculationAsync(
            "NaturalLogarithm",
            new { Value = value },
            async (request) =>
            {
                if (value <= 0m)
                {
                    throw new ArgumentException("Logarithm of non-positive number is undefined");
                }

                if (value == 1m) return CreateResult(0m, false);

                var result = await CalculateDecimalLogAsync(value);
                return CreateResult(result, false);
            });
    }

    /// <summary>
    /// Batch calculation for large datasets with automatic GPU acceleration
    /// </summary>
    public async Task<TradingResult<decimal[]>> BatchCalculateAsync(
        decimal[] operand1,
        decimal[] operand2,
        MathOperation operation)
    {
        return await ExecuteCalculationAsync(
            $"Batch{operation}",
            new { Operand1Length = operand1.Length, Operand2Length = operand2?.Length, Operation = operation },
            async (request) =>
            {
                ValidateBatchInputs(operand1, operand2, operation);

                var useGpu = ShouldUseGpu(operand1.Length);
                decimal[] result;

                var stopwatch = Stopwatch.StartNew();

                try
                {
                    result = operation switch
                    {
                        MathOperation.Add when useGpu && operand2 != null => 
                            await _gpuMath!.AddAsync(operand1, operand2, _config.DecimalPrecision.DefaultPrecision),
                        MathOperation.Add when operand2 != null => 
                            await BatchAddCpuAsync(operand1, operand2),
                        
                        MathOperation.Multiply when useGpu && operand2 != null => 
                            await _gpuMath!.MultiplyAsync(operand1, operand2, _config.DecimalPrecision.DefaultPrecision),
                        MathOperation.Multiply when operand2 != null => 
                            await BatchMultiplyCpuAsync(operand1, operand2),
                        
                        MathOperation.Divide when useGpu && operand2 != null => 
                            await _gpuMath!.DivideAsync(operand1, operand2, _config.DecimalPrecision.DefaultPrecision),
                        MathOperation.Divide when operand2 != null => 
                            await BatchDivideCpuAsync(operand1, operand2),
                        
                        MathOperation.SquareRoot when useGpu => 
                            await _gpuMath!.SquareRootAsync(operand1, _config.DecimalPrecision.DefaultPrecision),
                        MathOperation.SquareRoot => 
                            await BatchSqrtCpuAsync(operand1),
                        
                        _ => throw new ArgumentException($"Unsupported operation: {operation}")
                    };

                    stopwatch.Stop();

                    // Track performance metrics
                    TrackOperationPerformance(operation.ToString(), operand1.Length, useGpu, stopwatch.Elapsed);

                    return CreateBatchResult(result, useGpu);
                }
                catch (Exception ex)
                {
                    if (useGpu)
                    {
                        Logger.LogWarning("GPU calculation failed, retrying with CPU", ex);
                        
                        // Retry with CPU
                        result = operation switch
                        {
                            MathOperation.Add when operand2 != null => await BatchAddCpuAsync(operand1, operand2),
                            MathOperation.Multiply when operand2 != null => await BatchMultiplyCpuAsync(operand1, operand2),
                            MathOperation.Divide when operand2 != null => await BatchDivideCpuAsync(operand1, operand2),
                            MathOperation.SquareRoot => await BatchSqrtCpuAsync(operand1),
                            _ => throw
                        };

                        TrackOperationPerformance(operation.ToString(), operand1.Length, false, stopwatch.Elapsed);
                        return CreateBatchResult(result, false);
                    }
                    throw;
                }
            });
    }

    /// <summary>
    /// Financial calculation with automatic precision and rounding
    /// </summary>
    public async Task<TradingResult<decimal>> CalculateFinancialValueAsync(
        decimal principal,
        decimal rate,
        int periods,
        FinancialCalculationType calculationType)
    {
        return await ExecuteCalculationAsync(
            $"Financial{calculationType}",
            new { Principal = principal, Rate = rate, Periods = periods, CalculationType = calculationType },
            async (request) =>
            {
                var result = calculationType switch
                {
                    FinancialCalculationType.CompoundInterest => 
                        await CalculateCompoundInterestAsync(principal, rate, periods),
                    FinancialCalculationType.PresentValue => 
                        await CalculatePresentValueAsync(principal, rate, periods),
                    FinancialCalculationType.FutureValue => 
                        await CalculateFutureValueAsync(principal, rate, periods),
                    FinancialCalculationType.Annuity => 
                        await CalculateAnnuityAsync(principal, rate, periods),
                    _ => throw new ArgumentException($"Unsupported calculation type: {calculationType}")
                };

                // Apply financial rounding
                var roundedResult = RoundFinancial(result, _config.DecimalPrecision.DefaultPrecision);
                
                return CreateResult(roundedResult, false);
            });
    }

    /// <summary>
    /// Portfolio calculations with GPU acceleration for large portfolios
    /// </summary>
    public async Task<TradingResult<decimal[]>> CalculatePortfolioValuesAsync(
        decimal[] quantities,
        decimal[] prices)
    {
        return await ExecuteCalculationAsync(
            "PortfolioValues",
            new { PositionCount = quantities.Length },
            async (request) =>
            {
                if (quantities.Length != prices.Length)
                {
                    throw new ArgumentException("Quantities and prices arrays must have the same length");
                }

                var useGpu = ShouldUseGpu(quantities.Length);
                decimal[] result;

                if (useGpu && _gpuMath != null)
                {
                    result = await _gpuMath.CalculatePortfolioValuesAsync(
                        quantities, prices, _config.DecimalPrecision.DefaultPrecision);
                }
                else
                {
                    result = await CalculatePortfolioValuesCpuAsync(quantities, prices);
                }

                // Apply currency rounding to portfolio values
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = RoundToCurrency(result[i], "USD");
                }

                TrackOperationPerformance("PortfolioValues", quantities.Length, useGpu, TimeSpan.Zero);
                
                return CreateBatchResult(result, useGpu);
            });
    }

    /// <summary>
    /// Risk calculations with high precision requirements
    /// </summary>
    public async Task<TradingResult<RiskCalculationResult>> CalculateRiskMetricsAsync(
        decimal[] returns,
        decimal confidenceLevel = 0.95m)
    {
        return await ExecuteCalculationAsync(
            "RiskMetrics",
            new { ReturnCount = returns.Length, ConfidenceLevel = confidenceLevel },
            async (request) =>
            {
                var useGpu = ShouldUseGpu(returns.Length);
                
                // Calculate VaR
                decimal var95;
                if (useGpu && _gpuMath != null)
                {
                    var95 = await _gpuMath.CalculateVaRAsync(returns, confidenceLevel, 8);
                }
                else
                {
                    var95 = await CalculateVaRCpuAsync(returns, confidenceLevel);
                }

                // Calculate other risk metrics (CPU for now)
                var volatility = await CalculateVolatilityAsync(returns);
                var expectedShortfall = await CalculateExpectedShortfallAsync(returns, confidenceLevel);
                var maxDrawdown = await CalculateMaxDrawdownAsync(returns);

                var result = new RiskCalculationResult
                {
                    CalculationId = Guid.NewGuid().ToString(),
                    CalculationType = "RiskMetrics",
                    CalculatedAt = DateTime.UtcNow,
                    CalculationTimeMs = 0, // Will be set by base class
                    DecimalPrecision = 8,
                    RoundingMode = RegulatoryRoundingMode.BankersRounding,
                    UsedGpuAcceleration = useGpu,
                    
                    VaR95 = RoundFinancial(var95, 8),
                    Volatility = RoundFinancial(volatility, 8),
                    ExpectedShortfall = RoundFinancial(expectedShortfall, 8),
                    MaxDrawdown = RoundFinancial(maxDrawdown, 8),
                    ConfidenceLevel = confidenceLevel,
                    SampleSize = returns.Length
                };

                TrackOperationPerformance("RiskMetrics", returns.Length, useGpu, TimeSpan.Zero);
                
                return TradingResult<RiskCalculationResult>.Success(result);
            });
    }

    #endregion

    #region Advanced Mathematical Functions

    /// <summary>
    /// High-precision decimal square root using Newton's method
    /// </summary>
    private async Task<decimal> CalculateDecimalSqrtAsync(decimal value)
    {
        return await Task.Run(() =>
        {
            if (value <= 0m) return 0m;
            if (value == 1m) return 1m;
            
            decimal x = value / 2m; // Initial guess
            decimal lastX;
            
            do
            {
                lastX = x;
                x = (x + value / x) / 2m;
            }
            while (Math.Abs(x - lastX) > 0.000000001m); // High precision
            
            return x;
        });
    }

    /// <summary>
    /// High-precision decimal power calculation
    /// </summary>
    private async Task<decimal> CalculateDecimalPowerAsync(decimal baseValue, decimal exponent)
    {
        return await Task.Run(() =>
        {
            // Handle special cases
            if (exponent == 0m) return 1m;
            if (exponent == 1m) return baseValue;
            if (baseValue == 0m) return 0m;
            if (baseValue == 1m) return 1m;

            // For integer exponents, use repeated multiplication
            if (exponent == Math.Floor(exponent))
            {
                return CalculatePowerInteger(baseValue, (int)exponent);
            }

            // For fractional exponents, use logarithmic approach
            // baseValue^exponent = e^(exponent * ln(baseValue))
            var ln = CalculateNaturalLog(baseValue);
            var product = exponent * ln;
            return CalculateExponential(product);
        });
    }

    /// <summary>
    /// Natural logarithm calculation using Taylor series
    /// </summary>
    private async Task<decimal> CalculateDecimalLogAsync(decimal value)
    {
        return await Task.Run(() => CalculateNaturalLog(value));
    }

    private decimal CalculateNaturalLog(decimal value)
    {
        if (value <= 0m) throw new ArgumentException("Log of non-positive number");
        if (value == 1m) return 0m;

        // Reduce to range [0.5, 2] for better convergence
        int shifts = 0;
        while (value >= 2m)
        {
            value /= 2m;
            shifts++;
        }
        while (value < 0.5m)
        {
            value *= 2m;
            shifts--;
        }

        // Use Taylor series: ln(1+x) = x - x²/2 + x³/3 - x⁴/4 + ...
        decimal x = value - 1m;
        decimal sum = 0m;
        decimal term = x;
        
        for (int n = 1; n <= 100 && Math.Abs(term) > 0.000000001m; n++)
        {
            sum += term / n * (n % 2 == 1 ? 1 : -1);
            term *= x;
        }

        // Add back the shifts: ln(2^shifts * originalValue) = shifts * ln(2) + ln(reducedValue)
        return sum + shifts * 0.6931471805599453m; // ln(2)
    }

    private decimal CalculateExponential(decimal value)
    {
        // e^x = 1 + x + x²/2! + x³/3! + x⁴/4! + ...
        decimal sum = 1m;
        decimal term = 1m;
        
        for (int n = 1; n <= 100 && Math.Abs(term) > 0.000000001m; n++)
        {
            term *= value / n;
            sum += term;
        }
        
        return sum;
    }

    private decimal CalculatePowerInteger(decimal baseValue, int exponent)
    {
        if (exponent == 0) return 1m;
        if (exponent == 1) return baseValue;
        if (exponent < 0) return 1m / CalculatePowerInteger(baseValue, -exponent);
        
        decimal result = 1m;
        decimal currentBase = baseValue;
        int currentExponent = exponent;
        
        while (currentExponent > 0)
        {
            if ((currentExponent & 1) == 1)
                result *= currentBase;
            currentBase *= currentBase;
            currentExponent >>= 1;
        }
        
        return result;
    }

    #endregion

    #region Financial Calculations

    private async Task<decimal> CalculateCompoundInterestAsync(decimal principal, decimal rate, int periods)
    {
        return await Task.Run(async () =>
        {
            // A = P(1 + r)^n
            var factor = 1m + rate;
            var compound = await CalculateDecimalPowerAsync(factor, periods);
            return principal * compound;
        });
    }

    private async Task<decimal> CalculatePresentValueAsync(decimal futureValue, decimal rate, int periods)
    {
        return await Task.Run(async () =>
        {
            // PV = FV / (1 + r)^n
            var factor = 1m + rate;
            var compound = await CalculateDecimalPowerAsync(factor, periods);
            return futureValue / compound;
        });
    }

    private async Task<decimal> CalculateFutureValueAsync(decimal presentValue, decimal rate, int periods)
    {
        return await CalculateCompoundInterestAsync(presentValue, rate, periods);
    }

    private async Task<decimal> CalculateAnnuityAsync(decimal payment, decimal rate, int periods)
    {
        return await Task.Run(async () =>
        {
            if (rate == 0m) return payment * periods;
            
            // PV = PMT * [(1 - (1 + r)^(-n)) / r]
            var factor = 1m + rate;
            var compound = await CalculateDecimalPowerAsync(factor, -periods);
            return payment * (1m - compound) / rate;
        });
    }

    #endregion

    #region Risk Calculations

    private async Task<decimal> CalculateVaRCpuAsync(decimal[] returns, decimal confidenceLevel)
    {
        return await Task.Run(() =>
        {
            var sortedReturns = returns.OrderBy(r => r).ToArray();
            var index = (int)((1m - confidenceLevel) * returns.Length);
            index = Math.Max(0, Math.Min(index, returns.Length - 1));
            
            return Math.Abs(sortedReturns[index]);
        });
    }

    private async Task<decimal> CalculateVolatilityAsync(decimal[] returns)
    {
        return await Task.Run(async () =>
        {
            var mean = returns.Average();
            var variance = returns.Select(r => (r - mean) * (r - mean)).Average();
            return await CalculateDecimalSqrtAsync(variance);
        });
    }

    private async Task<decimal> CalculateExpectedShortfallAsync(decimal[] returns, decimal confidenceLevel)
    {
        return await Task.Run(() =>
        {
            var var = CalculateVaRCpuAsync(returns, confidenceLevel).Result;
            var worstReturns = returns.Where(r => r <= -var).ToArray();
            
            return worstReturns.Length > 0 ? Math.Abs(worstReturns.Average()) : var;
        });
    }

    private async Task<decimal> CalculateMaxDrawdownAsync(decimal[] returns)
    {
        return await Task.Run(() =>
        {
            decimal peak = 0m;
            decimal maxDrawdown = 0m;
            decimal cumulative = 0m;
            
            foreach (var ret in returns)
            {
                cumulative += ret;
                peak = Math.Max(peak, cumulative);
                var drawdown = peak - cumulative;
                maxDrawdown = Math.Max(maxDrawdown, drawdown);
            }
            
            return peak > 0 ? maxDrawdown / peak : 0m;
        });
    }

    #endregion

    #region CPU Batch Operations

    private async Task<decimal[]> BatchAddCpuAsync(decimal[] operand1, decimal[] operand2)
    {
        return await Task.Run(() =>
        {
            var result = new decimal[operand1.Length];
            Parallel.For(0, operand1.Length, i =>
            {
                result[i] = operand1[i] + operand2[i];
            });
            return result;
        });
    }

    private async Task<decimal[]> BatchMultiplyCpuAsync(decimal[] operand1, decimal[] operand2)
    {
        return await Task.Run(() =>
        {
            var result = new decimal[operand1.Length];
            Parallel.For(0, operand1.Length, i =>
            {
                result[i] = operand1[i] * operand2[i];
            });
            return result;
        });
    }

    private async Task<decimal[]> BatchDivideCpuAsync(decimal[] dividend, decimal[] divisor)
    {
        return await Task.Run(() =>
        {
            var result = new decimal[dividend.Length];
            Parallel.For(0, dividend.Length, i =>
            {
                result[i] = divisor[i] != 0m ? dividend[i] / divisor[i] : 0m;
            });
            return result;
        });
    }

    private async Task<decimal[]> BatchSqrtCpuAsync(decimal[] values)
    {
        return await Task.Run(async () =>
        {
            var result = new decimal[values.Length];
            var tasks = values.Select(async (value, index) =>
            {
                result[index] = await CalculateDecimalSqrtAsync(value);
            });
            
            await Task.WhenAll(tasks);
            return result;
        });
    }

    private async Task<decimal[]> CalculatePortfolioValuesCpuAsync(decimal[] quantities, decimal[] prices)
    {
        return await Task.Run(() =>
        {
            var result = new decimal[quantities.Length];
            Parallel.For(0, quantities.Length, i =>
            {
                result[i] = quantities[i] * prices[i];
            });
            return result;
        });
    }

    #endregion

    #region Utility Methods

    private bool ShouldUseGpu(int dataSize)
    {
        return _gpuMath != null && 
               _config.GpuConfiguration.EnableGpuAcceleration && 
               dataSize >= _gpuThreshold;
    }

    private void ValidateBatchInputs(decimal[] operand1, decimal[]? operand2, MathOperation operation)
    {
        if (operand1 == null || operand1.Length == 0)
            throw new ArgumentException("First operand cannot be null or empty");

        if (operation != MathOperation.SquareRoot)
        {
            if (operand2 == null)
                throw new ArgumentException("Second operand required for binary operations");
            
            if (operand1.Length != operand2.Length)
                throw new ArgumentException("Operand arrays must have the same length");
        }
    }

    private DecimalMathResult CreateResult(decimal value, bool usedGpu)
    {
        return new DecimalMathResult
        {
            CalculationId = Guid.NewGuid().ToString(),
            CalculationType = "DecimalMath",
            CalculatedAt = DateTime.UtcNow,
            CalculationTimeMs = 0, // Will be set by base class
            DecimalPrecision = _config.DecimalPrecision.DefaultPrecision,
            RoundingMode = _config.DecimalPrecision.DefaultRoundingMode,
            UsedGpuAcceleration = usedGpu,
            Value = value
        };
    }

    private DecimalMathBatchResult CreateBatchResult(decimal[] values, bool usedGpu)
    {
        return new DecimalMathBatchResult
        {
            CalculationId = Guid.NewGuid().ToString(),
            CalculationType = "DecimalMathBatch",
            CalculatedAt = DateTime.UtcNow,
            CalculationTimeMs = 0, // Will be set by base class
            DecimalPrecision = _config.DecimalPrecision.DefaultPrecision,
            RoundingMode = _config.DecimalPrecision.DefaultRoundingMode,
            UsedGpuAcceleration = usedGpu,
            Values = values,
            Count = values.Length
        };
    }

    private void TrackOperationPerformance(string operation, int dataSize, bool usedGpu, TimeSpan elapsed)
    {
        Interlocked.Increment(ref _totalCalculations);
        
        if (usedGpu)
            Interlocked.Increment(ref _gpuCalculations);
        else
            Interlocked.Increment(ref _cpuCalculations);

        _operationMetrics.AddOrUpdate(operation, 
            new PerformanceMetrics { Count = 1, TotalTime = elapsed, DataSize = dataSize },
            (key, existing) => new PerformanceMetrics
            {
                Count = existing.Count + 1,
                TotalTime = existing.TotalTime + elapsed,
                DataSize = Math.Max(existing.DataSize, dataSize)
            });
    }

    private void LogPerformanceMetrics(object? state)
    {
        try
        {
            var gpuRatio = _totalCalculations > 0 ? (double)_gpuCalculations / _totalCalculations : 0.0;
            
            Logger.LogInfo("DecimalMath performance summary", new
            {
                TotalCalculations = _totalCalculations,
                GpuCalculations = _gpuCalculations,
                CpuCalculations = _cpuCalculations,
                GpuRatio = gpuRatio,
                CacheEntries = _calculationCache.Count,
                Operations = _operationMetrics.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        Count = kvp.Value.Count,
                        AverageTimeMs = kvp.Value.Count > 0 ? kvp.Value.TotalTime.TotalMilliseconds / kvp.Value.Count : 0,
                        MaxDataSize = kvp.Value.DataSize
                    })
            });
        }
        catch (Exception ex)
        {
            Logger.LogError("Error logging performance metrics", ex);
        }
    }

    #endregion

    #region IDisposable

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _performanceMonitorTimer?.Dispose();
            _gpuMath?.Dispose();
            _calculationCache.Clear();
            _operationMetrics.Clear();
        }
        
        base.Dispose(disposing);
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Mathematical operations supported by the service
/// </summary>
public enum MathOperation
{
    Add,
    Subtract,
    Multiply,
    Divide,
    SquareRoot,
    Power,
    Logarithm
}

/// <summary>
/// Financial calculation types
/// </summary>
public enum FinancialCalculationType
{
    CompoundInterest,
    PresentValue,
    FutureValue,
    Annuity,
    NetPresentValue,
    InternalRateOfReturn
}

/// <summary>
/// Performance metrics tracking
/// </summary>
internal record PerformanceMetrics
{
    public long Count { get; init; }
    public TimeSpan TotalTime { get; init; }
    public int DataSize { get; init; }
}

/// <summary>
/// Calculation cache entry
/// </summary>
internal record CalculationCache
{
    public object Result { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
    public string OperationType { get; init; } = string.Empty;
}

#endregion