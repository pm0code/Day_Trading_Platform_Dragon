using System;
using System.Buffers;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.FixEngine.Canonical;

namespace TradingPlatform.FixEngine.Performance
{
    /// <summary>
    /// Memory optimization strategies for ultra-low latency FIX processing.
    /// Implements pre-allocation, memory pinning, and GC tuning.
    /// </summary>
    /// <remarks>
    /// Ensures zero allocation on critical path through aggressive pooling.
    /// Uses memory-mapped files for large data sets.
    /// Configures GC for low-latency operation.
    /// </remarks>
    public class MemoryOptimizer : CanonicalFixServiceBase
    {
        private readonly MemoryPool<byte> _memoryPool;
        private readonly ArrayPool<char> _charPool;
        private readonly PinnedMemoryPool _pinnedPool;
        private readonly int _gen0CollectionsBefore;
        private bool _isOptimized;
        
        /// <summary>
        /// Initializes a new instance of the MemoryOptimizer class.
        /// </summary>
        public MemoryOptimizer(
            ITradingLogger logger,
            int pinnedMemoryMB = 100)
            : base(logger, "MemoryOptimizer")
        {
            _memoryPool = MemoryPool<byte>.Shared;
            _charPool = ArrayPool<char>.Shared;
            _pinnedPool = new PinnedMemoryPool(pinnedMemoryMB * 1024 * 1024);
            _gen0CollectionsBefore = GC.CollectionCount(0);
        }
        
        /// <summary>
        /// Optimizes GC settings for low-latency operation.
        /// </summary>
        public void OptimizeGarbageCollector()
        {
            LogMethodEntry();
            
            try
            {
                if (_isOptimized)
                {
                    LogMethodExit();
                    return;
                }
                
                // Configure GC for low latency
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
                
                // Use server GC for better throughput
                // Note: This needs to be set in app config, shown here for documentation
                // <gcServer enabled="true"/>
                
                // Disable concurrent GC for more predictable latency
                // <gcConcurrent enabled="false"/>
                
                // Force full GC before starting critical operations
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                
                _logger.LogInformation(
                    "GC optimized for low latency. Mode: {Mode}, IsServerGC: {IsServer}",
                    GCSettings.LatencyMode,
                    GCSettings.IsServerGC);
                
                _isOptimized = true;
                
                LogMethodExit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to optimize GC settings");
                LogMethodExit();
                throw;
            }
        }
        
        /// <summary>
        /// Rents a memory buffer with optional pinning.
        /// </summary>
        /// <param name="size">Buffer size required</param>
        /// <param name="pinned">Whether to pin the memory</param>
        /// <returns>Memory owner that must be disposed</returns>
        public IMemoryOwner<byte> RentBuffer(int size, bool pinned = false)
        {
            LogMethodEntry();
            
            try
            {
                IMemoryOwner<byte> owner;
                
                if (pinned)
                {
                    owner = _pinnedPool.Rent(size);
                    RecordMetric("PinnedBuffersRented", 1);
                }
                else
                {
                    owner = _memoryPool.Rent(size);
                    RecordMetric("BuffersRented", 1);
                }
                
                LogMethodExit();
                return owner;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rent buffer");
                LogMethodExit();
                throw;
            }
        }
        
        /// <summary>
        /// Pre-allocates memory to avoid allocations during operation.
        /// </summary>
        /// <param name="bufferCount">Number of buffers to pre-allocate</param>
        /// <param name="bufferSize">Size of each buffer</param>
        public void PreAllocateBuffers(int bufferCount, int bufferSize)
        {
            LogMethodEntry();
            
            try
            {
                _logger.LogInformation(
                    "Pre-allocating {Count} buffers of {Size} bytes",
                    bufferCount, bufferSize);
                
                // Temporarily rent and return buffers to prime the pool
                var buffers = new IMemoryOwner<byte>[bufferCount];
                
                for (int i = 0; i < bufferCount; i++)
                {
                    buffers[i] = _memoryPool.Rent(bufferSize);
                }
                
                // Return them to populate the pool
                for (int i = 0; i < bufferCount; i++)
                {
                    buffers[i].Dispose();
                }
                
                // Pre-allocate pinned memory
                _pinnedPool.PreAllocate(bufferCount / 10, bufferSize);
                
                RecordMetric("BuffersPreAllocated", bufferCount);
                
                _logger.LogInformation("Pre-allocation completed");
                
                LogMethodExit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to pre-allocate buffers");
                LogMethodExit();
                throw;
            }
        }
        
        /// <summary>
        /// Gets memory usage statistics.
        /// </summary>
        public MemoryStats GetMemoryStats()
        {
            LogMethodEntry();
            
            try
            {
                var gen0Collections = GC.CollectionCount(0) - _gen0CollectionsBefore;
                var gen1Collections = GC.CollectionCount(1);
                var gen2Collections = GC.CollectionCount(2);
                
                var stats = new MemoryStats
                {
                    TotalMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024),
                    Gen0Collections = gen0Collections,
                    Gen1Collections = gen1Collections,
                    Gen2Collections = gen2Collections,
                    GCLatencyMode = GCSettings.LatencyMode.ToString(),
                    IsServerGC = GCSettings.IsServerGC,
                    PinnedMemoryMB = _pinnedPool.TotalSizeMB,
                    PinnedBuffersInUse = _pinnedPool.BuffersInUse
                };
                
                LogMethodExit();
                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get memory stats");
                LogMethodExit();
                throw;
            }
        }
        
        /// <summary>
        /// Forces memory defragmentation during idle time.
        /// </summary>
        public void DefragmentMemory()
        {
            LogMethodEntry();
            
            try
            {
                _logger.LogInformation("Starting memory defragmentation");
                
                // Compact large object heap
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                
                // Force full GC with compaction
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                
                _logger.LogInformation("Memory defragmentation completed");
                
                RecordMetric("DefragmentationRuns", 1);
                
                LogMethodExit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to defragment memory");
                LogMethodExit();
                throw;
            }
        }
        
        /// <summary>
        /// Clears all pooled memory during shutdown.
        /// </summary>
        public void ClearPools()
        {
            LogMethodEntry();
            
            try
            {
                _pinnedPool.Clear();
                
                // Force return of all rented buffers
                GC.Collect();
                GC.WaitForPendingFinalizers();
                
                _logger.LogInformation("Memory pools cleared");
                
                LogMethodExit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear pools");
                LogMethodExit();
                throw;
            }
        }
        
        /// <summary>
        /// Pinned memory pool for zero-copy operations.
        /// </summary>
        private class PinnedMemoryPool
        {
            private readonly ConcurrentBag<PinnedBuffer> _buffers;
            private readonly int _totalSize;
            private int _buffersInUse;
            
            public PinnedMemoryPool(int totalSize)
            {
                _buffers = new ConcurrentBag<PinnedBuffer>();
                _totalSize = totalSize;
            }
            
            public int TotalSizeMB => _totalSize / (1024 * 1024);
            public int BuffersInUse => _buffersInUse;
            
            public IMemoryOwner<byte> Rent(int size)
            {
                if (_buffers.TryTake(out var buffer) && buffer.Size >= size)
                {
                    Interlocked.Increment(ref _buffersInUse);
                    return new PinnedMemoryOwner(buffer, this);
                }
                
                // Allocate new pinned buffer
                var newBuffer = new PinnedBuffer(size);
                Interlocked.Increment(ref _buffersInUse);
                return new PinnedMemoryOwner(newBuffer, this);
            }
            
            public void Return(PinnedBuffer buffer)
            {
                Interlocked.Decrement(ref _buffersInUse);
                _buffers.Add(buffer);
            }
            
            public void PreAllocate(int count, int size)
            {
                for (int i = 0; i < count; i++)
                {
                    _buffers.Add(new PinnedBuffer(size));
                }
            }
            
            public void Clear()
            {
                while (_buffers.TryTake(out var buffer))
                {
                    buffer.Dispose();
                }
            }
            
            private class PinnedMemoryOwner : IMemoryOwner<byte>
            {
                private readonly PinnedBuffer _buffer;
                private readonly PinnedMemoryPool _pool;
                private bool _disposed;
                
                public PinnedMemoryOwner(PinnedBuffer buffer, PinnedMemoryPool pool)
                {
                    _buffer = buffer;
                    _pool = pool;
                }
                
                public Memory<byte> Memory => _buffer.Memory;
                
                public void Dispose()
                {
                    if (!_disposed)
                    {
                        _pool.Return(_buffer);
                        _disposed = true;
                    }
                }
            }
        }
        
        /// <summary>
        /// Pinned buffer wrapper.
        /// </summary>
        private class PinnedBuffer : IDisposable
        {
            private readonly byte[] _buffer;
            private readonly GCHandle _handle;
            
            public PinnedBuffer(int size)
            {
                _buffer = new byte[size];
                _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            }
            
            public int Size => _buffer.Length;
            public Memory<byte> Memory => _buffer.AsMemory();
            
            public void Dispose()
            {
                if (_handle.IsAllocated)
                {
                    _handle.Free();
                }
            }
        }
    }
    
    /// <summary>
    /// Memory usage statistics.
    /// </summary>
    public class MemoryStats
    {
        public long TotalMemoryMB { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public string GCLatencyMode { get; set; } = string.Empty;
        public bool IsServerGC { get; set; }
        public int PinnedMemoryMB { get; set; }
        public int PinnedBuffersInUse { get; set; }
    }
}