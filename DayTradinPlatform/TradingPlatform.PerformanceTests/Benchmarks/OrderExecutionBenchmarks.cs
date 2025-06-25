using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using TradingPlatform.Core.Models;
using TradingPlatform.PaperTrading.Services;
using TradingPlatform.Messaging.Services;
using TradingPlatform.PerformanceTests.Framework;
using Moq;

namespace TradingPlatform.PerformanceTests.Benchmarks
{
    /// <summary>
    /// Benchmarks for order execution to meet ultra-low latency requirements
    /// Target: < 100 microseconds order-to-wire
    /// </summary>
    [Config(typeof(UltraLowLatencyConfig))]
    public class OrderExecutionBenchmarks : CanonicalBenchmarkBase
    {
        private OrderExecutionEngineCanonical _executionEngine = null!;
        private Order _marketOrder = null!;
        private Order _limitOrder = null!;
        private Mock<ICanonicalMessageQueue> _mockMessageQueue = null!;

        [GlobalSetup]
        public override void GlobalSetup()
        {
            base.GlobalSetup();
            
            _mockMessageQueue = new Mock<ICanonicalMessageQueue>();
            _mockMessageQueue.Setup(x => x.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<MessagePriority>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult<string>.Success("message-id"));

            _executionEngine = new OrderExecutionEngineCanonical(Logger, _mockMessageQueue.Object);
            _executionEngine.InitializeAsync(CancellationToken.None).Wait();
            _executionEngine.StartAsync(CancellationToken.None).Wait();

            _marketOrder = new Order
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

            _limitOrder = new Order
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = "GOOGL",
                OrderType = OrderType.Limit,
                Side = OrderSide.Sell,
                Quantity = 50,
                Price = 2800m,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
        }

        [GlobalCleanup]
        public override void GlobalCleanup()
        {
            _executionEngine?.StopAsync(CancellationToken.None).Wait();
            _executionEngine?.Dispose();
            base.GlobalCleanup();
        }

        [Benchmark(Baseline = true)]
        public async Task<OrderExecution> ExecuteMarketOrder()
        {
            var result = await _executionEngine.ExecuteOrderAsync(_marketOrder, CancellationToken.None);
            return result.Value;
        }

        [Benchmark]
        public async Task<OrderExecution> ExecuteLimitOrder()
        {
            var result = await _executionEngine.ExecuteOrderAsync(_limitOrder, CancellationToken.None);
            return result.Value;
        }

        [Benchmark]
        public async Task<bool> ValidateOrder()
        {
            var result = await _executionEngine.ValidateOrderAsync(_marketOrder, CancellationToken.None);
            return result.Value;
        }

        [Benchmark]
        public OrderExecution SimulateExecution()
        {
            // Direct execution without async overhead
            return new OrderExecution
            {
                OrderId = _marketOrder.Id,
                Symbol = _marketOrder.Symbol,
                Side = _marketOrder.Side,
                Quantity = _marketOrder.Quantity,
                Price = _marketOrder.Price,
                Commission = _marketOrder.Quantity * _marketOrder.Price * 0.001m,
                ExecutedAt = DateTime.UtcNow,
                Slippage = 0.01m,
                MarketImpact = 0.005m
            };
        }

        [Benchmark]
        public decimal CalculateCommission()
        {
            return _marketOrder.Quantity * _marketOrder.Price * 0.001m;
        }

        [Benchmark]
        public decimal CalculateSlippage()
        {
            var baseSlippage = 0.0001m; // 1 basis point
            var volumeImpact = _marketOrder.Quantity / 1000000m; // Volume impact
            var volatilityMultiplier = 1.5m; // Volatility factor
            
            return _marketOrder.Price * baseSlippage * (1 + volumeImpact) * volatilityMultiplier;
        }
    }

    /// <summary>
    /// Benchmarks for high-frequency order processing
    /// </summary>
    public class HighFrequencyOrderBenchmarks : CanonicalBenchmarkBase
    {
        private Order[] _orders = null!;
        private readonly int _orderCount = 1000;

        [GlobalSetup]
        public override void GlobalSetup()
        {
            base.GlobalSetup();
            
            _orders = new Order[_orderCount];
            var random = new Random(42); // Fixed seed for reproducibility
            
            for (int i = 0; i < _orderCount; i++)
            {
                _orders[i] = new Order
                {
                    Id = Guid.NewGuid().ToString(),
                    Symbol = $"SYM{i % 10}",
                    OrderType = i % 2 == 0 ? OrderType.Market : OrderType.Limit,
                    Side = i % 2 == 0 ? OrderSide.Buy : OrderSide.Sell,
                    Quantity = random.Next(1, 1000),
                    Price = 100m + (decimal)(random.NextDouble() * 100),
                    Status = OrderStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
            }
        }

        [Benchmark]
        public int ProcessOrderBatch()
        {
            int processed = 0;
            foreach (var order in _orders)
            {
                if (order.OrderType == OrderType.Market || 
                    (order.OrderType == OrderType.Limit && order.Price > 0))
                {
                    processed++;
                }
            }
            return processed;
        }

        [Benchmark]
        public decimal CalculateTotalValue()
        {
            decimal total = 0;
            foreach (var order in _orders)
            {
                total += order.Quantity * order.Price;
            }
            return total;
        }

        [Benchmark]
        public int[] GroupBySymbol()
        {
            var groups = new int[10]; // We have 10 different symbols
            foreach (var order in _orders)
            {
                var symbolIndex = int.Parse(order.Symbol.Substring(3));
                groups[symbolIndex]++;
            }
            return groups;
        }

        [Benchmark]
        public async Task ProcessOrdersConcurrently()
        {
            var tasks = new Task[Environment.ProcessorCount];
            var ordersPerTask = _orderCount / tasks.Length;
            
            for (int i = 0; i < tasks.Length; i++)
            {
                var startIndex = i * ordersPerTask;
                var endIndex = i == tasks.Length - 1 ? _orderCount : startIndex + ordersPerTask;
                
                tasks[i] = Task.Run(() =>
                {
                    decimal localSum = 0;
                    for (int j = startIndex; j < endIndex; j++)
                    {
                        localSum += _orders[j].Quantity * _orders[j].Price;
                    }
                    return localSum;
                });
            }
            
            await Task.WhenAll(tasks);
        }
    }
}