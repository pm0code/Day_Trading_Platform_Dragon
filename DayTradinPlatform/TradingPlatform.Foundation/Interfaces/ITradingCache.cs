namespace TradingPlatform.Foundation.Interfaces;

/// <summary>
/// High-performance caching interface optimized for trading applications.
/// Provides sub-millisecond access times and trading-specific cache patterns.
/// </summary>
public interface ITradingCache
{
    /// <summary>
    /// Gets cached value by key with generic type support.
    /// Returns null if key doesn't exist or has expired.
    /// </summary>
    /// <typeparam name="T">Type of cached value</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token for timeout control</param>
    /// <returns>Cached value or null if not found</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Gets cached value with fallback to factory function if not found.
    /// Ensures atomic cache population to prevent multiple factory calls for same key.
    /// </summary>
    /// <typeparam name="T">Type of cached value</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="factory">Factory function to create value if not cached</param>
    /// <param name="expiration">Cache expiration time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached or newly created value</returns>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiration, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Sets value in cache with specified expiration.
    /// Overwrites existing value if key already exists.
    /// </summary>
    /// <typeparam name="T">Type of value to cache</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to cache</param>
    /// <param name="expiration">Cache expiration time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Removes value from cache by key.
    /// Returns true if key existed and was removed, false if key didn't exist.
    /// </summary>
    /// <param name="key">Cache key to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if removed, false if key didn't exist</returns>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes multiple keys from cache in a single operation.
    /// More efficient than individual remove operations for bulk operations.
    /// </summary>
    /// <param name="keys">Cache keys to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of keys that were actually removed</returns>
    Task<int> RemoveBulkAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a key exists in the cache without retrieving the value.
    /// Useful for existence checks without deserialization overhead.
    /// </summary>
    /// <param name="key">Cache key to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if key exists, false otherwise</returns>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the time-to-live (TTL) for a cached key.
    /// Returns null if key doesn't exist or has no expiration.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Remaining TTL or null if key doesn't exist</returns>
    Task<TimeSpan?> GetTtlAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extends the expiration time for an existing cached key.
    /// Does nothing if key doesn't exist.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="expiration">New expiration time from now</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if expiration was updated, false if key doesn't exist</returns>
    Task<bool> ExtendExpirationAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cache statistics for monitoring and optimization.
    /// Includes hit rates, memory usage, and performance metrics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comprehensive cache statistics</returns>
    Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Specialized cache interface for market data with trading-specific features.
/// Provides optimized access patterns for real-time and historical market data.
/// </summary>
public interface IMarketDataCache : ITradingCache
{
    /// <summary>
    /// Caches real-time market data with automatic expiration based on market hours.
    /// Data expires faster during market hours for fresher data.
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="marketData">Market data to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CacheMarketDataAsync(string symbol, object marketData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cached market data for a symbol with automatic freshness validation.
    /// Returns null if data is too stale for trading purposes.
    /// </summary>
    /// <typeparam name="T">Market data type</typeparam>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="maxAge">Maximum acceptable age for the data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Fresh market data or null if stale/missing</returns>
    Task<T?> GetFreshMarketDataAsync<T>(string symbol, TimeSpan maxAge, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Caches historical data with long expiration times since it doesn't change.
    /// Uses optimized storage patterns for large historical datasets.
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="startDate">Historical data start date</param>
    /// <param name="endDate">Historical data end date</param>
    /// <param name="historicalData">Historical data to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CacheHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate, object historicalData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all market data cache entries for a specific symbol.
    /// Useful when receiving correction data or handling corporate actions.
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of cache entries invalidated</returns>
    Task<int> InvalidateSymbolDataAsync(string symbol, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cache statistics for monitoring and optimization.
/// All performance metrics use decimal types for financial precision compliance.
/// </summary>
public record CacheStatistics(
    long TotalRequests,
    long CacheHits,
    long CacheMisses,
    decimal HitRatePercent,
    long TotalItems,
    long ExpiredItems,
    long EvictedItems,
    decimal AverageGetLatencyMs,
    decimal AverageSetLatencyMs,
    long MemoryUsageBytes,
    Dictionary<string, object>? AdditionalMetrics = null)
{
    /// <summary>
    /// Cache miss rate as a percentage.
    /// </summary>
    public decimal MissRatePercent => 100m - HitRatePercent;

    /// <summary>
    /// Memory efficiency as items per MB.
    /// </summary>
    public decimal ItemsPerMb => MemoryUsageBytes > 0 ? TotalItems / (MemoryUsageBytes / 1024m / 1024m) : 0m;
}

/// <summary>
/// Cache configuration for trading-specific optimization.
/// </summary>
public interface ICacheConfiguration
{
    /// <summary>
    /// Maximum memory usage for the cache in bytes.
    /// </summary>
    long MaxMemoryBytes { get; }

    /// <summary>
    /// Default expiration time for cached items.
    /// </summary>
    TimeSpan DefaultExpiration { get; }

    /// <summary>
    /// Whether to use compression for cached values to save memory.
    /// </summary>
    bool UseCompression { get; }

    /// <summary>
    /// Maximum number of items in the cache before eviction starts.
    /// </summary>
    int MaxItems { get; }

    /// <summary>
    /// Eviction policy when cache reaches capacity.
    /// </summary>
    CacheEvictionPolicy EvictionPolicy { get; }

    /// <summary>
    /// Whether to enable cache statistics collection.
    /// May have slight performance impact but provides valuable insights.
    /// </summary>
    bool EnableStatistics { get; }
}

/// <summary>
/// Cache eviction policies for when cache reaches capacity.
/// </summary>
public enum CacheEvictionPolicy
{
    /// <summary>
    /// Least Recently Used - evict items that haven't been accessed recently.
    /// </summary>
    LRU,

    /// <summary>
    /// Least Frequently Used - evict items with lowest access frequency.
    /// </summary>
    LFU,

    /// <summary>
    /// First In, First Out - evict oldest items first.
    /// </summary>
    FIFO,

    /// <summary>
    /// Random eviction - useful for avoiding pathological cases.
    /// </summary>
    Random,

    /// <summary>
    /// Time-based eviction - evict items closest to expiration.
    /// </summary>
    TTL
}