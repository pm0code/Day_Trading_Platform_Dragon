namespace TradingPlatform.Gateway.Services;

/// <summary>
/// Comprehensive health monitoring for trading platform services
/// Monitors system health, performance, and trading-specific metrics
/// </summary>
public interface IHealthMonitor
{
    /// <summary>
    /// Get overall system health status
    /// </summary>
    Task<HealthStatus> GetHealthStatusAsync();

    /// <summary>
    /// Get detailed health information for all services
    /// </summary>
    Task<ServiceHealthInfo[]> GetDetailedHealthAsync();

    /// <summary>
    /// Check if critical trading systems are operational
    /// </summary>
    Task<bool> AreCriticalSystemsHealthyAsync();

    /// <summary>
    /// Get trading-specific health metrics
    /// </summary>
    Task<TradingHealthMetrics> GetTradingHealthAsync();

    /// <summary>
    /// Register a custom health check
    /// </summary>
    void RegisterHealthCheck(string name, Func<Task<HealthCheckResult>> healthCheck);

    /// <summary>
    /// Start continuous health monitoring
    /// </summary>
    Task StartMonitoringAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get system alerts and warnings
    /// </summary>
    Task<SystemAlert[]> GetActiveAlertsAsync();
}

public record HealthStatus(
    bool IsHealthy,
    string Status,
    TimeSpan ResponseTime,
    Dictionary<string, bool> ServiceHealth,
    SystemAlert[] Alerts,
    DateTimeOffset Timestamp);

public record ServiceHealthInfo(
    string ServiceName,
    bool IsHealthy,
    string Status,
    TimeSpan ResponseTime,
    double CpuUsagePercent,
    long MemoryUsageMB,
    int ActiveConnections,
    DateTimeOffset LastCheck,
    string[] Issues);

public record TradingHealthMetrics(
    bool MarketDataConnected,
    TimeSpan AverageOrderLatency,
    TimeSpan AverageMarketDataLatency,
    bool RiskSystemOperational,
    int ActiveStrategies,
    decimal DailyPnL,
    bool PDTCompliant,
    int FailedOrders,
    DateTimeOffset MarketSessionStart,
    bool IsMarketOpen);

public record HealthCheckResult(
    bool IsHealthy,
    string Status,
    string Description,
    TimeSpan ResponseTime,
    Dictionary<string, object>? Data = null);

public record SystemAlert(
    string Id,
    AlertSeverity Severity,
    string Title,
    string Description,
    DateTimeOffset CreatedAt,
    bool IsAcknowledged,
    string? AcknowledgedBy,
    DateTimeOffset? AcknowledgedAt);

public enum AlertSeverity
{
    Info,
    Warning,
    Error,
    Critical
}