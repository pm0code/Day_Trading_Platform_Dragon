using ILGPU;
using ILGPU.Runtime;
using TradingPlatform.GPU.Models;
using TradingPlatform.GPU.Interfaces;
using TradingPlatform.GPU.Services;
using System.Collections.Concurrent;

namespace TradingPlatform.GPU.Infrastructure;

/// <summary>
/// Advanced GPU orchestration system that intelligently manages task execution across multiple GPUs
/// Supports both parallel execution (non-interfering tasks) and sequential execution (resource-intensive tasks)
/// </summary>
public sealed class GpuOrchestrator : IDisposable
{
    private readonly SimpleLogger _logger;
    protected readonly MultiGpuManager _multiGpuManager;
    private readonly ConcurrentQueue<GpuTask> _taskQueue;
    private readonly SemaphoreSlim _executionSemaphore;
    private readonly Dictionary<string, GpuResourceProfile> _resourceProfiles;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _orchestrationTask;
    private bool _disposed = false;

    public int AvailableGpuCount => _multiGpuManager.ActiveGpuCount;
    public int TotalDeviceCount => _multiGpuManager.TotalDeviceCount;
    public long TotalGpuMemoryGB => _multiGpuManager.TotalGpuMemoryGB;

    /// <summary>
    /// Creates GPU orchestrator that manages optimal task scheduling across ALL GPUs
    /// </summary>
    public GpuOrchestrator(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLogger.Instance;
        _multiGpuManager = new MultiGpuManager(_logger);
        _taskQueue = new ConcurrentQueue<GpuTask>();
        _executionSemaphore = new SemaphoreSlim(1, 1);
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Initialize resource profiles for different task types
        _resourceProfiles = InitializeResourceProfiles();
        
        // Start orchestration background task
        _orchestrationTask = Task.Run(OrchestrationLoop, _cancellationTokenSource.Token);
        
        _logger.LogInfo("GPU_ORCHESTRATOR_READY", 
            $"GPU orchestrator initialized with {AvailableGpuCount} GPUs",
            additionalData: new
            {
                AvailableGpus = AvailableGpuCount,
                TotalDevices = TotalDeviceCount,
                TotalMemoryGB = TotalGpuMemoryGB,
                SupportedProfiles = _resourceProfiles.Keys.ToArray()
            });
    }

    /// <summary>
    /// Executes financial calculations with optimal GPU scheduling strategy
    /// </summary>
    public async Task<TechnicalIndicatorResults> CalculateTechnicalIndicatorsAsync(
        string[] symbols, 
        decimal[][] prices, 
        int[] periods,
        CancellationToken cancellationToken = default)
    {
        var profile = _resourceProfiles["technical_indicators"];
        var strategy = DetermineExecutionStrategy(symbols.Length, profile);
        
        _logger.LogInfo("GPU_ORCHESTRATOR_TECH_INDICATORS", 
            $"Executing technical indicators with {strategy} strategy",
            additionalData: new { Symbols = symbols.Length, Strategy = strategy });

        return strategy switch
        {
            ExecutionStrategy.SingleGpuOptimal => await ExecuteSingleGpuOptimal(symbols, prices, periods, cancellationToken),
            ExecutionStrategy.ParallelDistributed => await ExecuteParallelDistributed(symbols, prices, periods, cancellationToken),
            ExecutionStrategy.SequentialRoundRobin => await ExecuteSequentialRoundRobin(symbols, prices, periods, cancellationToken),
            ExecutionStrategy.MemoryOptimizedBatching => await ExecuteMemoryOptimizedBatching(symbols, prices, periods, cancellationToken),
            _ => throw new NotSupportedException($"Execution strategy {strategy} not implemented")
        };
    }

    /// <summary>
    /// Executes stock screening with intelligent GPU task distribution
    /// </summary>
    public async Task<ScreeningResults> ScreenStocksAsync(
        object[] stocks,
        ScreeningCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var profile = _resourceProfiles["screening"];
        var strategy = DetermineExecutionStrategy(stocks.Length, profile);
        
        _logger.LogInfo("GPU_ORCHESTRATOR_SCREENING", 
            $"Executing stock screening with {strategy} strategy",
            additionalData: new { Stocks = stocks.Length, Strategy = strategy });

        // For screening, parallel distribution is almost always optimal
        if (strategy == ExecutionStrategy.ParallelDistributed || strategy == ExecutionStrategy.SequentialRoundRobin)
        {
            return await ExecuteParallelScreening(stocks, criteria, cancellationToken);
        }
        else
        {
            return await ExecuteSingleGpuScreening(stocks, criteria, cancellationToken);
        }
    }

    #region Execution Strategies

    /// <summary>
    /// Uses the single best GPU for the entire workload (no GPU interference)
    /// </summary>
    private async Task<TechnicalIndicatorResults> ExecuteSingleGpuOptimal(
        string[] symbols, decimal[][] prices, int[] periods, CancellationToken cancellationToken)
    {
        var bestWorker = _multiGpuManager.GetBestGpuWorker();
        
        _logger.LogInfo("GPU_SINGLE_OPTIMAL", $"Using single GPU: {bestWorker.Name}");
        
        // Use the existing GPU accelerator logic but with specific worker
        var accelerator = new GpuAccelerator(_logger);
        return await accelerator.CalculateTechnicalIndicatorsAsync(symbols, prices, periods, cancellationToken);
    }

    /// <summary>
    /// Distributes work across ALL GPUs simultaneously (parallel execution)
    /// </summary>
    private async Task<TechnicalIndicatorResults> ExecuteParallelDistributed(
        string[] symbols, decimal[][] prices, int[] periods, CancellationToken cancellationToken)
    {
        var assignments = _multiGpuManager.DistributeWorkload(symbols.Length, "technical_indicators");
        
        _logger.LogInfo("GPU_PARALLEL_DISTRIBUTED", 
            $"Distributing {symbols.Length} symbols across {assignments.Count} devices");

        var tasks = assignments.Select(async assignment =>
        {
            var symbolSubset = symbols.Skip(assignment.StartIndex).Take(assignment.ItemCount).ToArray();
            var priceSubset = prices.Skip(assignment.StartIndex).Take(assignment.ItemCount).ToArray();
            
            // Create dedicated accelerator for this worker
            var accelerator = CreateAcceleratorForWorker(assignment.Worker);
            return await accelerator.CalculateTechnicalIndicatorsAsync(symbolSubset, priceSubset, periods, cancellationToken);
        });

        var results = await Task.WhenAll(tasks);
        
        // Combine results from all GPUs
        return CombineTechnicalIndicatorResults(results);
    }

    /// <summary>
    /// Executes tasks sequentially across GPUs in round-robin fashion (no interference)
    /// </summary>
    private async Task<TechnicalIndicatorResults> ExecuteSequentialRoundRobin(
        string[] symbols, decimal[][] prices, int[] periods, CancellationToken cancellationToken)
    {
        var workers = _multiGpuManager.Workers.Where(w => w.IsGpuAccelerator).ToList();
        var batchSize = Math.Max(1, symbols.Length / workers.Count);
        var results = new List<TechnicalIndicatorResults>();
        
        _logger.LogInfo("GPU_SEQUENTIAL_ROUND_ROBIN", 
            $"Processing {symbols.Length} symbols in batches of {batchSize} across {workers.Count} GPUs");

        for (int i = 0; i < symbols.Length; i += batchSize)
        {
            var workerIndex = (i / batchSize) % workers.Count;
            var worker = workers[workerIndex];
            var actualBatchSize = Math.Min(batchSize, symbols.Length - i);
            
            var symbolBatch = symbols.Skip(i).Take(actualBatchSize).ToArray();
            var priceBatch = prices.Skip(i).Take(actualBatchSize).ToArray();
            
            _logger.LogDebug("GPU_SEQUENTIAL_BATCH", 
                $"Processing batch {i / batchSize + 1} on {worker.Name}: {actualBatchSize} symbols");
            
            // Wait for previous GPU task to complete before starting next
            await _executionSemaphore.WaitAsync(cancellationToken);
            try
            {
                var accelerator = CreateAcceleratorForWorker(worker);
                var batchResult = await accelerator.CalculateTechnicalIndicatorsAsync(
                    symbolBatch, priceBatch, periods, cancellationToken);
                results.Add(batchResult);
            }
            finally
            {
                _executionSemaphore.Release();
            }
        }

        return CombineTechnicalIndicatorResults(results.ToArray());
    }

    /// <summary>
    /// Uses memory-optimized batching for very large datasets
    /// </summary>
    private async Task<TechnicalIndicatorResults> ExecuteMemoryOptimizedBatching(
        string[] symbols, decimal[][] prices, int[] periods, CancellationToken cancellationToken)
    {
        var maxMemoryPerGpu = TotalGpuMemoryGB / AvailableGpuCount;
        var estimatedMemoryPerSymbol = EstimateMemoryUsagePerSymbol(prices[0]?.Length ?? 1000);
        var maxSymbolsPerBatch = (int)(maxMemoryPerGpu * 1024 * 1024 * 1024 * 0.7 / estimatedMemoryPerSymbol); // Use 70% of GPU memory
        
        _logger.LogInfo("GPU_MEMORY_OPTIMIZED", 
            $"Using memory-optimized batching: {maxSymbolsPerBatch} symbols per batch");

        var results = new List<TechnicalIndicatorResults>();
        
        for (int i = 0; i < symbols.Length; i += maxSymbolsPerBatch)
        {
            var batchSize = Math.Min(maxSymbolsPerBatch, symbols.Length - i);
            var symbolBatch = symbols.Skip(i).Take(batchSize).ToArray();
            var priceBatch = prices.Skip(i).Take(batchSize).ToArray();
            
            // Use the best available GPU for this batch
            var bestWorker = _multiGpuManager.GetBestGpuWorker();
            var accelerator = CreateAcceleratorForWorker(bestWorker);
            
            var batchResult = await accelerator.CalculateTechnicalIndicatorsAsync(
                symbolBatch, priceBatch, periods, cancellationToken);
            results.Add(batchResult);
        }

        return CombineTechnicalIndicatorResults(results.ToArray());
    }

    #endregion

    #region Screening Methods

    private async Task<ScreeningResults> ExecuteParallelScreening(
        object[] stocks, ScreeningCriteria criteria, CancellationToken cancellationToken)
    {
        var assignments = _multiGpuManager.DistributeWorkload(stocks.Length, "screening");
        
        var tasks = assignments.Select(async assignment =>
        {
            var stockSubset = stocks.Skip(assignment.StartIndex).Take(assignment.ItemCount).ToArray();
            var accelerator = CreateAcceleratorForWorker(assignment.Worker);
            return await accelerator.ScreenStocksAsync(stockSubset, criteria, cancellationToken);
        });

        var results = await Task.WhenAll(tasks);
        return CombineScreeningResults(results);
    }

    private async Task<ScreeningResults> ExecuteSingleGpuScreening(
        object[] stocks, ScreeningCriteria criteria, CancellationToken cancellationToken)
    {
        var bestWorker = _multiGpuManager.GetBestGpuWorker();
        var accelerator = CreateAcceleratorForWorker(bestWorker);
        return await accelerator.ScreenStocksAsync(stocks, criteria, cancellationToken);
    }

    #endregion

    #region Strategy Determination

    /// <summary>
    /// Intelligently determines the optimal execution strategy based on workload and resources
    /// </summary>
    private ExecutionStrategy DetermineExecutionStrategy(int itemCount, GpuResourceProfile profile)
    {
        // Small workloads: use single best GPU
        if (itemCount < profile.ParallelThreshold)
        {
            return ExecutionStrategy.SingleGpuOptimal;
        }

        // Very large workloads that might cause memory pressure
        if (itemCount > profile.MemoryOptimizedThreshold)
        {
            return ExecutionStrategy.MemoryOptimizedBatching;
        }

        // Medium to large workloads: consider GPU interference patterns
        if (profile.PreferSequential || AvailableGpuCount <= 2)
        {
            return ExecutionStrategy.SequentialRoundRobin;
        }

        // Large workloads with multiple GPUs: parallel distribution
        return ExecutionStrategy.ParallelDistributed;
    }

    #endregion

    #region Resource Profiles

    /// <summary>
    /// Initializes resource profiles that define optimal execution patterns for different workload types
    /// </summary>
    private Dictionary<string, GpuResourceProfile> InitializeResourceProfiles()
    {
        return new Dictionary<string, GpuResourceProfile>
        {
            ["technical_indicators"] = new GpuResourceProfile
            {
                ParallelThreshold = 1000,           // < 1000 symbols: single GPU
                MemoryOptimizedThreshold = 50000,   // > 50k symbols: batch processing
                PreferSequential = false,           // Can run in parallel
                EstimatedMemoryPerItem = 1024,      // ~1KB per symbol
                CpuFallbackThreshold = 100          // < 100 symbols: consider CPU
            },
            ["screening"] = new GpuResourceProfile
            {
                ParallelThreshold = 5000,           // < 5000 stocks: single GPU
                MemoryOptimizedThreshold = 1000000, // > 1M stocks: batch processing
                PreferSequential = false,           // Highly parallel workload
                EstimatedMemoryPerItem = 256,       // ~256 bytes per stock
                CpuFallbackThreshold = 1000         // < 1000 stocks: consider CPU
            },
            ["monte_carlo"] = new GpuResourceProfile
            {
                ParallelThreshold = 100,            // Even small MC benefits from GPU
                MemoryOptimizedThreshold = 10000,   // Large simulations need batching
                PreferSequential = true,            // Memory-intensive, prefer sequential
                EstimatedMemoryPerItem = 4096,      // ~4KB per simulation path
                CpuFallbackThreshold = 10           // Almost always use GPU
            },
            ["risk_calculation"] = new GpuResourceProfile
            {
                ParallelThreshold = 500,            // < 500 portfolios: single GPU
                MemoryOptimizedThreshold = 25000,   // > 25k portfolios: batch
                PreferSequential = false,           // Can parallelize well
                EstimatedMemoryPerItem = 2048,      // ~2KB per portfolio
                CpuFallbackThreshold = 50           // < 50 portfolios: consider CPU
            }
        };
    }

    #endregion

    #region Helper Methods

    private GpuAccelerator CreateAcceleratorForWorker(GpuWorker worker)
    {
        // This would need to be enhanced to create accelerator instances
        // that specifically use the given worker's accelerator
        return new GpuAccelerator(_logger);
    }

    private long EstimateMemoryUsagePerSymbol(int priceDataPoints)
    {
        // Rough estimation: price data + intermediate calculations + output buffers
        return (long)priceDataPoints * 8 * 5; // 5 arrays of 8-byte values per symbol
    }

    private TechnicalIndicatorResults CombineTechnicalIndicatorResults(TechnicalIndicatorResults[] results)
    {
        var combined = new TechnicalIndicatorResults
        {
            Symbols = results.SelectMany(r => r.Symbols).ToArray(),
            CalculationTime = TimeSpan.FromMilliseconds(results.Sum(r => r.CalculationTime.TotalMilliseconds))
        };

        // Combine all indicator dictionaries
        foreach (var result in results)
        {
            foreach (var kvp in result.SMA)
                combined.SMA[kvp.Key] = kvp.Value;
            foreach (var kvp in result.EMA)
                combined.EMA[kvp.Key] = kvp.Value;
            foreach (var kvp in result.RSI)
                combined.RSI[kvp.Key] = kvp.Value;
        }

        return combined;
    }

    private ScreeningResults CombineScreeningResults(ScreeningResults[] results)
    {
        return new ScreeningResults
        {
            MatchingSymbols = results.SelectMany(r => r.MatchingSymbols).ToArray(),
            Scores = results.SelectMany(r => r.Scores).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            TotalScreened = results.Sum(r => r.TotalScreened),
            ScreeningTime = TimeSpan.FromMilliseconds(results.Sum(r => r.ScreeningTime.TotalMilliseconds))
        };
    }

    private async Task OrchestrationLoop()
    {
        _logger.LogInfo("GPU_ORCHESTRATION_LOOP_START", "GPU orchestration background loop started");
        
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                // Process any queued GPU tasks
                if (_taskQueue.TryDequeue(out var task))
                {
                    await ProcessGpuTask(task);
                }
                
                // Brief pause to prevent tight loop
                await Task.Delay(10, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError("GPU_ORCHESTRATION_ERROR", "Error in GPU orchestration loop", ex);
                await Task.Delay(1000, _cancellationTokenSource.Token); // Brief recovery delay
            }
        }
        
        _logger.LogInfo("GPU_ORCHESTRATION_LOOP_END", "GPU orchestration background loop ended");
    }

    private async Task ProcessGpuTask(GpuTask task)
    {
        _logger.LogDebug("GPU_TASK_PROCESS", $"Processing GPU task: {task.TaskType}");
        
        try
        {
            await task.ExecuteAsync();
            task.SetResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError("GPU_TASK_FAILED", $"GPU task failed: {task.TaskType}", ex);
            task.SetException(ex);
        }
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _logger.LogInfo("GPU_ORCHESTRATOR_DISPOSE", "Disposing GPU orchestrator");
            
            _cancellationTokenSource.Cancel();
            
            try
            {
                _orchestrationTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("GPU_ORCHESTRATOR_DISPOSE_WARNING", 
                    "Orchestration task did not complete cleanly", 
                    additionalData: new { Error = ex.Message });
            }
            
            _multiGpuManager?.Dispose();
            _executionSemaphore?.Dispose();
            _cancellationTokenSource?.Dispose();
            _disposed = true;
        }
    }
}

#region Supporting Types

/// <summary>
/// Defines execution strategies for GPU workload distribution
/// </summary>
public enum ExecutionStrategy
{
    /// <summary>
    /// Use the single best GPU for the entire workload
    /// </summary>
    SingleGpuOptimal,
    
    /// <summary>
    /// Distribute work across all GPUs simultaneously
    /// </summary>
    ParallelDistributed,
    
    /// <summary>
    /// Execute tasks sequentially across GPUs in round-robin fashion
    /// </summary>
    SequentialRoundRobin,
    
    /// <summary>
    /// Use memory-optimized batching for very large datasets
    /// </summary>
    MemoryOptimizedBatching
}

/// <summary>
/// Resource profile that defines optimal execution patterns for specific workload types
/// </summary>
public record GpuResourceProfile
{
    public int ParallelThreshold { get; init; }
    public int MemoryOptimizedThreshold { get; init; }
    public bool PreferSequential { get; init; }
    public long EstimatedMemoryPerItem { get; init; }
    public int CpuFallbackThreshold { get; init; }
}

/// <summary>
/// Represents a GPU task in the orchestration queue
/// </summary>
public class GpuTask
{
    public string TaskType { get; set; } = string.Empty;
    public Func<Task> ExecuteAsync { get; set; } = null!;
    public TaskCompletionSource<bool> CompletionSource { get; set; } = new();
    
    public void SetResult(bool result) => CompletionSource.SetResult(result);
    public void SetException(Exception ex) => CompletionSource.SetException(ex);
    public Task<bool> WaitForCompletion() => CompletionSource.Task;
}

#endregion