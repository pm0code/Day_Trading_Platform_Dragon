namespace TradingPlatform.Foundation.Interfaces;

/// <summary>
/// Standardized health check interface for all trading platform components.
/// Provides consistent health monitoring across services, providers, and infrastructure components.
/// </summary>
public interface IHealthCheck
{
    /// <summary>
    /// Unique name identifying this health check.
    /// Should be consistent across deployments for proper monitoring.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the health check and returns the current status.
    /// Implementation should be fast (less than 5 seconds) and safe to call frequently.
    /// </summary>
    /// <param name="context">Additional context for the health check</param>
    /// <param name="cancellationToken">Cancellation token for timeout control</param>
    /// <returns>Health check result with status and diagnostic information</returns>
    Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Context information provided to health checks.
/// Contains registration information and shared state.
/// </summary>
public class HealthCheckContext
{
    /// <summary>
    /// Registration information for this health check.
    /// </summary>
    public HealthCheckRegistration Registration { get; }

    /// <summary>
    /// Additional properties that can be used by health checks.
    /// </summary>
    public IDictionary<string, object> Properties { get; }

    public HealthCheckContext(HealthCheckRegistration registration)
    {
        Registration = registration ?? throw new ArgumentNullException(nameof(registration));
        Properties = new Dictionary<string, object>();
    }
}

/// <summary>
/// Registration information for a health check.
/// </summary>
public class HealthCheckRegistration
{
    /// <summary>
    /// Unique name for the health check.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Factory function to create the health check instance.
    /// </summary>
    public Func<IServiceProvider, IHealthCheck> Factory { get; }

    /// <summary>
    /// Failure status to report when the health check throws an exception.
    /// </summary>
    public HealthStatus FailureStatus { get; }

    /// <summary>
    /// Optional tags for grouping and filtering health checks.
    /// </summary>
    public ISet<string> Tags { get; }

    /// <summary>
    /// Timeout for the health check execution.
    /// </summary>
    public TimeSpan Timeout { get; }

    public HealthCheckRegistration(
        string name,
        Func<IServiceProvider, IHealthCheck> factory,
        HealthStatus failureStatus = HealthStatus.Unhealthy,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        FailureStatus = failureStatus;
        Tags = new HashSet<string>(tags ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        Timeout = timeout ?? TimeSpan.FromSeconds(30);
    }
}

/// <summary>
/// Overall health status for the system or component.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// The component status is unknown or not yet determined.
    /// </summary>
    Unknown = -1,

    /// <summary>
    /// The component is healthy and operating normally.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// The component is operational but experiencing some issues.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// The component is not healthy and may not be operational.
    /// </summary>
    Unhealthy = 2
}

/// <summary>
/// Specialized health check for trading-specific components.
/// Includes trading session awareness and performance requirements.
/// </summary>
public interface ITradingHealthCheck : IHealthCheck
{
    /// <summary>
    /// Indicates whether this health check is critical for trading operations.
    /// Critical checks failing should stop trading activities.
    /// </summary>
    bool IsCriticalForTrading { get; }

    /// <summary>
    /// Indicates whether this health check should be performed during market hours only.
    /// Some checks may be skipped when markets are closed to reduce noise.
    /// </summary>
    bool MarketHoursOnly { get; }

    /// <summary>
    /// Expected maximum latency for this health check in milliseconds.
    /// Used for performance monitoring and alerting.
    /// </summary>
    int MaxExpectedLatencyMs { get; }
}

/// <summary>
/// Health check for market data providers with provider-specific concerns.
/// </summary>
public interface IMarketDataHealthCheck : ITradingHealthCheck
{
    /// <summary>
    /// The market data provider this health check monitors.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Checks if the provider has sufficient API quota remaining.
    /// </summary>
    Task<bool> HasSufficientQuotaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the current latency to the market data provider.
    /// </summary>
    Task<TimeSpan> MeasureLatencyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Health check for trading infrastructure components (messaging, database, etc.).
/// </summary>
public interface IInfrastructureHealthCheck : ITradingHealthCheck
{
    /// <summary>
    /// The infrastructure component this health check monitors.
    /// </summary>
    string ComponentName { get; }

    /// <summary>
    /// Checks if the component can handle the expected trading load.
    /// </summary>
    Task<bool> CanHandleExpectedLoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current resource utilization metrics for the component.
    /// </summary>
    Task<ResourceUtilizationMetrics> GetResourceUtilizationAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Resource utilization metrics for infrastructure components.
/// </summary>
public record ResourceUtilizationMetrics(
    double CpuUsagePercent,
    double MemoryUsagePercent,
    double DiskUsagePercent,
    double NetworkUsagePercent,
    int ActiveConnections,
    Dictionary<string, object>? CustomMetrics = null);