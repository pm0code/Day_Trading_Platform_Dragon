using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using TradingPlatform.Core.Models;
using TradingPlatform.Core.Performance;
using TradingPlatform.PerformanceTests.Framework;

namespace TradingPlatform.PerformanceTests.Benchmarks
{
    /// <summary>
    /// Benchmarks to validate performance optimizations
    /// </summary>
    [Config(typeof(UltraLowLatencyConfig))]
    public class OptimizationBenchmarks : CanonicalBenchmarkBase
    {
        private HighPerformancePool<Order> _orderPool = null!;
        private LockFreeQueue<Order> _lockFreeQueue = null!;
        private LockFreeRingBuffer<decimal> _ringBuffer = null!;
        private OptimizedOrderBook _orderBook = null!;
        private List<Order> _testOrders = null!;

        [GlobalSetup]
        public override void GlobalSetup()
        {
            base.GlobalSetup();

            // Initialize object pool
            _orderPool = new HighPerformancePool<Order>(
                () => new Order(),
                o => o.Clear(),
                maxSize: 1000);

            // Initialize lock-free structures
            _lockFreeQueue = new LockFreeQueue<Order>();
            _ringBuffer = new LockFreeRingBuffer<decimal>(1024);

            // Initialize order book
            _orderBook = new OptimizedOrderBook("AAPL");

            // Generate test orders
            _testOrders = new List<Order>(1000);
            var random = new Random(42);
            for (int i = 0; i < 1000; i++)
            {
                _testOrders.Add(new Order
                {
                    Id = Guid.NewGuid().ToString(),
                    Symbol = "AAPL",
                    OrderType = i % 2 == 0 ? OrderType.Limit : OrderType.Market,
                    Side = i % 2 == 0 ? OrderSide.Buy : OrderSide.Sell,
                    Quantity = random.Next(1, 100),
                    Price = 150m + (decimal)(random.NextDouble() * 10 - 5),
                    Status = OrderStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        [Benchmark(Baseline = true)]
        public Order AllocateOrderNormal()
        {
            return new Order
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = "AAPL",
                OrderType = OrderType.Market,
                Side = OrderSide.Buy,
                Quantity = 100,
                Price = 150m,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
        }

        [Benchmark]
        public Order AllocateOrderPooled()
        {
            var order = _orderPool.Rent();
            order.Id = Guid.NewGuid().ToString();
            order.Symbol = "AAPL";
            order.OrderType = OrderType.Market;
            order.Side = OrderSide.Buy;
            order.Quantity = 100;
            order.Price = 150m;
            order.Status = OrderStatus.Pending;
            order.CreatedAt = DateTime.UtcNow;
            return order;
        }

        [Benchmark]
        public void ReturnOrderToPool()
        {
            var order = _orderPool.Rent();
            _orderPool.Return(order);
        }

        [Benchmark]
        public void LockFreeQueueOperations()
        {
            var order = _testOrders[0];
            _lockFreeQueue.Enqueue(order);
            _lockFreeQueue.TryDequeue(out _);
        }

        [Benchmark]
        public void RingBufferOperations()
        {
            _ringBuffer.TryWrite(150.25m);
            _ringBuffer.TryRead(out _);
        }

        [Benchmark]
        public void OrderBookAddOrder()
        {
            _orderBook.Clear();
            foreach (var order in _testOrders.GetRange(0, 10))
            {
                _orderBook.AddOrder(order);
            }
        }

        [Benchmark]
        public (decimal, decimal) OrderBookGetBestBidAsk()
        {
            return _orderBook.GetBestBidAsk();
        }

        [Benchmark]
        public decimal OrderBookGetSpread()
        {
            return _orderBook.GetSpread();
        }

        [Benchmark]
        public void MemoryOptimizationArrayPool()
        {
            var buffer = MemoryOptimizations.RentArray<decimal>(1000);
            
            // Simulate some work
            for (int i = 0; i < 100; i++)
            {
                buffer[i] = i * 1.5m;
            }
            
            MemoryOptimizations.ReturnArray(buffer);
        }

        [Benchmark]
        public unsafe void UnmanagedMemoryAccess()
        {
            using var buffer = MemoryOptimizations.AllocateUnmanaged<decimal>(100);
            var span = buffer.AsSpan();
            
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = i * 1.5m;
            }
        }

        [Benchmark]
        public void StackAllocatedBuffer()
        {
            Span<byte> stackBuffer = stackalloc byte[256];
            var buffer = new StackBuffer256(stackBuffer);
            var decimalSpan = buffer.AsSpan<decimal>();
            
            for (int i = 0; i < decimalSpan.Length && i < 10; i++)
            {
                decimalSpan[i] = i * 1.5m;
            }
        }

        [Benchmark]
        public void LatencyTracking()
        {
            using (var scope = LatencyTracking.GetOrCreate("Test").MeasureScope())
            {
                // Simulate some work
                var sum = 0m;
                for (int i = 0; i < 100; i++)
                {
                    sum += i * 1.5m;
                }
            }
        }

        [Benchmark]
        public void BufferManagerUsage()
        {
            var manager = new BufferManager<decimal>();
            using var buffer = manager.Rent(100);
            
            for (int i = 0; i < buffer.Length && i < 100; i++)
            {
                buffer.Span[i] = i * 1.5m;
            }
        }
    }

    /// <summary>
    /// Concurrent performance benchmarks
    /// </summary>
    public class ConcurrentOptimizationBenchmarks : CanonicalBenchmarkBase
    {
        private LockFreeQueue<int> _queue = null!;
        private HighPerformancePool<TestObject> _pool = null!;
        private readonly int _iterations = 10000;

        private class TestObject
        {
            public int Value { get; set; }
            public string Data { get; set; } = "";
            
            public void Clear()
            {
                Value = 0;
                Data = "";
            }
        }

        [GlobalSetup]
        public override void GlobalSetup()
        {
            base.GlobalSetup();
            _queue = new LockFreeQueue<int>();
            _pool = new HighPerformancePool<TestObject>(
                () => new TestObject(),
                obj => obj.Clear(),
                maxSize: 1000);
        }

        [Benchmark]
        public async Task ConcurrentQueueOperations()
        {
            var tasks = new Task[4];
            
            // Producer tasks
            tasks[0] = Task.Run(() =>
            {
                for (int i = 0; i < _iterations; i++)
                {
                    _queue.Enqueue(i);
                }
            });
            
            tasks[1] = Task.Run(() =>
            {
                for (int i = 0; i < _iterations; i++)
                {
                    _queue.Enqueue(i + _iterations);
                }
            });
            
            // Consumer tasks
            tasks[2] = Task.Run(() =>
            {
                int consumed = 0;
                while (consumed < _iterations)
                {
                    if (_queue.TryDequeue(out _))
                    {
                        consumed++;
                    }
                }
            });
            
            tasks[3] = Task.Run(() =>
            {
                int consumed = 0;
                while (consumed < _iterations)
                {
                    if (_queue.TryDequeue(out _))
                    {
                        consumed++;
                    }
                }
            });
            
            await Task.WhenAll(tasks);
        }

        [Benchmark]
        public async Task ConcurrentPoolOperations()
        {
            var tasks = new Task[Environment.ProcessorCount];
            
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < _iterations / tasks.Length; j++)
                    {
                        var obj = _pool.Rent();
                        obj.Value = j;
                        obj.Data = $"Test {j}";
                        _pool.Return(obj);
                    }
                });
            }
            
            await Task.WhenAll(tasks);
        }
    }
}