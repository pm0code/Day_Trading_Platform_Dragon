using Microsoft.Extensions.Hosting;

namespace TradingPlatform.Foundation.Interfaces;

/// <summary>
/// Base interface for all trading services in the platform.
/// Provides standardized lifecycle management, health monitoring, and service identification.
/// All trading services must implement this interface to ensure consistent behavior.
/// </summary>
public interface ITradingService : IHostedService
{
    /// <summary>
    /// Unique identifier for the service used in logging, monitoring, and diagnostics.
    /// Should be consistent across restarts and deployments.
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Semantic version of the service implementation.
    /// Used for compatibility checks and deployment tracking.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Performs a comprehensive health check of the service.
    /// Should verify all critical dependencies and operational status.
    /// </summary>
    /// <returns>Detailed health check result with status and diagnostic information</returns>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current operational metrics for performance monitoring.
    /// Includes throughput, latency, error rates, and resource utilization.
    /// </summary>
    /// <returns>Real-time service metrics</returns>
    Task<ServiceMetrics> GetMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully handles configuration changes without service restart.
    /// Should validate new configuration and apply changes atomically.
    /// </summary>
    /// <param name="configurationChange">Details of the configuration change</param>
    Task HandleConfigurationChangeAsync(ConfigurationChangeEvent configurationChange, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when the service's health status changes.
    /// Critical for monitoring systems and automated response to failures.
    /// </summary>
    event EventHandler<HealthStatusChangedEventArgs> HealthStatusChanged;

    /// <summary>
    /// Event raised when service metrics cross configured thresholds.
    /// Enables proactive monitoring and performance optimization.
    /// </summary>
    event EventHandler<MetricsThresholdEventArgs> MetricsThresholdExceeded;
}

/// <summary>
/// Represents the result of a health check operation.
/// Provides comprehensive status information for monitoring and diagnostics.
/// </summary>
public record HealthCheckResult(
    bool IsHealthy,
    string Status,
    string? Description = null,
    TimeSpan Duration = default,
    Exception? Exception = null,
    Dictionary<string, object>? Data = null)
{
    /// <summary>
    /// Creates a healthy result with optional description and data.
    /// </summary>
    public static HealthCheckResult Healthy(string? description = null, Dictionary<string, object>? data = null)
        => new(true, "Healthy", description, default, null, data);

    /// <summary>
    /// Creates an unhealthy result with description and optional exception.
    /// </summary>
    public static HealthCheckResult Unhealthy(string description, Exception? exception = null, Dictionary<string, object>? data = null)
        => new(false, "Unhealthy", description, default, exception, data);

    /// <summary>
    /// Creates a degraded result indicating partial functionality.
    /// </summary>
    public static HealthCheckResult Degraded(string description, Dictionary<string, object>? data = null)
        => new(false, "Degraded", description, default, null, data);
}

/// <summary>
/// Contains operational metrics for a trading service.
/// All measurements use System.Decimal for financial precision compliance.
/// </summary>
public record ServiceMetrics(
    string ServiceName,
    DateTime Timestamp,
    TimeSpan Uptime,
    long RequestCount,
    decimal AverageLatencyMs,
    decimal MaxLatencyMs,
    decimal ThroughputPerSecond,
    long ErrorCount,
    decimal ErrorRate,
    long MemoryUsageBytes,
    double CpuUsagePercent)
{
    /// <summary>
    /// Additional custom metrics specific to the service.
    /// </summary>
    public Dictionary<string, object> CustomMetrics { get; init; } = new();
}

/// <summary>
/// Event arguments for health status change notifications.
/// </summary>
public class HealthStatusChangedEventArgs : EventArgs
{
    public HealthCheckResult PreviousStatus { get; }
    public HealthCheckResult CurrentStatus { get; }
    public DateTime ChangeTime { get; }

    public HealthStatusChangedEventArgs(HealthCheckResult previousStatus, HealthCheckResult currentStatus)
    {
        PreviousStatus = previousStatus ?? throw new ArgumentNullException(nameof(previousStatus));
        CurrentStatus = currentStatus ?? throw new ArgumentNullException(nameof(currentStatus));
        ChangeTime = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for metrics threshold violations.
/// </summary>
public class MetricsThresholdEventArgs : EventArgs
{
    public string MetricName { get; }
    public object CurrentValue { get; }
    public object ThresholdValue { get; }
    public ThresholdDirection Direction { get; }
    public DateTime EventTime { get; }

    public MetricsThresholdEventArgs(string metricName, object currentValue, object thresholdValue, ThresholdDirection direction)
    {
        MetricName = metricName ?? throw new ArgumentNullException(nameof(metricName));
        CurrentValue = currentValue ?? throw new ArgumentNullException(nameof(currentValue));
        ThresholdValue = thresholdValue ?? throw new ArgumentNullException(nameof(thresholdValue));
        Direction = direction;
        EventTime = DateTime.UtcNow;
    }
}

/// <summary>
/// Represents a configuration change event.
/// </summary>
public record ConfigurationChangeEvent(
    string ConfigurationKey,
    object? OldValue,
    object? NewValue,
    DateTime ChangeTime,
    string? ChangedBy = null);

/// <summary>
/// Direction of threshold violation.
/// </summary>
public enum ThresholdDirection
{
    /// <summary>
    /// Current value exceeded the upper threshold.
    /// </summary>
    Above,

    /// <summary>
    /// Current value fell below the lower threshold.
    /// </summary>
    Below
}