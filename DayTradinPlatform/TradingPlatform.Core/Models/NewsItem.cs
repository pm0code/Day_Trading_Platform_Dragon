// File: TradingPlatform.Core\Models\NewsItem.cs

using System;
using System.Collections.Generic;
using System.Linq;

namespace TradingPlatform.Core.Models
{
    /// <summary>
    /// Represents a financial news item with sentiment analysis and symbol associations.
    /// Designed for real-time news processing in day trading operations where news
    /// catalysts can significantly impact stock prices and trading decisions.
    /// 
    /// Supports multiple news sources and provides standardized access to news content,
    /// metadata, and derived analytics like sentiment scoring and symbol relevance.
    /// </summary>
    public class NewsItem
    {
        /// <summary>
        /// Unique identifier for this news item within the trading platform.
        /// Used for deduplication, tracking, and referencing in alerts and analysis.
        /// </summary>
        public required string Id { get; set; }

        /// <summary>
        /// The headline or title of the news article.
        /// Primary content used for quick scanning and alert notifications.
        /// </summary>
        public required string Title { get; set; }

        /// <summary>
        /// Brief summary or excerpt of the news article content.
        /// Provides additional context beyond the headline for trading decisions.
        /// May be null if the news source doesn't provide summaries.
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// Full content of the news article if available.
        /// Used for comprehensive sentiment analysis and keyword extraction.
        /// May be null for headline-only news feeds or premium content restrictions.
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// News source or publisher name.
        /// Examples: "Reuters", "Bloomberg", "MarketWatch", "PR Newswire"
        /// Used for source credibility weighting and filtering preferences.
        /// </summary>
        public required string Source { get; set; }

        /// <summary>
        /// Direct URL link to the original news article.
        /// Enables users to access full article content and verify information.
        /// </summary>
        public required string Url { get; set; }

        /// <summary>
        /// Date and time when the news was originally published.
        /// Critical for determining news freshness and market impact timing.
        /// </summary>
        public DateTime PublishedAt { get; set; }

        /// <summary>
        /// News category classification for filtering and organization.
        /// Examples: "earnings", "mergers", "FDA", "general", "analyst", "insider"
        /// Helps traders focus on specific types of market-moving events.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Overall sentiment classification of the news content.
        /// Values: "positive", "negative", "neutral"
        /// Derived from automated sentiment analysis of title, summary, and content.
        /// </summary>
        public string? Sentiment { get; set; }

        /// <summary>
        /// Numerical sentiment score if available from analysis.
        /// Range varies by sentiment analysis provider but typically -1.0 to 1.0.
        /// Higher absolute values indicate stronger sentiment polarity.
        /// </summary>
        public decimal? SentimentScore { get; set; }

        /// <summary>
        /// Confidence level of the sentiment analysis (0.0 to 1.0).
        /// Higher values indicate more reliable sentiment classification.
        /// Used for filtering news items with uncertain sentiment.
        /// </summary>
        public decimal? SentimentConfidence { get; set; }

        /// <summary>
        /// List of stock symbols mentioned or related to this news item.
        /// Critical for associating news with specific trading opportunities.
        /// Symbols should be standardized to platform format (e.g., "AAPL", "MSFT").
        /// </summary>
        public List<string>? RelatedSymbols { get; set; }

        /// <summary>
        /// Primary stock symbol if this news is specifically about one company.
        /// Used when news has a clear single-company focus (earnings, FDA approval, etc.).
        /// Should be the most relevant symbol from RelatedSymbols list.
        /// </summary>
        public string? PrimarySymbol { get; set; }

        /// <summary>
        /// Keywords extracted from the news content for categorization and search.
        /// Useful for custom filtering and identifying trading themes.
        /// Examples: "earnings beat", "FDA approval", "merger announcement"
        /// </summary>
        public List<string>? Keywords { get; set; }

        /// <summary>
        /// Priority level for this news item in trading context.
        /// Values: "high", "medium", "low"
        /// Based on factors like source credibility, content impact, and symbol relevance.
        /// </summary>
        public string? Priority { get; set; }

        /// <summary>
        /// Date and time when this news item was added to the trading platform.
        /// Used for tracking processing delays and ensuring timely alert delivery.
        /// </summary>
        public DateTime ProcessedAt { get; set; }

        /// <summary>
        /// Additional metadata specific to the news source or processing pipeline.
        /// Allows for extensibility without breaking the core model structure.
        /// May contain source-specific IDs, processing metrics, or custom attributes.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Validates the news item for completeness and data integrity.
        /// Ensures required fields are present and data formats are correct.
        /// </summary>
        /// <returns>True if the news item is valid for processing</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Id) &&
                   !string.IsNullOrWhiteSpace(Title) &&
                   !string.IsNullOrWhiteSpace(Source) &&
                   !string.IsNullOrWhiteSpace(Url) &&
                   PublishedAt > DateTime.MinValue &&
                   ProcessedAt > DateTime.MinValue;
        }

        /// <summary>
        /// Determines if this news item is still fresh for trading decisions.
        /// News older than the specified threshold may be less actionable.
        /// </summary>
        /// <param name="maxAge">Maximum age before news is considered stale</param>
        /// <returns>True if news is fresh, false if stale</returns>
        public bool IsFresh(TimeSpan maxAge)
        {
            return DateTime.UtcNow - PublishedAt <= maxAge;
        }

        /// <summary>
        /// Checks if this news item is relevant to a specific stock symbol.
        /// Considers both primary symbol and related symbols list.
        /// </summary>
        /// <param name="symbol">Stock symbol to check relevance for</param>
        /// <returns>True if news is relevant to the specified symbol</returns>
        public bool IsRelevantToSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return false;

            var normalizedSymbol = symbol.ToUpperInvariant();

            if (PrimarySymbol?.ToUpperInvariant() == normalizedSymbol)
                return true;

            return RelatedSymbols?.Any(s => s.ToUpperInvariant() == normalizedSymbol) == true;
        }

        /// <summary>
        /// Gets the processing delay between publication and platform ingestion.
        /// Important metric for evaluating news feed timeliness and competitiveness.
        /// </summary>
        /// <returns>Time span between publication and processing</returns>
        public TimeSpan GetProcessingDelay()
        {
            return ProcessedAt - PublishedAt;
        }

        /// <summary>
        /// Calculates a relevance score for a specific symbol based on position and context.
        /// Higher scores indicate stronger relevance for trading decisions.
        /// </summary>
        /// <param name="symbol">Symbol to calculate relevance for</param>
        /// <returns>Relevance score from 0.0 to 1.0</returns>
        public decimal GetSymbolRelevance(string symbol)
        {
            if (!IsRelevantToSymbol(symbol))
                return 0.0m;

            var normalizedSymbol = symbol.ToUpperInvariant();

            // Primary symbol gets highest relevance
            if (PrimarySymbol?.ToUpperInvariant() == normalizedSymbol)
                return 1.0m;

            // Related symbols get proportional relevance based on position
            if (RelatedSymbols?.Any() == true)
            {
                var index = RelatedSymbols.FindIndex(s => s.ToUpperInvariant() == normalizedSymbol);
                if (index >= 0)
                {
                    // Earlier mentions get higher relevance (1.0, 0.8, 0.6, 0.4, min 0.2)
                    return Math.Max(0.2m, 1.0m - (index * 0.2m));
                }
            }

            return 0.1m; // Minimal relevance if found in content but not explicitly listed
        }

        /// <summary>
        /// Creates a summary string suitable for alert notifications.
        /// Includes key information in a concise, readable format.
        /// </summary>
        /// <returns>Formatted summary for notifications</returns>
        public string GetAlertSummary()
        {
            var summary = $"📰 {Title}";

            if (!string.IsNullOrWhiteSpace(PrimarySymbol))
                summary = $"📰 ${PrimarySymbol}: {Title}";

            if (!string.IsNullOrWhiteSpace(Sentiment))
                summary += $" [{Sentiment.ToUpperInvariant()}]";

            summary += $" - {Source}";

            return summary;
        }

        /// <summary>
        /// String representation for logging and debugging purposes.
        /// Provides essential news information in a readable format.
        /// </summary>
        /// <returns>Formatted string representation of the news item</returns>
        public override string ToString()
        {
            var symbolInfo = PrimarySymbol ?? (RelatedSymbols?.FirstOrDefault()) ?? "No Symbol";
            return $"NewsItem[{Id}]: {symbolInfo} - {Title} ({Source}, {PublishedAt:yyyy-MM-dd HH:mm})";
        }
    }
}

// Total Lines: 223
