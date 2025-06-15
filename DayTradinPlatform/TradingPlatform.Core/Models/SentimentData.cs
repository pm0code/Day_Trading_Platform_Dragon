// File: TradingPlatform.Core\Models\SentimentData.cs

using System;
using System.Collections.Generic;

namespace TradingPlatform.Core.Models
{
    /// <summary>
    /// Represents sentiment analysis data for a financial instrument.
    /// Provides sentiment indicators, confidence levels, and source attribution
    /// for market sentiment analysis in day trading operations.
    /// 
    /// This model supports multiple sentiment calculation methodologies including
    /// news sentiment, social media sentiment, and insider trading sentiment.
    /// All confidence values are normalized to 0.0-1.0 range for consistency.
    /// </summary>
    public class SentimentData
    {
        /// <summary>
        /// The financial instrument symbol this sentiment data applies to.
        /// Must match the symbol format used throughout the trading platform.
        /// </summary>
        public required string Symbol { get; set; }

        /// <summary>
        /// The overall sentiment classification for the symbol.
        /// Valid values: "positive", "negative", "neutral"
        /// Must be lowercase for consistency across the platform.
        /// </summary>
        public required string Sentiment { get; set; }

        /// <summary>
        /// Confidence level of the sentiment analysis, normalized to 0.0-1.0 range.
        /// 0.0 = No confidence, 1.0 = Maximum confidence
        /// Used for filtering and weighting sentiment data in trading decisions.
        /// </summary>
        public decimal Confidence { get; set; }

        /// <summary>
        /// Timestamp when this sentiment data was generated or last updated.
        /// Critical for determining data freshness in real-time trading scenarios.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Source system or provider that generated this sentiment data.
        /// Examples: "Finnhub", "NewsAPI", "TwitterAPI", "AlphaVantage"
        /// Used for data quality assessment and source reliability tracking.
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// Numerical sentiment score if available from the source.
        /// Range typically varies by provider but should be documented.
        /// Null if the source only provides categorical sentiment.
        /// </summary>
        public decimal? SentimentScore { get; set; }

        /// <summary>
        /// Number of data points used to calculate this sentiment.
        /// Examples: number of news articles, social media posts, insider trades
        /// Higher sample sizes generally indicate more reliable sentiment.
        /// </summary>
        public int? SampleSize { get; set; }

        /// <summary>
        /// Time period over which this sentiment was calculated.
        /// Examples: "1hour", "24hours", "7days", "30days"
        /// Important for understanding the temporal scope of the sentiment.
        /// </summary>
        public string? TimeFrame { get; set; }

        /// <summary>
        /// Additional metadata and provider-specific data.
        /// Allows for extensibility without breaking the core model.
        /// May contain raw sentiment metrics, sub-category breakdowns, etc.
        /// </summary>
        public Dictionary<string, object>? AdditionalData { get; set; }

        /// <summary>
        /// Validates the sentiment data for consistency and completeness.
        /// Ensures sentiment values are valid and confidence is in proper range.
        /// </summary>
        /// <returns>True if the sentiment data is valid, false otherwise</returns>
        public bool IsValid()
        {
            var validSentiments = new[] { "positive", "negative", "neutral" };

            return !string.IsNullOrWhiteSpace(Symbol) &&
                   !string.IsNullOrWhiteSpace(Sentiment) &&
                   validSentiments.Contains(Sentiment.ToLowerInvariant()) &&
                   Confidence >= 0.0m && Confidence <= 1.0m &&
                   Timestamp > DateTime.MinValue;
        }

        /// <summary>
        /// Gets a normalized sentiment score between -1.0 and 1.0.
        /// -1.0 = Strong negative, 0.0 = Neutral, 1.0 = Strong positive
        /// Weighted by confidence level for more accurate representation.
        /// </summary>
        /// <returns>Normalized sentiment score weighted by confidence</returns>
        public decimal GetNormalizedScore()
        {
            return Sentiment.ToLowerInvariant() switch
            {
                "positive" => Confidence,
                "negative" => -Confidence,
                "neutral" => 0.0m,
                _ => 0.0m
            };
        }

        /// <summary>
        /// Determines if this sentiment data is still fresh for trading decisions.
        /// Sentiment older than the specified threshold is considered stale.
        /// </summary>
        /// <param name="maxAge">Maximum age before sentiment is considered stale</param>
        /// <returns>True if sentiment is fresh, false if stale</returns>
        public bool IsFresh(TimeSpan maxAge)
        {
            return DateTime.UtcNow - Timestamp <= maxAge;
        }

        /// <summary>
        /// Creates a copy of this sentiment data for historical tracking.
        /// Useful for maintaining sentiment history and trend analysis.
        /// </summary>
        /// <returns>A deep copy of the current sentiment data</returns>
        public SentimentData Clone()
        {
            return new SentimentData
            {
                Symbol = Symbol,
                Sentiment = Sentiment,
                Confidence = Confidence,
                Timestamp = Timestamp,
                Source = Source,
                SentimentScore = SentimentScore,
                SampleSize = SampleSize,
                TimeFrame = TimeFrame,
                AdditionalData = AdditionalData != null
                    ? new Dictionary<string, object>(AdditionalData)
                    : null
            };
        }

        /// <summary>
        /// String representation for logging and debugging purposes.
        /// Provides key sentiment information in a readable format.
        /// </summary>
        /// <returns>Formatted string representation of sentiment data</returns>
        public override string ToString()
        {
            return $"SentimentData[{Symbol}]: {Sentiment} (Confidence: {Confidence:P1}, Source: {Source ?? "Unknown"}, Time: {Timestamp:yyyy-MM-dd HH:mm:ss})";
        }
    }
}

// Total Lines: 131
