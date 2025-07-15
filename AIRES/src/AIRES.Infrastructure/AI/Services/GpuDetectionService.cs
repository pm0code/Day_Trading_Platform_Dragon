using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AIRES.Core.Interfaces;
using AIRES.Core.Models;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Infrastructure.AI.Models;

namespace AIRES.Infrastructure.AI.Services;

/// <summary>
/// Service for detecting and managing GPU resources for Ollama instances.
/// </summary>
public class GpuDetectionService : AIRESServiceBase, IGpuDetectionService
{
    private readonly IAIRESLogger _logger;
    private readonly Dictionary<int, GpuInfo> _gpuCache = new();
    private DateTime _lastDetection = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    public GpuDetectionService(IAIRESLogger logger) : base(logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Detects all available GPUs on the system.
    /// </summary>
    public async Task<IReadOnlyList<GpuInfo>> DetectAvailableGpusAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            // Check cache
            if (DateTime.UtcNow - _lastDetection < _cacheExpiration && _gpuCache.Any())
            {
                _logger.LogDebug("Returning cached GPU information");
                LogMethodExit();
                return _gpuCache.Values.ToList();
            }

            var gpus = new List<GpuInfo>();

            // Try nvidia-smi first
            var nvidiaGpus = await DetectNvidiaGpusAsync(cancellationToken);
            gpus.AddRange(nvidiaGpus);

            // Try ROCm for AMD GPUs
            var amdGpus = await DetectAmdGpusAsync(cancellationToken);
            gpus.AddRange(amdGpus);

            // Update cache
            _gpuCache.Clear();
            foreach (var gpu in gpus)
            {
                _gpuCache[gpu.Id] = gpu;
            }
            _lastDetection = DateTime.UtcNow;

            _logger.LogInfo($"Detected {gpus.Count} GPUs");
            
            LogMethodExit();
            return gpus;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to detect GPUs", ex);
            LogMethodExit();
            throw new InvalidOperationException($"GPU detection failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets detailed capabilities of a specific GPU.
    /// </summary>
    public async Task<GpuCapabilities> GetGpuCapabilitiesAsync(int gpuId, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            if (!_gpuCache.TryGetValue(gpuId, out var gpuInfo))
            {
                await DetectAvailableGpusAsync(cancellationToken);
                if (!_gpuCache.TryGetValue(gpuId, out gpuInfo))
                {
                    LogMethodExit();
                    throw new ArgumentException($"GPU {gpuId} not found");
                }
            }

            var capabilities = new GpuCapabilities
            {
                GpuId = gpuInfo.Id,
                Name = gpuInfo.Name,
                TotalMemoryMB = gpuInfo.MemoryTotalMB,
                AvailableMemoryMB = gpuInfo.MemoryAvailableMB,
                ComputeCapability = gpuInfo.ComputeCapability,
                SupportsFloat16 = gpuInfo.SupportsFloat16,
                SupportsBFloat16 = gpuInfo.SupportsBFloat16,
                RecommendedInstanceCount = CalculateRecommendedInstances(gpuInfo),
                RecommendedModels = GetRecommendedModels(gpuInfo)
            };

            LogMethodExit();
            return capabilities;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to get capabilities for GPU {gpuId}", ex);
            LogMethodExit();
            throw;
        }
    }

    /// <summary>
    /// Validates the health of a specific GPU.
    /// </summary>
    public async Task<GpuHealthStatus> ValidateGpuHealthAsync(int gpuId, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = $"--query-gpu=index,temperature.gpu,utilization.gpu,utilization.memory,memory.used,memory.total,power.draw --format=csv,noheader,nounits -i {gpuId}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                LogMethodExit();
                throw new InvalidOperationException("nvidia-smi failed");
            }

            var values = output.Trim().Split(',').Select(v => v.Trim()).ToArray();
            if (values.Length >= 7)
            {
                var health = new GpuHealthStatus
                {
                    GpuId = int.Parse(values[0]),
                    Temperature = int.Parse(values[1]),
                    GpuUtilization = int.Parse(values[2]),
                    MemoryUtilization = int.Parse(values[3]),
                    MemoryUsedMB = int.Parse(values[4]),
                    MemoryTotalMB = int.Parse(values[5]),
                    PowerDraw = float.Parse(values[6]),
                    IsHealthy = int.Parse(values[1]) < 85 && int.Parse(values[3]) < 95,
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogDebug($"GPU {gpuId} health: Temp={health.Temperature}°C, GPU={health.GpuUtilization}%, Mem={health.MemoryUtilization}%");

                LogMethodExit();
                return health;
            }

            LogMethodExit();
            throw new InvalidOperationException("Failed to parse nvidia-smi output");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to validate GPU {gpuId} health", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task<List<GpuInfo>> DetectNvidiaGpusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=index,name,memory.total,memory.free,compute_cap --format=csv,noheader,nounits",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                return new List<GpuInfo>();
            }

            var gpus = new List<GpuInfo>();
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var values = line.Split(',').Select(v => v.Trim()).ToArray();
                if (values.Length >= 5)
                {
                    var computeCap = values[4].Split('.');
                    var gpu = new GpuInfo
                    {
                        Id = int.Parse(values[0]),
                        Name = values[1],
                        MemoryTotalMB = int.Parse(values[2]),
                        MemoryAvailableMB = int.Parse(values[3]),
                        ComputeCapability = int.Parse(computeCap[0]) * 10 + int.Parse(computeCap[1]),
                        Vendor = "NVIDIA",
                        SupportsFloat16 = true,
                        SupportsBFloat16 = int.Parse(computeCap[0]) >= 8
                    };
                    gpus.Add(gpu);
                }
            }

            return gpus;
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"NVIDIA GPU detection failed: {ex.Message}");
            return new List<GpuInfo>();
        }
    }

    private async Task<List<GpuInfo>> DetectAmdGpusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "rocm-smi",
                    Arguments = "--showallinfo",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                return new List<GpuInfo>();
            }

            // Parse AMD GPU info (simplified for now)
            var gpus = new List<GpuInfo>();
            _logger.LogDebug("AMD GPU detection not fully implemented");

            return gpus;
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"AMD GPU detection failed: {ex.Message}");
            return new List<GpuInfo>();
        }
    }

    private int CalculateRecommendedInstances(GpuInfo gpu)
    {
        // Based on memory and best practices
        return gpu.MemoryTotalMB switch
        {
            < 8000 => 1,
            < 16000 => 2,
            < 24000 => 3,
            _ => 4
        };
    }

    private List<string> GetRecommendedModels(GpuInfo gpu)
    {
        var models = new List<string>();

        if (gpu.MemoryTotalMB >= 4000)
        {
            models.Add("mistral:7b-instruct-q4_K_M");
            models.Add("deepseek-coder:6.7b");
        }

        if (gpu.MemoryTotalMB >= 8000)
        {
            models.Add("codegemma:7b");
            models.Add("codellama:7b");
        }

        if (gpu.MemoryTotalMB >= 12000)
        {
            models.Add("gemma2:9b");
            models.Add("llama2:13b");
        }

        if (gpu.MemoryTotalMB >= 24000)
        {
            models.Add("mixtral:8x7b");
            models.Add("yi:34b");
        }

        return models;
    }
}