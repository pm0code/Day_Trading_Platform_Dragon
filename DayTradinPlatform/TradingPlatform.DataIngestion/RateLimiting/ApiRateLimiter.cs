// d:\Projects\C#.Net\DayTradingPlatform-P\DayTradinPlatform\ApiRateLimiter.cs
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TradingPlatform.DataIngestion.Interfaces;
using TradingPlatform.DataIngestion.Models;

namespace TradingPlatform.DataIngestion.RateLimiting
{
    public class ApiRateLimiter : IRateLimiter
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<ApiRateLimiter> _logger;
        private readonly ApiConfiguration _config;
        private readonly ConcurrentDictionary<string, DateTime> _lastRequestTimes;
        private readonly ConcurrentDictionary<string, int> _requestCounts;

        public ApiRateLimiter(IMemoryCache cache, ILogger<ApiRateLimiter> logger, ApiConfiguration config)
        {
            _cache = cache;
            _logger = logger;
            _config = config;
            _lastRequestTimes = new ConcurrentDictionary<string, DateTime>();
            _requestCounts = new ConcurrentDictionary<string, int>();
        }

        public async Task<bool> CanMakeRequestAsync(string provider)
        {
            var cacheKey = $"rate_limit_{provider}";
            var requestCount = _cache.Get<int>(cacheKey);

            var limit = provider.ToLower() switch
            {
                "alphavantage" => _config.AlphaVantage.RequestsPerMinute,
                "finnhub" => _config.Finnhub.RequestsPerMinute,
                _ => 60
            };

            _logger.LogDebug($"Rate limit check for {provider}: {requestCount}/{limit} requests");

            return requestCount < limit;
        }

        public async Task RecordRequestAsync(string provider)
        {
            var cacheKey = $"rate_limit_{provider}";
            var currentCount = _cache.Get<int>(cacheKey);

            _cache.Set(cacheKey, currentCount + 1, TimeSpan.FromMinutes(1));
            _lastRequestTimes.AddOrUpdate(provider, DateTime.UtcNow, (k, v) => DateTime.UtcNow);

            _logger.LogTrace($"Recorded API request for {provider}. Count: {currentCount + 1}");
        }

        public async Task<TimeSpan> GetWaitTimeAsync(string provider)
        {
            if (_lastRequestTimes.TryGetValue(provider, out var lastRequest))
            {
                var minInterval = provider.ToLower() switch
                {
                    "alphavantage" => TimeSpan.FromSeconds(12), // 5 requests per minute
                    "finnhub" => TimeSpan.FromSeconds(1), // 60 requests per minute
                    _ => TimeSpan.FromSeconds(1)
                };

                var elapsed = DateTime.UtcNow - lastRequest;
                return elapsed < minInterval ? minInterval - elapsed : TimeSpan.Zero;
            }

            return TimeSpan.Zero;
        }

        public async Task<int> GetRemainingRequestsAsync(string provider, TimeSpan window)
        {
            var cacheKey = $"rate_limit_{provider}";
            var currentCount = _cache.Get<int>(cacheKey);

            var limit = provider.ToLower() switch
            {
                "alphavantage" => _config.AlphaVantage.RequestsPerMinute,
                "finnhub" => _config.Finnhub.RequestsPerMinute,
                _ => 60
            };

            return Math.Max(0, limit - currentCount);
        }

        public async Task ResetLimitsAsync(string provider)
        {
            var cacheKey = $"rate_limit_{provider}";
            _cache.Remove(cacheKey);
            _lastRequestTimes.TryRemove(provider, out _);

            _logger.LogInformation($"Rate limits reset for {provider}");
        }

        public async Task<bool> IsProviderAvailableAsync(string provider)
        {
            var canMakeRequest = await CanMakeRequestAsync(provider);
            var waitTime = await GetWaitTimeAsync(provider);

            return canMakeRequest && waitTime == TimeSpan.Zero;
        }

        public async Task WaitForPermitAsync()
        {
            // Wait until a permit is available
            while (!TryAcquirePermit())
            {
                await Task.Delay(100); // Wait 100ms before trying again
            }
        }

        public bool TryAcquirePermit()
        {
            // Use the default provider for generic permit requests
            return TryAcquirePermit("default");
        }

        public async Task<bool> TryAcquirePermit(string provider)
        {
            return await CanMakeRequestAsync(provider);
        }

        public void RecordRequest()
        {
            // Record request for default provider
            RecordRequestAsync("default").Wait();
        }

        public void RecordFailure(Exception exception)
        {
            _logger.LogWarning($"API request failed: {exception.Message}");
            // For now, just log the failure. Could implement backoff logic here.
        }

        public bool IsLimitReached()
        {
            return IsLimitReached("default");
        }

        public bool IsLimitReached(string provider)
        {
            return !CanMakeRequestAsync(provider).Result;
        }

        public int GetRemainingCalls()
        {
            return GetRemainingCalls("default");
        }

        public int GetRemainingCalls(string provider)
        {
            return GetRemainingRequestsAsync(provider).Result;
        }

        public TimeSpan GetTimeUntilNextPermit()
        {
            return GetRequestWindowAsync("default").Result;
        }

        public async Task<TimeSpan> GetRequestWindowAsync(string provider)
        {
            return await GetWaitTimeAsync(provider);
        }
    }
}
// 85 lines
