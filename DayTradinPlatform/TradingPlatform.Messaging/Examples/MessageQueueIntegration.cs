using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Messaging.Events;
using TradingPlatform.Messaging.Services;

namespace TradingPlatform.Messaging.Examples
{
    /// <summary>
    /// Comprehensive example demonstrating canonical message queue integration
    /// for various trading platform scenarios
    /// </summary>
    public class MessageQueueIntegration
    {
        /// <summary>
        /// Example 1: Market Data Publisher Service
        /// Demonstrates high-frequency market data publishing with priority levels
        /// </summary>
        public class MarketDataPublisher : BackgroundService
        {
            private readonly ICanonicalMessageQueue _messageQueue;
            private readonly ITradingLogger _logger;

            public MarketDataPublisher(
                ICanonicalMessageQueue messageQueue,
                ITradingLogger logger)
            {
                _messageQueue = messageQueue;
                _logger = logger;
            }

            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                var symbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA" };
                var random = new Random();

                while (!stoppingToken.IsCancellationRequested)
                {
                    foreach (var symbol in symbols)
                    {
                        var marketData = new MarketDataEvent
                        {
                            Symbol = symbol,
                            Price = 100m + (decimal)(random.NextDouble() * 50),
                            Bid = 99m + (decimal)(random.NextDouble() * 50),
                            Ask = 101m + (decimal)(random.NextDouble() * 50),
                            Volume = random.Next(1000000, 10000000),
                            High = 105m + (decimal)(random.NextDouble() * 45),
                            Low = 95m + (decimal)(random.NextDouble() * 55),
                            DataType = MarketDataType.Quote,
                            Source = "MarketDataPublisher"
                        };

                        // Publish with appropriate priority based on volatility
                        var volatility = Math.Abs(marketData.High - marketData.Low) / marketData.Price;
                        var priority = volatility > 0.05m 
                            ? MessagePriority.High 
                            : MessagePriority.Normal;

                        var result = await _messageQueue.PublishAsync(
                            "market-data",
                            marketData,
                            priority,
                            stoppingToken);

                        if (!result.IsSuccess)
                        {
                            _logger.LogError("Failed to publish market data", null,
                                additionalData: new { Symbol = symbol, Error = result.Error });
                        }
                    }

                    // Simulate market data rate (100ms = 10 updates/second per symbol)
                    await Task.Delay(100, stoppingToken);
                }
            }
        }

        /// <summary>
        /// Example 2: Order Processing Service
        /// Demonstrates order event handling with acknowledgment patterns
        /// </summary>
        public class OrderProcessingService : IHostedService
        {
            private readonly ICanonicalMessageQueue _messageQueue;
            private readonly ITradingLogger _logger;
            private Task? _processingTask;

            public OrderProcessingService(
                ICanonicalMessageQueue messageQueue,
                ITradingLogger logger)
            {
                _messageQueue = messageQueue;
                _logger = logger;
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                // Subscribe to order events
                var result = await _messageQueue.SubscribeAsync<OrderEvent>(
                    "orders",
                    "order-processors",
                    ProcessOrderAsync,
                    new SubscriptionOptions
                    {
                        ConsumerName = $"order-processor-{Environment.MachineName}",
                        MaxRetries = 3,
                        EnableDeadLetterQueue = true
                    },
                    cancellationToken);

                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException($"Failed to subscribe to orders: {result.Error}");
                }

                _logger.LogInfo("Order processing service started");
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return _messageQueue.UnsubscribeAsync(
                    "orders",
                    "order-processors",
                    $"order-processor-{Environment.MachineName}");
            }

            private async Task<bool> ProcessOrderAsync(OrderEvent orderEvent, CancellationToken cancellationToken)
            {
                try
                {
                    _logger.LogInfo($"Processing order {orderEvent.OrderId}",
                        additionalData: new 
                        { 
                            Symbol = orderEvent.Symbol,
                            Action = orderEvent.Action,
                            Quantity = orderEvent.Quantity,
                            Price = orderEvent.Price
                        });

                    // Simulate order validation and processing
                    await Task.Delay(10, cancellationToken);

                    // Publish fill event if order is executed
                    if (orderEvent.Action == OrderAction.Submitted)
                    {
                        var fillEvent = new FillEvent
                        {
                            OrderId = orderEvent.OrderId,
                            FillId = Guid.NewGuid().ToString(),
                            Symbol = orderEvent.Symbol,
                            Quantity = orderEvent.Quantity,
                            Price = orderEvent.Price,
                            Commission = orderEvent.Quantity * 0.001m, // $0.001 per share
                            Side = orderEvent.Side,
                            IsPartial = false,
                            Source = "OrderProcessor",
                            CorrelationId = orderEvent.CorrelationId
                        };

                        await _messageQueue.PublishAsync(
                            "fills",
                            fillEvent,
                            MessagePriority.High,
                            cancellationToken);
                    }

                    return true; // Message processed successfully
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing order {orderEvent.OrderId}", ex);
                    return false; // Message will be retried
                }
            }
        }

        /// <summary>
        /// Example 3: Risk Monitoring Service
        /// Demonstrates aggregated event processing and alert generation
        /// </summary>
        public class RiskMonitoringService : IHostedService
        {
            private readonly ICanonicalMessageQueue _messageQueue;
            private readonly ITradingLogger _logger;
            private readonly Dictionary<string, decimal> _positionValues = new();

            public RiskMonitoringService(
                ICanonicalMessageQueue messageQueue,
                ITradingLogger logger)
            {
                _messageQueue = messageQueue;
                _logger = logger;
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                // Subscribe to multiple event streams for risk monitoring
                var subscriptions = new[]
                {
                    _messageQueue.SubscribeAsync<PositionEvent>(
                        "positions",
                        "risk-monitors",
                        ProcessPositionUpdateAsync,
                        new SubscriptionOptions { ConsumerName = "risk-monitor-positions" },
                        cancellationToken),

                    _messageQueue.SubscribeAsync<FillEvent>(
                        "fills",
                        "risk-monitors",
                        ProcessFillEventAsync,
                        new SubscriptionOptions { ConsumerName = "risk-monitor-fills" },
                        cancellationToken),

                    _messageQueue.SubscribeAsync<MarketDataEvent>(
                        "market-data",
                        "risk-monitors",
                        ProcessMarketDataAsync,
                        new SubscriptionOptions { ConsumerName = "risk-monitor-market" },
                        cancellationToken)
                };

                await Task.WhenAll(subscriptions);
                _logger.LogInfo("Risk monitoring service started");
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                var unsubscribeTasks = new[]
                {
                    _messageQueue.UnsubscribeAsync("positions", "risk-monitors", "risk-monitor-positions"),
                    _messageQueue.UnsubscribeAsync("fills", "risk-monitors", "risk-monitor-fills"),
                    _messageQueue.UnsubscribeAsync("market-data", "risk-monitors", "risk-monitor-market")
                };

                return Task.WhenAll(unsubscribeTasks);
            }

            private async Task<bool> ProcessPositionUpdateAsync(PositionEvent positionEvent, CancellationToken cancellationToken)
            {
                try
                {
                    lock (_positionValues)
                    {
                        _positionValues[positionEvent.Symbol] = 
                            positionEvent.Quantity * positionEvent.CurrentPrice;
                    }

                    // Check portfolio concentration
                    var totalValue = 0m;
                    lock (_positionValues)
                    {
                        totalValue = _positionValues.Values.Sum();
                    }

                    if (totalValue > 0)
                    {
                        var concentration = _positionValues[positionEvent.Symbol] / totalValue;
                        if (concentration > 0.2m) // 20% concentration limit
                        {
                            var riskEvent = new RiskEvent
                            {
                                AlertId = Guid.NewGuid().ToString(),
                                AlertType = RiskAlertType.ConcentrationLimit,
                                Symbol = positionEvent.Symbol,
                                Message = $"Position concentration {concentration:P1} exceeds 20% limit",
                                Severity = RiskSeverity.Warning,
                                Metrics = new Dictionary<string, decimal>
                                {
                                    ["Concentration"] = concentration,
                                    ["PositionValue"] = _positionValues[positionEvent.Symbol],
                                    ["TotalPortfolioValue"] = totalValue
                                },
                                Source = "RiskMonitor"
                            };

                            await _messageQueue.PublishAsync(
                                "risk-alerts",
                                riskEvent,
                                MessagePriority.High,
                                cancellationToken);
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error processing position update", ex);
                    return false;
                }
            }

            private Task<bool> ProcessFillEventAsync(FillEvent fillEvent, CancellationToken cancellationToken)
            {
                // Process fill events for risk metrics
                return Task.FromResult(true);
            }

            private Task<bool> ProcessMarketDataAsync(MarketDataEvent marketData, CancellationToken cancellationToken)
            {
                // Update position values based on market data
                return Task.FromResult(true);
            }
        }

        /// <summary>
        /// Example 4: Strategy Signal Aggregator
        /// Demonstrates batch processing and signal aggregation
        /// </summary>
        public class SignalAggregatorService : IHostedService
        {
            private readonly ICanonicalMessageQueue _messageQueue;
            private readonly ITradingLogger _logger;
            private readonly Dictionary<string, List<SignalEvent>> _pendingSignals = new();
            private Timer? _aggregationTimer;

            public SignalAggregatorService(
                ICanonicalMessageQueue messageQueue,
                ITradingLogger logger)
            {
                _messageQueue = messageQueue;
                _logger = logger;
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                await _messageQueue.SubscribeAsync<SignalEvent>(
                    "signals",
                    "signal-aggregators",
                    ProcessSignalAsync,
                    new SubscriptionOptions { ConsumerName = "signal-aggregator" },
                    cancellationToken);

                // Start aggregation timer (every 100ms)
                _aggregationTimer = new Timer(
                    async _ => await ProcessAggregatedSignals(cancellationToken),
                    null,
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(100));

                _logger.LogInfo("Signal aggregator service started");
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                _aggregationTimer?.Dispose();
                return _messageQueue.UnsubscribeAsync("signals", "signal-aggregators", "signal-aggregator");
            }

            private Task<bool> ProcessSignalAsync(SignalEvent signal, CancellationToken cancellationToken)
            {
                lock (_pendingSignals)
                {
                    if (!_pendingSignals.ContainsKey(signal.Symbol))
                    {
                        _pendingSignals[signal.Symbol] = new List<SignalEvent>();
                    }
                    _pendingSignals[signal.Symbol].Add(signal);
                }
                return Task.FromResult(true);
            }

            private async Task ProcessAggregatedSignals(CancellationToken cancellationToken)
            {
                Dictionary<string, List<SignalEvent>> signalsToProcess;
                
                lock (_pendingSignals)
                {
                    if (_pendingSignals.Count == 0)
                        return;

                    signalsToProcess = new Dictionary<string, List<SignalEvent>>(_pendingSignals);
                    _pendingSignals.Clear();
                }

                foreach (var kvp in signalsToProcess)
                {
                    var symbol = kvp.Key;
                    var signals = kvp.Value;

                    // Aggregate signals by confidence
                    var avgConfidence = signals.Average(s => s.Confidence);
                    var strongBuySignals = signals.Count(s => s.SignalType == "BUY" && s.Confidence > 0.7m);
                    var strongSellSignals = signals.Count(s => s.SignalType == "SELL" && s.Confidence > 0.7m);

                    if (strongBuySignals > strongSellSignals && avgConfidence > 0.6m)
                    {
                        // Create aggregated buy order
                        var orderEvent = new OrderEvent
                        {
                            OrderId = Guid.NewGuid().ToString(),
                            Symbol = symbol,
                            Action = OrderAction.Submitted,
                            Status = OrderStatus.New,
                            Quantity = 100, // Standard lot size
                            Price = signals.First().Price,
                            OrderType = "MARKET",
                            Side = "BUY",
                            Source = "SignalAggregator",
                            CorrelationId = signals.First().CorrelationId
                        };

                        await _messageQueue.PublishAsync(
                            "orders",
                            orderEvent,
                            MessagePriority.High,
                            cancellationToken);

                        _logger.LogInfo($"Aggregated buy signal for {symbol}",
                            additionalData: new 
                            { 
                                SignalCount = signals.Count,
                                AverageConfidence = avgConfidence,
                                StrongSignals = strongBuySignals
                            });
                    }
                }
            }
        }

        /// <summary>
        /// Example service registration
        /// </summary>
        public static void ConfigureServices(IServiceCollection services)
        {
            // Add canonical message queue
            services.AddSingleton<ICanonicalMessageQueue, CanonicalMessageQueue>();

            // Add example services
            services.AddHostedService<MarketDataPublisher>();
            services.AddHostedService<OrderProcessingService>();
            services.AddHostedService<RiskMonitoringService>();
            services.AddHostedService<SignalAggregatorService>();
        }
    }
}