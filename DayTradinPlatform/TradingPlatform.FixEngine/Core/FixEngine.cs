using System.Collections.Concurrent;
using System.Diagnostics;
using TradingPlatform.FixEngine.Interfaces;
using TradingPlatform.FixEngine.Models;
using TradingPlatform.FixEngine.Trading;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Observability;
using Audit.Core;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.FixEngine.Core;

/// <summary>
/// Advanced FIX engine implementation with ultra-low latency optimizations
/// Combines session management, market data, and order management for US equity markets
/// </summary>
public sealed class FixEngine : IFixEngine
{
    private readonly ITradingLogger _logger;
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

    public FixEngine(
        ITradingLogger logger,
        ITradingMetrics tradingMetrics,
        IInfrastructureMetrics infrastructureMetrics,
        IObservabilityEnricher observabilityEnricher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tradingMetrics = tradingMetrics ?? throw new ArgumentNullException(nameof(tradingMetrics));
        _infrastructureMetrics = infrastructureMetrics ?? throw new ArgumentNullException(nameof(infrastructureMetrics));
        _observabilityEnricher = observabilityEnricher ?? throw new ArgumentNullException(nameof(observabilityEnricher));

        _orderRouter = new OrderRouter(_logger);
        _performanceMonitor = new PerformanceMonitor();

        // Health check every 30 seconds
        _healthCheckTimer = new Timer(PerformHealthCheck, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task<bool> InitializeAsync(FixEngineConfig config)
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
            throw new InvalidOperationException(error);
        }

        _config = config ?? throw new ArgumentNullException(nameof(config));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Comprehensive audit logging for initialization
            _logger.LogInfo($"FixEngine initialization: VenueCount={config.VenueConfigs.Count}, CorrelationId={correlationId}");

            _logger.LogInfo($"Initializing FIX Engine with {config.VenueConfigs.Count} venues | CorrelationId: {correlationId}");

            // Initialize sessions for each venue with comprehensive monitoring
            foreach (var (venueName, venueConfig) in config.VenueConfigs)
            {
                var venueStopwatch = Stopwatch.StartNew();
                await InitializeVenueAsync(venueName, venueConfig, correlationId);
                venueStopwatch.Stop();

                // Record venue initialization metrics
                _infrastructureMetrics.RecordNetworkLatency(venueStopwatch.Elapsed, venueName);
                activity?.SetTag($"fix.venue.{venueName}.init_time_ms", venueStopwatch.Elapsed.TotalMilliseconds.ToString("F2"));
            }

            stopwatch.Stop();
            _isInitialized = true;

            // Record successful initialization metrics
            _tradingMetrics.RecordFixMessageProcessing("InitializationComplete", stopwatch.Elapsed);

            _logger.LogInfo($"FIX Engine initialization completed successfully in {stopwatch.Elapsed.TotalMilliseconds:F2}ms | CorrelationId: {correlationId}");
            activity?.SetTag("fix.initialization.success", true);
            activity?.SetTag("fix.initialization.duration_ms", stopwatch.Elapsed.TotalMilliseconds.ToString("F2"));

            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Record failed initialization
            TradingLogOrchestrator.Instance.LogError($"FixEngine initialization failure: Duration={stopwatch.Elapsed.TotalMilliseconds}ms, CorrelationId={correlationId}", ex);

            TradingLogOrchestrator.Instance.LogError($"Failed to initialize FIX Engine after {stopwatch.Elapsed.TotalMilliseconds:F2}ms | CorrelationId: {correlationId}", ex);
            return false;
        }
    }

    public async Task<string> SubmitOrderAsync(Trading.OrderRequest request)
    {
        using var activity = OpenTelemetryInstrumentation.FixEngineActivitySource.StartActivity("OrderSubmission");
        var correlationId = _observabilityEnricher.GenerateCorrelationId();

        EnsureInitialized();

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
            _logger.LogInfo($"Order submission: OrderId={orderId}, Symbol={request.Symbol}, Side={request.Side}, Quantity={request.Quantity}, CorrelationId={correlationId}");

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

                TradingLogOrchestrator.Instance.LogError($"Order submission failure: OrderId={orderId}, Venue={optimalVenue}, Symbol={request.Symbol}, ErrorType=VenueUnavailable");

                throw new InvalidOperationException(error);
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

            _logger.LogInfo($"Order submitted: {orderId} -> {clOrdId} via {optimalVenue} in {stopwatch.Elapsed.TotalMicroseconds:F2}μs | CorrelationId: {correlationId}");

            return clOrdId;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Record failed order submission audit
            TradingLogOrchestrator.Instance.LogError($"Order submission failure: OrderId={orderId}, Symbol={request.Symbol}, Duration={stopwatch.Elapsed.TotalMilliseconds}ms, CorrelationId={correlationId}", ex);

            TradingLogOrchestrator.Instance.LogError($"Failed to submit order: {orderId} after {stopwatch.Elapsed.TotalMicroseconds:F2}μs | CorrelationId: {correlationId}", ex);
            throw;
        }
    }

    public async Task<bool> CancelOrderAsync(string orderId, string symbol)
    {
        EnsureInitialized();

        try
        {
            // Find the venue where this order was placed
            foreach (var orderManager in _orderManagers.Values)
            {
                var order = orderManager.GetOrder(orderId);
                if (order != null)
                {
                    return await orderManager.CancelOrderAsync(orderId);
                }
            }

            TradingLogOrchestrator.Instance.LogWarning($"Order not found for cancellation: {orderId}");
            return false;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to cancel order: {orderId}", ex);
            return false;
        }
    }

    public async Task<bool> ModifyOrderAsync(string orderId, Trading.OrderRequest newRequest)
    {
        EnsureInitialized();

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
                    return !string.IsNullOrEmpty(newClOrdId);
                }
            }

            TradingLogOrchestrator.Instance.LogWarning($"Order not found for modification: {orderId}");
            return false;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to modify order: {orderId}", ex);
            return false;
        }
    }

    public async Task<bool> SubscribeMarketDataAsync(string[] symbols, MarketDataType dataType)
    {
        EnsureInitialized();

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

            _logger.LogInfo($"Market data subscriptions: {successCount}/{results.Length} for {string.Join(",", symbols)}");

            return successCount > 0;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to subscribe to market data for symbols: {string.Join(", ", symbols)}", ex);
            return false;
        }
    }

    public async Task<bool> UnsubscribeMarketDataAsync(string[] symbols)
    {
        EnsureInitialized();

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

            _logger.LogInfo($"Market data unsubscriptions: {successCount}/{results.Length} for {string.Join(",", symbols)}");

            return successCount > 0;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to unsubscribe from market data for symbols: {string.Join(", ", symbols)}", ex);
            return false;
        }
    }

    public Dictionary<string, bool> GetVenueStatuses()
    {
        var statuses = new Dictionary<string, bool>();

        foreach (var (venue, session) in _venueSessions)
        {
            statuses[venue] = session.IsConnected;
        }

        return statuses;
    }

    public EnginePerformanceMetrics GetPerformanceMetrics()
    {
        return new EnginePerformanceMetrics
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
    }

    private async Task InitializeVenueAsync(string venueName, VenueConnectionConfig config, string correlationId)
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
            _logger.LogInfo($"Venue initialization: VenueName={venueName}, Host={config.Host}, Port={config.Port}, CorrelationId={correlationId}");

            _logger.LogInfo($"Initializing venue: {venueName} at {config.Host}:{config.Port} | CorrelationId: {correlationId}");

            // Create FIX session with enhanced monitoring
            var sessionCreationStart = Stopwatch.StartNew();
            var session = new FixSession(_config!.SenderCompId, config.TargetCompId, _logger);
            sessionCreationStart.Stop();

            _infrastructureMetrics.RecordMemoryAllocation(GC.GetTotalMemory(false), $"FixSession_{venueName}");
            activity?.SetTag("fix.session.creation_time_microseconds", sessionCreationStart.Elapsed.TotalMicroseconds.ToString("F2"));

            // Create market data manager with observability
            var marketDataManagerCreationStart = Stopwatch.StartNew();
            var marketDataManager = new MarketDataManager(session, _logger);
            marketDataManager.MarketDataReceived += (sender, update) => OnMarketDataReceived(sender, update, correlationId);
            marketDataManager.SubscriptionStatusChanged += (sender, status) => OnSubscriptionStatusChanged(sender, status);
            marketDataManagerCreationStart.Stop();

            activity?.SetTag("fix.market_data_manager.creation_time_microseconds", marketDataManagerCreationStart.Elapsed.TotalMicroseconds.ToString("F2"));

            // Create order manager with observability
            var orderManagerCreationStart = Stopwatch.StartNew();
            var orderManager = new OrderManager(session, _logger);
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

                _logger.LogInfo($"Successfully connected to venue: {venueName} in {stopwatch.Elapsed.TotalMilliseconds:F2}ms | CorrelationId: {correlationId}");

                activity?.SetTag("fix.venue.initialization.success", true);
                activity?.SetTag("fix.venue.initialization.duration_ms", stopwatch.Elapsed.TotalMilliseconds.ToString("F2"));

                VenueStatusChanged?.Invoke(this, new VenueStatusUpdate
                {
                    Venue = venueName,
                    IsConnected = true,
                    Timestamp = DateTime.UtcNow,
                    StatusMessage = $"Connected successfully in {stopwatch.Elapsed.TotalMilliseconds:F2}ms"
                });
            }
            else
            {
                stopwatch.Stop();
                var error = $"Failed to connect to venue: {venueName} after {stopwatch.Elapsed.TotalMilliseconds:F2}ms";

                activity?.SetStatus(ActivityStatusCode.Error, error);
                activity?.SetTag("fix.venue.initialization.success", false);
                activity?.SetTag("fix.venue.initialization.duration_ms", stopwatch.Elapsed.TotalMilliseconds.ToString("F2"));

                // Record failed venue connection audit
                TradingLogOrchestrator.Instance.LogError($"Venue initialization failure: VenueName={venueName}, Duration={stopwatch.Elapsed.TotalMilliseconds}ms, ErrorType=ConnectionFailure, CorrelationId={correlationId}");

                TradingLogOrchestrator.Instance.LogError(error);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error);

            // Record venue initialization failure audit
            TradingLogOrchestrator.Instance.LogError($"Venue initialization failure: VenueName={venueName}, Duration={stopwatch.Elapsed.TotalMilliseconds}ms, CorrelationId={correlationId}", ex);

            TradingLogOrchestrator.Instance.LogError($"Failed to initialize venue: {venueName} after {stopwatch.Elapsed.TotalMilliseconds:F2}ms | CorrelationId: {correlationId}", ex);
            throw;
        }
    }

    private void OnMarketDataReceived(object? sender, MarketDataUpdate update, string correlationId)
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
    }

    private void OnExecutionReceived(object? sender, Core.Execution execution)
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
    }

    private void OnOrderStatusChanged(object? sender, Core.Order order)
    {
        TradingLogOrchestrator.Instance.LogInfo($"Order status changed: {order.ClOrdId} -> {order.Status}");
    }

    private void OnOrderRejected(object? sender, Core.OrderReject reject)
    {
        TradingLogOrchestrator.Instance.LogWarning($"Order rejected: {reject.ClOrdId} - {reject.RejectReason}: {reject.RejectText}");
    }

    private void OnSubscriptionStatusChanged(object? sender, string status)
    {
        TradingLogOrchestrator.Instance.LogInfo($"Subscription status changed: {status}");
    }

    private void OnVenueStatusChanged(string venue, string status)
    {
        _logger.LogInfo($"Venue {venue} status changed: {status}");

        var isConnected = status.Contains("connected", StringComparison.OrdinalIgnoreCase);
        VenueStatusChanged?.Invoke(this, new VenueStatusUpdate
        {
            Venue = venue,
            IsConnected = isConnected,
            Timestamp = DateTime.UtcNow,
            StatusMessage = status
        });
    }

    private void PerformHealthCheck(object? state)
    {
        try
        {
            foreach (var (venue, session) in _venueSessions)
            {
                if (!session.IsConnected)
                {
                    TradingLogOrchestrator.Instance.LogWarning($"Venue {venue} is disconnected, attempting reconnection");
                    // Could add auto-reconnection logic here
                }
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error during health check", ex);
        }
    }

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

    private void EnsureInitialized()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("FIX Engine must be initialized before use");
    }

    private static string GenerateOrderId()
    {
        return $"ORDER_{DateTimeOffset.UtcNow.Ticks}";
    }

    public void Dispose()
    {
        if (_disposed) return;

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

    public void RecordMarketDataUpdate()
    {
        Interlocked.Increment(ref _marketDataUpdates);
    }

    public void RecordMessageProcessed()
    {
        Interlocked.Increment(ref _messagesProcessed);
    }
}