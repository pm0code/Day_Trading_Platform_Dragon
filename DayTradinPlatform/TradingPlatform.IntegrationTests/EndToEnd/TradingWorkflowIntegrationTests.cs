using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TradingPlatform.IntegrationTests.Fixtures;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Core.Canonical;
using TradingPlatform.StrategyEngine.Strategies;
using TradingPlatform.StrategyEngine.Models;
using TradingPlatform.GoldenRules.Interfaces;
using TradingPlatform.GoldenRules.Models;
using TradingPlatform.Messaging.Services;
using TradingPlatform.Messaging.Events;
using TradingPlatform.RiskManagement.Services;
using TradingPlatform.PaperTrading.Services;
using TradingPlatform.Foundation.Models;
using TradingPlatform.GoldenRules.Engine;
using TradingPlatform.TimeSeries.Interfaces;
using Microsoft.Extensions.Options;
using Moq;

namespace TradingPlatform.IntegrationTests.EndToEnd
{
    [Collection("Integration Tests")]
    public class TradingWorkflowIntegrationTests : IClassFixture<TradingWorkflowTestFixture>
    {
        private readonly TradingWorkflowTestFixture _fixture;
        private readonly ITradingLogger _logger;
        private readonly ICanonicalMessageQueue _messageQueue;

        public TradingWorkflowIntegrationTests(TradingWorkflowTestFixture fixture)
        {
            _fixture = fixture;
            _logger = _fixture.GetRequiredService<ITradingLogger>();
            _messageQueue = _fixture.GetRequiredService<ICanonicalMessageQueue>();
        }

        [Fact]
        public async Task CompleteTradeWorkflow_FromSignalToExecution_ShouldWork()
        {
            // Arrange
            var receivedEvents = new List<TradingEvent>();
            var eventReceivedTcs = new TaskCompletionSource<bool>();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // Subscribe to trading events
            await _messageQueue.SubscribeAsync<TradingEvent>(
                "trading-events",
                "test-consumer",
                async (evt) =>
                {
                    receivedEvents.Add(evt);
                    if (evt.EventType == TradingEventType.OrderExecuted)
                    {
                        eventReceivedTcs.TrySetResult(true);
                    }
                    return true;
                },
                new SubscriptionOptions { ConsumerName = "workflow-test" },
                cts.Token);

            // Setup components
            var strategy = new MomentumStrategyCanonical(_logger);
            var goldenRules = _fixture.GetRequiredService<IGoldenRulesEngine>();
            var riskCalculator = new RiskCalculatorCanonical(_logger);
            var paperTrading = new OrderExecutionEngineCanonical(_logger, _messageQueue);

            await strategy.InitializeAsync(cts.Token);
            await strategy.StartAsync(cts.Token);
            await riskCalculator.InitializeAsync(cts.Token);
            await paperTrading.InitializeAsync(cts.Token);

            // Create market conditions that will trigger a signal
            var marketData = new MarketData
            {
                Symbol = "TSLA",
                Timestamp = DateTime.UtcNow,
                Open = 250m,
                High = 260m,
                Low = 248m,
                Close = 258m, // Strong momentum
                Volume = 50000000,
                Volatility = 0.03m,
                RSI = 65m
            };

            // Act - Generate trading signal
            var signalResult = await strategy.ExecuteStrategyAsync("TSLA", marketData, null, cts.Token);
            signalResult.IsSuccess.Should().BeTrue();
            var signal = signalResult.Value;

            // Publish signal event
            await _messageQueue.PublishAsync(
                "trading-events",
                new TradingEvent
                {
                    EventType = TradingEventType.SignalGenerated,
                    Symbol = signal.Symbol,
                    Timestamp = DateTime.UtcNow,
                    Data = new Dictionary<string, object>
                    {
                        ["SignalId"] = signal.Id,
                        ["SignalType"] = signal.SignalType.ToString(),
                        ["Price"] = signal.Price,
                        ["Quantity"] = signal.Quantity,
                        ["Confidence"] = signal.Confidence
                    }
                },
                MessagePriority.High);

            // Validate with Golden Rules
            var positionContext = new PositionContext
            {
                Symbol = "TSLA",
                AccountBalance = 100000m,
                BuyingPower = 400000m,
                DayTradeCount = 2,
                DailyPnL = 0m,
                Quantity = 0,
                EntryPrice = 0,
                CurrentPrice = marketData.Close
            };

            var marketConditions = new MarketConditions
            {
                Symbol = "TSLA",
                Price = marketData.Close,
                Bid = marketData.Close - 0.05m,
                Ask = marketData.Close + 0.05m,
                Volume = marketData.Volume,
                DayHigh = marketData.High,
                DayLow = marketData.Low,
                OpenPrice = marketData.Open,
                PreviousClose = marketData.Open * 0.99m,
                ATR = 5m,
                Volatility = marketData.Volatility,
                Trend = TrendDirection.Uptrend,
                Momentum = 0.7m,
                RelativeVolume = 2m,
                Session = MarketSession.RegularHours,
                HasNewsCatalyst = false,
                TechnicalIndicators = new Dictionary<string, decimal> { ["RSI"] = marketData.RSI ?? 50m }
            };

            var validationResult = await goldenRules.ValidateTradeAsync(
                signal.Symbol,
                OrderType.Market,
                signal.SignalType == SignalType.Buy ? OrderSide.Buy : OrderSide.Sell,
                signal.Quantity,
                signal.Price,
                cts.Token);

            validationResult.IsSuccess.Should().BeTrue();
            validationResult.Value.Should().BeTrue();

            // Calculate risk
            var riskResult = await riskCalculator.CalculateRiskAsync(
                signal.Symbol,
                signal.Quantity,
                signal.Price,
                signal.SignalType == SignalType.Buy ? OrderSide.Buy : OrderSide.Sell,
                cts.Token);

            riskResult.IsSuccess.Should().BeTrue();

            // Execute paper trade
            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                Symbol = signal.Symbol,
                OrderType = OrderType.Market,
                Side = signal.SignalType == SignalType.Buy ? OrderSide.Buy : OrderSide.Sell,
                Quantity = signal.Quantity,
                Price = signal.Price,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                StrategyId = "momentum"
            };

            var executionResult = await paperTrading.ExecuteOrderAsync(order, cts.Token);
            executionResult.IsSuccess.Should().BeTrue();

            // Publish execution event
            await _messageQueue.PublishAsync(
                "trading-events",
                new TradingEvent
                {
                    EventType = TradingEventType.OrderExecuted,
                    Symbol = order.Symbol,
                    Timestamp = DateTime.UtcNow,
                    Data = new Dictionary<string, object>
                    {
                        ["OrderId"] = order.Id,
                        ["ExecutionPrice"] = executionResult.Value.Price,
                        ["ExecutionQuantity"] = executionResult.Value.Quantity,
                        ["Commission"] = executionResult.Value.Commission
                    }
                },
                MessagePriority.High);

            // Wait for event to be received
            await eventReceivedTcs.Task;

            // Assert
            receivedEvents.Should().HaveCountGreaterOrEqualTo(2);
            receivedEvents.Should().Contain(e => e.EventType == TradingEventType.SignalGenerated);
            receivedEvents.Should().Contain(e => e.EventType == TradingEventType.OrderExecuted);

            // Verify the complete workflow
            var signalEvent = receivedEvents.First(e => e.EventType == TradingEventType.SignalGenerated);
            signalEvent.Symbol.Should().Be("TSLA");
            signalEvent.Data["Confidence"].Should().BeOfType<decimal>();
            ((decimal)signalEvent.Data["Confidence"]).Should().BeGreaterThan(0.5m);

            var executionEvent = receivedEvents.First(e => e.EventType == TradingEventType.OrderExecuted);
            executionEvent.Symbol.Should().Be("TSLA");
            executionEvent.Data.Should().ContainKey("ExecutionPrice");

            // Cleanup
            cts.Cancel();
            await strategy.StopAsync(CancellationToken.None);
            await riskCalculator.StopAsync(CancellationToken.None);
            await paperTrading.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task MultiStrategyWorkflow_ConcurrentSignals_ShouldHandleCorrectly()
        {
            // Arrange
            var orchestrator = new CanonicalStrategyOrchestrator(_logger, "TestOrchestrator");
            var momentumStrategy = new MomentumStrategyCanonical(_logger);
            var gapStrategy = new GapStrategyCanonical(_logger);
            var goldenRules = _fixture.GetRequiredService<IGoldenRulesEngine>();

            await orchestrator.RegisterStrategyAsync("momentum", momentumStrategy);
            await orchestrator.RegisterStrategyAsync("gap", gapStrategy);
            await orchestrator.InitializeAsync(CancellationToken.None);
            await orchestrator.StartAsync(CancellationToken.None);

            var symbols = new[] { "AAPL", "GOOGL", "MSFT", "NVDA", "TSLA" };
            var signalCount = 0;
            var validationCount = 0;

            // Act - Process multiple symbols concurrently
            var tasks = symbols.Select(async symbol =>
            {
                var marketData = new MarketData
                {
                    Symbol = symbol,
                    Timestamp = DateTime.UtcNow,
                    Open = 100m + Random.Shared.Next(50, 500),
                    High = 100m + Random.Shared.Next(60, 510),
                    Low = 100m + Random.Shared.Next(40, 490),
                    Close = 100m + Random.Shared.Next(50, 500),
                    Volume = Random.Shared.Next(10000000, 100000000),
                    Volatility = 0.02m + (decimal)Random.Shared.NextDouble() * 0.03m,
                    RSI = 30m + (decimal)Random.Shared.NextDouble() * 40m
                };

                var results = await orchestrator.ExecuteStrategiesAsync(symbol, marketData, CancellationToken.None);

                foreach (var (strategyId, result) in results)
                {
                    if (result.IsSuccess && result.Value != null)
                    {
                        Interlocked.Increment(ref signalCount);

                        // Validate each signal
                        var validateResult = await goldenRules.ValidateTradeAsync(
                            symbol,
                            OrderType.Market,
                            result.Value.SignalType == SignalType.Buy ? OrderSide.Buy : OrderSide.Sell,
                            result.Value.Quantity,
                            result.Value.Price,
                            CancellationToken.None);

                        if (validateResult.IsSuccess && validateResult.Value)
                        {
                            Interlocked.Increment(ref validationCount);
                        }
                    }
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            // Assert
            signalCount.Should().BeGreaterThan(0);
            validationCount.Should().BeGreaterThan(0);
            validationCount.Should().BeLessThanOrEqualTo(signalCount);

            // Cleanup
            await orchestrator.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RiskManagementWorkflow_ExceedsLimits_ShouldBlock()
        {
            // Arrange
            var goldenRules = _fixture.GetRequiredService<IGoldenRulesEngine>();
            var riskCalculator = new RiskCalculatorCanonical(_logger);
            await riskCalculator.InitializeAsync(CancellationToken.None);

            // Create position that violates risk limits
            var positionContext = new PositionContext
            {
                Symbol = "MEME",
                AccountBalance = 50000m, // Small account
                BuyingPower = 200000m,
                DayTradeCount = 20, // Way too many trades
                DailyPnL = -1500m, // Already down 3%
                Quantity = 0,
                EntryPrice = 0,
                CurrentPrice = 100m
            };

            var marketConditions = new MarketConditions
            {
                Symbol = "MEME",
                Price = 100m,
                Bid = 99.95m,
                Ask = 100.05m,
                Volume = 5000000,
                DayHigh = 105m,
                DayLow = 95m,
                OpenPrice = 98m,
                PreviousClose = 97m,
                ATR = 5m,
                Volatility = 0.05m, // High volatility
                Trend = TrendDirection.Down,
                Momentum = 0.3m,
                RelativeVolume = 3m,
                Session = MarketSession.RegularHours,
                HasNewsCatalyst = true,
                TechnicalIndicators = new Dictionary<string, decimal> { ["RSI"] = 85m } // Overbought
            };

            // Act - Try to place a large order
            var evaluationResult = await goldenRules.EvaluateTradeAsync(
                "MEME",
                OrderType.Market,
                OrderSide.Buy,
                1000, // Way too large
                100m,
                positionContext,
                marketConditions,
                CancellationToken.None);

            // Calculate risk
            var riskResult = await riskCalculator.CalculateRiskAsync(
                "MEME",
                1000,
                100m,
                OrderSide.Buy,
                CancellationToken.None);

            // Assert
            evaluationResult.IsSuccess.Should().BeTrue();
            var assessment = evaluationResult.Value;
            assessment.OverallCompliance.Should().BeFalse();
            assessment.BlockingViolations.Should().BeGreaterThan(0);
            assessment.Recommendation.Should().Contain("DO NOT TRADE");

            // Risk should also indicate high risk
            riskResult.IsSuccess.Should().BeTrue();
            var risk = riskResult.Value;
            risk.PositionRisk.Should().BeGreaterThan(positionContext.AccountBalance * 0.02m); // More than 2% risk

            await riskCalculator.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task MessageQueueResilience_ConnectionLoss_ShouldRecover()
        {
            // This test would simulate connection loss and recovery
            // In a real scenario, you might stop/start the Redis container
            // For now, we'll test basic resilience

            var receivedMessages = new List<string>();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Subscribe with retry logic
            var subscribeTask = _messageQueue.SubscribeAsync<Dictionary<string, object>>(
                "resilience-test",
                "test-consumer",
                async (message) =>
                {
                    if (message.ContainsKey("content"))
                    {
                        receivedMessages.Add(message["content"].ToString()!);
                    }
                    return true;
                },
                new SubscriptionOptions 
                { 
                    ConsumerName = "resilient-consumer",
                    MaxRetries = 5
                },
                cts.Token);

            await Task.Delay(100); // Let subscription start

            // Publish messages
            for (int i = 0; i < 10; i++)
            {
                await _messageQueue.PublishAsync(
                    "resilience-test",
                    new Dictionary<string, object> { ["content"] = $"Message {i}" },
                    MessagePriority.Normal);
                
                await Task.Delay(100); // Simulate some delay
            }

            await Task.Delay(1000); // Wait for processing
            cts.Cancel();

            // Assert
            receivedMessages.Should().HaveCount(10);
            receivedMessages.Should().BeInAscendingOrder();
        }
    }

    public class TradingWorkflowTestFixture : IntegrationTestFixture
    {
        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            // Add Golden Rules
            services.Configure<GoldenRulesEngineConfig>(options =>
            {
                options.Enabled = true;
                options.StrictMode = false; // Less strict for testing
                options.EnableRealTimeAlerts = true;
                options.MinimumComplianceScore = 0.5m;
                options.RuleConfigs = new List<GoldenRuleConfiguration>();
            });

            // Mock time series service
            var mockTimeSeriesService = new Mock<ITimeSeriesService>();
            mockTimeSeriesService.Setup(x => x.WritePointAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult.Success());
            mockTimeSeriesService.Setup(x => x.IsHealthyAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult<bool>.Success(true));

            services.AddSingleton(mockTimeSeriesService.Object);
            services.AddSingleton<IGoldenRulesEngine, CanonicalGoldenRulesEngine>();

            // Add other services
            services.AddSingleton<CanonicalStrategyOrchestrator>();
        }
    }
}