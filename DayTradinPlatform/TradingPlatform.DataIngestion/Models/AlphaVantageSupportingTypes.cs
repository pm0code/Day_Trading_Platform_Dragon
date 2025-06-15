// File: TradingPlatform.DataIngestion\Models\AlphaVantageSupportingTypes.cs

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TradingPlatform.DataIngestion.Models
{
    /// <summary>
    /// Collection of supporting types for AlphaVantage API integration.
    /// These models provide comprehensive metadata, error handling, and utility structures
    /// for all AlphaVantage API endpoints and responses.
    /// </summary>

    // ========== METADATA AND INFORMATION TYPES ==========

    /// <summary>
    /// Represents metadata information from AlphaVantage API responses.
    /// Contains information about the data series, refresh times, and API configuration.
    /// Common to most AlphaVantage API endpoints that return time series data.
    /// </summary>
    public class AlphaVantageMetaData
    {
        /// <summary>
        /// Descriptive information about the API function and data type.
        /// Maps to "1. Information" field in AlphaVantage JSON response.
        /// Example: "Daily Prices (open, high, low, close) and Volumes"
        /// </summary>
        [JsonPropertyName("1. Information")]
        public string Information { get; set; } = string.Empty;

        /// <summary>
        /// The stock symbol this metadata applies to.
        /// Maps to "2. Symbol" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("2. Symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp of when the data was last refreshed/updated.
        /// Maps to "3. Last Refreshed" field in AlphaVantage JSON response.
        /// Format typically: "YYYY-MM-DD HH:MM:SS" (US/Eastern timezone)
        /// </summary>
        [JsonPropertyName("3. Last Refreshed")]
        public string LastRefreshed { get; set; } = string.Empty;

        /// <summary>
        /// Indicates the size/scope of the data output.
        /// Maps to "4. Output Size" field in AlphaVantage JSON response.
        /// Values: "Compact" (100 data points), "Full size" (20+ years)
        /// </summary>
        [JsonPropertyName("4. Output Size")]
        public string OutputSize { get; set; } = string.Empty;

        /// <summary>
        /// Timezone for the timestamp and trading data.
        /// Maps to "5. Time Zone" field in AlphaVantage JSON response.
        /// Typically: "US/Eastern" for US markets
        /// </summary>
        [JsonPropertyName("5. Time Zone")]
        public string TimeZone { get; set; } = string.Empty;

        // ========== CALCULATED PROPERTIES ==========

        /// <summary>
        /// Converts the LastRefreshed string to DateTime.
        /// Returns DateTime.MinValue if parsing fails.
        /// </summary>
        [JsonIgnore]
        public DateTime LastRefreshedDateTime
        {
            get
            {
                if (DateTime.TryParse(LastRefreshed, out var result))
                    return result;
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Indicates if this is full historical data (vs compact).
        /// </summary>
        [JsonIgnore]
        public bool IsFullSize => OutputSize.Contains("Full", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Indicates if this is compact/limited data.
        /// </summary>
        [JsonIgnore]
        public bool IsCompact => OutputSize.Contains("Compact", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Determines if the metadata represents US market data.
        /// </summary>
        [JsonIgnore]
        public bool IsUSMarket => TimeZone.Contains("US/Eastern", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Calculates how old the data is based on last refresh time.
        /// </summary>
        [JsonIgnore]
        public TimeSpan DataAge => DateTime.UtcNow - LastRefreshedDateTime;

        // ========== VALIDATION METHODS ==========

        /// <summary>
        /// Validates that the metadata contains required information.
        /// </summary>
        /// <returns>True if metadata is complete and valid</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Symbol) &&
                   !string.IsNullOrWhiteSpace(Information) &&
                   !string.IsNullOrWhiteSpace(LastRefreshed) &&
                   LastRefreshedDateTime > DateTime.MinValue;
        }

        /// <summary>
        /// String representation for logging and debugging purposes.
        /// </summary>
        public override string ToString()
        {
            return $"AlphaVantageMetaData[{Symbol}]: {Information} - Last: {LastRefreshed} ({OutputSize})";
        }
    }

    // ========== ERROR HANDLING TYPES ==========

    /// <summary>
    /// Represents a generic AlphaVantage API error response.
    /// Used for consistent error handling across all AlphaVantage endpoints.
    /// AlphaVantage returns errors in various formats depending on the issue.
    /// </summary>
    public class AlphaVantageErrorResponse
    {
        /// <summary>
        /// Primary error message from the API.
        /// Common errors include rate limiting, invalid API key, invalid symbol, etc.
        /// </summary>
        [JsonPropertyName("Error Message")]
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Additional information or note about the error.
        /// Some AlphaVantage endpoints provide additional context.
        /// </summary>
        [JsonPropertyName("Note")]
        public string Note { get; set; } = string.Empty;

        /// <summary>
        /// HTTP status code associated with the error.
        /// Not directly from AlphaVantage but added during processing.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Timestamp when the error occurred (local processing time).
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The API endpoint that generated this error.
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Request parameters that caused the error (for debugging).
        /// </summary>
        public Dictionary<string, string>? RequestParameters { get; set; }

        // ========== ERROR CLASSIFICATION ==========

        /// <summary>
        /// Indicates if this is a rate limiting error.
        /// AlphaVantage has specific rate limiting messages.
        /// </summary>
        [JsonIgnore]
        public bool IsRateLimitError =>
            ErrorMessage.Contains("API call frequency", StringComparison.OrdinalIgnoreCase) ||
            ErrorMessage.Contains("per minute", StringComparison.OrdinalIgnoreCase) ||
            Note.Contains("API call frequency", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Indicates if this is an invalid API key error.
        /// </summary>
        [JsonIgnore]
        public bool IsInvalidApiKeyError =>
            ErrorMessage.Contains("Invalid API call", StringComparison.OrdinalIgnoreCase) ||
            ErrorMessage.Contains("API key", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Indicates if this is an invalid symbol error.
        /// </summary>
        [JsonIgnore]
        public bool IsInvalidSymbolError =>
            ErrorMessage.Contains("Invalid API call", StringComparison.OrdinalIgnoreCase) ||
            ErrorMessage.Contains("symbol", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Indicates if this is a temporary/retryable error.
        /// </summary>
        [JsonIgnore]
        public bool IsRetryableError => IsRateLimitError && !IsInvalidApiKeyError && !IsInvalidSymbolError;

        /// <summary>
        /// String representation for logging and debugging purposes.
        /// </summary>
        public override string ToString()
        {
            var endpoint = !string.IsNullOrWhiteSpace(Endpoint) ? $" ({Endpoint})" : "";
            return $"AlphaVantageError[{StatusCode}]{endpoint}: {ErrorMessage}" +
                   (!string.IsNullOrWhiteSpace(Note) ? $" - Note: {Note}" : "");
        }
    }

    // ========== RATE LIMITING AND QUOTA TYPES ==========

    /// <summary>
    /// Represents API rate limiting information for AlphaVantage.
    /// Tracks usage quotas and implements intelligent request throttling.
    /// AlphaVantage has different limits for free vs. premium tiers.
    /// </summary>
    public class AlphaVantageRateLimit
    {
        /// <summary>
        /// Maximum number of requests allowed per minute.
        /// Free tier: 5 requests per minute
        /// Premium tiers: Higher limits based on subscription
        /// </summary>
        public int RequestsPerMinute { get; set; } = 5;

        /// <summary>
        /// Maximum number of requests allowed per day.
        /// Free tier: 500 requests per day
        /// Premium tiers: Higher or unlimited based on subscription
        /// </summary>
        public int RequestsPerDay { get; set; } = 500;

        /// <summary>
        /// Number of requests used in the current minute window.
        /// Tracked for real-time rate limiting decisions.
        /// </summary>
        public int RequestsUsedThisMinute { get; set; }

        /// <summary>
        /// Number of requests used in the current day.
        /// Tracked for daily quota management.
        /// </summary>
        public int RequestsUsedToday { get; set; }

        /// <summary>
        /// Timestamp of the current minute window start.
        /// Used for determining when the minute limit resets.
        /// </summary>
        public DateTime CurrentMinuteWindowStart { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date of the current day for daily quota tracking.
        /// Resets at midnight UTC.
        /// </summary>
        public DateTime CurrentDay { get; set; } = DateTime.UtcNow.Date;

        // ========== CALCULATED PROPERTIES ==========

        /// <summary>
        /// Remaining requests in the current minute window.
        /// </summary>
        [JsonIgnore]
        public int RemainingRequestsThisMinute => Math.Max(0, RequestsPerMinute - RequestsUsedThisMinute);

        /// <summary>
        /// Remaining requests for the current day.
        /// </summary>
        [JsonIgnore]
        public int RemainingRequestsToday => Math.Max(0, RequestsPerDay - RequestsUsedToday);

        /// <summary>
        /// Percentage of minute quota used.
        /// </summary>
        [JsonIgnore]
        public decimal MinuteQuotaUsedPercent => RequestsPerMinute > 0 ?
            (decimal)RequestsUsedThisMinute / RequestsPerMinute * 100 : 0;

        /// <summary>
        /// Percentage of daily quota used.
        /// </summary>
        [JsonIgnore]
        public decimal DailyQuotaUsedPercent => RequestsPerDay > 0 ?
            (decimal)RequestsUsedToday / RequestsPerDay * 100 : 0;

        /// <summary>
        /// Indicates if the minute rate limit is exhausted.
        /// </summary>
        [JsonIgnore]
        public bool IsMinuteLimitReached => RemainingRequestsThisMinute <= 0;

        /// <summary>
        /// Indicates if the daily rate limit is exhausted.
        /// </summary>
        [JsonIgnore]
        public bool IsDailyLimitReached => RemainingRequestsToday <= 0;

        /// <summary>
        /// Indicates if any rate limit is currently blocking requests.
        /// </summary>
        [JsonIgnore]
        public bool IsLimitReached => IsMinuteLimitReached || IsDailyLimitReached;

        /// <summary>
        /// Time when the minute window resets (next minute).
        /// </summary>
        [JsonIgnore]
        public DateTime MinuteResetTime => CurrentMinuteWindowStart.AddMinutes(1);

        /// <summary>
        /// Time when the daily quota resets (next day).
        /// </summary>
        [JsonIgnore]
        public DateTime DailyResetTime => CurrentDay.AddDays(1);

        // ========== RATE LIMITING METHODS ==========

        /// <summary>
        /// Records a new API request and updates usage counters.
        /// Handles window resets automatically.
        /// </summary>
        public void RecordRequest()
        {
            var now = DateTime.UtcNow;

            // Check if we need to reset the minute window
            if (now >= MinuteResetTime)
            {
                RequestsUsedThisMinute = 0;
                CurrentMinuteWindowStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            }

            // Check if we need to reset the daily counter
            if (now.Date > CurrentDay)
            {
                RequestsUsedToday = 0;
                CurrentDay = now.Date;
            }

            // Increment counters
            RequestsUsedThisMinute++;
            RequestsUsedToday++;
        }

        /// <summary>
        /// Calculates the recommended delay before the next request.
        /// Returns TimeSpan.Zero if no delay is needed.
        /// </summary>
        /// <returns>TimeSpan to wait before next request</returns>
        public TimeSpan GetRecommendedDelay()
        {
            if (IsDailyLimitReached)
            {
                return DailyResetTime - DateTime.UtcNow;
            }

            if (IsMinuteLimitReached)
            {
                return MinuteResetTime - DateTime.UtcNow;
            }

            // For free tier, spread requests evenly across the minute
            if (RequestsPerMinute <= 5)
            {
                return TimeSpan.FromSeconds(60.0 / RequestsPerMinute);
            }

            return TimeSpan.Zero;
        }

        /// <summary>
        /// Creates a rate limit configuration for AlphaVantage free tier.
        /// </summary>
        /// <returns>RateLimit configured for free tier usage</returns>
        public static AlphaVantageRateLimit CreateFreeTierConfig()
        {
            return new AlphaVantageRateLimit
            {
                RequestsPerMinute = 5,
                RequestsPerDay = 500,
                RequestsUsedThisMinute = 0,
                RequestsUsedToday = 0,
                CurrentMinuteWindowStart = DateTime.UtcNow,
                CurrentDay = DateTime.UtcNow.Date
            };
        }

        /// <summary>
        /// Creates a rate limit configuration for AlphaVantage premium tier.
        /// </summary>
        /// <param name="requestsPerMinute">Premium tier requests per minute limit</param>
        /// <param name="requestsPerDay">Premium tier requests per day limit (or -1 for unlimited)</param>
        /// <returns>RateLimit configured for premium tier usage</returns>
        public static AlphaVantageRateLimit CreatePremiumTierConfig(int requestsPerMinute = 75, int requestsPerDay = -1)
        {
            return new AlphaVantageRateLimit
            {
                RequestsPerMinute = requestsPerMinute,
                RequestsPerDay = requestsPerDay == -1 ? int.MaxValue : requestsPerDay,
                RequestsUsedThisMinute = 0,
                RequestsUsedToday = 0,
                CurrentMinuteWindowStart = DateTime.UtcNow,
                CurrentDay = DateTime.UtcNow.Date
            };
        }

        /// <summary>
        /// String representation for logging and debugging purposes.
        /// </summary>
        public override string ToString()
        {
            return $"AlphaVantageRateLimit: {RequestsUsedThisMinute}/{RequestsPerMinute}/min, " +
                   $"{RequestsUsedToday}/{RequestsPerDay}/day " +
                   $"(Next reset: {MinuteResetTime:HH:mm:ss})";
        }
    }

    // ========== API CONFIGURATION TYPES ==========

    /// <summary>
    /// Comprehensive configuration for AlphaVantage API integration.
    /// Centralizes all AlphaVantage-specific settings and behaviors.
    /// </summary>
    public class AlphaVantageConfiguration
    {
        /// <summary>
        /// AlphaVantage API key for authentication.
        /// Required for all API calls.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Base URL for AlphaVantage API endpoints.
        /// Default: "https://www.alphavantage.co/query"
        /// </summary>
        public string BaseUrl { get; set; } = "https://www.alphavantage.co/query";

        /// <summary>
        /// Timeout for API requests in seconds.
        /// Default: 30 seconds for reliable network handling.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Number of retry attempts for failed requests.
        /// Default: 3 retries for transient failures.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Delay between retry attempts in milliseconds.
        /// Default: 1000ms (1 second) between retries.
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Default output size for time series requests.
        /// Values: "compact" (100 data points), "full" (20+ years)
        /// Default: "compact" for faster responses and lower quota usage.
        /// </summary>
        public string DefaultOutputSize { get; set; } = "compact";

        /// <summary>
        /// Rate limiting configuration for this AlphaVantage instance.
        /// </summary>
        public AlphaVantageRateLimit RateLimit { get; set; } = AlphaVantageRateLimit.CreateFreeTierConfig();

        /// <summary>
        /// Enable automatic request throttling based on rate limits.
        /// Default: true for automatic quota management.
        /// </summary>
        public bool EnableAutoThrottling { get; set; } = true;

        /// <summary>
        /// Enable caching of API responses to reduce quota usage.
        /// Default: true for improved performance and quota efficiency.
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Default cache duration for API responses in minutes.
        /// Default: 15 minutes for balance of freshness and efficiency.
        /// </summary>
        public int DefaultCacheDurationMinutes { get; set; } = 15;

        /// <summary>
        /// Validates the configuration for completeness and correctness.
        /// </summary>
        /// <returns>True if configuration is valid</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(ApiKey) &&
                   !string.IsNullOrWhiteSpace(BaseUrl) &&
                   Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) &&
                   TimeoutSeconds > 0 &&
                   MaxRetryAttempts >= 0 &&
                   RetryDelayMs >= 0 &&
                   RateLimit != null;
        }

        /// <summary>
        /// String representation for logging (hides API key for security).
        /// </summary>
        public override string ToString()
        {
            var maskedApiKey = !string.IsNullOrWhiteSpace(ApiKey)
                ? $"{ApiKey.Substring(0, Math.Min(4, ApiKey.Length))}***"
                : "MISSING";

            return $"AlphaVantageConfig: Key={maskedApiKey}, OutputSize={DefaultOutputSize}, " +
                   $"Timeout={TimeoutSeconds}s, Cache={EnableCaching}";
        }
    }
}

// Total Lines: 439
