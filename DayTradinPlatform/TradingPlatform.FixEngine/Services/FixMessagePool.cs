using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.FixEngine.Models;

namespace TradingPlatform.FixEngine.Services
{
    /// <summary>
    /// High-performance object pool for FIX messages to achieve zero allocation on critical path.
    /// Implements canonical object pooling pattern for ultra-low latency operations.
    /// </summary>
    /// <remarks>
    /// Pre-allocates FIX messages and byte buffers to eliminate GC pressure.
    /// Thread-safe implementation using lock-free data structures.
    /// </remarks>
    public class FixMessagePool : CanonicalObjectPool<FixMessage>
    {
        private readonly ArrayPool<byte> _byteArrayPool;
        private readonly ConcurrentBag<byte[]> _largeBuffers;
        private readonly int _maxMessageSize;
        private readonly ITradingLogger _logger;
        private long _rentCount;
        private long _returnCount;
        
        /// <summary>
        /// Initializes a new instance of the FixMessagePool class.
        /// </summary>
        /// <param name="logger">Trading logger for diagnostics</param>
        /// <param name="poolSize">Initial pool size (default: 10000)</param>
        /// <param name="maxMessageSize">Maximum FIX message size in bytes (default: 4096)</param>
        public FixMessagePool(
            ITradingLogger logger,
            int poolSize = 10000,
            int maxMessageSize = 4096) 
            : base(logger, "FixMessagePool", poolSize)
        {
            _logger = logger;
            _maxMessageSize = maxMessageSize;
            _byteArrayPool = ArrayPool<byte>.Create(maxMessageSize, poolSize);
            _largeBuffers = new ConcurrentBag<byte[]>();
            
            // Pre-allocate large buffers for ultra-low latency
            for (int i = 0; i < Math.Min(100, poolSize / 10); i++)
            {
                _largeBuffers.Add(new byte[maxMessageSize * 10]);
            }
            
            LogInitialization();
        }
        
        /// <summary>
        /// Creates a new FIX message instance for the pool.
        /// </summary>
        /// <returns>A new FIX message instance</returns>
        protected override FixMessage CreateInstance()
        {
            return new FixMessage
            {
                Fields = new System.Collections.Generic.Dictionary<int, string>(50)
            };
        }
        
        /// <summary>
        /// Resets a FIX message before returning it to the pool.
        /// </summary>
        /// <param name="instance">The FIX message to reset</param>
        protected override void ResetInstance(FixMessage instance)
        {
            LogMethodEntry();
            
            try
            {
                // Return byte array to pool if allocated
                if (instance.RawMessage != null && instance.RawMessage.Length <= _maxMessageSize)
                {
                    _byteArrayPool.Return(instance.RawMessage, clearArray: true);
                }
                
                // Reset all fields
                instance.MessageType = string.Empty;
                instance.SenderCompId = string.Empty;
                instance.TargetCompId = string.Empty;
                instance.SequenceNumber = 0;
                instance.SendingTime = default;
                instance.Fields.Clear();
                instance.RawMessage = null;
                instance.HardwareTimestamp = 0;
                
                LogMethodExit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting FIX message");
                LogMethodExit();
                throw;
            }
        }
        
        /// <summary>
        /// Rents a FIX message from the pool with optional byte buffer.
        /// </summary>
        /// <param name="requiresBuffer">Whether to allocate a byte buffer</param>
        /// <param name="bufferSize">Size of byte buffer if required</param>
        /// <returns>A FIX message ready for use</returns>
        public FixMessage RentWithBuffer(bool requiresBuffer = true, int bufferSize = 0)
        {
            LogMethodEntry();
            
            try
            {
                var message = Rent();
                
                if (requiresBuffer)
                {
                    var size = bufferSize > 0 ? bufferSize : _maxMessageSize;
                    message.RawMessage = _byteArrayPool.Rent(size);
                }
                
                Interlocked.Increment(ref _rentCount);
                
                LogMethodExit();
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renting FIX message with buffer");
                LogMethodExit();
                throw;
            }
        }
        
        /// <summary>
        /// Returns a FIX message to the pool.
        /// </summary>
        /// <param name="message">The message to return</param>
        public override void Return(FixMessage message)
        {
            LogMethodEntry();
            
            try
            {
                base.Return(message);
                Interlocked.Increment(ref _returnCount);
                
                LogMethodExit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error returning FIX message to pool");
                LogMethodExit();
                throw;
            }
        }
        
        /// <summary>
        /// Rents a large buffer for batch operations.
        /// </summary>
        /// <param name="minimumSize">Minimum buffer size required</param>
        /// <returns>A byte array of at least the requested size</returns>
        public byte[] RentLargeBuffer(int minimumSize)
        {
            LogMethodEntry();
            
            try
            {
                if (_largeBuffers.TryTake(out var buffer) && buffer.Length >= minimumSize)
                {
                    LogMethodExit();
                    return buffer;
                }
                
                // Allocate new buffer if none available
                var newBuffer = new byte[Math.Max(minimumSize, _maxMessageSize * 10)];
                
                LogMethodExit();
                return newBuffer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renting large buffer");
                LogMethodExit();
                throw;
            }
        }
        
        /// <summary>
        /// Returns a large buffer to the pool.
        /// </summary>
        /// <param name="buffer">The buffer to return</param>
        public void ReturnLargeBuffer(byte[] buffer)
        {
            LogMethodEntry();
            
            try
            {
                if (buffer != null && buffer.Length >= _maxMessageSize * 10)
                {
                    Array.Clear(buffer, 0, buffer.Length);
                    _largeBuffers.Add(buffer);
                }
                
                LogMethodExit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error returning large buffer");
                LogMethodExit();
            }
        }
        
        /// <summary>
        /// Gets pool statistics for monitoring.
        /// </summary>
        /// <returns>Pool statistics including rent/return counts and utilization</returns>
        public FixMessagePoolStats GetStats()
        {
            LogMethodEntry();
            
            try
            {
                var baseStats = GetStatistics();
                
                var stats = new FixMessagePoolStats
                {
                    TotalRentCount = _rentCount,
                    TotalReturnCount = _returnCount,
                    CurrentPoolSize = baseStats.AvailableCount,
                    TotalCreated = baseStats.TotalCreated,
                    UtilizationPercent = baseStats.UtilizationPercent,
                    LargeBuffersAvailable = _largeBuffers.Count
                };
                
                LogMethodExit();
                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pool statistics");
                LogMethodExit();
                throw;
            }
        }
        
        /// <summary>
        /// Logs pool initialization details.
        /// </summary>
        private void LogInitialization()
        {
            _logger.LogInformation(
                "FIX message pool initialized. Pool size: {PoolSize}, Max message size: {MaxMessageSize}, Large buffers: {LargeBuffers}",
                PoolSize,
                _maxMessageSize,
                _largeBuffers.Count);
        }
    }
    
    /// <summary>
    /// Statistics for FIX message pool monitoring.
    /// </summary>
    public class FixMessagePoolStats
    {
        /// <summary>
        /// Gets or sets total number of messages rented.
        /// </summary>
        public long TotalRentCount { get; set; }
        
        /// <summary>
        /// Gets or sets total number of messages returned.
        /// </summary>
        public long TotalReturnCount { get; set; }
        
        /// <summary>
        /// Gets or sets current number of messages in pool.
        /// </summary>
        public int CurrentPoolSize { get; set; }
        
        /// <summary>
        /// Gets or sets total number of messages created.
        /// </summary>
        public int TotalCreated { get; set; }
        
        /// <summary>
        /// Gets or sets pool utilization percentage.
        /// </summary>
        public decimal UtilizationPercent { get; set; }
        
        /// <summary>
        /// Gets or sets number of large buffers available.
        /// </summary>
        public int LargeBuffersAvailable { get; set; }
    }
}