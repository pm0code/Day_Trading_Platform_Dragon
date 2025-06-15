// File: TradingPlatform.DataIngestion\Models\FinnhubSupportingTypes.cs

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TradingPlatform.DataIngestion.Models
{
    /// <summary>
    /// Collection of supporting types for Finnhub API integration.
    /// These models map to various Finnhub API endpoints and provide
    /// comprehensive data structures for market data processing.
    /// </summary>

    // ========== SYMBOL AND MARKET DATA TYPES ==========

    /// <summary>
    /// Represents a stock symbol information structure from Finnhub.
    /// Used for symbol discovery and market coverage validation.
    /// Maps to Finnhub's /stock/symbol endpoint response.
    /// </summary>
    public class FinnhubSymbol
    {
        /// <summary>
        /// The stock symbol identifier (e.g., "AAPL", "MSFT").
        /// Primary key for all market data operations.
        /// </summary>
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable company or security description.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Display version of the symbol for UI presentation.
        /// May include exchange suffixes or formatting.
        /// </summary>
        [JsonPropertyName("displaySymbol")]
        public string DisplaySymbol { get; set; } = string.Empty;

        /// <summary>
        /// Security type classification.
        /// Values: "Common Stock", "ETF", "ADR", etc.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Exchange where the security is primarily traded.
        /// </summary>
        [JsonPropertyName("mic")]
        public string Exchange { get; set; } = string.Empty;

        /// <summary>
        /// ISO currency code for the security.
        /// </summary>
        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// Indicates if the symbol is actively tradeable.
        /// </summary>
        public bool IsActive => !string.IsNullOrWhiteSpace(Symbol) && !string.IsNullOrWhiteSpace(Description);

        /// <summary>
        /// String representation for logging and debugging.
        /// </summary>
        public override string ToString()
        {
            return $"FinnhubSymbol[{Symbol}]: {Description} ({Type}, {Exchange})";
        }
    }

    /// <summary>
    /// Represents Finnhub market status response.
    /// Indicates whether specific markets are currently open for trading.
    /// Maps to Finnhub's /stock/market-status endpoint.
    /// </summary>
    public class FinnhubMarketStatus
    {
        /// <summary>
        /// Indicates if the market is currently open for trading.
        /// </summary>
        [JsonPropertyName("isOpen")]
        public bool IsOpen { get; set; }

        /// <summary>
        /// Current trading session type.
        /// Values: "regular", "pre", "post", "closed"
        /// </summary>
        [JsonPropertyName("session")]
        public string Session { get; set; } = string.Empty;

        /// <summary>
        /// Market timezone identifier.
        /// Example: "America/New_York"
        /// </summary>
        [JsonPropertyName("timezone")]
        public string Timezone { get; set; } = string.Empty;

        /// <summary>
        /// Market opening time in local timezone.
        /// </summary>
        [JsonPropertyName("t")]
        public long Timestamp { get; set; }

        /// <summary>
        /// Convert timestamp to DateTime in UTC.
        /// </summary>
        [JsonIgnore]
        public DateTime DateTime => DateTimeOffset.FromUnixTimeSeconds(Timestamp).DateTime;

        /// <summary>
        /// Determines if the market is in regular trading hours.
        /// </summary>
        [JsonIgnore]
        public bool IsRegularSession => Session.Equals("regular", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// String representation for logging and debugging.
        /// </summary>
        public override string ToString()
        {
            var status = IsOpen ? "OPEN" : "CLOSED";
            return $"MarketStatus: {status} ({Session}, {Timezone})";
        }
    }

    // ========== SENTIMENT AND NEWS TYPES ==========

    /// <summary>
    /// Represents Finnhub sentiment analysis response.
    /// Contains insider trading data used for sentiment calculation.
    /// Maps to Finnhub's /stock/insider-sentiment endpoint.
    /// </summary>
    public class FinnhubSentimentResponse
    {
        /// <summary>
        /// Array of individual sentiment data points.
        /// </summary>
        [JsonPropertyName("data")]
        public List<FinnhubSentimentData> Data { get; set; } = new List<FinnhubSentimentData>();

        /// <summary>
        /// Symbol this sentiment data applies to.
        /// </summary>
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Calculates overall sentiment score from insider trading data.
        /// Positive values indicate bullish sentiment, negative bearish.
        /// </summary>
        [JsonIgnore]
        public decimal OverallSentimentScore
        {
            get
            {
                if (Data?.Count > 0)
                {
                    return Data.Sum(d => d.Change);
                }
                return 0m;
            }
        }

        /// <summary>
        /// Gets the most recent sentiment data point.
        /// </summary>
        [JsonIgnore]
        public FinnhubSentimentData? LatestSentiment => Data?.OrderByDescending(d => d.Year).ThenByDescending(d => d.Month).FirstOrDefault();
    }

    /// <summary>
    /// Individual sentiment data point from Finnhub insider trading analysis.
    /// Represents insider trading activity for a specific time period.
    /// </summary>
    public class FinnhubSentimentData
    {
        /// <summary>
        /// Net change in insider holdings (shares).
        /// Positive values indicate net buying, negative net selling.
        /// </summary>
        [JsonPropertyName("change")]
        public decimal Change { get; set; }

        /// <summary>
        /// Month of the sentiment data (1-12).
        /// </summary>
        [JsonPropertyName("month")]
        public int Month { get; set; }

        /// <summary>
        /// Year of the sentiment data.
        /// </summary>
        [JsonPropertyName("year")]
        public int Year { get; set; }

        /// <summary>
        /// Insider name or identifier (if available).
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Market value of the insider trading activity.
        /// </summary>
        [JsonPropertyName("mspr")]
        public decimal MarketValue { get; set; }

        /// <summary>
        /// Convert to DateTime for easier handling.
        /// </summary>
        [JsonIgnore]
        public DateTime Date => new DateTime(Year, Month, 1);

        /// <summary>
        /// Indicates bullish sentiment (net buying).
        /// </summary>
        [JsonIgnore]
        public bool IsBullish => Change > 0;

        /// <summary>
        /// Indicates bearish sentiment (net selling).
        /// </summary>
        [JsonIgnore]
        public bool IsBearish => Change < 0;

        /// <summary>
        /// String representation for logging and debugging.
        /// </summary>
        public override string ToString()
        {
            var direction = IsBullish ? "BUY" : IsBearish ? "SELL" : "NEUTRAL";
            return $"Sentiment[{Year}-{Month:D2}]: {direction} {Math.Abs(Change):N0} shares";
        }
    }

    /// <summary>
    /// Represents a Finnhub news item structure.
    /// Maps to the JSON response format from Finnhub's news endpoints.
    /// Used for market news and company-specific news feeds.
    /// </summary>
    public class FinnhubNewsItem
    {
        /// <summary>
        /// Unique identifier for this news item.
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// News article headline or title.
        /// </summary>
        [JsonPropertyName("headline")]
        public string Headline { get; set; } = string.Empty;

        /// <summary>
        /// Brief summary or excerpt of the news content.
        /// </summary>
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// News source or publisher name.
        /// </summary>
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Direct URL to the original news article.
        /// </summary>
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// UNIX timestamp of when the news was published.
        /// </summary>
        [JsonPropertyName("datetime")]
        public long Datetime { get; set; }

        /// <summary>
        /// News category classification.
        /// Examples: "general", "forex", "crypto", "merger"
        /// </summary>
        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// URL to associated image or thumbnail.
        /// </summary>
        [JsonPropertyName("image")]
        public string? Image { get; set; }

        /// <summary>
        /// List of related stock symbols mentioned in the news.
        /// </summary>
        [JsonPropertyName("related")]
        public List<string> Related { get; set; } = new List<string>();

        /// <summary>
        /// Convert timestamp to DateTime in UTC.
        /// </summary>
        [JsonIgnore]
        public DateTime PublishedAt => DateTimeOffset.FromUnixTimeSeconds(Datetime).DateTime;

        /// <summary>
        /// Determines if this news item is still fresh for trading decisions.
        /// </summary>
        /// <param name="maxAge">Maximum age before news is considered stale</param>
        /// <returns>True if news is fresh, false if stale</returns>
        public bool IsFresh(TimeSpan maxAge)
        {
            return DateTime.UtcNow - PublishedAt <= maxAge;
        }

        /// <summary>
        /// Checks if this news item is relevant to a specific stock symbol.
        /// </summary>
        /// <param name="symbol">Stock symbol to check relevance for</param>
        /// <returns>True if news is relevant to the specified symbol</returns>
        public bool IsRelevantToSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return false;

            var normalizedSymbol = symbol.ToUpperInvariant();
            return Related?.Any(s => s.ToUpperInvariant() == normalizedSymbol) == true ||
                   Headline.Contains(symbol, StringComparison.OrdinalIgnoreCase) ||
                   Summary.Contains(symbol, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// String representation for logging and debugging.
        /// </summary>
        public override string ToString()
        {
            var symbolInfo = Related?.FirstOrDefault() ?? "General";
            return $"FinnhubNews[{Id}]: {symbolInfo} - {Headline} ({Source}, {PublishedAt:yyyy-MM-dd HH:mm})";
        }
    }

    // ========== EARNINGS AND EVENTS TYPES ==========

    /// <summary>
    /// Represents earnings calendar data from Finnhub.
    /// Used for tracking upcoming and past earnings announcements.
    /// </summary>
    public class FinnhubEarningsCalendar
    {
        /// <summary>
        /// Stock symbol for the earnings announcement.
        /// </summary>
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Date of the earnings announcement.
        /// </summary>
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        /// <summary>
        /// Expected earnings per share (analyst estimate).
        /// </summary>
        [JsonPropertyName("epsEstimate")]
        public decimal? EpsEstimate { get; set; }

        /// <summary>
        /// Actual reported earnings per share.
        /// </summary>
        [JsonPropertyName("epsActual")]
        public decimal? EpsActual { get; set; }

        /// <summary>
        /// Revenue estimate from analysts.
        /// </summary>
        [JsonPropertyName("revenueEstimate")]
        public decimal? RevenueEstimate { get; set; }

        /// <summary>
        /// Actual reported revenue.
        /// </summary>
        [JsonPropertyName("revenueActual")]
        public decimal? RevenueActual { get; set; }

        /// <summary>
        /// Convert date string to DateTime.
        /// </summary>
        [JsonIgnore]
        public DateTime EarningsDate
        {
            get
            {
                if (DateTime.TryParse(Date, out var result))
                    return result;
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Indicates if earnings beat estimates.
        /// </summary>
        [JsonIgnore]
        public bool EarningsBeat => EpsActual.HasValue && EpsEstimate.HasValue && EpsActual > EpsEstimate;

        /// <summary>
        /// Indicates if earnings missed estimates.
        /// </summary>
        [JsonIgnore]
        public bool EarningsMiss => EpsActual.HasValue && EpsEstimate.HasValue && EpsActual < EpsEstimate;

        /// <summary>
        /// EPS surprise percentage.
        /// </summary>
        [JsonIgnore]
        public decimal? EpsSurprisePercent
        {
            get
            {
                if (EpsActual.HasValue && EpsEstimate.HasValue && EpsEstimate != 0)
                {
                    return ((EpsActual.Value - EpsEstimate.Value) / Math.Abs(EpsEstimate.Value)) * 100;
                }
                return null;
            }
        }
    }

    // ========== ERROR AND STATUS TYPES ==========

    /// <summary>
    /// Represents a generic Finnhub API error response.
    /// Used for consistent error handling across all Finnhub endpoints.
    /// </summary>
    public class FinnhubErrorResponse
    {
        /// <summary>
        /// Error message from the API.
        /// </summary>
        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// HTTP status code associated with the error.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Timestamp when the error occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional context or details about the error.
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// String representation for logging and debugging.
        /// </summary>
        public override string ToString()
        {
            return $"FinnhubError[{StatusCode}]: {Error} at {Timestamp:yyyy-MM-dd HH:mm:ss}";
        }
    }

    /// <summary>
    /// Represents API rate limiting information from Finnhub.
    /// Used for quota management and request throttling.
    /// </summary>
    public class FinnhubRateLimit
    {
        /// <summary>
        /// Maximum number of requests allowed per time period.
        /// </summary>
        public int RequestLimit { get; set; }

        /// <summary>
        /// Number of requests used in current time period.
        /// </summary>
        public int RequestsUsed { get; set; }

        /// <summary>
        /// Remaining requests in current time period.
        /// </summary>
        [JsonIgnore]
        public int RequestsRemaining => Math.Max(0, RequestLimit - RequestsUsed);

        /// <summary>
        /// Percentage of quota used.
        /// </summary>
        [JsonIgnore]
        public decimal QuotaUsedPercent => RequestLimit > 0 ? (decimal)RequestsUsed / RequestLimit * 100 : 0;

        /// <summary>
        /// Indicates if rate limit is nearly exhausted (>90%).
        /// </summary>
        [JsonIgnore]
        public bool IsNearLimit => QuotaUsedPercent > 90;

        /// <summary>
        /// Time when the rate limit resets.
        /// </summary>
        public DateTime ResetTime { get; set; }

        /// <summary>
        /// String representation for logging and debugging.
        /// </summary>
        public override string ToString()
        {
            return $"RateLimit: {RequestsUsed}/{RequestLimit} ({QuotaUsedPercent:F1}%) - Resets: {ResetTime:HH:mm:ss}";
        }
    }
}

// Total Lines: 412
