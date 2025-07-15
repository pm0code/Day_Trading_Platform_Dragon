using System.Collections.Immutable;

namespace AIRES.Core.Health;

/// <summary>
/// Comprehensive health check result with rich diagnostic information.
/// </summary>
public sealed class HealthCheckResult
{
    public HealthStatus Status { get; }
    public string ComponentName { get; }
    public string ComponentType { get; }
    public DateTime CheckedAt { get; }
    public long ResponseTimeMs { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }
    public IImmutableDictionary<string, object> Diagnostics { get; }
    public IImmutableList<string> FailureReasons { get; }
    
    private HealthCheckResult(
        HealthStatus status,
        string componentName,
        string componentType,
        DateTime checkedAt,
        long responseTimeMs,
        string? errorMessage,
        Exception? exception,
        IImmutableDictionary<string, object> diagnostics,
        IImmutableList<string> failureReasons)
    {
        Status = status;
        ComponentName = componentName ?? throw new ArgumentNullException(nameof(componentName));
        ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
        CheckedAt = checkedAt;
        ResponseTimeMs = responseTimeMs;
        ErrorMessage = errorMessage;
        Exception = exception;
        Diagnostics = diagnostics ?? ImmutableDictionary<string, object>.Empty;
        FailureReasons = failureReasons ?? ImmutableList<string>.Empty;
    }
    
    public static HealthCheckResult Healthy(
        string componentName,
        string componentType,
        long responseTimeMs,
        ImmutableDictionary<string, object>? diagnostics = null)
    {
        return new HealthCheckResult(
            HealthStatus.Healthy,
            componentName,
            componentType,
            DateTime.UtcNow,
            responseTimeMs,
            null,
            null,
            diagnostics ?? ImmutableDictionary<string, object>.Empty,
            ImmutableList<string>.Empty);
    }
    
    public static HealthCheckResult Degraded(
        string componentName,
        string componentType,
        long responseTimeMs,
        ImmutableList<string> reasons,
        ImmutableDictionary<string, object>? diagnostics = null)
    {
        return new HealthCheckResult(
            HealthStatus.Degraded,
            componentName,
            componentType,
            DateTime.UtcNow,
            responseTimeMs,
            $"Component degraded: {string.Join("; ", reasons)}",
            null,
            diagnostics ?? ImmutableDictionary<string, object>.Empty,
            reasons);
    }
    
    public static HealthCheckResult Unhealthy(
        string componentName,
        string componentType,
        long responseTimeMs,
        string errorMessage,
        Exception? exception = null,
        ImmutableList<string>? reasons = null,
        ImmutableDictionary<string, object>? diagnostics = null)
    {
        return new HealthCheckResult(
            HealthStatus.Unhealthy,
            componentName,
            componentType,
            DateTime.UtcNow,
            responseTimeMs,
            errorMessage,
            exception,
            diagnostics ?? ImmutableDictionary<string, object>.Empty,
            reasons ?? ImmutableList<string>.Empty);
    }

    public string GetDetailedReport()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine($"=== Health Check Report: {ComponentName} ({ComponentType}) ===");
        report.AppendLine($"Status: {Status}");
        report.AppendLine($"Checked At: {CheckedAt:yyyy-MM-dd HH:mm:ss.fff} UTC");
        report.AppendLine($"Response Time: {ResponseTimeMs}ms");
        
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            report.AppendLine($"Error: {ErrorMessage}");
        }
        
        if (FailureReasons.Any())
        {
            report.AppendLine("Failure Reasons:");
            foreach (var reason in FailureReasons)
            {
                report.AppendLine($"  - {reason}");
            }
        }
        
        if (Diagnostics.Any())
        {
            report.AppendLine("Diagnostics:");
            foreach (var kvp in Diagnostics)
            {
                report.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
        }
        
        if (Exception != null)
        {
            report.AppendLine($"Exception Type: {Exception.GetType().FullName}");
            report.AppendLine($"Exception Message: {Exception.Message}");
            if (Exception.InnerException != null)
            {
                report.AppendLine($"Inner Exception: {Exception.InnerException.Message}");
            }
            report.AppendLine("Stack Trace:");
            report.AppendLine(Exception.StackTrace);
        }
        
        return report.ToString();
    }
}

public enum HealthStatus
{
    /// <summary>
    /// Component is fully operational
    /// </summary>
    Healthy,
    
    /// <summary>
    /// Component is operational but with issues
    /// </summary>
    Degraded,
    
    /// <summary>
    /// Component is not operational
    /// </summary>
    Unhealthy
}