using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace TradingPlatform.Core.Performance
{
    /// <summary>
    /// High-performance object pool for reducing GC pressure in hot paths
    /// </summary>
    public sealed class HighPerformancePool<T> where T : class
    {
        private readonly ConcurrentBag<T> _pool = new();
        private readonly Func<T> _factory;
        private readonly Action<T>? _reset;
        private readonly int _maxSize;
        private int _currentSize;

        public HighPerformancePool(Func<T> factory, Action<T>? reset = null, int maxSize = 1000)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _reset = reset;
            _maxSize = maxSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Rent()
        {
            if (_pool.TryTake(out var item))
            {
                return item;
            }

            // Create new if under limit
            if (Interlocked.Increment(ref _currentSize) <= _maxSize)
            {
                return _factory();
            }

            // Over limit, decrease count and create anyway
            Interlocked.Decrement(ref _currentSize);
            return _factory();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(T item)
        {
            if (item == null) return;

            _reset?.Invoke(item);

            if (_pool.Count < _maxSize)
            {
                _pool.Add(item);
            }
            else
            {
                // Pool is full, let GC handle it
                Interlocked.Decrement(ref _currentSize);
            }
        }

        public int Count => _pool.Count;
        public int TotalCreated => _currentSize;
    }

    /// <summary>
    /// Pooled wrapper for automatic return to pool
    /// </summary>
    public readonly struct PooledObject<T> : IDisposable where T : class
    {
        private readonly HighPerformancePool<T> _pool;
        public readonly T Value;

        public PooledObject(HighPerformancePool<T> pool, T value)
        {
            _pool = pool;
            Value = value;
        }

        public void Dispose()
        {
            _pool?.Return(Value);
        }
    }

    /// <summary>
    /// Extension methods for easy pool usage
    /// </summary>
    public static class PoolExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledObject<T> RentPooled<T>(this HighPerformancePool<T> pool) where T : class
        {
            return new PooledObject<T>(pool, pool.Rent());
        }
    }
}