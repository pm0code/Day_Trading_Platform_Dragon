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