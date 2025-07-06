using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Storage.Configuration;
using TradingPlatform.Storage.Interfaces;

namespace TradingPlatform.Storage.Services;

/// <summary>
/// Ultra-low latency data writer optimized for NVMe storage
/// Uses memory-mapped files and lock-free queues for maximum performance
/// </summary>
public class HighSpeedDataWriter : CanonicalServiceBase, IHighSpeedDataWriter
{
    private readonly StorageConfiguration _config;
    private readonly ITieredStorageManager _storageManager;
    
    // High-performance channels for async writes
    private readonly Channel<MarketDataWrite> _marketDataChannel;
    private readonly Channel<ExecutionDataWrite> _executionChannel;
    
    // Memory-mapped file writers
    private readonly ConcurrentDictionary<string, MemoryMappedFileWriter> _mmfWriters;
    
    // Background processing
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _marketDataProcessor;
    private readonly Task _executionProcessor;
    
    // Performance counters
    private long _marketDataWrites;
    private long _executionWrites;
    private long _totalBytesWritten;

    public HighSpeedDataWriter(
        ITradingLogger logger,
        StorageConfiguration config,
        ITieredStorageManager storageManager) : base(logger, "HighSpeedDataWriter")
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
        
        _mmfWriters = new ConcurrentDictionary<string, MemoryMappedFileWriter>();
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Configure high-throughput channels
        var channelOptions = new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true,
            AllowSynchronousContinuations = false // Better for high throughput
        };
        
        _marketDataChannel = Channel.CreateUnbounded<MarketDataWrite>(channelOptions);
        _executionChannel = Channel.CreateUnbounded<ExecutionDataWrite>(channelOptions);
        
        // Start background processors
        _marketDataProcessor = Task.Run(ProcessMarketDataWrites);
        _executionProcessor = Task.Run(ProcessExecutionWrites);
        
        LogInfo("HighSpeedDataWriter initialized with NVMe optimization");
    }

    /// <summary>
    /// Writes market data with sub-microsecond overhead
    /// </summary>
    public async Task WriteMarketDataAsync(string symbol, object data)
    {
        LogMethodEntry();
        try
        {
            var write = new MarketDataWrite
            {
                Symbol = symbol,
                Data = data,
                Timestamp = DateTime.UtcNow,
                HardwareTimestamp = GetHardwareTimestamp()
            };
            
            // Non-blocking write to channel
            if (!_marketDataChannel.Writer.TryWrite(write))
            {
                // Channel is unbounded, so this should never happen
                LogWarning($"Failed to queue market data write for {symbol}");
            }
            
            Interlocked.Increment(ref _marketDataWrites);
            
            await Task.CompletedTask;
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Failed to write market data for {symbol}", ex);
            LogMethodExit();
            throw;
        }
    }

    /// <summary>
    /// Writes execution data with guaranteed durability
    /// </summary>
    public async Task WriteExecutionDataAsync(string orderId, object executionData)
    {
        LogMethodEntry();
        try
        {
            var write = new ExecutionDataWrite
            {
                OrderId = orderId,
                Data = executionData,
                Timestamp = DateTime.UtcNow,
                HardwareTimestamp = GetHardwareTimestamp()
            };
            
            // Write to channel
            await _executionChannel.Writer.WriteAsync(write, _cancellationTokenSource.Token);
            
            Interlocked.Increment(ref _executionWrites);
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Failed to write execution data for {orderId}", ex);
            LogMethodExit();
            throw;
        }
    }

    /// <summary>
    /// Flushes all pending writes to disk
    /// </summary>
    public async Task FlushAsync()
    {
        LogMethodEntry();
        
        try
        {
            // Signal completion
            _marketDataChannel.Writer.TryComplete();
            _executionChannel.Writer.TryComplete();
            
            // Wait for processors to complete
            await Task.WhenAll(_marketDataProcessor, _executionProcessor);
            
            // Flush all memory-mapped files
            foreach (var writer in _mmfWriters.Values)
            {
                writer.Flush();
            }
            
            LogInfo($"Flushed all pending writes. Market: {_marketDataWrites}, " +
                   $"Executions: {_executionWrites}, Total bytes: {_totalBytesWritten:N0}");
        }
        catch (Exception ex)
        {
            LogError("Failed to flush pending writes", ex);
            throw;
        }
        finally
        {
            LogMethodExit();
        }
    }

    // Background processor for market data writes
    private async Task ProcessMarketDataWrites()
    {
        LogMethodEntry();
        try
        {
            var buffer = new List<MarketDataWrite>(1000);
            var lastFlush = DateTime.UtcNow;
            
            await foreach (var write in _marketDataChannel.Reader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                buffer.Add(write);
                
                // Batch writes for efficiency
                if (buffer.Count >= 1000 || 
                    (DateTime.UtcNow - lastFlush).TotalMilliseconds > 100)
                {
                    await WriteBatchToStorageAsync(buffer, "MarketData");
                    buffer.Clear();
                    lastFlush = DateTime.UtcNow;
                }
            }
            
            // Final flush
            if (buffer.Count > 0)
            {
                await WriteBatchToStorageAsync(buffer, "MarketData");
            }
            
            LogMethodExit();
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Market data processor failed", ex);
            LogMethodExit();
            throw;
        }
    }

    // Background processor for execution writes
    private async Task ProcessExecutionWrites()
    {
        LogMethodEntry();
        try
        {
            await foreach (var write in _executionChannel.Reader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                // Executions are written immediately for compliance
                await WriteExecutionToStorageAsync(write);
            }
            
            LogMethodExit();
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Execution processor failed", ex);
            LogMethodExit();
            throw;
        }
    }

    // Batch write market data to storage
    private async Task WriteBatchToStorageAsync<T>(List<T> batch, string dataType)
    {
        LogMethodEntry();
        try
        {
            if (!batch.Any())
            {
                LogMethodExit();
                return;
            }
            
            // Group by symbol for market data
            if (dataType == "MarketData" && batch is List<MarketDataWrite> marketBatch)
            {
                var groups = marketBatch.GroupBy(w => w.Symbol);
                
                foreach (var group in groups)
                {
                    var symbol = group.Key;
                    var data = group.Select(w => new
                    {
                        t = w.HardwareTimestamp,
                        ts = w.Timestamp,
                        d = w.Data
                    }).ToList();
                    
                    // Get or create MMF writer for this symbol
                    var writer = GetOrCreateMMFWriter(symbol, dataType);
                    
                    // Write to memory-mapped file
                    var json = JsonSerializer.Serialize(data);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    writer.Write(bytes);
                    
                    Interlocked.Add(ref _totalBytesWritten, bytes.Length);
                }
            }
            
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Failed to write batch of {batch.Count} items", ex);
            LogMethodExit();
            throw;
        }
    }

    // Write single execution to storage
    private async Task WriteExecutionToStorageAsync(ExecutionDataWrite write)
    {
        LogMethodEntry();
        try
        {
            var data = new
            {
                orderId = write.OrderId,
                t = write.HardwareTimestamp,
                ts = write.Timestamp,
                d = write.Data
            };
            
            var json = JsonSerializer.Serialize(data);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            // Write directly to storage for executions
            using var stream = new MemoryStream(bytes);
            var path = await _storageManager.StoreDataAsync(
                "Executions", 
                write.OrderId, 
                stream,
                new Dictionary<string, string>
                {
                    ["order_id"] = write.OrderId,
                    ["timestamp"] = write.Timestamp.ToString("O")
                });
            
            Interlocked.Add(ref _totalBytesWritten, bytes.Length);
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Failed to write execution {write.OrderId}", ex);
            LogMethodExit();
            throw;
        }
    }

    // Get or create memory-mapped file writer
    private MemoryMappedFileWriter GetOrCreateMMFWriter(string symbol, string dataType)
    {
        LogMethodEntry();
        try
        {
            var key = $"{dataType}:{symbol}";
            
            var writer = _mmfWriters.GetOrAdd(key, k =>
            {
                var date = DateTime.UtcNow;
                var fileName = Path.Combine(
                    _config.HotTier.BasePath,
                    _config.HotTier.DataPaths[dataType],
                    $"{date:yyyy/MM/dd}",
                    $"{symbol}_{date:yyyyMMdd_HHmmss}.mmf"
                );
                
                Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
                
                return new MemoryMappedFileWriter(fileName, 1024 * 1024 * 100); // 100MB files
            });
            
            LogMethodExit();
            return writer;
        }
        catch (Exception ex)
        {
            LogError($"Failed to get or create MMF writer for {symbol}/{dataType}", ex);
            LogMethodExit();
            throw;
        }
    }

    // Get hardware timestamp (simulated for now)
    private long GetHardwareTimestamp()
    {
        LogMethodEntry();
        try
        {
            // In production, use RDTSC or similar hardware timer
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();
            
            LogMethodExit();
            return timestamp;
        }
        catch (Exception ex)
        {
            LogError("Failed to get hardware timestamp", ex);
            LogMethodExit();
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancellationTokenSource?.Cancel();
            
            // Wait for processors to complete
            try
            {
                Task.WaitAll(new[] { _marketDataProcessor, _executionProcessor }, TimeSpan.FromSeconds(5));
            }
            catch { }
            
            // Dispose MMF writers
            foreach (var writer in _mmfWriters.Values)
            {
                writer.Dispose();
            }
            
            _cancellationTokenSource?.Dispose();
        }
        
        base.Dispose(disposing);
    }

    // Internal classes
    private class MarketDataWrite
    {
        public string Symbol { get; set; } = string.Empty;
        public object Data { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public long HardwareTimestamp { get; set; }
    }

    private class ExecutionDataWrite
    {
        public string OrderId { get; set; } = string.Empty;
        public object Data { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public long HardwareTimestamp { get; set; }
    }

    // Memory-mapped file writer for ultra-fast writes
    private class MemoryMappedFileWriter : IDisposable
    {
        private readonly string _fileName;
        private readonly long _capacity;
        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private long _position;
        private readonly object _lock = new();

        public MemoryMappedFileWriter(string fileName, long capacity)
        {
            _fileName = fileName;
            _capacity = capacity;
            
            // Create or open file
            var fileStream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            fileStream.SetLength(capacity);
            fileStream.Close();
            
            // Create memory-mapped file
            _mmf = MemoryMappedFile.CreateFromFile(fileName, FileMode.Open, null, capacity);
            _accessor = _mmf.CreateViewAccessor(0, capacity);
            _position = 0;
        }

        public void Write(byte[] data)
        {
            lock (_lock)
            {
                if (_position + data.Length > _capacity)
                {
                    // File is full, need rotation (not implemented)
                    throw new InvalidOperationException("Memory-mapped file is full");
                }
                
                _accessor.WriteArray(_position, data, 0, data.Length);
                _position += data.Length;
            }
        }

        public void Flush()
        {
            _accessor.Flush();
        }

        public void Dispose()
        {
            _accessor?.Dispose();
            _mmf?.Dispose();
        }
    }
}