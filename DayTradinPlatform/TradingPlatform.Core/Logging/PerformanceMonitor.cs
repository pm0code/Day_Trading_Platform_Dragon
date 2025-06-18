// TradingPlatform.Core.Logging.PerformanceMonitor - PERFORMANCE THRESHOLD MONITORING
// User-configurable thresholds with deviation alerts for trading operations

using System.Collections.Concurrent;
using System.Diagnostics;

namespace TradingPlatform.Core.Logging;

/// <summary>
/// Monitors performance against user-configurable thresholds
/// Tracks trading operations, method execution times, and system resources
/// </summary>
internal class PerformanceMonitor
{
    private readonly PerformanceThresholds _thresholds;
    private readonly ConcurrentDictionary<string, PerformanceStats> _methodStats = new();
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;

    public PerformanceMonitor(PerformanceThresholds thresholds)
    {
        _thresholds = thresholds;
        
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
        }
        catch
        {
            // Performance counters may not be available in all environments
        }
    }

    public PerformanceViolation? CheckMethodPerformance(string methodName, TimeSpan duration)
    {
        var thresholdMs = GetMethodThreshold(methodName);
        if (duration.TotalMilliseconds > thresholdMs)
        {
            return new PerformanceViolation
            {
                Type = "Method Execution",
                Method = methodName,
                ActualValue = duration.TotalMilliseconds,
                ThresholdValue = thresholdMs,
                Severity = GetSeverity(duration.TotalMilliseconds, thresholdMs),
                Recommendation = $"Method {methodName} exceeded threshold by {duration.TotalMilliseconds - thresholdMs:F2}ms"
            };
        }
        return null;
    }

    public PerformanceViolation? CheckTradingPerformance(string action, TimeSpan executionTime)
    {
        var thresholdMicros = action.ToLower() switch
        {
            var a when a.Contains("order") => _thresholds.OrderExecutionMicroseconds,
            var a when a.Contains("trade") => _thresholds.TradingOperationMicroseconds,
            var a when a.Contains("risk") => _thresholds.RiskCalculationMicroseconds,
            var a when a.Contains("market") => _thresholds.MarketDataMicroseconds,
            _ => _thresholds.TradingOperationMicroseconds
        };

        var actualMicros = executionTime.TotalMicroseconds;
        if (actualMicros > thresholdMicros)
        {
            return new PerformanceViolation
            {
                Type = "Trading Operation",
                Method = action,
                ActualValue = actualMicros,
                ThresholdValue = thresholdMicros,
                Severity = GetSeverity(actualMicros, thresholdMicros),
                Recommendation = $"Trading operation {action} exceeded threshold by {actualMicros - thresholdMicros:F1}μs"
            };
        }
        return null;
    }

    public bool HasPerformanceData(string methodName)
    {
        return _methodStats.ContainsKey(methodName);
    }

    public PerformanceContext GetPerformanceContext(string methodName)
    {
        if (_methodStats.TryGetValue(methodName, out var stats))
        {
            return new PerformanceContext
            {
                Operation = methodName,
                DurationNanoseconds = stats.TotalDurationNs / Math.Max(1, stats.CallCount),
                DurationMilliseconds = (stats.TotalDurationNs / Math.Max(1, stats.CallCount)) / 1_000_000.0,
                ResourceUsage = new Dictionary<string, object>
                {
                    ["call_count"] = stats.CallCount,
                    ["avg_duration_ns"] = stats.AverageDurationNs,
                    ["min_duration_ns"] = stats.MinDurationNs,
                    ["max_duration_ns"] = stats.MaxDurationNs
                }
            };
        }
        return new PerformanceContext { Operation = methodName };
    }

    public void UpdateStatistics(List<LogEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.Performance != null && entry.Source.MethodName != null)
            {
                var stats = _methodStats.GetOrAdd(entry.Source.MethodName, _ => new PerformanceStats());
                var durationNs = entry.Performance.DurationNanoseconds ?? 0;
                
                stats.CallCount++;
                stats.TotalDurationNs += durationNs;
                stats.MinDurationNs = Math.Min(stats.MinDurationNs, durationNs);
                stats.MaxDurationNs = Math.Max(stats.MaxDurationNs, durationNs);
            }
        }
    }

    public double GetCpuUsage()
    {
        try
        {
            return _cpuCounter?.NextValue() ?? 0.0;
        }
        catch
        {
            return 0.0;
        }
    }

    public double GetMemoryUsage()
    {
        try
        {
            return _memoryCounter?.NextValue() ?? 0.0;
        }
        catch
        {
            return 0.0;
        }
    }

    private double GetMethodThreshold(string methodName)
    {
        return methodName.ToLower() switch
        {
            var m when m.Contains("trade") || m.Contains("order") => _thresholds.TradingOperationMicroseconds / 1000.0,
            var m when m.Contains("risk") => _thresholds.RiskCalculationMicroseconds / 1000.0,
            var m when m.Contains("market") || m.Contains("data") => _thresholds.MarketDataMicroseconds / 1000.0,
            var m when m.Contains("database") || m.Contains("db") => _thresholds.DatabaseOperationMilliseconds,
            _ => _thresholds.DataProcessingMilliseconds
        };
    }

    private string GetSeverity(double actual, double threshold)
    {
        var ratio = actual / threshold;
        return ratio switch
        {
            > 3.0 => "CRITICAL",
            > 2.0 => "HIGH",
            > 1.5 => "MEDIUM",
            _ => "LOW"
        };
    }
}

/// <summary>
/// Performance violation details
/// </summary>
public class PerformanceViolation
{
    public string Type { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public double ActualValue { get; set; }
    public double ThresholdValue { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}