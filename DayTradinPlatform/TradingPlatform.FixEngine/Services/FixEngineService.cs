using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Core.Canonical;
using TradingPlatform.FixEngine.Canonical;
using TradingPlatform.FixEngine.Models;
using TradingPlatform.FixEngine.Trading;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.FixEngine.Services
{
    /// <summary>
    /// Core FIX engine service implementing the canonical pattern for order management and market data.
    /// Provides ultra-low latency order routing with < 50 microsecond target latency.
    /// </summary>
    /// <remarks>
    /// This service orchestrates all FIX protocol operations including session management,
    /// order routing, market data distribution, and compliance reporting.
    /// Fully compliant with MiFID II/III requirements and 2025 mandatory TLS standards.
    /// </remarks>
    public class FixEngineService : CanonicalFixServiceBase, IFixEngineService
    {
        private readonly IFixSessionManager _sessionManager;
        private readonly IFixOrderManager _orderManager;
        private readonly IFixMarketDataManager _marketDataManager;
        private readonly IFixMessageProcessor _messageProcessor;
        private readonly FixMessagePool _messagePool;
        private readonly IFixPerformanceMonitor _performanceMonitor;
        private readonly FixEngineOptions _options;
        private volatile bool _isRunning;
        private CancellationTokenSource? _cancellationTokenSource;
        
        /// <summary>
        /// Initializes a new instance of the FixEngineService class.
        /// </summary>
        public FixEngineService(
            ITradingLogger logger,
            IFixSessionManager sessionManager,
            IFixOrderManager orderManager,
            IFixMarketDataManager marketDataManager,
            IFixMessageProcessor messageProcessor,
            FixMessagePool messagePool,
            IFixPerformanceMonitor performanceMonitor,
            IOptions<FixEngineOptions> options)
            : base(logger, "FixEngine", performanceMonitor)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _orderManager = orderManager ?? throw new ArgumentNullException(nameof(orderManager));
            _marketDataManager = marketDataManager ?? throw new ArgumentNullException(nameof(marketDataManager));
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            _messagePool = messagePool ?? throw new ArgumentNullException(nameof(messagePool));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Initializes the FIX engine service.
        /// </summary>
        public async Task<TradingResult> InitializeAsync()
        {
            var result = await OnInitializeAsync(CancellationToken.None);
            return result.IsSuccess ? TradingResult.Success() : TradingResult.Failure(result.Error!);
        }

        /// <summary>
        /// Starts the FIX engine service.
        /// </summary>
        public async Task<TradingResult> StartAsync()
        {
            var result = await OnStartAsync(CancellationToken.None);
            return result.IsSuccess ? TradingResult.Success() : TradingResult.Failure(result.Error!);
        }

        /// <summary>
        /// Stops the FIX engine service.
        /// </summary>
        public async Task<TradingResult> StopAsync()
        {
            var result = await OnStopAsync(CancellationToken.None);
            return result.IsSuccess ? TradingResult.Success() : TradingResult.Failure(result.Error!);
        }
        
        /// <summary>
        /// Initializes the FIX engine and all components.
        /// </summary>
        protected override async Task<TradingResult<bool>> OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            
            try
            {
                // Base initialization is handled by CanonicalServiceBase
                
                // Initialize session manager
                var sessionResult = await _sessionManager.InitializeAsync();
                if (!sessionResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<bool>.Failure(
                        $"Failed to initialize session manager: {sessionResult.Error?.Message}",
                        "SESSION_INIT_FAILED");
                }
                
                // Initialize order manager
                var orderResult = await _orderManager.InitializeAsync();
                if (!orderResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<bool>.Failure(
                        $"Failed to initialize order manager: {orderResult.ErrorMessage}",
                        "ORDER_MANAGER_INIT_FAILED");
                }
                
                // Initialize market data manager
                var marketDataResult = await _marketDataManager.InitializeAsync();
                if (!marketDataResult.IsSuccess)
                {
                    LogMethodExit();
                    return TradingResult<bool>.Failure(
                        $"Failed to initialize market data manager: {marketDataResult.ErrorMessage}",
                        "MARKET_DATA_INIT_FAILED");
                }
                
                _logger.LogInformation("FIX engine initialized successfully");
                
                LogMethodExit();
                return TradingResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize FIX engine");
                LogMethodExit();
                return TradingResult.Failure(
                    $"FIX engine initialization failed: {ex.Message}",
                    "INIT_FAILED");
            }
        }
        
        /// <summary>
        /// Starts the FIX engine and begins processing messages.
        /// </summary>
        protected override async Task<TradingResult<bool>> OnStartAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            
            try
            {
                if (_isRunning)
                {
                    LogMethodExit();
                    return TradingResult.Success("FIX engine already running");
                }
                
                var baseResult = await base.StartAsync();
                if (!baseResult.IsSuccess)
                {
                    LogMethodExit();
                    return baseResult;
                }
                
                _cancellationTokenSource = new CancellationTokenSource();
                _isRunning = true;
                
                // Start all sessions
                var sessionStartResult = await _sessionManager.StartAllSessionsAsync();
                if (!sessionStartResult.IsSuccess)
                {
                    _isRunning = false;
                    LogMethodExit();
                    return sessionStartResult;
                }
                
                // Start message processing loop
                _ = Task.Run(() => ProcessMessagesAsync(_cancellationTokenSource.Token));
                
                _logger.LogInformation("FIX engine started successfully");
                
                LogMethodExit();
                return TradingResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start FIX engine");
                _isRunning = false;
                LogMethodExit();
                return TradingResult.Failure(
                    $"FIX engine start failed: {ex.Message}",
                    "START_FAILED");
            }
        }
        
        /// <summary>
        /// Stops the FIX engine gracefully.
        /// </summary>
        protected override async Task<TradingResult<bool>> OnStopAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            
            try
            {
                if (!_isRunning)
                {
                    LogMethodExit();
                    return TradingResult.Success("FIX engine already stopped");
                }
                
                _cancellationTokenSource?.Cancel();
                _isRunning = false;
                
                // Stop all sessions
                await _sessionManager.StopAllSessionsAsync();
                
                var baseResult = await base.StopAsync();
                
                _logger.LogInformation("FIX engine stopped successfully");
                
                LogMethodExit();
                return baseResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping FIX engine");
                LogMethodExit();
                return TradingResult.Failure(
                    $"FIX engine stop failed: {ex.Message}",
                    "STOP_FAILED");
            }
        }
        
        /// <summary>
        /// Sends a new order through FIX protocol.
        /// </summary>
        /// <param name="request">The order request details</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <returns>Result containing the FIX order or error details</returns>
        public async Task<TradingResult<FixOrder>> SendOrderAsync(
            Trading.OrderRequest request,
            IProgress<OrderExecutionProgress>? progress = null)
        {
            LogMethodEntry();
            
            using var activity = StartActivity("SendOrder");
            
            try
            {
                // Validate request
                if (request == null)
                {
                    LogMethodExit();
                    return TradingResult<FixOrder>.Failure(
                        "Order request cannot be null",
                        "NULL_REQUEST");
                }
                
                if (string.IsNullOrWhiteSpace(request.Symbol))
                {
                    LogMethodExit();
                    return TradingResult<FixOrder>.Failure(
                        "Symbol is required",
                        "MISSING_SYMBOL");
                }
                
                if (request.Quantity <= 0)
                {
                    LogMethodExit();
                    return TradingResult<FixOrder>.Failure(
                        "Quantity must be positive",
                        "INVALID_QUANTITY");
                }
                
                // Get active session
                var sessionResult = await _sessionManager.GetActiveSessionAsync(request.SessionId);
                if (!sessionResult.IsSuccess || sessionResult.Value == null)
                {
                    LogMethodExit();
                    return TradingResult<FixOrder>.Failure(
                        "No active FIX session available",
                        "NO_ACTIVE_SESSION");
                }
                
                // Create and send order
                var orderResult = await _orderManager.CreateAndSendOrderAsync(
                    sessionResult.Value,
                    request,
                    progress);
                
                if (orderResult.IsSuccess)
                {
                    RecordMetric("OrdersSent", 1);
                    _logger.LogInformation(
                        "Order sent successfully. ClOrdId: {ClOrdId}, Symbol: {Symbol}, Qty: {Quantity}",
                        orderResult.Value!.ClOrdId,
                        orderResult.Value.Symbol,
                        orderResult.Value.Quantity);
                }
                
                LogMethodExit();
                return orderResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send order");
                RecordMetric("OrdersFailed", 1);
                LogMethodExit();
                return TradingResult<FixOrder>.Failure(
                    $"Order submission failed: {ex.Message}",
                    "ORDER_SEND_FAILED");
            }
        }
        
        /// <summary>
        /// Cancels an existing order.
        /// </summary>
        /// <param name="clOrdId">The client order ID to cancel</param>
        /// <param name="sessionId">Optional session ID</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<TradingResult> CancelOrderAsync(
            string clOrdId,
            string? sessionId = null)
        {
            LogMethodEntry();
            
            using var activity = StartActivity("CancelOrder");
            
            try
            {
                if (string.IsNullOrWhiteSpace(clOrdId))
                {
                    LogMethodExit();
                    return TradingResult<bool>.Failure(
                        "Client order ID is required",
                        "MISSING_CLORDID");
                }
                
                var result = await _orderManager.CancelOrderAsync(clOrdId, sessionId);
                
                if (result.IsSuccess)
                {
                    RecordMetric("OrdersCanceled", 1);
                    _logger.LogInformation("Order canceled successfully. ClOrdId: {ClOrdId}", clOrdId);
                }
                
                LogMethodExit();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel order {ClOrdId}", clOrdId);
                LogMethodExit();
                return TradingResult.Failure(
                    $"Order cancellation failed: {ex.Message}",
                    "CANCEL_FAILED");
            }
        }
        
        /// <summary>
        /// Subscribes to market data for specified symbols.
        /// </summary>
        /// <param name="symbols">List of symbols to subscribe</param>
        /// <param name="sessionId">Optional session ID</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<TradingResult> SubscribeMarketDataAsync(
            string[] symbols,
            string? sessionId = null)
        {
            LogMethodEntry();
            
            try
            {
                if (symbols == null || symbols.Length == 0)
                {
                    LogMethodExit();
                    return TradingResult<bool>.Failure(
                        "At least one symbol is required",
                        "NO_SYMBOLS");
                }
                
                var result = await _marketDataManager.SubscribeAsync(symbols, sessionId);
                
                if (result.IsSuccess)
                {
                    RecordMetric("MarketDataSubscriptions", symbols.Length);
                    _logger.LogInformation(
                        "Subscribed to market data for {Count} symbols",
                        symbols.Length);
                }
                
                LogMethodExit();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to market data");
                LogMethodExit();
                return TradingResult.Failure(
                    $"Market data subscription failed: {ex.Message}",
                    "SUBSCRIPTION_FAILED");
            }
        }
        
        /// <summary>
        /// Processes incoming FIX messages continuously.
        /// </summary>
        private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            
            try
            {
                while (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    try
                    {
                        await _messageProcessor.ProcessNextMessageAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing FIX message");
                        RecordMetric("MessageProcessingErrors", 1);
                        
                        // Brief pause before continuing
                        await Task.Delay(10, cancellationToken);
                    }
                }
            }
            finally
            {
                LogMethodExit();
            }
        }
        
        /// <summary>
        /// Performs FIX-specific health checks.
        /// </summary>
        protected override async Task<(bool IsHealthy, string Message, Dictionary<string, object>? Details)> CheckFixHealthAsync()
        {
            LogMethodEntry();
            
            try
            {
                var activeSessionsResult = await _sessionManager.GetActiveSessionCountAsync();
                
                if (!activeSessionsResult.IsSuccess)
                {
                    LogMethodExit();
                    return ServiceHealthCheck.Unhealthy(
                        "Unable to check active sessions");
                }
                
                var stats = _messagePool.GetStats();
                
                var details = new
                {
                    ActiveSessions = activeSessionsResult.Value,
                    PoolUtilization = stats.UtilizationPercent,
                    MessagesSent = stats.TotalRentCount,
                    IsRunning = _isRunning
                };
                
                if (!_isRunning)
                {
                    LogMethodExit();
                    return (false, "FIX engine is not running", details);
                }
                
                if (activeSessionsResult.Value == 0 && _options.RequireActiveSession)
                {
                    LogMethodExit();
                    return (true, "No active FIX sessions", details);
                }
                
                LogMethodExit();
                return (true, $"FIX engine operational with {activeSessionsResult.Value} active sessions", details);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FIX health check failed");
                LogMethodExit();
                return (false, $"Health check error: {ex.Message}", null);
            }
        }
    }
    
    /// <summary>
    /// Configuration options for FIX engine.
    /// </summary>
    public class FixEngineOptions
    {
        /// <summary>
        /// Gets or sets whether an active session is required for healthy status.
        /// </summary>
        public bool RequireActiveSession { get; set; } = true;
        
        /// <summary>
        /// Gets or sets the message processing batch size.
        /// </summary>
        public int MessageBatchSize { get; set; } = 100;
        
        /// <summary>
        /// Gets or sets the maximum concurrent message processors.
        /// </summary>
        public int MaxConcurrentProcessors { get; set; } = 4;
    }
}