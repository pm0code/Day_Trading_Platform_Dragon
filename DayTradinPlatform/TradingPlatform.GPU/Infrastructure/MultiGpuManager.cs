using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using ILGPU.Runtime.CPU;
using TradingPlatform.GPU.Models;

namespace TradingPlatform.GPU.Infrastructure;

/// <summary>
/// Manages multiple GPUs simultaneously for maximum parallel processing power
/// Automatically detects, prioritizes, and distributes workloads across ALL available GPUs
/// </summary>
public sealed class MultiGpuManager : IDisposable
{
    private readonly SimpleLogger _logger;
    private readonly Context _context;
    private readonly List<GpuWorker> _workers;
    private readonly object _workloadLock = new();
    private bool _disposed = false;

    public IReadOnlyList<GpuWorker> Workers => _workers.AsReadOnly();
    public int ActiveGpuCount => _workers.Count(w => w.IsGpuAccelerator);
    public int TotalDeviceCount => _workers.Count;
    public long TotalGpuMemoryGB => _workers.Where(w => w.IsGpuAccelerator).Sum(w => w.MemoryGB);

    /// <summary>
    /// Creates multi-GPU manager and initializes ALL available accelerators
    /// </summary>
    public MultiGpuManager(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLogger.Instance;
        _workers = new List<GpuWorker>();
        
        try
        {
            _logger.LogInfo("MULTI_GPU_INIT", "Initializing multi-GPU manager for ALL available devices");
            
            _context = Context.CreateDefault();
            InitializeAllAccelerators();
            
            _logger.LogInfo("MULTI_GPU_READY", 
                $"Multi-GPU manager ready with {ActiveGpuCount} GPUs and {TotalDeviceCount} total devices",
                additionalData: new
                {
                    ActiveGpus = ActiveGpuCount,
                    TotalDevices = TotalDeviceCount,
                    TotalGpuMemoryGB = TotalGpuMemoryGB,
                    Workers = _workers.Select(w => new { w.Name, w.Type, w.MemoryGB, w.Score }).ToArray()
                });
        }
        catch (Exception ex)
        {
            _logger.LogError("MULTI_GPU_INIT_FAILED", "Failed to initialize multi-GPU manager", ex);
            throw;
        }
    }

    /// <summary>
    /// Initializes accelerators for ALL available devices in the system
    /// </summary>
    private void InitializeAllAccelerators()
    {
        var devices = _context.GetDevices().ToArray();
        _logger.LogInfo("MULTI_GPU_SCAN", $"Scanning {devices.Length} total devices for GPU acceleration");

        // Score and sort all devices
        var scoredDevices = devices
            .Select(device => new
            {
                Device = device,
                Score = CalculateDeviceScore(device)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        // Initialize accelerator for EVERY device
        foreach (var scored in scoredDevices)
        {
            try
            {
                var accelerator = CreateAcceleratorForDevice(scored.Device);
                var worker = new GpuWorker(accelerator, scored.Score, _logger);
                _workers.Add(worker);
                
                _logger.LogInfo("MULTI_GPU_DEVICE_ADDED", 
                    $"Added device: {worker.Name} (Score: {worker.Score})",
                    additionalData: new
                    {
                        DeviceType = worker.Type,
                        MemoryGB = worker.MemoryGB,
                        IsGpu = worker.IsGpuAccelerator,
                        MaxThreads = worker.MaxThreadsPerGroup
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("MULTI_GPU_DEVICE_FAILED", 
                    $"Failed to initialize device {scored.Device.Name}: {ex.Message}");
            }
        }

        if (_workers.Count == 0)
        {
            throw new InvalidOperationException("No accelerators could be initialized");
        }
    }

    /// <summary>
    /// Creates accelerator for any device type
    /// </summary>
    private Accelerator CreateAcceleratorForDevice(Device device)
    {
        return device.AcceleratorType switch
        {
            AcceleratorType.Cuda => _context.CreateCudaAccelerator(device.DeviceId),
            AcceleratorType.OpenCL => _context.CreateCLAccelerator(device.DeviceId),
            AcceleratorType.CPU => _context.CreateCPUAccelerator(device.DeviceId),
            _ => throw new NotSupportedException($"Accelerator type {device.AcceleratorType} not supported")
        };
    }

    /// <summary>
    /// Distributes work across ALL available accelerators based on their capabilities
    /// </summary>
    public List<WorkloadAssignment> DistributeWorkload(int totalItems, string workloadType = "default")
    {
        lock (_workloadLock)
        {
            var assignments = new List<WorkloadAssignment>();
            
            if (totalItems <= 0)
                return assignments;

            // Calculate relative performance weights for each worker
            var totalScore = _workers.Sum(w => GetWorkloadScore(w, workloadType));
            
            if (totalScore <= 0)
            {
                // Fallback: use best single device
                var bestWorker = _workers.OrderByDescending(w => w.Score).First();
                assignments.Add(new WorkloadAssignment
                {
                    Worker = bestWorker,
                    StartIndex = 0,
                    ItemCount = totalItems,
                    WorkloadType = workloadType
                });
                return assignments;
            }

            // Distribute work proportionally across ALL workers
            int assignedItems = 0;
            for (int i = 0; i < _workers.Count; i++)
            {
                var worker = _workers[i];
                var workerScore = GetWorkloadScore(worker, workloadType);
                
                if (workerScore <= 0) continue;
                
                // Calculate this worker's share of the total workload
                int itemCount;
                if (i == _workers.Count - 1)
                {
                    // Last worker gets all remaining items
                    itemCount = totalItems - assignedItems;
                }
                else
                {
                    // Proportional distribution based on capability score
                    itemCount = (int)Math.Round((double)totalItems * workerScore / totalScore);
                }
                
                if (itemCount > 0)
                {
                    assignments.Add(new WorkloadAssignment
                    {
                        Worker = worker,
                        StartIndex = assignedItems,
                        ItemCount = itemCount,
                        WorkloadType = workloadType
                    });
                    assignedItems += itemCount;
                }
            }

            _logger.LogInfo("MULTI_GPU_WORKLOAD_DISTRIBUTED", 
                $"Distributed {totalItems} items across {assignments.Count} devices",
                additionalData: new
                {
                    TotalItems = totalItems,
                    WorkloadType = workloadType,
                    Assignments = assignments.Select(a => new 
                    {
                        Device = a.Worker.Name,
                        StartIndex = a.StartIndex,
                        ItemCount = a.ItemCount,
                        Percentage = (double)a.ItemCount / totalItems * 100
                    }).ToArray()
                });

            return assignments;
        }
    }

    /// <summary>
    /// Gets workload-specific performance score for a worker
    /// </summary>
    private int GetWorkloadScore(GpuWorker worker, string workloadType)
    {
        var baseScore = worker.Score;
        
        // Adjust score based on workload type
        return workloadType.ToLowerInvariant() switch
        {
            "technical_indicators" => worker.IsGpuAccelerator ? baseScore : baseScore / 10, // Prefer GPU
            "screening" => worker.IsGpuAccelerator ? baseScore : baseScore / 5,            // Prefer GPU  
            "monte_carlo" => worker.IsGpuAccelerator ? baseScore : baseScore / 20,         // Strongly prefer GPU
            "risk_calculation" => baseScore,                                               // Any device OK
            _ => baseScore
        };
    }

    /// <summary>
    /// Executes work across all assigned accelerators in parallel
    /// </summary>
    public async Task<T[]> ExecuteParallelWorkload<T>(
        List<WorkloadAssignment> assignments,
        Func<GpuWorker, int, int, Task<T[]>> workFunction)
    {
        var tasks = assignments.Select(async assignment =>
        {
            try
            {
                _logger.LogDebug("MULTI_GPU_TASK_START", 
                    $"Starting task on {assignment.Worker.Name}: {assignment.ItemCount} items");
                
                var result = await workFunction(assignment.Worker, assignment.StartIndex, assignment.ItemCount);
                
                _logger.LogDebug("MULTI_GPU_TASK_COMPLETE", 
                    $"Completed task on {assignment.Worker.Name}");
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("MULTI_GPU_TASK_FAILED", 
                    $"Task failed on {assignment.Worker.Name}", ex);
                return Array.Empty<T>();
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        
        // Combine results in correct order
        var combinedResults = new List<T>();
        for (int i = 0; i < assignments.Count; i++)
        {
            combinedResults.AddRange(results[i]);
        }

        return combinedResults.ToArray();
    }

    /// <summary>
    /// Gets the best GPU worker for single-device operations
    /// </summary>
    public GpuWorker GetBestGpuWorker()
    {
        return _workers
            .Where(w => w.IsGpuAccelerator)
            .OrderByDescending(w => w.Score)
            .FirstOrDefault() ?? _workers.OrderByDescending(w => w.Score).First();
    }

    /// <summary>
    /// Synchronizes all accelerators
    /// </summary>
    public void SynchronizeAll()
    {
        foreach (var worker in _workers)
        {
            worker.Synchronize();
        }
    }

    /// <summary>
    /// Enhanced device scoring for multi-GPU scenarios
    /// </summary>
    private int CalculateDeviceScore(Device device)
    {
        int score = 0;
        
        // Base device type scoring
        score += device.AcceleratorType switch
        {
            AcceleratorType.Cuda => 1000,      // NVIDIA GPUs preferred
            AcceleratorType.OpenCL => 500,     // AMD/Intel GPUs second
            AcceleratorType.CPU => 100,        // CPU last resort
            _ => 0
        };

        // Memory scoring (more memory = better for large datasets)
        var memoryGB = device.MemorySize / (1024L * 1024L * 1024L);
        score += (int)Math.Min(memoryGB * 10, 500); // Cap at 50GB effective scoring

        // RTX series detection and generation bonuses
        var name = device.Name.ToUpperInvariant();
        if (name.Contains("RTX"))
        {
            score += 500; // Base RTX bonus
            
            // Generation-specific bonuses (newer = better)
            if (name.Contains("RTX 40")) score += 300;      // RTX 40xx series
            else if (name.Contains("RTX 30")) score += 200; // RTX 30xx series  
            else if (name.Contains("RTX 20")) score += 100; // RTX 20xx series
            
            // High-end model detection
            if (name.Contains("4090") || name.Contains("4080")) score += 200;
            else if (name.Contains("3090") || name.Contains("3080")) score += 150;
            else if (name.Contains("2080")) score += 100;
        }

        // Quadro/Professional GPU bonuses
        if (name.Contains("QUADRO") || name.Contains("TESLA") || name.Contains("A100") || name.Contains("H100"))
        {
            score += 400; // Professional compute GPUs
        }

        // Compute capability bonus for CUDA devices
        if (device.AcceleratorType == AcceleratorType.Cuda)
        {
            score += 100; // CUDA ecosystem bonus
        }

        return score;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _logger.LogInfo("MULTI_GPU_DISPOSE", "Disposing multi-GPU manager and all workers");
            
            foreach (var worker in _workers)
            {
                worker.Dispose();
            }
            _workers.Clear();
            
            _context?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents a single GPU/accelerator worker in the multi-GPU system
/// </summary>
public sealed class GpuWorker : IDisposable
{
    private readonly SimpleLogger _logger;
    private readonly Accelerator _accelerator;
    private bool _disposed = false;

    public string Name => _accelerator.Device.Name;
    public AcceleratorType Type => _accelerator.Device.AcceleratorType;
    public long MemoryGB => _accelerator.Device.MemorySize / (1024L * 1024L * 1024L);
    public int MaxThreadsPerGroup => _accelerator.Device.MaxNumThreadsPerGroup;
    public int WarpSize => _accelerator.Device.WarpSize;
    public bool IsGpuAccelerator => Type != AcceleratorType.CPU;
    public int Score { get; }
    public Accelerator Accelerator => _accelerator;

    public GpuWorker(Accelerator accelerator, int score, SimpleLogger logger)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        Score = score;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Allocates memory buffer on this specific device
    /// </summary>
    public MemoryBuffer1D<T, Stride1D.Dense> AllocateBuffer<T>(int length) where T : unmanaged
    {
        return _accelerator.Allocate1D<T>(length);
    }

    /// <summary>
    /// Allocates 2D memory buffer on this specific device
    /// </summary>
    public MemoryBuffer2D<T, Stride2D.DenseX> AllocateBuffer2D<T>(int width, int height) where T : unmanaged
    {
        return _accelerator.Allocate2DDenseX<T>(new Index2D(width, height));
    }

    /// <summary>
    /// Synchronizes this specific accelerator
    /// </summary>
    public void Synchronize()
    {
        _accelerator.Synchronize();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _logger.LogDebug("GPU_WORKER_DISPOSE", $"Disposing GPU worker: {Name}");
            _accelerator?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents a workload assignment to a specific GPU worker
/// </summary>
public record WorkloadAssignment
{
    public GpuWorker Worker { get; init; } = null!;
    public int StartIndex { get; init; }
    public int ItemCount { get; init; }
    public string WorkloadType { get; init; } = string.Empty;
}