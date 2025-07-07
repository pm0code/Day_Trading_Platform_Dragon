using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace TradingPlatform.Core.Performance;

/// <summary>
/// Comprehensive performance monitoring system for ultra-low latency trading
/// Tracks latency, throughput, resource usage, and system health metrics
/// Target: Sub-100 microsecond execution monitoring with minimal overhead
/// </summary>
public sealed class PerformanceMonitor : IDisposable
{
    private readonly ConcurrentDictionary<string, LatencyTracker> _latencyTrackers = new();
    private readonly ConcurrentDictionary<string, ThroughputTracker> _throughputTrackers = new();
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;
    private readonly Timer _metricsCollectionTimer;
    private readonly Timer _alertCheckTimer;
    private readonly object _lock = new();

    private long _totalOperations;
    private long _totalLatencyMicroseconds;
    private double _maxLatencyMicroseconds;
    private DateTime _monitoringStartTime;
    private bool _disposed;

    // Performance thresholds for alerts
    private const double CRITICAL_LATENCY_MICROSECONDS = 100.0; // 100μs
    private const double WARNING_LATENCY_MICROSECONDS = 50.0;   // 50μs
    private const double MAX_CPU_USAGE_PERCENT = 80.0;          // 80%
    private const long MAX_MEMORY_USAGE_MB = 2048;              // 2GB

    // Events for performance alerts
    public event EventHandler<PerformanceAlert>? PerformanceAlertTriggered;
    public event EventHandler<PerformanceMetrics>? MetricsUpdated;

    private static readonly Lazy<PerformanceMonitor> _instance = new(() => new PerformanceMonitor());
    public static PerformanceMonitor Instance => _instance.Value;

    private PerformanceMonitor()
    {
        _monitoringStartTime = DateTime.UtcNow;

        try
        {
            // Initialize performance counters (Windows-specific)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
        }
        catch
        {
            // Performance counters may not be available in all environments
        }

        // Collect metrics every second
        _metricsCollectionTimer = new Timer(CollectMetrics, null,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        // Check for alerts every 100ms for near real-time monitoring
        _alertCheckTimer = new Timer(CheckAlerts, null,
            TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));

        InitializeDefaultTrackers();
    }

    /// <summary>
    /// Measures execution latency for a specific operation
    /// </summary>
    public LatencyMeasurement MeasureLatency(string operationName)
    {
        return new LatencyMeasurement(this, operationName);
    }

    /// <summary>
    /// Records a completed operation with its latency
    /// </summary>
    public void RecordLatency(string operationName, double latencyMicroseconds)
    {
        // Update global counters
        Interlocked.Increment(ref _totalOperations);
        Interlocked.Add(ref _totalLatencyMicroseconds, (long)latencyMicroseconds);

        // Update max latency
        lock (_lock)
        {
            if (latencyMicroseconds > _maxLatencyMicroseconds)
            {
                _maxLatencyMicroseconds = latencyMicroseconds;
            }
        }

        // Update operation-specific tracker
        var tracker = _latencyTrackers.GetOrAdd(operationName, name => new LatencyTracker(name));
        tracker.RecordLatency((long)latencyMicroseconds);

        // Check for latency violations
        if (latencyMicroseconds > CRITICAL_LATENCY_MICROSECONDS)
        {
            TriggerAlert(AlertSeverity.Critical,
                $"Critical latency violation: {operationName} took {latencyMicroseconds:F2}μs");
        }
        else if (latencyMicroseconds > WARNING_LATENCY_MICROSECONDS)
        {
            TriggerAlert(AlertSeverity.Warning,
                $"Latency warning: {operationName} took {latencyMicroseconds:F2}μs");
        }
    }

    /// <summary>
    /// Records throughput for a specific operation
    /// </summary>
    public void RecordThroughput(string operationName, int operationCount = 1)
    {
        var tracker = _throughputTrackers.GetOrAdd(operationName, _ => new ThroughputTracker());
        tracker.RecordOperations(operationCount);
    }

    /// <summary>
    /// Gets comprehensive performance metrics
    /// </summary>
    public PerformanceMetrics GetMetrics()
    {
        var uptime = DateTime.UtcNow - _monitoringStartTime;
        var totalOps = Interlocked.Read(ref _totalOperations);
        var totalLatency = Interlocked.Read(ref _totalLatencyMicroseconds);

        return new PerformanceMetrics
        {
            Uptime = uptime,
            TotalOperations = totalOps,
            AverageLatencyMicroseconds = totalOps > 0 ? (double)totalLatency / totalOps : 0,
            MaxLatencyMicroseconds = _maxLatencyMicroseconds,
            OperationsPerSecond = uptime.TotalSeconds > 0 ? totalOps / uptime.TotalSeconds : 0,
            LatencyTrackers = _latencyTrackers.ToDictionary(kvp => kvp.Key, kvp => 
            {
                var metrics = kvp.Value.GetMetrics();
                return new LatencyMetrics
                {
                    OperationCount = (long)metrics["Count"],
                    AverageLatencyMicroseconds = (double)metrics["Mean"],
                    MinLatencyMicroseconds = (double)metrics["Min"],
                    MaxLatencyMicroseconds = (double)metrics["Max"]
                };
            }),
            ThroughputTrackers = _throughputTrackers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetMetrics()),
            SystemMetrics = GetSystemMetrics(),
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets latency percentiles for a specific operation
    /// </summary>
    public LatencyPercentiles GetLatencyPercentiles(string operationName)
    {
        if (_latencyTrackers.TryGetValue(operationName, out var tracker))
        {
            var percentiles = tracker.GetPercentiles(50, 95, 99, 999);
            return new LatencyPercentiles
            {
                P50 = percentiles.ContainsKey(50) ? percentiles[50] : 0,
                P95 = percentiles.ContainsKey(95) ? percentiles[95] : 0,
                P99 = percentiles.ContainsKey(99) ? percentiles[99] : 0,
                P999 = percentiles.ContainsKey(999) ? percentiles[999] : 0
            };
        }

        return new LatencyPercentiles();
    }

    /// <summary>
    /// Resets all performance counters
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _totalOperations, 0);
        Interlocked.Exchange(ref _totalLatencyMicroseconds, 0);

        lock (_lock)
        {
            _maxLatencyMicroseconds = 0;
            _monitoringStartTime = DateTime.UtcNow;
        }

        foreach (var tracker in _latencyTrackers.Values)
        {
            tracker.Reset();
        }

        foreach (var tracker in _throughputTrackers.Values)
        {
            tracker.Reset();
        }
    }

    /// <summary>
    /// Enables or disables high-frequency monitoring mode
    /// </summary>
    public void SetHighFrequencyMode(bool enabled)
    {
        var interval = enabled ? TimeSpan.FromMilliseconds(10) : TimeSpan.FromMilliseconds(100);

        _alertCheckTimer?.Change(interval, interval);

        // Adjust collection frequency
        var collectionInterval = enabled ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromSeconds(1);
        _metricsCollectionTimer?.Change(collectionInterval, collectionInterval);
    }

    /// <summary>
    /// Gets system resource usage metrics
    /// </summary>
    public SystemMetrics GetSystemMetrics()
    {
        var metrics = new SystemMetrics
        {
            ProcessorCount = Environment.ProcessorCount,
            WorkingSetMB = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024),
            GCTotalMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            ThreadCount = Process.GetCurrentProcess().Threads.Count
        };

        try
        {
            if (_cpuCounter != null)
            {
                metrics.CpuUsagePercent = _cpuCounter.NextValue();
            }

            if (_memoryCounter != null)
            {
                metrics.AvailableMemoryMB = _memoryCounter.NextValue();
            }
        }
        catch
        {
            // Performance counters may fail in some environments
        }

        return metrics;
    }

    private void InitializeDefaultTrackers()
    {
        // Pre-create trackers for common trading operations
        var commonOperations = new[]
        {
            "OrderExecution",
            "MarketDataProcessing",
            "FixMessageParsing",
            "FixMessageGeneration",
            "OrderValidation",
            "RiskCalculation",
            "DatabaseWrite",
            "DatabaseRead"
        };

        foreach (var operation in commonOperations)
        {
            _latencyTrackers.TryAdd(operation, new LatencyTracker(operation));
            _throughputTrackers.TryAdd(operation, new ThroughputTracker());
        }
    }

    private void CollectMetrics(object? state)
    {
        try
        {
            var metrics = GetMetrics();
            MetricsUpdated?.Invoke(this, metrics);

            // Check system resource thresholds
            if (metrics.SystemMetrics.CpuUsagePercent > MAX_CPU_USAGE_PERCENT)
            {
                TriggerAlert(AlertSeverity.Warning,
                    $"High CPU usage: {metrics.SystemMetrics.CpuUsagePercent:F1}%");
            }

            if (metrics.SystemMetrics.WorkingSetMB > MAX_MEMORY_USAGE_MB)
            {
                TriggerAlert(AlertSeverity.Warning,
                    $"High memory usage: {metrics.SystemMetrics.WorkingSetMB:F0} MB");
            }
        }
        catch
        {
            // Ignore collection failures to prevent monitoring from affecting performance
        }
    }

    private void CheckAlerts(object? state)
    {
        try
        {
            // This method is called frequently, so it should be very lightweight
            // More intensive alert checking is done in CollectMetrics

            // Check for excessive GC activity
            var gen2Collections = GC.CollectionCount(2);
            if (gen2Collections > 10) // Threshold for Gen2 collections
            {
                TriggerAlert(AlertSeverity.Warning,
                    $"Excessive Gen2 GC activity: {gen2Collections} collections");
            }
        }
        catch
        {
            // Ignore alert check failures
        }
    }

    private void TriggerAlert(AlertSeverity severity, string message)
    {
        var alert = new PerformanceAlert
        {
            Severity = severity,
            Message = message,
            Timestamp = DateTime.UtcNow,
            Metrics = GetMetrics()
        };

        PerformanceAlertTriggered?.Invoke(this, alert);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _metricsCollectionTimer?.Dispose();
        _alertCheckTimer?.Dispose();
        _cpuCounter?.Dispose();
        _memoryCounter?.Dispose();

        _disposed = true;
    }
}

/// <summary>
/// Latency measurement helper that automatically records timing
/// </summary>
public readonly struct LatencyMeasurement : IDisposable
{
    private readonly PerformanceMonitor _monitor;
    private readonly string _operationName;
    private readonly long _startTicks;

    internal LatencyMeasurement(PerformanceMonitor monitor, string operationName)
    {
        _monitor = monitor;
        _operationName = operationName;
        _startTicks = Stopwatch.GetTimestamp();
    }

    public void Dispose()
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - _startTicks;
        var latencyMicroseconds = (double)elapsedTicks / Stopwatch.Frequency * 1_000_000;
        _monitor.RecordLatency(_operationName, latencyMicroseconds);
    }
}

/// <summary>
/// Tracks throughput statistics for a specific operation
/// </summary>
public sealed class ThroughputTracker
{
    private readonly ConcurrentQueue<(DateTime Timestamp, int Count)> _recentOperations = new();
    private long _totalOperations;

    public void RecordOperations(int count)
    {
        _recentOperations.Enqueue((DateTime.UtcNow, count));
        Interlocked.Add(ref _totalOperations, count);

        // Keep only last 60 seconds of data
        var cutoff = DateTime.UtcNow.AddSeconds(-60);
        while (_recentOperations.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
        {
            _recentOperations.TryDequeue(out _);
        }
    }

    public ThroughputMetrics GetMetrics()
    {
        var recentOps = _recentOperations.ToArray();
        var totalRecent = recentOps.Sum(op => op.Count);
        var timeSpan = recentOps.Length > 0 ?
            DateTime.UtcNow - recentOps[0].Timestamp :
            TimeSpan.Zero;

        return new ThroughputMetrics
        {
            TotalOperations = Interlocked.Read(ref _totalOperations),
            RecentOperationsPerSecond = timeSpan.TotalSeconds > 0 ? totalRecent / timeSpan.TotalSeconds : 0
        };
    }

    public void Reset()
    {
        while (_recentOperations.TryDequeue(out _)) { }
        Interlocked.Exchange(ref _totalOperations, 0);
    }
}

/// <summary>
/// Comprehensive performance metrics for the trading system
/// </summary>
public class PerformanceMetrics
{
    public TimeSpan Uptime { get; set; }
    public long TotalOperations { get; set; }
    public double AverageLatencyMicroseconds { get; set; }
    public double MaxLatencyMicroseconds { get; set; }
    public double OperationsPerSecond { get; set; }
    public Dictionary<string, LatencyMetrics> LatencyTrackers { get; set; } = new();
    public Dictionary<string, ThroughputMetrics> ThroughputTrackers { get; set; } = new();
    public SystemMetrics SystemMetrics { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Latency metrics for a specific operation
/// </summary>
public class LatencyMetrics
{
    public long OperationCount { get; set; }
    public double AverageLatencyMicroseconds { get; set; }
    public double MinLatencyMicroseconds { get; set; }
    public double MaxLatencyMicroseconds { get; set; }
}

/// <summary>
/// Latency percentiles for statistical analysis
/// </summary>
public class LatencyPercentiles
{
    public double P50 { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
    public double P999 { get; set; }
}

/// <summary>
/// Throughput metrics for a specific operation
/// </summary>
public class ThroughputMetrics
{
    public long TotalOperations { get; set; }
    public double RecentOperationsPerSecond { get; set; }
}

/// <summary>
/// System resource usage metrics
/// </summary>
public class SystemMetrics
{
    public int ProcessorCount { get; set; }
    public double CpuUsagePercent { get; set; }
    public long WorkingSetMB { get; set; }
    public long GCTotalMemoryMB { get; set; }
    public double AvailableMemoryMB { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public int ThreadCount { get; set; }
}

/// <summary>
/// Performance alert information
/// </summary>
public class PerformanceAlert
{
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public PerformanceMetrics Metrics { get; set; } = new();
}

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}