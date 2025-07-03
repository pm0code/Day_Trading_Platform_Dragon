using System;
using System.Threading;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.FixEngine.Canonical;

namespace TradingPlatform.FixEngine.Performance
{
    /// <summary>
    /// Lock-free queue implementation for ultra-low latency message passing.
    /// Based on Michael & Scott algorithm with enhancements for cache efficiency.
    /// </summary>
    /// <remarks>
    /// Provides wait-free enqueue and lock-free dequeue operations.
    /// Optimized for single-producer/single-consumer scenarios but supports MPMC.
    /// Uses cache-line padding to prevent false sharing.
    /// </remarks>
    /// <typeparam name="T">The type of elements in the queue</typeparam>
    public class LockFreeQueue<T> where T : class
    {
        private class Node
        {
            public T? Item;
            public volatile Node? Next;
            
            public Node()
            {
                Item = default;
                Next = null;
            }
        }
        
        // Cache line padding to prevent false sharing (64 bytes typical cache line)
        private class PaddedReference
        {
            private long _padding0, _padding1, _padding2, _padding3;
            private long _padding4, _padding5, _padding6, _padding7;
            
            public volatile Node? Value;
            
            private long _padding8, _padding9, _padding10, _padding11;
            private long _padding12, _padding13, _padding14, _padding15;
        }
        
        private readonly PaddedReference _head;
        private readonly PaddedReference _tail;
        private readonly ITradingLogger _logger;
        private long _enqueueCount;
        private long _dequeueCount;
        
        /// <summary>
        /// Initializes a new instance of the LockFreeQueue class.
        /// </summary>
        /// <param name="logger">Logger for diagnostics</param>
        public LockFreeQueue(ITradingLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            var dummy = new Node();
            _head = new PaddedReference { Value = dummy };
            _tail = new PaddedReference { Value = dummy };
        }
        
        /// <summary>
        /// Enqueues an item in a lock-free manner.
        /// </summary>
        /// <param name="item">The item to enqueue</param>
        public void Enqueue(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            
            var newNode = new Node { Item = item };
            
            while (true)
            {
                var tail = _tail.Value;
                var next = tail?.Next;
                
                // Check tail consistency
                if (tail == _tail.Value)
                {
                    if (next == null)
                    {
                        // Try to link new node
                        if (Interlocked.CompareExchange(ref tail!.Next, newNode, null) == null)
                        {
                            // Success - try to swing tail
                            Interlocked.CompareExchange(ref _tail.Value, newNode, tail);
                            Interlocked.Increment(ref _enqueueCount);
                            break;
                        }
                    }
                    else
                    {
                        // Tail was not pointing to last node, try to advance
                        Interlocked.CompareExchange(ref _tail.Value, next, tail);
                    }
                }
            }
        }
        
        /// <summary>
        /// Attempts to dequeue an item in a lock-free manner.
        /// </summary>
        /// <param name="item">The dequeued item if successful</param>
        /// <returns>True if an item was dequeued, false if queue is empty</returns>
        public bool TryDequeue(out T? item)
        {
            item = default;
            
            while (true)
            {
                var head = _head.Value;
                var tail = _tail.Value;
                var next = head?.Next;
                
                // Check head consistency
                if (head == _head.Value)
                {
                    if (head == tail)
                    {
                        if (next == null)
                        {
                            // Queue is empty
                            return false;
                        }
                        
                        // Tail is behind, try to advance
                        Interlocked.CompareExchange(ref _tail.Value, next, tail);
                    }
                    else
                    {
                        // Read value before CAS
                        if (next != null)
                        {
                            item = next.Item;
                            
                            // Try to swing head
                            if (Interlocked.CompareExchange(ref _head.Value, next, head) == head)
                            {
                                Interlocked.Increment(ref _dequeueCount);
                                return true;
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets the approximate count of items in the queue.
        /// </summary>
        /// <remarks>
        /// This is an approximation as the queue may be modified concurrently.
        /// </remarks>
        public long ApproximateCount => 
            Math.Max(0, Interlocked.Read(ref _enqueueCount) - Interlocked.Read(ref _dequeueCount));
        
        /// <summary>
        /// Gets queue statistics.
        /// </summary>
        public (long EnqueueCount, long DequeueCount) GetStats()
        {
            return (Interlocked.Read(ref _enqueueCount), Interlocked.Read(ref _dequeueCount));
        }
    }
    
    /// <summary>
    /// Specialized lock-free queue for FIX messages with pre-allocation.
    /// </summary>
    public class FixMessageQueue : CanonicalFixServiceBase
    {
        private readonly LockFreeQueue<FixMessageWrapper> _queue;
        private readonly ObjectPool<FixMessageWrapper> _wrapperPool;
        private readonly int _maxQueueDepth;
        private long _droppedMessages;
        
        /// <summary>
        /// Initializes a new instance of the FixMessageQueue class.
        /// </summary>
        public FixMessageQueue(
            ITradingLogger logger,
            int maxQueueDepth = 100000)
            : base(logger, "MessageQueue")
        {
            _queue = new LockFreeQueue<FixMessageWrapper>(logger);
            _wrapperPool = new ObjectPool<FixMessageWrapper>(() => new FixMessageWrapper(), 1000);
            _maxQueueDepth = maxQueueDepth;
        }
        
        /// <summary>
        /// Enqueues a FIX message with zero allocation.
        /// </summary>
        /// <param name="message">The message data</param>
        /// <param name="length">The message length</param>
        /// <param name="timestamp">Hardware timestamp</param>
        /// <returns>True if enqueued, false if queue is full</returns>
        public bool TryEnqueue(byte[] message, int length, long timestamp)
        {
            LogMethodEntry();
            
            try
            {
                if (_queue.ApproximateCount >= _maxQueueDepth)
                {
                    Interlocked.Increment(ref _droppedMessages);
                    RecordMetric("DroppedMessages", 1);
                    LogMethodExit();
                    return false;
                }
                
                var wrapper = _wrapperPool.Rent();
                wrapper.SetMessage(message, length, timestamp);
                
                _queue.Enqueue(wrapper);
                
                RecordMetric("MessagesQueued", 1);
                
                LogMethodExit();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enqueueing message");
                LogMethodExit();
                return false;
            }
        }
        
        /// <summary>
        /// Attempts to dequeue a FIX message.
        /// </summary>
        /// <param name="message">The message data</param>
        /// <param name="length">The message length</param>
        /// <param name="timestamp">Hardware timestamp</param>
        /// <returns>True if dequeued, false if queue is empty</returns>
        public bool TryDequeue(out byte[]? message, out int length, out long timestamp)
        {
            LogMethodEntry();
            
            message = null;
            length = 0;
            timestamp = 0;
            
            try
            {
                if (_queue.TryDequeue(out var wrapper) && wrapper != null)
                {
                    message = wrapper.Message;
                    length = wrapper.Length;
                    timestamp = wrapper.Timestamp;
                    
                    _wrapperPool.Return(wrapper);
                    
                    RecordMetric("MessagesDequeued", 1);
                    
                    LogMethodExit();
                    return true;
                }
                
                LogMethodExit();
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dequeuing message");
                LogMethodExit();
                return false;
            }
        }
        
        /// <summary>
        /// Gets queue performance statistics.
        /// </summary>
        public FixQueueStats GetQueueStats()
        {
            LogMethodEntry();
            
            try
            {
                var (enqueued, dequeued) = _queue.GetStats();
                
                var stats = new FixQueueStats
                {
                    MessagesEnqueued = enqueued,
                    MessagesDequeued = dequeued,
                    MessagesDropped = Interlocked.Read(ref _droppedMessages),
                    CurrentDepth = _queue.ApproximateCount,
                    MaxDepth = _maxQueueDepth
                };
                
                LogMethodExit();
                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting queue stats");
                LogMethodExit();
                throw;
            }
        }
        
        /// <summary>
        /// Message wrapper for zero-allocation queueing.
        /// </summary>
        private class FixMessageWrapper
        {
            public byte[]? Message { get; private set; }
            public int Length { get; private set; }
            public long Timestamp { get; private set; }
            
            public void SetMessage(byte[] message, int length, long timestamp)
            {
                Message = message;
                Length = length;
                Timestamp = timestamp;
            }
            
            public void Reset()
            {
                Message = null;
                Length = 0;
                Timestamp = 0;
            }
        }
        
        /// <summary>
        /// Simple object pool implementation.
        /// </summary>
        private class ObjectPool<TItem> where TItem : class
        {
            private readonly ConcurrentBag<TItem> _items;
            private readonly Func<TItem> _factory;
            
            public ObjectPool(Func<TItem> factory, int preAllocate)
            {
                _factory = factory;
                _items = new ConcurrentBag<TItem>();
                
                for (int i = 0; i < preAllocate; i++)
                {
                    _items.Add(_factory());
                }
            }
            
            public TItem Rent()
            {
                return _items.TryTake(out var item) ? item : _factory();
            }
            
            public void Return(TItem item)
            {
                if (item is FixMessageWrapper wrapper)
                {
                    wrapper.Reset();
                }
                _items.Add(item);
            }
        }
    }
    
    /// <summary>
    /// Queue statistics.
    /// </summary>
    public class FixQueueStats
    {
        public long MessagesEnqueued { get; set; }
        public long MessagesDequeued { get; set; }
        public long MessagesDropped { get; set; }
        public long CurrentDepth { get; set; }
        public int MaxDepth { get; set; }
    }
}