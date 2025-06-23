// d:\Projects\C#.Net\DayTradingPlatform-P\DayTradinPlatform\CacheService.cs
using Microsoft.Extensions.Caching.Memory;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.DataIngestion.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.DataIngestion.Services
{
    public interface ICacheService
    {
        Task<T> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class;
        Task RemoveAsync(string key);
        Task ClearMarketDataAsync(string marketCode);
        Task<bool> ExistsAsync(string key);
    }

    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ITradingLogger _logger;
        private readonly ApiConfiguration _config;

        public CacheService(IMemoryCache cache, ITradingLogger logger, ApiConfiguration config)
        {
            _cache = cache;
            _logger = logger;
            _config = config;
        }

        public async Task<T> GetAsync<T>(string key) where T : class
        {
            if (_cache.TryGetValue(key, out T value))
            {
                TradingLogOrchestrator.Instance.LogInfo($"Cache hit for key: {key}");
                return value;
            }

            TradingLogOrchestrator.Instance.LogInfo($"Cache miss for key: {key}");
            return null;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
        {
            if (value == null)
            {
                TradingLogOrchestrator.Instance.LogWarning($"Attempted to cache null value for key: {key}");
                return;
            }

            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration,
                SlidingExpiration = TimeSpan.FromMinutes(Math.Min(expiration.TotalMinutes / 2, 30)),
                Priority = CacheItemPriority.Normal
            };

            _cache.Set(key, value, options);
            TradingLogOrchestrator.Instance.LogInfo($"Cached value for key: {key}, expires in: {expiration}");
        }

        public async Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            TradingLogOrchestrator.Instance.LogInfo($"Removed cache entry for key: {key}");
        }

        public async Task ClearMarketDataAsync(string marketCode)
        {
            // In a real implementation, we'd need to track keys by market
            // For MVP, we'll implement a simple approach
            TradingLogOrchestrator.Instance.LogInfo($"Clearing market data cache for: {marketCode}");

            // This is a simplified implementation
            // In production, we'd maintain a key registry by market
        }

        public async Task<bool> ExistsAsync(string key)
        {
            var exists = _cache.TryGetValue(key, out _);
            TradingLogOrchestrator.Instance.LogInfo($"Cache existence check for key: {key} = {exists}");
            return exists;
        }
    }
}
// 73 lines
