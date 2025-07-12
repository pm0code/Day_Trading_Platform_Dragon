# Dual GPU Coordination System Design
## Automatic GPU Detection and Task Distribution

**Date**: January 11, 2025  
**Author**: tradingagent  
**Status**: Design Proposal

---

## Overview

Design for automatic GPU detection and intelligent task distribution across RTX 4070 Ti (primary) and RTX 3060 Ti (secondary) GPUs.

## GPU Specifications

| GPU | CUDA Cores | Memory | TFlops | Best For |
|-----|------------|---------|--------|----------|
| RTX 4070 Ti | 7,680 | 12GB GDDR6X | 40.09 | Large models, complex inference |
| RTX 3060 Ti | 4,864 | 8GB GDDR6 | 16.20 | Parallel tasks, smaller models |

## Architecture Design

```csharp
namespace MarketAnalyzer.Infrastructure.AI.GPU
{
    /// <summary>
    /// Manages automatic GPU detection and task distribution
    /// </summary>
    public class DualGPUCoordinator : CanonicalServiceBase
    {
        private readonly ConcurrentDictionary<int, GPUDevice> _gpuDevices;
        private readonly ITaskScheduler _taskScheduler;
        
        public async Task<TradingResult<GPUConfiguration>> DetectGPUsAsync()
        {
            LogMethodEntry();
            
            try
            {
                var devices = new List<GPUDevice>();
                
                // Use CUDA API to enumerate devices
                var deviceCount = CudaRuntime.GetDeviceCount();
                
                for (int i = 0; i < deviceCount; i++)
                {
                    var props = CudaRuntime.GetDeviceProperties(i);
                    var device = new GPUDevice
                    {
                        DeviceId = i,
                        Name = props.DeviceName,
                        TotalMemory = props.TotalMemory,
                        ComputeCapability = $"{props.Major}.{props.Minor}",
                        MultiProcessorCount = props.MultiProcessorCount,
                        ClockRate = props.ClockRateMhz,
                        IsRTX4070Ti = props.DeviceName.Contains("4070 Ti"),
                        IsRTX3060Ti = props.DeviceName.Contains("3060 Ti")
                    };
                    
                    devices.Add(device);
                    _gpuDevices[i] = device;
                    
                    LogInfo($"Detected GPU {i}: {device.Name}, Memory: {device.TotalMemory / (1024*1024*1024)}GB");
                }
                
                // Assign roles based on detected GPUs
                var config = AssignGPURoles(devices);
                
                LogMethodExit();
                return TradingResult<GPUConfiguration>.Success(config);
            }
            catch (Exception ex)
            {
                LogError("GPU detection failed", ex);
                LogMethodExit();
                return TradingResult<GPUConfiguration>.Failure(
                    "GPU_DETECTION_FAILED", 
                    "Failed to detect GPUs", 
                    ex);
            }
        }
        
        private GPUConfiguration AssignGPURoles(List<GPUDevice> devices)
        {
            var config = new GPUConfiguration();
            
            // Intelligent role assignment
            foreach (var device in devices.OrderByDescending(d => d.TotalMemory))
            {
                if (device.IsRTX4070Ti || (!config.HasPrimary && device.TotalMemory >= 12L * 1024 * 1024 * 1024))
                {
                    config.PrimaryGPU = device;
                    config.HasPrimary = true;
                    device.Role = GPURole.Primary;
                }
                else if (device.IsRTX3060Ti || (!config.HasSecondary && device.TotalMemory >= 8L * 1024 * 1024 * 1024))
                {
                    config.SecondaryGPU = device;
                    config.HasSecondary = true;
                    device.Role = GPURole.Secondary;
                }
            }
            
            // Fallback if specific GPUs not found
            if (!config.HasPrimary && devices.Any())
            {
                config.PrimaryGPU = devices.First();
                config.HasPrimary = true;
            }
            
            return config;
        }
    }
}
```

## Task Distribution Strategy

### 1. Task Classification
```csharp
public enum TaskType
{
    // Heavy tasks for RTX 4070 Ti
    LargeModelInference,      // >5GB models
    BatchPrediction,          // >100 symbols
    ModelTraining,            // Fine-tuning
    
    // Light tasks for RTX 3060 Ti
    SmallModelInference,      // <2GB models
    PatternRecognition,       // Technical patterns
    IndicatorCalculation,     // GPU-accelerated indicators
    
    // Parallel tasks (both GPUs)
    PortfolioOptimization,    // Split across GPUs
    MonteCarloSimulation,     // Parallel paths
    RiskCalculation          // VaR, CVaR
}
```

### 2. Dynamic Load Balancing
```csharp
public class GPUTaskScheduler : CanonicalServiceBase
{
    private readonly DualGPUCoordinator _gpuCoordinator;
    private readonly ConcurrentQueue<GPUTask> _taskQueue;
    
    public async Task<TradingResult<T>> ExecuteTaskAsync<T>(
        GPUTask task, 
        CancellationToken cancellationToken)
    {
        LogMethodEntry();
        
        // Select GPU based on task type and current load
        var gpu = await SelectOptimalGPUAsync(task);
        
        if (!gpu.IsSuccess)
        {
            // Fallback to CPU
            return await ExecuteOnCPUAsync<T>(task, cancellationToken);
        }
        
        // Monitor GPU utilization
        using (var monitor = new GPUMonitor(gpu.Value))
        {
            var result = await ExecuteOnGPUAsync<T>(
                task, 
                gpu.Value, 
                cancellationToken);
                
            // Record metrics
            UpdateMetric($"GPU_{gpu.Value.DeviceId}_Utilization", 
                        monitor.GetUtilization());
            
            LogMethodExit();
            return result;
        }
    }
    
    private async Task<TradingResult<GPUDevice>> SelectOptimalGPUAsync(GPUTask task)
    {
        // Get current GPU states
        var primary = _gpuCoordinator.PrimaryGPU;
        var secondary = _gpuCoordinator.SecondaryGPU;
        
        // Decision matrix
        switch (task.Type)
        {
            case TaskType.LargeModelInference:
            case TaskType.ModelTraining:
                // Always use RTX 4070 Ti if available
                if (primary?.IsAvailable() == true)
                    return TradingResult<GPUDevice>.Success(primary);
                break;
                
            case TaskType.SmallModelInference:
            case TaskType.PatternRecognition:
                // Prefer RTX 3060 Ti to keep 4070 Ti free
                if (secondary?.IsAvailable() == true)
                    return TradingResult<GPUDevice>.Success(secondary);
                break;
                
            case TaskType.PortfolioOptimization:
            case TaskType.MonteCarloSimulation:
                // Can use either, choose least loaded
                var leastLoaded = GetLeastLoadedGPU();
                if (leastLoaded != null)
                    return TradingResult<GPUDevice>.Success(leastLoaded);
                break;
        }
        
        // Fallback logic
        return SelectAnyAvailableGPU();
    }
}
```

## Memory Management

```csharp
public class GPUMemoryManager : CanonicalServiceBase
{
    private readonly Dictionary<int, GPUMemoryPool> _memoryPools;
    
    public async Task<TradingResult<GPUMemoryAllocation>> AllocateAsync(
        int deviceId, 
        long sizeBytes,
        MemoryPriority priority)
    {
        LogMethodEntry();
        
        var pool = _memoryPools[deviceId];
        
        // Check available memory
        var available = await pool.GetAvailableMemoryAsync();
        
        if (available < sizeBytes)
        {
            // Try to free memory based on priority
            if (priority == MemoryPriority.High)
            {
                await pool.EvictLowPriorityAllocationsAsync(sizeBytes);
            }
            else
            {
                LogWarning($"Insufficient GPU memory on device {deviceId}");
                return TradingResult<GPUMemoryAllocation>.Failure(
                    "INSUFFICIENT_GPU_MEMORY",
                    $"Need {sizeBytes} bytes, only {available} available");
            }
        }
        
        // Allocate memory
        var allocation = await pool.AllocateAsync(sizeBytes);
        
        LogMethodExit();
        return TradingResult<GPUMemoryAllocation>.Success(allocation);
    }
}
```

## Failover and Recovery

```csharp
public class GPUFailoverManager : CanonicalServiceBase
{
    public async Task<TradingResult<bool>> HandleGPUFailureAsync(
        int failedDeviceId,
        GPUTask failedTask)
    {
        LogMethodEntry();
        LogError($"GPU {failedDeviceId} failure detected");
        
        // 1. Mark GPU as unavailable
        _gpuCoordinator.MarkGPUUnavailable(failedDeviceId);
        
        // 2. Attempt task migration
        var alternateGPU = GetAlternateGPU(failedDeviceId);
        
        if (alternateGPU != null)
        {
            // Migrate to other GPU
            var result = await MigrateTaskAsync(failedTask, alternateGPU);
            if (result.IsSuccess)
            {
                LogInfo($"Task migrated to GPU {alternateGPU.DeviceId}");
                return TradingResult<bool>.Success(true);
            }
        }
        
        // 3. Fallback to CPU
        LogWarning("No alternate GPU available, falling back to CPU");
        var cpuResult = await ExecuteOnCPUAsync(failedTask);
        
        // 4. Attempt GPU recovery
        _ = Task.Run(() => AttemptGPURecoveryAsync(failedDeviceId));
        
        LogMethodExit();
        return cpuResult;
    }
}
```

## Performance Monitoring

```csharp
public class GPUPerformanceMonitor : CanonicalServiceBase
{
    public class GPUMetrics
    {
        public int DeviceId { get; set; }
        public double Utilization { get; set; }
        public long MemoryUsed { get; set; }
        public long MemoryTotal { get; set; }
        public double Temperature { get; set; }
        public double PowerDraw { get; set; }
        public int ActiveTasks { get; set; }
        public double InferencePerSecond { get; set; }
    }
    
    public async Task<TradingResult<List<GPUMetrics>>> GetMetricsAsync()
    {
        var metrics = new List<GPUMetrics>();
        
        foreach (var gpu in _gpuCoordinator.GetActiveGPUs())
        {
            var metric = await CollectGPUMetricsAsync(gpu);
            metrics.Add(metric);
            
            // Alert on issues
            if (metric.Temperature > 85)
            {
                LogWarning($"GPU {gpu.DeviceId} running hot: {metric.Temperature}°C");
            }
            
            if (metric.Utilization < 50 && metric.ActiveTasks > 0)
            {
                LogWarning($"GPU {gpu.DeviceId} underutilized: {metric.Utilization}%");
            }
        }
        
        return TradingResult<List<GPUMetrics>>.Success(metrics);
    }
}
```

## Usage Example

```csharp
// In MarketAnalyzer startup
public class AIInferenceService : CanonicalServiceBase
{
    private readonly DualGPUCoordinator _gpuCoordinator;
    private readonly GPUTaskScheduler _scheduler;
    
    protected override async Task<TradingResult<bool>> OnInitializeAsync(
        CancellationToken cancellationToken)
    {
        // Auto-detect GPUs
        var gpuConfig = await _gpuCoordinator.DetectGPUsAsync();
        
        if (!gpuConfig.IsSuccess)
        {
            LogWarning("No GPUs detected, will use CPU inference");
            return TradingResult<bool>.Success(true);
        }
        
        LogInfo($"Detected GPUs: Primary={gpuConfig.Value.PrimaryGPU?.Name}, " +
                $"Secondary={gpuConfig.Value.SecondaryGPU?.Name}");
        
        return TradingResult<bool>.Success(true);
    }
    
    public async Task<TradingResult<PricePrediction>> PredictPriceAsync(
        string symbol,
        MarketData data)
    {
        // Large model goes to RTX 4070 Ti
        var task = new GPUTask
        {
            Type = TaskType.LargeModelInference,
            ModelName = "price_prediction_transformer",
            Input = data.ToTensor(),
            Priority = TaskPriority.High
        };
        
        return await _scheduler.ExecuteTaskAsync<PricePrediction>(
            task, 
            CancellationToken.None);
    }
}
```

## Benefits

1. **Automatic Detection**: No manual GPU configuration needed
2. **Intelligent Distribution**: Tasks go to optimal GPU
3. **Load Balancing**: Prevents GPU bottlenecks
4. **Failover**: Continues operation if GPU fails
5. **Monitoring**: Real-time performance metrics
6. **Memory Management**: Prevents OOM errors
7. **Priority System**: Critical tasks get resources

---

*This design ensures optimal utilization of both GPUs while maintaining system reliability.*