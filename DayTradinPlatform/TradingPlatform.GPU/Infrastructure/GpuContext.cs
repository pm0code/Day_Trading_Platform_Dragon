using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using ILGPU.Runtime.CPU;
// Temporarily removed Core dependencies

namespace TradingPlatform.GPU.Infrastructure;

/// <summary>
/// Manages GPU context and device selection for the trading platform
/// </summary>
public sealed class GpuContext : IDisposable
{
    private readonly SimpleLogger _logger;
    private readonly Context _context;
    private readonly Accelerator _accelerator;
    private readonly bool _disposed = false;

    public Accelerator Accelerator => _accelerator;
    public Device Device => _accelerator.Device;
    public string DeviceName => Device.Name;
    public long DeviceMemoryGB => Device.MemorySize / (1024L * 1024L * 1024L);
    public AcceleratorType DeviceType => Device.AcceleratorType;

    /// <summary>
    /// Creates a new GPU context, preferring NVIDIA RTX GPUs when available
    /// </summary>
    public GpuContext(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLogger.Instance;
        
        try
        {
            _logger.LogInfo("GPU_CONTEXT_INIT", "Initializing GPU context for trading platform");
            
            // Create main context
            _context = Context.CreateDefault();

            // Get best available accelerator
            _accelerator = SelectBestAccelerator();
            
            _logger.LogInfo("GPU_DEVICE_SELECTED", 
                $"Selected GPU: {DeviceName} with {DeviceMemoryGB}GB memory",
                additionalData: new
                {
                    DeviceType = Device.AcceleratorType,
                    MaxThreadsPerGroup = Device.MaxNumThreadsPerGroup,
                    MaxSharedMemory = Device.MaxSharedMemoryPerGroup,
                    WarpSize = Device.WarpSize
                });
        }
        catch (Exception ex)
        {
            _logger.LogError("GPU_CONTEXT_FAILED", "Failed to initialize GPU context", ex);
            throw;
        }
    }

    /// <summary>
    /// Selects the best available accelerator, preferring RTX GPUs
    /// </summary>
    private Accelerator SelectBestAccelerator()
    {
        var devices = _context.GetDevices<Device>().ToArray();
        _logger.LogInfo("GPU_DEVICES_FOUND", $"Found {devices.Length} GPU devices");

        // Score each device
        var scoredDevices = devices
            .Select(device => new
            {
                Device = device,
                Score = CalculateDeviceScore(device)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        foreach (var scored in scoredDevices)
        {
            _logger.LogDebug("GPU_DEVICE_SCORE", 
                $"Device: {scored.Device.Name}, Score: {scored.Score}",
                additionalData: new { 
                    Type = scored.Device.AcceleratorType,
                    Memory = scored.Device.MemorySize / (1024L * 1024L * 1024L)
                });
        }

        // Try to create accelerator with best device
        foreach (var scored in scoredDevices)
        {
            try
            {
                return scored.Device.AcceleratorType switch
                {
                    AcceleratorType.Cuda => _context.CreateCudaAccelerator(0),
                    AcceleratorType.OpenCL => _context.CreateCLAccelerator(0),
                    AcceleratorType.CPU => _context.CreateCPUAccelerator(0),
                    _ => throw new NotSupportedException($"Accelerator type {scored.Device.AcceleratorType} not supported")
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning("GPU_DEVICE_INIT_FAILED", 
                    $"Failed to initialize device {scored.Device.Name}: {ex.Message}");
            }
        }

        // Fallback to CPU
        _logger.LogWarning("GPU_FALLBACK_CPU", "No GPU available, falling back to CPU accelerator");
        return _context.CreateCPUAccelerator(0);
    }

    /// <summary>
    /// Calculates a score for device selection, preferring RTX GPUs
    /// </summary>
    private int CalculateDeviceScore(Device device)
    {
        int score = 0;
        
        // Device type scoring
        score += device.AcceleratorType switch
        {
            AcceleratorType.Cuda => 1000,  // Prefer NVIDIA
            AcceleratorType.OpenCL => 500,  // AMD/Intel second
            AcceleratorType.CPU => 100,     // CPU last resort
            _ => 0
        };

        // Memory scoring (1 point per GB)
        score += (int)(device.MemorySize / (1024L * 1024L * 1024L));

        // RTX detection (name-based)
        var name = device.Name.ToUpperInvariant();
        if (name.Contains("RTX"))
        {
            score += 500; // Significant bonus for RTX
            
            // Generation bonuses
            if (name.Contains("RTX 40")) score += 200;
            else if (name.Contains("RTX 30")) score += 150;
            else if (name.Contains("RTX 20")) score += 100;
        }

        // Compute capability for CUDA devices
        if (device.AcceleratorType == AcceleratorType.Cuda && device is CudaDevice cudaDevice)
        {
            // Higher compute capability = better
            // Removed as Architecture property access has changed
        }

        return score;
    }

    /// <summary>
    /// Creates a memory buffer on the GPU
    /// </summary>
    public MemoryBuffer1D<T, Stride1D.Dense> AllocateBuffer<T>(int length) 
        where T : unmanaged
    {
        return _accelerator.Allocate1D<T>(length);
    }

    /// <summary>
    /// Creates a 2D memory buffer on the GPU
    /// </summary>
    public MemoryBuffer2D<T, Stride2D.DenseX> AllocateBuffer2D<T>(int width, int height) 
        where T : unmanaged
    {
        return _accelerator.Allocate2DDenseX<T>(new Index2D(width, height));
    }

    /// <summary>
    /// Synchronizes the accelerator and waits for all operations to complete
    /// </summary>
    public void Synchronize()
    {
        _accelerator.Synchronize();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _logger.LogInfo("GPU_CONTEXT_DISPOSE", "Disposing GPU context");
            _accelerator?.Dispose();
            _context?.Dispose();
        }
    }
}

