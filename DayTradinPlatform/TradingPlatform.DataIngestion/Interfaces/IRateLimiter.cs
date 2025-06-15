// File: TradingPlatform.DataIngestion\Interfaces\IRateLimiter.cs

using System;
using System.Threading.Tasks;

namespace TradingPlatform.DataIngestion.Interfaces
{
    /// <summary>
    /// Defines the contract for rate limiting functionality across all data providers.
    /// Ensures compliance with API quotas and prevents service disruption due to
    /// excessive request rates. Essential for managing free tier limitations and
    /// maintaining service availability for day trading operations.
    /// 
    /// All implementations must be thread-safe as multiple providers may access
    /// rate limiting services concurrently in a high-performance trading environment.
    /// </summary>
    public interface IRateLimiter
    {
        // ========== CORE RATE LIMITING METHODS ==========

        /// <summary>
        /// Waits for permission to make an API request, respecting rate limits.
        /// This method will block (asynchronously) until a request slot is available,
        /// ensuring compliance with provider-specific rate limiting rules.
        /// </summary>
        /// <returns>A task that completes when permission is granted to proceed</returns>
        Task WaitForPermitAsync();

        /// <summary>
        /// Attempts to acquire permission for an API request without waiting.
        /// Returns immediately with true/false indicating if request can proceed.
        /// Useful for scenarios where caller can skip non-critical requests.
        /// </summary>
        /// <returns>True if request can proceed immediately, false if rate limited</returns>
        bool TryAcquirePermit();

        /// <summary>
        /// Records that an API request has been made, updating internal counters.
        /// Must be called after each successful API request to maintain accurate
        /// rate limiting state and quota tracking.
        /// </summary>
        void RecordRequest();

        /// <summary>
        /// Records that an API request failed, which may affect rate limiting logic.
        /// Some implementations may reduce back-off times for failed requests or
        /// implement different retry strategies based on failure patterns.
        /// </summary>
        /// <param name="exception">The exception that caused the request failure</param>
        void RecordFailure(Exception exception);

        // ========== QUOTA AND STATUS METHODS ==========

        /// <summary>
        /// Checks if the rate limit has been reached for the current time window.
        /// Used by providers to determine if they should attempt requests or
        /// switch to fallback data sources.
        /// </summary>
        /// <returns>True if rate limit is reached, false if requests can proceed</returns>
        bool IsLimitReached();

        /// <summary>
        /// Gets the number of remaining API calls available in the current quota period.
        /// Critical for managing daily/monthly quotas and planning request distribution
        /// across trading hours for optimal data availability.
        /// </summary>
        /// <returns>Number of remaining API calls, or -1 if unlimited</returns>
        int GetRemainingCalls();

        /// <summary>
        /// Gets the number of requests used in the current time window.
        /// Useful for monitoring usage patterns and optimizing request distribution.
        /// </summary>
        /// <returns>Number of requests used in current window</returns>
        int GetUsedCalls();

        /// <summary>
        /// Gets the maximum number of requests allowed in the current time window.
        /// </summary>
        /// <returns>Maximum requests per time window, or -1 if unlimited</returns>
        int GetMaxCalls();

        /// <summary>
        /// Gets the time when the current rate limiting window resets.
        /// Enables intelligent request scheduling and user feedback about
        /// when service will be available again.
        /// </summary>
        /// <returns>DateTime when the rate limit window resets</returns>
        DateTime GetResetTime();

        /// <summary>
        /// Gets the recommended delay before the next request attempt.
        /// Implements intelligent back-off strategies to optimize request timing
        /// while respecting provider limitations.
        /// </summary>
        /// <returns>TimeSpan to wait before next request, or TimeSpan.Zero if no delay needed</returns>
        TimeSpan GetRecommendedDelay();

        // ========== CONFIGURATION AND MANAGEMENT ==========

        /// <summary>
        /// Updates rate limiting configuration for the provider.
        /// Allows dynamic adjustment of limits based on subscription tier changes
        /// or provider policy updates without requiring application restart.
        /// </summary>
        /// <param name="requestsPerMinute">Maximum requests per minute</param>
        /// <param name="requestsPerDay">Maximum requests per day (-1 for unlimited)</param>
        void UpdateLimits(int requestsPerMinute, int requestsPerDay = -1);

        /// <summary>
        /// Resets rate limiting state, clearing all counters and timers.
        /// Used for testing, configuration changes, or recovery scenarios.
        /// Should be used cautiously in production environments.
        /// </summary>
        void Reset();

        /// <summary>
        /// Gets detailed statistics about rate limiting performance and usage.
        /// Provides insights for optimization and monitoring of API usage patterns.
        /// </summary>
        /// <returns>Rate limiting statistics and metrics</returns>
        RateLimitingStatistics GetStatistics();

        // ========== EVENTS AND NOTIFICATIONS ==========

        /// <summary>
        /// Event raised when rate limit is reached or exceeded.
        /// Enables proactive handling of rate limiting situations and
        /// automatic switching to fallback data sources.
        /// </summary>
        event EventHandler<RateLimitReachedEventArgs> RateLimitReached;

        /// <summary>
        /// Event raised when rate limit status changes (reached to available or vice versa).
        /// Useful for updating UI indicators and logging rate limiting state changes.
        /// </summary>
        event EventHandler<RateLimitStatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// Event raised when quota usage crosses configurable thresholds.
        /// Enables early warning systems for quota management and usage optimization.
        /// </summary>
        event EventHandler<QuotaThresholdEventArgs> QuotaThresholdReached;
    }

    // ========== SUPPORTING TYPES ==========

    /// <summary>
    /// Comprehensive statistics about rate limiting performance and usage patterns.
    /// Used for monitoring, optimization, and capacity planning.
    /// </summary>
    public class RateLimitingStatistics
    {
        /// <summary>
        /// Total number of requests processed since last reset.
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Number of requests that were rate limited (delayed).
        /// </summary>
        public long RateLimitedRequests { get; set; }

        /// <summary>
        /// Number of requests that failed due to rate limiting.
        /// </summary>
        public long FailedRequests { get; set; }

        /// <summary>
        /// Average delay imposed by rate limiting (milliseconds).
        /// </summary>
        public double AverageDelayMs { get; set; }

        /// <summary>
        /// Maximum delay imposed by rate limiting (milliseconds).
        /// </summary>
        public double MaxDelayMs { get; set; }

        /// <summary>
        /// Current requests per minute rate.
        /// </summary>
        public double CurrentRpm { get; set; }

        /// <summary>
        /// Peak requests per minute rate observed.
        /// </summary>
        public double PeakRpm { get; set; }

        /// <summary>
        /// Percentage of quota used in current period.
        /// </summary>
        public double QuotaUsagePercent { get; set; }

        /// <summary>
        /// Time when statistics collection started.
        /// </summary>
        public DateTime StatisticsStartTime { get; set; }

        /// <summary>
        /// Duration of statistics collection period.
        /// </summary>
        public TimeSpan CollectionDuration => DateTime.UtcNow - StatisticsStartTime;

        /// <summary>
        /// Overall efficiency (successful requests / total requests).
        /// </summary>
        public double Efficiency => TotalRequests > 0 ? (double)(TotalRequests - FailedRequests) / TotalRequests * 100 : 0;
    }

    /// <summary>
    /// Event arguments for rate limit reached events.
    /// Provides context about the rate limiting situation.
    /// </summary>
    public class RateLimitReachedEventArgs : EventArgs
    {
        /// <summary>
        /// The provider or service that reached its rate limit.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Time when the rate limit was reached.
        /// </summary>
        public DateTime ReachedTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Current number of requests in the time window.
        /// </summary>
        public int CurrentRequests { get; set; }

        /// <summary>
        /// Maximum allowed requests in the time window.
        /// </summary>
        public int MaxRequests { get; set; }

        /// <summary>
        /// Time when the rate limit will reset.
        /// </summary>
        public DateTime ResetTime { get; set; }

        /// <summary>
        /// Recommended action to take (wait, switch provider, etc.).
        /// </summary>
        public string RecommendedAction { get; set; } = "Wait for reset";

        /// <summary>
        /// Duration until rate limit resets.
        /// </summary>
        public TimeSpan TimeToReset => ResetTime - DateTime.UtcNow;
    }

    /// <summary>
    /// Event arguments for rate limit status change events.
    /// Tracks transitions between available and rate-limited states.
    /// </summary>
    public class RateLimitStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Previous rate limiting status.
        /// </summary>
        public RateLimitStatus PreviousStatus { get; set; }

        /// <summary>
        /// Current rate limiting status.
        /// </summary>
        public RateLimitStatus CurrentStatus { get; set; }

        /// <summary>
        /// Time when the status change occurred.
        /// </summary>
        public DateTime StatusChangeTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional context about the status change.
        /// </summary>
        public string Context { get; set; } = string.Empty;
    }

    /// <summary>
    /// Event arguments for quota threshold events.
    /// Enables proactive quota management and usage optimization.
    /// </summary>
    public class QuotaThresholdEventArgs : EventArgs
    {
        /// <summary>
        /// The threshold percentage that was reached (e.g., 80.0 for 80%).
        /// </summary>
        public double ThresholdPercent { get; set; }

        /// <summary>
        /// Current quota usage percentage.
        /// </summary>
        public double CurrentUsagePercent { get; set; }

        /// <summary>
        /// Number of requests remaining in quota.
        /// </summary>
        public int RemainingRequests { get; set; }

        /// <summary>
        /// Time when quota resets.
        /// </summary>
        public DateTime QuotaResetTime { get; set; }

        /// <summary>
        /// Recommended action for quota management.
        /// </summary>
        public string RecommendedAction { get; set; } = "Monitor usage closely";

        /// <summary>
        /// Time when threshold was reached.
        /// </summary>
        public DateTime ThresholdReachedTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Enumeration of rate limiting status states.
    /// Used for status tracking and event notifications.
    /// </summary>
    public enum RateLimitStatus
    {
        /// <summary>
        /// Rate limiting is active and requests can proceed normally.
        /// </summary>
        Available,

        /// <summary>
        /// Approaching rate limit, requests are being throttled.
        /// </summary>
        Throttled,

        /// <summary>
        /// Rate limit reached, requests are being blocked/delayed.
        /// </summary>
        Limited,

        /// <summary>
        /// Quota exhausted, no more requests allowed until reset.
        /// </summary>
        Exhausted,

        /// <summary>
        /// Rate limiter is disabled or not configured.
        /// </summary>
        Disabled,

        /// <summary>
        /// Rate limiter encountered an error and cannot function properly.
        /// </summary>
        Error
    }
}

// Total Lines: 285
