// TradingPlatform.Logging.Services.PerformanceLogger - CANONICAL DELEGATION TO TradingLogOrchestrator
// ZERO Microsoft.Extensions.Logging dependencies - Delegates to unified LogOrchestrator
// ALL PERFORMANCE LOGGING MUST GO THROUGH TradingLogOrchestrator.Instance

using System.Collections.Concurrent;
using System.Diagnostics;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Logging.Interfaces;

namespace TradingPlatform.Logging.Services;

/// <summary>
/// CANONICAL PERFORMANCE LOGGER - Delegates to TradingLogOrchestrator.Instance
/// High-performance wrapper that ensures all performance logging goes through the orchestrator
/// CRITICAL: Use TradingLogOrchestrator.Instance directly for best performance
/// </summary>
public class PerformanceLogger : IPerformanceLogger
{
    private readonly TradingLogOrchestrator _orchestrator;
    private readonly Core.Interfaces.ITradingLogger _logger;
    private readonly ConcurrentDictionary<string, List<double>> _latencyHistograms = new();
    private readonly ConcurrentDictionary<string, PerformanceCounters> _performanceCounters = new();
    private readonly Timer _reportingTimer;

    public PerformanceLogger(Core.Interfaces.ITradingLogger logger)
    {
        _orchestrator = TradingLogOrchestrator.Instance;
        _logger = logger;

        // Report performance statistics every 30 seconds
        _reportingTimer = new Timer(ReportPerformanceStatistics, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        // Log delegation setup
        _orchestrator.LogInfo("PerformanceLogger initialized - delegating to TradingLogOrchestrator",
            new { DelegationType = "Canonical", ReportingInterval = "30s" });
    }

    /// <summary>
    /// Measure operation execution time with automatic logging
    /// Returns disposable that logs completion when disposed
    /// </summary>
    public IDisposable MeasureOperation(string operationName, string? correlationId = null)
    {
        correlationId ??= _orchestrator.GenerateCorrelationId();

        _orchestrator.LogMethodEntry(new { OperationName = operationName, CorrelationId = correlationId });

        return new PerformanceMeasurement(operationName, correlationId, this);
    }

    /// <summary>
    /// Log completed operation with performance metrics - Delegates to Orchestrator
    /// </summary>
    public void LogOperationComplete(string operationName, TimeSpan duration, bool success, string? correlationId = null)
    {
        correlationId ??= _orchestrator.GenerateCorrelationId();

        // Record latency for histogram calculation
        RecordLatency(operationName, duration.TotalMilliseconds);

        // Update performance counters
        UpdatePerformanceCounters(operationName, duration, success);

        // Delegate to orchestrator for actual logging
        _orchestrator.LogPerformance(operationName, duration, success, null, null, null, null);

        // Check for performance violations
        CheckPerformanceThresholds(operationName, duration, correlationId);
    }

    /// <summary>
    /// Log throughput metrics for batch operations - Delegates to Orchestrator
    /// </summary>
    public void LogThroughput(string operation, int itemsProcessed, TimeSpan duration, string? correlationId = null)
    {
        correlationId ??= _orchestrator.GenerateCorrelationId();

        var throughputPerSecond = itemsProcessed / duration.TotalSeconds;
        var avgLatencyMs = duration.TotalMilliseconds / itemsProcessed;

        // Delegate to orchestrator
        _orchestrator.LogPerformance(operation, duration, true, throughputPerSecond,
            new { ItemsProcessed = itemsProcessed, AvgLatencyMs = avgLatencyMs },
            new { ThroughputPerSecond = throughputPerSecond });
    }

    /// <summary>
    /// Log latency percentiles for operation analysis - Delegates to Orchestrator
    /// </summary>
    public void LogLatencyPercentile(string operation, TimeSpan p50, TimeSpan p95, TimeSpan p99, string? correlationId = null)
    {
        correlationId ??= _orchestrator.GenerateCorrelationId();

        // Delegate individual percentile logging to orchestrator
        _orchestrator.LogPerformance($"{operation}.P50", p50, true, null, null, new { Percentile = "50th" });
        _orchestrator.LogPerformance($"{operation}.P95", p95, true, null, null, new { Percentile = "95th" });
        _orchestrator.LogPerformance($"{operation}.P99", p99, true, null, null, new { Percentile = "99th" });

        // Log summary
        _orchestrator.LogInfo($"Latency percentiles for {operation}",
            new { P50 = p50.TotalMilliseconds, P95 = p95.TotalMilliseconds, P99 = p99.TotalMilliseconds });

        // Alert on high P99 latency (>100ms for critical operations)
        if (IsCriticalOperation(operation) && p99.TotalMilliseconds > 100)
        {
            _orchestrator.LogWarning($"High P99 latency for {operation}",
                impact: $"Performance degradation: {p99.TotalMilliseconds}ms P99 latency",
                recommendedAction: "Investigate performance bottleneck");
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

            _orchestrator.LogWarning($"Performance threshold exceeded for {operationName}",
                impact: $"Performance degradation: {duration.TotalMilliseconds}ms vs {thresholds.Warning.TotalMilliseconds}ms threshold",
                recommendedAction: "Investigate performance bottleneck",
                additionalData: new { OperationName = operationName, Duration = duration, Threshold = thresholds.Warning, Severity = severity, CorrelationId = correlationId });

            if (duration > thresholds.Critical)
            {
                _orchestrator.LogRisk("PerformanceViolation", "CRITICAL",
                    $"Critical performance threshold exceeded: {operationName}",
                    null, null,
                    new[] { "Immediate performance investigation required" },
                    "Performance SLA violation");
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

                // Delegate performance reporting to orchestrator
                _orchestrator.LogPerformance($"Report.{operationName}",
                    TimeSpan.FromMilliseconds(avgDurationMs), true, null,
                    new { SuccessRate = successRate, TotalOperations = counters.TotalOperations, MinDuration = counters.MinDurationMs, MaxDuration = counters.MaxDurationMs });

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
            }
        }
        catch (Exception ex)
        {
            _orchestrator.LogError("Failed to report performance statistics", ex,
                operationContext: "Performance statistics reporting",
                troubleshootingHints: "Check performance logger health");
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
        _orchestrator.LogInfo("PerformanceLogger disposed");
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