namespace TradingPlatform.Gateway.Services;

/// <summary>
/// Manages local microservice processes on the trading workstation
/// Handles process lifecycle, CPU affinity, and performance optimization
/// </summary>
public interface IProcessManager
{
    /// <summary>
    /// Get status of all trading platform processes
    /// </summary>
    Task<ProcessInfo[]> GetProcessStatusAsync();

    /// <summary>
    /// Start a specific trading service with optimized configuration
    /// </summary>
    Task StartServiceAsync(string serviceName);

    /// <summary>
    /// Stop a specific trading service gracefully
    /// </summary>
    Task StopServiceAsync(string serviceName);

    /// <summary>
    /// Restart a service with zero-downtime where possible
    /// </summary>
    Task RestartServiceAsync(string serviceName);

    /// <summary>
    /// Configure CPU affinity for optimal performance
    /// </summary>
    Task SetCpuAffinityAsync(string serviceName, int[] cpuCores);

    /// <summary>
    /// Set process priority for trading optimization
    /// </summary>
    Task SetProcessPriorityAsync(string serviceName, ProcessPriorityLevel priority);

    /// <summary>
    /// Get real-time performance metrics for all services
    /// </summary>
    Task<ServicePerformanceMetrics[]> GetPerformanceMetricsAsync();

    /// <summary>
    /// Configure Windows 11 real-time optimization
    /// </summary>
    Task OptimizeForTradingAsync();
}

public record ProcessInfo(
    string ServiceName,
    int ProcessId,
    ProcessStatus Status,
    TimeSpan Uptime,
    double CpuUsagePercent,
    long MemoryUsageMB,
    int ThreadCount,
    int[] CpuAffinity,
    ProcessPriorityLevel Priority);

public record ServicePerformanceMetrics(
    string ServiceName,
    TimeSpan AverageResponseTime,
    long RequestsPerSecond,
    double CpuUsagePercent,
    long MemoryUsageMB,
    int ActiveConnections,
    long TotalRequests,
    long FailedRequests,
    DateTimeOffset LastMeasurement);

public enum ProcessStatus
{
    Running,
    Stopped,
    Starting,
    Stopping,
    Error,
    Unknown
}

public enum ProcessPriorityLevel
{
    RealTime,    // For critical trading processes (FixEngine, Risk)
    High,        // For important services (MarketData, Strategy)
    Normal,      // For background services
    Low          // For logging, monitoring
}