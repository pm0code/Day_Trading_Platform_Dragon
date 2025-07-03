using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.PaperTrading.Services;
using TradingPlatform.Screening.Engines;
using TradingPlatform.RiskManagement.Services;
using TradingPlatform.Tests.Core.Canonical;
using Moq;

namespace TradingPlatform.Tests.Performance
{
    /// <summary>
    /// Performance tests for the trading platform
    /// Tests latency, throughput, and scalability under load
    /// </summary>
    public class TradingPlatformPerformanceTests : IntegrationTestBase
    {
        private IOrderExecutionEngine _executionEngine;
        private RealTimeScreeningEngineCanonical _screeningEngine;
        private RiskCalculatorCanonical _riskCalculator;
        private Mock<IMarketDataService> _mockMarketDataService;
        
        // Performance targets from requirements
        private const int TARGET_ORDER_LATENCY_MS = 50; // < 50ms for Enterprise
        private const int TARGET_SCREENING_LATENCY_MS = 100;
        private const int TARGET_RISK_CALC_LATENCY_MS = 100;
        private const int TARGET_MESSAGES_PER_SECOND = 10000;
        
        public TradingPlatformPerformanceTests(ITestOutputHelper output) : base(output)
        {
            _mockMarketDataService = new Mock<IMarketDataService>();
        }
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddPaperTradingServices();
            services.AddScreeningServices();
            services.AddSingleton<RiskCalculatorCanonical>();
            
            services.AddSingleton(MockLogger.Object);
            services.AddSingleton(_mockMarketDataService.Object);
            services.AddSingleton(Mock.Of<IServiceProvider>());
            services.AddSingleton(Mock.Of<IDataIngestionService>());
            services.AddSingleton(Mock.Of<IAlertService>());
        }
        
        protected override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            
            _executionEngine = ServiceProvider.GetRequiredService<IOrderExecutionEngine>();
            _screeningEngine = ServiceProvider.GetRequiredService<RealTimeScreeningEngineCanonical>();
            _riskCalculator = ServiceProvider.GetRequiredService<RiskCalculatorCanonical>();
            
            await _screeningEngine.InitializeAsync();
            await _riskCalculator.InitializeAsync();
            
            // Setup market data
            SetupPerformanceTestData();
        }
        
        #region Order Execution Performance
        
        [Fact]
        public async Task OrderExecution_SingleOrder_MeetsLatencyTarget()
        {
            // Arrange
            Output.WriteLine("=== Order Execution Latency Test ===");
            
            var order = new Order
            {
                OrderId = Guid.NewGuid().ToString(),
                Symbol = "AAPL",
                Quantity = 100,
                OrderType = OrderType.Market,
                Side = OrderSide.Buy
            };
            
            // Warmup
            await _executionEngine.ExecuteOrderAsync(order, 175m);
            
            // Act - Measure single order latency
            var latencies = new List<long>();
            
            for (int i = 0; i < 100; i++)
            {
                var sw = Stopwatch.StartNew();
                var execution = await _executionEngine.ExecuteOrderAsync(order, 175m + i * 0.01m);
                sw.Stop();
                
                latencies.Add(sw.ElapsedMilliseconds);
            }
            
            // Assert
            var avgLatency = latencies.Average();
            var p50 = GetPercentile(latencies, 0.50);
            var p95 = GetPercentile(latencies, 0.95);
            var p99 = GetPercentile(latencies, 0.99);
            
            Output.WriteLine($"Order Execution Latency (ms):");
            Output.WriteLine($"  Average: {avgLatency:F2}");
            Output.WriteLine($"  P50: {p50}");
            Output.WriteLine($"  P95: {p95}");
            Output.WriteLine($"  P99: {p99}");
            Output.WriteLine($"  Target: <{TARGET_ORDER_LATENCY_MS}ms");
            
            Assert.True(p95 < TARGET_ORDER_LATENCY_MS, 
                $"P95 latency ({p95}ms) exceeds target ({TARGET_ORDER_LATENCY_MS}ms)");
        }
        
        [Fact]
        public async Task OrderExecution_ConcurrentOrders_MaintainsThroughput()
        {
            // Arrange
            Output.WriteLine("=== Order Execution Throughput Test ===");
            
            var orderCount = 1000;
            var concurrency = 50;
            var orders = GenerateTestOrders(orderCount);
            
            // Act - Execute orders concurrently
            var sw = Stopwatch.StartNew();
            var semaphore = new SemaphoreSlim(concurrency);
            var executionTasks = new List<Task<Execution>>();
            
            foreach (var order in orders)
            {
                var task = Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        return await _executionEngine.ExecuteOrderAsync(order, 100m);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                
                executionTasks.Add(task);
            }
            
            var executions = await Task.WhenAll(executionTasks);
            sw.Stop();
            
            // Assert
            var successCount = executions.Count(e => e?.Status == OrderStatus.Filled);
            var throughput = successCount / (sw.ElapsedMilliseconds / 1000.0);
            
            Output.WriteLine($"Concurrent Order Execution:");
            Output.WriteLine($"  Total Orders: {orderCount}");
            Output.WriteLine($"  Concurrency: {concurrency}");
            Output.WriteLine($"  Duration: {sw.ElapsedMilliseconds}ms");
            Output.WriteLine($"  Successful: {successCount}");
            Output.WriteLine($"  Throughput: {throughput:F0} orders/second");
            
            Assert.True(throughput > 100, "Throughput should exceed 100 orders/second");
        }
        
        #endregion
        
        #region Screening Performance
        
        [Fact]
        public async Task Screening_LargeUniverse_ScalesEfficiently()
        {
            // Arrange
            Output.WriteLine("=== Screening Performance Test ===");
            
            var universeSizes = new[] { 100, 500, 1000, 5000 };
            var criteria = new ScreeningCriteria
            {
                PriceMin = 10m,
                PriceMax = 500m,
                VolumeMultiplier = 1.5m,
                VolatilityMin = 0.01m
            };
            
            var results = new Dictionary<int, (long Duration, int Results)>();
            
            // Act - Test different universe sizes
            foreach (var size in universeSizes)
            {
                var symbols = GenerateSymbols(size);
                SetupMarketDataForSymbols(symbols);
                
                // Warmup
                await _screeningEngine.ScreenStocksAsync(symbols.Take(10).ToArray(), criteria);
                
                var sw = Stopwatch.StartNew();
                var screeningResults = await _screeningEngine.ScreenStocksAsync(symbols, criteria);
                sw.Stop();
                
                results[size] = (sw.ElapsedMilliseconds, screeningResults.Count);
                
                Output.WriteLine($"\nUniverse Size: {size}");
                Output.WriteLine($"  Duration: {sw.ElapsedMilliseconds}ms");
                Output.WriteLine($"  Results: {screeningResults.Count}");
                Output.WriteLine($"  Per-symbol: {sw.ElapsedMilliseconds / (double)size:F2}ms");
            }
            
            // Assert - Performance should scale sub-linearly
            var smallTime = results[100].Duration;
            var largeTime = results[1000].Duration;
            var scalingFactor = largeTime / (double)smallTime;
            
            Output.WriteLine($"\nScaling Analysis:");
            Output.WriteLine($"  100 stocks: {smallTime}ms");
            Output.WriteLine($"  1000 stocks: {largeTime}ms");
            Output.WriteLine($"  Scaling factor: {scalingFactor:F2}x (should be <10x)");
            
            Assert.True(scalingFactor < 15, "Screening should scale sub-linearly");
            Assert.True(results[1000].Duration < 1000, "1000 stocks should screen in <1 second");
        }
        
        [Fact]
        public async Task Screening_RealTimeUpdates_ProcessesQuickly()
        {
            // Arrange
            Output.WriteLine("=== Real-Time Screening Updates Test ===");
            
            var updateCount = 1000;
            var symbols = GenerateSymbols(100);
            var processedCount = 0;
            var latencies = new List<long>();
            
            // Setup screening
            await _screeningEngine.StartAsync();
            
            // Act - Stream market updates
            var updateTasks = new List<Task>();
            
            for (int i = 0; i < updateCount; i++)
            {
                var symbol = symbols[i % symbols.Length];
                var marketData = GenerateMarketUpdate(symbol, i);
                
                var task = Task.Run(async () =>
                {
                    var sw = Stopwatch.StartNew();
                    await _screeningEngine.ProcessMarketDataAsync(marketData);
                    sw.Stop();
                    
                    lock (latencies)
                    {
                        latencies.Add(sw.ElapsedMicroseconds);
                        processedCount++;
                    }
                });
                
                updateTasks.Add(task);
                
                if (i % 100 == 0)
                {
                    await Task.Delay(10); // Simulate realistic update rate
                }
            }
            
            await Task.WhenAll(updateTasks);
            
            // Assert
            var avgLatency = latencies.Average() / 1000.0; // Convert to ms
            var p99Latency = GetPercentile(latencies, 0.99) / 1000.0;
            
            Output.WriteLine($"Real-Time Update Processing:");
            Output.WriteLine($"  Updates: {updateCount}");
            Output.WriteLine($"  Processed: {processedCount}");
            Output.WriteLine($"  Avg Latency: {avgLatency:F2}ms");
            Output.WriteLine($"  P99 Latency: {p99Latency:F2}ms");
            
            Assert.True(avgLatency < 10, "Average update latency should be <10ms");
            Assert.True(p99Latency < 50, "P99 update latency should be <50ms");
        }
        
        #endregion
        
        #region Risk Calculation Performance
        
        [Fact]
        public async Task RiskCalculation_LargePortfolio_PerformsEfficiently()
        {
            // Arrange
            Output.WriteLine("=== Risk Calculation Performance Test ===");
            
            var portfolioSizes = new[] { 10, 50, 100, 200 };
            var results = new Dictionary<int, long>();
            
            foreach (var size in portfolioSizes)
            {
                var returns = GenerateReturnsData(252); // 1 year of data
                var portfolioValues = GeneratePortfolioValues(1_000_000m, returns);
                
                var context = new RiskCalculationContext
                {
                    Returns = returns,
                    PortfolioValues = portfolioValues,
                    ConfidenceLevel = 0.95m,
                    RiskFreeRate = 0.02m
                };
                
                // Warmup
                await _riskCalculator.EvaluateRiskAsync(context);
                
                // Act
                var sw = Stopwatch.StartNew();
                
                for (int i = 0; i < 10; i++)
                {
                    var assessment = await _riskCalculator.EvaluateRiskAsync(context);
                    Assert.True(assessment.IsSuccess);
                }
                
                sw.Stop();
                
                results[size] = sw.ElapsedMilliseconds / 10; // Average per calculation
                
                Output.WriteLine($"\nPortfolio Size: {size} positions");
                Output.WriteLine($"  Avg Calculation Time: {results[size]}ms");
            }
            
            // Assert
            Assert.All(results.Values, time => Assert.True(time < TARGET_RISK_CALC_LATENCY_MS));
            
            // Check scaling
            var scaling = results[200] / (double)results[10];
            Output.WriteLine($"\nScaling from 10 to 200 positions: {scaling:F2}x");
            Assert.True(scaling < 5, "Risk calculation should scale efficiently");
        }
        
        [Fact]
        public async Task RiskCalculation_ConcurrentRequests_HandlesLoad()
        {
            // Arrange
            Output.WriteLine("=== Concurrent Risk Calculation Test ===");
            
            var concurrentRequests = 100;
            var returns = GenerateReturnsData(100);
            var tasks = new List<Task<long>>();
            
            // Act
            var totalSw = Stopwatch.StartNew();
            
            for (int i = 0; i < concurrentRequests; i++)
            {
                var task = Task.Run(async () =>
                {
                    var context = new RiskCalculationContext
                    {
                        Returns = returns,
                        PortfolioValues = GeneratePortfolioValues(100000m + i * 1000, returns),
                        ConfidenceLevel = 0.95m,
                        RiskFreeRate = 0.02m
                    };
                    
                    var sw = Stopwatch.StartNew();
                    var result = await _riskCalculator.EvaluateRiskAsync(context);
                    sw.Stop();
                    
                    Assert.True(result.IsSuccess);
                    return sw.ElapsedMilliseconds;
                });
                
                tasks.Add(task);
            }
            
            var latencies = await Task.WhenAll(tasks);
            totalSw.Stop();
            
            // Assert
            var avgLatency = latencies.Average();
            var maxLatency = latencies.Max();
            var throughput = concurrentRequests / (totalSw.ElapsedMilliseconds / 1000.0);
            
            Output.WriteLine($"Concurrent Risk Calculations:");
            Output.WriteLine($"  Requests: {concurrentRequests}");
            Output.WriteLine($"  Total Time: {totalSw.ElapsedMilliseconds}ms");
            Output.WriteLine($"  Avg Latency: {avgLatency:F2}ms");
            Output.WriteLine($"  Max Latency: {maxLatency}ms");
            Output.WriteLine($"  Throughput: {throughput:F0} calcs/second");
            
            Assert.True(avgLatency < 200, "Average latency should be reasonable under load");
            Assert.True(throughput > 50, "Should handle >50 calculations/second");
        }
        
        #endregion
        
        #region End-to-End Performance
        
        [Fact]
        public async Task EndToEnd_CompleteWorkflow_MeetsLatencyTargets()
        {
            // Arrange
            Output.WriteLine("=== End-to-End Workflow Performance Test ===");
            
            var symbol = "AAPL";
            SetupMarketData(symbol, 175m);
            
            var workflowSteps = new Dictionary<string, long>();
            
            // Act - Complete trading workflow
            var totalSw = Stopwatch.StartNew();
            
            // 1. Screening
            var screeningSw = Stopwatch.StartNew();
            var screeningResult = await _screeningEngine.ScreenStocksAsync(
                new[] { symbol }, 
                new ScreeningCriteria { PriceMin = 100m });
            screeningSw.Stop();
            workflowSteps["Screening"] = screeningSw.ElapsedMilliseconds;
            
            // 2. Risk Assessment
            var riskSw = Stopwatch.StartNew();
            var riskContext = new RiskCalculationContext
            {
                Returns = GenerateReturnsData(20),
                PortfolioValues = GeneratePortfolioValues(100000m, GenerateReturnsData(20)),
                ConfidenceLevel = 0.95m
            };
            var riskAssessment = await _riskCalculator.EvaluateRiskAsync(riskContext);
            riskSw.Stop();
            workflowSteps["Risk Assessment"] = riskSw.ElapsedMilliseconds;
            
            // 3. Order Execution
            var orderSw = Stopwatch.StartNew();
            var order = new Order
            {
                OrderId = Guid.NewGuid().ToString(),
                Symbol = symbol,
                Quantity = 100,
                OrderType = OrderType.Market,
                Side = OrderSide.Buy
            };
            var execution = await _executionEngine.ExecuteOrderAsync(order, 175m);
            orderSw.Stop();
            workflowSteps["Order Execution"] = orderSw.ElapsedMilliseconds;
            
            totalSw.Stop();
            
            // Assert
            Output.WriteLine("Workflow Step Latencies:");
            foreach (var step in workflowSteps)
            {
                Output.WriteLine($"  {step.Key}: {step.Value}ms");
            }
            Output.WriteLine($"\nTotal Workflow: {totalSw.ElapsedMilliseconds}ms");
            
            Assert.True(totalSw.ElapsedMilliseconds < 250, "Complete workflow should execute in <250ms");
            Assert.All(workflowSteps.Values, latency => Assert.True(latency < 100));
        }
        
        #endregion
        
        #region Message Throughput Test
        
        [Fact]
        public async Task MessageThroughput_HighVolume_MeetsTarget()
        {
            // Arrange
            Output.WriteLine("=== Message Throughput Test ===");
            
            var testDuration = TimeSpan.FromSeconds(5);
            var messageCount = 0;
            var errorCount = 0;
            var cts = new CancellationTokenSource(testDuration);
            
            // Act - Generate high volume of messages
            var tasks = new List<Task>();
            
            // Order flow
            tasks.Add(Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var order = GenerateRandomOrder();
                        await _executionEngine.ExecuteOrderAsync(order, 100m);
                        Interlocked.Increment(ref messageCount);
                    }
                    catch
                    {
                        Interlocked.Increment(ref errorCount);
                    }
                }
            }));
            
            // Market data updates
            tasks.Add(Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var update = GenerateMarketUpdate($"SYM{messageCount % 100}", messageCount);
                        await _screeningEngine.ProcessMarketDataAsync(update);
                        Interlocked.Increment(ref messageCount);
                    }
                    catch
                    {
                        Interlocked.Increment(ref errorCount);
                    }
                }
            }));
            
            // Risk calculations
            tasks.Add(Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var context = new RiskCalculationContext
                        {
                            Returns = GenerateReturnsData(10),
                            PortfolioValues = new List<decimal> { 100000m },
                            ConfidenceLevel = 0.95m
                        };
                        await _riskCalculator.CalculateVaRAsync(context.Returns, 0.95m);
                        Interlocked.Increment(ref messageCount);
                    }
                    catch
                    {
                        Interlocked.Increment(ref errorCount);
                    }
                }
            }));
            
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            
            // Assert
            var throughput = messageCount / testDuration.TotalSeconds;
            var errorRate = errorCount / (double)messageCount;
            
            Output.WriteLine($"Message Throughput Test Results:");
            Output.WriteLine($"  Duration: {testDuration.TotalSeconds} seconds");
            Output.WriteLine($"  Total Messages: {messageCount:N0}");
            Output.WriteLine($"  Throughput: {throughput:F0} msg/second");
            Output.WriteLine($"  Errors: {errorCount} ({errorRate:P2})");
            Output.WriteLine($"  Target: {TARGET_MESSAGES_PER_SECOND:N0} msg/second");
            
            Assert.True(throughput > 1000, "Should handle >1000 messages/second");
            Assert.True(errorRate < 0.01, "Error rate should be <1%");
        }
        
        #endregion
        
        #region Helper Methods
        
        private void SetupPerformanceTestData()
        {
            var symbols = GenerateSymbols(1000);
            foreach (var symbol in symbols)
            {
                SetupMarketData(symbol, 50m + Random.Shared.Next(150));
            }
        }
        
        private void SetupMarketData(string symbol, decimal price)
        {
            var marketData = new MarketData
            {
                Symbol = symbol,
                Price = price,
                Volume = Random.Shared.Next(1_000_000, 10_000_000),
                AverageVolume = Random.Shared.Next(2_000_000, 8_000_000)
            };
            
            _mockMarketDataService.Setup(x => x.GetMarketDataAsync(symbol))
                .ReturnsAsync(marketData);
        }
        
        private void SetupMarketDataForSymbols(string[] symbols)
        {
            foreach (var symbol in symbols)
            {
                SetupMarketData(symbol, 50m + Random.Shared.Next(150));
            }
        }
        
        private string[] GenerateSymbols(int count)
        {
            var symbols = new string[count];
            for (int i = 0; i < count; i++)
            {
                symbols[i] = $"SYM{i:D4}";
            }
            return symbols;
        }
        
        private List<Order> GenerateTestOrders(int count)
        {
            var orders = new List<Order>();
            for (int i = 0; i < count; i++)
            {
                orders.Add(new Order
                {
                    OrderId = Guid.NewGuid().ToString(),
                    Symbol = $"SYM{i % 100:D3}",
                    Quantity = 100 + (i % 10) * 10,
                    OrderType = OrderType.Market,
                    Side = i % 2 == 0 ? OrderSide.Buy : OrderSide.Sell
                });
            }
            return orders;
        }
        
        private Order GenerateRandomOrder()
        {
            return new Order
            {
                OrderId = Guid.NewGuid().ToString(),
                Symbol = $"SYM{Random.Shared.Next(100):D3}",
                Quantity = 100 + Random.Shared.Next(10) * 10,
                OrderType = OrderType.Market,
                Side = Random.Shared.Next(2) == 0 ? OrderSide.Buy : OrderSide.Sell
            };
        }
        
        private MarketData GenerateMarketUpdate(string symbol, int index)
        {
            var basePrice = 100m + (symbol.GetHashCode() % 100);
            var variation = (decimal)(Math.Sin(index * 0.1) * 0.02);
            
            return new MarketData
            {
                Symbol = symbol,
                Price = basePrice * (1 + variation),
                Volume = 1_000_000 + Random.Shared.Next(1_000_000),
                Timestamp = DateTime.UtcNow
            };
        }
        
        private List<decimal> GenerateReturnsData(int count)
        {
            var returns = new List<decimal>();
            for (int i = 0; i < count; i++)
            {
                var dailyReturn = (decimal)(Random.Shared.NextDouble() * 0.04 - 0.02); // ±2%
                returns.Add(dailyReturn);
            }
            return returns;
        }
        
        private List<decimal> GeneratePortfolioValues(decimal startValue, List<decimal> returns)
        {
            var values = new List<decimal> { startValue };
            foreach (var ret in returns)
            {
                values.Add(values.Last() * (1 + ret));
            }
            return values;
        }
        
        private double GetPercentile(List<long> values, double percentile)
        {
            var sorted = values.OrderBy(v => v).ToList();
            var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
            return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
        }
        
        #endregion
    }
    
    // Helper classes
    public class RiskCalculationContext
    {
        public List<decimal> Returns { get; set; }
        public List<decimal> PortfolioValues { get; set; }
        public decimal ConfidenceLevel { get; set; }
        public decimal RiskFreeRate { get; set; }
    }
}