using ILGPU;
using ILGPU.Runtime;
// Temporarily removed Core dependencies
using TradingPlatform.GPU.Infrastructure;
using TradingPlatform.GPU.Interfaces;
using TradingPlatform.GPU.Kernels;
using TradingPlatform.GPU.Models;
using System.Diagnostics;

namespace TradingPlatform.GPU.Services;

/// <summary>
/// GPU accelerator implementation using ILGPU for financial calculations
/// </summary>
public class GpuAccelerator : IGpuAccelerator
{
    private readonly GpuContext _context;
    private readonly SimpleLogger _logger;
    private readonly bool _disposed = false;

    // Compiled kernels
    private readonly Action<Index1D, ArrayView2D<long, Stride2D.DenseX>, ArrayView2D<long, Stride2D.DenseX>, int> _smaKernel;
    private readonly Action<Index1D, ArrayView2D<long, Stride2D.DenseX>, ArrayView2D<long, Stride2D.DenseX>, int> _emaKernel;
    private readonly Action<Index1D, ArrayView2D<long, Stride2D.DenseX>, ArrayView2D<long, Stride2D.DenseX>, int> _rsiKernel;
    private readonly Action<Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
        ArrayView1D<long, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, long, long, long, long> _screeningKernel;

    public GpuDeviceInfo DeviceInfo { get; }
    public bool IsGpuAvailable => _context.Device.AcceleratorType != AcceleratorType.CPU;

    public GpuAccelerator(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLogger.Instance;
        
        try
        {
            _context = new GpuContext(_logger);
            
            // Initialize device info
            DeviceInfo = new GpuDeviceInfo
            {
                Name = _context.DeviceName,
                Type = _context.Device.AcceleratorType,
                MemoryGB = _context.DeviceMemoryGB,
                MaxThreadsPerGroup = _context.Device.MaxNumThreadsPerGroup,
                WarpSize = _context.Device.WarpSize,
                IsRtx = _context.DeviceName.ToUpperInvariant().Contains("RTX"),
                Score = 0 // Already selected
            };

            // Compile kernels
            _logger.LogInfo("GPU_KERNEL_COMPILE", "Compiling GPU kernels for financial calculations");
            
            _smaKernel = _context.Accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView2D<long, Stride2D.DenseX>, ArrayView2D<long, Stride2D.DenseX>, int>(
                FinancialKernels.CalculateSMAKernel);
                
            _emaKernel = _context.Accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView2D<long, Stride2D.DenseX>, ArrayView2D<long, Stride2D.DenseX>, int>(
                FinancialKernels.CalculateEMAKernel);
                
            _rsiKernel = _context.Accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView2D<long, Stride2D.DenseX>, ArrayView2D<long, Stride2D.DenseX>, int>(
                FinancialKernels.CalculateRSIKernel);
                
            _screeningKernel = _context.Accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView1D<long, Stride1D.Dense>, ArrayView1D<long, Stride1D.Dense>, 
                ArrayView1D<long, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, long, long, long, long>(
                FinancialKernels.ScreenStocksKernel);
                
            _logger.LogInfo("GPU_ACCELERATOR_READY", 
                $"GPU accelerator initialized with {DeviceInfo.Name}",
                additionalData: DeviceInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError("GPU_ACCELERATOR_INIT_FAILED", "Failed to initialize GPU accelerator", ex);
            throw;
        }
    }

    /// <summary>
    /// Calculates technical indicators using GPU acceleration
    /// </summary>
    public async Task<TechnicalIndicatorResults> CalculateTechnicalIndicatorsAsync(
        string[] symbols,
        decimal[][] prices,
        int[] periods,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInfo("GPU_INDICATORS_START", 
                $"Starting GPU calculation for {symbols.Length} symbols",
                additionalData: new { Symbols = symbols.Length, Periods = periods });

            // Convert decimal prices to scaled long values
            var numSymbols = symbols.Length;
            var numPrices = prices[0].Length;
            var scaledPrices = new long[numSymbols, numPrices];
            
            for (int i = 0; i < numSymbols; i++)
            {
                for (int j = 0; j < numPrices; j++)
                {
                    scaledPrices[i, j] = (long)(prices[i][j] * FinancialKernels.DECIMAL_SCALE);
                }
            }

            // Allocate GPU buffers
            using var pricesBuffer = _context.AllocateBuffer2D<long>(numSymbols, numPrices);
            using var smaBuffer = _context.AllocateBuffer2D<long>(numSymbols, numPrices);
            using var emaBuffer = _context.AllocateBuffer2D<long>(numSymbols, numPrices);
            using var rsiBuffer = _context.AllocateBuffer2D<long>(numSymbols, numPrices);

            // Copy data to GPU
            pricesBuffer.CopyFromCPU(scaledPrices);

            // Launch kernels for each period
            var defaultPeriod = periods.Length > 0 ? periods[0] : 20;
            
            _smaKernel(numSymbols, pricesBuffer.View, smaBuffer.View, defaultPeriod);
            _emaKernel(numSymbols, pricesBuffer.View, emaBuffer.View, defaultPeriod);
            _rsiKernel(numSymbols, pricesBuffer.View, rsiBuffer.View, 14); // RSI typically uses 14 periods

            // Synchronize and get results
            _context.Synchronize();

            // Copy results back to CPU
            var smaResults = new long[numSymbols, numPrices];
            var emaResults = new long[numSymbols, numPrices];
            var rsiResults = new long[numSymbols, numPrices];

            smaBuffer.CopyToCPU(smaResults);
            emaBuffer.CopyToCPU(emaResults);
            rsiBuffer.CopyToCPU(rsiResults);

            // Convert back to decimals and organize results
            var results = new TechnicalIndicatorResults
            {
                Symbols = symbols,
                CalculationTime = stopwatch.Elapsed
            };

            for (int i = 0; i < numSymbols; i++)
            {
                var symbol = symbols[i];
                var smaValues = new decimal[numPrices];
                var emaValues = new decimal[numPrices];
                var rsiValues = new decimal[numPrices];

                for (int j = 0; j < numPrices; j++)
                {
                    smaValues[j] = smaResults[i, j] / (decimal)FinancialKernels.DECIMAL_SCALE;
                    emaValues[j] = emaResults[i, j] / (decimal)FinancialKernels.DECIMAL_SCALE;
                    rsiValues[j] = rsiResults[i, j] / (decimal)FinancialKernels.DECIMAL_SCALE;
                }

                results.SMA[symbol] = smaValues;
                results.EMA[symbol] = emaValues;
                results.RSI[symbol] = rsiValues;
            }

            _logger.LogInfo("GPU_INDICATORS_COMPLETE", 
                $"Completed GPU calculations in {stopwatch.ElapsedMilliseconds}ms",
                additionalData: new { ElapsedMs = stopwatch.ElapsedMilliseconds });

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError("GPU_INDICATORS_FAILED", "Failed to calculate indicators on GPU", ex);
            throw;
        }
    }

    /// <summary>
    /// Screens stocks using GPU acceleration
    /// </summary>
    public async Task<ScreeningResults> ScreenStocksAsync(
        object[] stocks,
        ScreeningCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInfo("GPU_SCREENING_START", 
                $"Starting GPU screening for {stocks.Length} stocks");

            var numStocks = stocks.Length;

            // Convert criteria to scaled values
            long minPrice = criteria.MinPrice.HasValue ? 
                (long)(criteria.MinPrice.Value * FinancialKernels.DECIMAL_SCALE) : 0;
            long maxPrice = criteria.MaxPrice.HasValue ? 
                (long)(criteria.MaxPrice.Value * FinancialKernels.DECIMAL_SCALE) : long.MaxValue;
            long minVolume = (long)(criteria.MinVolume ?? 0);
            long minMarketCap = criteria.MinMarketCap.HasValue ? 
                (long)(criteria.MinMarketCap.Value * FinancialKernels.DECIMAL_SCALE) : 0;

            // Prepare data arrays
            var prices = new long[numStocks];
            var volumes = new long[numStocks];
            var marketCaps = new long[numStocks];

            for (int i = 0; i < numStocks; i++)
            {
                // TODO: Extract proper values when integrated
                prices[i] = 10000L * FinancialKernels.DECIMAL_SCALE; // Placeholder
                volumes[i] = 1000000L; // Placeholder
                marketCaps[i] = 1000000000L * FinancialKernels.DECIMAL_SCALE; // Placeholder
            }

            // Allocate GPU buffers
            using var pricesBuffer = _context.AllocateBuffer<long>(numStocks);
            using var volumesBuffer = _context.AllocateBuffer<long>(numStocks);
            using var marketCapsBuffer = _context.AllocateBuffer<long>(numStocks);
            using var resultsBuffer = _context.AllocateBuffer<byte>(numStocks);

            // Copy data to GPU
            pricesBuffer.CopyFromCPU(prices);
            volumesBuffer.CopyFromCPU(volumes);
            marketCapsBuffer.CopyFromCPU(marketCaps);

            // Launch screening kernel
            _screeningKernel(
                numStocks,
                pricesBuffer.View,
                volumesBuffer.View,
                marketCapsBuffer.View,
                resultsBuffer.View,
                minPrice,
                maxPrice,
                minVolume,
                minMarketCap);

            // Synchronize and get results
            _context.Synchronize();

            var results = new byte[numStocks];
            resultsBuffer.CopyToCPU(results);

            // Build results
            var matchingSymbols = new List<string>();
            var scores = new Dictionary<string, ScreeningScore>();

            for (int i = 0; i < numStocks; i++)
            {
                if (results[i] == 1)
                {
                    // TODO: Add proper stock symbol extraction when integrated
                    matchingSymbols.Add($"STOCK_{i}");
                    scores[$"STOCK_{i}"] = new ScreeningScore
                    {
                        TotalScore = 100m, // Simple pass/fail for now
                        PassedAllCriteria = true
                    };
                }
            }

            _logger.LogInfo("GPU_SCREENING_COMPLETE", 
                $"Screened {numStocks} stocks in {stopwatch.ElapsedMilliseconds}ms, found {matchingSymbols.Count} matches");

            return new ScreeningResults
            {
                MatchingSymbols = matchingSymbols.ToArray(),
                Scores = scores,
                TotalScreened = numStocks,
                ScreeningTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("GPU_SCREENING_FAILED", "Failed to screen stocks on GPU", ex);
            throw;
        }
    }

    /// <summary>
    /// Calculates risk metrics using GPU (placeholder for now)
    /// </summary>
    public async Task<RiskMetrics[]> CalculateRiskMetricsAsync(
        object[] portfolios,
        object marketData,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement VaR, Expected Shortfall, Beta calculations
        _logger.LogWarning("GPU_RISK_NOT_IMPLEMENTED", "Risk metrics GPU calculation not yet implemented");
        
        return portfolios.Select((p, i) => new RiskMetrics
        {
            PortfolioId = $"PORTFOLIO_{i}",
            ValueAtRisk = 0m,
            ExpectedShortfall = 0m,
            Beta = 1m,
            Sharpe = 0m,
            MaxDrawdown = 0m
        }).ToArray();
    }

    /// <summary>
    /// Runs Monte Carlo simulations (placeholder for now)
    /// </summary>
    public async Task<MonteCarloResults> RunMonteCarloSimulationAsync(
        object[] options,
        int simulations,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement Monte Carlo option pricing
        _logger.LogWarning("GPU_MONTECARLO_NOT_IMPLEMENTED", "Monte Carlo GPU simulation not yet implemented");
        
        return new MonteCarloResults
        {
            SimulationsRun = 0,
            SimulationTime = TimeSpan.Zero
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _context?.Dispose();
        }
    }
}

// Placeholder types until integrated with Core models
// These will be removed once Core project is fixed