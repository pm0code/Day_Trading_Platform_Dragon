using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical base class for all data providers in the trading platform.
    /// Provides standardized patterns for data fetching, caching, rate limiting, and error handling.
    /// </summary>
    public abstract class CanonicalProvider<TData> : CanonicalServiceBase where TData : class
    {
        #region Provider Configuration

        protected virtual int DefaultCacheDurationMinutes => 5;
        protected virtual int MaxRetryAttempts => 3;
        protected virtual int RetryDelayMilliseconds => 1000;
        protected virtual int RateLimitRequestsPerMinute => 60;
        protected virtual bool EnableCaching => true;
        protected virtual bool EnableRateLimiting => true;

        #endregion

        #region Provider Infrastructure

        private readonly IMemoryCache? _cache;
        private readonly SemaphoreSlim _rateLimitSemaphore;
        private readonly ConcurrentQueue<DateTime> _requestTimestamps;
        private readonly Timer _rateLimitResetTimer;
        private readonly object _rateLimitLock = new object();
        private int _totalRequests;
        private int _failedRequests;
        private int _cacheHits;
        private int _cacheMisses;

        #endregion

        #region Constructor

        protected CanonicalProvider(
            ITradingLogger logger,
            string providerName,
            IMemoryCache? cache = null) 
            : base(logger, providerName)
        {
            _cache = cache;
            _rateLimitSemaphore = new SemaphoreSlim(RateLimitRequestsPerMinute, RateLimitRequestsPerMinute);
            _requestTimestamps = new ConcurrentQueue<DateTime>();
            
            // Reset rate limit every minute
            _rateLimitResetTimer = new Timer(
                ResetRateLimit,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(1));

            LogMethodEntry(new { providerName, cacheEnabled = EnableCaching, rateLimitEnabled = EnableRateLimiting });
        }

        #endregion

        #region Canonical Provider Methods

        /// <summary>
        /// Fetches data with canonical patterns for caching, rate limiting, and error handling
        /// </summary>
        protected async Task<TradingResult<TData>> FetchDataAsync(
            string dataKey,
            Func<Task<TData>> dataFetcher,
            CachePolicy? cachePolicy = null,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string methodName = "")
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                // Check cache first
                if (EnableCaching && _cache != null && TryGetFromCache(dataKey, out TData? cachedData))
                {
                    Interlocked.Increment(ref _cacheHits);
                    UpdateMetric("CacheHitRate", (double)_cacheHits / (_cacheHits + _cacheMisses));
                    LogDebug($"Cache hit for key: {dataKey}");
                    return TradingResult<TData>.Success(cachedData!);
                }

                Interlocked.Increment(ref _cacheMisses);

                // Apply rate limiting
                if (EnableRateLimiting)
                {
                    await ApplyRateLimitAsync(cancellationToken);
                }

                // Track request
                Interlocked.Increment(ref _totalRequests);
                _requestTimestamps.Enqueue(DateTime.UtcNow);

                // Fetch data with retry logic
                var result = await FetchWithRetryAsync(dataFetcher, cancellationToken);

                if (result.IsSuccess && EnableCaching && _cache != null)
                {
                    // Cache successful result
                    var policy = cachePolicy ?? GetDefaultCachePolicy();
                    AddToCache(dataKey, result.Value!, policy);
                }
                else if (!result.IsSuccess)
                {
                    Interlocked.Increment(ref _failedRequests);
                    UpdateMetric("FailureRate", (double)_failedRequests / _totalRequests);
                }

                UpdateProviderMetrics();
                return result;

            }, $"FetchData for key: {dataKey}", 
               "Data fetch operation failed",
               "Check network connectivity and API credentials",
               methodName);
        }

        /// <summary>
        /// Fetches multiple data items with canonical batching patterns
        /// </summary>
        protected async Task<TradingResult<IEnumerable<TData>>> FetchBatchDataAsync(
            IEnumerable<string> dataKeys,
            Func<IEnumerable<string>, Task<IEnumerable<TData>>> batchFetcher,
            int maxBatchSize = 100,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string methodName = "")
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                var keys = dataKeys.ToList();
                var results = new List<TData>();
                var errors = new List<TradingError>();

                // Process in batches
                for (int i = 0; i < keys.Count; i += maxBatchSize)
                {
                    var batch = keys.Skip(i).Take(maxBatchSize).ToList();
                    
                    try
                    {
                        var batchResult = await FetchWithRetryAsync(
                            async () => await batchFetcher(batch),
                            cancellationToken);

                        if (batchResult.IsSuccess)
                        {
                            results.AddRange(batchResult.Value!);
                        }
                        else
                        {
                            errors.Add(batchResult.Error!);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new TradingError(
                            TradingError.ErrorCodes.ExternalServiceError,
                            $"Batch fetch failed for batch starting at index {i}",
                            ex));
                    }

                    // Add delay between batches to avoid overwhelming the service
                    if (i + maxBatchSize < keys.Count)
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                }

                if (errors.Any())
                {
                    LogWarning($"Batch fetch completed with {errors.Count} errors out of {keys.Count} items");
                    
                    if (results.Count == 0)
                    {
                        return TradingResult<IEnumerable<TData>>.Failure(
                            TradingError.ErrorCodes.ExternalServiceError,
                            $"All batch fetches failed. First error: {errors.First().Message}");
                    }
                }

                return TradingResult<IEnumerable<TData>>.Success(results);

            }, $"BatchFetch for {dataKeys.Count()} items",
               "Batch data fetch operation failed",
               "Check network connectivity and consider reducing batch size",
               methodName);
        }

        /// <summary>
        /// Validates provider configuration and connectivity
        /// </summary>
        public virtual async Task<TradingResult> ValidateProviderAsync(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                // Validate configuration
                var configResult = await ValidateConfigurationAsync();
                if (!configResult.IsSuccess)
                {
                    return configResult;
                }

                // Test connectivity
                var connectivityResult = await TestConnectivityAsync(cancellationToken);
                if (!connectivityResult.IsSuccess)
                {
                    return connectivityResult;
                }

                // Validate authentication
                var authResult = await ValidateAuthenticationAsync(cancellationToken);
                if (!authResult.IsSuccess)
                {
                    return authResult;
                }

                LogInfo($"{ComponentName} validation successful");
                return TradingResult.Success();

            }, "Provider validation",
               "Provider validation failed",
               "Check configuration, network connectivity, and authentication credentials");
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Validates provider-specific configuration
        /// </summary>
        protected abstract Task<TradingResult> ValidateConfigurationAsync();

        /// <summary>
        /// Tests connectivity to the data source
        /// </summary>
        protected abstract Task<TradingResult> TestConnectivityAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Validates authentication credentials
        /// </summary>
        protected abstract Task<TradingResult> ValidateAuthenticationAsync(CancellationToken cancellationToken);

        #endregion

        #region Rate Limiting

        private async Task ApplyRateLimitAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            
            await _rateLimitSemaphore.WaitAsync(cancellationToken);
            
            try
            {
                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    LogWarning($"Rate limit wait time: {stopwatch.ElapsedMilliseconds}ms",
                        impact: "Increased latency due to rate limiting");
                }
            }
            finally
            {
                // Semaphore is released by the timer
            }
        }

        private void ResetRateLimit(object? state)
        {
            lock (_rateLimitLock)
            {
                // Clear old timestamps
                var cutoff = DateTime.UtcNow.AddMinutes(-1);
                while (_requestTimestamps.TryPeek(out var timestamp) && timestamp < cutoff)
                {
                    _requestTimestamps.TryDequeue(out _);
                }

                // Reset semaphore to allow new requests
                var currentCount = _rateLimitSemaphore.CurrentCount;
                var releaseCount = Math.Min(RateLimitRequestsPerMinute - currentCount, RateLimitRequestsPerMinute);
                
                if (releaseCount > 0)
                {
                    _rateLimitSemaphore.Release(releaseCount);
                }
            }
        }

        #endregion

        #region Caching

        private bool TryGetFromCache(string key, out TData? data)
        {
            if (_cache == null)
            {
                data = null;
                return false;
            }

            return _cache.TryGetValue(GetCacheKey(key), out data);
        }

        private void AddToCache(string key, TData data, CachePolicy policy)
        {
            if (_cache == null) return;

            var cacheKey = GetCacheKey(key);
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = policy.AbsoluteExpiration,
                SlidingExpiration = policy.SlidingExpiration,
                Priority = policy.Priority
            };

            _cache.Set(cacheKey, data, cacheOptions);
            LogDebug($"Added to cache: {key} with expiration: {policy.AbsoluteExpiration}");
        }

        private string GetCacheKey(string key) => $"{ComponentName}:{key}";

        private CachePolicy GetDefaultCachePolicy() => new CachePolicy
        {
            AbsoluteExpiration = TimeSpan.FromMinutes(DefaultCacheDurationMinutes),
            Priority = CacheItemPriority.Normal
        };

        #endregion

        #region Retry Logic

        private async Task<TradingResult<T>> FetchWithRetryAsync<T>(
            Func<Task<T>> fetcher,
            CancellationToken cancellationToken)
        {
            var attempts = 0;
            var exceptions = new List<Exception>();

            while (attempts < MaxRetryAttempts)
            {
                attempts++;
                
                try
                {
                    var result = await fetcher();
                    return TradingResult<T>.Success(result);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    
                    if (attempts < MaxRetryAttempts)
                    {
                        var delay = RetryDelayMilliseconds * attempts;
                        LogWarning($"Attempt {attempts} failed, retrying in {delay}ms", 
                            impact: $"Temporary failure in {ComponentName}",
                            troubleshooting: ex.Message);
                        
                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }

            var aggregateEx = new AggregateException(
                $"Failed after {MaxRetryAttempts} attempts", 
                exceptions);

            return TradingResult<T>.Failure(
                TradingError.ErrorCodes.ExternalServiceError,
                $"{ComponentName} failed after {MaxRetryAttempts} attempts",
                aggregateEx);
        }

        #endregion

        #region Metrics

        private void UpdateProviderMetrics()
        {
            UpdateMetric("TotalRequests", _totalRequests);
            UpdateMetric("FailedRequests", _failedRequests);
            UpdateMetric("CacheHits", _cacheHits);
            UpdateMetric("CacheMisses", _cacheMisses);
            UpdateMetric("RequestsPerMinute", _requestTimestamps.Count);
            
            if (_totalRequests > 0)
            {
                UpdateMetric("SuccessRate", (double)(_totalRequests - _failedRequests) / _totalRequests);
            }
            
            if ((_cacheHits + _cacheMisses) > 0)
            {
                UpdateMetric("CacheHitRate", (double)_cacheHits / (_cacheHits + _cacheMisses));
            }
        }

        public override IReadOnlyDictionary<string, object> GetMetrics()
        {
            var metrics = base.GetMetrics().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            // Add provider-specific metrics
            metrics["Provider.CacheEnabled"] = EnableCaching;
            metrics["Provider.RateLimitEnabled"] = EnableRateLimiting;
            metrics["Provider.RateLimitPerMinute"] = RateLimitRequestsPerMinute;
            metrics["Provider.CurrentRequestsInWindow"] = _requestTimestamps.Count;
            
            return metrics;
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _rateLimitResetTimer?.Dispose();
                _rateLimitSemaphore?.Dispose();
            }
            
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// Cache policy configuration
    /// </summary>
    public class CachePolicy
    {
        public TimeSpan? AbsoluteExpiration { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }
        public CacheItemPriority Priority { get; set; } = CacheItemPriority.Normal;
    }
}