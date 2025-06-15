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

        public Task WaitForPermitAsync()
        {
            throw new NotImplementedException();
        }

        public bool TryAcquire()
        {
            throw new NotImplementedException();
        }

        public TimeSpan GetTimeUntilNextPermit()
        {
            throw new NotImplementedException();
        }
    }
}
// 85 lines
