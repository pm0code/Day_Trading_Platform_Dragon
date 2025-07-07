using TradingPlatform.PaperTrading.Models;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Messaging.Events;
using System.Collections.Concurrent;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Foundation.Models;
using System.Diagnostics;

namespace TradingPlatform.PaperTrading.Services;

/// <summary>
/// High-performance paper trading service for risk-free trading simulation
/// Implements comprehensive order management with realistic market simulation
/// All operations use TradingResult pattern for consistent error handling and observability
/// Maintains sub-millisecond order processing for real-time trading experience
/// </summary>
public class PaperTradingService : CanonicalServiceBase, IPaperTradingService
{
    private readonly IOrderExecutionEngine _executionEngine;
    private readonly IPortfolioManager _portfolioManager;
    private readonly IOrderBookSimulator _orderBookSimulator;
    private readonly IExecutionAnalytics _analytics;
    private readonly IMessageBus _messageBus;
    private readonly ConcurrentDictionary<string, Order> _orders = new();
    private readonly ConcurrentQueue<Order> _pendingOrders = new();
    
    // Performance tracking
    private long _totalOrdersSubmitted = 0;
    private long _totalOrdersFilled = 0;
    private long _totalOrdersCancelled = 0;
    private readonly object _metricsLock = new();

    /// <summary>
    /// Initializes a new instance of the PaperTradingService with comprehensive dependencies and canonical patterns
    /// </summary>
    /// <param name="executionEngine">Order execution engine for simulating market fills</param>
    /// <param name="portfolioManager">Portfolio manager for position and buying power management</param>
    /// <param name="orderBookSimulator">Order book simulator for realistic market conditions</param>
    /// <param name="analytics">Execution analytics for performance tracking</param>
    /// <param name="messageBus">Message bus for event publishing</param>
    /// <param name="logger">Trading logger for comprehensive operation tracking</param>
    public PaperTradingService(
        IOrderExecutionEngine executionEngine,
        IPortfolioManager portfolioManager,
        IOrderBookSimulator orderBookSimulator,
        IExecutionAnalytics analytics,
        IMessageBus messageBus,
        ITradingLogger logger) : base(logger, "PaperTradingService")
    {
        _executionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));
        _portfolioManager = portfolioManager ?? throw new ArgumentNullException(nameof(portfolioManager));
        _orderBookSimulator = orderBookSimulator ?? throw new ArgumentNullException(nameof(orderBookSimulator));
        _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
    }

    /// <summary>
    /// Submits a new paper trading order with comprehensive validation and risk checks
    /// Simulates realistic order submission with market conditions and portfolio constraints
    /// </summary>
    /// <param name="orderRequest">The order request containing symbol, quantity, price, and order type</param>
    /// <returns>A TradingResult containing the order submission result or error information</returns>
    public async Task<TradingResult<OrderResult>> SubmitOrderAsync(OrderRequest orderRequest)
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            if (orderRequest == null)
            {
                LogMethodExit();
                return TradingResult<OrderResult>.Failure("INVALID_REQUEST", "Order request cannot be null");
            }

            LogInfo($"Submitting paper order for {orderRequest.Symbol}, {orderRequest.Side} {orderRequest.Quantity} shares");

            // Validate order request
            var validationResult = await ValidateOrderRequestAsync(orderRequest);
            if (!validationResult.IsValid)
            {
                LogWarning($"Order validation failed for {orderRequest.Symbol}: {validationResult.ErrorMessage}");
                var rejectedResult = new OrderResult(false, null, validationResult.ErrorMessage, OrderStatus.Rejected, DateTime.UtcNow);
                LogMethodExit();
                return TradingResult<OrderResult>.Success(rejectedResult);
            }

            // Check buying power
            var currentPrice = await _orderBookSimulator.GetCurrentPriceAsync(orderRequest.Symbol);
            var hasSufficientBuyingPower = await _portfolioManager.HasSufficientBuyingPowerAsync(orderRequest, currentPrice);

            if (!hasSufficientBuyingPower)
            {
                LogWarning($"Insufficient buying power for {orderRequest.Symbol} order");
                var insufficientFundsResult = new OrderResult(false, null, "Insufficient buying power", OrderStatus.Rejected, DateTime.UtcNow);
                LogMethodExit();
                return TradingResult<OrderResult>.Success(insufficientFundsResult);
            }

            // Create order
            var orderId = Guid.NewGuid().ToString();
            var order = new Order(
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

            _orders.TryAdd(orderId, order);
            _pendingOrders.Enqueue(order);
            
            // Update metrics
            lock (_metricsLock)
            {
                _totalOrdersSubmitted++;
            }

            // Publish order event
            await PublishOrderEventAsync(order, "New");

            stopwatch.Stop();
            LogInfo($"Order {orderId} submitted for {orderRequest.Symbol} in {stopwatch.ElapsedMilliseconds}ms");

            var successResult = new OrderResult(true, orderId, "Order submitted successfully", OrderStatus.New, DateTime.UtcNow);
            LogMethodExit();
            return TradingResult<OrderResult>.Success(successResult);
        }
        catch (Exception ex)
        {
            LogError($"Error submitting order for {orderRequest?.Symbol}", ex);
            var errorResult = new OrderResult(false, null, $"Internal error: {ex.Message}", OrderStatus.Rejected, DateTime.UtcNow);
            LogMethodExit();
            return TradingResult<OrderResult>.Success(errorResult);
        }
    }

    /// <summary>
    /// Retrieves a specific order by its unique identifier
    /// Provides real-time order status and execution details
    /// </summary>
    /// <param name="orderId">The unique identifier of the order to retrieve</param>
    /// <returns>A TradingResult containing the order details or error information</returns>
    public async Task<TradingResult<Order?>> GetOrderAsync(string orderId)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(orderId))
            {
                LogMethodExit();
                return TradingResult<Order?>.Failure("INVALID_ORDER_ID", "Order ID cannot be null or empty");
            }

            LogDebug($"Retrieving order {orderId}");
            
            _orders.TryGetValue(orderId, out var order);
            
            if (order == null)
            {
                LogDebug($"Order {orderId} not found");
            }
            else
            {
                LogDebug($"Order {orderId} found: {order.Symbol} {order.Side} {order.Quantity} @ {order.Status}");
            }
            
            LogMethodExit();
            return await Task.FromResult(TradingResult<Order?>.Success(order));
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving order {orderId}", ex);
            LogMethodExit();
            return TradingResult<Order?>.Failure("ORDER_RETRIEVAL_ERROR", 
                $"Failed to retrieve order: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves all orders in the paper trading system
    /// Returns orders sorted by creation time in descending order
    /// </summary>
    /// <returns>A TradingResult containing the list of all orders or error information</returns>
    public async Task<TradingResult<IEnumerable<Order>>> GetOrdersAsync()
    {
        LogMethodEntry();
        try
        {
            LogDebug("Retrieving all orders");
            
            var orders = _orders.Values.OrderByDescending(o => o.CreatedAt).ToList();
            
            LogInfo($"Retrieved {orders.Count} orders from paper trading system");
            
            LogMethodExit();
            return await Task.FromResult(TradingResult<IEnumerable<Order>>.Success(orders));
        }
        catch (Exception ex)
        {
            LogError("Error retrieving orders", ex);
            LogMethodExit();
            return TradingResult<IEnumerable<Order>>.Failure("ORDERS_RETRIEVAL_ERROR", 
                $"Failed to retrieve orders: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Cancels an existing paper trading order if eligible
    /// Validates order status before cancellation to ensure market integrity
    /// </summary>
    /// <param name="orderId">The unique identifier of the order to cancel</param>
    /// <returns>A TradingResult containing the cancellation result or error information</returns>
    public async Task<TradingResult<OrderResult>> CancelOrderAsync(string orderId)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(orderId))
            {
                LogMethodExit();
                return TradingResult<OrderResult>.Failure("INVALID_ORDER_ID", "Order ID cannot be null or empty");
            }

            LogInfo($"Attempting to cancel order {orderId}");

            if (!_orders.TryGetValue(orderId, out var order))
            {
                LogWarning($"Order {orderId} not found for cancellation");
                var notFoundResult = new OrderResult(false, orderId, "Order not found", OrderStatus.Rejected, DateTime.UtcNow);
                LogMethodExit();
                return TradingResult<OrderResult>.Success(notFoundResult);
            }

            if (order.Status == OrderStatus.Filled)
            {
                LogWarning($"Cannot cancel filled order {orderId}");
                var filledResult = new OrderResult(false, orderId, "Cannot cancel filled order", OrderStatus.Filled, DateTime.UtcNow);
                LogMethodExit();
                return TradingResult<OrderResult>.Success(filledResult);
            }

            if (order.Status == OrderStatus.Cancelled)
            {
                LogWarning($"Order {orderId} already cancelled");
                var alreadyCancelledResult = new OrderResult(false, orderId, "Order already cancelled", OrderStatus.Cancelled, DateTime.UtcNow);
                LogMethodExit();
                return TradingResult<OrderResult>.Success(alreadyCancelledResult);
            }

            // Update order status
            var cancelledOrder = order with
            {
                Status = OrderStatus.Cancelled,
                UpdatedAt = DateTime.UtcNow
            };

            if (!_orders.TryUpdate(orderId, cancelledOrder, order))
            {
                LogError($"Failed to update order {orderId} status to cancelled");
                var updateFailedResult = new OrderResult(false, orderId, "Failed to update order status", order.Status, DateTime.UtcNow);
                LogMethodExit();
                return TradingResult<OrderResult>.Success(updateFailedResult);
            }
            
            // Update metrics
            lock (_metricsLock)
            {
                _totalOrdersCancelled++;
            }

            // Publish cancellation event
            await PublishOrderEventAsync(cancelledOrder, "Cancelled");

            LogInfo($"Order {orderId} cancelled successfully for {order.Symbol}");

            var successResult = new OrderResult(true, orderId, "Order cancelled successfully", OrderStatus.Cancelled, DateTime.UtcNow);
            LogMethodExit();
            return TradingResult<OrderResult>.Success(successResult);
        }
        catch (Exception ex)
        {
            LogError($"Error cancelling order {orderId}", ex);
            var errorResult = new OrderResult(false, orderId, $"Error cancelling order: {ex.Message}", OrderStatus.Rejected, DateTime.UtcNow);
            LogMethodExit();
            return TradingResult<OrderResult>.Success(errorResult);
        }
    }

    /// <summary>
    /// Modifies an existing paper trading order with new parameters
    /// Validates order status and filled quantity before allowing modifications
    /// </summary>
    /// <param name="orderId">The unique identifier of the order to modify</param>
    /// <param name="modifiedOrder">The modified order request with new parameters</param>
    /// <returns>A TradingResult containing the modification result or error information</returns>
    public async Task<TradingResult<OrderResult>> ModifyOrderAsync(string orderId, OrderRequest modifiedOrder)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(orderId))
            {
                LogMethodExit();
                return TradingResult<OrderResult>.Failure("INVALID_ORDER_ID", "Order ID cannot be null or empty");
            }

            if (modifiedOrder == null)
            {
                LogMethodExit();
                return TradingResult<OrderResult>.Failure("INVALID_REQUEST", "Modified order request cannot be null");
            }

            LogInfo($"Attempting to modify order {orderId}");

            if (!_orders.TryGetValue(orderId, out var existingOrder))
            {
                LogWarning($"Order {orderId} not found for modification");
                var notFoundResult = new OrderResult(false, orderId, "Order not found", OrderStatus.Rejected, DateTime.UtcNow);
                LogMethodExit();
                return TradingResult<OrderResult>.Success(notFoundResult);
            }

            if (existingOrder.Status != OrderStatus.New && existingOrder.Status != OrderStatus.PartiallyFilled)
            {
                LogWarning($"Cannot modify order {orderId} in status {existingOrder.Status}");
                var invalidStatusResult = new OrderResult(false, orderId, "Cannot modify order in current status", existingOrder.Status, DateTime.UtcNow);
                LogMethodExit();
                return TradingResult<OrderResult>.Success(invalidStatusResult);
            }
            
            // Validate modified quantity
            if (modifiedOrder.Quantity < existingOrder.FilledQuantity)
            {
                LogWarning($"Cannot reduce order quantity below filled quantity for order {orderId}");
                var invalidQuantityResult = new OrderResult(false, orderId, "New quantity cannot be less than filled quantity", existingOrder.Status, DateTime.UtcNow);
                LogMethodExit();
                return TradingResult<OrderResult>.Success(invalidQuantityResult);
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

            if (!_orders.TryUpdate(orderId, updatedOrder, existingOrder))
            {
                LogError($"Failed to update order {orderId} with modifications");
                var updateFailedResult = new OrderResult(false, orderId, "Failed to update order", existingOrder.Status, DateTime.UtcNow);
                LogMethodExit();
                return TradingResult<OrderResult>.Success(updateFailedResult);
            }

            // Publish modification event
            await PublishOrderEventAsync(updatedOrder, "Modified");

            LogInfo($"Order {orderId} modified successfully for {existingOrder.Symbol}");

            var successResult = new OrderResult(true, orderId, "Order modified successfully", updatedOrder.Status, DateTime.UtcNow);
            LogMethodExit();
            return TradingResult<OrderResult>.Success(successResult);
        }
        catch (Exception ex)
        {
            LogError($"Error modifying order {orderId}", ex);
            var errorResult = new OrderResult(false, orderId, $"Error modifying order: {ex.Message}", OrderStatus.Rejected, DateTime.UtcNow);
            LogMethodExit();
            return TradingResult<OrderResult>.Success(errorResult);
        }
    }

    // ========== PRIVATE HELPER METHODS ==========

    /// <summary>
    /// Validates order request parameters for paper trading compliance
    /// Ensures all required fields are present and valid before order submission
    /// </summary>
    private async Task<(bool IsValid, string ErrorMessage)> ValidateOrderRequestAsync(OrderRequest orderRequest)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrWhiteSpace(orderRequest.Symbol))
            {
                LogMethodExit();
                return (false, "Symbol is required");
            }

            if (orderRequest.Quantity <= 0)
            {
                LogMethodExit();
                return (false, "Quantity must be positive");
            }

            if (orderRequest.Type == OrderType.Limit && orderRequest.LimitPrice <= 0)
            {
                LogMethodExit();
                return (false, "Limit price must be positive for limit orders");
            }

            if (orderRequest.Type == OrderType.Stop && orderRequest.StopPrice <= 0)
            {
                LogMethodExit();
                return (false, "Stop price must be positive for stop orders");
            }

            if (orderRequest.Type == OrderType.StopLimit &&
                (orderRequest.LimitPrice <= 0 || orderRequest.StopPrice <= 0))
            {
                LogMethodExit();
                return (false, "Both limit and stop prices must be positive for stop-limit orders");
            }
            
            LogDebug($"Order validation passed for {orderRequest.Symbol}");
            LogMethodExit();
            return await Task.FromResult((true, string.Empty));
        }
        catch (Exception ex)
        {
            LogError($"Error validating order request for {orderRequest?.Symbol}", ex);
            LogMethodExit();
            return (false, $"Validation error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Publishes order events to the message bus for real-time notifications
    /// </summary>
    private async Task PublishOrderEventAsync(Order order, string status)
    {
        LogMethodEntry();
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
                Status = status,
                ExecutionTime = DateTime.UtcNow
            };

            var eventType = status.ToLower() switch
            {
                "new" => "orders.submitted",
                "cancelled" => "orders.cancelled",
                "modified" => "orders.modified",
                "filled" => "orders.filled",
                _ => "orders.updated"
            };

            await _messageBus.PublishAsync(eventType, orderEvent);
            
            LogDebug($"Published {eventType} event for order {order.OrderId}");
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Error publishing order event for {order?.OrderId}", ex);
            LogMethodExit();
        }
    }
    
    /// <summary>
    /// Processes pending orders for execution based on market conditions
    /// This method would typically be called by a background service
    /// </summary>
    public async Task ProcessPendingOrdersAsync()
    {
        LogMethodEntry();
        try
        {
            while (_pendingOrders.TryDequeue(out var order))
            {
                if (order.Status != OrderStatus.New && order.Status != OrderStatus.PartiallyFilled)
                {
                    continue;
                }

                var marketPrice = await _orderBookSimulator.GetCurrentPriceAsync(order.Symbol);
                var shouldExecute = await _executionEngine.ShouldExecuteOrderAsync(order, marketPrice);

                if (shouldExecute)
                {
                    var execution = await _executionEngine.ExecuteOrderAsync(order, marketPrice);
                    if (execution != null)
                    {
                        await ProcessExecutionAsync(order, execution);
                    }
                }
                else
                {
                    // Re-queue if not executed
                    _pendingOrders.Enqueue(order);
                }
            }
            
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Error processing pending orders", ex);
            LogMethodExit();
        }
    }
    
    /// <summary>
    /// Processes order execution and updates portfolio positions
    /// </summary>
    private async Task ProcessExecutionAsync(Order order, Execution execution)
    {
        LogMethodEntry();
        try
        {
            // Update order with execution details
            var updatedOrder = order with
            {
                Status = execution.FilledQuantity >= order.Quantity ? OrderStatus.Filled : OrderStatus.PartiallyFilled,
                FilledQuantity = order.FilledQuantity + execution.FilledQuantity,
                RemainingQuantity = order.RemainingQuantity - execution.FilledQuantity,
                AveragePrice = ((order.FilledQuantity * order.AveragePrice) + (execution.FilledQuantity * execution.Price)) / 
                               (order.FilledQuantity + execution.FilledQuantity),
                UpdatedAt = DateTime.UtcNow
            };

            _orders.TryUpdate(order.OrderId, updatedOrder, order);

            // Update portfolio
            await _portfolioManager.UpdatePositionAsync(order.Symbol, execution);

            // Record analytics
            await _analytics.RecordExecutionAsync(execution);

            // Update metrics
            if (updatedOrder.Status == OrderStatus.Filled)
            {
                lock (_metricsLock)
                {
                    _totalOrdersFilled++;
                }
            }

            // Publish execution event
            await PublishOrderEventAsync(updatedOrder, updatedOrder.Status.ToString());
            
            LogInfo($"Processed execution for order {order.OrderId}: {execution.FilledQuantity} shares @ ${execution.Price}");
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Error processing execution for order {order?.OrderId}", ex);
            LogMethodExit();
        }
    }
    
    // ========== SERVICE HEALTH & METRICS ==========
    
    /// <summary>
    /// Gets comprehensive metrics about the paper trading service performance
    /// </summary>
    public async Task<TradingResult<PaperTradingMetrics>> GetMetricsAsync()
    {
        LogMethodEntry();
        try
        {
            var metrics = new PaperTradingMetrics
            {
                TotalOrdersSubmitted = _totalOrdersSubmitted,
                TotalOrdersFilled = _totalOrdersFilled,
                TotalOrdersCancelled = _totalOrdersCancelled,
                ActiveOrders = _orders.Values.Count(o => o.Status == OrderStatus.New || o.Status == OrderStatus.PartiallyFilled),
                PendingOrders = _pendingOrders.Count,
                Timestamp = DateTime.UtcNow
            };
            
            LogInfo($"Paper trading metrics: {metrics.TotalOrdersSubmitted} submitted, {metrics.TotalOrdersFilled} filled, {metrics.ActiveOrders} active");
            LogMethodExit();
            return await Task.FromResult(TradingResult<PaperTradingMetrics>.Success(metrics));
        }
        catch (Exception ex)
        {
            LogError("Error retrieving paper trading metrics", ex);
            LogMethodExit();
            return TradingResult<PaperTradingMetrics>.Failure("METRICS_ERROR", 
                $"Failed to retrieve metrics: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Performs health check on the paper trading service
    /// </summary>
    protected override async Task<HealthCheckResult> PerformHealthCheckAsync()
    {
        LogMethodEntry();
        try
        {
            // Check execution engine
            var executionHealthy = _executionEngine != null;
            
            // Check portfolio manager
            var portfolio = await _portfolioManager.GetPortfolioAsync();
            var portfolioHealthy = portfolio != null;
            
            // Check order book simulator
            var simulatorHealthy = _orderBookSimulator != null;
            
            // Check message bus
            var messageBusHealthy = _messageBus != null;
            
            var isHealthy = executionHealthy && portfolioHealthy && simulatorHealthy && messageBusHealthy;
            
            var details = new Dictionary<string, object>
            {
                ["ExecutionEngine"] = executionHealthy ? "Healthy" : "Unhealthy",
                ["PortfolioManager"] = portfolioHealthy ? "Healthy" : "Unhealthy",
                ["OrderBookSimulator"] = simulatorHealthy ? "Healthy" : "Unhealthy",
                ["MessageBus"] = messageBusHealthy ? "Healthy" : "Unhealthy",
                ["ActiveOrders"] = _orders.Count,
                ["PendingOrders"] = _pendingOrders.Count
            };
            
            LogMethodExit();
            return new HealthCheckResult(isHealthy, "Paper Trading Service", details);
        }
        catch (Exception ex)
        {
            LogError("Error performing health check", ex);
            LogMethodExit();
            return new HealthCheckResult(false, "Paper Trading Service", 
                new Dictionary<string, object> { ["Error"] = ex.Message });
        }
    }
}