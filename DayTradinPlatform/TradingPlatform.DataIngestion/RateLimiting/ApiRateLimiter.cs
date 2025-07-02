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
    /// <summary>
    /// Rate limiter with sliding window, proper async handling, and jitter to prevent thundering herd
    /// </summary>
    public class ApiRateLimiter : IRateLimiter
    {
        private readonly IMemoryCache _cache;
        private readonly ITradingLogger _logger;
        private readonly ApiConfiguration _config;
        private readonly ConcurrentDictionary<string, SlidingWindow> _slidingWindows;
        private readonly ConcurrentDictionary<string, RateLimitingStatistics> _statistics;
        private readonly SemaphoreSlim _semaphore;
        private readonly Random _random = new Random();

        // Configuration
        private int _requestsPerMinute = 60;
        private int _requestsPerDay = 500;
        private readonly string _defaultProvider = "default";

        // Events
        public event EventHandler<RateLimitReachedEventArgs>? RateLimitReached;
        public event EventHandler<RateLimitStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<QuotaThresholdEventArgs>? QuotaThresholdReached;

        public ApiRateLimiter(IMemoryCache cache, ITradingLogger logger, ApiConfiguration config)
        {
            _cache = cache;
            _logger = logger;
            _config = config;
            _slidingWindows = new ConcurrentDictionary<string, SlidingWindow>();
            _statistics = new ConcurrentDictionary<string, RateLimitingStatistics>();
            _semaphore = new SemaphoreSlim(1, 1);
        }

        public ApiRateLimiter(IMemoryCache cache)
        {
            _cache = cache;
            _logger = new ConsoleLogger(); // Fallback logger
            _config = new ApiConfiguration();
            _slidingWindows = new ConcurrentDictionary<string, SlidingWindow>();
            _statistics = new ConcurrentDictionary<string, RateLimitingStatistics>();
            _semaphore = new SemaphoreSlim(1, 1);
        }

        public Task<bool> CanMakeRequestAsync(string provider)
        {
            var window = GetOrCreateWindow(provider);
            var limit = GetRateLimitForProvider(provider);
            
            window.CleanOldEntries();
            return Task.FromResult(window.GetRequestCount() < limit);
        }

        public async Task<bool> TryAcquireAsync(string provider, int requestsPerMinute)
        {
            return await CanMakeRequestAsync(provider);
        }

        public async Task RecordRequestAsync(string provider)
        {
            var window = GetOrCreateWindow(provider);
            window.RecordRequest();

            // Update statistics
            var stats = GetOrCreateStatistics(provider);
            stats.TotalRequests++;
            stats.CurrentRpm = window.GetRequestCount();

            // Check if we've hit the limit
            var limit = GetRateLimitForProvider(provider);
            if (stats.CurrentRpm >= limit)
            {
                stats.RateLimitedRequests++;
                RateLimitReached?.Invoke(this, new RateLimitReachedEventArgs 
                { 
                    Provider = provider, 
                    CurrentCount = stats.CurrentRpm,
                    Limit = limit 
                });
            }

            await Task.CompletedTask;
        }

        public async Task<TimeSpan> GetWaitTimeAsync(string provider)
        {
            var window = GetOrCreateWindow(provider);
            var limit = GetRateLimitForProvider(provider);
            
            window.CleanOldEntries();
            var currentCount = window.GetRequestCount();
            
            if (currentCount < limit)
            {
                return TimeSpan.Zero;
            }

            // Calculate time until the oldest request expires
            var oldestRequest = window.GetOldestRequestTime();
            if (oldestRequest.HasValue)
            {
                var timeSinceOldest = DateTime.UtcNow - oldestRequest.Value;
                var remainingTime = TimeSpan.FromMinutes(1) - timeSinceOldest;
                
                // Add jitter to prevent thundering herd (±20% randomization)
                var jitterPercent = (_random.NextDouble() * 0.4) - 0.2; // -0.2 to +0.2
                var jitterMs = remainingTime.TotalMilliseconds * jitterPercent;
                
                return remainingTime.Add(TimeSpan.FromMilliseconds(jitterMs));
            }

            return TimeSpan.Zero;
        }

        public async Task<int> GetRemainingRequestsAsync(string provider, TimeSpan window)
        {
            var slidingWindow = GetOrCreateWindow(provider);
            var limit = GetRateLimitForProvider(provider);
            
            slidingWindow.CleanOldEntries();
            var currentCount = slidingWindow.GetRequestCount();
            
            return await Task.FromResult(Math.Max(0, limit - currentCount));
        }

        public async Task ResetLimitsAsync(string provider)
        {
            if (_slidingWindows.TryRemove(provider, out var window))
            {
                window.Clear();
            }

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

            while (!await TryAcquirePermitAsync(provider))
            {
                stats.RateLimitedRequests++;
                var waitTime = await GetWaitTimeAsync(provider);
                
                // Wait with jitter
                await Task.Delay(waitTime == TimeSpan.Zero ? TimeSpan.FromMilliseconds(100) : waitTime);
            }

            var delay = (DateTime.UtcNow - startTime).TotalMilliseconds;
            stats.AverageDelayMs = (stats.AverageDelayMs * (stats.RateLimitedRequests - 1) + delay) / stats.RateLimitedRequests;
            stats.MaxDelayMs = Math.Max(stats.MaxDelayMs, delay);
        }

        public bool TryAcquirePermit()
        {
            return TryAcquirePermitAsync(_defaultProvider).GetAwaiter().GetResult();
        }

        public bool TryAcquirePermit(string provider)
        {
            return TryAcquirePermitAsync(provider).GetAwaiter().GetResult();
        }

        public async Task<bool> TryAcquirePermitAsync(string provider)
        {
            return await CanMakeRequestAsync(provider);
        }

        public void RecordRequest()
        {
            RecordRequestAsync(_defaultProvider).GetAwaiter().GetResult();
        }

        public void RecordFailure(Exception exception)
        {
            var stats = GetOrCreateStatistics(_defaultProvider);
            stats.FailedRequests++;

            _logger?.LogError($"Rate limiter recorded failure: {exception.Message}", exception);
        }

        public bool IsLimitReached()
        {
            return IsLimitReachedAsync(_defaultProvider).GetAwaiter().GetResult();
        }

        public bool IsLimitReached(string provider)
        {
            return IsLimitReachedAsync(provider).GetAwaiter().GetResult();
        }

        public async Task<bool> IsLimitReachedAsync(string provider)
        {
            return !await CanMakeRequestAsync(provider);
        }

        public int GetRemainingCalls()
        {
            return GetRemainingCallsAsync(_defaultProvider).GetAwaiter().GetResult();
        }

        public int GetRemainingCalls(string provider)
        {
            return GetRemainingCallsAsync(provider).GetAwaiter().GetResult();
        }

        public async Task<int> GetRemainingCallsAsync(string provider)
        {
            return await GetRemainingRequestsAsync(provider, TimeSpan.FromMinutes(1));
        }

        public int GetUsedCalls()
        {
            var window = GetOrCreateWindow(_defaultProvider);
            window.CleanOldEntries();
            return window.GetRequestCount();
        }

        public int GetMaxCalls()
        {
            return _requestsPerMinute;
        }

        public DateTime GetResetTime()
        {
            // With sliding window, there's no fixed reset time
            // Return the time when the oldest request will expire
            var window = GetOrCreateWindow(_defaultProvider);
            var oldest = window.GetOldestRequestTime();
            
            if (oldest.HasValue)
            {
                return oldest.Value.AddMinutes(1);
            }
            
            return DateTime.UtcNow.AddMinutes(1);
        }

        public TimeSpan GetRecommendedDelay()
        {
            return GetWaitTimeAsync(_defaultProvider).GetAwaiter().GetResult();
        }

        public void UpdateLimits(int requestsPerMinute, int requestsPerDay = -1)
        {
            _requestsPerMinute = requestsPerMinute;
            if (requestsPerDay > 0)
            {
                _requestsPerDay = requestsPerDay;
            }

            _logger?.LogInfo($"Rate limits updated: {requestsPerMinute}/min, {requestsPerDay}/day");
        }

        public void Reset()
        {
            _slidingWindows.Clear();
            _statistics.Clear();

            _logger?.LogInfo("Rate limiter reset completed");
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
        private SlidingWindow GetOrCreateWindow(string provider)
        {
            return _slidingWindows.GetOrAdd(provider, p => new SlidingWindow(TimeSpan.FromMinutes(1)));
        }

        private RateLimitingStatistics GetOrCreateStatistics(string provider)
        {
            return _statistics.GetOrAdd(provider, p => new RateLimitingStatistics
            {
                StatisticsStartTime = DateTime.UtcNow
            });
        }

        private int GetRateLimitForProvider(string provider)
        {
            return provider.ToLower() switch
            {
                "alphavantage" => 5, // Free tier: 5 requests per minute
                "finnhub" => 60, // Free tier: 60 requests per minute
                _ => 60
            };
        }

        /// <summary>
        /// Sliding window implementation for rate limiting
        /// </summary>
        private class SlidingWindow
        {
            private readonly ConcurrentQueue<DateTime> _requests = new();
            private readonly TimeSpan _windowSize;
            private readonly object _lock = new();

            public SlidingWindow(TimeSpan windowSize)
            {
                _windowSize = windowSize;
            }

            public void RecordRequest()
            {
                lock (_lock)
                {
                    _requests.Enqueue(DateTime.UtcNow);
                    CleanOldEntries();
                }
            }

            public int GetRequestCount()
            {
                lock (_lock)
                {
                    CleanOldEntries();
                    return _requests.Count;
                }
            }

            public DateTime? GetOldestRequestTime()
            {
                lock (_lock)
                {
                    CleanOldEntries();
                    if (_requests.TryPeek(out var oldest))
                    {
                        return oldest;
                    }
                    return null;
                }
            }

            public void CleanOldEntries()
            {
                var cutoff = DateTime.UtcNow - _windowSize;
                while (_requests.TryPeek(out var oldest) && oldest < cutoff)
                {
                    _requests.TryDequeue(out _);
                }
            }

            public void Clear()
            {
                while (_requests.TryDequeue(out _)) { }
            }
        }

        // Simple console logger fallback
        private class ConsoleLogger : ITradingLogger
        {
            public void LogInfo(string message) => Console.WriteLine($"[INFO] {message}");
            public void LogWarning(string message) => Console.WriteLine($"[WARN] {message}");
            public void LogError(string message, Exception? ex = null) => Console.WriteLine($"[ERROR] {message} {ex?.Message}");
            public void LogDebug(string message) => Console.WriteLine($"[DEBUG] {message}");
            public void LogCritical(string message, Exception? ex = null) => Console.WriteLine($"[CRITICAL] {message} {ex?.Message}");
        }
    }

    // Event Args classes
    public class RateLimitReachedEventArgs : EventArgs
    {
        public string Provider { get; set; } = string.Empty;
        public int CurrentCount { get; set; }
        public int Limit { get; set; }
    }

    public class RateLimitStatusChangedEventArgs : EventArgs
    {
        public string Provider { get; set; } = string.Empty;
        public bool IsLimited { get; set; }
    }

    public class QuotaThresholdEventArgs : EventArgs
    {
        public string Provider { get; set; } = string.Empty;
        public decimal PercentUsed { get; set; }
    }
}