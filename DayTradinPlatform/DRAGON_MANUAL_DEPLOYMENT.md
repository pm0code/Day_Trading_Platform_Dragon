# DRAGON MANUAL DEPLOYMENT - COPY THESE FILES

Since you're on DRAGON now, please manually copy these 2 files to complete the canonical logging cleanup:

## FILE 1: PerformanceStats.cs (NEW FILE)
**Location**: `d:\BuildWorkspace\DayTradingPlatform\TradingPlatform.Core\Logging\PerformanceStats.cs`

```csharp
// TradingPlatform.Core.Logging.PerformanceStats - CANONICAL PERFORMANCE TRACKING
// Thread-safe performance statistics for high-frequency trading operations
// Tracks call counts, timing statistics, and performance deviations

namespace TradingPlatform.Core.Logging;

/// <summary>
/// Thread-safe performance statistics for tracking method execution metrics
/// Optimized for high-frequency trading operations with nanosecond precision
/// </summary>
public sealed class PerformanceStats
{
    private long _callCount = 0;
    private double _totalDurationNs = 0;
    private double _minDurationNs = double.MaxValue;
    private double _maxDurationNs = 0;
    private readonly object _lock = new object();

    /// <summary>
    /// Total number of method calls tracked
    /// </summary>
    public long CallCount 
    { 
        get 
        { 
            lock (_lock) 
            { 
                return _callCount; 
            } 
        } 
        set 
        { 
            lock (_lock) 
            { 
                _callCount = value; 
            } 
        } 
    }

    /// <summary>
    /// Total execution duration in nanoseconds
    /// </summary>
    public double TotalDurationNs 
    { 
        get 
        { 
            lock (_lock) 
            { 
                return _totalDurationNs; 
            } 
        } 
        set 
        { 
            lock (_lock) 
            { 
                _totalDurationNs = value; 
            } 
        } 
    }

    /// <summary>
    /// Minimum execution duration in nanoseconds
    /// </summary>
    public double MinDurationNs 
    { 
        get 
        { 
            lock (_lock) 
            { 
                return _minDurationNs; 
            } 
        } 
        set 
        { 
            lock (_lock) 
            { 
                _minDurationNs = value; 
            } 
        } 
    }

    /// <summary>
    /// Maximum execution duration in nanoseconds
    /// </summary>
    public double MaxDurationNs 
    { 
        get 
        { 
            lock (_lock) 
            { 
                return _maxDurationNs; 
            } 
        } 
        set 
        { 
            lock (_lock) 
            { 
                _maxDurationNs = value; 
            } 
        } 
    }

    /// <summary>
    /// Average execution duration in nanoseconds
    /// </summary>
    public double AverageDurationNs
    {
        get
        {
            lock (_lock)
            {
                return _callCount > 0 ? _totalDurationNs / _callCount : 0;
            }
        }
    }

    /// <summary>
    /// Thread-safe method to update statistics with new execution time
    /// </summary>
    public void UpdateStats(double durationNs)
    {
        lock (_lock)
        {
            _callCount++;
            _totalDurationNs += durationNs;
            
            if (durationNs < _minDurationNs)
                _minDurationNs = durationNs;
                
            if (durationNs > _maxDurationNs)
                _maxDurationNs = durationNs;
        }
    }

    /// <summary>
    /// Reset all statistics to initial state
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _callCount = 0;
            _totalDurationNs = 0;
            _minDurationNs = double.MaxValue;
            _maxDurationNs = 0;
        }
    }
}
```

## FILE 2: Updated TradingLogOrchestrator.cs
**Location**: `d:\BuildWorkspace\DayTradingPlatform\TradingPlatform.Core\Logging\TradingLogOrchestrator.cs`

**CRITICAL CHANGE**: Replace the `EnqueueLogEntry` method (around lines 602-621) and `TrackPerformanceAsync` method (around lines 851-862) with the fixed versions.

**KEY FIXES in EnqueueLogEntry method**:
```csharp
        var logEntry = new LogEntry
        {
            Timestamp = timestamp,
            Level = level,
            Message = message,
            Exception = exception != null ? ExceptionContext.FromException(exception) : null,
            Source = new SourceContext 
            {
                MethodName = memberName,
                FilePath = Path.GetFileName(sourceFilePath),
                LineNumber = lineNumber,
                Service = _serviceName
            },
            Thread = new ThreadContext
            {
                ThreadId = Environment.CurrentManagedThreadId
            },
            CorrelationId = correlationId,
            AdditionalData = data != null ? new Dictionary<string, object> { ["Data"] = data } : null
        };
```

**KEY FIXES in TrackPerformanceAsync method**:
```csharp
    private void TrackPerformanceAsync(string metricName, double value, string unit)
    {
        Task.Run(() =>
        {
            _performanceStats.AddOrUpdate(metricName,
                new PerformanceStats(),
                (key, existing) => existing);
            
            // Update the stats using the thread-safe method
            _performanceStats[metricName].UpdateStats(value);
        });
    }
```

## AFTER COPYING FILES:
```powershell
cd d:\BuildWorkspace\DayTradingPlatform
dotnet build TradingPlatform.Core --verbosity normal
```

**EXPECTED**: Zero compilation errors (down from 14)