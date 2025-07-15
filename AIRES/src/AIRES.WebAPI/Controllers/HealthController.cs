namespace AIRES.WebAPI.Controllers;

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AIRES.CLI.Health;
using AIRES.Core.Health;

/// <summary>
/// Controller for system health monitoring endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckExecutor _healthCheckExecutor;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        HealthCheckExecutor healthCheckExecutor,
        ILogger<HealthController> logger)
    {
        _healthCheckExecutor = healthCheckExecutor ?? throw new ArgumentNullException(nameof(healthCheckExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets comprehensive health status of all AIRES components.
    /// </summary>
    /// <returns>Detailed health check results for all components.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealth()
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Health check endpoint called");

        try
        {
            var report = await _healthCheckExecutor.RunHealthChecksAsync();
            stopwatch.Stop();

            var overallStatus = HealthStatus.Healthy;
            var unhealthyCount = 0;
            var degradedCount = 0;

            foreach (var result in report.Results.Values)
            {
                if (result.Status == HealthStatus.Unhealthy)
                {
                    unhealthyCount++;
                    overallStatus = HealthStatus.Unhealthy;
                }
                else if (result.Status == HealthStatus.Degraded && overallStatus != HealthStatus.Unhealthy)
                {
                    degradedCount++;
                    overallStatus = HealthStatus.Degraded;
                }
            }

            var response = new
            {
                status = overallStatus.ToString(),
                totalDuration = stopwatch.ElapsedMilliseconds,
                timestamp = DateTime.UtcNow,
                summary = new
                {
                    total = report.Results.Count,
                    healthy = report.Results.Values.Count(r => r.Status == HealthStatus.Healthy),
                    degraded = degradedCount,
                    unhealthy = unhealthyCount
                },
                components = report.Results.Values.Select(r => new
                {
                    name = r.ComponentName,
                    status = r.Status.ToString(),
                    category = r.ComponentType,
                    duration = r.ResponseTimeMs,
                    checkedAt = r.CheckedAt,
                    failureReasons = r.FailureReasons.ToList(),
                    exception = r.Exception?.Message,
                    diagnostics = r.Diagnostics
                }).ToList()
            };

            var statusCode = overallStatus switch
            {
                HealthStatus.Healthy => StatusCodes.Status200OK,
                HealthStatus.Degraded => StatusCodes.Status503ServiceUnavailable, // Degraded needs attention!
                _ => StatusCodes.Status503ServiceUnavailable
            };

            _logger.LogInformation(
                "Health check completed in {Duration}ms - Status: {Status} (Healthy: {Healthy}, Degraded: {Degraded}, Unhealthy: {Unhealthy})",
                stopwatch.ElapsedMilliseconds,
                overallStatus, 
                report.Results.Values.Count(r => r.Status == HealthStatus.Healthy),
                degradedCount,
                unhealthyCount);

            return StatusCode(statusCode, response);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error executing health checks");

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "Unhealthy",
                error = "Failed to execute health checks",
                message = ex.Message,
                timestamp = DateTime.UtcNow,
                duration = stopwatch.ElapsedMilliseconds
            });
        }
    }

    /// <summary>
    /// Gets quick health status (liveness probe).
    /// </summary>
    /// <returns>Simple alive status.</returns>
    [HttpGet("live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetLiveness()
    {
        return Ok(new 
        { 
            status = "Alive",
            timestamp = DateTime.UtcNow,
            service = "AIRES"
        });
    }

    /// <summary>
    /// Gets readiness status (readiness probe).
    /// </summary>
    /// <returns>Service readiness status.</returns>
    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetReadiness()
    {
        _logger.LogInformation("Readiness check endpoint called");

        try
        {
            // Quick check of critical components only
            var report = await _healthCheckExecutor.RunQuickHealthCheckAsync();
            
            // Check critical components (Orchestrator, Configuration, Watchdog)
            var criticalComponents = new[] { "AIResearchOrchestratorService", "AIRESConfigurationService", "FileWatchdogService" };
            var criticalResults = report.Results.Values.Where(r => criticalComponents.Contains(r.ComponentName)).ToList();

            var allCriticalHealthy = criticalResults.All(r => r.Status != HealthStatus.Unhealthy);

            if (allCriticalHealthy)
            {
                return Ok(new 
                { 
                    status = "Ready",
                    timestamp = DateTime.UtcNow,
                    criticalComponents = criticalResults.Select(r => new
                    {
                        name = r.ComponentName,
                        status = r.Status.ToString()
                    })
                });
            }

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "NotReady",
                timestamp = DateTime.UtcNow,
                failedComponents = criticalResults
                    .Where(r => r.Status == HealthStatus.Unhealthy)
                    .Select(r => new
                    {
                        name = r.ComponentName,
                        reasons = r.FailureReasons
                    })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing readiness check");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "NotReady",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Gets health status for a specific component.
    /// </summary>
    /// <param name="componentName">Name of the component to check.</param>
    /// <returns>Health status of the specified component.</returns>
    [HttpGet("{componentName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetComponentHealth(string componentName)
    {
        _logger.LogInformation("Component health check requested for: {ComponentName}", componentName);

        try
        {
            var report = await _healthCheckExecutor.RunHealthChecksAsync();
            var componentResult = report.Results.Values.FirstOrDefault(r => 
                r.ComponentName.Equals(componentName, StringComparison.OrdinalIgnoreCase));

            if (componentResult == null)
            {
                return NotFound(new
                {
                    error = "Component not found",
                    component = componentName,
                    availableComponents = report.Results.Values.Select(r => r.ComponentName).Distinct().OrderBy(n => n)
                });
            }

            var statusCode = componentResult.Status switch
            {
                HealthStatus.Healthy => StatusCodes.Status200OK,
                HealthStatus.Degraded => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status503ServiceUnavailable
            };

            return StatusCode(statusCode, new
            {
                name = componentResult.ComponentName,
                status = componentResult.Status.ToString(),
                category = componentResult.ComponentType,
                duration = componentResult.ResponseTimeMs,
                checkedAt = componentResult.CheckedAt,
                failureReasons = componentResult.FailureReasons,
                exception = componentResult.Exception?.Message,
                diagnostics = componentResult.Diagnostics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking component health for: {ComponentName}", componentName);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "Error",
                component = componentName,
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }
}