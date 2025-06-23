using System.Collections.Concurrent;
using System.Text.Json;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.MarketData.Services;

/// <summary>
/// High-performance in-memory and Redis-backed market data cache
/// Optimized for sub-millisecond access patterns in trading applications
/// </summary>
public class MarketDataCache : IMarketDataCache
{
    private readonly ITradingLogger _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _memoryCache;
    private readonly Timer _cleanupTimer;
    private readonly object _statsLock = new();

    // Cache statistics
    private long _hitCount;
    private long _missCount;
    private long _memoryUsageBytes;

    public MarketDataCache(ITradingLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = new ConcurrentDictionary<string, CacheEntry>();
        
        // Cleanup expired entries every 30 seconds
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task<MarketData?> GetAsync(string symbol)
    {
        try
        {
            // Try memory cache first (sub-microsecond access)
            var memoryKey = GetMemoryKey(symbol);
            if (_memoryCache.TryGetValue(memoryKey, out var cacheEntry))
            {
                if (!cacheEntry.IsExpired)
                {
                    Interlocked.Increment(ref _hitCount);
                    TradingLogOrchestrator.Instance.LogInfo("Memory cache hit for {Symbol}", symbol);
                    return cacheEntry.Data;
                }
                else
                {
                    // Remove expired entry
                    _memoryCache.TryRemove(memoryKey, out _);
                }
            }

            // For MVP, use memory-only cache (Redis integration can be added later)
            // This provides sub-millisecond access for active trading

            Interlocked.Increment(ref _missCount);
            TradingLogOrchestrator.Instance.LogInfo("Cache miss for {Symbol}", symbol);
            return null;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error getting cached data for {Symbol}", symbol, ex);
            Interlocked.Increment(ref _missCount);
            return null;
        }
    }

    public async Task SetAsync(string symbol, MarketData data, TimeSpan? ttl = null)
    {
        try
        {
            var expiry = ttl ?? TimeSpan.FromSeconds(5); // Default 5-second TTL
            var expiryTime = DateTime.UtcNow.Add(expiry);

            // Set in memory cache immediately
            var memoryKey = GetMemoryKey(symbol);
            var cacheEntry = new CacheEntry(data, expiryTime);
            _memoryCache.AddOrUpdate(memoryKey, cacheEntry, (k, v) => cacheEntry);

            // Update memory usage estimate
            var serializedData = JsonSerializer.Serialize(data);
            var dataSize = System.Text.Encoding.UTF8.GetByteCount(serializedData);
            Interlocked.Add(ref _memoryUsageBytes, dataSize);

            TradingLogOrchestrator.Instance.LogInfo("Cached market data for {Symbol} with TTL {TTL}ms", 
                symbol, expiry.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error caching data for {Symbol}", symbol, ex);
        }
    }

    public async Task InvalidateAsync(string symbol)
    {
        try
        {
            // Remove from memory cache
            var memoryKey = GetMemoryKey(symbol);
            _memoryCache.TryRemove(memoryKey, out _);

            // For MVP, only memory cache needs invalidation

            TradingLogOrchestrator.Instance.LogInfo("Invalidated cache for {Symbol}", symbol);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error invalidating cache for {Symbol}", symbol, ex);
        }
    }

    public async Task<CacheStats> GetStatsAsync()
    {
        await Task.CompletedTask;
        
        lock (_statsLock)
        {
            var totalRequests = _hitCount + _missCount;
            var hitRate = totalRequests > 0 ? (double)_hitCount / totalRequests : 0.0;
            
            // Calculate average age of cached entries
            var now = DateTime.UtcNow;
            var averageAge = _memoryCache.Values.Count > 0 ?
                TimeSpan.FromTicks((long)_memoryCache.Values.Average(e => (now - e.CreatedAt).Ticks)) :
                TimeSpan.Zero;

            return new CacheStats(
                _hitCount,
                _missCount,
                hitRate,
                _memoryCache.Count,
                _memoryUsageBytes,
                averageAge);
        }
    }

    // Private helper methods
    private static string GetMemoryKey(string symbol) => $"memory:marketdata:{symbol.ToUpperInvariant()}";

    private void CleanupExpiredEntries(object? state)
    {
        try
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _memoryCache
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToArray();

            foreach (var key in expiredKeys)
            {
                _memoryCache.TryRemove(key, out _);
            }

            if (expiredKeys.Length > 0)
            {
                TradingLogOrchestrator.Instance.LogInfo("Cleaned up {ExpiredCount} expired cache entries", expiredKeys.Length);
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error during cache cleanup", ex);
        }
    }

    // Cache entry wrapper
    private class CacheEntry
    {
        public MarketData Data { get; }
        public DateTime ExpiryTime { get; }
        public DateTime CreatedAt { get; }
        public bool IsExpired => DateTime.UtcNow > ExpiryTime;

        public CacheEntry(MarketData data, DateTime expiryTime)
        {
            Data = data;
            ExpiryTime = expiryTime;
            CreatedAt = DateTime.UtcNow;
        }
    }
}