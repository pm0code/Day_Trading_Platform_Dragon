using System.Threading;
using System.Threading.Tasks;
using AIRES.Core.Health;

namespace AIRES.CLI.Health;

/// <summary>
/// Interface for CLI health check implementations.
/// </summary>
public interface IHealthCheck
{
    /// <summary>
    /// Gets the name of the health check component.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gets the component type for categorization.
    /// </summary>
    string ComponentType { get; }
    
    /// <summary>
    /// Performs the health check asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health check result with detailed diagnostics.</returns>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}