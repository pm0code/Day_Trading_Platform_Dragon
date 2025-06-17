using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TradingPlatform.FixEngine.Interfaces;
using TradingPlatform.FixEngine.Models;
using TradingPlatform.FixEngine.Trading;
using TradingPlatform.Core.Interfaces;

namespace TradingPlatform.FixEngine.Core;

/// <summary>
/// Advanced FIX engine implementation with ultra-low latency optimizations
/// Combines session management, market data, and order management for US equity markets
/// </summary>
public sealed class FixEngine : IFixEngine
{
    private readonly ILogger _logger;
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
    
    public FixEngine(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _orderRouter = new OrderRouter(_logger);
        _performanceMonitor = new PerformanceMonitor();
        
        // Health check every 30 seconds
        _healthCheckTimer = new Timer(PerformHealthCheck, null, 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }
    
    public async Task<bool> InitializeAsync(FixEngineConfig config)
    {
        if (_isInitialized)
            throw new InvalidOperationException("FIX Engine is already initialized");
        
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        try
        {
            _logger.LogInformation("Initializing FIX Engine with {VenueCount} venues", 
                config.VenueConfigs.Count);
            
            // Initialize sessions for each venue
            foreach (var (venueName, venueConfig) in config.VenueConfigs)
            {
                await InitializeVenueAsync(venueName, venueConfig);
            }
            
            _isInitialized = true;
            _logger.LogInformation("FIX Engine initialization completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize FIX Engine");
            return false;
        }
    }
    
    public async Task<string> SubmitOrderAsync(Trading.OrderRequest request)
    {
        EnsureInitialized();
        
        var stopwatch = Stopwatch.StartNew();
        var orderId = GenerateOrderId();
        
        try
        {
            // Smart routing to optimal venue
            var optimalVenue = _orderRouter.SelectOptimalVenue(
                request.Symbol, 
                (int)request.Quantity, 
                request.Price ?? 0m);
            
            if (!_orderManagers.TryGetValue(optimalVenue, out var orderManager))
            {
                throw new InvalidOperationException($"Order manager not available for venue: {optimalVenue}");
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
            
            var clOrdId = await orderManager.SubmitOrderAsync(fixOrderRequest);
            
            stopwatch.Stop();
            _performanceMonitor.RecordOrderLatency(stopwatch.Elapsed.TotalMicroseconds);
            
            _logger.LogInformation("Order submitted: {OrderId} -> {ClOrdId} via {Venue} in {Latency}μs", 
                orderId, clOrdId, optimalVenue, stopwatch.Elapsed.TotalMicroseconds);
            
            return clOrdId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit order: {OrderId}", orderId);
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
            
            _logger.LogWarning("Order not found for cancellation: {OrderId}", orderId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel order: {OrderId}", orderId);
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
            
            _logger.LogWarning("Order not found for modification: {OrderId}", orderId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to modify order: {OrderId}", orderId);
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
            
            _logger.LogInformation("Market data subscriptions: {Success}/{Total} for {Symbols}", 
                successCount, results.Length, string.Join(",", symbols));
            
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to market data for symbols: {Symbols}", 
                string.Join(",", symbols));
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
            
            _logger.LogInformation("Market data unsubscriptions: {Success}/{Total} for {Symbols}", 
                successCount, results.Length, string.Join(",", symbols));
            
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe from market data for symbols: {Symbols}", 
                string.Join(",", symbols));
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
    
    private async Task InitializeVenueAsync(string venueName, VenueConnectionConfig config)
    {
        try
        {
            _logger.LogInformation("Initializing venue: {Venue} at {Host}:{Port}", 
                venueName, config.Host, config.Port);
            
            // Create FIX session
            var session = new FixSession(_config!.SenderCompId, config.TargetCompId, _logger);
            
            // Create market data manager
            var marketDataManager = new MarketDataManager(session, _logger);
            marketDataManager.MarketDataReceived += OnMarketDataReceived;
            marketDataManager.SubscriptionStatusChanged += OnSubscriptionStatusChanged;
            
            // Create order manager
            var orderManager = new OrderManager(session, _logger);
            orderManager.ExecutionReceived += OnExecutionReceived;
            orderManager.OrderStatusChanged += OnOrderStatusChanged;
            orderManager.OrderRejected += OnOrderRejected;
            
            // Session event handlers
            session.SessionStateChanged += (s, state) => OnVenueStatusChanged(venueName, state);
            session.MessageReceived += (s, msg) => _performanceMonitor.RecordMessageProcessed();
            
            // Store components
            _venueSessions[venueName] = session;
            _marketDataManagers[venueName] = marketDataManager;
            _orderManagers[venueName] = orderManager;
            
            // Connect to venue
            var connected = await session.ConnectAsync(config.Host, config.Port);
            if (connected)
            {
                _logger.LogInformation("Successfully connected to venue: {Venue}", venueName);
                VenueStatusChanged?.Invoke(this, new VenueStatusUpdate
                {
                    Venue = venueName,
                    IsConnected = true,
                    Timestamp = DateTime.UtcNow,
                    StatusMessage = "Connected successfully"
                });
            }
            else
            {
                _logger.LogError("Failed to connect to venue: {Venue}", venueName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize venue: {Venue}", venueName);
            throw;
        }
    }
    
    private void OnMarketDataReceived(object? sender, MarketDataUpdate update)
    {
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
        _logger.LogDebug("Order status changed: {ClOrdId} -> {Status}", order.ClOrdId, order.Status);
    }
    
    private void OnOrderRejected(object? sender, Core.OrderReject reject)
    {
        _logger.LogWarning("Order rejected: {ClOrdId} - {Reason}: {Text}", 
            reject.ClOrdId, reject.RejectReason, reject.RejectText);
    }
    
    private void OnSubscriptionStatusChanged(object? sender, string status)
    {
        _logger.LogDebug("Subscription status changed: {Status}", status);
    }
    
    private void OnVenueStatusChanged(string venue, string status)
    {
        _logger.LogInformation("Venue {Venue} status changed: {Status}", venue, status);
        
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
                    _logger.LogWarning("Venue {Venue} is disconnected, attempting reconnection", venue);
                    // Could add auto-reconnection logic here
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check");
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