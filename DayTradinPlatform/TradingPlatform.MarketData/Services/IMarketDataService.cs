using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.MarketData.Services;

/// <summary>
/// High-performance market data service interface for on-premise trading workstation
/// Provides real-time and historical market data with sub-millisecond distribution
/// All operations use TradingResult pattern for consistent error handling
/// </summary>
public interface IMarketDataService
{
    /// <summary>
    /// Get real-time market data for a single symbol
    /// </summary>
    Task<TradingResult<Core.Models.MarketData?>> GetMarketDataAsync(string symbol);

    /// <summary>
    /// Get market data for multiple symbols in a single request
    /// </summary>
    Task<TradingResult<Dictionary<string, Core.Models.MarketData>>> GetMarketDataBatchAsync(string[] symbols);

    /// <summary>
    /// Get historical market data for a symbol
    /// </summary>
    Task<TradingResult<HistoricalData?>> GetHistoricalDataAsync(string symbol, string interval);

    /// <summary>
    /// Start background processing for Redis Streams and real-time data distribution
    /// </summary>
    Task<TradingResult<bool>> StartBackgroundProcessingAsync();

    /// <summary>
    /// Get service health status
    /// </summary>
    Task<TradingResult<MarketDataHealthStatus>> GetHealthStatusAsync();

    /// <summary>
    /// Get performance metrics
    /// </summary>
    Task<TradingResult<MarketDataMetrics>> GetPerformanceMetricsAsync();

    /// <summary>
    /// Force refresh market data from external providers
    /// </summary>
    Task<TradingResult<bool>> RefreshMarketDataAsync(string symbol);

    /// <summary>
    /// Get market data latency statistics
    /// </summary>
    Task<TradingResult<LatencyStats>> GetLatencyStatsAsync();
}

/// <summary>
/// Market data subscription management interface
/// </summary>
public interface ISubscriptionManager
{
    /// <summary>
    /// Subscribe to real-time updates for a symbol
    /// </summary>
    Task SubscribeAsync(string symbol);

    /// <summary>
    /// Unsubscribe from real-time updates for a symbol
    /// </summary>
    Task UnsubscribeAsync(string symbol);

    /// <summary>
    /// Get all active subscriptions
    /// </summary>
    Task<string[]> GetActiveSubscriptionsAsync();

    /// <summary>
    /// Check if subscribed to a symbol
    /// </summary>
    Task<bool> IsSubscribedAsync(string symbol);
}

/// <summary>
/// High-performance market data caching interface
/// </summary>
public interface IMarketDataCache
{
    /// <summary>
    /// Get cached market data
    /// </summary>
    Task<Core.Models.MarketData?> GetAsync(string symbol);

    /// <summary>
    /// Cache market data with TTL
    /// </summary>
    Task SetAsync(string symbol, Core.Models.MarketData data, TimeSpan? ttl = null);

    /// <summary>
    /// Invalidate cached data for a symbol
    /// </summary>
    Task InvalidateAsync(string symbol);

    /// <summary>
    /// Get cache statistics
    /// </summary>
    Task<CacheStats> GetStatsAsync();
}

// Supporting data models
public record MarketDataHealthStatus(
    bool IsHealthy,
    string Status,
    TimeSpan ProviderLatency,
    int ActiveSubscriptions,
    long TotalRequests,
    DateTime LastUpdate,
    string[] Issues);

public record MarketDataMetrics(
    long RequestsPerSecond,
    TimeSpan AverageLatency,
    TimeSpan MaxLatency,
    double CacheHitRate,
    int ActiveConnections,
    long TotalDataPoints,
    TimeSpan Uptime);

public record LatencyStats(
    TimeSpan Average,
    TimeSpan P50,
    TimeSpan P95,
    TimeSpan P99,
    TimeSpan Max,
    int SampleCount,
    DateTime LastUpdated);

public record CacheStats(
    long HitCount,
    long MissCount,
    double HitRate,
    int EntryCount,
    long MemoryUsageBytes,
    TimeSpan AverageAge);