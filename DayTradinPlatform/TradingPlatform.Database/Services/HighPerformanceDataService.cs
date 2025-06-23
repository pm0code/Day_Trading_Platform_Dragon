using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Threading.Channels;
using TradingPlatform.Database.Context;
using TradingPlatform.Database.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Database.Services;

/// <summary>
/// High-performance data service optimized for microsecond-precision market data storage
/// Uses batching, connection pooling, and async patterns for maximum throughput
/// </summary>
public class HighPerformanceDataService : IDisposable
{
    private readonly TradingDbContext _context;
    private readonly ITradingLogger _logger;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    // High-performance channels for async data processing
    private readonly Channel<MarketDataRecord> _marketDataChannel;
    private readonly Channel<ExecutionRecord> _executionChannel;
    private readonly Channel<PerformanceMetric> _performanceChannel;

    private readonly ChannelWriter<MarketDataRecord> _marketDataWriter;
    private readonly ChannelWriter<ExecutionRecord> _executionWriter;
    private readonly ChannelWriter<PerformanceMetric> _performanceWriter;

    // Performance counters
    private long _marketDataInsertCount;
    private long _executionInsertCount;
    private long _performanceInsertCount;

    // Batch processing configuration
    private const int BatchSize = 1000;
    private const int MaxBatchWaitMs = 100; // Maximum wait time before flushing partial batch

    public HighPerformanceDataService(TradingDbContext context, ITradingLogger logger)
    {
        _context = context;
        _logger = logger;

        // Configure high-throughput channels
        var channelOptions = new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _marketDataChannel = Channel.CreateBounded<MarketDataRecord>(channelOptions);
        _executionChannel = Channel.CreateBounded<ExecutionRecord>(channelOptions);
        _performanceChannel = Channel.CreateBounded<PerformanceMetric>(channelOptions);

        _marketDataWriter = _marketDataChannel.Writer;
        _executionWriter = _executionChannel.Writer;
        _performanceWriter = _performanceChannel.Writer;

        // Start background batch processors
        _ = Task.Run(ProcessMarketDataBatches, _cancellationTokenSource.Token);
        _ = Task.Run(ProcessExecutionBatches, _cancellationTokenSource.Token);
        _ = Task.Run(ProcessPerformanceBatches, _cancellationTokenSource.Token);

        _logger.LogInfo("HighPerformanceDataService initialized with batch processing");
    }

    /// <summary>
    /// Asynchronously inserts market data record with microsecond precision
    /// </summary>
    public async Task<bool> InsertMarketDataAsync(MarketDataRecord record)
    {
        try
        {
            record.HardwareTimestampNs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;
            record.InsertedAt = DateTime.UtcNow;

            await _marketDataWriter.WriteAsync(record, _cancellationTokenSource.Token);
            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to queue market data record: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Asynchronously inserts execution record for regulatory compliance
    /// </summary>
    public async Task<bool> InsertExecutionAsync(ExecutionRecord record)
    {
        try
        {
            record.HardwareTimestampNs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;
            record.InsertedAt = DateTime.UtcNow;

            await _executionWriter.WriteAsync(record, _cancellationTokenSource.Token);
            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to queue execution record: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Asynchronously inserts performance metric
    /// </summary>
    public async Task<bool> InsertPerformanceMetricAsync(PerformanceMetric metric)
    {
        try
        {
            metric.HardwareTimestampNs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;
            metric.InsertedAt = DateTime.UtcNow;

            await _performanceWriter.WriteAsync(metric, _cancellationTokenSource.Token);
            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to queue performance metric: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Retrieves recent market data for symbol with optimized queries
    /// </summary>
    public async Task<List<MarketDataRecord>> GetRecentMarketDataAsync(
        string symbol,
        DateTime fromTime,
        DateTime toTime,
        string? venue = null,
        int limit = 10000)
    {
        try
        {
            var query = _context.MarketData
                .AsNoTracking()
                .Where(md => md.Symbol == symbol &&
                           md.Timestamp >= fromTime &&
                           md.Timestamp <= toTime);

            if (!string.IsNullOrEmpty(venue))
            {
                query = query.Where(md => md.Venue == venue);
            }

            return await query
                .OrderByDescending(md => md.Timestamp)
                .ThenByDescending(md => md.SequenceNumber)
                .Take(limit)
                .ToListAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to retrieve market data for {symbol}: {ex.Message}", ex);
            return new List<MarketDataRecord>();
        }
    }

    /// <summary>
    /// Retrieves execution history for analysis and reporting
    /// </summary>
    public async Task<List<ExecutionRecord>> GetExecutionHistoryAsync(
        string? symbol = null,
        string? account = null,
        DateTime? fromTime = null,
        DateTime? toTime = null,
        int limit = 1000)
    {
        try
        {
            var query = _context.Executions.AsNoTracking();

            if (!string.IsNullOrEmpty(symbol))
                query = query.Where(e => e.Symbol == symbol);

            if (!string.IsNullOrEmpty(account))
                query = query.Where(e => e.Account == account);

            if (fromTime.HasValue)
                query = query.Where(e => e.ExecutionTime >= fromTime.Value);

            if (toTime.HasValue)
                query = query.Where(e => e.ExecutionTime <= toTime.Value);

            return await query
                .OrderByDescending(e => e.ExecutionTime)
                .Take(limit)
                .ToListAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to retrieve execution history: {ex.Message}", ex);
            return new List<ExecutionRecord>();
        }
    }

    /// <summary>
    /// Calculates average latency metrics for performance monitoring
    /// </summary>
    public async Task<Dictionary<string, decimal>> GetLatencyMetricsAsync(
        string category,
        DateTime fromTime,
        DateTime toTime)
    {
        try
        {
            var metrics = await _context.PerformanceMetrics
                .AsNoTracking()
                .Where(pm => pm.Category == category &&
                           pm.Timestamp >= fromTime &&
                           pm.Timestamp <= toTime &&
                           pm.LatencyNs.HasValue)
                .GroupBy(pm => pm.Operation)
                .Select(g => new
                {
                    Operation = g.Key,
                    AvgLatencyNs = g.Average(x => x.LatencyNs!.Value),
                    MinLatencyNs = g.Min(x => x.LatencyNs!.Value),
                    MaxLatencyNs = g.Max(x => x.LatencyNs!.Value),
                    Count = g.Count()
                })
                .ToListAsync(_cancellationTokenSource.Token);

            return metrics.ToDictionary(
                m => m.Operation,
                m => (decimal)(m.AvgLatencyNs / 1000.0) // Convert to microseconds
            );
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to calculate latency metrics: {ex.Message}", ex);
            return new Dictionary<string, decimal>();
        }
    }

    private async Task ProcessMarketDataBatches()
    {
        var batch = new List<MarketDataRecord>(BatchSize);

        try
        {
            await foreach (var record in _marketDataChannel.Reader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                batch.Add(record);

                if (batch.Count >= BatchSize)
                {
                    await FlushMarketDataBatch(batch);
                    batch.Clear();
                }
            }

            // Flush remaining records
            if (batch.Count > 0)
            {
                await FlushMarketDataBatch(batch);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error processing market data batches: {ex.Message}", ex);
        }
    }

    private async Task ProcessExecutionBatches()
    {
        var batch = new List<ExecutionRecord>(BatchSize);

        try
        {
            await foreach (var record in _executionChannel.Reader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                batch.Add(record);

                if (batch.Count >= BatchSize)
                {
                    await FlushExecutionBatch(batch);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await FlushExecutionBatch(batch);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error processing execution batches: {ex.Message}", ex);
        }
    }

    private async Task ProcessPerformanceBatches()
    {
        var batch = new List<PerformanceMetric>(BatchSize);

        try
        {
            await foreach (var record in _performanceChannel.Reader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                batch.Add(record);

                if (batch.Count >= BatchSize)
                {
                    await FlushPerformanceBatch(batch);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await FlushPerformanceBatch(batch);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error processing performance batches: {ex.Message}", ex);
        }
    }

    private async Task FlushMarketDataBatch(List<MarketDataRecord> batch)
    {
        try
        {
            await _context.MarketData.AddRangeAsync(batch, _cancellationTokenSource.Token);
            await _context.SaveChangesAsync(_cancellationTokenSource.Token);

            Interlocked.Add(ref _marketDataInsertCount, batch.Count);
            TradingLogOrchestrator.Instance.LogInfo($"Inserted {batch.Count} market data records");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to flush market data batch: {ex.Message}", ex);
        }
    }

    private async Task FlushExecutionBatch(List<ExecutionRecord> batch)
    {
        try
        {
            await _context.Executions.AddRangeAsync(batch, _cancellationTokenSource.Token);
            await _context.SaveChangesAsync(_cancellationTokenSource.Token);

            Interlocked.Add(ref _executionInsertCount, batch.Count);
            TradingLogOrchestrator.Instance.LogInfo($"Inserted {batch.Count} execution records");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to flush execution batch: {ex.Message}", ex);
        }
    }

    private async Task FlushPerformanceBatch(List<PerformanceMetric> batch)
    {
        try
        {
            await _context.PerformanceMetrics.AddRangeAsync(batch, _cancellationTokenSource.Token);
            await _context.SaveChangesAsync(_cancellationTokenSource.Token);

            Interlocked.Add(ref _performanceInsertCount, batch.Count);
            TradingLogOrchestrator.Instance.LogInfo($"Inserted {batch.Count} performance metrics");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to flush performance batch: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _marketDataWriter.Complete();
        _executionWriter.Complete();
        _performanceWriter.Complete();

        _context.Dispose();
        _cancellationTokenSource.Dispose();

        _logger.LogInfo($"HighPerformanceDataService disposed. Total inserts - Market Data: {_marketDataInsertCount}, Executions: {_executionInsertCount}, Performance: {_performanceInsertCount}");
    }
}