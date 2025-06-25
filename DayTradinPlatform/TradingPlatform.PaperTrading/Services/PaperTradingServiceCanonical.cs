using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Messaging.Events;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.PaperTrading.Models;

namespace TradingPlatform.PaperTrading.Services
{
    /// <summary>
    /// Canonical implementation of the paper trading service.
    /// Orchestrates order management, execution, and portfolio updates.
    /// </summary>
    public class PaperTradingServiceCanonical : CanonicalServiceBase, IPaperTradingService
    {
        #region Configuration

        protected virtual int MaxActiveOrders => 1000;
        protected virtual int MaxOrderHistory => 10000;
        protected virtual int OrderProcessingDelayMs => 10; // Simulate realistic processing delay
        protected virtual bool EnablePreTradeValidation => true;
        protected virtual bool EnableRiskChecks => true;
        protected virtual bool PublishOrderEvents => true;

        #endregion

        #region Infrastructure

        private readonly IOrderExecutionEngine _executionEngine;
        private readonly IPortfolioManager _portfolioManager;
        private readonly IOrderBookSimulator _orderBookSimulator;
        private readonly IExecutionAnalytics _analytics;
        private readonly IMessageBus _messageBus;
        
        private readonly ConcurrentDictionary<string, Order> _orders = new();
        private readonly ConcurrentQueue<Order> _pendingOrders = new();
        private readonly ConcurrentDictionary<string, OrderStatistics> _orderStats = new();
        
        private long _totalOrdersSubmitted = 0;
        private long _totalOrdersExecuted = 0;
        private long _totalOrdersRejected = 0;
        private long _totalOrdersCancelled = 0;

        #endregion

        #region Constructor

        public PaperTradingServiceCanonical(
            IOrderExecutionEngine executionEngine,
            IPortfolioManager portfolioManager,
            IOrderBookSimulator orderBookSimulator,
            IExecutionAnalytics analytics,
            IMessageBus messageBus,
            ITradingLogger logger)
            : base(logger, "PaperTradingService")
        {
            _executionEngine = executionEngine;
            _portfolioManager = portfolioManager;
            _orderBookSimulator = orderBookSimulator;
            _analytics = analytics;
            _messageBus = messageBus;
            
            LogMethodEntry(new { maxActiveOrders = MaxActiveOrders });
        }

        #endregion

        #region IPaperTradingService Implementation

        public async Task<OrderResult> SubmitOrderAsync(OrderRequest orderRequest)
        {
            return await ExecuteServiceOperationAsync(async () =>
            {
                var startTime = DateTime.UtcNow;
                ValidateNotNull(orderRequest, nameof(orderRequest));

                // Pre-trade validation
                if (EnablePreTradeValidation)
                {
                    var validationResult = await ValidateOrderRequestAsync(orderRequest);
                    if (!validationResult.IsValid)
                    {
                        Interlocked.Increment(ref _totalOrdersRejected);
                        UpdateOrderStatistics(orderRequest.Symbol, OrderStatus.Rejected);
                        
                        LogWarning($"Order rejected for {orderRequest.Symbol}: {validationResult.ErrorMessage}",
                                 impact: "Order will not be processed",
                                 troubleshooting: "Check order parameters and limits");
                        
                        return new OrderResult(false, null, validationResult.ErrorMessage, 
                            OrderStatus.Rejected, DateTime.UtcNow);
                    }
                }

                // Risk checks
                if (EnableRiskChecks)
                {
                    var currentPrice = await _orderBookSimulator.GetCurrentPriceAsync(orderRequest.Symbol);
                    var hasSufficientBuyingPower = await _portfolioManager.HasSufficientBuyingPowerAsync(
                        orderRequest, currentPrice);

                    if (!hasSufficientBuyingPower)
                    {
                        Interlocked.Increment(ref _totalOrdersRejected);
                        UpdateOrderStatistics(orderRequest.Symbol, OrderStatus.Rejected);
                        
                        LogWarning($"Insufficient buying power for {orderRequest.Symbol}",
                                 $"Required: ${orderRequest.Quantity * currentPrice:N2}",
                                 impact: "Order rejected",
                                 troubleshooting: "Reduce order size or close existing positions");
                        
                        return new OrderResult(false, null, "Insufficient buying power", 
                            OrderStatus.Rejected, DateTime.UtcNow);
                    }
                }

                // Check order limits
                if (_orders.Count >= MaxActiveOrders)
                {
                    LogWarning("Maximum active orders reached",
                             $"Current: {_orders.Count}, Max: {MaxActiveOrders}",
                             impact: "New orders will be rejected",
                             troubleshooting: "Cancel existing orders or increase limit");
                    
                    return new OrderResult(false, null, "Maximum active orders exceeded", 
                        OrderStatus.Rejected, DateTime.UtcNow);
                }

                // Create order
                var orderId = GenerateOrderId();
                var order = CreateOrder(orderId, orderRequest);
                
                _orders.TryAdd(orderId, order);
                _pendingOrders.Enqueue(order);
                
                Interlocked.Increment(ref _totalOrdersSubmitted);
                UpdateOrderStatistics(orderRequest.Symbol, OrderStatus.New);

                // Simulate processing delay
                if (OrderProcessingDelayMs > 0)
                {
                    await Task.Delay(OrderProcessingDelayMs);
                }

                // Publish order event
                if (PublishOrderEvents)
                {
                    await PublishOrderEventAsync(order, "submitted");
                }

                var elapsed = DateTime.UtcNow - startTime;
                
                UpdateMetric("Orders.SubmissionTimeMs", elapsed.TotalMilliseconds);
                UpdateMetric("Orders.Active", _orders.Count);
                UpdateMetric("Orders.Pending", _pendingOrders.Count);
                
                LogInfo($"Order {orderId} submitted for {orderRequest.Symbol} " +
                       $"{orderRequest.Quantity}@{orderRequest.LimitPrice ?? 0:C} " +
                       $"in {elapsed.TotalMilliseconds:F2}ms");

                return new OrderResult(true, orderId, "Order submitted successfully", 
                    OrderStatus.New, DateTime.UtcNow);

            }, "Submit order",
               incrementOperationCounter: true);
        }

        public async Task<Order?> GetOrderAsync(string orderId)
        {
            return await ExecuteServiceOperationAsync(async () =>
            {
                ValidateParameter(orderId, nameof(orderId), id => !string.IsNullOrWhiteSpace(id), 
                    "Order ID is required");

                _orders.TryGetValue(orderId, out var order);
                
                if (order != null)
                {
                    LogDebug($"Retrieved order {orderId} with status {order.Status}");
                }
                else
                {
                    LogDebug($"Order {orderId} not found");
                }
                
                return await Task.FromResult(order);

            }, $"Get order {orderId}",
               incrementOperationCounter: false); // Don't count reads
        }

        public async Task<IEnumerable<Order>> GetOrdersAsync()
        {
            return await ExecuteServiceOperationAsync(async () =>
            {
                var orders = _orders.Values
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(MaxOrderHistory)
                    .ToList();
                
                LogDebug($"Retrieved {orders.Count} orders");
                
                return await Task.FromResult(orders.AsEnumerable());

            }, "Get all orders",
               incrementOperationCounter: false);
        }

        public async Task<OrderResult> CancelOrderAsync(string orderId)
        {
            return await ExecuteServiceOperationAsync(async () =>
            {
                ValidateParameter(orderId, nameof(orderId), id => !string.IsNullOrWhiteSpace(id), 
                    "Order ID is required");

                if (!_orders.TryGetValue(orderId, out var order))
                {
                    LogWarning($"Order {orderId} not found for cancellation");
                    return new OrderResult(false, orderId, "Order not found", 
                        OrderStatus.Rejected, DateTime.UtcNow);
                }

                // Validate order can be cancelled
                if (order.Status == OrderStatus.Filled)
                {
                    LogWarning($"Cannot cancel filled order {orderId}",
                             impact: "Cancellation rejected",
                             troubleshooting: "Only open orders can be cancelled");
                    
                    return new OrderResult(false, orderId, "Cannot cancel filled order", 
                        OrderStatus.Filled, DateTime.UtcNow);
                }

                if (order.Status == OrderStatus.Cancelled)
                {
                    LogDebug($"Order {orderId} already cancelled");
                    return new OrderResult(false, orderId, "Order already cancelled", 
                        OrderStatus.Cancelled, DateTime.UtcNow);
                }

                // Update order status
                var cancelledOrder = order with
                {
                    Status = OrderStatus.Cancelled,
                    UpdatedAt = DateTime.UtcNow
                };

                if (_orders.TryUpdate(orderId, cancelledOrder, order))
                {
                    Interlocked.Increment(ref _totalOrdersCancelled);
                    UpdateOrderStatistics(order.Symbol, OrderStatus.Cancelled);
                    
                    // Publish cancellation event
                    if (PublishOrderEvents)
                    {
                        await PublishOrderEventAsync(cancelledOrder, "cancelled");
                    }

                    UpdateMetric("Orders.Cancelled", _totalOrdersCancelled);
                    
                    LogInfo($"Order {orderId} cancelled for {order.Symbol}");

                    return new OrderResult(true, orderId, "Order cancelled successfully", 
                        OrderStatus.Cancelled, DateTime.UtcNow);
                }
                else
                {
                    LogError($"Failed to update order {orderId} status to cancelled");
                    return new OrderResult(false, orderId, "Failed to cancel order", 
                        order.Status, DateTime.UtcNow);
                }

            }, $"Cancel order {orderId}",
               incrementOperationCounter: true);
        }

        public async Task<OrderResult> ModifyOrderAsync(string orderId, OrderRequest modifiedOrder)
        {
            return await ExecuteServiceOperationAsync(async () =>
            {
                ValidateParameter(orderId, nameof(orderId), id => !string.IsNullOrWhiteSpace(id), 
                    "Order ID is required");
                ValidateNotNull(modifiedOrder, nameof(modifiedOrder));

                if (!_orders.TryGetValue(orderId, out var existingOrder))
                {
                    LogWarning($"Order {orderId} not found for modification");
                    return new OrderResult(false, orderId, "Order not found", 
                        OrderStatus.Rejected, DateTime.UtcNow);
                }

                // Validate order can be modified
                if (existingOrder.Status != OrderStatus.New && 
                    existingOrder.Status != OrderStatus.PartiallyFilled)
                {
                    LogWarning($"Cannot modify order {orderId} in status {existingOrder.Status}",
                             impact: "Modification rejected",
                             troubleshooting: "Only open or partially filled orders can be modified");
                    
                    return new OrderResult(false, orderId, 
                        $"Cannot modify order in {existingOrder.Status} status", 
                        existingOrder.Status, DateTime.UtcNow);
                }

                // Validate modified parameters
                var validationResult = await ValidateOrderRequestAsync(modifiedOrder);
                if (!validationResult.IsValid)
                {
                    LogWarning($"Invalid modification for order {orderId}: {validationResult.ErrorMessage}");
                    return new OrderResult(false, orderId, validationResult.ErrorMessage, 
                        existingOrder.Status, DateTime.UtcNow);
                }

                // Create modified order
                var updatedOrder = existingOrder with
                {
                    Quantity = modifiedOrder.Quantity,
                    LimitPrice = modifiedOrder.LimitPrice,
                    StopPrice = modifiedOrder.StopPrice,
                    TimeInForce = modifiedOrder.TimeInForce,
                    RemainingQuantity = modifiedOrder.Quantity - existingOrder.FilledQuantity,
                    UpdatedAt = DateTime.UtcNow
                };

                if (updatedOrder.RemainingQuantity <= 0)
                {
                    LogWarning($"Modified quantity for order {orderId} is less than filled quantity",
                             $"Requested: {modifiedOrder.Quantity}, Filled: {existingOrder.FilledQuantity}");
                    
                    return new OrderResult(false, orderId, 
                        "Modified quantity must be greater than filled quantity", 
                        existingOrder.Status, DateTime.UtcNow);
                }

                if (_orders.TryUpdate(orderId, updatedOrder, existingOrder))
                {
                    // Publish modification event
                    if (PublishOrderEvents)
                    {
                        await PublishOrderEventAsync(updatedOrder, "modified");
                    }

                    LogInfo($"Order {orderId} modified for {existingOrder.Symbol}: " +
                           $"Qty {existingOrder.Quantity}->{modifiedOrder.Quantity}, " +
                           $"Price {existingOrder.LimitPrice}->{modifiedOrder.LimitPrice}");

                    return new OrderResult(true, orderId, "Order modified successfully", 
                        updatedOrder.Status, DateTime.UtcNow);
                }
                else
                {
                    LogError($"Failed to update order {orderId} with modifications");
                    return new OrderResult(false, orderId, "Failed to modify order", 
                        existingOrder.Status, DateTime.UtcNow);
                }

            }, $"Modify order {orderId}",
               incrementOperationCounter: true);
        }

        #endregion

        #region Order Management

        private Order CreateOrder(string orderId, OrderRequest orderRequest)
        {
            return new Order(
                OrderId: orderId,
                Symbol: orderRequest.Symbol,
                Type: orderRequest.Type,
                Side: orderRequest.Side,
                Quantity: orderRequest.Quantity,
                LimitPrice: orderRequest.LimitPrice,
                StopPrice: orderRequest.StopPrice,
                Status: OrderStatus.New,
                TimeInForce: orderRequest.TimeInForce,
                FilledQuantity: 0m,
                RemainingQuantity: orderRequest.Quantity,
                AveragePrice: 0m,
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: null,
                ClientOrderId: orderRequest.ClientOrderId
            );
        }

        private string GenerateOrderId()
        {
            // Generate unique order ID with timestamp component for easy sorting
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var random = Guid.NewGuid().ToString("N").Substring(0, 8);
            return $"ORD-{timestamp}-{random}";
        }

        private async Task<(bool IsValid, string ErrorMessage)> ValidateOrderRequestAsync(OrderRequest orderRequest)
        {
            // Symbol validation
            if (string.IsNullOrWhiteSpace(orderRequest.Symbol))
                return (false, "Symbol is required");

            if (orderRequest.Symbol.Length > 10)
                return (false, "Symbol length exceeds maximum allowed");

            // Quantity validation
            if (orderRequest.Quantity <= 0)
                return (false, "Quantity must be positive");

            if (orderRequest.Quantity > 1_000_000)
                return (false, "Quantity exceeds maximum allowed");

            // Price validation for limit orders
            if (orderRequest.Type == OrderType.Limit && orderRequest.LimitPrice <= 0)
                return (false, "Limit price must be positive for limit orders");

            // Price validation for stop orders
            if (orderRequest.Type == OrderType.Stop && orderRequest.StopPrice <= 0)
                return (false, "Stop price must be positive for stop orders");

            // Price validation for stop-limit orders
            if (orderRequest.Type == OrderType.StopLimit &&
                (orderRequest.LimitPrice <= 0 || orderRequest.StopPrice <= 0))
                return (false, "Both limit and stop prices must be positive for stop-limit orders");

            // Price sanity checks
            if (orderRequest.LimitPrice > 100_000)
                return (false, "Limit price exceeds maximum allowed");

            if (orderRequest.StopPrice > 100_000)
                return (false, "Stop price exceeds maximum allowed");

            return await Task.FromResult((true, string.Empty));
        }

        #endregion

        #region Event Publishing

        private async Task PublishOrderEventAsync(Order order, string eventType)
        {
            try
            {
                var orderEvent = new OrderEvent
                {
                    OrderId = order.OrderId,
                    Symbol = order.Symbol,
                    OrderType = order.Type.ToString(),
                    Side = order.Side.ToString(),
                    Quantity = order.Quantity,
                    Price = order.LimitPrice,
                    Status = order.Status.ToString(),
                    ExecutionTime = DateTime.UtcNow
                };

                var topic = $"orders.{eventType}";
                await _messageBus.PublishAsync(topic, orderEvent);
                
                LogDebug($"Published {eventType} event for order {order.OrderId}");
            }
            catch (Exception ex)
            {
                LogError($"Error publishing order event for {order.OrderId}", ex,
                        impact: "Event subscribers will not be notified",
                        troubleshooting: "Check message bus connectivity");
            }
        }

        #endregion

        #region Statistics

        private void UpdateOrderStatistics(string symbol, OrderStatus status)
        {
            var stats = _orderStats.GetOrAdd(symbol, _ => new OrderStatistics { Symbol = symbol });

            switch (status)
            {
                case OrderStatus.New:
                    Interlocked.Increment(ref stats.TotalSubmitted);
                    break;
                case OrderStatus.Filled:
                    Interlocked.Increment(ref stats.TotalExecuted);
                    break;
                case OrderStatus.Rejected:
                    Interlocked.Increment(ref stats.TotalRejected);
                    break;
                case OrderStatus.Cancelled:
                    Interlocked.Increment(ref stats.TotalCancelled);
                    break;
            }

            stats.LastUpdated = DateTime.UtcNow;
            
            UpdateMetric($"Orders.{symbol}.{status}", 1);
        }

        #endregion

        #region Metrics

        public override IReadOnlyDictionary<string, object> GetMetrics()
        {
            var metrics = base.GetMetrics().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Add paper trading specific metrics
            metrics["PaperTrading.TotalOrdersSubmitted"] = _totalOrdersSubmitted;
            metrics["PaperTrading.TotalOrdersExecuted"] = _totalOrdersExecuted;
            metrics["PaperTrading.TotalOrdersRejected"] = _totalOrdersRejected;
            metrics["PaperTrading.TotalOrdersCancelled"] = _totalOrdersCancelled;
            metrics["PaperTrading.ActiveOrders"] = _orders.Count;
            metrics["PaperTrading.PendingOrders"] = _pendingOrders.Count;
            metrics["PaperTrading.SymbolsTraded"] = _orderStats.Count;
            
            if (_totalOrdersSubmitted > 0)
            {
                metrics["PaperTrading.ExecutionRate"] = 
                    (double)_totalOrdersExecuted / _totalOrdersSubmitted;
                metrics["PaperTrading.RejectionRate"] = 
                    (double)_totalOrdersRejected / _totalOrdersSubmitted;
            }

            return metrics;
        }

        #endregion

        #region Lifecycle

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Initializing PaperTradingService with max {MaxActiveOrders} active orders");
            
            // Ensure dependent services are initialized
            if (_executionEngine is CanonicalServiceBase executionService)
            {
                await executionService.InitializeAsync(cancellationToken);
            }
            
            if (_portfolioManager is CanonicalServiceBase portfolioService)
            {
                await portfolioService.InitializeAsync(cancellationToken);
            }
            
            if (_orderBookSimulator is CanonicalServiceBase orderBookService)
            {
                await orderBookService.InitializeAsync(cancellationToken);
            }
            
            if (_analytics is CanonicalServiceBase analyticsService)
            {
                await analyticsService.InitializeAsync(cancellationToken);
            }
        }

        protected override async Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo("Starting PaperTradingService");
            
            // Start dependent services
            if (_executionEngine is CanonicalServiceBase executionService)
            {
                await executionService.StartAsync(cancellationToken);
            }
            
            if (_portfolioManager is CanonicalServiceBase portfolioService)
            {
                await portfolioService.StartAsync(cancellationToken);
            }
            
            if (_orderBookSimulator is CanonicalServiceBase orderBookService)
            {
                await orderBookService.StartAsync(cancellationToken);
            }
            
            if (_analytics is CanonicalServiceBase analyticsService)
            {
                await analyticsService.StartAsync(cancellationToken);
            }
        }

        protected override async Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Stopping PaperTradingService. Total orders processed: {_totalOrdersSubmitted}");
            
            // Log final statistics
            var activeOrders = _orders.Values.Where(o => 
                o.Status == OrderStatus.New || o.Status == OrderStatus.PartiallyFilled).ToList();
            
            if (activeOrders.Any())
            {
                LogWarning($"{activeOrders.Count} active orders remaining at shutdown",
                         impact: "Orders will not be processed",
                         troubleshooting: "Consider cancelling all orders before shutdown");
            }
            
            // Stop dependent services
            var stopTasks = new List<Task>();
            
            if (_executionEngine is CanonicalServiceBase executionService)
            {
                stopTasks.Add(executionService.StopAsync(cancellationToken));
            }
            
            if (_portfolioManager is CanonicalServiceBase portfolioService)
            {
                stopTasks.Add(portfolioService.StopAsync(cancellationToken));
            }
            
            if (_orderBookSimulator is CanonicalServiceBase orderBookService)
            {
                stopTasks.Add(orderBookService.StopAsync(cancellationToken));
            }
            
            if (_analytics is CanonicalServiceBase analyticsService)
            {
                stopTasks.Add(analyticsService.StopAsync(cancellationToken));
            }
            
            await Task.WhenAll(stopTasks);
        }

        #endregion

        #region Nested Types

        private class OrderStatistics
        {
            public string Symbol { get; set; } = string.Empty;
            public long TotalSubmitted;
            public long TotalExecuted;
            public long TotalRejected;
            public long TotalCancelled;
            public DateTime LastUpdated { get; set; }
        }

        #endregion
    }
}