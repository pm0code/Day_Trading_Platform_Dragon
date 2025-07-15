using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AIRES.Core.Models;

namespace AIRES.Core.Interfaces;

/// <summary>
/// Service for detecting and managing GPU resources for Ollama instances.
/// </summary>
public interface IGpuDetectionService
{
    /// <summary>
    /// Detects all available GPUs on the system.
    /// </summary>
    Task<IReadOnlyList<GpuInfo>> DetectAvailableGpusAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets detailed capabilities of a specific GPU.
    /// </summary>
    Task<GpuCapabilities> GetGpuCapabilitiesAsync(int gpuId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates the health of a specific GPU.
    /// </summary>
    Task<GpuHealthStatus> ValidateGpuHealthAsync(int gpuId, CancellationToken cancellationToken = default);
}