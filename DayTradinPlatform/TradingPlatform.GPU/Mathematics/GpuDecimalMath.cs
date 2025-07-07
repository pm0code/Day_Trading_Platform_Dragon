// TradingPlatform.GPU.Mathematics.GpuDecimalMath
// GPU-accelerated decimal mathematics for high-performance financial calculations
// Maintains System.Decimal precision using scaled integer arithmetic on GPU

using System.Collections.Concurrent;
using System.Diagnostics;
using ILGPU;
using ILGPU.Runtime;
using Microsoft.Extensions.Logging;
using TradingPlatform.GPU.Infrastructure;

namespace TradingPlatform.GPU.Mathematics;

/// <summary>
/// GPU-accelerated decimal mathematics library for financial calculations
/// Maintains System.Decimal precision using scaled integer arithmetic
/// Automatically falls back to CPU for small datasets or GPU failures
/// </summary>
public class GpuDecimalMath : IDisposable
{
    #region Constants and Configuration

    // Scale factors for different precision requirements
    public const long PRICE_SCALE = 10000L;           // 4 decimal places for prices
    public const long RATE_SCALE = 100000000L;        // 8 decimal places for rates/percentages
    public const long QUANTITY_SCALE = 1000000L;      // 6 decimal places for quantities
    public const long CURRENCY_SCALE = 100L;          // 2 decimal places for currencies
    public const long HIGH_PRECISION_SCALE = 1000000000000000000L; // 18 decimal places

    // Performance thresholds
    private const int GPU_THRESHOLD_SMALL = 100;      // Minimum items for GPU usage
    private const int GPU_THRESHOLD_MEDIUM = 1000;    // Optimal GPU batch size
    private const int GPU_THRESHOLD_LARGE = 10000;    // Large dataset threshold

    // Calculation precision constants
    private const int NEWTON_ITERATIONS = 50;         // Newton's method iterations
    private const long PRECISION_EPSILON = 1L;        // Minimum precision difference

    #endregion

    #region Private Fields

    private readonly GpuContext _gpuContext;
    private readonly ILogger<GpuDecimalMath> _logger;
    private readonly ConcurrentDictionary<string, object> _kernelCache;
    private readonly MultiGpuManager? _multiGpuManager;
    private bool _disposed;

    // GPU kernels for mathematical operations
    private Action<Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
        ArrayView1D<long, Stride1D.Dense>, long>? _additionKernel;
    
    private Action<Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
        ArrayView1D<long, Stride1D.Dense>, long>? _multiplicationKernel;
    
    private Action<Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
        ArrayView1D<long, Stride1D.Dense>, long>? _divisionKernel;
    
    private Action<Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
        long>? _squareRootKernel;
    
    private Action<Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
        ArrayView1D<long, Stride1D.Dense>, long>? _powerKernel;

    private Action<Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
        ArrayView1D<long, Stride1D.Dense>, long>? _portfolioValueKernel;

    private Action<Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
        ArrayView1D<long, Stride1D.Dense>, long, long>? _varCalculationKernel;

    #endregion

    #region Constructor and Initialization

    public GpuDecimalMath(
        GpuContext gpuContext, 
        ILogger<GpuDecimalMath> logger,
        MultiGpuManager? multiGpuManager = null)
    {
        _gpuContext = gpuContext ?? throw new ArgumentNullException(nameof(gpuContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _multiGpuManager = multiGpuManager;
        _kernelCache = new ConcurrentDictionary<string, object>();

        InitializeKernels();
        
        _logger.LogInformation("GpuDecimalMath initialized with GPU context: {DeviceType}", 
            _gpuContext.DeviceType);
    }

    private void InitializeKernels()
    {
        try
        {
            if (_gpuContext.Accelerator == null)
            {
                _logger.LogWarning("GPU accelerator not available, kernels will not be loaded");
                return;
            }

            // Load basic arithmetic kernels
            _additionKernel = _gpuContext.Accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
                ArrayView1D<long, Stride1D.Dense>, long>(AdditionKernel);

            _multiplicationKernel = _gpuContext.Accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
                ArrayView1D<long, Stride1D.Dense>, long>(MultiplicationKernel);

            _divisionKernel = _gpuContext.Accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
                ArrayView1D<long, Stride1D.Dense>, long>(DivisionKernel);

            _squareRootKernel = _gpuContext.Accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
                long>(SquareRootKernel);

            _powerKernel = _gpuContext.Accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
                ArrayView1D<long, Stride1D.Dense>, long>(PowerKernel);

            // Load financial calculation kernels
            _portfolioValueKernel = _gpuContext.Accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
                ArrayView1D<long, Stride1D.Dense>, long>(PortfolioValueKernel);

            _varCalculationKernel = _gpuContext.Accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
                ArrayView1D<long, Stride1D.Dense>, long, long>(VarCalculationKernel);

            _logger.LogInformation("GPU mathematical kernels loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load GPU mathematical kernels");
        }
    }

    #endregion

    #region GPU Kernels

    /// <summary>
    /// GPU kernel for scaled decimal addition
    /// </summary>
    public static void AdditionKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> operand1,
        ArrayView1D<long, Stride1D.Dense> operand2,
        ArrayView1D<long, Stride1D.Dense> result,
        long scaleFactor)
    {
        var i = index.X;
        if (i >= operand1.Length) return;

        result[i] = operand1[i] + operand2[i];
    }

    /// <summary>
    /// GPU kernel for scaled decimal multiplication
    /// </summary>
    public static void MultiplicationKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> operand1,
        ArrayView1D<long, Stride1D.Dense> operand2,
        ArrayView1D<long, Stride1D.Dense> result,
        long scaleFactor)
    {
        var i = index.X;
        if (i >= operand1.Length) return;

        // Multiply and scale down to maintain precision
        result[i] = (operand1[i] * operand2[i]) / scaleFactor;
    }

    /// <summary>
    /// GPU kernel for scaled decimal division
    /// </summary>
    public static void DivisionKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> dividend,
        ArrayView1D<long, Stride1D.Dense> divisor,
        ArrayView1D<long, Stride1D.Dense> result,
        long scaleFactor)
    {
        var i = index.X;
        if (i >= dividend.Length) return;

        if (divisor[i] == 0L)
        {
            result[i] = 0L; // Handle division by zero
            return;
        }

        // Scale up dividend to maintain precision after division
        result[i] = (dividend[i] * scaleFactor) / divisor[i];
    }

    /// <summary>
    /// GPU kernel for square root using Newton's method
    /// </summary>
    public static void SquareRootKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> input,
        ArrayView1D<long, Stride1D.Dense> result,
        long scaleFactor)
    {
        var i = index.X;
        if (i >= input.Length) return;

        var value = input[i];
        if (value <= 0L)
        {
            result[i] = 0L;
            return;
        }

        // Newton's method for square root
        var x = value / 2L;
        for (int iter = 0; iter < 20; iter++)
        {
            var newX = (x + (value * scaleFactor) / x) / 2L;
            if (Math.Abs(newX - x) <= PRECISION_EPSILON) break;
            x = newX;
        }

        result[i] = x;
    }

    /// <summary>
    /// GPU kernel for power calculation (integer exponents)
    /// </summary>
    public static void PowerKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> baseValues,
        ArrayView1D<long, Stride1D.Dense> exponents,
        ArrayView1D<long, Stride1D.Dense> result,
        long scaleFactor)
    {
        var i = index.X;
        if (i >= baseValues.Length) return;

        var baseValue = baseValues[i];
        var exponent = exponents[i];

        if (exponent == 0L)
        {
            result[i] = scaleFactor; // base^0 = 1
            return;
        }

        if (exponent == 1L)
        {
            result[i] = baseValue;
            return;
        }

        // Calculate power using repeated multiplication
        var currentResult = scaleFactor;
        var absExponent = exponent < 0L ? -exponent : exponent;

        for (long exp = 0; exp < absExponent; exp++)
        {
            currentResult = (currentResult * baseValue) / scaleFactor;
        }

        result[i] = exponent < 0L ? (scaleFactor * scaleFactor) / currentResult : currentResult;
    }

    /// <summary>
    /// GPU kernel for portfolio value calculation
    /// </summary>
    public static void PortfolioValueKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> quantities,
        ArrayView1D<long, Stride1D.Dense> prices,
        ArrayView1D<long, Stride1D.Dense> marketValues,
        long scaleFactor)
    {
        var i = index.X;
        if (i >= quantities.Length) return;

        // Market Value = Quantity × Price
        marketValues[i] = (quantities[i] * prices[i]) / scaleFactor;
    }

    /// <summary>
    /// GPU kernel for VaR calculation preprocessing
    /// </summary>
    public static void VarCalculationKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> returns,
        ArrayView1D<long, Stride1D.Dense> squaredDeviations,
        ArrayView1D<long, Stride1D.Dense> sortedReturns,
        long mean,
        long scaleFactor)
    {
        var i = index.X;
        if (i >= returns.Length) return;

        // Calculate squared deviations for variance
        var deviation = returns[i] - mean;
        squaredDeviations[i] = (deviation * deviation) / scaleFactor;

        // Copy for sorting (GPU doesn't sort, but prepares data)
        sortedReturns[i] = returns[i];
    }

    #endregion

    #region Public API - Basic Operations

    /// <summary>
    /// Adds two arrays of decimal values using GPU acceleration
    /// </summary>
    public async Task<decimal[]> AddAsync(decimal[] operand1, decimal[] operand2, int decimalPlaces = 4)
    {
        if (operand1 == null || operand2 == null)
            throw new ArgumentNullException("Operands cannot be null");

        if (operand1.Length != operand2.Length)
            throw new ArgumentException("Operand arrays must have the same length");

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Use CPU for small datasets
            if (operand1.Length < GPU_THRESHOLD_SMALL || _additionKernel == null)
            {
                return await AddCpuAsync(operand1, operand2);
            }

            var scaleFactor = GetScaleFactor(decimalPlaces);
            
            // Convert to scaled integers
            var scaled1 = operand1.Select(d => ToScaledInteger(d, decimalPlaces)).ToArray();
            var scaled2 = operand2.Select(d => ToScaledInteger(d, decimalPlaces)).ToArray();

            using var buffer1 = _gpuContext.Accelerator!.Allocate1D<long>(scaled1.Length);
            using var buffer2 = _gpuContext.Accelerator.Allocate1D<long>(scaled2.Length);
            using var resultBuffer = _gpuContext.Accelerator.Allocate1D<long>(scaled1.Length);

            buffer1.CopyFromCPU(scaled1);
            buffer2.CopyFromCPU(scaled2);

            // Execute kernel
            _additionKernel(scaled1.Length, buffer1.View, buffer2.View, resultBuffer.View, scaleFactor);
            _gpuContext.Accelerator.Synchronize();

            // Copy result back and convert to decimal
            var scaledResult = resultBuffer.GetAsArray1D();
            var result = scaledResult.Select(s => FromScaledInteger(s, decimalPlaces)).ToArray();

            stopwatch.Stop();
            _logger.LogDebug("GPU addition completed: {Count} elements in {ElapsedMs}ms", 
                operand1.Length, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GPU addition failed, falling back to CPU");
            return await AddCpuAsync(operand1, operand2);
        }
    }

    /// <summary>
    /// Multiplies two arrays of decimal values using GPU acceleration
    /// </summary>
    public async Task<decimal[]> MultiplyAsync(decimal[] operand1, decimal[] operand2, int decimalPlaces = 4)
    {
        if (operand1 == null || operand2 == null)
            throw new ArgumentNullException("Operands cannot be null");

        if (operand1.Length != operand2.Length)
            throw new ArgumentException("Operand arrays must have the same length");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Use CPU for small datasets
            if (operand1.Length < GPU_THRESHOLD_SMALL || _multiplicationKernel == null)
            {
                return await MultiplyCpuAsync(operand1, operand2);
            }

            var scaleFactor = GetScaleFactor(decimalPlaces);
            
            // Convert to scaled integers
            var scaled1 = operand1.Select(d => ToScaledInteger(d, decimalPlaces)).ToArray();
            var scaled2 = operand2.Select(d => ToScaledInteger(d, decimalPlaces)).ToArray();

            using var buffer1 = _gpuContext.Accelerator!.Allocate1D<long>(scaled1.Length);
            using var buffer2 = _gpuContext.Accelerator.Allocate1D<long>(scaled2.Length);
            using var resultBuffer = _gpuContext.Accelerator.Allocate1D<long>(scaled1.Length);

            buffer1.CopyFromCPU(scaled1);
            buffer2.CopyFromCPU(scaled2);

            // Execute kernel
            _multiplicationKernel(scaled1.Length, buffer1.View, buffer2.View, resultBuffer.View, scaleFactor);
            _gpuContext.Accelerator.Synchronize();

            // Copy result back and convert to decimal
            var scaledResult = resultBuffer.GetAsArray1D();
            var result = scaledResult.Select(s => FromScaledInteger(s, decimalPlaces)).ToArray();

            stopwatch.Stop();
            _logger.LogDebug("GPU multiplication completed: {Count} elements in {ElapsedMs}ms", 
                operand1.Length, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GPU multiplication failed, falling back to CPU");
            return await MultiplyCpuAsync(operand1, operand2);
        }
    }

    /// <summary>
    /// Divides two arrays of decimal values using GPU acceleration
    /// </summary>
    public async Task<decimal[]> DivideAsync(decimal[] dividend, decimal[] divisor, int decimalPlaces = 4)
    {
        if (dividend == null || divisor == null)
            throw new ArgumentNullException("Operands cannot be null");

        if (dividend.Length != divisor.Length)
            throw new ArgumentException("Operand arrays must have the same length");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Use CPU for small datasets
            if (dividend.Length < GPU_THRESHOLD_SMALL || _divisionKernel == null)
            {
                return await DivideCpuAsync(dividend, divisor);
            }

            var scaleFactor = GetScaleFactor(decimalPlaces);
            
            // Convert to scaled integers
            var scaledDividend = dividend.Select(d => ToScaledInteger(d, decimalPlaces)).ToArray();
            var scaledDivisor = divisor.Select(d => ToScaledInteger(d, decimalPlaces)).ToArray();

            using var dividendBuffer = _gpuContext.Accelerator!.Allocate1D<long>(scaledDividend.Length);
            using var divisorBuffer = _gpuContext.Accelerator.Allocate1D<long>(scaledDivisor.Length);
            using var resultBuffer = _gpuContext.Accelerator.Allocate1D<long>(scaledDividend.Length);

            dividendBuffer.CopyFromCPU(scaledDividend);
            divisorBuffer.CopyFromCPU(scaledDivisor);

            // Execute kernel
            _divisionKernel(scaledDividend.Length, dividendBuffer.View, divisorBuffer.View, 
                resultBuffer.View, scaleFactor);
            _gpuContext.Accelerator.Synchronize();

            // Copy result back and convert to decimal
            var scaledResult = resultBuffer.GetAsArray1D();
            var result = scaledResult.Select(s => FromScaledInteger(s, decimalPlaces)).ToArray();

            stopwatch.Stop();
            _logger.LogDebug("GPU division completed: {Count} elements in {ElapsedMs}ms", 
                dividend.Length, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GPU division failed, falling back to CPU");
            return await DivideCpuAsync(dividend, divisor);
        }
    }

    /// <summary>
    /// Calculates square root of decimal values using GPU acceleration
    /// </summary>
    public async Task<decimal[]> SquareRootAsync(decimal[] values, int decimalPlaces = 4)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Use CPU for small datasets
            if (values.Length < GPU_THRESHOLD_SMALL || _squareRootKernel == null)
            {
                return await SquareRootCpuAsync(values);
            }

            var scaleFactor = GetScaleFactor(decimalPlaces);
            
            // Convert to scaled integers
            var scaledValues = values.Select(d => ToScaledInteger(d, decimalPlaces)).ToArray();

            using var inputBuffer = _gpuContext.Accelerator!.Allocate1D<long>(scaledValues.Length);
            using var resultBuffer = _gpuContext.Accelerator.Allocate1D<long>(scaledValues.Length);

            inputBuffer.CopyFromCPU(scaledValues);

            // Execute kernel
            _squareRootKernel(scaledValues.Length, inputBuffer.View, resultBuffer.View, scaleFactor);
            _gpuContext.Accelerator.Synchronize();

            // Copy result back and convert to decimal
            var scaledResult = resultBuffer.GetAsArray1D();
            var result = scaledResult.Select(s => FromScaledInteger(s, decimalPlaces)).ToArray();

            stopwatch.Stop();
            _logger.LogDebug("GPU square root completed: {Count} elements in {ElapsedMs}ms", 
                values.Length, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GPU square root failed, falling back to CPU");
            return await SquareRootCpuAsync(values);
        }
    }

    #endregion

    #region Public API - Financial Calculations

    /// <summary>
    /// Calculates portfolio values using GPU acceleration
    /// </summary>
    public async Task<decimal[]> CalculatePortfolioValuesAsync(
        decimal[] quantities, 
        decimal[] prices, 
        int decimalPlaces = 4)
    {
        if (quantities == null || prices == null)
            throw new ArgumentNullException("Input arrays cannot be null");

        if (quantities.Length != prices.Length)
            throw new ArgumentException("Quantities and prices arrays must have the same length");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Use CPU for small datasets
            if (quantities.Length < GPU_THRESHOLD_SMALL || _portfolioValueKernel == null)
            {
                return await CalculatePortfolioValuesCpuAsync(quantities, prices);
            }

            var scaleFactor = GetScaleFactor(decimalPlaces);
            
            // Convert to scaled integers
            var scaledQuantities = quantities.Select(d => ToScaledInteger(d, decimalPlaces)).ToArray();
            var scaledPrices = prices.Select(d => ToScaledInteger(d, decimalPlaces)).ToArray();

            using var quantitiesBuffer = _gpuContext.Accelerator!.Allocate1D<long>(scaledQuantities.Length);
            using var pricesBuffer = _gpuContext.Accelerator.Allocate1D<long>(scaledPrices.Length);
            using var resultBuffer = _gpuContext.Accelerator.Allocate1D<long>(scaledQuantities.Length);

            quantitiesBuffer.CopyFromCPU(scaledQuantities);
            pricesBuffer.CopyFromCPU(scaledPrices);

            // Execute kernel
            _portfolioValueKernel(scaledQuantities.Length, quantitiesBuffer.View, pricesBuffer.View, 
                resultBuffer.View, scaleFactor);
            _gpuContext.Accelerator.Synchronize();

            // Copy result back and convert to decimal
            var scaledResult = resultBuffer.GetAsArray1D();
            var result = scaledResult.Select(s => FromScaledInteger(s, decimalPlaces)).ToArray();

            stopwatch.Stop();
            _logger.LogDebug("GPU portfolio calculation completed: {Count} positions in {ElapsedMs}ms", 
                quantities.Length, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GPU portfolio calculation failed, falling back to CPU");
            return await CalculatePortfolioValuesCpuAsync(quantities, prices);
        }
    }

    /// <summary>
    /// Calculates Value at Risk (VaR) using GPU acceleration for preprocessing
    /// </summary>
    public async Task<decimal> CalculateVaRAsync(
        decimal[] returns, 
        decimal confidenceLevel = 0.95m, 
        int decimalPlaces = 8)
    {
        if (returns == null || returns.Length == 0)
            throw new ArgumentException("Returns array cannot be null or empty");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Use CPU for small datasets
            if (returns.Length < GPU_THRESHOLD_MEDIUM || _varCalculationKernel == null)
            {
                return await CalculateVaRCpuAsync(returns, confidenceLevel);
            }

            // Calculate mean first
            var mean = returns.Average();
            var scaledMean = ToScaledInteger(mean, decimalPlaces);
            var scaleFactor = GetScaleFactor(decimalPlaces);

            // Convert to scaled integers
            var scaledReturns = returns.Select(r => ToScaledInteger(r, decimalPlaces)).ToArray();

            using var returnsBuffer = _gpuContext.Accelerator!.Allocate1D<long>(scaledReturns.Length);
            using var deviationsBuffer = _gpuContext.Accelerator.Allocate1D<long>(scaledReturns.Length);
            using var sortedBuffer = _gpuContext.Accelerator.Allocate1D<long>(scaledReturns.Length);

            returnsBuffer.CopyFromCPU(scaledReturns);

            // Execute preprocessing kernel
            _varCalculationKernel(scaledReturns.Length, returnsBuffer.View, deviationsBuffer.View, 
                sortedBuffer.View, scaledMean, scaleFactor);
            _gpuContext.Accelerator.Synchronize();

            // Copy back and complete calculation on CPU (sorting is complex on GPU)
            var sortedReturns = sortedBuffer.GetAsArray1D()
                .Select(s => FromScaledInteger(s, decimalPlaces))
                .OrderBy(r => r)
                .ToArray();

            var index = (int)((1m - confidenceLevel) * returns.Length);
            index = Math.Max(0, Math.Min(index, returns.Length - 1));

            var var = Math.Abs(sortedReturns[index]);

            stopwatch.Stop();
            _logger.LogDebug("GPU VaR calculation completed: {Count} returns in {ElapsedMs}ms", 
                returns.Length, stopwatch.ElapsedMilliseconds);

            return var;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GPU VaR calculation failed, falling back to CPU");
            return await CalculateVaRCpuAsync(returns, confidenceLevel);
        }
    }

    /// <summary>
    /// Calculates drawdown for a series of portfolio values
    /// </summary>
    public async Task<decimal[]> CalculateDrawdownAsync(decimal[] portfolioValues, int decimalPlaces = 4)
    {
        if (portfolioValues == null || portfolioValues.Length == 0)
            throw new ArgumentException("Portfolio values cannot be null or empty");

        // This calculation is inherently sequential (requires running maximum), so use CPU
        return await Task.Run(() =>
        {
            var drawdowns = new decimal[portfolioValues.Length];
            var runningMax = portfolioValues[0];

            for (int i = 0; i < portfolioValues.Length; i++)
            {
                runningMax = Math.Max(runningMax, portfolioValues[i]);
                
                if (runningMax > 0)
                {
                    drawdowns[i] = (runningMax - portfolioValues[i]) / runningMax;
                }
                else
                {
                    drawdowns[i] = 0m;
                }
            }

            _logger.LogDebug("CPU drawdown calculation completed: {Count} values", portfolioValues.Length);
            return drawdowns;
        });
    }

    #endregion

    #region CPU Fallback Methods

    private async Task<decimal[]> AddCpuAsync(decimal[] operand1, decimal[] operand2)
    {
        return await Task.Run(() =>
        {
            var result = new decimal[operand1.Length];
            for (int i = 0; i < operand1.Length; i++)
            {
                result[i] = operand1[i] + operand2[i];
            }
            return result;
        });
    }

    private async Task<decimal[]> MultiplyCpuAsync(decimal[] operand1, decimal[] operand2)
    {
        return await Task.Run(() =>
        {
            var result = new decimal[operand1.Length];
            for (int i = 0; i < operand1.Length; i++)
            {
                result[i] = operand1[i] * operand2[i];
            }
            return result;
        });
    }

    private async Task<decimal[]> DivideCpuAsync(decimal[] dividend, decimal[] divisor)
    {
        return await Task.Run(() =>
        {
            var result = new decimal[dividend.Length];
            for (int i = 0; i < dividend.Length; i++)
            {
                result[i] = divisor[i] != 0m ? dividend[i] / divisor[i] : 0m;
            }
            return result;
        });
    }

    private async Task<decimal[]> SquareRootCpuAsync(decimal[] values)
    {
        return await Task.Run(() =>
        {
            var result = new decimal[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                result[i] = DecimalSqrt(values[i]);
            }
            return result;
        });
    }

    private async Task<decimal[]> CalculatePortfolioValuesCpuAsync(decimal[] quantities, decimal[] prices)
    {
        return await Task.Run(() =>
        {
            var result = new decimal[quantities.Length];
            for (int i = 0; i < quantities.Length; i++)
            {
                result[i] = quantities[i] * prices[i];
            }
            return result;
        });
    }

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

    #endregion

    #region Utility Methods

    /// <summary>
    /// Converts decimal to scaled integer for GPU processing
    /// </summary>
    public static long ToScaledInteger(decimal value, int decimalPlaces)
    {
        var scaleFactor = GetScaleFactor(decimalPlaces);
        return (long)(value * scaleFactor);
    }

    /// <summary>
    /// Converts scaled integer back to decimal
    /// </summary>
    public static decimal FromScaledInteger(long scaledValue, int decimalPlaces)
    {
        var scaleFactor = GetScaleFactor(decimalPlaces);
        return (decimal)scaledValue / scaleFactor;
    }

    /// <summary>
    /// Gets the scale factor for specified decimal places
    /// </summary>
    public static long GetScaleFactor(int decimalPlaces)
    {
        return decimalPlaces switch
        {
            0 => 1L,
            1 => 10L,
            2 => CURRENCY_SCALE,
            4 => PRICE_SCALE,
            6 => QUANTITY_SCALE,
            8 => RATE_SCALE,
            18 => HIGH_PRECISION_SCALE,
            _ => (long)Math.Pow(10, decimalPlaces)
        };
    }

    /// <summary>
    /// Validates precision is maintained after GPU calculation
    /// </summary>
    public static bool ValidatePrecision(decimal original, long scaled, int decimalPlaces)
    {
        var converted = FromScaledInteger(scaled, decimalPlaces);
        var difference = Math.Abs(original - converted);
        var tolerance = 1m / GetScaleFactor(decimalPlaces + 2);
        
        return difference <= tolerance;
    }

    /// <summary>
    /// Decimal square root using Newton's method (CPU implementation)
    /// </summary>
    private static decimal DecimalSqrt(decimal value)
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
        while (Math.Abs(x - lastX) > 0.0001m);
        
        return Math.Round(x, 4, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Gets GPU performance statistics
    /// </summary>
    public GpuPerformanceStats GetPerformanceStats()
    {
        return new GpuPerformanceStats
        {
            DeviceType = _gpuContext.DeviceType,
            IsGpuAvailable = _gpuContext.Accelerator != null,
            KernelsLoaded = _kernelCache.Count,
            MemoryInfo = _gpuContext.GetMemoryInfo()
        };
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _kernelCache.Clear();
            _logger.LogInformation("GpuDecimalMath disposed");
            _disposed = true;
        }
    }

    #endregion
}

/// <summary>
/// GPU performance statistics for monitoring
/// </summary>
public record GpuPerformanceStats
{
    public string DeviceType { get; init; } = string.Empty;
    public bool IsGpuAvailable { get; init; }
    public int KernelsLoaded { get; init; }
    public string MemoryInfo { get; init; } = string.Empty;
}