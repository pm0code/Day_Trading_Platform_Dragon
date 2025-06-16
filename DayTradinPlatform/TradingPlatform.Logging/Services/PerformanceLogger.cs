using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TradingPlatform.Logging.Interfaces;

namespace TradingPlatform.Logging.Services;

/// <summary>
/// High-performance logger for ultra-low latency trading operations
/// Tracks method execution times, throughput, and latency percentiles
/// CRITICAL: Every performance-sensitive operation must be measured
/// </summary>
public class PerformanceLogger : IPerformanceLogger
{
    private readonly ITradingLogger _tradingLogger;
    private readonly ILogger<PerformanceLogger> _logger;
    private readonly ConcurrentDictionary<string, List<double>> _latencyHistograms = new();
    private readonly ConcurrentDictionary<string, PerformanceCounters> _performanceCounters = new();
    private readonly Timer _reportingTimer;

    public PerformanceLogger(ITradingLogger tradingLogger, ILogger<PerformanceLogger> logger)
    {
        _tradingLogger = tradingLogger ?? throw new ArgumentNullException(nameof(tradingLogger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Report performance statistics every 30 seconds
        _reportingTimer = new Timer(ReportPerformanceStatistics, null, 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Measure operation execution time with automatic logging
    /// Returns disposable that logs completion when disposed
    /// </summary>
    public IDisposable MeasureOperation(string operationName, string? correlationId = null)
    {
        correlationId ??= _tradingLogger.GenerateCorrelationId();
        
        _tradingLogger.LogDebugTrace($"Starting performance measurement for operation: {operationName}", 
            new Dictionary<string, object>
            {
                ["operation"] = operationName,
                ["correlation_id"] = correlationId,
                ["start_time"] = DateTime.UtcNow
            });

        return new PerformanceMeasurement(operationName, correlationId, this);
    }

    /// <summary>
    /// Log completed operation with performance metrics
    /// </summary>
    public void LogOperationComplete(string operationName, TimeSpan duration, bool success, string? correlationId = null)
    {
        correlationId ??= _tradingLogger.GenerateCorrelationId();

        // Record latency for histogram calculation
        RecordLatency(operationName, duration.TotalMilliseconds);
        
        // Update performance counters
        UpdatePerformanceCounters(operationName, duration, success);

        // Log the operation completion
        _tradingLogger.LogPerformanceMetric($"operation.{operationName}.duration", 
            duration.TotalMilliseconds, "ms", new Dictionary<string, object>
            {
                ["operation"] = operationName,
                ["success"] = success,
                ["correlation_id"] = correlationId
            });

        var logLevel = success ? LogLevel.Debug : LogLevel.Warning;
        var status = success ? "SUCCESS" : "FAILED";

        _logger.Log(logLevel, 
            "OPERATION_COMPLETE: {OperationName} {Status} in {DurationMs}ms [CorrelationId: {CorrelationId}]",
            operationName, status, duration.TotalMilliseconds, correlationId);

        // Check for performance violations
        CheckPerformanceThresholds(operationName, duration, correlationId);
    }

    /// <summary>
    /// Log throughput metrics for batch operations
    /// </summary>
    public void LogThroughput(string operation, int itemsProcessed, TimeSpan duration, string? correlationId = null)
    {
        correlationId ??= _tradingLogger.GenerateCorrelationId();

        var throughputPerSecond = itemsProcessed / duration.TotalSeconds;
        var avgLatencyMs = duration.TotalMilliseconds / itemsProcessed;

        _tradingLogger.LogPerformanceMetric($"throughput.{operation}.items_per_second", 
            throughputPerSecond, "items/sec", new Dictionary<string, object>
            {
                ["operation"] = operation,
                ["items_processed"] = itemsProcessed,
                ["duration_ms"] = duration.TotalMilliseconds,
                ["avg_latency_ms"] = avgLatencyMs,
                ["correlation_id"] = correlationId
            });

        _logger.LogInformation(
            "THROUGHPUT: {Operation} processed {ItemsProcessed} items in {DurationMs}ms ({ThroughputPerSec:F2} items/sec, {AvgLatencyMs:F2}ms avg) [CorrelationId: {CorrelationId}]",
            operation, itemsProcessed, duration.TotalMilliseconds, throughputPerSecond, avgLatencyMs, correlationId);
    }

    /// <summary>
    /// Log latency percentiles for operation analysis
    /// </summary>
    public void LogLatencyPercentile(string operation, TimeSpan p50, TimeSpan p95, TimeSpan p99, string? correlationId = null)
    {
        correlationId ??= _tradingLogger.GenerateCorrelationId();

        _tradingLogger.LogPerformanceMetric($"latency.{operation}.p50", p50.TotalMilliseconds, "ms");
        _tradingLogger.LogPerformanceMetric($"latency.{operation}.p95", p95.TotalMilliseconds, "ms");
        _tradingLogger.LogPerformanceMetric($"latency.{operation}.p99", p99.TotalMilliseconds, "ms");

        _logger.LogInformation(
            "LATENCY_PERCENTILES: {Operation} - P50: {P50Ms}ms, P95: {P95Ms}ms, P99: {P99Ms}ms [CorrelationId: {CorrelationId}]",
            operation, p50.TotalMilliseconds, p95.TotalMilliseconds, p99.TotalMilliseconds, correlationId);

        // Alert on high P99 latency (>100ms for critical operations)
        if (IsCriticalOperation(operation) && p99.TotalMilliseconds > 100)
        {
            _tradingLogger.LogLatencyViolation(operation, p99, TimeSpan.FromMilliseconds(100), correlationId);
        }
    }

    private void RecordLatency(string operationName, double latencyMs)
    {
        _latencyHistograms.AddOrUpdate(operationName, 
            new List<double> { latencyMs },
            (key, existing) =>
            {
                lock (existing)
                {
                    existing.Add(latencyMs);
                    // Keep only last 1000 measurements for memory efficiency
                    if (existing.Count > 1000)
                    {
                        existing.RemoveRange(0, existing.Count - 1000);
                    }
                    return existing;
                }
            });
    }

    private void UpdatePerformanceCounters(string operationName, TimeSpan duration, bool success)
    {
        _performanceCounters.AddOrUpdate(operationName,
            new PerformanceCounters
            {
                TotalOperations = 1,
                SuccessfulOperations = success ? 1 : 0,
                TotalDurationMs = duration.TotalMilliseconds,
                MinDurationMs = duration.TotalMilliseconds,
                MaxDurationMs = duration.TotalMilliseconds
            },
            (key, existing) =>
            {
                existing.TotalOperations++;
                if (success) existing.SuccessfulOperations++;
                existing.TotalDurationMs += duration.TotalMilliseconds;
                existing.MinDurationMs = Math.Min(existing.MinDurationMs, duration.TotalMilliseconds);
                existing.MaxDurationMs = Math.Max(existing.MaxDurationMs, duration.TotalMilliseconds);
                return existing;
            });
    }

    private void CheckPerformanceThresholds(string operationName, TimeSpan duration, string correlationId)
    {
        var thresholds = GetPerformanceThresholds(operationName);
        
        if (duration > thresholds.Warning)
        {
            var severity = duration > thresholds.Critical ? "CRITICAL" : "WARNING";
            
            _logger.LogWarning(
                "PERFORMANCE_THRESHOLD_EXCEEDED: {OperationName} took {DurationMs}ms, threshold: {ThresholdMs}ms, severity: {Severity} [CorrelationId: {CorrelationId}]",
                operationName, duration.TotalMilliseconds, thresholds.Warning.TotalMilliseconds, severity, correlationId);

            if (duration > thresholds.Critical)
            {
                _tradingLogger.LogLatencyViolation(operationName, duration, thresholds.Critical, correlationId);
            }
        }
    }

    private static PerformanceThresholds GetPerformanceThresholds(string operationName)
    {
        // Define performance thresholds for different operation types
        return operationName.ToLower() switch
        {
            var op when op.Contains("order") => new PerformanceThresholds
            {
                Warning = TimeSpan.FromMicroseconds(100),   // 100μs warning
                Critical = TimeSpan.FromMicroseconds(500)   // 500μs critical
            },
            var op when op.Contains("market") => new PerformanceThresholds
            {
                Warning = TimeSpan.FromMicroseconds(50),    // 50μs warning
                Critical = TimeSpan.FromMicroseconds(200)   // 200μs critical
            },
            var op when op.Contains("strategy") => new PerformanceThresholds
            {
                Warning = TimeSpan.FromMilliseconds(45),    // 45ms warning
                Critical = TimeSpan.FromMilliseconds(100)   // 100ms critical
            },
            var op when op.Contains("risk") => new PerformanceThresholds
            {
                Warning = TimeSpan.FromMilliseconds(10),    // 10ms warning
                Critical = TimeSpan.FromMilliseconds(50)    // 50ms critical
            },
            _ => new PerformanceThresholds
            {
                Warning = TimeSpan.FromMilliseconds(100),   // 100ms warning default
                Critical = TimeSpan.FromMilliseconds(500)   // 500ms critical default
            }
        };
    }

    private static bool IsCriticalOperation(string operationName)
    {
        var criticalOperations = new[] { "order", "execution", "market_data", "risk_check" };
        return criticalOperations.Any(op => operationName.ToLower().Contains(op));
    }

    private void ReportPerformanceStatistics(object? state)
    {
        try
        {
            foreach (var kvp in _performanceCounters.ToList())
            {
                var operationName = kvp.Key;
                var counters = kvp.Value;
                
                if (counters.TotalOperations == 0) continue;

                var avgDurationMs = counters.TotalDurationMs / counters.TotalOperations;
                var successRate = (double)counters.SuccessfulOperations / counters.TotalOperations * 100;

                _tradingLogger.LogPerformanceMetric($"operation.{operationName}.avg_duration", avgDurationMs, "ms");
                _tradingLogger.LogPerformanceMetric($"operation.{operationName}.success_rate", successRate, "percent");
                _tradingLogger.LogPerformanceMetric($"operation.{operationName}.total_operations", counters.TotalOperations, "count");

                // Calculate and report percentiles if we have histogram data
                if (_latencyHistograms.TryGetValue(operationName, out var latencies) && latencies.Count > 0)
                {
                    lock (latencies)
                    {
                        var sortedLatencies = latencies.OrderBy(x => x).ToArray();
                        var p50 = GetPercentile(sortedLatencies, 0.50);
                        var p95 = GetPercentile(sortedLatencies, 0.95);
                        var p99 = GetPercentile(sortedLatencies, 0.99);

                        LogLatencyPercentile(operationName, 
                            TimeSpan.FromMilliseconds(p50),
                            TimeSpan.FromMilliseconds(p95),
                            TimeSpan.FromMilliseconds(p99));
                    }
                }

                _logger.LogDebug(
                    "PERFORMANCE_REPORT: {OperationName} - Avg: {AvgMs}ms, Min: {MinMs}ms, Max: {MaxMs}ms, Success: {SuccessRate:F1}%, Count: {TotalOps}",
                    operationName, avgDurationMs, counters.MinDurationMs, counters.MaxDurationMs, successRate, counters.TotalOperations);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to report performance statistics");
        }
    }

    private static double GetPercentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0) return 0;
        
        var index = (percentile * (sortedValues.Length - 1));
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        
        if (lower == upper) return sortedValues[lower];
        
        var weight = index - lower;
        return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
    }

    public void Dispose()
    {
        _reportingTimer?.Dispose();
    }
}

/// <summary>
/// Performance measurement scope that automatically logs completion
/// </summary>
internal class PerformanceMeasurement : IDisposable
{
    private readonly string _operationName;
    private readonly string _correlationId;
    private readonly PerformanceLogger _performanceLogger;
    private readonly Stopwatch _stopwatch;
    private bool _disposed;

    public PerformanceMeasurement(string operationName, string correlationId, PerformanceLogger performanceLogger)
    {
        _operationName = operationName;
        _correlationId = correlationId;
        _performanceLogger = performanceLogger;
        _stopwatch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _stopwatch.Stop();
        _performanceLogger.LogOperationComplete(_operationName, _stopwatch.Elapsed, true, _correlationId);
        _disposed = true;
    }
}

/// <summary>
/// Performance counters for operation tracking
/// </summary>
internal class PerformanceCounters
{
    public long TotalOperations { get; set; }
    public long SuccessfulOperations { get; set; }
    public double TotalDurationMs { get; set; }
    public double MinDurationMs { get; set; } = double.MaxValue;
    public double MaxDurationMs { get; set; }
}

/// <summary>
/// Performance thresholds for different operation types
/// </summary>
internal record PerformanceThresholds
{
    public TimeSpan Warning { get; init; }
    public TimeSpan Critical { get; init; }
}