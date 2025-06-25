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
using TradingPlatform.StrategyEngine.Strategies;
using TradingPlatform.PaperTrading.Services;
using TradingPlatform.RiskManagement.Services;
using TradingPlatform.GoldenRules.Engine;
using TradingPlatform.Messaging.Services;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.Options;
using TradingPlatform.GoldenRules.Models;
using TradingPlatform.TimeSeries.Interfaces;
using Moq;

namespace TradingPlatform.ChaosTests.Resilience
{
    [Collection("Chaos Tests")]
    public class TradingWorkflowResilienceTests : ChaosTestBase
    {
        private readonly ITradingLogger _logger;
        private readonly ICanonicalMessageQueue _messageQueue;
        private readonly Mock<ITimeSeriesService> _mockTimeSeriesService;

        public TradingWorkflowResilienceTests(ITestOutputHelper output) : base(output)
        {
            _logger = GetRequiredService<ITradingLogger>();
            _messageQueue = GetRequiredService<ICanonicalMessageQueue>();
            
            _mockTimeSeriesService = new Mock<ITimeSeriesService>();
            _mockTimeSeriesService.Setup(x => x.WritePointAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult.Success());
        }

        [Fact]
        public async Task CompleteTradeWorkflow_WithChaosInjection_MaintainsConsistency()
        {
            // Arrange
            var successfulTrades = 0;
            var failedTrades = 0;
            var totalTrades = 50;

            // Create services with chaos policies
            var strategy = new MomentumStrategyCanonical(_logger);
            var riskCalculator = new RiskCalculatorCanonical(_logger);
            var executionEngine = new OrderExecutionEngineCanonical(_logger, _messageQueue);
            
            var goldenRulesConfig = Options.Create(new GoldenRulesEngineConfig
            {
                Enabled = true,
                StrictMode = false // Allow some violations in chaos
            });
            var goldenRules = new CanonicalGoldenRulesEngine(_logger, _mockTimeSeriesService.Object, goldenRulesConfig);

            // Initialize services
            await strategy.InitializeAsync(CancellationToken.None);
            await strategy.StartAsync(CancellationToken.None);
            await riskCalculator.InitializeAsync(CancellationToken.None);
            await riskCalculator.StartAsync(CancellationToken.None);
            await executionEngine.InitializeAsync(CancellationToken.None);
            await executionEngine.StartAsync(CancellationToken.None);
            await goldenRules.InitializeAsync(CancellationToken.None);
            await goldenRules.StartAsync(CancellationToken.None);

            // Create chaos policies
            var networkChaos = CreateExceptionChaosPolicy<TradingResult<TradingSignal>>(
                injectionRate: 0.1,
                exceptionFactory: (ctx, ct) => new System.Net.Http.HttpRequestException("Network timeout"));

            var latencyChaos = CreateLatencyChaosPolicy<TradingResult<bool>>(
                injectionRate: 0.2,
                latency: TimeSpan.FromSeconds(2));

            // Act - Execute trades with chaos
            var tasks = new Task[totalTrades];
            for (int i = 0; i < totalTrades; i++)
            {
                var tradeIndex = i;
                tasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        // Generate market data
                        var marketData = new MarketData
                        {
                            Symbol = $"SYM{tradeIndex % 10}",
                            Timestamp = DateTime.UtcNow,
                            Open = 100m + ChaosRandom.Next(-10, 10),
                            High = 110m + ChaosRandom.Next(-5, 5),
                            Low = 90m + ChaosRandom.Next(-5, 5),
                            Close = 105m + ChaosRandom.Next(-10, 10),
                            Volume = 1000000 + ChaosRandom.Next(-500000, 500000),
                            Volatility = 0.02m,
                            RSI = 50m + ChaosRandom.Next(-20, 20)
                        };

                        // Step 1: Generate signal with network chaos
                        var signalResult = await networkChaos.ExecuteAsync(async () =>
                            await strategy.ExecuteStrategyAsync(marketData.Symbol, marketData, null, CancellationToken.None));

                        if (!signalResult.IsSuccess)
                        {
                            Interlocked.Increment(ref failedTrades);
                            return;
                        }

                        var signal = signalResult.Value;

                        // Step 2: Validate with Golden Rules (with latency)
                        var validationResult = await latencyChaos.ExecuteAsync(async () =>
                            await goldenRules.ValidateTradeAsync(
                                signal.Symbol,
                                OrderType.Market,
                                signal.SignalType == SignalType.Buy ? OrderSide.Buy : OrderSide.Sell,
                                signal.Quantity,
                                signal.Price,
                                CancellationToken.None));

                        if (!validationResult.IsSuccess || !validationResult.Value)
                        {
                            Interlocked.Increment(ref failedTrades);
                            return;
                        }

                        // Step 3: Calculate risk
                        var riskResult = await riskCalculator.CalculateRiskAsync(
                            signal.Symbol,
                            signal.Quantity,
                            signal.Price,
                            signal.SignalType == SignalType.Buy ? OrderSide.Buy : OrderSide.Sell,
                            CancellationToken.None);

                        if (!riskResult.IsSuccess || !riskResult.Value.IsWithinLimits)
                        {
                            Interlocked.Increment(ref failedTrades);
                            return;
                        }

                        // Step 4: Execute order
                        var order = new Order
                        {
                            Id = Guid.NewGuid().ToString(),
                            Symbol = signal.Symbol,
                            OrderType = OrderType.Market,
                            Side = signal.SignalType == SignalType.Buy ? OrderSide.Buy : OrderSide.Sell,
                            Quantity = signal.Quantity,
                            Price = signal.Price,
                            Status = OrderStatus.Pending,
                            CreatedAt = DateTime.UtcNow
                        };

                        var executionResult = await executionEngine.ExecuteOrderAsync(order, CancellationToken.None);
                        
                        if (executionResult.IsSuccess)
                        {
                            Interlocked.Increment(ref successfulTrades);
                        }
                        else
                        {
                            Interlocked.Increment(ref failedTrades);
                        }
                    }
                    catch (Exception ex)
                    {
                        Output.WriteLine($"Trade {tradeIndex} failed with: {ex.Message}");
                        Interlocked.Increment(ref failedTrades);
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Cleanup
            await strategy.StopAsync(CancellationToken.None);
            await riskCalculator.StopAsync(CancellationToken.None);
            await executionEngine.StopAsync(CancellationToken.None);
            await goldenRules.StopAsync(CancellationToken.None);

            // Assert
            var totalProcessed = successfulTrades + failedTrades;
            totalProcessed.Should().Be(totalTrades);
            
            var successRate = (double)successfulTrades / totalTrades;
            successRate.Should().BeGreaterThan(0.7); // At least 70% success despite chaos
            
            Output.WriteLine($"Trade success rate under chaos: {successRate:P2} ({successfulTrades}/{totalTrades})");
        }

        [Fact]
        public async Task TradingSystem_WithCascadingFailures_RecoversProperly()
        {
            // Arrange
            var systemStates = new List<string>();
            var recoveryTime = TimeSpan.Zero;
            var failureStartTime = DateTime.UtcNow;

            // Create interconnected services
            var services = new List<ICanonicalService>
            {
                new MomentumStrategyCanonical(_logger),
                new GapStrategyCanonical(_logger),
                new RiskCalculatorCanonical(_logger),
                new OrderExecutionEngineCanonical(_logger, _messageQueue)
            };

            // Initialize all services
            foreach (var service in services)
            {
                await service.InitializeAsync(CancellationToken.None);
                await service.StartAsync(CancellationToken.None);
            }

            // Act - Simulate cascading failure
            Output.WriteLine("Initiating cascading failure...");
            
            // Step 1: Fail primary service
            await services[0].StopAsync(CancellationToken.None);
            systemStates.Add("Primary service failed");

            // Step 2: Cascade to dependent services
            await Task.Delay(1000);
            for (int i = 1; i < services.Count / 2; i++)
            {
                await services[i].StopAsync(CancellationToken.None);
                systemStates.Add($"Service {i} failed due to cascade");
            }

            // Step 3: Begin recovery
            await Task.Delay(2000);
            Output.WriteLine("Beginning recovery...");
            var recoveryStartTime = DateTime.UtcNow;

            // Restart services in dependency order
            foreach (var service in services)
            {
                if (service.State != ServiceState.Running)
                {
                    var restartResult = await service.StartAsync(CancellationToken.None);
                    if (restartResult.IsSuccess)
                    {
                        systemStates.Add($"{service.GetType().Name} recovered");
                    }
                }
            }

            recoveryTime = DateTime.UtcNow - recoveryStartTime;

            // Verify system health after recovery
            var healthyServices = 0;
            foreach (var service in services)
            {
                var healthResult = await service.CheckHealthAsync(CancellationToken.None);
                if (healthResult.IsHealthy)
                {
                    healthyServices++;
                }
            }

            // Assert
            healthyServices.Should().Be(services.Count);
            recoveryTime.Should().BeLessThan(TimeSpan.FromSeconds(10));
            systemStates.Should().Contain(s => s.Contains("recovered"));
            
            Output.WriteLine($"System recovered in {recoveryTime.TotalSeconds:F2} seconds");
            Output.WriteLine($"Recovery sequence: {string.Join(" -> ", systemStates)}");
        }

        [Fact]
        public async Task TradingSystem_UnderSustainedLoad_WithFailures_MaintainsPerformance()
        {
            // Arrange
            var metricsCollector = new MetricsCollector();
            var loadDuration = TimeSpan.FromSeconds(30);
            var targetTps = 100; // Transactions per second
            
            // Create services
            var strategy = new MomentumStrategyCanonical(_logger);
            var executionEngine = new OrderExecutionEngineCanonical(_logger, _messageQueue);
            
            await strategy.InitializeAsync(CancellationToken.None);
            await strategy.StartAsync(CancellationToken.None);
            await executionEngine.InitializeAsync(CancellationToken.None);
            await executionEngine.StartAsync(CancellationToken.None);

            // Create chaos policy that degrades over time
            var degradationRate = 0.0;
            var degradationChaos = CreateExceptionChaosPolicy<TradingResult<OrderExecution>>(
                injectionRate: 0.0, // Starts at 0
                exceptionFactory: (ctx, ct) =>
                {
                    degradationRate = Math.Min(0.3, degradationRate + 0.001); // Increase failure rate
                    return new InvalidOperationException("Service degradation");
                },
                enabled: true);

            // Act - Generate sustained load
            var cts = new CancellationTokenSource(loadDuration);
            var loadTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var tasks = new Task[targetTps / 10]; // Batch of 10
                    
                    for (int i = 0; i < tasks.Length; i++)
                    {
                        tasks[i] = Task.Run(async () =>
                        {
                            var startTime = DateTime.UtcNow;
                            
                            try
                            {
                                var order = new Order
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Symbol = $"SYM{ChaosRandom.Next(10)}",
                                    OrderType = OrderType.Market,
                                    Side = OrderSide.Buy,
                                    Quantity = 100,
                                    Price = 100m + ChaosRandom.Next(-10, 10),
                                    Status = OrderStatus.Pending,
                                    CreatedAt = DateTime.UtcNow
                                };

                                var result = await degradationChaos.ExecuteAsync(async () =>
                                    await executionEngine.ExecuteOrderAsync(order, CancellationToken.None));

                                var latency = (DateTime.UtcNow - startTime).TotalMilliseconds;
                                
                                if (result.IsSuccess)
                                {
                                    metricsCollector.RecordSuccess(latency);
                                }
                                else
                                {
                                    metricsCollector.RecordFailure(latency);
                                }
                            }
                            catch
                            {
                                var latency = (DateTime.UtcNow - startTime).TotalMilliseconds;
                                metricsCollector.RecordFailure(latency);
                            }
                        });
                    }

                    await Task.WhenAll(tasks);
                    await Task.Delay(100); // 10 batches per second
                }
            });

            await loadTask;

            // Cleanup
            await strategy.StopAsync(CancellationToken.None);
            await executionEngine.StopAsync(CancellationToken.None);

            // Assert
            var metrics = metricsCollector.GetMetrics();
            
            metrics.SuccessRate.Should().BeGreaterThan(0.7); // 70% success despite degradation
            metrics.MedianLatency.Should().BeLessThan(100); // Under 100ms median
            metrics.P99Latency.Should().BeLessThan(1000); // Under 1s for 99th percentile
            
            Output.WriteLine($"Performance under sustained load with failures:");
            Output.WriteLine($"  Success Rate: {metrics.SuccessRate:P2}");
            Output.WriteLine($"  Median Latency: {metrics.MedianLatency:F2}ms");
            Output.WriteLine($"  P99 Latency: {metrics.P99Latency:F2}ms");
            Output.WriteLine($"  Total Transactions: {metrics.TotalTransactions}");
        }

        [Fact]
        public async Task TradingSystem_WithTimeSkew_HandlesCorrectly()
        {
            // Arrange
            var skewedResults = new List<bool>();
            var skewAmount = TimeSpan.FromMinutes(5);
            
            var strategy = new MomentumStrategyCanonical(_logger);
            await strategy.InitializeAsync(CancellationToken.None);
            await strategy.StartAsync(CancellationToken.None);

            // Act - Test with various time skews
            var skews = new[] { -skewAmount, TimeSpan.Zero, skewAmount };
            
            foreach (var skew in skews)
            {
                var skewedTime = SimulateClockSkew(skew);
                
                var marketData = new MarketData
                {
                    Symbol = "AAPL",
                    Timestamp = skewedTime.DateTime,
                    Open = 150m,
                    High = 155m,
                    Low = 149m,
                    Close = 154m,
                    Volume = 50000000,
                    RSI = 60m
                };

                var result = await strategy.ExecuteStrategyAsync("AAPL", marketData, null, CancellationToken.None);
                skewedResults.Add(result.IsSuccess);
                
                Output.WriteLine($"Time skew {skew}: Result = {result.IsSuccess}");
            }

            await strategy.StopAsync(CancellationToken.None);

            // Assert - System should handle time skew gracefully
            skewedResults.Should().Contain(true); // At least some should succeed
            skewedResults.Count(r => r).Should().BeGreaterOrEqualTo(2); // Most should work
        }

        private class MetricsCollector
        {
            private readonly List<double> _successLatencies = new();
            private readonly List<double> _failureLatencies = new();
            private int _successCount;
            private int _failureCount;

            public void RecordSuccess(double latencyMs)
            {
                lock (_successLatencies)
                {
                    _successLatencies.Add(latencyMs);
                    _successCount++;
                }
            }

            public void RecordFailure(double latencyMs)
            {
                lock (_failureLatencies)
                {
                    _failureLatencies.Add(latencyMs);
                    _failureCount++;
                }
            }

            public PerformanceMetrics GetMetrics()
            {
                lock (_successLatencies)
                {
                    var allLatencies = _successLatencies.Concat(_failureLatencies).OrderBy(l => l).ToList();
                    
                    return new PerformanceMetrics
                    {
                        TotalTransactions = _successCount + _failureCount,
                        SuccessRate = (double)_successCount / (_successCount + _failureCount),
                        MedianLatency = allLatencies.Any() ? allLatencies[allLatencies.Count / 2] : 0,
                        P99Latency = allLatencies.Any() ? allLatencies[(int)(allLatencies.Count * 0.99)] : 0
                    };
                }
            }
        }

        private class PerformanceMetrics
        {
            public int TotalTransactions { get; set; }
            public double SuccessRate { get; set; }
            public double MedianLatency { get; set; }
            public double P99Latency { get; set; }
        }
    }
}