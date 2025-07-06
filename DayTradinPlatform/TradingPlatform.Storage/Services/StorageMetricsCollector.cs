using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Storage.Interfaces;
using TradingPlatform.Storage.Services;

namespace TradingPlatform.Storage.Services;

/// <summary>
/// Collects and aggregates storage performance metrics
/// </summary>
public class StorageMetricsCollector : CanonicalServiceBase, IStorageMetricsCollector
{
    private readonly ConcurrentBag<StorageMetricEntry> _metrics;
    private readonly Timer _aggregationTimer;
    private readonly object _aggregationLock = new();

    public StorageMetricsCollector(ITradingLogger logger) : base(logger, "StorageMetricsCollector")
    {
        _metrics = new ConcurrentBag<StorageMetricEntry>();
        
        // Aggregate metrics every minute
        _aggregationTimer = new Timer(async _ => await AggregateMetricsAsync(), null, 
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Records a storage operation metric
    /// </summary>
    public async Task RecordStorageOperationAsync(string operation, StorageTier tier, 
        string dataType, long sizeBytes)
    {
        LogMethodEntry();
        try
        {
            var metric = new StorageMetricEntry
            {
                Timestamp = DateTime.UtcNow,
                Operation = operation,
                Tier = tier,
                DataType = dataType,
                SizeBytes = sizeBytes,
                LatencyMs = 0 // To be implemented with actual timing
            };

            _metrics.Add(metric);

            // Log high-level metrics periodically
            if (_metrics.Count % 1000 == 0)
            {
                LogInfo($"Recorded {_metrics.Count} storage operations");
            }

            await Task.CompletedTask;
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Failed to record storage operation {operation}", ex);
            LogMethodExit();
            throw;
        }
    }

    /// <summary>
    /// Gets aggregated metrics for specified period
    /// </summary>
    public async Task<Dictionary<string, object>> GetAggregatedMetricsAsync(TimeSpan period)
    {
        LogMethodEntry();

        try
        {
            var cutoff = DateTime.UtcNow.Subtract(period);
            var relevantMetrics = _metrics.Where(m => m.Timestamp >= cutoff).ToList();

            var aggregated = new Dictionary<string, object>();

            // Operations by type
            var operationCounts = relevantMetrics
                .GroupBy(m => m.Operation)
                .ToDictionary(g => g.Key + "_count", g => (object)g.Count());
            
            foreach (var kvp in operationCounts)
                aggregated[kvp.Key] = kvp.Value;

            // Data volume by tier
            var tierVolumes = relevantMetrics
                .GroupBy(m => m.Tier)
                .ToDictionary(
                    g => $"tier_{g.Key}_volume_gb", 
                    g => (object)(g.Sum(m => m.SizeBytes) / (1024.0 * 1024.0 * 1024.0))
                );
            
            foreach (var kvp in tierVolumes)
                aggregated[kvp.Key] = kvp.Value;

            // Operations by data type
            var dataTypeOps = relevantMetrics
                .GroupBy(m => m.DataType)
                .ToDictionary(
                    g => $"datatype_{g.Key}_ops",
                    g => (object)g.Count()
                );
            
            foreach (var kvp in dataTypeOps)
                aggregated[kvp.Key] = kvp.Value;

            // Performance metrics
            aggregated["total_operations"] = relevantMetrics.Count;
            aggregated["total_data_gb"] = relevantMetrics.Sum(m => m.SizeBytes) / (1024.0 * 1024.0 * 1024.0);
            aggregated["avg_operation_size_mb"] = relevantMetrics.Any() 
                ? relevantMetrics.Average(m => m.SizeBytes) / (1024.0 * 1024.0) 
                : 0;

            // Throughput
            if (period.TotalSeconds > 0)
            {
                aggregated["throughput_ops_per_sec"] = relevantMetrics.Count / period.TotalSeconds;
                aggregated["throughput_mb_per_sec"] = (relevantMetrics.Sum(m => m.SizeBytes) / (1024.0 * 1024.0)) / period.TotalSeconds;
            }

            await Task.CompletedTask;
            
            return aggregated;
        }
        catch (Exception ex)
        {
            LogError("Failed to aggregate metrics", ex);
            throw;
        }
        finally
        {
            LogMethodExit();
        }
    }

    /// <summary>
    /// Periodically aggregates and cleans old metrics
    /// </summary>
    private async Task AggregateMetricsAsync()
    {
        LogMethodEntry();

        lock (_aggregationLock)
        {
            try
            {
                // Remove metrics older than 24 hours
                var cutoff = DateTime.UtcNow.AddHours(-24);
                var oldMetrics = new List<StorageMetricEntry>();
                var currentMetrics = new List<StorageMetricEntry>();

                // Separate old and current metrics
                while (_metrics.TryTake(out var metric))
                {
                    if (metric.Timestamp < cutoff)
                        oldMetrics.Add(metric);
                    else
                        currentMetrics.Add(metric);
                }

                // Put current metrics back
                foreach (var metric in currentMetrics)
                {
                    _metrics.Add(metric);
                }

                if (oldMetrics.Any())
                {
                    LogInfo($"Cleaned {oldMetrics.Count} old metrics from memory");
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to aggregate metrics", ex);
            }
        }

        await Task.CompletedTask;
        LogMethodExit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _aggregationTimer?.Dispose();
        }
        base.Dispose(disposing);
    }

    private class StorageMetricEntry
    {
        public DateTime Timestamp { get; set; }
        public string Operation { get; set; } = string.Empty;
        public StorageTier Tier { get; set; }
        public string DataType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public double LatencyMs { get; set; }
    }
}