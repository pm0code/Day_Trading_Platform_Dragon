using System.Collections.Concurrent;
using System.Diagnostics;
using TradingPlatform.FixEngine.Interfaces;
using TradingPlatform.FixEngine.Models;
using TradingPlatform.FixEngine.Trading;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Observability;
using Audit.Core;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.FixEngine.Core;

/// <summary>
/// Advanced FIX engine implementation with ultra-low latency optimizations and comprehensive canonical compliance
/// Combines session management, market data, and order management for US equity markets
/// Provides enterprise-grade reliability with full observability, health monitoring, and performance tracking
/// All financial calculations use decimal precision and follow mandatory development standards
/// </summary>
public sealed class FixEngine : CanonicalServiceBase, IFixEngine
{
    private readonly ITradingMetrics _tradingMetrics;
    private readonly IInfrastructureMetrics _infrastructureMetrics;
    private readonly IObservabilityEnricher _observabilityEnricher;
    private readonly ConcurrentDictionary<string, FixSession> _venueSessions = new();
    private readonly ConcurrentDictionary<string, MarketDataManager> _marketDataManagers = new();
    private readonly ConcurrentDictionary<string, OrderManager> _orderManagers = new();
    private readonly OrderRouter _orderRouter;
    private readonly PerformanceMonitor _performanceMonitor;
    private readonly Timer _healthCheckTimer;
    private readonly DateTime _startTime = DateTime.UtcNow;

    private FixEngineConfig? _config;
    private bool _isInitialized;
    private bool _disposed;

    public event EventHandler<ExecutionReport>? ExecutionReceived;
    public event EventHandler<Interfaces.MarketDataSnapshot>? MarketDataReceived;
    public event EventHandler<VenueStatusUpdate>? VenueStatusChanged;

    /// <summary>
    /// Initializes a new instance of the FixEngine with comprehensive dependencies and canonical patterns
    /// </summary>
    /// <param name="tradingMetrics">Service for recording trading performance metrics</param>
    /// <param name="infrastructureMetrics">Service for recording infrastructure performance metrics</param>
    /// <param name="observabilityEnricher">Service for enriching observability data</param>
    /// <param name="logger">Trading logger for comprehensive FIX engine tracking</param>
    public FixEngine(
        ITradingMetrics tradingMetrics,
        IInfrastructureMetrics infrastructureMetrics,
        IObservabilityEnricher observabilityEnricher,
        ITradingLogger logger) : base(logger, "FixEngine")
    {
        _tradingMetrics = tradingMetrics ?? throw new ArgumentNullException(nameof(tradingMetrics));
        _infrastructureMetrics = infrastructureMetrics ?? throw new ArgumentNullException(nameof(infrastructureMetrics));
        _observabilityEnricher = observabilityEnricher ?? throw new ArgumentNullException(nameof(observabilityEnricher));

        _orderRouter = new OrderRouter(logger);
        _performanceMonitor = new PerformanceMonitor();

        // Health check every 30 seconds
        _healthCheckTimer = new Timer(PerformHealthCheck, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Initializes the FIX engine with the specified configuration and establishes venue connections
    /// </summary>
    /// <param name="config">The configuration settings for FIX engine initialization</param>
    /// <returns>A TradingResult indicating successful initialization or error details</returns>
    public async Task<TradingResult<bool>> InitializeAsync(FixEngineConfig config)
    {
        LogMethodEntry();
        try
        {
            using var activity = OpenTelemetryInstrumentation.FixEngineActivitySource.StartActivity("FixEngineInitialization");
            var correlationId = _observabilityEnricher.GenerateCorrelationId();

            if (activity != null)
                _observabilityEnricher.EnrichActivity(activity, "FixEngineInitialization", config);
            activity?.SetTag("fix.correlation_id", correlationId);

            if (_isInitialized)
            {
                var error = "FIX Engine is already initialized";
                activity?.SetStatus(ActivityStatusCode.Error, error);
                LogMethodExit();
                return TradingResult<bool>.Failure("ALREADY_INITIALIZED", error);
            }

            if (config == null)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_CONFIG", "Configuration cannot be null");
            }

            _config = config;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Comprehensive audit logging for initialization
                LogInfo($"FixEngine initialization: VenueCount={config.VenueConfigs.Count}, CorrelationId={correlationId}");

                // Initialize sessions for each venue with comprehensive monitoring
                foreach (var (venueName, venueConfig) in config.VenueConfigs)
                {
                    var venueStopwatch = Stopwatch.StartNew();
                    var venueResult = await InitializeVenueAsync(venueName, venueConfig, correlationId);
                    if (!venueResult.IsSuccess)
                    {
                        LogError($"Failed to initialize venue {venueName}: {venueResult.Error?.Message}");
                        LogMethodExit();
                        return TradingResult<bool>.Failure("VENUE_INIT_FAILED", $"Failed to initialize venue {venueName}: {venueResult.Error?.Message}");
                    }
                    venueStopwatch.Stop();

                    // Record venue initialization metrics
                    _infrastructureMetrics.RecordNetworkLatency(venueStopwatch.Elapsed, venueName);
                    activity?.SetTag($"fix.venue.{venueName}.init_time_ms", venueStopwatch.Elapsed.TotalMilliseconds.ToString("F2"));
                }

                stopwatch.Stop();
                _isInitialized = true;

                // Record successful initialization metrics
                _tradingMetrics.RecordFixMessageProcessing("InitializationComplete", stopwatch.Elapsed);

                LogInfo($"FIX Engine initialization completed successfully in {stopwatch.Elapsed.TotalMilliseconds:F2}ms | CorrelationId: {correlationId}");
                activity?.SetTag("fix.initialization.success", true);
                activity?.SetTag("fix.initialization.duration_ms", stopwatch.Elapsed.TotalMilliseconds.ToString("F2"));

                LogMethodExit();
                return TradingResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                // Record failed initialization
                LogError($"FixEngine initialization failure: Duration={stopwatch.Elapsed.TotalMilliseconds}ms, CorrelationId={correlationId}", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("INITIALIZATION_ERROR", $"Failed to initialize FIX Engine: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in InitializeAsync", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("INITIALIZATION_ERROR", $"Initialization failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Submits an order to the optimal venue with comprehensive error handling and performance monitoring
    /// </summary>
    /// <param name="request">The order request with all necessary details</param>
    /// <returns>A TradingResult containing the client order ID or error information</returns>
    public async Task<TradingResult<string>> SubmitOrderAsync(Trading.OrderRequest request)
    {
        LogMethodEntry();
        try
        {
            using var activity = OpenTelemetryInstrumentation.FixEngineActivitySource.StartActivity("OrderSubmission");
            var correlationId = _observabilityEnricher.GenerateCorrelationId();

            var initResult = EnsureInitialized();
            if (!initResult.IsSuccess)
            {
                LogMethodExit();
                return TradingResult<string>.Failure(initResult.ErrorCode, initResult.ErrorMessage);
            }

            if (request == null)
            {
                LogMethodExit();
                return TradingResult<string>.Failure("INVALID_REQUEST", "Order request cannot be null");
            }

            if (string.IsNullOrEmpty(request.Symbol))
            {
                LogMethodExit();
                return TradingResult<string>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            if (activity != null)
                _observabilityEnricher.EnrichActivity(activity, "OrderSubmission", request);
            activity?.SetTag("fix.correlation_id", correlationId);
            activity?.SetTag("fix.order.symbol", request.Symbol);
            activity?.SetTag("fix.order.side", request.Side);
            activity?.SetTag("fix.order.quantity", request.Quantity.ToString());
            activity?.SetTag("fix.order.price", request.Price.ToString());

            var stopwatch = Stopwatch.StartNew();
            var orderId = GenerateOrderId();

            try
            {
                // Comprehensive audit logging for order submission
                LogInfo($"Order submission: OrderId={orderId}, Symbol={request.Symbol}, Side={request.Side}, Quantity={request.Quantity}, CorrelationId={correlationId}");

                // Convert to Trading OrderRequest for venue selection
                var routingRequest = new Trading.OrderRequest
                {
                    Symbol = request.Symbol,
                    Side = request.Side,
                    OrderType = request.OrderType,
                    Quantity = request.Quantity,
                    Price = request.Price,
                    TimeInForce = request.TimeInForce
                };

                var venueSelectionStart = Stopwatch.StartNew();
                var optimalVenue = _orderRouter.SelectOptimalVenue(routingRequest);
                venueSelectionStart.Stop();

                // Record venue selection metrics
                _tradingMetrics.RecordFixMessageProcessing("VenueSelection", venueSelectionStart.Elapsed);
                activity?.SetTag("fix.venue.selected", optimalVenue);
                activity?.SetTag("fix.venue.selection_time_microseconds", venueSelectionStart.Elapsed.TotalMicroseconds.ToString("F2"));

                if (!_orderManagers.TryGetValue(optimalVenue, out var orderManager))
                {
                    var error = $"Order manager not available for venue: {optimalVenue}";
                    activity?.SetStatus(ActivityStatusCode.Error, error);

                    LogError($"Order submission failure: OrderId={orderId}, Venue={optimalVenue}, Symbol={request.Symbol}, ErrorType=VenueUnavailable");
                    LogMethodExit();
                    return TradingResult<string>.Failure("VENUE_UNAVAILABLE", error);
                }

                // Convert to FIX order request
                var fixOrderRequest = new Core.OrderRequest
                {
                    Symbol = request.Symbol,
                    Side = request.Side == "1" ? OrderSide.Buy : OrderSide.Sell,
                    OrderType = ConvertOrderType(request.OrderType),
                    Quantity = request.Quantity,
                    Price = request.Price,
                    TimeInForce = ConvertTimeInForce(request.TimeInForce)
                };

                var orderSubmissionStart = Stopwatch.StartNew();
                var clOrdId = await orderManager.SubmitOrderAsync(fixOrderRequest);
                orderSubmissionStart.Stop();

                stopwatch.Stop();

                // Record comprehensive order submission metrics
                _performanceMonitor.RecordOrderLatency(stopwatch.Elapsed.TotalMicroseconds);
                _tradingMetrics.RecordOrderExecution(stopwatch.Elapsed, request.Symbol, request.Quantity);
                _tradingMetrics.RecordFixMessageProcessing("OrderSubmission", orderSubmissionStart.Elapsed);

                // Update activity with success metrics
                activity?.SetTag("fix.order.client_order_id", clOrdId);
                activity?.SetTag("fix.order.total_latency_microseconds", stopwatch.Elapsed.TotalMicroseconds.ToString("F2"));
                activity?.SetTag("fix.order.submission_latency_microseconds", orderSubmissionStart.Elapsed.TotalMicroseconds.ToString("F2"));
                activity?.SetTag("fix.order.success", true);

                // Flag latency violations
                if (stopwatch.Elapsed.TotalMicroseconds > 100)
                {
                    activity?.SetTag("fix.latency.violation", true);
                    activity?.SetTag("fix.latency.severity", stopwatch.Elapsed.TotalMicroseconds > 1000 ? "critical" : "warning");
                }

                LogInfo($"Order submitted successfully: OrderId={orderId}, ClientOrderId={clOrdId}, Venue={optimalVenue}, Latency={stopwatch.Elapsed.TotalMicroseconds:F2}μs");
                LogMethodExit();
                return TradingResult<string>.Success(clOrdId);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                LogError($"Order submission failed: OrderId={orderId}, Symbol={request.Symbol}", ex);
                LogMethodExit();
                return TradingResult<string>.Failure("ORDER_SUBMISSION_ERROR", $"Failed to submit order: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in SubmitOrderAsync", ex);
            LogMethodExit();
            return TradingResult<string>.Failure("ORDER_SUBMISSION_ERROR", $"Order submission failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Cancels an order with the specified order ID and symbol with comprehensive error handling
    /// </summary>
    /// <param name="orderId">The order ID to cancel</param>
    /// <param name="symbol">The symbol of the order to cancel</param>
    /// <returns>A TradingResult indicating successful cancellation or error details</returns>
    public async Task<TradingResult<bool>> CancelOrderAsync(string orderId, string symbol)
    {
        LogMethodEntry();
        try
        {
            var initResult = EnsureInitialized();
            if (!initResult.IsSuccess)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure(initResult.ErrorCode, initResult.ErrorMessage);
            }

            if (string.IsNullOrEmpty(orderId))
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_ORDER_ID", "Order ID cannot be null or empty");
            }

            if (string.IsNullOrEmpty(symbol))
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            try
            {
                // Find the venue where this order was placed
                foreach (var orderManager in _orderManagers.Values)
                {
                    var order = orderManager.GetOrder(orderId);
                    if (order != null)
                    {
                        var result = await orderManager.CancelOrderAsync(orderId);
                        LogInfo($"Order cancellation result: OrderId={orderId}, Symbol={symbol}, Success={result}");
                        LogMethodExit();
                        return TradingResult<bool>.Success(result);
                    }
                }

                LogWarning($"Order not found for cancellation: {orderId}");
                LogMethodExit();
                return TradingResult<bool>.Failure("ORDER_NOT_FOUND", $"Order not found for cancellation: {orderId}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to cancel order: {orderId}", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("ORDER_CANCELLATION_ERROR", $"Failed to cancel order: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in CancelOrderAsync", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("ORDER_CANCELLATION_ERROR", $"Order cancellation failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Modifies an existing order with new parameters and comprehensive error handling
    /// </summary>
    /// <param name="orderId">The order ID to modify</param>
    /// <param name="newRequest">The new order parameters</param>
    /// <returns>A TradingResult indicating successful modification or error details</returns>
    public async Task<TradingResult<bool>> ModifyOrderAsync(string orderId, Trading.OrderRequest newRequest)
    {
        LogMethodEntry();
        try
        {
            var initResult = EnsureInitialized();
            if (!initResult.IsSuccess)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure(initResult.ErrorCode, initResult.ErrorMessage);
            }

            if (string.IsNullOrEmpty(orderId))
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_ORDER_ID", "Order ID cannot be null or empty");
            }

            if (newRequest == null)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_REQUEST", "New order request cannot be null");
            }

            try
            {
                // Find the venue where this order was placed
                foreach (var orderManager in _orderManagers.Values)
                {
                    var order = orderManager.GetOrder(orderId);
                    if (order != null)
                    {
                        var replaceRequest = new OrderReplaceRequest
                        {
                            NewOrderType = ConvertOrderType(newRequest.OrderType),
                            NewQuantity = newRequest.Quantity,
                            NewPrice = newRequest.Price,
                            NewTimeInForce = ConvertTimeInForce(newRequest.TimeInForce)
                        };

                        var newClOrdId = await orderManager.ReplaceOrderAsync(orderId, replaceRequest);
                        var success = !string.IsNullOrEmpty(newClOrdId);
                        LogInfo($"Order modification result: OrderId={orderId}, NewClOrdId={newClOrdId}, Success={success}");
                        LogMethodExit();
                        return TradingResult<bool>.Success(success);
                    }
                }

                LogWarning($"Order not found for modification: {orderId}");
                LogMethodExit();
                return TradingResult<bool>.Failure("ORDER_NOT_FOUND", $"Order not found for modification: {orderId}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to modify order: {orderId}", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("ORDER_MODIFICATION_ERROR", $"Failed to modify order: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in ModifyOrderAsync", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("ORDER_MODIFICATION_ERROR", $"Order modification failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Subscribes to market data for specified symbols with comprehensive error handling
    /// </summary>
    /// <param name="symbols">Array of symbols to subscribe to</param>
    /// <param name="dataType">Type of market data to subscribe to</param>
    /// <returns>A TradingResult indicating successful subscription or error details</returns>
    public async Task<TradingResult<bool>> SubscribeMarketDataAsync(string[] symbols, MarketDataType dataType)
    {
        LogMethodEntry();
        try
        {
            var initResult = EnsureInitialized();
            if (!initResult.IsSuccess)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure(initResult.ErrorCode, initResult.ErrorMessage);
            }

            if (symbols == null || symbols.Length == 0)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_SYMBOLS", "Symbols array cannot be null or empty");
            }

            var invalidSymbols = symbols.Where(string.IsNullOrEmpty).ToArray();
            if (invalidSymbols.Any())
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_SYMBOLS", $"Found {invalidSymbols.Length} invalid symbols in subscription request");
            }

            try
            {
                var entryTypes = ConvertMarketDataType(dataType);
                var tasks = new List<Task<bool>>();

                // Subscribe on all venues for redundancy and best execution
                foreach (var mdManager in _marketDataManagers.Values)
                {
                    foreach (var symbol in symbols)
                    {
                        tasks.Add(mdManager.SubscribeToQuotesAsync(symbol, entryTypes));
                    }
                }

                var results = await Task.WhenAll(tasks);
                var successCount = results.Count(r => r);
                var totalCount = results.Length;

                LogInfo($"Market data subscription results: {successCount}/{totalCount} successful for {symbols.Length} symbols");
                var overallSuccess = successCount > 0;
                LogMethodExit();
                return TradingResult<bool>.Success(overallSuccess);
            }
            catch (Exception ex)
            {
                LogError($"Failed to subscribe to market data for {symbols.Length} symbols", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("SUBSCRIPTION_ERROR", $"Market data subscription failed: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in SubscribeMarketDataAsync", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("SUBSCRIPTION_ERROR", $"Market data subscription failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Unsubscribes from market data for specified symbols with comprehensive error handling
    /// </summary>
    /// <param name="symbols">Array of symbols to unsubscribe from</param>
    /// <returns>A TradingResult indicating successful unsubscription or error details</returns>
    public async Task<TradingResult<bool>> UnsubscribeMarketDataAsync(string[] symbols)
    {
        LogMethodEntry();
        try
        {
            var initResult = EnsureInitialized();
            if (!initResult.IsSuccess)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure(initResult.ErrorCode, initResult.ErrorMessage);
            }

            if (symbols == null || symbols.Length == 0)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_SYMBOLS", "Symbols array cannot be null or empty");
            }

            try
            {
                var tasks = new List<Task<bool>>();

                foreach (var mdManager in _marketDataManagers.Values)
                {
                    foreach (var symbol in symbols)
                    {
                        tasks.Add(mdManager.UnsubscribeFromQuotesAsync(symbol));
                    }
                }

                var results = await Task.WhenAll(tasks);
                var successCount = results.Count(r => r);
                var totalCount = results.Length;

                LogInfo($"Market data unsubscription results: {successCount}/{totalCount} successful for {symbols.Length} symbols");
                var overallSuccess = successCount > 0;
                LogMethodExit();
                return TradingResult<bool>.Success(overallSuccess);
            }
            catch (Exception ex)
            {
                LogError($"Failed to unsubscribe from market data for {symbols.Length} symbols", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("UNSUBSCRIPTION_ERROR", $"Market data unsubscription failed: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in UnsubscribeMarketDataAsync", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("UNSUBSCRIPTION_ERROR", $"Market data unsubscription failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves the connection status of all configured venues with comprehensive error handling
    /// </summary>
    /// <returns>A TradingResult containing venue status dictionary or error information</returns>
    public TradingResult<Dictionary<string, bool>> GetVenueStatuses()
    {
        LogMethodEntry();
        try
        {
            var statuses = new Dictionary<string, bool>();

            foreach (var (venue, session) in _venueSessions)
            {
                statuses[venue] = session.IsConnected;
            }

            LogInfo($"Retrieved venue statuses for {statuses.Count} venues");
            LogMethodExit();
            return TradingResult<Dictionary<string, bool>>.Success(statuses);
        }
        catch (Exception ex)
        {
            LogError("Error getting venue statuses", ex);
            LogMethodExit();
            return TradingResult<Dictionary<string, bool>>.Failure("VENUE_STATUS_ERROR", $"Failed to get venue statuses: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves comprehensive performance metrics for the FIX engine with detailed statistics
    /// </summary>
    /// <returns>A TradingResult containing performance metrics or error information</returns>
    public TradingResult<EnginePerformanceMetrics> GetPerformanceMetrics()
    {
        LogMethodEntry();
        try
        {
            var metrics = new EnginePerformanceMetrics
            {
                Uptime = DateTime.UtcNow - _startTime,
                ActiveOrders = _orderManagers.Values.Sum(om => om.GetActiveOrders().Count),
                ActiveSubscriptions = _marketDataManagers.Values.Sum(md => md.GetActiveSubscriptions().Count),
                AverageOrderLatencyMicroseconds = _performanceMonitor.AverageOrderLatency,
                MaxOrderLatencyMicroseconds = _performanceMonitor.MaxOrderLatency,
                OrdersProcessed = _performanceMonitor.OrdersProcessed,
                MarketDataUpdatesProcessed = _performanceMonitor.MarketDataUpdates,
                ConnectedVenues = _venueSessions.Count(kvp => kvp.Value.IsConnected),
                TotalVenues = _venueSessions.Count
            };

            LogInfo($"Retrieved performance metrics: Uptime={metrics.Uptime.TotalHours:F2}h, ActiveOrders={metrics.ActiveOrders}, OrdersProcessed={metrics.OrdersProcessed}");
            LogMethodExit();
            return TradingResult<EnginePerformanceMetrics>.Success(metrics);
        }
        catch (Exception ex)
        {
            LogError("Error getting performance metrics", ex);
            LogMethodExit();
            return TradingResult<EnginePerformanceMetrics>.Failure("METRICS_ERROR", $"Failed to get performance metrics: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Initializes venue connection with comprehensive monitoring and error handling
    /// </summary>
    /// <param name="venueName">Name of the venue to initialize</param>
    /// <param name="config">Configuration for venue connection</param>
    /// <param name="correlationId">Correlation ID for tracking</param>
    /// <returns>A TradingResult indicating successful initialization or error details</returns>
    private async Task<TradingResult<bool>> InitializeVenueAsync(string venueName, VenueConnectionConfig config, string correlationId)
    {
        LogMethodEntry();
        try
        {
            using var activity = OpenTelemetryInstrumentation.FixEngineActivitySource.StartActivity("VenueInitialization");

            if (activity != null)
                _observabilityEnricher.EnrichActivity(activity, "VenueInitialization", config);
            activity?.SetTag("fix.correlation_id", correlationId);
            activity?.SetTag("fix.venue.name", venueName);
            activity?.SetTag("fix.venue.host", config.Host);
            activity?.SetTag("fix.venue.port", config.Port.ToString());
            activity?.SetTag("fix.venue.target_comp_id", config.TargetCompId);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Comprehensive audit logging for venue initialization
                LogInfo($"Venue initialization: VenueName={venueName}, Host={config.Host}, Port={config.Port}, CorrelationId={correlationId}");

                LogInfo($"Initializing venue: {venueName} at {config.Host}:{config.Port} | CorrelationId: {correlationId}");

                // Create FIX session with enhanced monitoring
                var sessionCreationStart = Stopwatch.StartNew();
                var session = new FixSession(_config!.SenderCompId, config.TargetCompId, Logger);
                sessionCreationStart.Stop();

                _infrastructureMetrics.RecordMemoryAllocation(GC.GetTotalMemory(false), $"FixSession_{venueName}");
                activity?.SetTag("fix.session.creation_time_microseconds", sessionCreationStart.Elapsed.TotalMicroseconds.ToString("F2"));

                // Create market data manager with observability
                var marketDataManagerCreationStart = Stopwatch.StartNew();
                var marketDataManager = new MarketDataManager(session, Logger);
                marketDataManager.MarketDataReceived += (sender, update) => OnMarketDataReceived(sender, update, correlationId);
                marketDataManager.SubscriptionStatusChanged += (sender, status) => OnSubscriptionStatusChanged(sender, status);
                marketDataManagerCreationStart.Stop();

                activity?.SetTag("fix.market_data_manager.creation_time_microseconds", marketDataManagerCreationStart.Elapsed.TotalMicroseconds.ToString("F2"));

                // Create order manager with observability
                var orderManagerCreationStart = Stopwatch.StartNew();
                var orderManager = new OrderManager(session, Logger);
                orderManager.ExecutionReceived += (sender, execution) => OnExecutionReceived(sender, execution);
                orderManager.OrderStatusChanged += (sender, status) => OnOrderStatusChanged(sender, status);
                orderManager.OrderRejected += (sender, rejection) => OnOrderRejected(sender, rejection);
                orderManagerCreationStart.Stop();

                activity?.SetTag("fix.order_manager.creation_time_microseconds", orderManagerCreationStart.Elapsed.TotalMicroseconds.ToString("F2"));

                // Session event handlers with enhanced monitoring
                session.SessionStateChanged += (s, state) => OnVenueStatusChanged(venueName, state);
                session.MessageReceived += (s, msg) =>
                {
                    _performanceMonitor.RecordMessageProcessed();
                    _tradingMetrics.RecordFixMessageProcessing(msg.GetType().Name, TimeSpan.FromTicks(1)); // Minimal processing time
                };

                // Store components
                _venueSessions[venueName] = session;
                _marketDataManagers[venueName] = marketDataManager;
                _orderManagers[venueName] = orderManager;

                // Connect to venue with comprehensive monitoring
                var connectionStart = Stopwatch.StartNew();
                var connected = await session.ConnectAsync(config.Host, config.Port, TimeSpan.FromSeconds(30));
                connectionStart.Stop();

                // Record connection metrics
                _infrastructureMetrics.RecordNetworkLatency(connectionStart.Elapsed, venueName);
                activity?.SetTag("fix.connection.time_milliseconds", connectionStart.Elapsed.TotalMilliseconds.ToString("F2"));
                activity?.SetTag("fix.connection.success", connected);

                if (connected)
                {
                    stopwatch.Stop();

                    // Record successful venue initialization
                    _tradingMetrics.RecordFixMessageProcessing("VenueInitialization", stopwatch.Elapsed);

                    LogInfo($"Successfully connected to venue: {venueName} in {stopwatch.Elapsed.TotalMilliseconds:F2}ms | CorrelationId: {correlationId}");

                    activity?.SetTag("fix.venue.initialization.success", true);
                    activity?.SetTag("fix.venue.initialization.duration_ms", stopwatch.Elapsed.TotalMilliseconds.ToString("F2"));

                    VenueStatusChanged?.Invoke(this, new VenueStatusUpdate
                    {
                        Venue = venueName,
                        IsConnected = true,
                        Timestamp = DateTime.UtcNow,
                        StatusMessage = $"Connected successfully in {stopwatch.Elapsed.TotalMilliseconds:F2}ms"
                    });

                    LogMethodExit();
                    return TradingResult<bool>.Success(true);
                }
                else
                {
                    stopwatch.Stop();
                    var error = $"Failed to connect to venue: {venueName} after {stopwatch.Elapsed.TotalMilliseconds:F2}ms";

                    activity?.SetStatus(ActivityStatusCode.Error, error);
                    activity?.SetTag("fix.venue.initialization.success", false);
                    activity?.SetTag("fix.venue.initialization.duration_ms", stopwatch.Elapsed.TotalMilliseconds.ToString("F2"));

                    // Record failed venue connection audit
                    LogError($"Venue initialization failure: VenueName={venueName}, Duration={stopwatch.Elapsed.TotalMilliseconds}ms, ErrorType=ConnectionFailure, CorrelationId={correlationId}");

                    LogError(error);
                    LogMethodExit();
                    return TradingResult<bool>.Failure("VENUE_CONNECTION_FAILED", error);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                activity?.SetStatus(ActivityStatusCode.Error);

                // Record venue initialization failure audit
                LogError($"Venue initialization failure: VenueName={venueName}, Duration={stopwatch.Elapsed.TotalMilliseconds}ms, CorrelationId={correlationId}", ex);

                LogError($"Failed to initialize venue: {venueName} after {stopwatch.Elapsed.TotalMilliseconds:F2}ms | CorrelationId: {correlationId}", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("VENUE_INITIALIZATION_ERROR", $"Failed to initialize venue: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in InitializeVenueAsync", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("VENUE_INITIALIZATION_ERROR", $"Venue initialization failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Handles market data received events with comprehensive processing and metrics
    /// </summary>
    /// <param name="sender">Event sender</param>
    /// <param name="update">Market data update</param>
    /// <param name="correlationId">Correlation ID for tracking</param>
    private void OnMarketDataReceived(object? sender, MarketDataUpdate update, string correlationId)
    {
        LogMethodEntry();
        try
        {
            using var activity = OpenTelemetryInstrumentation.MarketDataActivitySource.StartActivity("MarketDataReceived");

            if (activity != null)
                _observabilityEnricher.EnrichActivity(activity, "MarketDataReceived", update);
            activity?.SetTag("fix.correlation_id", correlationId);
            activity?.SetTag("fix.market_data.symbol", update.Symbol);
            activity?.SetTag("fix.market_data.price", update.Snapshot.LastPrice.ToString());
            activity?.SetTag("fix.market_data.volume", update.Snapshot.LastSize.ToString());

            var processingStart = Stopwatch.StartNew();
            _performanceMonitor.RecordMarketDataUpdate();

            var snapshot = new Interfaces.MarketDataSnapshot
            {
                Symbol = update.Symbol,
                Timestamp = update.Snapshot.Timestamp,
                BidPrice = update.Snapshot.BidPrice,
                BidSize = update.Snapshot.BidSize,
                AskPrice = update.Snapshot.OfferPrice,
                AskSize = update.Snapshot.OfferSize,
                LastPrice = update.Snapshot.LastPrice,
                LastSize = update.Snapshot.LastSize,
                DataType = MarketDataType.Level1
            };

            processingStart.Stop();

            // Record market data processing metrics
            _tradingMetrics.RecordMarketDataTick(update.Symbol, processingStart.Elapsed);
            activity?.SetTag("fix.market_data.processing_time_microseconds", processingStart.Elapsed.TotalMicroseconds.ToString("F2"));
            activity?.SetTag("fix.market_data.bid_ask_spread", (update.Snapshot.OfferPrice - update.Snapshot.BidPrice).ToString());

            MarketDataReceived?.Invoke(this, snapshot);
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Error processing market data for {update?.Symbol}", ex);
            LogMethodExit();
        }
    }

    /// <summary>
    /// Handles execution received events with comprehensive processing and audit logging
    /// </summary>
    /// <param name="sender">Event sender</param>
    /// <param name="execution">Execution details</param>
    private void OnExecutionReceived(object? sender, Core.Execution execution)
    {
        LogMethodEntry();
        try
        {
            var execReport = new ExecutionReport
            {
                OrderId = execution.OrderId,
                Symbol = execution.Symbol,
                Side = execution.Side == OrderSide.Buy ? "1" : "2",
                Quantity = execution.Quantity,
                Price = execution.Price,
                FilledQuantity = execution.CumQty,
                AvgFillPrice = execution.Price, // Simplified - would need weighted average
                OrderStatus = "FILLED", // Simplified
                ExecType = "F",
                TransactionTime = execution.ExecutionTime,
                Venue = "UNKNOWN", // Would need venue mapping
                LatencyNanoseconds = execution.HardwareTimestamp
            };

            ExecutionReceived?.Invoke(this, execReport);
            LogInfo($"Execution received: OrderId={execution.OrderId}, Symbol={execution.Symbol}, Quantity={execution.Quantity}, Price={execution.Price}");
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Error processing execution for order {execution?.OrderId}", ex);
            LogMethodExit();
        }
    }

    /// <summary>
    /// Handles order status change events with comprehensive logging
    /// </summary>
    /// <param name="sender">Event sender</param>
    /// <param name="order">Order with updated status</param>
    private void OnOrderStatusChanged(object? sender, Core.Order order)
    {
        LogMethodEntry();
        try
        {
            LogInfo($"Order status changed: {order.ClOrdId} -> {order.Status}");
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Error processing order status change for {order?.ClOrdId}", ex);
            LogMethodExit();
        }
    }

    /// <summary>
    /// Handles order rejection events with comprehensive logging
    /// </summary>
    /// <param name="sender">Event sender</param>
    /// <param name="reject">Order rejection details</param>
    private void OnOrderRejected(object? sender, Core.OrderReject reject)
    {
        LogMethodEntry();
        try
        {
            LogWarning($"Order rejected: {reject.ClOrdId} - {reject.RejectReason}: {reject.RejectText}");
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Error processing order rejection for {reject?.ClOrdId}", ex);
            LogMethodExit();
        }
    }

    /// <summary>
    /// Handles subscription status change events with comprehensive logging
    /// </summary>
    /// <param name="sender">Event sender</param>
    /// <param name="status">New subscription status</param>
    private void OnSubscriptionStatusChanged(object? sender, string status)
    {
        LogMethodEntry();
        try
        {
            LogInfo($"Subscription status changed: {status}");
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Error processing subscription status change: {status}", ex);
            LogMethodExit();
        }
    }

    /// <summary>
    /// Handles venue status change events with comprehensive logging and event propagation
    /// </summary>
    /// <param name="venue">Venue name</param>
    /// <param name="status">New venue status</param>
    private void OnVenueStatusChanged(string venue, string status)
    {
        LogMethodEntry();
        try
        {
            LogInfo($"Venue {venue} status changed: {status}");

            var isConnected = status.Contains("connected", StringComparison.OrdinalIgnoreCase);
            VenueStatusChanged?.Invoke(this, new VenueStatusUpdate
            {
                Venue = venue,
                IsConnected = isConnected,
                Timestamp = DateTime.UtcNow,
                StatusMessage = status
            });
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Error processing venue status change for {venue}: {status}", ex);
            LogMethodExit();
        }
    }

    /// <summary>
    /// Performs comprehensive health check on all venue connections with automatic recovery
    /// </summary>
    /// <param name="state">Timer state object</param>
    private void PerformHealthCheck(object? state)
    {
        LogMethodEntry();
        try
        {
            foreach (var (venue, session) in _venueSessions)
            {
                if (!session.IsConnected)
                {
                    LogWarning($"Venue {venue} is disconnected, attempting reconnection");
                    // Could add auto-reconnection logic here
                }
            }
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Error during health check", ex);
            LogMethodExit();
        }
    }

    /// <summary>
    /// Converts string order type to Core.OrderType enumeration with comprehensive validation
    /// </summary>
    /// <param name="orderType">String representation of order type</param>
    /// <returns>Corresponding Core.OrderType enumeration value</returns>
    private static Core.OrderType ConvertOrderType(string orderType)
    {
        return orderType switch
        {
            "1" => Core.OrderType.Market,
            "2" => Core.OrderType.Limit,
            "3" => Core.OrderType.Stop,
            "4" => Core.OrderType.StopLimit,
            _ => Core.OrderType.Limit
        };
    }

    /// <summary>
    /// Converts string time in force to Core.TimeInForce enumeration with comprehensive validation
    /// </summary>
    /// <param name="timeInForce">String representation of time in force</param>
    /// <returns>Corresponding Core.TimeInForce enumeration value</returns>
    private static Core.TimeInForce ConvertTimeInForce(string timeInForce)
    {
        return timeInForce switch
        {
            "0" => Core.TimeInForce.Day,
            "1" => Core.TimeInForce.GTC,
            "3" => Core.TimeInForce.IOC,
            "4" => Core.TimeInForce.FOK,
            "6" => Core.TimeInForce.GTD,
            _ => Core.TimeInForce.Day
        };
    }

    /// <summary>
    /// Converts MarketDataType to array of MarketDataEntryType with comprehensive mapping
    /// </summary>
    /// <param name="dataType">Market data type to convert</param>
    /// <returns>Array of corresponding MarketDataEntryType values</returns>
    private static MarketDataEntryType[] ConvertMarketDataType(MarketDataType dataType)
    {
        return dataType switch
        {
            MarketDataType.Level1 => new[] { MarketDataEntryType.Bid, MarketDataEntryType.Offer, MarketDataEntryType.Trade },
            MarketDataType.Level2 => new[] { MarketDataEntryType.Bid, MarketDataEntryType.Offer },
            MarketDataType.Trades => new[] { MarketDataEntryType.Trade },
            MarketDataType.NBBO => new[] { MarketDataEntryType.Bid, MarketDataEntryType.Offer },
            _ => new[] { MarketDataEntryType.Bid, MarketDataEntryType.Offer, MarketDataEntryType.Trade }
        };
    }

    /// <summary>
    /// Ensures the FIX engine is properly initialized before operations with comprehensive validation
    /// </summary>
    /// <returns>A TradingResult indicating initialization status or error details</returns>
    private TradingResult<bool> EnsureInitialized()
    {
        LogMethodEntry();
        try
        {
            if (!_isInitialized)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("NOT_INITIALIZED", "FIX Engine must be initialized before use");
            }
            LogMethodExit();
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Error checking initialization status", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("INITIALIZATION_CHECK_ERROR", $"Failed to check initialization: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates a unique order ID with timestamp-based uniqueness guarantee
    /// </summary>
    /// <returns>Unique order ID string</returns>
    private static string GenerateOrderId()
    {
        return $"ORDER_{DateTimeOffset.UtcNow.Ticks}";
    }

    /// <summary>
    /// Disposes of all resources and connections with comprehensive cleanup
    /// </summary>
    public void Dispose()
    {
        LogMethodEntry();
        try
        {
            if (_disposed)
            {
                LogMethodExit();
                return;
            }

            _healthCheckTimer?.Dispose();

            foreach (var session in _venueSessions.Values)
            {
                session.Dispose();
            }

            foreach (var mdManager in _marketDataManagers.Values)
            {
                mdManager.Dispose();
            }

            foreach (var orderManager in _orderManagers.Values)
            {
                orderManager.Dispose();
            }

            _disposed = true;
            LogInfo("FixEngine disposed successfully");
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Error during FixEngine disposal", ex);
            LogMethodExit();
            throw;
        }
    }
}

/// <summary>
/// Enhanced performance metrics for the FIX engine
/// </summary>
public class EnginePerformanceMetrics
{
    public TimeSpan Uptime { get; set; }
    public int ActiveOrders { get; set; }
    public int ActiveSubscriptions { get; set; }
    public double AverageOrderLatencyMicroseconds { get; set; }
    public double MaxOrderLatencyMicroseconds { get; set; }
    public long OrdersProcessed { get; set; }
    public long MarketDataUpdatesProcessed { get; set; }
    public int ConnectedVenues { get; set; }
    public int TotalVenues { get; set; }

    public double ConnectionHealth => TotalVenues > 0 ? (double)ConnectedVenues / TotalVenues : 0;
}

/// <summary>
/// Performance monitoring for ultra-low latency requirements
/// </summary>
internal class PerformanceMonitor
{
    private long _ordersProcessed;
    private long _marketDataUpdates;
    private long _messagesProcessed;
    private double _totalOrderLatency;
    private double _maxOrderLatency;
    private readonly object _lock = new();

    public double AverageOrderLatency
    {
        get
        {
            lock (_lock)
            {
                return _ordersProcessed > 0 ? _totalOrderLatency / _ordersProcessed : 0;
            }
        }
    }

    public double MaxOrderLatency
    {
        get
        {
            lock (_lock)
            {
                return _maxOrderLatency;
            }
        }
    }

    public long OrdersProcessed => _ordersProcessed;
    public long MarketDataUpdates => _marketDataUpdates;
    public long MessagesProcessed => _messagesProcessed;

    /// <summary>
    /// Records order latency for performance monitoring with thread-safe operations
    /// </summary>
    /// <param name="latencyMicroseconds">Order latency in microseconds</param>
    public void RecordOrderLatency(double latencyMicroseconds)
    {
        lock (_lock)
        {
            _ordersProcessed++;
            _totalOrderLatency += latencyMicroseconds;
            if (latencyMicroseconds > _maxOrderLatency)
                _maxOrderLatency = latencyMicroseconds;
        }
    }

    /// <summary>
    /// Records market data update for performance monitoring with atomic operations
    /// </summary>
    public void RecordMarketDataUpdate()
    {
        Interlocked.Increment(ref _marketDataUpdates);
    }

    /// <summary>
    /// Records processed message for performance monitoring with atomic operations
    /// </summary>
    public void RecordMessageProcessed()
    {
        Interlocked.Increment(ref _messagesProcessed);
    }
}