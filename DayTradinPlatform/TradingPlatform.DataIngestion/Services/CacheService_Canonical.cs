using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Diagnostics;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Canonical;
using TradingPlatform.DataIngestion.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.DataIngestion.Services
{
    /// <summary>
    /// Canonical implementation of caching service with comprehensive logging, 
    /// error handling, and performance monitoring
    /// </summary>
    public class CacheService_Canonical : CanonicalServiceBase, ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ApiConfiguration _config;
        private readonly ConcurrentDictionary<string, HashSet<string>> _keysByMarket;
        private readonly ConcurrentDictionary<string, CacheEntryMetadata> _cacheMetadata;
        private long _hitCount;
        private long _missCount;
        private long _evictionCount;

        public CacheService_Canonical(
            IMemoryCache cache, 
            ITradingLogger logger, 
            ApiConfiguration config) 
            : base(logger, "CacheService")
        {
            ValidateNotNull(cache, nameof(cache));
            ValidateNotNull(config, nameof(config));
            
            _cache = cache;
            _config = config;
            _keysByMarket = new ConcurrentDictionary<string, HashSet<string>>();
            _cacheMetadata = new ConcurrentDictionary<string, CacheEntryMetadata>();
            
            LogInfo("CacheService initialized", new { CacheType = "MemoryCache" });
        }

        #region ICacheService Implementation

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    ValidateNotNullOrEmpty(key, nameof(key));
                    
                    var stopwatch = Stopwatch.StartNew();
                    
                    if (_cache.TryGetValue(key, out T? value))
                    {
                        Interlocked.Increment(ref _hitCount);
                        UpdateMetric("CacheHitRate", CalculateHitRate());
                        
                        // Update metadata
                        if (_cacheMetadata.TryGetValue(key, out var metadata))
                        {
                            metadata.LastAccessTime = DateTime.UtcNow;
                            metadata.AccessCount++;
                        }
                        
                        stopwatch.Stop();
                        
                        LogDebug($"Cache hit for key: {key}", new 
                        { 
                            Key = key,
                            Type = typeof(T).Name,
                            RetrievalTimeMs = stopwatch.ElapsedMilliseconds,
                            HitCount = _hitCount
                        });
                        
                        return await Task.FromResult(value);
                    }
                    
                    Interlocked.Increment(ref _missCount);
                    UpdateMetric("CacheHitRate", CalculateHitRate());
                    
                    stopwatch.Stop();
                    
                    LogDebug($"Cache miss for key: {key}", new 
                    { 
                        Key = key,
                        Type = typeof(T).Name,
                        LookupTimeMs = stopwatch.ElapsedMilliseconds,
                        MissCount = _missCount
                    });
                    
                    return await Task.FromResult<T?>(null);
                },
                "GetAsync<T>",
                incrementOperationCounter: true
            );
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
        {
            await ExecuteServiceOperationAsync(
                async () =>
                {
                    ValidateNotNullOrEmpty(key, nameof(key));
                    ValidateNotNull(value, nameof(value));
                    ValidateParameter(
                        expiration,
                        nameof(expiration),
                        exp => exp > TimeSpan.Zero && exp < TimeSpan.FromDays(1),
                        "Expiration must be between 0 and 24 hours"
                    );
                    
                    var stopwatch = Stopwatch.StartNew();
                    
                    var options = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = expiration,
                        SlidingExpiration = TimeSpan.FromMinutes(Math.Min(expiration.TotalMinutes / 2, 30)),
                        Priority = DetermineCachePriority(key, value)
                    };
                    
                    // Register eviction callback
                    options.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
                    {
                        EvictionCallback = OnCacheEviction,
                        State = key
                    });
                    
                    _cache.Set(key, value, options);
                    
                    // Track metadata
                    var metadata = new CacheEntryMetadata
                    {
                        Key = key,
                        Type = typeof(T).Name,
                        Size = EstimateObjectSize(value),
                        CreatedTime = DateTime.UtcNow,
                        ExpirationTime = DateTime.UtcNow.Add(expiration),
                        LastAccessTime = DateTime.UtcNow,
                        AccessCount = 0
                    };
                    
                    _cacheMetadata[key] = metadata;
                    
                    // Track by market if applicable
                    TrackKeyByMarket(key);
                    
                    stopwatch.Stop();
                    
                    IncrementCounter("CacheSetOperations");
                    UpdateMetric("TotalCachedItems", _cacheMetadata.Count);
                    UpdateMetric("EstimatedCacheSizeBytes", _cacheMetadata.Values.Sum(m => m.Size));
                    
                    LogInfo($"Cached value for key: {key}", new 
                    { 
                        Key = key,
                        Type = typeof(T).Name,
                        ExpirationMinutes = expiration.TotalMinutes,
                        Priority = options.Priority,
                        EstimatedSizeBytes = metadata.Size,
                        SetTimeMs = stopwatch.ElapsedMilliseconds
                    });
                    
                    return Task.CompletedTask;
                },
                "SetAsync<T>",
                incrementOperationCounter: true
            );
        }

        public async Task RemoveAsync(string key)
        {
            await ExecuteServiceOperationAsync(
                async () =>
                {
                    ValidateNotNullOrEmpty(key, nameof(key));
                    
                    var stopwatch = Stopwatch.StartNew();
                    
                    _cache.Remove(key);
                    
                    // Remove metadata
                    _cacheMetadata.TryRemove(key, out var removedMetadata);
                    
                    // Remove from market tracking
                    RemoveKeyFromMarketTracking(key);
                    
                    stopwatch.Stop();
                    
                    IncrementCounter("CacheRemoveOperations");
                    UpdateMetric("TotalCachedItems", _cacheMetadata.Count);
                    
                    LogInfo($"Removed cache entry for key: {key}", new 
                    { 
                        Key = key,
                        WasPresent = removedMetadata != null,
                        RemovalTimeMs = stopwatch.ElapsedMilliseconds,
                        RemovedMetadata = removedMetadata
                    });
                    
                    return Task.CompletedTask;
                },
                "RemoveAsync",
                incrementOperationCounter: true
            );
        }

        public async Task ClearMarketDataAsync(string marketCode)
        {
            await ExecuteServiceOperationAsync(
                async () =>
                {
                    ValidateNotNullOrEmpty(marketCode, nameof(marketCode));
                    
                    using var progress = new CanonicalProgressReporter($"Clear market data cache for {marketCode}");
                    var stopwatch = Stopwatch.StartNew();
                    
                    progress.ReportProgress(10, "Starting market data cache clear");
                    
                    if (_keysByMarket.TryGetValue(marketCode, out var marketKeys))
                    {
                        var keyCount = marketKeys.Count;
                        var processedCount = 0;
                        
                        LogInfo($"Clearing {keyCount} cache entries for market: {marketCode}");
                        
                        foreach (var key in marketKeys.ToList())
                        {
                            _cache.Remove(key);
                            _cacheMetadata.TryRemove(key, out _);
                            processedCount++;
                            
                            if (processedCount % 100 == 0)
                            {
                                progress.ReportProgress(
                                    10 + (processedCount * 80.0 / keyCount),
                                    $"Cleared {processedCount}/{keyCount} entries"
                                );
                            }
                        }
                        
                        // Clear the market key set
                        _keysByMarket.TryRemove(marketCode, out _);
                        
                        progress.ReportProgress(90, "Updating metrics");
                        
                        UpdateMetric("TotalCachedItems", _cacheMetadata.Count);
                        UpdateMetric($"MarketClearOperations_{marketCode}", 1);
                        
                        stopwatch.Stop();
                        
                        progress.Complete($"Cleared {keyCount} entries in {stopwatch.ElapsedMilliseconds}ms");
                        
                        LogInfo($"Market data cache cleared for: {marketCode}", new 
                        { 
                            MarketCode = marketCode,
                            EntriesCleared = keyCount,
                            ClearTimeMs = stopwatch.ElapsedMilliseconds
                        });
                    }
                    else
                    {
                        progress.Complete("No entries found for market");
                        LogDebug($"No cache entries found for market: {marketCode}");
                    }
                    
                    return Task.CompletedTask;
                },
                "ClearMarketDataAsync",
                incrementOperationCounter: true
            );
        }

        public async Task<bool> ExistsAsync(string key)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    ValidateNotNullOrEmpty(key, nameof(key));
                    
                    var exists = _cache.TryGetValue(key, out _);
                    
                    LogDebug($"Cache existence check for key: {key}", new 
                    { 
                        Key = key,
                        Exists = exists
                    });
                    
                    return await Task.FromResult(exists);
                },
                "ExistsAsync",
                incrementOperationCounter: true
            );
        }

        #endregion

        #region CanonicalServiceBase Implementation

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo("Initializing CacheService");
            
            // Initialize metrics
            UpdateMetric("CacheHitRate", 0);
            UpdateMetric("TotalCachedItems", 0);
            UpdateMetric("EstimatedCacheSizeBytes", 0);
            
            await Task.CompletedTask;
        }

        protected override async Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo("Starting CacheService");
            
            // Could start background cleanup tasks here if needed
            
            await Task.CompletedTask;
        }

        protected override async Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo("Stopping CacheService");
            
            // Log final statistics
            LogInfo("Cache service final statistics", new
            {
                TotalHits = _hitCount,
                TotalMisses = _missCount,
                HitRate = CalculateHitRate(),
                TotalEvictions = _evictionCount,
                FinalItemCount = _cacheMetadata.Count
            });
            
            await Task.CompletedTask;
        }

        protected override async Task<(bool IsHealthy, string Message, Dictionary<string, object>? Details)> 
            OnCheckHealthAsync(CancellationToken cancellationToken)
        {
            var isHealthy = true;
            var message = "Cache service is operational";
            
            var details = new Dictionary<string, object>
            {
                ["HitRate"] = $"{CalculateHitRate():P2}",
                ["TotalItems"] = _cacheMetadata.Count,
                ["EstimatedSizeMB"] = _cacheMetadata.Values.Sum(m => m.Size) / (1024.0 * 1024.0),
                ["Markets"] = _keysByMarket.Count,
                ["TotalHits"] = _hitCount,
                ["TotalMisses"] = _missCount,
                ["TotalEvictions"] = _evictionCount
            };
            
            // Check if cache is performing poorly
            var hitRate = CalculateHitRate();
            if (hitRate < 0.5 && (_hitCount + _missCount) > 1000)
            {
                isHealthy = false;
                message = "Cache hit rate is below acceptable threshold";
            }
            
            return await Task.FromResult((isHealthy, message, details));
        }

        #endregion

        #region Private Methods

        private void OnCacheEviction(object key, object value, EvictionReason reason, object state)
        {
            Interlocked.Increment(ref _evictionCount);
            
            var keyString = key?.ToString() ?? "unknown";
            _cacheMetadata.TryRemove(keyString, out _);
            RemoveKeyFromMarketTracking(keyString);
            
            LogDebug($"Cache entry evicted: {keyString}", new
            {
                Key = keyString,
                Reason = reason.ToString(),
                TotalEvictions = _evictionCount
            });
            
            UpdateMetric("TotalEvictions", _evictionCount);
        }

        private CacheItemPriority DetermineCachePriority(string key, object value)
        {
            // Market quotes and real-time data get higher priority
            if (key.Contains("quote", StringComparison.OrdinalIgnoreCase) || 
                key.Contains("realtime", StringComparison.OrdinalIgnoreCase))
            {
                return CacheItemPriority.High;
            }
            
            // Historical data and company info get normal priority
            if (key.Contains("historical", StringComparison.OrdinalIgnoreCase) || 
                key.Contains("company", StringComparison.OrdinalIgnoreCase))
            {
                return CacheItemPriority.Normal;
            }
            
            // Everything else gets low priority
            return CacheItemPriority.Low;
        }

        private long EstimateObjectSize(object obj)
        {
            // This is a simplified estimation
            // In production, we'd use a more sophisticated size calculation
            if (obj == null) return 0;
            
            var json = System.Text.Json.JsonSerializer.Serialize(obj);
            return System.Text.Encoding.UTF8.GetByteCount(json);
        }

        private void TrackKeyByMarket(string key)
        {
            // Extract market code from key (assumes format like "MARKET:AAPL:quote")
            var parts = key.Split(':');
            if (parts.Length >= 2)
            {
                var marketCode = parts[0];
                _keysByMarket.AddOrUpdate(
                    marketCode,
                    new HashSet<string> { key },
                    (_, existing) =>
                    {
                        lock (existing)
                        {
                            existing.Add(key);
                            return existing;
                        }
                    });
            }
        }

        private void RemoveKeyFromMarketTracking(string key)
        {
            var parts = key.Split(':');
            if (parts.Length >= 2)
            {
                var marketCode = parts[0];
                if (_keysByMarket.TryGetValue(marketCode, out var marketKeys))
                {
                    lock (marketKeys)
                    {
                        marketKeys.Remove(key);
                        if (marketKeys.Count == 0)
                        {
                            _keysByMarket.TryRemove(marketCode, out _);
                        }
                    }
                }
            }
        }

        private double CalculateHitRate()
        {
            var total = _hitCount + _missCount;
            return total > 0 ? (double)_hitCount / total : 0;
        }

        #endregion

        #region Helper Classes

        private class CacheEntryMetadata
        {
            public string Key { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public long Size { get; set; }
            public DateTime CreatedTime { get; set; }
            public DateTime ExpirationTime { get; set; }
            public DateTime LastAccessTime { get; set; }
            public long AccessCount { get; set; }
        }

        #endregion
    }
}