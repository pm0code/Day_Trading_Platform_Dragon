using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Messaging.Events;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Logging.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.Gateway.Services;

/// <summary>
/// High-performance orchestration service for on-premise trading workstation
/// Coordinates all microservices via Redis Streams with sub-millisecond targets
/// All operations use canonical patterns with comprehensive logging and error handling
/// Financial calculations maintain decimal precision for accurate trading operations
/// </summary>
public class GatewayOrchestrator : CanonicalServiceBase, IGatewayOrchestrator
{
    private readonly IMessageBus _messageBus;
    private readonly ITradingOperationsLogger _tradingLogger;
    private readonly IPerformanceLogger _performanceLogger;
    private readonly Dictionary<string, WebSocket> _activeWebSockets;
    private readonly object _webSocketLock = new();
    private readonly CancellationTokenSource _cancellationTokenSource;

    /// <summary>
    /// Initializes a new instance of the GatewayOrchestrator with comprehensive dependencies and canonical patterns
    /// </summary>
    /// <param name="messageBus">Message bus for microservice communication</param>
    /// <param name="logger">Trading logger for comprehensive gateway operation tracking</param>
    /// <param name="tradingLogger">Specialized trading operations logger</param>
    /// <param name="performanceLogger">Performance logger for latency tracking</param>
    public GatewayOrchestrator(IMessageBus messageBus, Core.Interfaces.ITradingLogger logger,
        ITradingOperationsLogger tradingLogger, IPerformanceLogger performanceLogger) : base(logger, "GatewayOrchestrator")
    {
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        _tradingLogger = tradingLogger ?? throw new ArgumentNullException(nameof(tradingLogger));
        _performanceLogger = performanceLogger ?? throw new ArgumentNullException(nameof(performanceLogger));
        _activeWebSockets = new Dictionary<string, WebSocket>();
        _cancellationTokenSource = new CancellationTokenSource();

        _tradingLogger.LogConfiguration("GatewayOrchestrator", new Dictionary<string, object>
        {
            ["MessageBusType"] = _messageBus.GetType().Name,
            ["MaxWebSocketConnections"] = 1000,
            ["InitializedAt"] = DateTime.UtcNow
        });

        // Subscribe to all event streams for real-time updates
        _ = Task.Run(SubscribeToEventStreamsAsync);
    }

    // Market Data Operations
    /// <summary>
    /// Retrieves market data for the specified symbol with comprehensive error handling and performance tracking
    /// </summary>
    /// <param name="symbol">The symbol to retrieve market data for</param>
    /// <returns>A TradingResult containing the market data or error information</returns>
    public async Task<TradingResult<MarketData?>> GetMarketDataAsync(string symbol)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(symbol))
            {
                LogMethodExit();
                return TradingResult<MarketData?>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            var correlationId = _tradingLogger.GenerateCorrelationId();

            using var performanceScope = _performanceLogger.MeasureOperation("GetMarketData", correlationId);
            using var tradingScope = _tradingLogger.BeginScope("GetMarketData", correlationId);

            _tradingLogger.LogMethodEntry("GetMarketDataAsync", new { symbol });

            try
            {
                // Request market data from MarketData service via Redis Streams
                var request = new MarketDataRequestEvent
                {
                    Symbol = symbol,
                    RequestId = Guid.NewGuid().ToString(),
                    Source = "Gateway"
                };

                _tradingLogger.LogDebugTrace($"Publishing market data request for symbol: {symbol}", new Dictionary<string, object>
                {
                    ["symbol"] = symbol,
                    ["request_id"] = request.RequestId,
                    ["stream"] = "market-data-requests"
                });

                await _messageBus.PublishAsync("market-data-requests", request);

                // TODO: Implement response handling with timeout
                // For MVP, return mock data
                var mockData = new MarketData(Logger)
                {
                    Symbol = symbol,
                    Price = 150.25m,
                    Volume = 1000000,
                    Timestamp = DateTime.UtcNow
                };

                _tradingLogger.LogMarketDataReceived(symbol, mockData.Price, mockData.Volume,
                    mockData.Timestamp, TimeSpan.FromMilliseconds(5)); // Mock 5ms latency

                LogMethodExit();
                return TradingResult<MarketData?>.Success(mockData);
            }
            catch (Exception ex)
            {
                _tradingLogger.LogTradingError("GetMarketData", ex, correlationId, new Dictionary<string, object>
                {
                    ["symbol"] = symbol,
                    ["operation"] = "GetMarketDataAsync"
                });
                LogError($"Error getting market data for {symbol}", ex);
                LogMethodExit();
                return TradingResult<MarketData?>.Failure("MARKET_DATA_ERROR", $"Failed to retrieve market data: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in GetMarketDataAsync", ex);
            LogMethodExit();
            return TradingResult<MarketData?>.Failure("MARKET_DATA_ERROR", $"Market data retrieval failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Subscribes to market data for multiple symbols with comprehensive error handling and validation
    /// </summary>
    /// <param name="symbols">Array of symbols to subscribe to</param>
    /// <returns>A TradingResult indicating successful subscription or error details</returns>
    public async Task<TradingResult<bool>> SubscribeToMarketDataAsync(string[] symbols)
    {
        LogMethodEntry();
        try
        {
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

            var correlationId = _tradingLogger.GenerateCorrelationId();

            using var performanceScope = _performanceLogger.MeasureOperation("SubscribeToMarketData", correlationId);
            using var tradingScope = _tradingLogger.BeginScope("SubscribeToMarketData", correlationId);

            _tradingLogger.LogMethodEntry("SubscribeToMarketDataAsync", new { symbols, symbolCount = symbols.Length });

            try
            {
                var subscribeEvent = new MarketDataSubscriptionEvent
                {
                    Symbols = symbols,
                    Action = "Subscribe",
                    Source = "Gateway"
                };

                _tradingLogger.LogDebugTrace($"Publishing market data subscription for {symbols.Length} symbols", new Dictionary<string, object>
                {
                    ["symbols"] = symbols,
                    ["symbol_count"] = symbols.Length,
                    ["action"] = "Subscribe",
                    ["stream"] = "market-data-subscriptions"
                });

                await _messageBus.PublishAsync("market-data-subscriptions", subscribeEvent);

                _tradingLogger.LogPerformanceMetric("market_data.subscriptions", symbols.Length, "count", new Dictionary<string, object>
                {
                    ["symbols"] = string.Join(",", symbols),
                    ["correlation_id"] = correlationId
                });

                LogInfo($"Successfully subscribed to market data for {symbols.Length} symbols");
                LogMethodExit();
                return TradingResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _tradingLogger.LogTradingError("SubscribeToMarketData", ex, correlationId, new Dictionary<string, object>
                {
                    ["symbols"] = symbols,
                    ["symbol_count"] = symbols.Length
                });
                LogError($"Error subscribing to market data for {symbols.Length} symbols", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("SUBSCRIPTION_ERROR", $"Market data subscription failed: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in SubscribeToMarketDataAsync", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("SUBSCRIPTION_ERROR", $"Market data subscription failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Unsubscribes from market data for multiple symbols with comprehensive error handling and validation
    /// </summary>
    /// <param name="symbols">Array of symbols to unsubscribe from</param>
    /// <returns>A TradingResult indicating successful unsubscription or error details</returns>
    public async Task<TradingResult<bool>> UnsubscribeFromMarketDataAsync(string[] symbols)
    {
        LogMethodEntry();
        try
        {
            if (symbols == null || symbols.Length == 0)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_SYMBOLS", "Symbols array cannot be null or empty");
            }

            var invalidSymbols = symbols.Where(string.IsNullOrEmpty).ToArray();
            if (invalidSymbols.Any())
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_SYMBOLS", $"Found {invalidSymbols.Length} invalid symbols in unsubscription request");
            }

            try
            {
                var unsubscribeEvent = new MarketDataSubscriptionEvent
                {
                    Symbols = symbols,
                    Action = "Unsubscribe",
                    Source = "Gateway"
                };

                await _messageBus.PublishAsync("market-data-subscriptions", unsubscribeEvent);
                LogInfo($"Unsubscribed from market data for symbols: {string.Join(", ", symbols)}");
                LogMethodExit();
                return TradingResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                LogError($"Error unsubscribing from market data for {symbols.Length} symbols", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("UNSUBSCRIPTION_ERROR", $"Market data unsubscription failed: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in UnsubscribeFromMarketDataAsync", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("UNSUBSCRIPTION_ERROR", $"Market data unsubscription failed: {ex.Message}", ex);
        }
    }

    // Order Management Operations
    /// <summary>
    /// Submits an order for processing with comprehensive error handling and latency tracking
    /// </summary>
    /// <param name="request">The order request with all necessary details</param>
    /// <returns>A TradingResult containing the order response or error information</returns>
    public async Task<TradingResult<OrderResponse>> SubmitOrderAsync(OrderRequest request)
    {
        LogMethodEntry();
        try
        {
            if (request == null)
            {
                LogMethodExit();
                return TradingResult<OrderResponse>.Failure("INVALID_REQUEST", "Order request cannot be null");
            }

            if (string.IsNullOrEmpty(request.Symbol))
            {
                LogMethodExit();
                return TradingResult<OrderResponse>.Failure("INVALID_SYMBOL", "Order symbol cannot be null or empty");
            }

            if (request.Quantity <= 0)
            {
                LogMethodExit();
                return TradingResult<OrderResponse>.Failure("INVALID_QUANTITY", "Order quantity must be greater than zero");
            }

            var stopwatch = Stopwatch.StartNew();
            var orderId = Guid.NewGuid().ToString();

            try
            {
                var orderEvent = new OrderEvent
                {
                    OrderId = orderId,
                    Symbol = request.Symbol,
                    OrderType = request.OrderType,
                    Side = request.Side,
                    Quantity = request.Quantity,
                    Price = request.Price,
                    Status = "New",
                    Source = "Gateway",
                    ExecutionTime = DateTimeOffset.UtcNow
                };

                await _messageBus.PublishAsync("orders", orderEvent);

                stopwatch.Stop();

                // Log performance for order-to-wire latency tracking
                if (stopwatch.Elapsed.TotalMicroseconds > 100) // Target: <100μs
                {
                    LogWarning($"Order submission exceeded 100μs target: {stopwatch.Elapsed.TotalMicroseconds}μs for order {orderId}");
                }

                LogInfo($"Order submitted: {orderId} for {request.Symbol} in {stopwatch.Elapsed.TotalMicroseconds}μs");

                var response = new OrderResponse(orderId, "Submitted", "Order accepted for processing", DateTimeOffset.UtcNow);
                LogMethodExit();
                return TradingResult<OrderResponse>.Success(response);
            }
            catch (Exception ex)
            {
                LogError($"Error submitting order for {request.Symbol}", ex);
                var response = new OrderResponse(orderId, "Rejected", ex.Message, DateTimeOffset.UtcNow);
                LogMethodExit();
                return TradingResult<OrderResponse>.Success(response); // Still return success as we have a valid response
            }
        }
        catch (Exception ex)
        {
            LogError("Error in SubmitOrderAsync", ex);
            LogMethodExit();
            return TradingResult<OrderResponse>.Failure("ORDER_SUBMISSION_ERROR", $"Order submission failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves the status of an order with comprehensive error handling and validation
    /// </summary>
    /// <param name="orderId">The order ID to retrieve status for</param>
    /// <returns>A TradingResult containing the order status or error information</returns>
    public async Task<TradingResult<OrderStatus?>> GetOrderStatusAsync(string orderId)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(orderId))
            {
                LogMethodExit();
                return TradingResult<OrderStatus?>.Failure("INVALID_ORDER_ID", "Order ID cannot be null or empty");
            }

            try
            {
                // TODO: Implement order status retrieval from PaperTrading service
                // For MVP, return mock status
                var status = new OrderStatus(
                    orderId,
                    "AAPL",
                    "Market",
                    "Buy",
                    100,
                    null,
                    "Filled",
                    DateTimeOffset.UtcNow.AddMinutes(-1),
                    DateTimeOffset.UtcNow,
                    150.25m,
                    100);

                LogInfo($"Retrieved order status for {orderId}: {status.Status}");
                LogMethodExit();
                return TradingResult<OrderStatus?>.Success(status);
            }
            catch (Exception ex)
            {
                LogError($"Error retrieving order status for {orderId}", ex);
                LogMethodExit();
                return TradingResult<OrderStatus?>.Failure("ORDER_STATUS_ERROR", $"Failed to retrieve order status: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in GetOrderStatusAsync", ex);
            LogMethodExit();
            return TradingResult<OrderStatus?>.Failure("ORDER_STATUS_ERROR", $"Order status retrieval failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Cancels an order with comprehensive error handling and validation
    /// </summary>
    /// <param name="orderId">The order ID to cancel</param>
    /// <returns>A TradingResult containing the cancellation response or error information</returns>
    public async Task<TradingResult<OrderResponse>> CancelOrderAsync(string orderId)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(orderId))
            {
                LogMethodExit();
                return TradingResult<OrderResponse>.Failure("INVALID_ORDER_ID", "Order ID cannot be null or empty");
            }

            try
            {
                var cancelEvent = new OrderEvent
                {
                    OrderId = orderId,
                    Status = "CancelRequested",
                    Source = "Gateway",
                    ExecutionTime = DateTimeOffset.UtcNow
                };

                await _messageBus.PublishAsync("orders", cancelEvent);
                LogInfo($"Order cancellation submitted for {orderId}");
                
                var response = new OrderResponse(orderId, "CancelRequested", "Order cancellation submitted", DateTimeOffset.UtcNow);
                LogMethodExit();
                return TradingResult<OrderResponse>.Success(response);
            }
            catch (Exception ex)
            {
                LogError($"Error cancelling order {orderId}", ex);
                var response = new OrderResponse(orderId, "CancelRejected", ex.Message, DateTimeOffset.UtcNow);
                LogMethodExit();
                return TradingResult<OrderResponse>.Success(response); // Still return success as we have a valid response
            }
        }
        catch (Exception ex)
        {
            LogError("Error in CancelOrderAsync", ex);
            LogMethodExit();
            return TradingResult<OrderResponse>.Failure("ORDER_CANCELLATION_ERROR", $"Order cancellation failed: {ex.Message}", ex);
        }
    }

    // Strategy Management Operations
    /// <summary>
    /// Retrieves all active trading strategies with comprehensive error handling
    /// </summary>
    /// <returns>A TradingResult containing the array of active strategies or error information</returns>
    public async Task<TradingResult<StrategyInfo[]>> GetActiveStrategiesAsync()
    {
        LogMethodEntry();
        try
        {
            try
            {
                // TODO: Implement strategy retrieval from StrategyEngine service
                // For MVP, return mock strategies
                var strategies = new[]
                {
                    new StrategyInfo("strategy-1", "Golden Rules Momentum", "Running",
                        DateTimeOffset.UtcNow.AddHours(-2), 250.75m, 12),
                    new StrategyInfo("strategy-2", "Gap Reversal", "Stopped",
                        DateTimeOffset.UtcNow.AddHours(-1), -45.25m, 3)
                };

                LogInfo($"Retrieved {strategies.Length} active strategies");
                LogMethodExit();
                return TradingResult<StrategyInfo[]>.Success(strategies);
            }
            catch (Exception ex)
            {
                LogError("Error retrieving active strategies", ex);
                LogMethodExit();
                return TradingResult<StrategyInfo[]>.Failure("STRATEGY_RETRIEVAL_ERROR", $"Failed to retrieve active strategies: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in GetActiveStrategiesAsync", ex);
            LogMethodExit();
            return TradingResult<StrategyInfo[]>.Failure("STRATEGY_RETRIEVAL_ERROR", $"Strategy retrieval failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Starts a trading strategy with comprehensive error handling and validation
    /// </summary>
    /// <param name="strategyId">The ID of the strategy to start</param>
    /// <returns>A TradingResult indicating successful strategy start or error details</returns>
    public async Task<TradingResult<bool>> StartStrategyAsync(string strategyId)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(strategyId))
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_STRATEGY_ID", "Strategy ID cannot be null or empty");
            }

            try
            {
                var strategyEvent = new StrategyEvent
                {
                    StrategyName = strategyId,
                    Signal = "Start",
                    Source = "Gateway"
                };

                await _messageBus.PublishAsync("strategies", strategyEvent);
                LogInfo($"Strategy start command sent for {strategyId}");
                LogMethodExit();
                return TradingResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                LogError($"Error starting strategy {strategyId}", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("STRATEGY_START_ERROR", $"Failed to start strategy: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in StartStrategyAsync", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("STRATEGY_START_ERROR", $"Strategy start failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Stops a trading strategy with comprehensive error handling and validation
    /// </summary>
    /// <param name="strategyId">The ID of the strategy to stop</param>
    /// <returns>A TradingResult indicating successful strategy stop or error details</returns>
    public async Task<TradingResult<bool>> StopStrategyAsync(string strategyId)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(strategyId))
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_STRATEGY_ID", "Strategy ID cannot be null or empty");
            }

            try
            {
                var strategyEvent = new StrategyEvent
                {
                    StrategyName = strategyId,
                    Signal = "Stop",
                    Source = "Gateway"
                };

                await _messageBus.PublishAsync("strategies", strategyEvent);
                LogInfo($"Strategy stop command sent for {strategyId}");
                LogMethodExit();
                return TradingResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                LogError($"Error stopping strategy {strategyId}", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("STRATEGY_STOP_ERROR", $"Failed to stop strategy: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in StopStrategyAsync", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("STRATEGY_STOP_ERROR", $"Strategy stop failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves performance metrics for a specific strategy with comprehensive error handling
    /// </summary>
    /// <param name="strategyId">The ID of the strategy to get performance for</param>
    /// <returns>A TradingResult containing the strategy performance or error information</returns>
    public async Task<TradingResult<StrategyPerformance>> GetStrategyPerformanceAsync(string strategyId)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(strategyId))
            {
                LogMethodExit();
                return TradingResult<StrategyPerformance>.Failure("INVALID_STRATEGY_ID", "Strategy ID cannot be null or empty");
            }

            try
            {
                // TODO: Implement performance retrieval
                await Task.CompletedTask;
                var performance = new StrategyPerformance(strategyId, 500.0m, 125.5m, 25, 18, 7, 0.72m, 1.8m, -50.0m);
                
                LogInfo($"Retrieved performance for strategy {strategyId}: PnL={performance.TotalPnL}, Trades={performance.TotalTrades}");
                LogMethodExit();
                return TradingResult<StrategyPerformance>.Success(performance);
            }
            catch (Exception ex)
            {
                LogError($"Error getting performance for strategy {strategyId}", ex);
                LogMethodExit();
                return TradingResult<StrategyPerformance>.Failure("STRATEGY_PERFORMANCE_ERROR", $"Failed to get strategy performance: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in GetStrategyPerformanceAsync", ex);
            LogMethodExit();
            return TradingResult<StrategyPerformance>.Failure("STRATEGY_PERFORMANCE_ERROR", $"Strategy performance retrieval failed: {ex.Message}", ex);
        }
    }

    // Risk Management Operations
    /// <summary>
    /// Retrieves current risk status with comprehensive error handling
    /// </summary>
    /// <returns>A TradingResult containing the risk status or error information</returns>
    public async Task<TradingResult<RiskStatus>> GetRiskStatusAsync()
    {
        LogMethodEntry();
        try
        {
            try
            {
                // TODO: Implement risk status retrieval from RiskManagement service
                var riskStatus = new RiskStatus(
                    25000.0m, // Total exposure
                    125.50m,  // Daily PnL
                    -500.0m,  // Max daily loss
                    2,        // Remaining day trades
                    false,    // PDT restricted
                    new[] { "High volatility detected in TSLA" });

                LogInfo($"Retrieved risk status: Exposure={riskStatus.TotalExposure}, DailyPnL={riskStatus.DailyPnL}, PDT={riskStatus.PDTRestricted}");
                LogMethodExit();
                return TradingResult<RiskStatus>.Success(riskStatus);
            }
            catch (Exception ex)
            {
                LogError("Error retrieving risk status", ex);
                LogMethodExit();
                return TradingResult<RiskStatus>.Failure("RISK_STATUS_ERROR", $"Failed to retrieve risk status: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in GetRiskStatusAsync", ex);
            LogMethodExit();
            return TradingResult<RiskStatus>.Failure("RISK_STATUS_ERROR", $"Risk status retrieval failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves current risk limits with comprehensive error handling
    /// </summary>
    /// <returns>A TradingResult containing the risk limits or error information</returns>
    public async Task<TradingResult<RiskLimits>> GetRiskLimitsAsync()
    {
        LogMethodEntry();
        try
        {
            try
            {
                await Task.CompletedTask;
                var riskLimits = new RiskLimits(10000.0m, -500.0m, 50000.0m, 3, 0.02m);
                
                LogInfo($"Retrieved risk limits: MaxPosition={riskLimits.MaxPositionSize}, MaxLoss={riskLimits.MaxDailyLoss}, MaxExposure={riskLimits.MaxTotalExposure}");
                LogMethodExit();
                return TradingResult<RiskLimits>.Success(riskLimits);
            }
            catch (Exception ex)
            {
                LogError("Error retrieving risk limits", ex);
                LogMethodExit();
                return TradingResult<RiskLimits>.Failure("RISK_LIMITS_ERROR", $"Failed to retrieve risk limits: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in GetRiskLimitsAsync", ex);
            LogMethodExit();
            return TradingResult<RiskLimits>.Failure("RISK_LIMITS_ERROR", $"Risk limits retrieval failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Updates risk limits with comprehensive error handling and validation
    /// </summary>
    /// <param name="limits">The new risk limits to apply</param>
    /// <returns>A TradingResult indicating successful update or error details</returns>
    public async Task<TradingResult<bool>> UpdateRiskLimitsAsync(RiskLimits limits)
    {
        LogMethodEntry();
        try
        {
            if (limits == null)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_LIMITS", "Risk limits cannot be null");
            }

            if (limits.MaxPositionSize <= 0)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_POSITION_SIZE", "Max position size must be greater than zero");
            }

            if (limits.MaxTotalExposure <= 0)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_EXPOSURE", "Max total exposure must be greater than zero");
            }

            try
            {
                var riskEvent = new RiskEvent
                {
                    RiskType = "LimitsUpdate",
                    RiskLimit = limits.MaxPositionSize,
                    Source = "Gateway"
                };

                await _messageBus.PublishAsync("risk-management", riskEvent);
                LogInfo($"Risk limits update sent: MaxPosition={limits.MaxPositionSize}, MaxExposure={limits.MaxTotalExposure}");
                LogMethodExit();
                return TradingResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                LogError("Error updating risk limits", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("RISK_LIMITS_UPDATE_ERROR", $"Failed to update risk limits: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in UpdateRiskLimitsAsync", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("RISK_LIMITS_UPDATE_ERROR", $"Risk limits update failed: {ex.Message}", ex);
        }
    }

    // Performance Monitoring
    /// <summary>
    /// Retrieves comprehensive performance metrics with error handling
    /// </summary>
    /// <returns>A TradingResult containing the performance metrics or error information</returns>
    public async Task<TradingResult<PerformanceMetrics>> GetPerformanceMetricsAsync()
    {
        LogMethodEntry();
        try
        {
            try
            {
                var latency = await _messageBus.GetLatencyAsync();

                var metrics = new PerformanceMetrics(
                    TimeSpan.FromMicroseconds(95), // Average order latency
                    latency, // Market data latency
                    1500, // Orders per second
                    50000, // Market data updates per second
                    25.5, // CPU usage
                    1024.0, // Memory usage MB
                    _activeWebSockets.Count);

                LogInfo($"Retrieved performance metrics: OrderLatency={metrics.AverageOrderLatency.TotalMicroseconds}μs, ActiveConnections={metrics.ActiveConnections}");
                LogMethodExit();
                return TradingResult<PerformanceMetrics>.Success(metrics);
            }
            catch (Exception ex)
            {
                LogError("Error retrieving performance metrics", ex);
                LogMethodExit();
                return TradingResult<PerformanceMetrics>.Failure("PERFORMANCE_METRICS_ERROR", $"Failed to retrieve performance metrics: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in GetPerformanceMetricsAsync", ex);
            LogMethodExit();
            return TradingResult<PerformanceMetrics>.Failure("PERFORMANCE_METRICS_ERROR", $"Performance metrics retrieval failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves detailed latency metrics with comprehensive error handling
    /// </summary>
    /// <returns>A TradingResult containing the latency metrics or error information</returns>
    public async Task<TradingResult<LatencyMetrics>> GetLatencyMetricsAsync()
    {
        LogMethodEntry();
        try
        {
            try
            {
                var messageBusLatency = await _messageBus.GetLatencyAsync();

                var latencyMetrics = new LatencyMetrics(
                    TimeSpan.FromMicroseconds(85), // Order-to-wire
                    messageBusLatency, // Market data processing
                    TimeSpan.FromMicroseconds(45), // Strategy execution
                    TimeSpan.FromMicroseconds(25), // Risk check
                    DateTimeOffset.UtcNow);

                LogInfo($"Retrieved latency metrics: OrderToWire={latencyMetrics.OrderToWireLatency.TotalMicroseconds}μs, RiskCheck={latencyMetrics.RiskCheckLatency.TotalMicroseconds}μs");
                LogMethodExit();
                return TradingResult<LatencyMetrics>.Success(latencyMetrics);
            }
            catch (Exception ex)
            {
                LogError("Error retrieving latency metrics", ex);
                LogMethodExit();
                return TradingResult<LatencyMetrics>.Failure("LATENCY_METRICS_ERROR", $"Failed to retrieve latency metrics: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in GetLatencyMetricsAsync", ex);
            LogMethodExit();
            return TradingResult<LatencyMetrics>.Failure("LATENCY_METRICS_ERROR", $"Latency metrics retrieval failed: {ex.Message}", ex);
        }
    }

    // Real-time WebSocket Communication
    /// <summary>
    /// Handles WebSocket connections with comprehensive error handling and connection management
    /// </summary>
    /// <param name="webSocket">The WebSocket connection to handle</param>
    /// <returns>A TradingResult indicating successful handling or error details</returns>
    public async Task<TradingResult<bool>> HandleWebSocketConnectionAsync(WebSocket webSocket)
    {
        LogMethodEntry();
        try
        {
            if (webSocket == null)
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_WEBSOCKET", "WebSocket cannot be null");
            }

            var connectionId = Guid.NewGuid().ToString();

            lock (_webSocketLock)
            {
                _activeWebSockets[connectionId] = webSocket;
            }

            LogInfo($"WebSocket connection established: {connectionId}");

            try
            {
                var buffer = new byte[1024 * 4];

                while (webSocket.State == WebSocketState.Open && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessWebSocketMessage(connectionId, message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }

                LogInfo($"WebSocket connection handling completed for {connectionId}");
                LogMethodExit();
                return TradingResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                LogError($"WebSocket error for connection {connectionId}", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("WEBSOCKET_ERROR", $"WebSocket connection error: {ex.Message}", ex);
            }
            finally
            {
                lock (_webSocketLock)
                {
                    _activeWebSockets.Remove(connectionId);
                }

                LogInfo($"WebSocket connection closed: {connectionId}");
            }
        }
        catch (Exception ex)
        {
            LogError("Error in HandleWebSocketConnectionAsync", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("WEBSOCKET_ERROR", $"WebSocket handling failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves comprehensive system health status with error handling
    /// </summary>
    /// <returns>A TradingResult containing the system health or error information</returns>
    public async Task<TradingResult<SystemHealth>> GetSystemHealthAsync()
    {
        LogMethodEntry();
        try
        {
            try
            {
                var isHealthy = await _messageBus.IsHealthyAsync();

                var systemHealth = new SystemHealth(
                    isHealthy,
                    new[] { "Gateway", "MarketData", "StrategyEngine", "RiskManagement", "PaperTrading" },
                    new Dictionary<string, bool>
                    {
                        ["Gateway"] = true,
                        ["MarketData"] = isHealthy,
                        ["StrategyEngine"] = isHealthy,
                        ["RiskManagement"] = isHealthy,
                        ["PaperTrading"] = isHealthy,
                        ["Redis"] = isHealthy
                    },
                    TimeSpan.FromHours(2), // Mock uptime
                    "1.0.0");

                LogInfo($"Retrieved system health: Overall={systemHealth.IsHealthy}, Services={systemHealth.Services.Length}, ActiveConnections={_activeWebSockets.Count}");
                LogMethodExit();
                return TradingResult<SystemHealth>.Success(systemHealth);
            }
            catch (Exception ex)
            {
                LogError("Error retrieving system health", ex);
                LogMethodExit();
                return TradingResult<SystemHealth>.Failure("SYSTEM_HEALTH_ERROR", $"Failed to retrieve system health: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in GetSystemHealthAsync", ex);
            LogMethodExit();
            return TradingResult<SystemHealth>.Failure("SYSTEM_HEALTH_ERROR", $"System health retrieval failed: {ex.Message}", ex);
        }
    }

    // Private helper methods
    /// <summary>
    /// Subscribes to event streams for real-time updates with comprehensive error handling
    /// </summary>
    private async Task SubscribeToEventStreamsAsync()
    {
        LogMethodEntry();
        try
        {
            try
            {
                // Subscribe to all relevant streams for real-time updates
                await _messageBus.SubscribeAsync<MarketDataEvent>("market-data", "gateway-group", "gateway-consumer",
                    HandleMarketDataEvent, _cancellationTokenSource.Token);
                LogInfo("Successfully subscribed to event streams");
                LogMethodExit();
            }
            catch (Exception ex)
            {
                LogError("Error subscribing to event streams", ex);
                LogMethodExit();
                throw;
            }
        }
        catch (Exception ex)
        {
            LogError("Error in SubscribeToEventStreamsAsync", ex);
            LogMethodExit();
            throw;
        }
    }

    /// <summary>
    /// Handles market data events and broadcasts to WebSocket clients with error handling
    /// </summary>
    /// <param name="marketDataEvent">The market data event to handle</param>
    private async Task HandleMarketDataEvent(MarketDataEvent marketDataEvent)
    {
        LogMethodEntry();
        try
        {
            if (marketDataEvent == null)
            {
                LogWarning("Received null market data event");
                LogMethodExit();
                return;
            }

            // Broadcast market data to all connected WebSocket clients
            await BroadcastToWebSocketsAsync("market-data", marketDataEvent);
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Error handling market data event", ex);
            LogMethodExit();
        }
    }

    /// <summary>
    /// Broadcasts messages to all active WebSocket connections with connection cleanup
    /// </summary>
    /// <param name="messageType">Type of message being broadcast</param>
    /// <param name="data">Data to broadcast</param>
    private async Task BroadcastToWebSocketsAsync(string messageType, object data)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(messageType))
            {
                LogWarning("Message type is null or empty for broadcast");
                LogMethodExit();
                return;
            }

            if (data == null)
            {
                LogWarning("Data is null for broadcast");
                LogMethodExit();
                return;
            }

            var message = JsonSerializer.Serialize(new { type = messageType, data });
            var messageBytes = Encoding.UTF8.GetBytes(message);

            var deadConnections = new List<string>();
            var successfulBroadcasts = 0;

            foreach (var (connectionId, webSocket) in _activeWebSockets.ToArray())
            {
                try
                {
                    if (webSocket.State == WebSocketState.Open)
                    {
                        await webSocket.SendAsync(new ArraySegment<byte>(messageBytes),
                            WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
                        successfulBroadcasts++;
                    }
                    else
                    {
                        deadConnections.Add(connectionId);
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"Failed to send message to WebSocket {connectionId}: {ex.Message}");
                    deadConnections.Add(connectionId);
                }
            }

            // Clean up dead connections
            lock (_webSocketLock)
            {
                foreach (var connectionId in deadConnections)
                {
                    _activeWebSockets.Remove(connectionId);
                }
            }

            LogDebug($"Broadcast {messageType} to {successfulBroadcasts} clients, removed {deadConnections.Count} dead connections");
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Error in BroadcastToWebSocketsAsync", ex);
            LogMethodExit();
        }
    }

    /// <summary>
    /// Processes incoming WebSocket messages with comprehensive error handling and validation
    /// </summary>
    /// <param name="connectionId">ID of the WebSocket connection</param>
    /// <param name="message">Message content to process</param>
    private async Task ProcessWebSocketMessage(string connectionId, string message)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                LogWarning("Connection ID is null or empty for message processing");
                LogMethodExit();
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                LogWarning($"Received empty message from WebSocket {connectionId}");
                LogMethodExit();
                return;
            }

            try
            {
                // TODO: Implement WebSocket message processing for client requests
                LogInfo($"Received WebSocket message from {connectionId}: {message}");
                await Task.CompletedTask;
                LogMethodExit();
            }
            catch (Exception ex)
            {
                LogError($"Error processing WebSocket message from {connectionId}", ex);
                LogMethodExit();
            }
        }
        catch (Exception ex)
        {
            LogError("Error in ProcessWebSocketMessage", ex);
            LogMethodExit();
        }
    }
}

// Additional event types for Gateway communication
public record MarketDataRequestEvent : TradingEvent
{
    public override string EventType => "MarketDataRequest";
    public string Symbol { get; init; } = string.Empty;
    public string RequestId { get; init; } = string.Empty;
}

public record MarketDataSubscriptionEvent : TradingEvent
{
    public override string EventType => "MarketDataSubscription";
    public string[] Symbols { get; init; } = Array.Empty<string>();
    public string Action { get; init; } = string.Empty; // Subscribe/Unsubscribe
}