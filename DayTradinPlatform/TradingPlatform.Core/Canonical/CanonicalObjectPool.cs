using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical base class for high-performance object pooling.
    /// Implements thread-safe object pooling with metrics tracking.
    /// </summary>
    /// <typeparam name="T">The type of objects to pool</typeparam>
    public abstract class CanonicalObjectPool<T> : CanonicalServiceBase where T : class
    {
        private readonly ConcurrentBag<T> _pool;
        private readonly Func<T> _objectFactory;
        private readonly Action<T>? _resetAction;
        private readonly int _maxPoolSize;
        private long _rentCount;
        private long _returnCount;
        private long _createCount;

        /// <summary>
        /// Initializes a new instance of the CanonicalObjectPool class.
        /// </summary>
        /// <param name="logger">The trading logger</param>
        /// <param name="serviceName">The name of this pool service</param>
        /// <param name="maxPoolSize">Maximum number of objects to pool</param>
        protected CanonicalObjectPool(
            ITradingLogger logger,
            string serviceName,
            int maxPoolSize = 1000) : base(logger, serviceName)
        {
            _pool = new ConcurrentBag<T>();
            _maxPoolSize = maxPoolSize;
            _objectFactory = CreateObject;
            _resetAction = ResetObject;
        }

        /// <summary>
        /// Creates a new object instance for the pool.
        /// Must be implemented by derived classes.
        /// </summary>
        /// <returns>A new instance of T</returns>
        protected abstract T CreateObject();

        /// <summary>
        /// Resets an object before returning it to the pool.
        /// Override in derived classes if objects need resetting.
        /// </summary>
        /// <param name="obj">The object to reset</param>
        protected virtual void ResetObject(T obj)
        {
            // Default implementation does nothing
            // Override in derived classes if needed
        }

        /// <summary>
        /// Rents an object from the pool or creates a new one if none available.
        /// </summary>
        /// <returns>An object instance</returns>
        public T Rent()
        {
            LogMethodEntry();
            try
            {
                Interlocked.Increment(ref _rentCount);
                
                if (_pool.TryTake(out var item))
                {
                    LogMethodExit();
                    return item;
                }

                // Pool is empty, create a new object
                Interlocked.Increment(ref _createCount);
                var newItem = _objectFactory();
                
                UpdateMetric("ObjectsCreated", 1);
                LogMethodExit();
                return newItem;
            }
            catch (Exception ex)
            {
                LogError("Failed to rent object from pool", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Returns an object to the pool for reuse.
        /// </summary>
        /// <param name="obj">The object to return</param>
        public void Return(T obj)
        {
            LogMethodEntry();
            try
            {
                if (obj == null)
                {
                    LogWarning("Attempted to return null object to pool");
                    LogMethodExit();
                    return;
                }

                Interlocked.Increment(ref _returnCount);

                // Reset the object before returning to pool
                _resetAction?.Invoke(obj);

                // Only add to pool if under max size
                if (_pool.Count < _maxPoolSize)
                {
                    _pool.Add(obj);
                    UpdateMetric("ObjectsPooled", 1);
                }
                else
                {
                    // Pool is full, let GC collect the object
                    UpdateMetric("ObjectsDiscarded", 1);
                }

                LogMethodExit();
            }
            catch (Exception ex)
            {
                LogError("Failed to return object to pool", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Gets the current pool statistics.
        /// </summary>
        /// <returns>Pool statistics</returns>
        public (int PoolSize, long RentCount, long ReturnCount, long CreateCount) GetStats()
        {
            LogMethodEntry();
            try
            {
                var stats = (_pool.Count, 
                    Interlocked.Read(ref _rentCount), 
                    Interlocked.Read(ref _returnCount),
                    Interlocked.Read(ref _createCount));
                
                LogMethodExit();
                return stats;
            }
            catch (Exception ex)
            {
                LogError("Failed to get pool stats", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Clears all objects from the pool.
        /// </summary>
        public void Clear()
        {
            LogMethodEntry();
            try
            {
                while (_pool.TryTake(out _))
                {
                    // Drain the pool
                }
                
                LogInfo("Object pool cleared");
                UpdateMetric("PoolCleared", 1);
                LogMethodExit();
            }
            catch (Exception ex)
            {
                LogError("Failed to clear pool", ex);
                LogMethodExit();
                throw;
            }
        }

        protected override async Task<TradingResult<bool>> OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            try
            {
                LogInfo($"Initializing object pool with max size: {_maxPoolSize}");
                
                // Pre-populate the pool
                var prePopulateCount = Math.Min(10, _maxPoolSize / 10);
                for (int i = 0; i < prePopulateCount; i++)
                {
                    _pool.Add(_objectFactory());
                }
                
                LogInfo($"Pre-populated pool with {prePopulateCount} objects");
                LogMethodExit();
                return TradingResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                LogError("Failed to initialize object pool", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("INIT_FAILED", "Failed to initialize object pool", ex);
            }
        }

        protected override async Task<TradingResult<bool>> OnStartAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            try
            {
                LogInfo("Starting object pool service");
                UpdateMetric("PoolStarted", 1);
                LogMethodExit();
                return TradingResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                LogError("Failed to start object pool", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("START_FAILED", "Failed to start object pool", ex);
            }
        }

        protected override async Task<TradingResult<bool>> OnStopAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            try
            {
                var (poolSize, rentCount, returnCount, createCount) = GetStats();
                LogInfo($"Stopping object pool - Pool size: {poolSize}, Rented: {rentCount}, Returned: {returnCount}, Created: {createCount}");
                
                Clear();
                UpdateMetric("PoolStopped", 1);
                LogMethodExit();
                return TradingResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                LogError("Failed to stop object pool", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("STOP_FAILED", "Failed to stop object pool", ex);
            }
        }
    }
}