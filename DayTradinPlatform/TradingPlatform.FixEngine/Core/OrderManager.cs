using System.Collections.Concurrent;
using TradingPlatform.FixEngine.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.FixEngine.Core;

/// <summary>
/// Advanced order management system for FIX protocol trading
/// Supports single orders, IOC, FOK, hidden orders, and smart order routing
/// </summary>
public sealed class OrderManager : IDisposable
{
    private readonly FixSession _fixSession;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, Order> _activeOrders = new();
    private readonly ConcurrentDictionary<string, List<Execution>> _orderExecutions = new();
    private readonly Timer _orderTimeoutChecker;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    private int _orderIdCounter = 1;
    
    public event EventHandler<Order>? OrderStatusChanged;
    public event EventHandler<Execution>? ExecutionReceived;
    public event EventHandler<OrderReject>? OrderRejected;
    
    public OrderManager(FixSession fixSession, ILogger logger)
    {
        _fixSession = fixSession ?? throw new ArgumentNullException(nameof(fixSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Subscribe to execution reports
        _fixSession.MessageReceived += OnFixMessageReceived;
        
        // Check for order timeouts every 10 seconds
        _orderTimeoutChecker = new Timer(CheckOrderTimeouts, null, 
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }
    
    /// <summary>
    /// Submit a new single order
    /// </summary>
    public async Task<string> SubmitOrderAsync(OrderRequest orderRequest)
    {
        ValidateOrderRequest(orderRequest);
        
        var orderId = GenerateOrderId();
        var clOrdId = $"ORD_{orderId}_{DateTimeOffset.UtcNow.Ticks}";
        
        var order = new Order
        {
            OrderId = orderId,
            ClOrdId = clOrdId,
            Symbol = orderRequest.Symbol,
            Side = orderRequest.Side,
            OrderType = orderRequest.OrderType,
            Quantity = orderRequest.Quantity,
            Price = orderRequest.Price,
            StopPrice = orderRequest.StopPrice,
            TimeInForce = orderRequest.TimeInForce,
            MinQty = orderRequest.MinQty,
            MaxFloor = orderRequest.MaxFloor,
            ExpireTime = orderRequest.ExpireTime,
            ExecutionInstructions = orderRequest.ExecutionInstructions,
            Status = OrderStatus.PendingNew,
            CreatedTime = DateTime.UtcNow,
            HardwareTimestamp = GetHardwareTimestamp()
        };
        
        _activeOrders[clOrdId] = order;
        
        try
        {
            var fixMessage = CreateNewOrderSingleMessage(order);
            var success = await _fixSession.SendMessageAsync(fixMessage);
            
            if (success)
            {
                order.Status = OrderStatus.New;
                _logger.LogInfo($"Order submitted: {clOrdId} - {order.Symbol} {order.Side} {order.Quantity}@{order.Price}");
                OrderStatusChanged?.Invoke(this, order);
            }
            else
            {
                order.Status = OrderStatus.Rejected;
                _activeOrders.TryRemove(clOrdId, out _);
                TradingLogOrchestrator.Instance.LogError($"Failed to send order: {clOrdId}");
            }
            
            return clOrdId;
        }
        catch (Exception ex)
        {
            order.Status = OrderStatus.Rejected;
            _activeOrders.TryRemove(clOrdId, out _);
            TradingLogOrchestrator.Instance.LogError($"Error submitting order: {clOrdId}", ex);
            throw;
        }
    }
    
    /// <summary>
    /// Cancel an existing order
    /// </summary>
    public async Task<bool> CancelOrderAsync(string clOrdId, string? origClOrdId = null)
    {
        if (!_activeOrders.TryGetValue(origClOrdId ?? clOrdId, out var order))
        {
            TradingLogOrchestrator.Instance.LogWarning($"Cannot cancel order - not found: {clOrdId}");
            return false;
        }
        
        if (order.Status == OrderStatus.Filled || order.Status == OrderStatus.Cancelled)
        {
            TradingLogOrchestrator.Instance.LogWarning($"Cannot cancel order in status: {order.Status}");
            return false;
        }
        
        try
        {
            var cancelRequest = new FixMessage
            {
                MsgType = FixMessageTypes.OrderCancelRequest,
                HardwareTimestamp = GetHardwareTimestamp()
            };
            
            cancelRequest.SetField(11, clOrdId); // ClOrdID
            cancelRequest.SetField(41, origClOrdId ?? order.ClOrdId); // OrigClOrdID
            cancelRequest.SetField(55, order.Symbol); // Symbol
            cancelRequest.SetField(54, order.Side == OrderSide.Buy ? "1" : "2"); // Side
            cancelRequest.SetField(60, DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff")); // TransactTime
            
            var success = await _fixSession.SendMessageAsync(cancelRequest);
            
            if (success)
            {
                order.Status = OrderStatus.PendingCancel;
                _logger.LogInfo($"Cancel request sent for order: {clOrdId}");
                OrderStatusChanged?.Invoke(this, order);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error cancelling order: {clOrdId}", ex);
            return false;
        }
    }
    
    /// <summary>
    /// Replace (modify) an existing order
    /// </summary>
    public async Task<string> ReplaceOrderAsync(string origClOrdId, OrderReplaceRequest replaceRequest)
    {
        if (!_activeOrders.TryGetValue(origClOrdId, out var originalOrder))
        {
            throw new InvalidOperationException($"Original order not found: {origClOrdId}");
        }
        
        var newClOrdId = $"REP_{GenerateOrderId()}_{DateTimeOffset.UtcNow.Ticks}";
        
        try
        {
            var replaceMessage = new FixMessage
            {
                MsgType = FixMessageTypes.OrderCancelReplaceRequest,
                HardwareTimestamp = GetHardwareTimestamp()
            };
            
            replaceMessage.SetField(11, newClOrdId); // ClOrdID
            replaceMessage.SetField(41, origClOrdId); // OrigClOrdID
            replaceMessage.SetField(55, originalOrder.Symbol); // Symbol
            replaceMessage.SetField(54, originalOrder.Side == OrderSide.Buy ? "1" : "2"); // Side
            replaceMessage.SetField(60, DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff")); // TransactTime
            
            // New order parameters
            replaceMessage.SetField(38, replaceRequest.NewQuantity); // OrderQty
            replaceMessage.SetField(40, GetFixOrderType(replaceRequest.NewOrderType)); // OrdType
            
            if (replaceRequest.NewPrice.HasValue)
                replaceMessage.SetField(44, replaceRequest.NewPrice.Value); // Price
            
            if (replaceRequest.NewStopPrice.HasValue)
                replaceMessage.SetField(99, replaceRequest.NewStopPrice.Value); // StopPx
            
            replaceMessage.SetField(59, GetFixTimeInForce(replaceRequest.NewTimeInForce)); // TimeInForce
            
            var success = await _fixSession.SendMessageAsync(replaceMessage);
            
            if (success)
            {
                // Create new order object for the replacement
                var newOrder = new Order
                {
                    OrderId = newClOrdId,
                    ClOrdId = newClOrdId,
                    Symbol = originalOrder.Symbol,
                    Side = originalOrder.Side,
                    OrderType = replaceRequest.NewOrderType,
                    Quantity = replaceRequest.NewQuantity,
                    Price = replaceRequest.NewPrice,
                    StopPrice = replaceRequest.NewStopPrice,
                    TimeInForce = replaceRequest.NewTimeInForce,
                    Status = OrderStatus.PendingReplace,
                    CreatedTime = DateTime.UtcNow,
                    HardwareTimestamp = GetHardwareTimestamp(),
                    OriginalOrderId = origClOrdId
                };
                
                _activeOrders[newClOrdId] = newOrder;
                originalOrder.Status = OrderStatus.PendingReplace;
                
                _logger.LogInfo($"Order replace request sent: {origClOrdId} -> {newClOrdId}");
                
                OrderStatusChanged?.Invoke(this, originalOrder);
                OrderStatusChanged?.Invoke(this, newOrder);
            }
            
            return newClOrdId;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error replacing order: {origClOrdId}", ex);
            throw;
        }
    }
    
    /// <summary>
    /// Submit a mass cancel request for all orders or specific criteria
    /// </summary>
    public async Task<bool> MassCancelOrdersAsync(string? symbol = null, OrderSide? side = null)
    {
        try
        {
            var massCancelRequest = new FixMessage
            {
                MsgType = FixMessageTypes.OrderMassCancelRequest,
                HardwareTimestamp = GetHardwareTimestamp()
            };
            
            var clOrdId = $"MC_{GenerateOrderId()}_{DateTimeOffset.UtcNow.Ticks}";
            massCancelRequest.SetField(11, clOrdId); // ClOrdID
            massCancelRequest.SetField(60, DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff")); // TransactTime
            
            // Mass cancel type (1=Cancel all orders, 7=Cancel all orders for a security)
            if (string.IsNullOrEmpty(symbol))
            {
                massCancelRequest.SetField(530, "1"); // MassCancelRequestType
            }
            else
            {
                massCancelRequest.SetField(530, "7"); // MassCancelRequestType
                massCancelRequest.SetField(55, symbol); // Symbol
            }
            
            if (side.HasValue)
            {
                massCancelRequest.SetField(54, side == OrderSide.Buy ? "1" : "2"); // Side
            }
            
            var success = await _fixSession.SendMessageAsync(massCancelRequest);
            
            if (success)
            {
                _logger.LogInfo($"Mass cancel request sent - Symbol: {symbol ?? "ALL"}, Side: {side?.ToString() ?? "ALL"}");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error sending mass cancel request", ex);
            return false;
        }
    }
    
    /// <summary>
    /// Get order by client order ID
    /// </summary>
    public Order? GetOrder(string clOrdId)
    {
        return _activeOrders.TryGetValue(clOrdId, out var order) ? order : null;
    }
    
    /// <summary>
    /// Get all active orders
    /// </summary>
    public IReadOnlyCollection<Order> GetActiveOrders()
    {
        return _activeOrders.Values.Where(o => IsActiveStatus(o.Status)).ToList();
    }
    
    /// <summary>
    /// Get executions for an order
    /// </summary>
    public IReadOnlyList<Execution> GetOrderExecutions(string clOrdId)
    {
        return _orderExecutions.TryGetValue(clOrdId, out var executions) 
            ? executions.AsReadOnly() 
            : new List<Execution>().AsReadOnly();
    }
    
    private void OnFixMessageReceived(object? sender, FixMessage message)
    {
        try
        {
            switch (message.MsgType)
            {
                case FixMessageTypes.ExecutionReport:
                    HandleExecutionReport(message);
                    break;
                    
                case FixMessageTypes.OrderCancelReject:
                    HandleOrderCancelReject(message);
                    break;
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error processing order message: {message.MsgType}", ex);
        }
    }
    
    private void HandleExecutionReport(FixMessage message)
    {
        var clOrdId = message.GetField(11); // ClOrdID
        var execType = message.GetField(150); // ExecType
        var ordStatus = message.GetField(39); // OrdStatus
        
        if (string.IsNullOrEmpty(clOrdId)) return;
        
        if (_activeOrders.TryGetValue(clOrdId, out var order))
        {
            // Update order status
            order.Status = ConvertFixOrderStatus(ordStatus);
            order.LastUpdateTime = DateTime.UtcNow;
            
            // Handle execution
            if (execType == "F") // Trade (partial or full fill)
            {
                var execution = new Execution
                {
                    ExecId = message.GetField(17) ?? "", // ExecID
                    OrderId = clOrdId,
                    Symbol = order.Symbol,
                    Side = order.Side,
                    Quantity = message.GetDecimalField(32), // LastShares
                    Price = message.GetDecimalField(31), // LastPx
                    ExecutionTime = DateTime.UtcNow,
                    HardwareTimestamp = message.HardwareTimestamp,
                    LeavesQty = message.GetDecimalField(151), // LeavesQty
                    CumQty = message.GetDecimalField(14) // CumQty
                };
                
                if (!_orderExecutions.ContainsKey(clOrdId))
                    _orderExecutions[clOrdId] = new List<Execution>();
                
                _orderExecutions[clOrdId].Add(execution);
                
                order.FilledQuantity = execution.CumQty;
                order.AveragePrice = CalculateAveragePrice(clOrdId);
                
                ExecutionReceived?.Invoke(this, execution);
                
                _logger.LogInfo($"Execution received: {clOrdId} - {execution.Quantity}@{execution.Price}, Status: {order.Status}");
            }
            
            OrderStatusChanged?.Invoke(this, order);
            
            // Remove completed orders
            if (order.Status == OrderStatus.Filled || 
                order.Status == OrderStatus.Cancelled || 
                order.Status == OrderStatus.Rejected)
            {
                _activeOrders.TryRemove(clOrdId, out _);
            }
        }
    }
    
    private void HandleOrderCancelReject(FixMessage message)
    {
        var clOrdId = message.GetField(11); // ClOrdID
        var cxlRejReason = message.GetField(102); // CxlRejReason
        var text = message.GetField(58); // Text
        
        TradingLogOrchestrator.Instance.LogWarning($"Order cancel rejected: {clOrdId}, Reason: {cxlRejReason}, Text: {text}");
        
        if (!string.IsNullOrEmpty(clOrdId) && _activeOrders.TryGetValue(clOrdId, out var order))
        {
            // Revert status back to previous state
            order.Status = OrderStatus.New; // Assume it was New before cancel attempt
            order.LastUpdateTime = DateTime.UtcNow;
            OrderStatusChanged?.Invoke(this, order);
        }
        
        var reject = new OrderReject
        {
            ClOrdId = clOrdId ?? "",
            RejectReason = cxlRejReason ?? "",
            RejectText = text ?? "",
            RejectTime = DateTime.UtcNow
        };
        
        OrderRejected?.Invoke(this, reject);
    }
    
    private FixMessage CreateNewOrderSingleMessage(Order order)
    {
        var message = new FixMessage
        {
            MsgType = FixMessageTypes.NewOrderSingle,
            HardwareTimestamp = order.HardwareTimestamp
        };
        
        message.SetField(11, order.ClOrdId); // ClOrdID
        message.SetField(55, order.Symbol); // Symbol
        message.SetField(54, order.Side == OrderSide.Buy ? "1" : "2"); // Side
        message.SetField(60, DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff")); // TransactTime
        message.SetField(38, order.Quantity); // OrderQty
        message.SetField(40, GetFixOrderType(order.OrderType)); // OrdType
        
        if (order.Price.HasValue && order.OrderType != OrderType.Market)
            message.SetField(44, order.Price.Value); // Price
        
        if (order.StopPrice.HasValue)
            message.SetField(99, order.StopPrice.Value); // StopPx
        
        message.SetField(59, GetFixTimeInForce(order.TimeInForce)); // TimeInForce
        
        if (order.MinQty.HasValue)
            message.SetField(110, order.MinQty.Value); // MinQty
        
        if (order.MaxFloor.HasValue)
            message.SetField(111, order.MaxFloor.Value); // MaxFloor
        
        if (order.ExpireTime.HasValue)
            message.SetField(432, order.ExpireTime.Value.ToString("yyyyMMdd-HH:mm:ss")); // ExpireTime
        
        if (!string.IsNullOrEmpty(order.ExecutionInstructions))
            message.SetField(18, order.ExecutionInstructions); // ExecInst
        
        return message;
    }
    
    private static string GetFixOrderType(OrderType orderType)
    {
        return orderType switch
        {
            OrderType.Market => "1",
            OrderType.Limit => "2",
            OrderType.Stop => "3",
            OrderType.StopLimit => "4",
            _ => "2" // Default to Limit
        };
    }
    
    private static string GetFixTimeInForce(TimeInForce timeInForce)
    {
        return timeInForce switch
        {
            TimeInForce.Day => "0",
            TimeInForce.GTC => "1",
            TimeInForce.OPG => "2",
            TimeInForce.IOC => "3",
            TimeInForce.FOK => "4",
            TimeInForce.GTD => "6",
            _ => "0" // Default to Day
        };
    }
    
    private static OrderStatus ConvertFixOrderStatus(string? fixStatus)
    {
        return fixStatus switch
        {
            "0" => OrderStatus.New,
            "1" => OrderStatus.PartiallyFilled,
            "2" => OrderStatus.Filled,
            "4" => OrderStatus.Cancelled,
            "6" => OrderStatus.PendingCancel,
            "8" => OrderStatus.Rejected,
            "A" => OrderStatus.PendingNew,
            "E" => OrderStatus.PendingReplace,
            _ => OrderStatus.Unknown
        };
    }
    
    private static bool IsActiveStatus(OrderStatus status)
    {
        return status is OrderStatus.New or OrderStatus.PartiallyFilled or 
                       OrderStatus.PendingNew or OrderStatus.PendingCancel or 
                       OrderStatus.PendingReplace;
    }
    
    private decimal CalculateAveragePrice(string clOrdId)
    {
        if (!_orderExecutions.TryGetValue(clOrdId, out var executions) || !executions.Any())
            return 0m;
        
        var totalValue = executions.Sum(e => e.Quantity * e.Price);
        var totalQuantity = executions.Sum(e => e.Quantity);
        
        return totalQuantity > 0 ? totalValue / totalQuantity : 0m;
    }
    
    private void CheckOrderTimeouts(object? state)
    {
        var timeoutThreshold = TimeSpan.FromMinutes(10); // Orders timeout after 10 minutes
        var now = DateTime.UtcNow;
        
        foreach (var order in _activeOrders.Values.ToList())
        {
            if (order.Status == OrderStatus.PendingNew && 
                now - order.CreatedTime > timeoutThreshold)
            {
                TradingLogOrchestrator.Instance.LogWarning($"Order timeout detected: {order.ClOrdId}");
                order.Status = OrderStatus.Rejected;
                OrderStatusChanged?.Invoke(this, order);
                _activeOrders.TryRemove(order.ClOrdId, out _);
            }
        }
    }
    
    private static void ValidateOrderRequest(OrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            throw new ArgumentException("Symbol is required", nameof(request));
        
        if (request.Quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(request));
        
        if (request.OrderType != OrderType.Market && !request.Price.HasValue)
            throw new ArgumentException("Price is required for non-market orders", nameof(request));
        
        if (request.OrderType == OrderType.StopLimit && !request.StopPrice.HasValue)
            throw new ArgumentException("Stop price is required for stop-limit orders", nameof(request));
    }
    
    private string GenerateOrderId()
    {
        return Interlocked.Increment(ref _orderIdCounter).ToString();
    }
    
    private long GetHardwareTimestamp()
    {
        return (DateTimeOffset.UtcNow.Ticks - DateTimeOffset.UnixEpoch.Ticks) * 100L;
    }
    
    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _orderTimeoutChecker?.Dispose();
        _cancellationTokenSource.Dispose();
    }
}

/// <summary>
/// Order request for new order submission
/// </summary>
public class OrderRequest
{
    public required string Symbol { get; set; }
    public OrderSide Side { get; set; }
    public OrderType OrderType { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal? StopPrice { get; set; }
    public TimeInForce TimeInForce { get; set; } = TimeInForce.Day;
    public decimal? MinQty { get; set; }
    public decimal? MaxFloor { get; set; }
    public DateTime? ExpireTime { get; set; }
    public string? ExecutionInstructions { get; set; }
}

/// <summary>
/// Order replace request
/// </summary>
public class OrderReplaceRequest
{
    public OrderType NewOrderType { get; set; }
    public decimal NewQuantity { get; set; }
    public decimal? NewPrice { get; set; }
    public decimal? NewStopPrice { get; set; }
    public TimeInForce NewTimeInForce { get; set; }
}

/// <summary>
/// Order representation
/// </summary>
public class Order
{
    public required string OrderId { get; set; }
    public required string ClOrdId { get; set; }
    public required string Symbol { get; set; }
    public OrderSide Side { get; set; }
    public OrderType OrderType { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal? StopPrice { get; set; }
    public TimeInForce TimeInForce { get; set; }
    public decimal? MinQty { get; set; }
    public decimal? MaxFloor { get; set; }
    public DateTime? ExpireTime { get; set; }
    public string? ExecutionInstructions { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? LastUpdateTime { get; set; }
    public long HardwareTimestamp { get; set; }
    public string? OriginalOrderId { get; set; }
    public decimal FilledQuantity { get; set; }
    public decimal AveragePrice { get; set; }
    
    public decimal RemainingQuantity => Quantity - FilledQuantity;
}

/// <summary>
/// Order execution representation
/// </summary>
public class Execution
{
    public required string ExecId { get; set; }
    public required string OrderId { get; set; }
    public required string Symbol { get; set; }
    public OrderSide Side { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime ExecutionTime { get; set; }
    public long HardwareTimestamp { get; set; }
    public decimal LeavesQty { get; set; }
    public decimal CumQty { get; set; }
}

/// <summary>
/// Order rejection information
/// </summary>
public class OrderReject
{
    public required string ClOrdId { get; set; }
    public required string RejectReason { get; set; }
    public required string RejectText { get; set; }
    public DateTime RejectTime { get; set; }
}

/// <summary>
/// Order side enumeration
/// </summary>
public enum OrderSide
{
    Buy = 1,
    Sell = 2
}

/// <summary>
/// Order type enumeration
/// </summary>
public enum OrderType
{
    Market = 1,
    Limit = 2,
    Stop = 3,
    StopLimit = 4
}

/// <summary>
/// Time in force enumeration
/// </summary>
public enum TimeInForce
{
    Day = 0,
    GTC = 1,    // Good Till Cancel
    OPG = 2,    // At the Opening
    IOC = 3,    // Immediate or Cancel
    FOK = 4,    // Fill or Kill
    GTD = 6     // Good Till Date
}

/// <summary>
/// Order status enumeration
/// </summary>
public enum OrderStatus
{
    PendingNew,
    New,
    PartiallyFilled,
    Filled,
    PendingCancel,
    Cancelled,
    PendingReplace,
    Replaced,
    Rejected,
    Unknown
}