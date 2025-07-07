using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TradingPlatform.Core.Performance
{
    /// <summary>
    /// Memory optimization utilities for high-performance scenarios
    /// </summary>
    public static class MemoryOptimizations
    {
        /// <summary>
        /// Rent a buffer from the shared array pool
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] RentArray<T>(int minimumLength)
        {
            return ArrayPool<T>.Shared.Rent(minimumLength);
        }

        /// <summary>
        /// Return a buffer to the shared array pool
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnArray<T>(T[] array, bool clearArray = false)
        {
            ArrayPool<T>.Shared.Return(array, clearArray);
        }

        /// <summary>
        /// Pin memory to prevent GC movement
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryHandle PinMemory<T>(Memory<T> memory)
        {
            return memory.Pin();
        }

        /// <summary>
        /// Allocate unmanaged memory for ultra-low latency scenarios
        /// </summary>
        public static unsafe UnmanagedBuffer<T> AllocateUnmanaged<T>(int count) where T : unmanaged
        {
            var size = sizeof(T) * count;
            var ptr = Marshal.AllocHGlobal(size);
            return new UnmanagedBuffer<T>(ptr, count);
        }
    }

    /// <summary>
    /// Wrapper for unmanaged memory with automatic cleanup
    /// </summary>
    public unsafe struct UnmanagedBuffer<T> : IDisposable where T : unmanaged
    {
        private IntPtr _ptr;
        private readonly int _count;

        internal UnmanagedBuffer(IntPtr ptr, int count)
        {
            _ptr = ptr;
            _count = count;
        }

        public Span<T> AsSpan()
        {
            return new Span<T>(_ptr.ToPointer(), _count);
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)_count)
                    throw new IndexOutOfRangeException();
                
                return ref ((T*)_ptr)[index];
            }
        }

        public void Dispose()
        {
            if (_ptr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_ptr);
                _ptr = IntPtr.Zero;
            }
        }
    }

    /// <summary>
    /// Stack-allocated buffer for small temporary data
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public ref struct StackBuffer256
    {
        private Span<byte> _buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StackBuffer256(Span<byte> buffer)
        {
            if (buffer.Length < 256)
                throw new ArgumentException("Buffer must be at least 256 bytes");
            
            _buffer = buffer.Slice(0, 256);
        }

        public Span<byte> Span => _buffer;
        public int Length => 256;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(_buffer);
        }
    }

    /// <summary>
    /// Reusable buffer manager for reducing allocations
    /// </summary>
    public sealed class BufferManager<T>
    {
        private readonly ArrayPool<T> _pool;
        private readonly int _defaultSize;

        public BufferManager(int defaultSize = 4096, int maxArrayLength = 1024 * 1024)
        {
            _defaultSize = defaultSize;
            _pool = ArrayPool<T>.Create(maxArrayLength, 50);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RentedBuffer<T> Rent(int minimumLength = -1)
        {
            var size = minimumLength < 0 ? _defaultSize : minimumLength;
            var array = _pool.Rent(size);
            return new RentedBuffer<T>(_pool, array, size);
        }
    }

    /// <summary>
    /// Auto-returning rented buffer
    /// </summary>
    public readonly struct RentedBuffer<T> : IDisposable
    {
        private readonly ArrayPool<T> _pool;
        private readonly T[] _array;
        
        public readonly int Length;
        public Span<T> Span => _array.AsSpan(0, Length);

        internal RentedBuffer(ArrayPool<T> pool, T[] array, int length)
        {
            _pool = pool;
            _array = array;
            Length = length;
        }

        public void Dispose()
        {
            _pool?.Return(_array, clearArray: true);
        }
    }

    /// <summary>
    /// Cache line padding to prevent false sharing
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CacheLinePadding
    {
        // 64 bytes of padding to ensure different threads don't share cache lines
    }

    /// <summary>
    /// Padded value to prevent false sharing in concurrent scenarios
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PaddedValue<T> where T : struct
    {
        private CacheLinePadding _padding1;
        public T Value;
        private CacheLinePadding _padding2;

        public PaddedValue(T value)
        {
            _padding1 = default;
            _padding2 = default;
            Value = value;
        }
    }
}