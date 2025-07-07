using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace TradingPlatform.Core.Performance
{
    /// <summary>
    /// High-precision latency tracking for performance monitoring
    /// </summary>
    public sealed class LatencyTracker
    {
        private readonly ConcurrentBag<long> _latencies = new();
        private readonly Timer _reportTimer;
        private readonly string _name;
        private readonly int _maxSamples;
        private long _totalSamples;
        private long _totalLatency;

        public LatencyTracker(string name, int maxSamples = 10000, TimeSpan? reportInterval = null)
        {
            _name = name;
            _maxSamples = maxSamples;
            
            if (reportInterval.HasValue)
            {
                _reportTimer = new Timer(_ => PrintReport(), null, reportInterval.Value, reportInterval.Value);
            }
            else
            {
                _reportTimer = null!;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LatencyScope MeasureScope()
        {
            return new LatencyScope(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordLatency(long microseconds)
        {
            Interlocked.Increment(ref _totalSamples);
            Interlocked.Add(ref _totalLatency, microseconds);

            if (_latencies.Count < _maxSamples)
            {
                _latencies.Add(microseconds);
            }
        }

        public LatencyStatistics GetStatistics()
        {
            var samples = _latencies.ToArray();
            if (samples.Length == 0)
            {
                return new LatencyStatistics
                {
                    Name = _name,
                    Count = _totalSamples,
                    Mean = 0,
                    Min = 0,
                    Max = 0,
                    P50 = 0,
                    P95 = 0,
                    P99 = 0
                };
            }

            Array.Sort(samples);
            
            return new LatencyStatistics
            {
                Name = _name,
                Count = _totalSamples,
                Mean = _totalLatency / (double)_totalSamples,
                Min = samples[0],
                Max = samples[samples.Length - 1],
                P50 = GetPercentile(samples, 50),
                P95 = GetPercentile(samples, 95),
                P99 = GetPercentile(samples, 99)
            };
        }

        private void PrintReport()
        {
            var stats = GetStatistics();
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {stats}");
        }

        private static long GetPercentile(long[] sortedArray, int percentile)
        {
            var index = (int)Math.Ceiling(sortedArray.Length * percentile / 100.0) - 1;
            return sortedArray[Math.Max(0, Math.Min(index, sortedArray.Length - 1))];
        }

        public Dictionary<string, object> GetMetrics()
        {
            var stats = GetStatistics();
            return new Dictionary<string, object>
            {
                ["Name"] = stats.Name,
                ["Count"] = stats.Count,
                ["Mean"] = stats.Mean,
                ["Min"] = stats.Min,
                ["Max"] = stats.Max,
                ["P50"] = stats.P50,
                ["P95"] = stats.P95,
                ["P99"] = stats.P99
            };
        }

        public Dictionary<int, long> GetPercentiles(params int[] percentiles)
        {
            var samples = _latencies.ToArray();
            if (samples.Length == 0)
            {
                return percentiles.ToDictionary(p => p, p => 0L);
            }

            Array.Sort(samples);
            return percentiles.ToDictionary(p => p, p => GetPercentile(samples, p));
        }

        public void Reset()
        {
            _latencies.Clear();
            Interlocked.Exchange(ref _totalSamples, 0);
            Interlocked.Exchange(ref _totalLatency, 0);
        }

        public void Clear()
        {
            Reset();
        }

        public void Dispose()
        {
            _reportTimer?.Dispose();
        }

        public readonly struct LatencyScope : IDisposable
        {
            private readonly LatencyTracker _tracker;
            private readonly long _startTicks;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal LatencyScope(LatencyTracker tracker)
            {
                _tracker = tracker;
                _startTicks = Stopwatch.GetTimestamp();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                var endTicks = Stopwatch.GetTimestamp();
                var elapsedTicks = endTicks - _startTicks;
                var microseconds = elapsedTicks * 1_000_000 / Stopwatch.Frequency;
                _tracker.RecordLatency(microseconds);
            }
        }
    }

    public struct LatencyStatistics
    {
        public string Name { get; init; }
        public long Count { get; init; }
        public double Mean { get; init; }
        public long Min { get; init; }
        public long Max { get; init; }
        public long P50 { get; init; }
        public long P95 { get; init; }
        public long P99 { get; init; }

        public override string ToString()
        {
            return $"{Name}: Count={Count}, Mean={Mean:F2}μs, Min={Min}μs, Max={Max}μs, P50={P50}μs, P95={P95}μs, P99={P99}μs";
        }
    }

    /// <summary>
    /// Global latency tracking registry
    /// </summary>
    public static class LatencyTracking
    {
        private static readonly ConcurrentDictionary<string, LatencyTracker> _trackers = new();

        public static LatencyTracker GetOrCreate(string name, int maxSamples = 10000)
        {
            return _trackers.GetOrAdd(name, n => new LatencyTracker(n, maxSamples));
        }

        public static void PrintAllStatistics()
        {
            Console.WriteLine("\n=== Latency Statistics ===");
            foreach (var tracker in _trackers.Values)
            {
                Console.WriteLine(tracker.GetStatistics());
            }
            Console.WriteLine("========================\n");
        }
    }
}