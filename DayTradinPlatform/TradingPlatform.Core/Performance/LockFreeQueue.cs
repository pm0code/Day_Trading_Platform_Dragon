using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace TradingPlatform.Core.Performance
{
    /// <summary>
    /// Lock-free single-producer single-consumer queue for ultra-low latency
    /// </summary>
    public sealed class LockFreeQueue<T> where T : class
    {
        private class Node
        {
            public T? Value;
            public Node? Next;
        }

        private Node _head;
        private Node _tail;

        public LockFreeQueue()
        {
            _head = _tail = new Node();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(T item)
        {
            var node = new Node { Value = item };
            var prevTail = Interlocked.Exchange(ref _tail, node);
            prevTail.Next = node;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T? item)
        {
            Node head;
            Node next;

            do
            {
                head = _head;
                next = head.Next!;

                if (next == null)
                {
                    item = null;
                    return false;
                }
            } while (Interlocked.CompareExchange(ref _head, next, head) != head);

            item = next.Value;
            next.Value = null; // Help GC
            return true;
        }

        public bool IsEmpty => _head.Next == null;
    }

    /// <summary>
    /// Lock-free ring buffer for fixed-size ultra-low latency scenarios
    /// </summary>
    public sealed class LockFreeRingBuffer<T> where T : struct
    {
        private readonly T[] _buffer;
        private readonly int _mask;
        private long _writePosition;
        private long _readPosition;

        public LockFreeRingBuffer(int size)
        {
            if ((size & (size - 1)) != 0)
                throw new ArgumentException("Size must be power of 2");

            _buffer = new T[size];
            _mask = size - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWrite(in T item)
        {
            var writePos = _writePosition;
            var readPos = Volatile.Read(ref _readPosition);

            if (writePos - readPos >= _buffer.Length)
                return false;

            _buffer[writePos & _mask] = item;
            Volatile.Write(ref _writePosition, writePos + 1);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRead(out T item)
        {
            var readPos = _readPosition;
            var writePos = Volatile.Read(ref _writePosition);

            if (readPos >= writePos)
            {
                item = default;
                return false;
            }

            item = _buffer[readPos & _mask];
            Volatile.Write(ref _readPosition, readPos + 1);
            return true;
        }

        public int Count => (int)(Volatile.Read(ref _writePosition) - Volatile.Read(ref _readPosition));
        public int Capacity => _buffer.Length;
    }

    /// <summary>
    /// Busy-wait utilities for ultra-low latency scenarios
    /// </summary>
    public static class SpinWaitExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SpinUntil(Func<bool> condition, int iterations = 1000)
        {
            var spinner = new SpinWait();
            var count = 0;

            while (!condition() && count < iterations)
            {
                spinner.SpinOnce();
                count++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T SpinWaitFor<T>(Func<T?> getter, int iterations = 1000) where T : class
        {
            var spinner = new SpinWait();
            var count = 0;
            T? result;

            while ((result = getter()) == null && count < iterations)
            {
                spinner.SpinOnce();
                count++;
            }

            return result ?? throw new TimeoutException("SpinWait timeout");
        }
    }
}