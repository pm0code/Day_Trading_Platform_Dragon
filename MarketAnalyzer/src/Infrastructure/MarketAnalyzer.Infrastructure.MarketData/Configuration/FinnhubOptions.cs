using System.ComponentModel.DataAnnotations;

namespace MarketAnalyzer.Infrastructure.MarketData.Configuration;

/// <summary>
/// Configuration options for Finnhub API integration.
/// Uses industry-standard Options Pattern from Microsoft.Extensions.Options.
/// </summary>
public class FinnhubOptions
{
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "Finnhub";

    /// <summary>
    /// Gets or sets the Finnhub API key.
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base URL for Finnhub API.
    /// </summary>
    [Url]
    public string BaseUrl { get; set; } = "https://finnhub.io/api/v1";

    /// <summary>
    /// Gets or sets the WebSocket URL for real-time data.
    /// </summary>
    [Url]
    public string WebSocketUrl { get; set; } = "wss://ws.finnhub.io";

    /// <summary>
    /// Gets or sets the maximum number of API calls per minute.
    /// Free tier: 60 calls/minute, Premium: 300 calls/minute.
    /// </summary>
    [Range(1, 1000)]
    public int MaxCallsPerMinute { get; set; } = 60;

    /// <summary>
    /// Gets or sets the maximum burst calls per second.
    /// Free tier: 30 calls/second, Premium: 30 calls/second.
    /// </summary>
    [Range(1, 100)]
    public int MaxCallsPerSecond { get; set; } = 30;

    /// <summary>
    /// Gets or sets the HTTP client timeout in seconds.
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the cache expiration time for quote data in seconds.
    /// </summary>
    [Range(1, 3600)]
    public int QuoteCacheExpirationSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the cache expiration time for company data in seconds.
    /// </summary>
    [Range(60, 86400)]
    public int CompanyCacheExpirationSeconds { get; set; } = 3600;

    /// <summary>
    /// Gets or sets whether to enable circuit breaker pattern.
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of failures before opening circuit breaker.
    /// </summary>
    [Range(1, 100)]
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets circuit breaker timeout in seconds.
    /// </summary>
    [Range(1, 300)]
    public int CircuitBreakerTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets whether to enable retry policy.
    /// </summary>
    public bool EnableRetryPolicy { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether to enable request/response logging.
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this is a premium subscription.
    /// </summary>
    public bool IsPremium { get; set; } = false;
}