// File: TradingPlatform.DataIngestion\RateLimiting\ApiRateLimiter.cs
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.DataIngestion.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.DataIngestion.RateLimiting
{
    public class ApiRateLimiter : IRateLimiter
    {
        private readonly IMemoryCache _cache;
        private readonly ITradingLogger _logger;
        private readonly ApiConfiguration _config;
        private readonly ConcurrentDictionary<string, DateTime> _lastRequestTimes;
        private readonly ConcurrentDictionary<string, int> _requestCounts;
        private readonly ConcurrentDictionary<string, RateLimitingStatistics> _statistics;
        private readonly SemaphoreSlim _semaphore;
        
        // Configuration
        private int _requestsPerMinute = 60;
        private int _requestsPerDay = 500;
        private readonly string _defaultProvider = "default";
        
        // Events
        public event EventHandler<RateLimitReachedEventArgs> RateLimitReached;
        public event EventHandler<RateLimitStatusChangedEventArgs> StatusChanged;
        public event EventHandler<QuotaThresholdEventArgs> QuotaThresholdReached;

        public ApiRateLimiter(IMemoryCache cache, ITradingLogger logger, ApiConfiguration config)
        {
            _cache = cache;
            _logger = logger;
            _config = config;
            _lastRequestTimes = new ConcurrentDictionary<string, DateTime>();
            _requestCounts = new ConcurrentDictionary<string, int>();
            _statistics = new ConcurrentDictionary<string, RateLimitingStatistics>();
            _semaphore = new SemaphoreSlim(1, 1);
        }

        public async Task<bool> CanMakeRequestAsync(string provider)
        {
            var cacheKey = GetCacheKey(provider);
            var requestCount = _cache.Get<int>(cacheKey);

            var limit = provider.ToLower() switch
            {
                "alphavantage" => 5, // Free tier: 5 requests per minute
                "finnhub" => 60, // Free tier: 60 requests per minute
                _ => 60
            };

            return requestCount < limit;
        }

        public async Task RecordRequestAsync(string provider)
        {
            var cacheKey = GetCacheKey(provider);
            var currentCount = _cache.Get<int>(cacheKey);

            _cache.Set(cacheKey, currentCount + 1, TimeSpan.FromMinutes(1));
            _lastRequestTimes.AddOrUpdate(provider, DateTime.UtcNow, (k, v) => DateTime.UtcNow);
            
            // Update statistics
            var stats = GetOrCreateStatistics(provider);
            stats.TotalRequests++;
            stats.CurrentRpm = currentCount + 1;
            
            await Task.CompletedTask;
        }

        public async Task<TimeSpan> GetWaitTimeAsync(string provider)
        {
            if (_lastRequestTimes.TryGetValue(provider, out var lastRequest))
            {
                var minInterval = provider.ToLower() switch
                {
                    _ => TimeSpan.FromSeconds(1)
                };

                var elapsed = DateTime.UtcNow - lastRequest;
                return elapsed < minInterval ? minInterval - elapsed : TimeSpan.Zero;
            }

            return TimeSpan.Zero;
        }

        public async Task<int> GetRemainingRequestsAsync(string provider, TimeSpan window)
        {
            var cacheKey = GetCacheKey(provider);
            var currentCount = _cache.Get<int>(cacheKey);

            var limit = provider.ToLower() switch
            {
                "alphavantage" => 5,
                "finnhub" => 60,
                _ => 60
            };

            return await Task.FromResult(Math.Max(0, limit - currentCount));
        }

        public async Task ResetLimitsAsync(string provider)
        {
            var cacheKey = GetCacheKey(provider);
            _cache.Remove(cacheKey);
            _lastRequestTimes.TryRemove(provider, out _);
            
            await Task.CompletedTask;
        }

        public async Task<bool> IsProviderAvailableAsync(string provider)
        {
            var canMakeRequest = await CanMakeRequestAsync(provider);
            var waitTime = await GetWaitTimeAsync(provider);

            return canMakeRequest && waitTime == TimeSpan.Zero;
        }

        public async Task WaitForPermitAsync()
        {
            await WaitForPermitAsync(_defaultProvider);
        }
        
        public async Task WaitForPermitAsync(string provider)
        {
            var stats = GetOrCreateStatistics(provider);
            var startTime = DateTime.UtcNow;
            
            while (!TryAcquirePermit())
            {
                stats.RateLimitedRequests++;
                await Task.Delay(100);
            }
            
            var delay = (DateTime.UtcNow - startTime).TotalMilliseconds;
            stats.AverageDelayMs = (stats.AverageDelayMs * (stats.RateLimitedRequests - 1) + delay) / stats.RateLimitedRequests;
            stats.MaxDelayMs = Math.Max(stats.MaxDelayMs, delay);
        }

        public bool TryAcquirePermit()
        {
            return TryAcquirePermit(_defaultProvider);
        }
        
        public bool TryAcquirePermit(string provider)
        {
            return CanMakeRequestAsync(provider).Result;
        }

        public void RecordRequest()
        {
            RecordRequestAsync(_defaultProvider).Wait();
        }

        public void RecordFailure(Exception exception)
        {
            var stats = GetOrCreateStatistics(_defaultProvider);
            stats.FailedRequests++;
            
            _logger.LogError($"Rate limiter recorded failure: {exception.Message}", exception);
        }

        public bool IsLimitReached()
        {
            return IsLimitReached(_defaultProvider);
        }
        
        public bool IsLimitReached(string provider)
        {
            return !CanMakeRequestAsync(provider).Result;
        }

        public int GetRemainingCalls()
        {
            return GetRemainingCalls(_defaultProvider);
        }
        
        public int GetRemainingCalls(string provider)
        {
            return GetRemainingRequestsAsync(provider, TimeSpan.FromMinutes(1)).Result;
        }
        
        public int GetUsedCalls()
        {
            var cacheKey = GetCacheKey(_defaultProvider);
            return _cache.Get<int>(cacheKey);
        }
        
        public int GetMaxCalls()
        {
            return _requestsPerMinute;
        }
        
        public DateTime GetResetTime()
        {
            // Rate limits reset every minute
            return DateTime.UtcNow.AddMinutes(1).AddSeconds(-DateTime.UtcNow.Second);
        }
        
        public TimeSpan GetRecommendedDelay()
        {
            if (IsLimitReached())
            {
                return GetResetTime() - DateTime.UtcNow;
            }
            return TimeSpan.Zero;
        }
        
        public void UpdateLimits(int requestsPerMinute, int requestsPerDay = -1)
        {
            _requestsPerMinute = requestsPerMinute;
            if (requestsPerDay > 0)
            {
                _requestsPerDay = requestsPerDay;
            }
            
            _logger.LogInfo($"Rate limits updated: {requestsPerMinute}/min, {requestsPerDay}/day");
        }
        
        public void Reset()
        {
            _cache.Remove(GetCacheKey(_defaultProvider));
            _lastRequestTimes.Clear();
            _requestCounts.Clear();
            _statistics.Clear();
            
            _logger.LogInfo("Rate limiter reset completed");
        }
        
        public RateLimitingStatistics GetStatistics()
        {
            return GetOrCreateStatistics(_defaultProvider);
        }

        public async Task<TimeSpan> GetRequestWindowAsync(string provider)
        {
            return await GetWaitTimeAsync(provider);
        }
        
        // Helper methods
        private string GetCacheKey(string provider)
        {
            return $"rate_limit_{provider}_{DateTime.UtcNow:yyyyMMddHHmm}";
        }
        
        private RateLimitingStatistics GetOrCreateStatistics(string provider)
        {
            return _statistics.GetOrAdd(provider, p => new RateLimitingStatistics
            {
                StatisticsStartTime = DateTime.UtcNow
            });
        }
    }
}
