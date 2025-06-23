using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace TradingPlatform.Core.Performance;

/// <summary>
/// Memory optimization manager for ultra-low latency trading operations
/// Implements object pooling, buffer management, and cache locality optimizations
/// Target: Minimize GC pressure and memory allocations in hot trading paths
/// </summary>
public sealed class MemoryOptimizer : IDisposable
{
    private readonly ConcurrentQueue<StringBuilder> _stringBuilderPool = new();
    private readonly ConcurrentQueue<byte[]> _smallBufferPool = new();
    private readonly ConcurrentQueue<byte[]> _mediumBufferPool = new();
    private readonly ConcurrentQueue<byte[]> _largeBufferPool = new();
    private readonly ArrayPool<byte> _arrayPool;
    private readonly ArrayPool<char> _charPool;
    private readonly Timer _cleanupTimer;
    private readonly object _lock = new();

    // Buffer size constants optimized for trading operations
    private const int SMALL_BUFFER_SIZE = 1024;     // FIX messages, small data
    private const int MEDIUM_BUFFER_SIZE = 4096;    // Market data snapshots
    private const int LARGE_BUFFER_SIZE = 16384;    // Bulk data operations
    private const int MAX_POOL_SIZE = 100;          // Maximum objects per pool

    // Pre-allocated trading-specific objects
    private readonly ConcurrentQueue<TradingMessageBuffer> _messageBufferPool = new();
    private readonly ConcurrentQueue<MarketDataBuffer> _marketDataBufferPool = new();
    private readonly ConcurrentQueue<OrderBuffer> _orderBufferPool = new();

    private static readonly Lazy<MemoryOptimizer> _instance = new(() => new MemoryOptimizer());
    public static MemoryOptimizer Instance => _instance.Value;

    private bool _disposed;

    private MemoryOptimizer()
    {
        _arrayPool = ArrayPool<byte>.Create(LARGE_BUFFER_SIZE * 2, MAX_POOL_SIZE);
        _charPool = ArrayPool<char>.Create(LARGE_BUFFER_SIZE, MAX_POOL_SIZE);

        // Initialize pools with pre-allocated objects
        InitializePools();

        // Cleanup timer to prevent memory leaks
        _cleanupTimer = new Timer(PerformCleanup, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        // Configure runtime for low-latency scenarios
        ConfigureRuntime();
    }

    /// <summary>
    /// Gets a pooled StringBuilder optimized for FIX message construction
    /// </summary>
    public StringBuilder GetStringBuilder()
    {
        if (_stringBuilderPool.TryDequeue(out var sb))
        {
            sb.Clear();
            return sb;
        }

        return new StringBuilder(2048); // Optimal size for FIX messages
    }

    /// <summary>
    /// Returns a StringBuilder to the pool for reuse
    /// </summary>
    public void ReturnStringBuilder(StringBuilder stringBuilder)
    {
        if (stringBuilder.Capacity <= 8192 && _stringBuilderPool.Count < MAX_POOL_SIZE)
        {
            stringBuilder.Clear();
            _stringBuilderPool.Enqueue(stringBuilder);
        }
    }

    /// <summary>
    /// Gets a pooled byte buffer for the specified size category
    /// </summary>
    public byte[] GetBuffer(BufferSize size)
    {
        var pool = size switch
        {
            BufferSize.Small => _smallBufferPool,
            BufferSize.Medium => _mediumBufferPool,
            BufferSize.Large => _largeBufferPool,
            _ => _mediumBufferPool
        };

        if (pool.TryDequeue(out var buffer))
        {
            Array.Clear(buffer, 0, buffer.Length);
            return buffer;
        }

        var bufferSize = size switch
        {
            BufferSize.Small => SMALL_BUFFER_SIZE,
            BufferSize.Medium => MEDIUM_BUFFER_SIZE,
            BufferSize.Large => LARGE_BUFFER_SIZE,
            _ => MEDIUM_BUFFER_SIZE
        };

        return new byte[bufferSize];
    }

    /// <summary>
    /// Returns a byte buffer to the appropriate pool for reuse
    /// </summary>
    public void ReturnBuffer(byte[] buffer)
    {
        if (buffer == null) return;

        var pool = buffer.Length switch
        {
            SMALL_BUFFER_SIZE => _smallBufferPool,
            MEDIUM_BUFFER_SIZE => _mediumBufferPool,
            LARGE_BUFFER_SIZE => _largeBufferPool,
            _ => null
        };

        if (pool != null && pool.Count < MAX_POOL_SIZE)
        {
            pool.Enqueue(buffer);
        }
    }

    /// <summary>
    /// Gets a pooled array from the system ArrayPool
    /// </summary>
    public byte[] RentArray(int minimumLength)
    {
        return _arrayPool.Rent(minimumLength);
    }

    /// <summary>
    /// Returns an array to the system ArrayPool
    /// </summary>
    public void ReturnArray(byte[] array, bool clearArray = true)
    {
        _arrayPool.Return(array, clearArray);
    }

    /// <summary>
    /// Gets a pooled char array for string operations
    /// </summary>
    public char[] RentCharArray(int minimumLength)
    {
        return _charPool.Rent(minimumLength);
    }

    /// <summary>
    /// Returns a char array to the pool
    /// </summary>
    public void ReturnCharArray(char[] array, bool clearArray = true)
    {
        _charPool.Return(array, clearArray);
    }

    /// <summary>
    /// Gets a specialized trading message buffer
    /// </summary>
    public TradingMessageBuffer GetTradingMessageBuffer()
    {
        if (_messageBufferPool.TryDequeue(out var buffer))
        {
            buffer.Reset();
            return buffer;
        }

        return new TradingMessageBuffer();
    }

    /// <summary>
    /// Returns a trading message buffer to the pool
    /// </summary>
    public void ReturnTradingMessageBuffer(TradingMessageBuffer buffer)
    {
        if (_messageBufferPool.Count < MAX_POOL_SIZE)
        {
            buffer.Reset();
            _messageBufferPool.Enqueue(buffer);
        }
    }

    /// <summary>
    /// Gets a specialized market data buffer
    /// </summary>
    public MarketDataBuffer GetMarketDataBuffer()
    {
        if (_marketDataBufferPool.TryDequeue(out var buffer))
        {
            buffer.Reset();
            return buffer;
        }

        return new MarketDataBuffer();
    }

    /// <summary>
    /// Returns a market data buffer to the pool
    /// </summary>
    public void ReturnMarketDataBuffer(MarketDataBuffer buffer)
    {
        if (_marketDataBufferPool.Count < MAX_POOL_SIZE)
        {
            buffer.Reset();
            _marketDataBufferPool.Enqueue(buffer);
        }
    }

    /// <summary>
    /// Gets a specialized order buffer
    /// </summary>
    public OrderBuffer GetOrderBuffer()
    {
        if (_orderBufferPool.TryDequeue(out var buffer))
        {
            buffer.Reset();
            return buffer;
        }

        return new OrderBuffer();
    }

    /// <summary>
    /// Returns an order buffer to the pool
    /// </summary>
    public void ReturnOrderBuffer(OrderBuffer buffer)
    {
        if (_orderBufferPool.Count < MAX_POOL_SIZE)
        {
            buffer.Reset();
            _orderBufferPool.Enqueue(buffer);
        }
    }

    /// <summary>
    /// Forces immediate garbage collection and optimization
    /// Should be called during non-trading hours or low-activity periods
    /// </summary>
    public void ForceOptimization()
    {
        // Compact the large object heap
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

        // Force full collection and compaction
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        // Pre-allocate critical objects after cleanup
        PreAllocateCriticalObjects();
    }

    /// <summary>
    /// Gets memory usage statistics for monitoring
    /// </summary>
    public MemoryStatistics GetMemoryStatistics()
    {
        return new MemoryStatistics
        {
            TotalMemoryUsed = GC.GetTotalMemory(false),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            StringBuilderPoolSize = _stringBuilderPool.Count,
            SmallBufferPoolSize = _smallBufferPool.Count,
            MediumBufferPoolSize = _mediumBufferPool.Count,
            LargeBufferPoolSize = _largeBufferPool.Count,
            TradingMessageBufferPoolSize = _messageBufferPool.Count,
            MarketDataBufferPoolSize = _marketDataBufferPool.Count,
            OrderBufferPoolSize = _orderBufferPool.Count
        };
    }

    /// <summary>
    /// Pins memory for critical trading data structures to prevent GC movement
    /// </summary>
    public unsafe GCHandle PinMemory(object obj)
    {
        return GCHandle.Alloc(obj, GCHandleType.Pinned);
    }

    /// <summary>
    /// Unpins previously pinned memory
    /// </summary>
    public void UnpinMemory(GCHandle handle)
    {
        if (handle.IsAllocated)
        {
            handle.Free();
        }
    }

    private void InitializePools()
    {
        // Pre-populate pools with initial objects
        for (int i = 0; i < 20; i++)
        {
            _stringBuilderPool.Enqueue(new StringBuilder(2048));
            _smallBufferPool.Enqueue(new byte[SMALL_BUFFER_SIZE]);
            _mediumBufferPool.Enqueue(new byte[MEDIUM_BUFFER_SIZE]);
            _largeBufferPool.Enqueue(new byte[LARGE_BUFFER_SIZE]);
            _messageBufferPool.Enqueue(new TradingMessageBuffer());
            _marketDataBufferPool.Enqueue(new MarketDataBuffer());
            _orderBufferPool.Enqueue(new OrderBuffer());
        }
    }

    private void ConfigureRuntime()
    {
        // Configure GC for sustained low latency
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        // Disable concurrent GC for more predictable timing
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Platform-specific optimizations could go here
        }

        // Suggest server GC mode for better performance on multi-core systems
        if (!GCSettings.IsServerGC)
        {
            // Note: Server GC must be configured at application startup
            // This is just for monitoring purposes
        }
    }

    private void PreAllocateCriticalObjects()
    {
        // Pre-allocate frequently used objects to avoid allocation during trading
        var warmupOperations = new Action[]
        {
            () => { var sb = GetStringBuilder(); ReturnStringBuilder(sb); },
            () => { var buf = GetBuffer(BufferSize.Medium); ReturnBuffer(buf); },
            () => { var arr = RentArray(1024); ReturnArray(arr); },
            () => { var msg = GetTradingMessageBuffer(); ReturnTradingMessageBuffer(msg); },
            () => { var md = GetMarketDataBuffer(); ReturnMarketDataBuffer(md); },
            () => { var ord = GetOrderBuffer(); ReturnOrderBuffer(ord); }
        };

        foreach (var operation in warmupOperations)
        {
            try
            {
                operation();
            }
            catch
            {
                // Ignore warmup failures
            }
        }
    }

    private void PerformCleanup(object? state)
    {
        try
        {
            // Trim pools if they grow too large
            TrimPool(_stringBuilderPool, MAX_POOL_SIZE / 2);
            TrimPool(_smallBufferPool, MAX_POOL_SIZE / 2);
            TrimPool(_mediumBufferPool, MAX_POOL_SIZE / 2);
            TrimPool(_largeBufferPool, MAX_POOL_SIZE / 2);
            TrimPool(_messageBufferPool, MAX_POOL_SIZE / 2);
            TrimPool(_marketDataBufferPool, MAX_POOL_SIZE / 2);
            TrimPool(_orderBufferPool, MAX_POOL_SIZE / 2);

            // Suggest minor collection during low activity
            if (DateTime.UtcNow.Hour >= 20 || DateTime.UtcNow.Hour <= 6) // Outside trading hours
            {
                GC.Collect(0, GCCollectionMode.Optimized, blocking: false);
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }

    private static void TrimPool<T>(ConcurrentQueue<T> pool, int targetSize)
    {
        while (pool.Count > targetSize && pool.TryDequeue(out _))
        {
            // Remove excess items
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cleanupTimer?.Dispose();
        // ArrayPool instances are singleton and don't need disposal

        // Clear all pools
        while (_stringBuilderPool.TryDequeue(out _)) { }
        while (_smallBufferPool.TryDequeue(out _)) { }
        while (_mediumBufferPool.TryDequeue(out _)) { }
        while (_largeBufferPool.TryDequeue(out _)) { }
        while (_messageBufferPool.TryDequeue(out _)) { }
        while (_marketDataBufferPool.TryDequeue(out _)) { }
        while (_orderBufferPool.TryDequeue(out _)) { }

        _disposed = true;
    }
}

/// <summary>
/// Buffer size categories for optimal memory allocation
/// </summary>
public enum BufferSize
{
    Small,   // 1KB - FIX messages, small data
    Medium,  // 4KB - Market data snapshots
    Large    // 16KB - Bulk operations
}

/// <summary>
/// Memory usage statistics for monitoring and optimization
/// </summary>
public class MemoryStatistics
{
    public long TotalMemoryUsed { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public int StringBuilderPoolSize { get; set; }
    public int SmallBufferPoolSize { get; set; }
    public int MediumBufferPoolSize { get; set; }
    public int LargeBufferPoolSize { get; set; }
    public int TradingMessageBufferPoolSize { get; set; }
    public int MarketDataBufferPoolSize { get; set; }
    public int OrderBufferPoolSize { get; set; }

    public double MemoryUsageMB => TotalMemoryUsed / (1024.0 * 1024.0);
    public int TotalCollections => Gen0Collections + Gen1Collections + Gen2Collections;
}

/// <summary>
/// Specialized buffer for trading message construction
/// </summary>
public sealed class TradingMessageBuffer
{
    private readonly StringBuilder _builder = new(4096);
    private readonly Dictionary<int, string> _fields = new();

    public void SetField(int tag, string value)
    {
        _fields[tag] = value;
    }

    public void SetField(int tag, decimal value)
    {
        _fields[tag] = value.ToString("F8");
    }

    public void SetField(int tag, int value)
    {
        _fields[tag] = value.ToString();
    }

    public string BuildMessage()
    {
        _builder.Clear();

        foreach (var kvp in _fields.OrderBy(x => x.Key))
        {
            _builder.Append(kvp.Key).Append('=').Append(kvp.Value).Append('\x01');
        }

        return _builder.ToString();
    }

    public void Reset()
    {
        _builder.Clear();
        _fields.Clear();
    }
}

/// <summary>
/// Specialized buffer for market data operations
/// </summary>
public sealed class MarketDataBuffer
{
    public string Symbol { get; set; } = "";
    public decimal BidPrice { get; set; }
    public decimal BidSize { get; set; }
    public decimal AskPrice { get; set; }
    public decimal AskSize { get; set; }
    public decimal LastPrice { get; set; }
    public decimal LastSize { get; set; }
    public long Timestamp { get; set; }

    public void Reset()
    {
        Symbol = "";
        BidPrice = 0;
        BidSize = 0;
        AskPrice = 0;
        AskSize = 0;
        LastPrice = 0;
        LastSize = 0;
        Timestamp = 0;
    }
}

/// <summary>
/// Specialized buffer for order operations
/// </summary>
public sealed class OrderBuffer
{
    public string Symbol { get; set; } = "";
    public string Side { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public string OrderType { get; set; } = "";
    public string TimeInForce { get; set; } = "";
    public long Timestamp { get; set; }

    public void Reset()
    {
        Symbol = "";
        Side = "";
        Quantity = 0;
        Price = 0;
        OrderType = "";
        TimeInForce = "";
        Timestamp = 0;
    }
}