using TradingPlatform.PaperTrading.Models;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Messaging.Events;
using System.Collections.Concurrent;

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
namespace TradingPlatform.PaperTrading.Services;

public class PaperTradingService : IPaperTradingService
{
    private readonly IOrderExecutionEngine _executionEngine;
    private readonly IPortfolioManager _portfolioManager;
    private readonly IOrderBookSimulator _orderBookSimulator;
    private readonly IExecutionAnalytics _analytics;
    private readonly IMessageBus _messageBus;
    private readonly ITradingLogger _logger;
    private readonly ConcurrentDictionary<string, Order> _orders = new();
    private readonly ConcurrentQueue<Order> _pendingOrders = new();

    public PaperTradingService(
        IOrderExecutionEngine executionEngine,
        IPortfolioManager portfolioManager,
        IOrderBookSimulator orderBookSimulator,
        IExecutionAnalytics analytics,
        IMessageBus messageBus,
        ITradingLogger logger)
    {
        _executionEngine = executionEngine;
        _portfolioManager = portfolioManager;
        _orderBookSimulator = orderBookSimulator;
        _analytics = analytics;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<OrderResult> SubmitOrderAsync(OrderRequest orderRequest)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Validate order request
            var validationResult = await ValidateOrderRequestAsync(orderRequest);
            if (!validationResult.IsValid)
            {
                return new OrderResult(false, null, validationResult.ErrorMessage, OrderStatus.Rejected, DateTime.UtcNow);
            }

            // Check buying power
            var currentPrice = await _orderBookSimulator.GetCurrentPriceAsync(orderRequest.Symbol);
            var hasSufficientBuyingPower = await _portfolioManager.HasSufficientBuyingPowerAsync(orderRequest, currentPrice);
            
            if (!hasSufficientBuyingPower)
            {
                return new OrderResult(false, null, "Insufficient buying power", OrderStatus.Rejected, DateTime.UtcNow);
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

            // Publish order event
            await _messageBus.PublishAsync("orders.submitted", new OrderEvent
            {
                OrderId = orderId,
                Symbol = orderRequest.Symbol,
                OrderType = orderRequest.Type.ToString(),
                Side = orderRequest.Side.ToString(),
                Quantity = orderRequest.Quantity,
                Price = orderRequest.LimitPrice,
                Status = "New",
                ExecutionTime = DateTime.UtcNow
            });

            var elapsed = DateTime.UtcNow - startTime;
            TradingLogOrchestrator.Instance.LogInfo($"Order {orderId} submitted for {orderRequest.Symbol} in {elapsed.TotalMilliseconds}ms");

            return new OrderResult(true, orderId, "Order submitted successfully", OrderStatus.New, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error submitting order for {orderRequest.Symbol}", ex);
            return new OrderResult(false, null, $"Internal error: {ex.Message}", OrderStatus.Rejected, DateTime.UtcNow);
        }
    }

    public async Task<Order?> GetOrderAsync(string orderId)
    {
        _orders.TryGetValue(orderId, out var order);
        return await Task.FromResult(order);
    }

    public async Task<IEnumerable<Order>> GetOrdersAsync()
    {
        return await Task.FromResult(_orders.Values.OrderByDescending(o => o.CreatedAt));
    }

    public async Task<OrderResult> CancelOrderAsync(string orderId)
    {
        try
        {
            if (!_orders.TryGetValue(orderId, out var order))
            {
                return new OrderResult(false, orderId, "Order not found", OrderStatus.Rejected, DateTime.UtcNow);
            }

            if (order.Status == OrderStatus.Filled)
            {
                return new OrderResult(false, orderId, "Cannot cancel filled order", OrderStatus.Filled, DateTime.UtcNow);
            }

            if (order.Status == OrderStatus.Cancelled)
            {
                return new OrderResult(false, orderId, "Order already cancelled", OrderStatus.Cancelled, DateTime.UtcNow);
            }

            // Update order status
            var cancelledOrder = order with 
            { 
                Status = OrderStatus.Cancelled, 
                UpdatedAt = DateTime.UtcNow 
            };
            
            _orders.TryUpdate(orderId, cancelledOrder, order);

            // Publish cancellation event
            await _messageBus.PublishAsync("orders.cancelled", new OrderEvent
            {
                OrderId = orderId,
                Symbol = order.Symbol,
                OrderType = order.Type.ToString(),
                Side = order.Side.ToString(),
                Quantity = order.RemainingQuantity,
                Price = order.LimitPrice,
                Status = "Cancelled",
                ExecutionTime = DateTime.UtcNow
            });

            TradingLogOrchestrator.Instance.LogInfo($"Order {orderId} cancelled for {order.Symbol}");

            return new OrderResult(true, orderId, "Order cancelled successfully", OrderStatus.Cancelled, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error cancelling order {orderId}", ex);
            return new OrderResult(false, orderId, $"Error cancelling order: {ex.Message}", OrderStatus.Rejected, DateTime.UtcNow);
        }
    }

    public Task<OrderResult> ModifyOrderAsync(string orderId, OrderRequest modifiedOrder)
    {
        try
        {
            if (!_orders.TryGetValue(orderId, out var existingOrder))
            {
                return Task.FromResult(new OrderResult(false, orderId, "Order not found", OrderStatus.Rejected, DateTime.UtcNow));
            }

            if (existingOrder.Status != OrderStatus.New && existingOrder.Status != OrderStatus.PartiallyFilled)
            {
                return Task.FromResult(new OrderResult(false, orderId, "Cannot modify order in current status", existingOrder.Status, DateTime.UtcNow));
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

            _orders.TryUpdate(orderId, updatedOrder, existingOrder);

            TradingLogOrchestrator.Instance.LogInfo($"Order {orderId} modified for {existingOrder.Symbol}");

            return Task.FromResult(new OrderResult(true, orderId, "Order modified successfully", updatedOrder.Status, DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error modifying order {orderId}", ex);
            return Task.FromResult(new OrderResult(false, orderId, $"Error modifying order: {ex.Message}", OrderStatus.Rejected, DateTime.UtcNow));
        }
    }

    private async Task<(bool IsValid, string ErrorMessage)> ValidateOrderRequestAsync(OrderRequest orderRequest)
    {
        if (string.IsNullOrWhiteSpace(orderRequest.Symbol))
            return (false, "Symbol is required");

        if (orderRequest.Quantity <= 0)
            return (false, "Quantity must be positive");

        if (orderRequest.Type == OrderType.Limit && orderRequest.LimitPrice <= 0)
            return (false, "Limit price must be positive for limit orders");

        if (orderRequest.Type == OrderType.Stop && orderRequest.StopPrice <= 0)
            return (false, "Stop price must be positive for stop orders");

        if (orderRequest.Type == OrderType.StopLimit && 
            (orderRequest.LimitPrice <= 0 || orderRequest.StopPrice <= 0))
            return (false, "Both limit and stop prices must be positive for stop-limit orders");

        return await Task.FromResult((true, string.Empty));
    }
}