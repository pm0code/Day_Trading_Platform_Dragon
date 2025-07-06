using System.Diagnostics;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.Messaging.Events;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.MarketData.Services;

/// <summary>
/// High-performance market data service for on-premise trading workstation
/// Integrates with external providers and distributes data via Redis Streams
/// All operations use canonical patterns with comprehensive logging and error handling
/// Maintains sub-millisecond performance targets for real-time trading operations
/// </summary>
public class MarketDataService : CanonicalServiceBase, IMarketDataService
{
    private readonly IDataIngestionService _dataIngestionService;
    private readonly IMarketDataAggregator _marketDataAggregator;
    private readonly IMarketDataCache _cache;
    private readonly IMessageBus _messageBus;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Dictionary<string, DateTime> _lastUpdateTimes;
    private readonly object _metricsLock = new();

    // Performance tracking
    private long _totalRequests;
    private long _cacheHits;
    private long _cacheMisses;
    private readonly List<TimeSpan> _latencySamples = new();
    private readonly DateTime _startTime = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the MarketDataService with comprehensive dependencies and canonical patterns
    /// </summary>
    /// <param name="dataIngestionService">Data ingestion service for historical data retrieval</param>
    /// <param name="marketDataAggregator">Market data aggregator for real-time data</param>
    /// <param name="cache">High-performance cache for sub-millisecond response times</param>
    /// <param name="messageBus">Message bus for Redis Streams distribution</param>
    /// <param name="logger">Trading logger for comprehensive market data operation tracking</param>
    public MarketDataService(
        IDataIngestionService dataIngestionService,
        IMarketDataAggregator marketDataAggregator,
        IMarketDataCache cache,
        IMessageBus messageBus,
        ITradingLogger logger) : base(logger, "MarketDataService")
    {
        _dataIngestionService = dataIngestionService ?? throw new ArgumentNullException(nameof(dataIngestionService));
        _marketDataAggregator = marketDataAggregator ?? throw new ArgumentNullException(nameof(marketDataAggregator));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        _cancellationTokenSource = new CancellationTokenSource();
        _lastUpdateTimes = new Dictionary<string, DateTime>();
    }

    /// <summary>
    /// Retrieves real-time market data for a single symbol with comprehensive error handling and performance tracking
    /// Implements sub-millisecond caching and Redis Streams distribution for high-frequency trading
    /// </summary>
    /// <param name="symbol">The trading symbol to retrieve market data for</param>
    /// <returns>A TradingResult containing the market data or error information</returns>
    public async Task<TradingResult<Core.Models.MarketData?>> GetMarketDataAsync(string symbol)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(symbol))
            {
                LogMethodExit();
                return TradingResult<Core.Models.MarketData?>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _totalRequests);

            try
            {
                // Try cache first for sub-millisecond response
                var cachedData = await _cache.GetAsync(symbol);
                if (cachedData != null && IsDataFresh(symbol, cachedData.Timestamp))
                {
                    Interlocked.Increment(ref _cacheHits);
                    stopwatch.Stop();
                    RecordLatency(stopwatch.Elapsed);

                    LogDebug($"Cache hit for {symbol} in {stopwatch.Elapsed.TotalMicroseconds}μs");
                    LogMethodExit();
                    return TradingResult<Core.Models.MarketData?>.Success(cachedData);
                }

                Interlocked.Increment(ref _cacheMisses);

                // Fetch from providers using aggregator
                var freshData = await _marketDataAggregator.GetMarketDataAsync(symbol);
                if (freshData != null)
                {
                    // Cache with 5-second TTL for high-frequency trading
                    await _cache.SetAsync(symbol, freshData, TimeSpan.FromSeconds(5));

                    // Update last update time
                    lock (_lastUpdateTimes)
                    {
                        _lastUpdateTimes[symbol] = DateTime.UtcNow;
                    }

                    // Publish to Redis Streams for real-time distribution
                    var marketDataEvent = new MarketDataEvent
                    {
                        Symbol = symbol,
                        Price = freshData.Price,
                        Volume = freshData.Volume,
                        Timestamp = freshData.Timestamp,
                        Source = "MarketDataService"
                    };

                    await _messageBus.PublishAsync("market-data", marketDataEvent);

                    stopwatch.Stop();
                    RecordLatency(stopwatch.Elapsed);

                    LogDebug($"Updated market data for {symbol} in {stopwatch.Elapsed.TotalMicroseconds}μs");
                    LogMethodExit();
                    return TradingResult<Core.Models.MarketData?>.Success(freshData);
                }

                stopwatch.Stop();
                LogWarning($"No market data available for {symbol}");
                LogMethodExit();
                return TradingResult<Core.Models.MarketData?>.Success(null);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogError($"Error retrieving market data for {symbol}", ex);
                LogMethodExit();
                return TradingResult<Core.Models.MarketData?>.Failure("MARKET_DATA_ERROR", $"Failed to retrieve market data: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in GetMarketDataAsync", ex);
            LogMethodExit();
            return TradingResult<Core.Models.MarketData?>.Failure("MARKET_DATA_ERROR", $"Market data retrieval failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves market data for multiple symbols in a single high-performance batch request
    /// Uses parallel processing to maximize throughput while maintaining error isolation
    /// </summary>
    /// <param name="symbols">Array of trading symbols to retrieve market data for</param>
    /// <returns>A TradingResult containing the market data dictionary or error information</returns>
    public async Task<TradingResult<Dictionary<string, Core.Models.MarketData>>> GetMarketDataBatchAsync(string[] symbols)
    {
        LogMethodEntry();
        try
        {
            if (symbols == null || symbols.Length == 0)
            {
                LogMethodExit();
                return TradingResult<Dictionary<string, Core.Models.MarketData>>.Failure("INVALID_SYMBOLS", "Symbols array cannot be null or empty");
            }

            var stopwatch = Stopwatch.StartNew();
            var results = new Dictionary<string, Core.Models.MarketData>();

            try
            {
                // Process symbols in parallel for maximum throughput
                var tasks = symbols.Select(async symbol =>
                {
                    var dataResult = await GetMarketDataAsync(symbol);
                    return new { Symbol = symbol, Result = dataResult };
                }).ToArray();

                var batchResults = await Task.WhenAll(tasks);

                foreach (var result in batchResults)
                {
                    if (result.Result.IsSuccess && result.Result.Value != null)
                    {
                        results[result.Symbol] = result.Result.Value;
                    }
                }

                stopwatch.Stop();
                LogDebug($"Batch request for {symbols.Length} symbols completed in {stopwatch.Elapsed.TotalMilliseconds}ms, {results.Count} successful");
                LogMethodExit();
                return TradingResult<Dictionary<string, Core.Models.MarketData>>.Success(results);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogError("Error processing batch market data request", ex);
                LogMethodExit();
                return TradingResult<Dictionary<string, Core.Models.MarketData>>.Failure("BATCH_MARKET_DATA_ERROR", $"Batch market data request failed: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in GetMarketDataBatchAsync", ex);
            LogMethodExit();
            return TradingResult<Dictionary<string, Core.Models.MarketData>>.Failure("BATCH_MARKET_DATA_ERROR", $"Batch market data retrieval failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves historical market data for a symbol with intelligent interval-based date range selection
    /// Optimizes data retrieval based on the requested interval for efficient performance
    /// </summary>
    /// <param name="symbol">The trading symbol to retrieve historical data for</param>
    /// <param name="interval">The data interval (1m, 5m, 15m, 30m, 1h, 4h, 1d, 1w)</param>
    /// <returns>A TradingResult containing the historical data or error information</returns>
    public async Task<TradingResult<HistoricalData?>> GetHistoricalDataAsync(string symbol, string interval)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(symbol))
            {
                LogMethodExit();
                return TradingResult<HistoricalData?>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            if (string.IsNullOrEmpty(interval))
            {
                LogMethodExit();
                return TradingResult<HistoricalData?>.Failure("INVALID_INTERVAL", "Interval cannot be null or empty");
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Use DataIngestion service for historical data
                // For now, default to last 30 days of data based on interval
                var endDate = DateTime.UtcNow;
                var startDate = interval.ToLower() switch
                {
                    "1m" or "5m" or "15m" or "30m" => endDate.AddDays(-1), // Intraday data for 1 day
                    "1h" or "4h" => endDate.AddDays(-7), // Hourly data for 1 week
                    "1d" or "daily" => endDate.AddDays(-30), // Daily data for 30 days
                    "1w" or "weekly" => endDate.AddMonths(-6), // Weekly data for 6 months
                    _ => endDate.AddDays(-30) // Default to 30 days
                };

                var dailyDataList = await _dataIngestionService.GetHistoricalDataAsync(symbol, startDate, endDate);

                // Convert List<DailyData> to HistoricalData
                var historicalData = new HistoricalData
                {
                    Symbol = symbol,
                    DailyPrices = dailyDataList,
                    StartDate = startDate,
                    EndDate = endDate
                };

                stopwatch.Stop();
                LogDebug($"Retrieved historical data for {symbol} ({interval}) in {stopwatch.Elapsed.TotalMilliseconds}ms, {dailyDataList?.Count ?? 0} data points");
                LogMethodExit();
                return TradingResult<HistoricalData?>.Success(historicalData);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogError($"Error retrieving historical data for {symbol}", ex);
                LogMethodExit();
                return TradingResult<HistoricalData?>.Failure("HISTORICAL_DATA_ERROR", $"Failed to retrieve historical data: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in GetHistoricalDataAsync", ex);
            LogMethodExit();
            return TradingResult<HistoricalData?>.Failure("HISTORICAL_DATA_ERROR", $"Historical data retrieval failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Starts background processing for Redis Streams and real-time data distribution
    /// Establishes subscriptions for market data requests and subscription management
    /// </summary>
    /// <returns>A TradingResult indicating success or failure of background processing startup</returns>
    public async Task<TradingResult<bool>> StartBackgroundProcessingAsync()
    {
        LogMethodEntry();
        try
        {
            LogInfo("Starting background Redis Streams processing");

            try
            {
                // Subscribe to market data requests from Gateway
                await _messageBus.SubscribeAsync<MarketDataRequestEvent>("market-data-requests",
                    "marketdata-group", "marketdata-consumer",
                    HandleMarketDataRequest, _cancellationTokenSource.Token);

                // Subscribe to subscription management requests
                await _messageBus.SubscribeAsync<MarketDataSubscriptionEvent>("market-data-subscriptions",
                    "marketdata-group", "subscription-consumer",
                    HandleSubscriptionRequest, _cancellationTokenSource.Token);

                LogInfo("Background processing started successfully");
                LogMethodExit();
                return TradingResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                LogError("Error starting background processing", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("BACKGROUND_PROCESSING_ERROR", $"Failed to start background processing: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in StartBackgroundProcessingAsync", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("BACKGROUND_PROCESSING_ERROR", $"Background processing startup failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves comprehensive health status of the market data service including provider latency and cache statistics
    /// Performs real-time health checks and identifies potential performance issues
    /// </summary>
    /// <returns>A TradingResult containing the health status or error information</returns>
    public async Task<TradingResult<MarketDataHealthStatus>> GetHealthStatusAsync()
    {
        LogMethodEntry();
        try
        {
            try
            {
                var isHealthy = await _messageBus.IsHealthyAsync();
                var providerLatency = await MeasureProviderLatency();
                var cacheStats = await _cache.GetStatsAsync();

                var issues = new List<string>();

                if (providerLatency.TotalMilliseconds > 1000)
                    issues.Add($"High provider latency: {providerLatency.TotalMilliseconds:F1}ms");

                if (cacheStats.HitRate < 0.8)
                    issues.Add($"Low cache hit rate: {cacheStats.HitRate:P1}");

                var healthStatus = new MarketDataHealthStatus(
                    isHealthy && issues.Count == 0,
                    isHealthy ? "Healthy" : "Degraded",
                    providerLatency,
                    await GetActiveSubscriptionCount(),
                    _totalRequests,
                    DateTime.UtcNow,
                    issues.ToArray());

                LogMethodExit();
                return TradingResult<MarketDataHealthStatus>.Success(healthStatus);
            }
            catch (Exception ex)
            {
                LogError("Error getting health status", ex);
                var errorHealthStatus = new MarketDataHealthStatus(false, "Error", TimeSpan.Zero, 0, 0, DateTime.UtcNow,
                    new[] { ex.Message });
                LogMethodExit();
                return TradingResult<MarketDataHealthStatus>.Success(errorHealthStatus);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in GetHealthStatusAsync", ex);
            LogMethodExit();
            return TradingResult<MarketDataHealthStatus>.Failure("HEALTH_STATUS_ERROR", $"Health status retrieval failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves comprehensive performance metrics for the market data service
    /// Includes throughput, latency, cache performance, and operational statistics
    /// </summary>
    /// <returns>A TradingResult containing the performance metrics or error information</returns>
    public async Task<TradingResult<MarketDataMetrics>> GetPerformanceMetricsAsync()
    {
        LogMethodEntry();
        try
        {
            try
            {
                var cacheStats = await _cache.GetStatsAsync();
                var uptime = DateTime.UtcNow - _startTime;

                lock (_metricsLock)
                {
                    var rps = _totalRequests > 0 ? _totalRequests / Math.Max(1, uptime.TotalSeconds) : 0;
                    var avgLatency = _latencySamples.Count > 0 ?
                        TimeSpan.FromTicks((long)_latencySamples.Average(l => l.Ticks)) : TimeSpan.Zero;
                    var maxLatency = _latencySamples.Count > 0 ?
                        _latencySamples.Max() : TimeSpan.Zero;

                    var metrics = new MarketDataMetrics(
                        (long)rps,
                        avgLatency,
                        maxLatency,
                        cacheStats.HitRate,
                        0, // Active connections - to be implemented
                        _totalRequests,
                        uptime);

                    LogMethodExit();
                    return TradingResult<MarketDataMetrics>.Success(metrics);
                }
            }
            catch (Exception ex)
            {
                LogError("Error getting performance metrics", ex);
                LogMethodExit();
                return TradingResult<MarketDataMetrics>.Failure("PERFORMANCE_METRICS_ERROR", $"Failed to retrieve performance metrics: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in GetPerformanceMetricsAsync", ex);
            LogMethodExit();
            return TradingResult<MarketDataMetrics>.Failure("PERFORMANCE_METRICS_ERROR", $"Performance metrics retrieval failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Forces refresh of market data from external providers by invalidating cache and fetching fresh data
    /// Ensures the most up-to-date market data is available for trading decisions
    /// </summary>
    /// <param name="symbol">The trading symbol to refresh market data for</param>
    /// <returns>A TradingResult indicating success or failure of the refresh operation</returns>
    public async Task<TradingResult<bool>> RefreshMarketDataAsync(string symbol)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrEmpty(symbol))
            {
                LogMethodExit();
                return TradingResult<bool>.Failure("INVALID_SYMBOL", "Symbol cannot be null or empty");
            }

            try
            {
                // Invalidate cache to force fresh fetch
                await _cache.InvalidateAsync(symbol);

                // Fetch fresh data
                var refreshResult = await GetMarketDataAsync(symbol);

                if (refreshResult.IsSuccess)
                {
                    LogInfo($"Refreshed market data for {symbol}");
                    LogMethodExit();
                    return TradingResult<bool>.Success(true);
                }
                else
                {
                    LogWarning($"Failed to refresh market data for {symbol}: {refreshResult.Error?.Message}");
                    LogMethodExit();
                    return TradingResult<bool>.Failure("REFRESH_FAILED", $"Market data refresh failed: {refreshResult.Error?.Message}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error refreshing market data for {symbol}", ex);
                LogMethodExit();
                return TradingResult<bool>.Failure("REFRESH_ERROR", $"Failed to refresh market data: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in RefreshMarketDataAsync", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("REFRESH_ERROR", $"Market data refresh failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves detailed latency statistics including percentiles for performance analysis
    /// Provides comprehensive latency metrics for trading system optimization
    /// </summary>
    /// <returns>A TradingResult containing the latency statistics or error information</returns>
    public async Task<TradingResult<LatencyStats>> GetLatencyStatsAsync()
    {
        LogMethodEntry();
        try
        {
            await Task.CompletedTask;

            lock (_metricsLock)
            {
                if (_latencySamples.Count == 0)
                {
                    var emptyStats = new LatencyStats(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero,
                        TimeSpan.Zero, TimeSpan.Zero, 0, DateTime.UtcNow);
                    LogMethodExit();
                    return TradingResult<LatencyStats>.Success(emptyStats);
                }

                var sorted = _latencySamples.OrderBy(l => l.Ticks).ToArray();
                var average = TimeSpan.FromTicks((long)sorted.Average(l => l.Ticks));
                var p50 = sorted[sorted.Length / 2];
                var p95 = sorted[(int)(sorted.Length * 0.95)];
                var p99 = sorted[(int)(sorted.Length * 0.99)];
                var max = sorted.Last();

                var stats = new LatencyStats(average, p50, p95, p99, max, sorted.Length, DateTime.UtcNow);
                LogMethodExit();
                return TradingResult<LatencyStats>.Success(stats);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in GetLatencyStatsAsync", ex);
            LogMethodExit();
            return TradingResult<LatencyStats>.Failure("LATENCY_STATS_ERROR", $"Latency statistics retrieval failed: {ex.Message}", ex);
        }
    }

    // Private helper methods
    /// <summary>
    /// Handles market data requests from Redis Streams with comprehensive error handling and response publishing
    /// </summary>
    private async Task HandleMarketDataRequest(MarketDataRequestEvent request)
    {
        LogMethodEntry();
        try
        {
            if (request?.Symbol == null)
            {
                LogWarning("Received market data request with null symbol");
                LogMethodExit();
                return;
            }

            LogInfo($"Processing market data request for {request.Symbol} from {request.Source}");

            var dataResult = await GetMarketDataAsync(request.Symbol);

            if (dataResult.IsSuccess && dataResult.Value != null)
            {
                var responseEvent = new MarketDataEvent
                {
                    Symbol = request.Symbol,
                    Price = dataResult.Value.Price,
                    Volume = dataResult.Value.Volume,
                    Timestamp = dataResult.Value.Timestamp,
                    Source = "MarketDataService",
                    RequestId = request.RequestId
                };

                await _messageBus.PublishAsync("market-data", responseEvent);
            }
            else
            {
                LogWarning($"No market data available for {request.Symbol}, request ID: {request.RequestId}");
            }

            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Error handling market data request for {request?.Symbol}", ex);
            LogMethodExit();
        }
    }

    /// <summary>
    /// Handles subscription management requests for real-time market data updates
    /// </summary>
    private async Task HandleSubscriptionRequest(MarketDataSubscriptionEvent request)
    {
        LogMethodEntry();
        try
        {
            if (request?.Symbols == null || !request.Symbols.Any())
            {
                LogWarning("Received subscription request with null or empty symbols");
                LogMethodExit();
                return;
            }

            LogInfo($"Processing subscription request: {request.Action} for symbols: {string.Join(", ", request.Symbols)}");

            // Implementation would manage real-time subscriptions
            // For MVP, just acknowledge the request
            await Task.CompletedTask;

            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Error handling subscription request", ex);
            LogMethodExit();
        }
    }

    /// <summary>
    /// Determines if cached market data is still fresh for high-frequency trading requirements
    /// </summary>
    private bool IsDataFresh(string symbol, DateTime dataTimestamp)
    {
        LogMethodEntry();
        try
        {
            // Consider data fresh if it's less than 5 seconds old
            var isFresh = DateTime.UtcNow - dataTimestamp < TimeSpan.FromSeconds(5);
            LogMethodExit();
            return isFresh;
        }
        catch (Exception ex)
        {
            LogError("Error in IsDataFresh", ex);
            LogMethodExit();
            return false;
        }
    }

    /// <summary>
    /// Records latency metrics for performance monitoring and optimization
    /// </summary>
    private void RecordLatency(TimeSpan latency)
    {
        LogMethodEntry();
        try
        {
            lock (_metricsLock)
            {
                _latencySamples.Add(latency);

                // Keep only last 1000 samples for memory efficiency
                if (_latencySamples.Count > 1000)
                {
                    _latencySamples.RemoveAt(0);
                }
            }
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Error in RecordLatency", ex);
            LogMethodExit();
        }
    }

    /// <summary>
    /// Measures provider latency by testing with a common symbol for health monitoring
    /// </summary>
    private async Task<TimeSpan> MeasureProviderLatency()
    {
        LogMethodEntry();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Test with a common symbol
                await _marketDataAggregator.GetMarketDataAsync("AAPL");
                stopwatch.Stop();
                LogMethodExit();
                return stopwatch.Elapsed;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogWarning($"Provider latency measurement failed: {ex.Message}");
                LogMethodExit();
                return TimeSpan.FromSeconds(10); // High latency for errors
            }
        }
        catch (Exception ex)
        {
            LogError("Error in MeasureProviderLatency", ex);
            LogMethodExit();
            return TimeSpan.FromSeconds(10);
        }
    }

    /// <summary>
    /// Gets the count of active real-time subscriptions for monitoring purposes
    /// </summary>
    private async Task<int> GetActiveSubscriptionCount()
    {
        LogMethodEntry();
        try
        {
            // Implementation would track active subscriptions
            await Task.CompletedTask;
            LogMethodExit();
            return 0; // Placeholder for MVP
        }
        catch (Exception ex)
        {
            LogError("Error in GetActiveSubscriptionCount", ex);
            LogMethodExit();
            return 0;
        }
    }
}