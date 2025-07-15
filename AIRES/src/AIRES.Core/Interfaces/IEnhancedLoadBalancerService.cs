using System.Threading;
using System.Threading.Tasks;
using AIRES.Core.Models;

namespace AIRES.Core.Interfaces;

/// <summary>
/// Enhanced multi-GPU load balancer with weighted routing and health scoring.
/// </summary>
public interface IEnhancedLoadBalancerService
{
    /// <summary>
    /// Selects the best instance for a request using weighted scoring.
    /// </summary>
    Task<GpuInstance> SelectInstanceAsync(
        ModelRequirements requirements,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reports successful request completion.
    /// </summary>
    Task ReportSuccessAsync(
        string instanceId,
        long responseTimeMs,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reports request failure.
    /// </summary>
    Task ReportFailureAsync(
        string instanceId,
        string errorCode,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets current health status of all instances.
    /// </summary>
    Task<LoadBalancerHealth> GetHealthStatusAsync(CancellationToken cancellationToken = default);
}