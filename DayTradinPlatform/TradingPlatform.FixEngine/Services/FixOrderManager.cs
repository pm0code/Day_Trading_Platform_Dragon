using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.FixEngine.Canonical;
using TradingPlatform.FixEngine.Models;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.FixEngine.Services
{
    /// <summary>
    /// Manages FIX order lifecycle including creation, modification, and execution tracking.
    /// Implements canonical patterns for order management with full audit trail.
    /// </summary>
    /// <remarks>
    /// Thread-safe implementation supporting high-frequency order operations.
    /// Maintains order state with microsecond precision timestamps.
    /// Compliant with MiFID II/III requirements for order tracking.
    /// </remarks>
    public class FixOrderManager : CanonicalFixServiceBase, IFixOrderManager
    {
        private readonly ConcurrentDictionary<string, FixOrder> _orders;
        private readonly ConcurrentDictionary<string, FixOrder> _ordersByExchangeId;
        private readonly IFixSessionManager _sessionManager;
        private readonly IFixMessageParser _messageParser;
        private readonly FixMessagePool _messagePool;
        private readonly IFixComplianceService _complianceService;
        private long _orderSequence;
        
        /// <summary>
        /// Initializes a new instance of the FixOrderManager class.
        /// </summary>
        public FixOrderManager(
            ITradingLogger logger,
            IFixSessionManager sessionManager,
            IFixMessageParser messageParser,
            FixMessagePool messagePool,
            IFixComplianceService complianceService,
            IFixPerformanceMonitor? performanceMonitor = null)
            : base(logger, "OrderManager", performanceMonitor)
        {
            _orders = new ConcurrentDictionary<string, FixOrder>();
            _ordersByExchangeId = new ConcurrentDictionary<string, FixOrder>();
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _messageParser = messageParser ?? throw new ArgumentNullException(nameof(messageParser));
            _messagePool = messagePool ?? throw new ArgumentNullException(nameof(messagePool));
            _complianceService = complianceService ?? throw new ArgumentNullException(nameof(complianceService));
            _orderSequence = DateTime.UtcNow.Ticks;
        }
        
        /// <summary>
        /// Initializes the order manager.
        /// </summary>
        public async Task<TradingResult> InitializeAsync()
        {
            LogMethodEntry();
            
            try
            {
                // Initialize compliance service
                var complianceResult = await _complianceService.InitializeAsync();
                if (!complianceResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult.Failure(
                        $"Failed to initialize compliance service: {complianceResult.ErrorMessage}",
                        "COMPLIANCE_INIT_FAILED");
                }
                
                _logger.LogInformation("Order manager initialized");
                
                LogMethodExit();
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize order manager");
                LogMethodExit();
                return TradingResult.Failure(
                    $"Order manager initialization failed: {ex.Message}",
                    "INIT_FAILED");
            }
        }
        
        /// <summary>
        /// Creates and sends a new order through FIX.
        /// </summary>
        public async Task<TradingResult<FixOrder>> CreateAndSendOrderAsync(
            FixSession session,
            OrderRequest request,
            IProgress<OrderExecutionProgress>? progress = null)
        {
            LogMethodEntry();
            
            using var activity = StartActivity("CreateAndSendOrder");
            
            try
            {
                // Validate request
                var validationResult = ValidateOrderRequest(request);
                if (!validationResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<FixOrder>.Failure(
                        validationResult.ErrorMessage,
                        validationResult.ErrorCode);
                }
                
                // Check compliance
                var complianceResult = await _complianceService.CheckOrderComplianceAsync(request);
                if (!complianceResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<FixOrder>.Failure(
                        $"Compliance check failed: {complianceResult.ErrorMessage}",
                        "COMPLIANCE_FAILED");
                }
                
                // Create order
                var order = CreateFixOrder(request);
                
                // Store order
                _orders[order.ClOrdId] = order;
                
                // Report progress
                progress?.Report(new OrderExecutionProgress
                {
                    OrderId = order.ClOrdId,
                    Status = "Creating",
                    PercentComplete = 25,
                    Message = "Order created, preparing to send"
                });
                
                // Build FIX message
                var fixMessage = BuildNewOrderSingle(order, session);
                
                // Send order
                var sendResult = await _sessionManager.SendMessageAsync(
                    session.SessionId, 
                    fixMessage);
                    
                if (!sendResult.IsSuccess)
                {
                    order.Status = OrderStatus.Rejected;
                    LogMethodExit();
                    return TradingResult<FixOrder>.Failure(
                        sendResult.ErrorMessage,
                        sendResult.ErrorCode);
                }
                
                order.Status = OrderStatus.PendingNew;
                
                // Report progress
                progress?.Report(new OrderExecutionProgress
                {
                    OrderId = order.ClOrdId,
                    Status = "Sent",
                    PercentComplete = 50,
                    Message = "Order sent to exchange"
                });
                
                RecordMetric("OrdersCreated", 1);
                RecordMetric("OrderValue", order.Quantity * (order.Price ?? 0));
                
                _logger.LogInformation(
                    "Order created and sent: ClOrdId={ClOrdId}, Symbol={Symbol}, Side={Side}, Qty={Qty}, Price={Price}",
                    order.ClOrdId, order.Symbol, order.Side, order.Quantity, order.Price);
                
                LogMethodExit();
                return TradingResult<FixOrder>.Success(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create and send order");
                RecordMetric("OrderCreateErrors", 1);
                LogMethodExit();
                return TradingResult<FixOrder>.Failure(
                    $"Order creation failed: {ex.Message}",
                    "CREATE_FAILED");
            }
        }
        
        /// <summary>
        /// Cancels an existing order.
        /// </summary>
        public async Task<TradingResult> CancelOrderAsync(string clOrdId, string? sessionId = null)
        {
            LogMethodEntry();
            
            using var activity = StartActivity("CancelOrder");
            
            try
            {
                if (!_orders.TryGetValue(clOrdId, out var order))
                {
                    LogMethodExit();
                    return TradingResult.Failure(
                        $"Order {clOrdId} not found",
                        "ORDER_NOT_FOUND");
                }
                
                // Check if order can be canceled
                if (order.Status == OrderStatus.Filled || 
                    order.Status == OrderStatus.Canceled ||
                    order.Status == OrderStatus.Expired)
                {
                    LogMethodExit();
                    return TradingResult.Failure(
                        $"Order {clOrdId} cannot be canceled in status {order.Status}",
                        "INVALID_STATUS");
                }
                
                // Get session
                var sessionResult = await _sessionManager.GetActiveSessionAsync(sessionId);
                if (!sessionResult.IsSuccess || sessionResult.Value == null)
                {
                    LogMethodExit();
                    return TradingResult.Failure(
                        "No active session for cancel",
                        "NO_SESSION");
                }
                
                // Build cancel request
                var cancelMessage = BuildOrderCancelRequest(order, sessionResult.Value);
                
                // Send cancel
                var sendResult = await _sessionManager.SendMessageAsync(
                    sessionResult.Value.SessionId,
                    cancelMessage);
                    
                if (!sendResult.IsSuccess)
                {
                    LogMethodExit();
                    return sendResult;
                }
                
                order.Status = OrderStatus.PendingCancel;
                
                RecordMetric("CancelRequests", 1);
                
                _logger.LogInformation("Cancel request sent for order {ClOrdId}", clOrdId);
                
                LogMethodExit();
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel order {ClOrdId}", clOrdId);
                LogMethodExit();
                return TradingResult.Failure(
                    $"Cancel failed: {ex.Message}",
                    "CANCEL_FAILED");
            }
        }
        
        /// <summary>
        /// Replaces an existing order.
        /// </summary>
        public async Task<TradingResult<FixOrder>> ReplaceOrderAsync(
            string originalClOrdId,
            OrderRequest newRequest,
            string? sessionId = null)
        {
            LogMethodEntry();
            
            using var activity = StartActivity("ReplaceOrder");
            
            try
            {
                if (!_orders.TryGetValue(originalClOrdId, out var originalOrder))
                {
                    LogMethodExit();
                    return TradingResult<FixOrder>.Failure(
                        $"Original order {originalClOrdId} not found",
                        "ORDER_NOT_FOUND");
                }
                
                // Validate new request
                var validationResult = ValidateOrderRequest(newRequest);
                if (!validationResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<FixOrder>.Failure(
                        validationResult.ErrorMessage,
                        validationResult.ErrorCode);
                }
                
                // Check compliance for modification
                var complianceResult = await _complianceService.CheckOrderModificationAsync(
                    originalOrder, newRequest);
                if (!complianceResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<FixOrder>.Failure(
                        $"Compliance check failed: {complianceResult.ErrorMessage}",
                        "COMPLIANCE_FAILED");
                }
                
                // Get session
                var sessionResult = await _sessionManager.GetActiveSessionAsync(sessionId);
                if (!sessionResult.IsSuccess || sessionResult.Value == null)
                {
                    LogMethodExit();
                    return TradingResult<FixOrder>.Failure(
                        "No active session for replace",
                        "NO_SESSION");
                }
                
                // Create new order with updated values
                var newOrder = CreateFixOrder(newRequest);
                newOrder.OrderId = originalOrder.OrderId; // Preserve exchange order ID
                
                // Build replace request
                var replaceMessage = BuildOrderCancelReplace(
                    originalOrder, newOrder, sessionResult.Value);
                
                // Send replace
                var sendResult = await _sessionManager.SendMessageAsync(
                    sessionResult.Value.SessionId,
                    replaceMessage);
                    
                if (!sendResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<FixOrder>.Failure(
                        sendResult.ErrorMessage,
                        sendResult.ErrorCode);
                }
                
                // Store new order
                _orders[newOrder.ClOrdId] = newOrder;
                originalOrder.Status = OrderStatus.PendingReplace;
                newOrder.Status = OrderStatus.PendingReplace;
                
                RecordMetric("ReplaceRequests", 1);
                
                _logger.LogInformation(
                    "Replace request sent: Original={Original}, New={New}",
                    originalClOrdId, newOrder.ClOrdId);
                
                LogMethodExit();
                return TradingResult<FixOrder>.Success(newOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to replace order {ClOrdId}", originalClOrdId);
                LogMethodExit();
                return TradingResult<FixOrder>.Failure(
                    $"Replace failed: {ex.Message}",
                    "REPLACE_FAILED");
            }
        }
        
        /// <summary>
        /// Gets order status.
        /// </summary>
        public async Task<TradingResult<FixOrder>> GetOrderStatusAsync(string clOrdId)
        {
            LogMethodEntry();
            
            try
            {
                if (_orders.TryGetValue(clOrdId, out var order))
                {
                    LogMethodExit();
                    return TradingResult<FixOrder>.Success(order);
                }
                
                LogMethodExit();
                return TradingResult<FixOrder>.Failure(
                    $"Order {clOrdId} not found",
                    "ORDER_NOT_FOUND");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order status");
                LogMethodExit();
                return TradingResult<FixOrder>.Failure(
                    $"Status query failed: {ex.Message}",
                    "STATUS_FAILED");
            }
        }
        
        /// <summary>
        /// Processes an execution report from the exchange.
        /// </summary>
        public async Task<TradingResult> ProcessExecutionReportAsync(FixExecutionReport report)
        {
            LogMethodEntry();
            
            using var activity = StartActivity("ProcessExecutionReport");
            
            try
            {
                if (report == null)
                {
                    LogMethodExit();
                    return TradingResult.Failure(
                        "Execution report cannot be null",
                        "NULL_REPORT");
                }
                
                // Find order
                if (!_orders.TryGetValue(report.ClOrdId, out var order))
                {
                    _logger.LogWarning("Received execution report for unknown order: {ClOrdId}", 
                        report.ClOrdId);
                    LogMethodExit();
                    return TradingResult.Failure(
                        $"Order {report.ClOrdId} not found",
                        "ORDER_NOT_FOUND");
                }
                
                // Update order state
                order.Status = report.OrderStatus;
                order.ExecutedQuantity = report.CumulativeQuantity;
                order.AveragePrice = report.AveragePrice > 0 ? report.AveragePrice : order.AveragePrice;
                order.UpdateTime = DateTime.UtcNow;
                
                // Store exchange order ID
                if (!string.IsNullOrEmpty(report.OrderId) && string.IsNullOrEmpty(order.OrderId))
                {
                    order.OrderId = report.OrderId;
                    _ordersByExchangeId[report.OrderId] = order;
                }
                
                // Record metrics based on execution type
                switch (report.ExecType)
                {
                    case ExecType.New:
                        RecordMetric("OrdersAccepted", 1);
                        break;
                    case ExecType.PartialFill:
                    case ExecType.Fill:
                        RecordMetric("OrdersFilled", 1);
                        RecordMetric("VolumeExecuted", report.LastQuantity);
                        RecordMetric("NotionalExecuted", report.LastQuantity * report.LastPrice);
                        break;
                    case ExecType.Canceled:
                        RecordMetric("OrdersCanceled", 1);
                        break;
                    case ExecType.Rejected:
                        RecordMetric("OrdersRejected", 1);
                        break;
                }
                
                // Notify compliance service
                await _complianceService.RecordExecutionAsync(order, report);
                
                _logger.LogInformation(
                    "Processed execution report: ClOrdId={ClOrdId}, ExecType={ExecType}, Status={Status}, " +
                    "LastQty={LastQty}, LastPx={LastPx}, CumQty={CumQty}, AvgPx={AvgPx}",
                    report.ClOrdId, report.ExecType, report.OrderStatus,
                    report.LastQuantity, report.LastPrice, report.CumulativeQuantity, report.AveragePrice);
                
                LogMethodExit();
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process execution report");
                LogMethodExit();
                return TradingResult.Failure(
                    $"Execution processing failed: {ex.Message}",
                    "PROCESS_FAILED");
            }
        }
        
        /// <summary>
        /// Validates an order request.
        /// </summary>
        private TradingResult ValidateOrderRequest(OrderRequest request)
        {
            if (request == null)
                return TradingResult.Failure("Order request cannot be null", "NULL_REQUEST");
                
            if (string.IsNullOrWhiteSpace(request.Symbol))
                return TradingResult.Failure("Symbol is required", "MISSING_SYMBOL");
                
            if (request.Quantity <= 0)
                return TradingResult.Failure("Quantity must be positive", "INVALID_QUANTITY");
                
            if (request.OrderType == OrderType.Limit && (!request.Price.HasValue || request.Price <= 0))
                return TradingResult.Failure("Price is required for limit orders", "MISSING_PRICE");
                
            if (request.OrderType == OrderType.Stop && (!request.StopPrice.HasValue || request.StopPrice <= 0))
                return TradingResult.Failure("Stop price is required for stop orders", "MISSING_STOP_PRICE");
                
            return TradingResult.Success();
        }
        
        /// <summary>
        /// Creates a FIX order from request.
        /// </summary>
        private FixOrder CreateFixOrder(OrderRequest request)
        {
            var clOrdId = GenerateClOrdId();
            
            return new FixOrder
            {
                ClOrdId = clOrdId,
                Symbol = request.Symbol,
                Side = request.Side,
                OrderType = request.OrderType,
                Quantity = request.Quantity,
                Price = request.Price,
                StopPrice = request.StopPrice,
                TimeInForce = request.TimeInForce,
                Status = OrderStatus.PendingNew,
                CreateTime = DateTime.UtcNow,
                HardwareTimestamp = GetHardwareTimestamp(),
                AlgorithmId = request.AlgorithmId,
                TradingCapacity = request.TradingCapacity
            };
        }
        
        /// <summary>
        /// Generates a unique client order ID.
        /// </summary>
        private string GenerateClOrdId()
        {
            var sequence = System.Threading.Interlocked.Increment(ref _orderSequence);
            return $"ORD{DateTime.UtcNow:yyyyMMddHHmmss}{sequence:D6}";
        }
        
        /// <summary>
        /// Builds a New Order Single FIX message.
        /// </summary>
        private FixMessage BuildNewOrderSingle(FixOrder order, FixSession session)
        {
            var message = _messagePool.Rent();
            
            message.MessageType = "D"; // New Order Single
            message.Fields[11] = order.ClOrdId; // ClOrdID
            message.Fields[21] = "1"; // HandlInst = Automated
            message.Fields[55] = order.Symbol; // Symbol
            message.Fields[54] = ((char)order.Side).ToString(); // Side
            message.Fields[60] = DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff"); // TransactTime
            message.Fields[40] = ((char)order.OrderType).ToString(); // OrdType
            message.Fields[38] = order.Quantity.ToString("F8"); // OrderQty
            
            if (order.Price.HasValue)
                message.Fields[44] = order.Price.Value.ToString("F8"); // Price
                
            if (order.StopPrice.HasValue)
                message.Fields[99] = order.StopPrice.Value.ToString("F8"); // StopPx
                
            message.Fields[59] = ((char)order.TimeInForce).ToString(); // TimeInForce
            
            // MiFID II fields
            if (!string.IsNullOrEmpty(order.AlgorithmId))
                message.Fields[7928] = order.AlgorithmId; // AlgoID
                
            if (order.TradingCapacity.HasValue)
                message.Fields[1815] = order.TradingCapacity.Value.ToString(); // TradingCapacity
            
            return message;
        }
        
        /// <summary>
        /// Builds an Order Cancel Request FIX message.
        /// </summary>
        private FixMessage BuildOrderCancelRequest(FixOrder order, FixSession session)
        {
            var message = _messagePool.Rent();
            
            message.MessageType = "F"; // Order Cancel Request
            message.Fields[11] = GenerateClOrdId(); // ClOrdID (new ID for cancel)
            message.Fields[41] = order.ClOrdId; // OrigClOrdID
            message.Fields[55] = order.Symbol; // Symbol
            message.Fields[54] = ((char)order.Side).ToString(); // Side
            message.Fields[60] = DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff"); // TransactTime
            
            if (!string.IsNullOrEmpty(order.OrderId))
                message.Fields[37] = order.OrderId; // OrderID
            
            return message;
        }
        
        /// <summary>
        /// Builds an Order Cancel/Replace Request FIX message.
        /// </summary>
        private FixMessage BuildOrderCancelReplace(
            FixOrder originalOrder, 
            FixOrder newOrder, 
            FixSession session)
        {
            var message = _messagePool.Rent();
            
            message.MessageType = "G"; // Order Cancel/Replace Request
            message.Fields[11] = newOrder.ClOrdId; // ClOrdID
            message.Fields[41] = originalOrder.ClOrdId; // OrigClOrdID
            message.Fields[55] = newOrder.Symbol; // Symbol
            message.Fields[54] = ((char)newOrder.Side).ToString(); // Side
            message.Fields[60] = DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff"); // TransactTime
            message.Fields[40] = ((char)newOrder.OrderType).ToString(); // OrdType
            message.Fields[38] = newOrder.Quantity.ToString("F8"); // OrderQty
            
            if (newOrder.Price.HasValue)
                message.Fields[44] = newOrder.Price.Value.ToString("F8"); // Price
                
            if (!string.IsNullOrEmpty(originalOrder.OrderId))
                message.Fields[37] = originalOrder.OrderId; // OrderID
            
            return message;
        }
    }
    
    /// <summary>
    /// Interface for FIX compliance service.
    /// </summary>
    public interface IFixComplianceService
    {
        Task<TradingResult> InitializeAsync();
        Task<TradingResult> CheckOrderComplianceAsync(Trading.OrderRequest request);
        Task<TradingResult> CheckOrderModificationAsync(FixOrder originalOrder, Trading.OrderRequest newRequest);
        Task<TradingResult> RecordExecutionAsync(FixOrder order, FixExecutionReport report);
    }
}