using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIRES.Core.Health;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;

namespace AIRES.CLI.Health;

/// <summary>
/// Executes health checks for all registered components.
/// </summary>
public class HealthCheckExecutor : AIRESServiceBase
{
    private static readonly string[] CriticalTypes = { "AI Service", "Infrastructure", "Core Service" };
    
    private readonly IEnumerable<IHealthCheck> _healthChecks;
    private readonly TimeSpan _timeout;

    public HealthCheckExecutor(
        IAIRESLogger logger,
        IEnumerable<IHealthCheck> healthChecks) 
        : base(logger, nameof(HealthCheckExecutor))
    {
        _healthChecks = healthChecks ?? throw new ArgumentNullException(nameof(healthChecks));
        _timeout = TimeSpan.FromSeconds(30); // 30 second timeout for all health checks
    }

    /// <summary>
    /// Runs all health checks in parallel with timeout.
    /// </summary>
    public async Task<HealthCheckReport> RunHealthChecksAsync(
        bool detailed = true, 
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        var results = new Dictionary<string, HealthCheckResult>();

        try
        {
            UpdateMetric("HealthCheckExecutor.Executions", 1);
            LogInfo($"Starting health checks for {_healthChecks.Count()} components");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            // Execute health checks concurrently
            var tasks = _healthChecks.Select(async healthCheck =>
            {
                try
                {
                    var checkStopwatch = Stopwatch.StartNew();
                    var result = await healthCheck.CheckHealthAsync(cts.Token);
                    checkStopwatch.Stop();
                    
                    lock (results)
                    {
                        results[healthCheck.Name] = result;
                    }
                    
                    UpdateMetric($"HealthCheck.{healthCheck.Name}.ResponseTime", checkStopwatch.ElapsedMilliseconds);
                    
                    if (result.Status == HealthStatus.Healthy)
                    {
                        LogDebug($"Health check '{healthCheck.Name}' passed in {checkStopwatch.ElapsedMilliseconds}ms");
                        UpdateMetric($"HealthCheck.{healthCheck.Name}.Healthy", 1);
                    }
                    else if (result.Status == HealthStatus.Degraded)
                    {
                        LogWarning($"Health check '{healthCheck.Name}' degraded: {string.Join("; ", result.FailureReasons)}");
                        UpdateMetric($"HealthCheck.{healthCheck.Name}.Degraded", 1);
                    }
                    else
                    {
                        LogError($"Health check '{healthCheck.Name}' failed: {result.ErrorMessage}");
                        UpdateMetric($"HealthCheck.{healthCheck.Name}.Unhealthy", 1);
                    }
                }
                catch (OperationCanceledException)
                {
                    var timeoutResult = HealthCheckResult.Unhealthy(
                        healthCheck.Name,
                        healthCheck.ComponentType,
                        (long)_timeout.TotalMilliseconds,
                        "Health check timed out",
                        null,
                        new[] { $"Timeout after {_timeout.TotalSeconds} seconds" }.ToImmutableList()
                    );
                    
                    lock (results)
                    {
                        results[healthCheck.Name] = timeoutResult;
                    }
                    
                    LogError($"Health check '{healthCheck.Name}' timed out");
                    UpdateMetric($"HealthCheck.{healthCheck.Name}.Timeouts", 1);
                }
                catch (Exception ex)
                {
                    LogError($"Error running health check '{healthCheck.Name}'", ex);
                    
                    var errorResult = HealthCheckResult.Unhealthy(
                        healthCheck.Name,
                        healthCheck.ComponentType,
                        0,
                        $"Exception during health check: {ex.Message}",
                        ex,
                        new[] { $"Exception: {ex.GetType().Name}" }.ToImmutableList()
                    );
                    
                    lock (results)
                    {
                        results[healthCheck.Name] = errorResult;
                    }
                    
                    UpdateMetric($"HealthCheck.{healthCheck.Name}.Exceptions", 1);
                }
            });

            await Task.WhenAll(tasks);

            stopwatch.Stop();
            
            var report = new HealthCheckReport
            {
                CheckedAt = DateTime.UtcNow,
                TotalDuration = stopwatch.ElapsedMilliseconds,
                Results = results,
                OverallStatus = DetermineOverallStatus(results.Values),
                Summary = GenerateSummary(results)
            };

            UpdateMetric("HealthCheckExecutor.TotalDuration", stopwatch.ElapsedMilliseconds);
            LogInfo($"Health checks completed in {stopwatch.ElapsedMilliseconds}ms. Overall status: {report.OverallStatus}");
            
            LogMethodExit();
            return report;
        }
        catch (Exception ex)
        {
            UpdateMetric("HealthCheckExecutor.Failures", 1);
            LogError("Failed to execute health checks", ex);
            LogMethodExit();
            
            return new HealthCheckReport
            {
                CheckedAt = DateTime.UtcNow,
                TotalDuration = stopwatch.ElapsedMilliseconds,
                Results = results,
                OverallStatus = HealthStatus.Unhealthy,
                Summary = $"Health check execution failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Runs a quick health check on critical components only.
    /// </summary>
    public async Task<HealthCheckReport> RunQuickHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            // Filter for critical components only
            var criticalChecks = _healthChecks.Where(hc => CriticalTypes.Contains(hc.ComponentType));
            
            var executor = new HealthCheckExecutor(Logger, criticalChecks);
            var result = await executor.RunHealthChecksAsync(false, cancellationToken);
            
            LogMethodExit();
            return result;
        }
        catch (Exception ex)
        {
            LogError("Failed to run quick health check", ex);
            LogMethodExit();
            throw;
        }
    }

    private HealthStatus DetermineOverallStatus(IEnumerable<HealthCheckResult> results)
    {
        if (results.Any(r => r.Status == HealthStatus.Unhealthy))
            return HealthStatus.Unhealthy;
        
        if (results.Any(r => r.Status == HealthStatus.Degraded))
            return HealthStatus.Degraded;
        
        return HealthStatus.Healthy;
    }

    private string GenerateSummary(Dictionary<string, HealthCheckResult> results)
    {
        var healthy = results.Count(r => r.Value.Status == HealthStatus.Healthy);
        var degraded = results.Count(r => r.Value.Status == HealthStatus.Degraded);
        var unhealthy = results.Count(r => r.Value.Status == HealthStatus.Unhealthy);
        
        return $"{healthy} healthy, {degraded} degraded, {unhealthy} unhealthy out of {results.Count} total components";
    }
}

/// <summary>
/// Comprehensive health check report.
/// </summary>
public class HealthCheckReport
{
    public DateTime CheckedAt { get; set; }
    public long TotalDuration { get; set; }
    public Dictionary<string, HealthCheckResult> Results { get; set; } = new();
    public HealthStatus OverallStatus { get; set; }
    public string Summary { get; set; } = string.Empty;
    
    public bool IsHealthy => OverallStatus == HealthStatus.Healthy;
    public bool HasFailures => Results.Any(r => r.Value.Status == HealthStatus.Unhealthy);
    public bool HasWarnings => Results.Any(r => r.Value.Status == HealthStatus.Degraded);
}