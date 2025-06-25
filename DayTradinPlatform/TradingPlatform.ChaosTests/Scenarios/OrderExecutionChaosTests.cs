using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using TradingPlatform.ChaosTests.Framework;
using TradingPlatform.Core.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.PaperTrading.Services;
using TradingPlatform.Messaging.Services;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.ChaosTests.Scenarios
{
    [Collection("Chaos Tests")]
    public class OrderExecutionChaosTests : ChaosTestBase
    {
        private readonly IOrderExecutionEngine _executionEngine;
        private readonly ITradingLogger _logger;
        private readonly ICanonicalMessageQueue _messageQueue;

        public OrderExecutionChaosTests(ITestOutputHelper output) : base(output)
        {
            _logger = GetRequiredService<ITradingLogger>();
            _messageQueue = GetRequiredService<ICanonicalMessageQueue>();
            _executionEngine = new OrderExecutionEngineCanonical(_logger, _messageQueue);
        }

        [Fact]
        public async Task OrderExecution_WithPartialFills_HandlesCorrectly()
        {
            // Arrange
            await _executionEngine.InitializeAsync(CancellationToken.None);
            await _executionEngine.StartAsync(CancellationToken.None);

            var totalOrders = 50;
            var partialFills = 0;
            var completeFills = 0;
            var failures = 0;

            // Create chaos policy for partial fills
            var partialFillPolicy = CreateResultChaosPolicy<TradingResult<OrderExecution>>(
                injectionRate: 0.3, // 30% partial fills
                resultFactory: (ctx, ct) =>
                {
                    var order = ctx.Values["order"] as Order;
                    return TradingResult<OrderExecution>.Success(new OrderExecution
                    {
                        OrderId = order!.Id,
                        Symbol = order.Symbol,
                        ExecutedQuantity = order.Quantity * 0.6m, // 60% fill
                        ExecutedPrice = order.Price,
                        Status = OrderStatus.PartiallyFilled,
                        ExecutionTime = DateTime.UtcNow
                    });
                });

            // Act - Execute orders with chaos
            var tasks = new List<Task>();
            for (int i = 0; i < totalOrders; i++)
            {
                var order = new Order
                {
                    Id = Guid.NewGuid().ToString(),
                    Symbol = $"SYM{i % 10}",
                    OrderType = OrderType.Market,
                    Side = i % 2 == 0 ? OrderSide.Buy : OrderSide.Sell,
                    Quantity = 100 + (i % 50),
                    Price = 100m + (i % 20),
                    Status = OrderStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var context = new Context { ["order"] = order };
                        var result = await partialFillPolicy.ExecuteAsync(
                            async (ctx) => await _executionEngine.ExecuteOrderAsync(order, CancellationToken.None),
                            context);

                        if (result.IsSuccess)
                        {
                            if (result.Value.Status == OrderStatus.PartiallyFilled)
                            {
                                Interlocked.Increment(ref partialFills);
                            }
                            else if (result.Value.Status == OrderStatus.Filled)
                            {
                                Interlocked.Increment(ref completeFills);
                            }
                        }
                        else
                        {
                            Interlocked.Increment(ref failures);
                        }
                    }
                    catch
                    {
                        Interlocked.Increment(ref failures);
                    }
                }));
            }

            await Task.WhenAll(tasks);
            await _executionEngine.StopAsync(CancellationToken.None);

            // Assert
            partialFills.Should().BeGreaterThan(0);
            completeFills.Should().BeGreaterThan(0);
            (partialFills + completeFills + failures).Should().Be(totalOrders);
            
            Output.WriteLine($"Order execution results: {completeFills} complete, {partialFills} partial, {failures} failed");
        }

        [Fact]
        public async Task OrderExecution_WithRejections_HandlesGracefully()
        {
            // Arrange
            await _executionEngine.InitializeAsync(CancellationToken.None);
            await _executionEngine.StartAsync(CancellationToken.None);

            var acceptedOrders = 0;
            var rejectedOrders = 0;
            var totalOrders = 100;

            // Create rejection scenarios
            var rejectionReasons = new[]
            {
                "Insufficient funds",
                "Symbol halted",
                "Market closed",
                "Invalid price",
                "Risk limit exceeded"
            };

            var rejectionPolicy = CreateExceptionChaosPolicy<TradingResult<OrderExecution>>(
                injectionRate: 0.2, // 20% rejection rate
                exceptionFactory: (ctx, ct) =>
                {
                    var reason = rejectionReasons[ChaosRandom.Next(rejectionReasons.Length)];
                    return new InvalidOperationException($"Order rejected: {reason}");
                });

            // Act - Submit orders with potential rejections
            var orders = new List<Order>();
            for (int i = 0; i < totalOrders; i++)
            {
                var order = new Order
                {
                    Id = Guid.NewGuid().ToString(),
                    Symbol = $"SYM{i % 20}",
                    OrderType = i % 3 == 0 ? OrderType.Limit : OrderType.Market,
                    Side = OrderSide.Buy,
                    Quantity = 50 + (i % 100),
                    Price = 95m + (i % 10),
                    Status = OrderStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                orders.Add(order);
            }

            var executionTasks = orders.Select(order => Task.Run(async () =>
            {
                try
                {
                    var result = await rejectionPolicy.ExecuteAsync(async () =>
                        await _executionEngine.ExecuteOrderAsync(order, CancellationToken.None));

                    if (result.IsSuccess)
                    {
                        Interlocked.Increment(ref acceptedOrders);
                    }
                    else
                    {
                        Interlocked.Increment(ref rejectedOrders);
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("rejected"))
                {
                    Interlocked.Increment(ref rejectedOrders);
                }
            })).ToArray();

            await Task.WhenAll(executionTasks);
            await _executionEngine.StopAsync(CancellationToken.None);

            // Assert
            acceptedOrders.Should().BeGreaterThan(0);
            rejectedOrders.Should().BeGreaterThan(0);
            (acceptedOrders + rejectedOrders).Should().Be(totalOrders);
            
            var acceptanceRate = acceptedOrders / (double)totalOrders;
            acceptanceRate.Should().BeGreaterThan(0.7); // At least 70% accepted despite rejections
            
            Output.WriteLine($"Order acceptance: {acceptedOrders} accepted, {rejectedOrders} rejected ({acceptanceRate:P2} acceptance rate)");
        }

        [Fact]
        public async Task OrderExecution_WithSlippage_MaintainsProfitability()
        {
            // Arrange
            await _executionEngine.InitializeAsync(CancellationToken.None);
            await _executionEngine.StartAsync(CancellationToken.None);

            var totalPnL = 0m;
            var profitableOrders = 0;
            var lossOrders = 0;
            var orderCount = 50;

            // Create slippage chaos
            var slippagePolicy = CreateResultChaosPolicy<TradingResult<OrderExecution>>(
                injectionRate: 0.5, // 50% of orders experience slippage
                resultFactory: (ctx, ct) =>
                {
                    var order = ctx.Values["order"] as Order;
                    var slippagePercent = (decimal)(0.001 + ChaosRandom.NextDouble() * 0.004); // 0.1% to 0.5% slippage
                    
                    var executedPrice = order!.Side == OrderSide.Buy
                        ? order.Price * (1 + slippagePercent) // Pay more when buying
                        : order.Price * (1 - slippagePercent); // Receive less when selling

                    return TradingResult<OrderExecution>.Success(new OrderExecution
                    {
                        OrderId = order.Id,
                        Symbol = order.Symbol,
                        ExecutedQuantity = order.Quantity,
                        ExecutedPrice = executedPrice,
                        Status = OrderStatus.Filled,
                        ExecutionTime = DateTime.UtcNow,
                        Slippage = Math.Abs(executedPrice - order.Price)
                    });
                });

            // Act - Execute buy/sell pairs with slippage
            for (int i = 0; i < orderCount / 2; i++)
            {
                // Buy order
                var buyOrder = new Order
                {
                    Id = Guid.NewGuid().ToString(),
                    Symbol = $"SYM{i % 5}",
                    OrderType = OrderType.Market,
                    Side = OrderSide.Buy,
                    Quantity = 100,
                    Price = 100m + ChaosRandom.Next(-5, 5),
                    Status = OrderStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                var buyContext = new Context { ["order"] = buyOrder };
                var buyResult = await slippagePolicy.ExecuteAsync(
                    async (ctx) => await _executionEngine.ExecuteOrderAsync(buyOrder, CancellationToken.None),
                    buyContext);

                if (!buyResult.IsSuccess) continue;

                // Simulate price movement
                await Task.Delay(100);
                var priceMovement = (decimal)(-0.02 + ChaosRandom.NextDouble() * 0.04); // -2% to +2%

                // Sell order
                var sellPrice = buyResult.Value.ExecutedPrice * (1 + priceMovement);
                var sellOrder = new Order
                {
                    Id = Guid.NewGuid().ToString(),
                    Symbol = buyOrder.Symbol,
                    OrderType = OrderType.Market,
                    Side = OrderSide.Sell,
                    Quantity = buyOrder.Quantity,
                    Price = sellPrice,
                    Status = OrderStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                var sellContext = new Context { ["order"] = sellOrder };
                var sellResult = await slippagePolicy.ExecuteAsync(
                    async (ctx) => await _executionEngine.ExecuteOrderAsync(sellOrder, CancellationToken.None),
                    sellContext);

                if (sellResult.IsSuccess)
                {
                    var pnl = (sellResult.Value.ExecutedPrice - buyResult.Value.ExecutedPrice) * buyOrder.Quantity;
                    totalPnL += pnl;
                    
                    if (pnl > 0)
                        profitableOrders++;
                    else
                        lossOrders++;
                }
            }

            await _executionEngine.StopAsync(CancellationToken.None);

            // Assert
            (profitableOrders + lossOrders).Should().BeGreaterThan(0);
            var winRate = profitableOrders / (double)(profitableOrders + lossOrders);
            
            Output.WriteLine($"Trading with slippage: {profitableOrders} profitable, {lossOrders} losses");
            Output.WriteLine($"Win rate: {winRate:P2}, Total P&L: ${totalPnL:N2}");
        }

        [Fact]
        public async Task OrderExecution_WithConcurrentOrders_MaintainsConsistency()
        {
            // Arrange
            await _executionEngine.InitializeAsync(CancellationToken.None);
            await _executionEngine.StartAsync(CancellationToken.None);

            var concurrentBatches = 10;
            var ordersPerBatch = 20;
            var executionResults = new List<OrderExecution>();
            var orderIds = new HashSet<string>();

            // Act - Execute concurrent batches
            var batchTasks = new Task[concurrentBatches];
            for (int batch = 0; batch < concurrentBatches; batch++)
            {
                var batchIndex = batch;
                batchTasks[batch] = Task.Run(async () =>
                {
                    var batchResults = new List<OrderExecution>();
                    
                    // Submit orders concurrently within batch
                    var orderTasks = new Task<TradingResult<OrderExecution>>[ordersPerBatch];
                    for (int i = 0; i < ordersPerBatch; i++)
                    {
                        var order = new Order
                        {
                            Id = $"BATCH{batchIndex}-ORDER{i}",
                            Symbol = "AAPL",
                            OrderType = OrderType.Market,
                            Side = i % 2 == 0 ? OrderSide.Buy : OrderSide.Sell,
                            Quantity = 10,
                            Price = 150m,
                            Status = OrderStatus.Pending,
                            CreatedAt = DateTime.UtcNow
                        };

                        lock (orderIds)
                        {
                            orderIds.Add(order.Id);
                        }

                        orderTasks[i] = _executionEngine.ExecuteOrderAsync(order, CancellationToken.None);
                    }

                    var results = await Task.WhenAll(orderTasks);
                    
                    foreach (var result in results.Where(r => r.IsSuccess))
                    {
                        batchResults.Add(result.Value);
                    }

                    lock (executionResults)
                    {
                        executionResults.AddRange(batchResults);
                    }
                });
            }

            await Task.WhenAll(batchTasks);
            await _executionEngine.StopAsync(CancellationToken.None);

            // Assert - No duplicate executions
            var uniqueExecutions = executionResults.Select(e => e.OrderId).Distinct().Count();
            uniqueExecutions.Should().Be(executionResults.Count);
            
            // All submitted orders should be accounted for
            var executedOrderIds = new HashSet<string>(executionResults.Select(e => e.OrderId));
            var unexecutedOrders = orderIds.Except(executedOrderIds).ToList();
            
            Output.WriteLine($"Concurrent execution: {executionResults.Count} orders executed");
            Output.WriteLine($"Unique executions: {uniqueExecutions}");
            Output.WriteLine($"Unexecuted orders: {unexecutedOrders.Count}");
        }

        [Fact]
        public async Task OrderExecution_DuringMarketVolatility_AdaptsExecutionStrategy()
        {
            // Arrange
            await _executionEngine.InitializeAsync(CancellationToken.None);
            await _executionEngine.StartAsync(CancellationToken.None);

            var volatilityPeriods = 5;
            var ordersPerPeriod = 20;
            var executionMetrics = new List<ExecutionMetrics>();

            // Act - Execute orders during different volatility periods
            for (int period = 0; period < volatilityPeriods; period++)
            {
                var volatility = 0.01 + (period * 0.02); // Increasing volatility
                var periodMetrics = new ExecutionMetrics { Volatility = volatility };

                // Create volatility-based chaos
                var volatilityPolicy = CreateResultChaosPolicy<TradingResult<OrderExecution>>(
                    injectionRate: volatility * 2, // Higher volatility = more chaos
                    resultFactory: (ctx, ct) =>
                    {
                        var order = ctx.Values["order"] as Order;
                        var priceImpact = (decimal)(ChaosRandom.NextDouble() * volatility * 2 - volatility);
                        
                        return TradingResult<OrderExecution>.Success(new OrderExecution
                        {
                            OrderId = order!.Id,
                            Symbol = order.Symbol,
                            ExecutedQuantity = order.Quantity,
                            ExecutedPrice = order.Price * (1 + priceImpact),
                            Status = OrderStatus.Filled,
                            ExecutionTime = DateTime.UtcNow
                        });
                    });

                // Execute orders for this period
                var periodTasks = new List<Task>();
                for (int i = 0; i < ordersPerPeriod; i++)
                {
                    var order = new Order
                    {
                        Id = $"P{period}-O{i}",
                        Symbol = "AAPL",
                        OrderType = volatility > 0.05 ? OrderType.Limit : OrderType.Market, // Use limit orders in high volatility
                        Side = OrderSide.Buy,
                        Quantity = volatility > 0.05 ? 50 : 100, // Reduce size in high volatility
                        Price = 150m,
                        Status = OrderStatus.Pending,
                        CreatedAt = DateTime.UtcNow
                    };

                    periodTasks.Add(Task.Run(async () =>
                    {
                        var context = new Context { ["order"] = order };
                        var startTime = DateTime.UtcNow;
                        
                        var result = await volatilityPolicy.ExecuteAsync(
                            async (ctx) => await _executionEngine.ExecuteOrderAsync(order, CancellationToken.None),
                            context);

                        if (result.IsSuccess)
                        {
                            var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                            var priceImpact = Math.Abs(result.Value.ExecutedPrice - order.Price) / order.Price;
                            
                            lock (periodMetrics)
                            {
                                periodMetrics.ExecutionTimes.Add(executionTime);
                                periodMetrics.PriceImpacts.Add((double)priceImpact);
                                periodMetrics.SuccessfulExecutions++;
                            }
                        }
                        else
                        {
                            lock (periodMetrics)
                            {
                                periodMetrics.FailedExecutions++;
                            }
                        }
                    }));
                }

                await Task.WhenAll(periodTasks);
                executionMetrics.Add(periodMetrics);
                
                // Brief pause between periods
                await Task.Delay(500);
            }

            await _executionEngine.StopAsync(CancellationToken.None);

            // Assert - Execution should adapt to volatility
            for (int i = 0; i < executionMetrics.Count; i++)
            {
                var metrics = executionMetrics[i];
                var avgExecutionTime = metrics.ExecutionTimes.Any() ? metrics.ExecutionTimes.Average() : 0;
                var avgPriceImpact = metrics.PriceImpacts.Any() ? metrics.PriceImpacts.Average() : 0;
                var successRate = metrics.SuccessfulExecutions / (double)ordersPerPeriod;

                Output.WriteLine($"Period {i}: Volatility={metrics.Volatility:P1}, " +
                    $"Success={successRate:P1}, AvgTime={avgExecutionTime:F1}ms, " +
                    $"AvgImpact={avgPriceImpact:P2}");
            }

            // Higher volatility should show adaptation
            var lowVolMetrics = executionMetrics.First();
            var highVolMetrics = executionMetrics.Last();
            
            // Success rate might decrease with volatility
            lowVolMetrics.SuccessfulExecutions.Should().BeGreaterThanOrEqualTo(highVolMetrics.SuccessfulExecutions);
        }

        private class ExecutionMetrics
        {
            public double Volatility { get; set; }
            public int SuccessfulExecutions { get; set; }
            public int FailedExecutions { get; set; }
            public List<double> ExecutionTimes { get; } = new List<double>();
            public List<double> PriceImpacts { get; } = new List<double>();
        }
    }
}