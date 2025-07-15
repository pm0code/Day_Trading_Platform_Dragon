using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AIRES.Core.Interfaces;
using AIRES.Core.Models;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Results;
using AIRES.Foundation.Logging;

namespace AIRES.Infrastructure.AI.Services;

/// <summary>
/// Wrapper for GPU detection service that provides AIRESResult pattern.
/// </summary>
public class GpuDetectionServiceWrapper : AIRESServiceBase
{
    private readonly IGpuDetectionService _innerService;
    private readonly IAIRESLogger _logger;

    public GpuDetectionServiceWrapper(
        IGpuDetectionService innerService,
        IAIRESLogger logger) : base(logger)
    {
        _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Detects all available GPUs on the system.
    /// </summary>
    public async Task<AIRESResult<IReadOnlyList<GpuInfo>>> DetectAvailableGpusAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            var result = await _innerService.DetectAvailableGpusAsync(cancellationToken);
            LogMethodExit();
            return AIRESResult<IReadOnlyList<GpuInfo>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to detect GPUs", ex);
            LogMethodExit();
            return AIRESResult<IReadOnlyList<GpuInfo>>.Failure("GPU_DETECTION_FAILED", ex.Message);
        }
    }

    /// <summary>
    /// Gets detailed capabilities of a specific GPU.
    /// </summary>
    public async Task<AIRESResult<GpuCapabilities>> GetGpuCapabilitiesAsync(int gpuId, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            var result = await _innerService.GetGpuCapabilitiesAsync(gpuId, cancellationToken);
            LogMethodExit();
            return AIRESResult<GpuCapabilities>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to get capabilities for GPU {gpuId}", ex);
            LogMethodExit();
            return AIRESResult<GpuCapabilities>.Failure("CAPABILITY_CHECK_FAILED", ex.Message);
        }
    }

    /// <summary>
    /// Validates the health of a specific GPU.
    /// </summary>
    public async Task<AIRESResult<GpuHealthStatus>> ValidateGpuHealthAsync(int gpuId, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            var result = await _innerService.ValidateGpuHealthAsync(gpuId, cancellationToken);
            LogMethodExit();
            return AIRESResult<GpuHealthStatus>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to validate GPU {gpuId} health", ex);
            LogMethodExit();
            return AIRESResult<GpuHealthStatus>.Failure("HEALTH_CHECK_ERROR", ex.Message);
        }
    }
}