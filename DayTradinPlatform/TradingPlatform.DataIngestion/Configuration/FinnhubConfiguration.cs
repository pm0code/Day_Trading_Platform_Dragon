// File: TradingPlatform.DataIngestion\Configuration\FinnhubConfiguration.cs

using System.ComponentModel.DataAnnotations;

namespace TradingPlatform.DataIngestion.Configuration
{
    /// <summary>
    /// Finnhub-specific configuration options using the IOptions pattern.
    /// Supports validation and hot-reload capabilities for production environments.
    /// Based on Microsoft.Extensions.Configuration best practices.
    /// </summary>
    public class FinnhubConfiguration
    {
        public const string SectionName = "FinnhubProvider";

        /// <summary>
        /// Finnhub API key for authentication.
        /// Required for all API calls. Free tier: 60 calls/minute.
        /// </summary>
        [Required(ErrorMessage = "Finnhub API key is required")]
        [MinLength(10, ErrorMessage = "API key must be at least 10 characters")]
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Base URL for Finnhub REST API endpoints.
        /// Default: https://finnhub.io/api/v1
        /// </summary>
        [Required]
        [Url(ErrorMessage = "Base URL must be a valid URL")]
        public string BaseUrl { get; set; } = "https://finnhub.io/api/v1";

        /// <summary>
        /// WebSocket URL for real-time data streaming.
        /// Default: wss://ws.finnhub.io
        /// </summary>
        [Url(ErrorMessage = "WebSocket URL must be a valid URL")]
        public string WebSocketUrl { get; set; } = "wss://ws.finnhub.io";

        /// <summary>
        /// HTTP request timeout in seconds.
        /// Default: 30 seconds for reliable network handling.
        /// </summary>
        [Range(5, 300, ErrorMessage = "Timeout must be between 5 and 300 seconds")]
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Maximum retry attempts for failed requests.
        /// Default: 3 retries with exponential backoff.
        /// </summary>
        [Range(0, 10, ErrorMessage = "Retry attempts must be between 0 and 10")]
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Rate limiting configuration for API calls.
        /// </summary>
        public FinnhubRateLimitConfiguration RateLimit { get; set; } = new();

        /// <summary>
        /// Caching configuration for API responses.
        /// </summary>
        public FinnhubCacheConfiguration Cache { get; set; } = new();

        /// <summary>
        /// Enable detailed logging for debugging and monitoring.
        /// Should be false in production for performance.
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;

        /// <summary>
        /// Enable response validation using FluentValidation.
        /// Recommended for production environments.
        /// </summary>
        public bool EnableResponseValidation { get; set; } = true;

        /// <summary>
        /// Default symbols to warm up in cache on startup.
        /// Improves initial response times for common symbols.
        /// </summary>
        public List<string> WarmupSymbols { get; set; } = new() { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA" };
    }

    /// <summary>
    /// Rate limiting configuration specific to Finnhub API tiers.
    /// </summary>
    public class FinnhubRateLimitConfiguration
    {
        /// <summary>
        /// Requests per minute limit (Free: 60, Premium: varies).
        /// </summary>
        [Range(1, 10000, ErrorMessage = "Requests per minute must be between 1 and 10000")]
        public int RequestsPerMinute { get; set; } = 60;

        /// <summary>
        /// Requests per day limit (Free: unlimited, Premium: varies).
        /// Set to -1 for unlimited.
        /// </summary>
        public int RequestsPerDay { get; set; } = -1;

        /// <summary>
        /// Enable automatic request throttling.
        /// Recommended for production environments.
        /// </summary>
        public bool EnableAutoThrottling { get; set; } = true;

        /// <summary>
        /// Delay between requests in milliseconds when throttling.
        /// Calculated automatically based on rate limits.
        /// </summary>
        public int ThrottleDelayMs { get; set; } = 1000;
    }

    /// <summary>
    /// Cache configuration for Finnhub API responses.
    /// </summary>
    public class FinnhubCacheConfiguration
    {
        /// <summary>
        /// Quote cache duration in seconds.
        /// Default: 15 seconds for real-time trading.
        /// </summary>
        [Range(1, 3600, ErrorMessage = "Quote cache duration must be between 1 and 3600 seconds")]
        public int QuoteCacheSeconds { get; set; } = 15;

        /// <summary>
        /// Company profile cache duration in hours.
        /// Default: 24 hours (changes infrequently).
        /// </summary>
        [Range(1, 168, ErrorMessage = "Profile cache duration must be between 1 and 168 hours")]
        public int ProfileCacheHours { get; set; } = 24;

        /// <summary>
        /// News cache duration in minutes.
        /// Default: 10 minutes for fresh news updates.
        /// </summary>
        [Range(1, 1440, ErrorMessage = "News cache duration must be between 1 and 1440 minutes")]
        public int NewsCacheMinutes { get; set; } = 10;

        /// <summary>
        /// Market status cache duration in minutes.
        /// Default: 5 minutes around market open/close times.
        /// </summary>
        [Range(1, 60, ErrorMessage = "Market status cache duration must be between 1 and 60 minutes")]
        public int MarketStatusCacheMinutes { get; set; } = 5;

        /// <summary>
        /// Enable cache warming on application startup.
        /// </summary>
        public bool EnableCacheWarming { get; set; } = true;
    }
}

// Total Lines: 135
