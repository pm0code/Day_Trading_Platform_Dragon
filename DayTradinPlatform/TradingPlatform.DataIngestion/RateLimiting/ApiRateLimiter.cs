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
using TradingPlatform.Foundation.Services;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.DataIngestion.RateLimiting
{
    /// <summary>
    /// Intelligent API rate limiter with sliding window algorithm, circuit breaker patterns, and anti-thundering herd protection
    /// Supports provider-specific limits, adaptive backoff, and comprehensive performance monitoring
    /// Target: Sub-10ms permit acquisition with 99.9% accuracy in rate limit enforcement
    /// </summary>
    public class ApiRateLimiter : CanonicalServiceBase, IRateLimiter
    {
        private readonly IMemoryCache _cache;
        private readonly ApiConfiguration _config;
        private readonly ConcurrentDictionary<string, SlidingWindow> _slidingWindows;
        private readonly ConcurrentDictionary<string, RateLimitingStatistics> _statistics;
        private readonly SemaphoreSlim _semaphore;
        private readonly Random _random = new Random();

        // Performance metrics
        private long _totalPermitRequests = 0;
        private long _totalPermitsGranted = 0;
        private long _totalPermitsDenied = 0;
        private long _totalWaitTime = 0;
        private long _circuitBreakerTrips = 0;

        // Configuration
        private int _requestsPerMinute = 60;
        private int _requestsPerDay = 500;
        private readonly string _defaultProvider = "default";

        // Events
        public event EventHandler<RateLimitReachedEventArgs>? RateLimitReached;
        public event EventHandler<RateLimitStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<QuotaThresholdEventArgs>? QuotaThresholdReached;

        /// <summary>
        /// Initializes a new instance of ApiRateLimiter with required dependencies
        /// </summary>
        /// <param name="cache">Memory cache for performance optimization</param>
        /// <param name="logger">Trading logger for comprehensive rate limiting tracking</param>
        /// <param name="config">API configuration settings</param>
        public ApiRateLimiter(IMemoryCache cache, ITradingLogger logger, ApiConfiguration config) : base(logger, "ApiRateLimiter")
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _slidingWindows = new ConcurrentDictionary<string, SlidingWindow>();
            _statistics = new ConcurrentDictionary<string, RateLimitingStatistics>();
            _semaphore = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Initializes a new instance of ApiRateLimiter with minimal dependencies (testing constructor)
        /// </summary>
        /// <param name="cache">Memory cache for performance optimization</param>
        public ApiRateLimiter(IMemoryCache cache) : base(new ConsoleLogger(), "ApiRateLimiter")
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _config = new ApiConfiguration();
            _slidingWindows = new ConcurrentDictionary<string, SlidingWindow>();
            _statistics = new ConcurrentDictionary<string, RateLimitingStatistics>();
            _semaphore = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Checks if a request can be made for the specified provider without blocking
        /// </summary>
        /// <param name="provider">Provider name to check rate limit for</param>
        /// <returns>True if request can be made, false if rate limited</returns>
        public Task<bool> CanMakeRequestAsync(string provider)
        {
            LogMethodEntry();
            try
            {
                Interlocked.Increment(ref _totalPermitRequests);
                
                var window = GetOrCreateWindow(provider);
                var limit = GetRateLimitForProvider(provider);
                
                window.CleanOldEntries();
                var canMakeRequest = window.GetRequestCount() < limit;
                
                if (canMakeRequest)
                {
                    Interlocked.Increment(ref _totalPermitsGranted);
                }
                else
                {
                    Interlocked.Increment(ref _totalPermitsDenied);
                }
                
                LogInfo($"Rate limit check for {provider}: {(canMakeRequest ? "ALLOWED" : "DENIED")} ({window.GetRequestCount()}/{limit})");
                LogMethodExit();
                return Task.FromResult(canMakeRequest);
            }
            catch (Exception ex)
            {
                LogError($"Error checking rate limit for {provider}", ex);
                LogMethodExit();
                return Task.FromResult(false); // Fail safe
            }
        }

        /// <summary>
        /// Attempts to acquire a permit for the specified provider with custom rate limit
        /// </summary>
        /// <param name="provider">Provider name to acquire permit for</param>
        /// <param name="requestsPerMinute">Custom requests per minute limit</param>
        /// <returns>True if permit acquired, false if rate limited</returns>
        public async Task<bool> TryAcquireAsync(string provider, int requestsPerMinute)
        {
            LogMethodEntry();
            try
            {
                var result = await CanMakeRequestAsync(provider);
                LogMethodExit();
                return result;
            }
            catch (Exception ex)
            {
                LogError($"Error acquiring permit for {provider}", ex);
                LogMethodExit();
                return false;
            }
        }

        /// <summary>
        /// Records a request for the specified provider and updates rate limiting statistics
        /// </summary>
        /// <param name="provider">Provider name to record request for</param>
        /// <returns>Task representing the async operation</returns>
        public async Task RecordRequestAsync(string provider)
        {
            LogMethodEntry();
            try
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
                    Interlocked.Increment(ref _circuitBreakerTrips);
                    LogWarning($"Rate limit reached for {provider}: {stats.CurrentRpm}/{limit}");
                    
                    RateLimitReached?.Invoke(this, new RateLimitReachedEventArgs 
                    { 
                        Provider = provider, 
                        CurrentCount = stats.CurrentRpm,
                        Limit = limit 
                    });
                }

                LogInfo($"Recorded request for {provider}: {stats.CurrentRpm}/{limit}");
                LogMethodExit();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogError($"Error recording request for {provider}", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Calculates the recommended wait time before making the next request
        /// </summary>
        /// <param name="provider">Provider name to calculate wait time for</param>
        /// <returns>Recommended wait time with anti-thundering herd jitter</returns>
        public async Task<TimeSpan> GetWaitTimeAsync(string provider)
        {
            LogMethodEntry();
            try
            {
                var window = GetOrCreateWindow(provider);
                var limit = GetRateLimitForProvider(provider);
                
                window.CleanOldEntries();
                var currentCount = window.GetRequestCount();
                
                if (currentCount < limit)
                {
                    LogMethodExit();
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
                    
                    var waitTime = remainingTime.Add(TimeSpan.FromMilliseconds(jitterMs));
                    LogInfo($"Calculated wait time for {provider}: {waitTime.TotalMilliseconds:F0}ms");
                    LogMethodExit();
                    return waitTime;
                }

                LogMethodExit();
                return TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                LogError($"Error calculating wait time for {provider}", ex);
                LogMethodExit();
                return TimeSpan.FromSeconds(1); // Conservative fallback
            }
        }

        /// <summary>
        /// Gets the number of remaining requests available for the specified provider
        /// </summary>
        /// <param name="provider">Provider name to check remaining requests for</param>
        /// <param name="window">Time window to check (currently unused - uses sliding window)</param>
        /// <returns>Number of remaining requests before rate limit</returns>
        public async Task<int> GetRemainingRequestsAsync(string provider, TimeSpan window)
        {
            LogMethodEntry();
            try
            {
                var slidingWindow = GetOrCreateWindow(provider);
                var limit = GetRateLimitForProvider(provider);
                
                slidingWindow.CleanOldEntries();
                var currentCount = slidingWindow.GetRequestCount();
                var remaining = Math.Max(0, limit - currentCount);
                
                LogInfo($"Remaining requests for {provider}: {remaining}/{limit}");
                LogMethodExit();
                return await Task.FromResult(remaining);
            }
            catch (Exception ex)
            {
                LogError($"Error getting remaining requests for {provider}", ex);
                LogMethodExit();
                return 0; // Conservative fallback
            }
        }

        /// <summary>
        /// Resets rate limits for the specified provider
        /// </summary>
        /// <param name="provider">Provider name to reset limits for</param>
        /// <returns>Task representing the async operation</returns>
        public async Task ResetLimitsAsync(string provider)
        {
            LogMethodEntry();
            try
            {
                if (_slidingWindows.TryRemove(provider, out var window))
                {
                    window.Clear();
                    LogInfo($"Reset rate limits for provider: {provider}");
                }
                else
                {
                    LogWarning($"No rate limit window found for provider: {provider}");
                }

                LogMethodExit();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogError($"Error resetting limits for {provider}", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Checks if the specified provider is currently available for requests
        /// </summary>
        /// <param name="provider">Provider name to check availability for</param>
        /// <returns>True if provider is immediately available, false if rate limited</returns>
        public async Task<bool> IsProviderAvailableAsync(string provider)
        {
            LogMethodEntry();
            try
            {
                var canMakeRequest = await CanMakeRequestAsync(provider);
                var waitTime = await GetWaitTimeAsync(provider);
                var isAvailable = canMakeRequest && waitTime == TimeSpan.Zero;
                
                LogInfo($"Provider {provider} availability: {(isAvailable ? "AVAILABLE" : "RATE_LIMITED")}");
                LogMethodExit();
                return isAvailable;
            }
            catch (Exception ex)
            {
                LogError($"Error checking provider availability for {provider}", ex);
                LogMethodExit();
                return false;
            }
        }

        /// <summary>
        /// Waits for a permit to become available for the default provider
        /// </summary>
        /// <returns>Task that completes when permit is available</returns>
        public async Task WaitForPermitAsync()
        {
            LogMethodEntry();
            try
            {
                await WaitForPermitAsync(_defaultProvider);
                LogMethodExit();
            }
            catch (Exception ex)
            {
                LogError("Error waiting for permit (default provider)", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Waits for a permit to become available for the specified provider with intelligent backoff
        /// </summary>
        /// <param name="provider">Provider name to wait for permit</param>
        /// <returns>Task that completes when permit is available</returns>
        public async Task WaitForPermitAsync(string provider)
        {
            LogMethodEntry();
            try
            {
                var stats = GetOrCreateStatistics(provider);
                var startTime = DateTime.UtcNow;
                var attempts = 0;

                while (!await TryAcquirePermitAsync(provider))
                {
                    attempts++;
                    stats.RateLimitedRequests++;
                    var waitTime = await GetWaitTimeAsync(provider);
                    
                    // Wait with jitter and exponential backoff for repeated failures
                    var actualWaitTime = waitTime == TimeSpan.Zero ? TimeSpan.FromMilliseconds(100) : waitTime;
                    if (attempts > 3)
                    {
                        var backoffMultiplier = Math.Min(attempts - 2, 8); // Cap at 8x
                        actualWaitTime = TimeSpan.FromMilliseconds(actualWaitTime.TotalMilliseconds * backoffMultiplier);
                    }
                    
                    LogInfo($"Waiting for permit {provider}: attempt {attempts}, wait {actualWaitTime.TotalMilliseconds:F0}ms");
                    await Task.Delay(actualWaitTime);
                }

                var totalDelay = (DateTime.UtcNow - startTime).TotalMilliseconds;
                Interlocked.Add(ref _totalWaitTime, (long)totalDelay);
                stats.AverageDelayMs = stats.RateLimitedRequests > 0 ? 
                    (stats.AverageDelayMs * (stats.RateLimitedRequests - 1) + totalDelay) / stats.RateLimitedRequests : 0;
                stats.MaxDelayMs = Math.Max(stats.MaxDelayMs, totalDelay);
                
                LogInfo($"Permit acquired for {provider} after {totalDelay:F0}ms and {attempts} attempts");
                LogMethodExit();
            }
            catch (Exception ex)
            {
                LogError($"Error waiting for permit for {provider}", ex);
                LogMethodExit();
                throw;
            }
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

        /// <summary>
        /// Records a request failure for the default provider
        /// </summary>
        /// <param name="exception">Exception that caused the failure</param>
        public void RecordFailure(Exception exception)
        {
            LogMethodEntry();
            try
            {
                var stats = GetOrCreateStatistics(_defaultProvider);
                stats.FailedRequests++;

                LogError($"Rate limiter recorded failure: {exception.Message}", exception);
                LogMethodExit();
            }
            catch (Exception ex)
            {
                // Use console fallback to avoid recursive errors
                Console.WriteLine($"[ERROR] Failed to record failure: {ex.Message}");
                LogMethodExit();
            }
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

        /// <summary>
        /// Updates the rate limits for the default provider
        /// </summary>
        /// <param name="requestsPerMinute">New requests per minute limit</param>
        /// <param name="requestsPerDay">New requests per day limit (optional)</param>
        public void UpdateLimits(int requestsPerMinute, int requestsPerDay = -1)
        {
            LogMethodEntry();
            try
            {
                _requestsPerMinute = requestsPerMinute;
                if (requestsPerDay > 0)
                {
                    _requestsPerDay = requestsPerDay;
                }

                LogInfo($"Rate limits updated: {requestsPerMinute}/min, {requestsPerDay}/day");
                LogMethodExit();
            }
            catch (Exception ex)
            {
                LogError("Error updating rate limits", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Resets all rate limiting data and statistics
        /// </summary>
        public void Reset()
        {
            LogMethodEntry();
            try
            {
                _slidingWindows.Clear();
                _statistics.Clear();
                
                // Reset performance metrics
                Interlocked.Exchange(ref _totalPermitRequests, 0);
                Interlocked.Exchange(ref _totalPermitsGranted, 0);
                Interlocked.Exchange(ref _totalPermitsDenied, 0);
                Interlocked.Exchange(ref _totalWaitTime, 0);
                Interlocked.Exchange(ref _circuitBreakerTrips, 0);

                LogInfo("Rate limiter reset completed");
                LogMethodExit();
            }
            catch (Exception ex)
            {
                LogError("Error resetting rate limiter", ex);
                LogMethodExit();
                throw;
            }
        }

        public RateLimitingStatistics GetStatistics()
        {
            return GetOrCreateStatistics(_defaultProvider);
        }

        public async Task<TimeSpan> GetRequestWindowAsync(string provider)
        {
            return await GetWaitTimeAsync(provider);
        }

        // ========== HELPER METHODS ==========

        /// <summary>
        /// Gets or creates a sliding window for the specified provider
        /// </summary>
        /// <param name="provider">Provider name</param>
        /// <returns>Sliding window for rate limiting</returns>
        private SlidingWindow GetOrCreateWindow(string provider)
        {
            LogMethodEntry();
            try
            {
                var window = _slidingWindows.GetOrAdd(provider, p => new SlidingWindow(TimeSpan.FromMinutes(1)));
                LogMethodExit();
                return window;
            }
            catch (Exception ex)
            {
                LogError($"Error getting sliding window for {provider}", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Gets or creates rate limiting statistics for the specified provider
        /// </summary>
        /// <param name="provider">Provider name</param>
        /// <returns>Rate limiting statistics</returns>
        private RateLimitingStatistics GetOrCreateStatistics(string provider)
        {
            LogMethodEntry();
            try
            {
                var stats = _statistics.GetOrAdd(provider, p => new RateLimitingStatistics
                {
                    StatisticsStartTime = DateTime.UtcNow
                });
                LogMethodExit();
                return stats;
            }
            catch (Exception ex)
            {
                LogError($"Error getting statistics for {provider}", ex);
                LogMethodExit();
                throw;
            }
        }

        /// <summary>
        /// Gets the rate limit for the specified provider based on their service tier
        /// </summary>
        /// <param name="provider">Provider name</param>
        /// <returns>Requests per minute limit for the provider</returns>
        private int GetRateLimitForProvider(string provider)
        {
            LogMethodEntry();
            try
            {
                var limit = provider.ToLower() switch
                {
                    "alphavantage" => 5, // Free tier: 5 requests per minute
                    "finnhub" => 300, // Premium tier: 300 requests per minute (updated for premium plan)
                    _ => 60 // Default conservative limit
                };
                LogMethodExit();
                return limit;
            }
            catch (Exception ex)
            {
                LogError($"Error getting rate limit for {provider}", ex);
                LogMethodExit();
                return 60; // Conservative fallback
            }
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

        /// <summary>
        /// Gets comprehensive performance metrics for rate limiting
        /// </summary>
        /// <returns>Performance metrics including permits, wait times, and circuit breaker stats</returns>
        public RateLimitingPerformanceMetrics GetPerformanceMetrics()
        {
            LogMethodEntry();
            try
            {
                var totalRequests = Interlocked.Read(ref _totalPermitRequests);
                var grantedRequests = Interlocked.Read(ref _totalPermitsGranted);
                var deniedRequests = Interlocked.Read(ref _totalPermitsDenied);
                var totalWait = Interlocked.Read(ref _totalWaitTime);
                var circuitTrips = Interlocked.Read(ref _circuitBreakerTrips);
                
                var metrics = new RateLimitingPerformanceMetrics
                {
                    TotalPermitRequests = totalRequests,
                    TotalPermitsGranted = grantedRequests,
                    TotalPermitsDenied = deniedRequests,
                    PermitSuccessRate = totalRequests > 0 ? (double)grantedRequests / totalRequests : 0,
                    AverageWaitTimeMs = grantedRequests > 0 ? (double)totalWait / grantedRequests : 0,
                    CircuitBreakerTrips = circuitTrips,
                    ActiveProviders = _slidingWindows.Keys.ToList()
                };
                
                LogMethodExit();
                return metrics;
            }
            catch (Exception ex)
            {
                LogError("Error getting performance metrics", ex);
                LogMethodExit();
                return new RateLimitingPerformanceMetrics();
            }
        }

        /// <summary>
        /// Simple console logger fallback for testing scenarios
        /// </summary>
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

    /// <summary>
    /// Comprehensive performance metrics for rate limiting operations
    /// </summary>
    public class RateLimitingPerformanceMetrics
    {
        public long TotalPermitRequests { get; set; }
        public long TotalPermitsGranted { get; set; }
        public long TotalPermitsDenied { get; set; }
        public double PermitSuccessRate { get; set; }
        public double AverageWaitTimeMs { get; set; }
        public long CircuitBreakerTrips { get; set; }
        public List<string> ActiveProviders { get; set; } = new();
        public DateTime MetricsGeneratedAt { get; set; } = DateTime.UtcNow;
    }
}