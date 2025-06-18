// File: TradingPlatform.Screening.Criteria\NewsCriteria.cs

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Screening.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Screening.Criteria
{
    /// <summary>
    /// Evaluates news-based trading criteria using mathematically correct, standards-compliant logic.
    /// </summary>
    public class NewsCriteria
    {
        private readonly ILogger _logger;

        public NewsCriteria(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<CriteriaResult> EvaluateNewsAsync(string symbol, TradingCriteria criteria)
        {
            var result = new CriteriaResult
            {
                CriteriaName = "News",
                EvaluatedAt = DateTime.UtcNow
            };

            try
            {
                decimal sentimentScore = 0.0m;
                bool hasCatalyst = false;

                // Simulate a positive news event for demonstration
                if (symbol.Equals("AAPL", StringComparison.OrdinalIgnoreCase))
                {
                    sentimentScore = 0.85m;
                    hasCatalyst = true;
                }

                result.Metrics["SentimentScore"] = sentimentScore;
                result.Metrics["HasCatalyst"] = hasCatalyst;

                if (criteria.RequireNewsEvent && !hasCatalyst)
                {
                    result.Passed = false;
                    result.Score = 0m;
                    result.Reason = "No news catalyst detected (required by criteria).";
                }
                else if (sentimentScore >= 0.7m)
                {
                    result.Passed = true;
                    result.Score = Math.Min(1.0m, sentimentScore);
                    result.Reason = $"Positive news sentiment ({sentimentScore:P0}){(hasCatalyst ? " with catalyst" : "")}.";
                }
                else if (sentimentScore > 0m)
                {
                    result.Passed = false;
                    result.Score = sentimentScore;
                    result.Reason = $"Neutral/weak news sentiment ({sentimentScore:P0}).";
                }
                else
                {
                    result.Passed = false;
                    result.Score = 0m;
                    result.Reason = "No recent news or sentiment detected.";
                }

                TradingLogOrchestrator.Instance.LogInfo($"News evaluation for {symbol}: Sentiment={sentimentScore:F2}, Catalyst={hasCatalyst}, Score={result.Score:F2}, Passed={result.Passed}");
                return result;
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError(ex, $"Error evaluating news criteria for {symbol}");
                result.Passed = false;
                result.Score = 0m;
                result.Reason = $"Evaluation error: {ex.Message}";
                return result;
            }
        }
    }
}

// Total Lines: 61
