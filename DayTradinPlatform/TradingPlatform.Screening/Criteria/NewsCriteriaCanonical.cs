// File: TradingPlatform.Screening.Criteria\NewsCriteriaCanonical.cs

using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Foundation;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Models;
using TradingPlatform.Screening.Models;

namespace TradingPlatform.Screening.Criteria
{
    /// <summary>
    /// Canonical implementation of news-based trading criteria evaluation.
    /// Evaluates stocks based on news sentiment, catalyst events, and news velocity.
    /// </summary>
    public class NewsCriteriaCanonical : CanonicalCriteriaEvaluator<TradingCriteria>
    {
        private const decimal MINIMUM_PASSING_SCORE = 70m;
        private const decimal POSITIVE_SENTIMENT_THRESHOLD = 0.7m;
        private const decimal NEGATIVE_SENTIMENT_THRESHOLD = 0.3m;
        private const decimal HIGH_VELOCITY_THRESHOLD = 5; // 5+ news items in 24h
        private const decimal CATALYST_SCORE_BOOST = 20m;

        private readonly INewsDataProvider _newsProvider;

        protected override string CriteriaName => "News";

        public NewsCriteriaCanonical(IServiceProvider serviceProvider)
            : base(serviceProvider, serviceProvider.GetRequiredService<ITradingLogger>(), "NewsCriteriaEvaluator")
        {
            // In production, inject actual news provider
            _newsProvider = serviceProvider.GetService<INewsDataProvider>() ?? new MockNewsProvider();
        }

        protected override async Task<TradingResult<CriteriaResult>> EvaluateCriteriaAsync(
            MarketData marketData,
            TradingCriteria criteria,
            CriteriaResult result)
        {
            try
            {
                // Fetch news data
                var newsData = await _newsProvider.GetNewsDataAsync(marketData.Symbol);
                
                // Extract key metrics
                decimal sentimentScore = newsData.SentimentScore;
                bool hasCatalyst = newsData.HasCatalyst;
                int newsCount24h = newsData.NewsCount24Hours;
                int newsCount7d = newsData.NewsCount7Days;
                decimal newsVelocity = CalculateNewsVelocity(newsCount24h, newsCount7d);

                // Record metrics
                result.Metrics["SentimentScore"] = sentimentScore;
                result.Metrics["HasCatalyst"] = hasCatalyst;
                result.Metrics["NewsCount24h"] = newsCount24h;
                result.Metrics["NewsCount7d"] = newsCount7d;
                result.Metrics["NewsVelocity"] = newsVelocity;
                result.Metrics["RequireNewsEvent"] = criteria.RequireNewsEvent;
                
                if (newsData.LatestHeadline != null)
                {
                    result.Metrics["LatestHeadline"] = newsData.LatestHeadline;
                }

                // Check if news event is required but missing
                if (criteria.RequireNewsEvent && !hasCatalyst && newsCount24h == 0)
                {
                    result.Passed = false;
                    result.Score = 0m;
                    result.Reason = "No news events detected (required by criteria)";
                    RecordMetric("NoNewsRejections", 1);
                    return TradingResult<CriteriaResult>.Success(result);
                }

                // Calculate component scores
                decimal sentimentScoreComponent = CalculateSentimentScore(sentimentScore);
                decimal velocityScore = CalculateVelocityScore(newsVelocity);
                decimal catalystScore = CalculateCatalystScore(hasCatalyst, sentimentScore);
                decimal freshnessScore = CalculateFreshnessScore(newsCount24h, newsCount7d);

                // Calculate weighted final score
                decimal finalScore = CalculateWeightedScore(
                    (sentimentScoreComponent, 0.4m),  // Sentiment is most important
                    (velocityScore, 0.2m),             // News velocity/momentum
                    (catalystScore, 0.25m),            // Catalyst events
                    (freshnessScore, 0.15m)            // Recent news activity
                );

                // Apply catalyst boost if present
                if (hasCatalyst && sentimentScore >= POSITIVE_SENTIMENT_THRESHOLD)
                {
                    finalScore = Math.Min(100m, finalScore + CATALYST_SCORE_BOOST);
                }

                // Set results
                result.Score = Math.Round(finalScore, 2);
                result.Passed = result.Score >= MINIMUM_PASSING_SCORE;

                // Additional metrics
                result.Metrics["SentimentScoreComponent"] = Math.Round(sentimentScoreComponent, 2);
                result.Metrics["VelocityScore"] = Math.Round(velocityScore, 2);
                result.Metrics["CatalystScore"] = Math.Round(catalystScore, 2);
                result.Metrics["FreshnessScore"] = Math.Round(freshnessScore, 2);
                result.Metrics["SentimentClassification"] = ClassifySentiment(sentimentScore);

                // Generate reason
                result.Reason = GenerateNewsReason(
                    sentimentScore,
                    hasCatalyst,
                    newsCount24h,
                    result.Passed);

                // Record performance metrics
                RecordMetric($"Sentiment.{ClassifySentiment(sentimentScore)}", 1);
                if (hasCatalyst)
                {
                    RecordMetric("CatalystEvents", 1);
                }
                if (newsVelocity >= HIGH_VELOCITY_THRESHOLD)
                {
                    RecordMetric("HighVelocityNews", 1);
                }

                _logger.LogDebug(
                    $"News evaluation completed for {marketData.Symbol}",
                    new
                    {
                        Symbol = marketData.Symbol,
                        SentimentScore = sentimentScore,
                        HasCatalyst = hasCatalyst,
                        NewsCount = newsCount24h,
                        Score = result.Score,
                        Passed = result.Passed
                    });

                return TradingResult<CriteriaResult>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error evaluating news criteria for {marketData.Symbol}", ex);
                return TradingResult<CriteriaResult>.Failure($"News evaluation error: {ex.Message}", ex);
            }
        }

        private decimal CalculateNewsVelocity(int newsCount24h, int newsCount7d)
        {
            if (newsCount7d == 0) return newsCount24h;
            
            decimal dailyAverage7d = newsCount7d / 7m;
            return dailyAverage7d > 0 ? newsCount24h / dailyAverage7d : newsCount24h;
        }

        private decimal CalculateSentimentScore(decimal sentimentScore)
        {
            // Convert 0-1 sentiment to 0-100 score with optimal range
            if (sentimentScore >= POSITIVE_SENTIMENT_THRESHOLD)
            {
                // Positive sentiment: 70-100 score
                decimal range = 1m - POSITIVE_SENTIMENT_THRESHOLD;
                decimal position = (sentimentScore - POSITIVE_SENTIMENT_THRESHOLD) / range;
                return 70m + (30m * position);
            }
            else if (sentimentScore >= NEGATIVE_SENTIMENT_THRESHOLD)
            {
                // Neutral sentiment: 40-70 score
                decimal range = POSITIVE_SENTIMENT_THRESHOLD - NEGATIVE_SENTIMENT_THRESHOLD;
                decimal position = (sentimentScore - NEGATIVE_SENTIMENT_THRESHOLD) / range;
                return 40m + (30m * position);
            }
            else
            {
                // Negative sentiment: 0-40 score
                return 40m * (sentimentScore / NEGATIVE_SENTIMENT_THRESHOLD);
            }
        }

        private decimal CalculateVelocityScore(decimal newsVelocity)
        {
            if (newsVelocity >= HIGH_VELOCITY_THRESHOLD)
                return 100m;
            else if (newsVelocity >= 2m)
                return 70m + (30m * ((newsVelocity - 2m) / (HIGH_VELOCITY_THRESHOLD - 2m)));
            else if (newsVelocity >= 1m)
                return 50m + (20m * (newsVelocity - 1m));
            else
                return 50m * newsVelocity;
        }

        private decimal CalculateCatalystScore(bool hasCatalyst, decimal sentimentScore)
        {
            if (!hasCatalyst)
                return 50m; // Neutral score without catalyst

            // Catalyst value depends on sentiment
            if (sentimentScore >= POSITIVE_SENTIMENT_THRESHOLD)
                return 100m; // Positive catalyst
            else if (sentimentScore <= NEGATIVE_SENTIMENT_THRESHOLD)
                return 20m; // Negative catalyst (could be shorting opportunity)
            else
                return 60m; // Neutral catalyst
        }

        private decimal CalculateFreshnessScore(int newsCount24h, int newsCount7d)
        {
            if (newsCount24h >= 3)
                return 100m; // Very fresh news
            else if (newsCount24h >= 1)
                return 80m + (20m * ((newsCount24h - 1m) / 2m));
            else if (newsCount7d >= 5)
                return 60m; // Some recent activity
            else if (newsCount7d > 0)
                return 40m + (20m * (newsCount7d / 5m));
            else
                return 20m; // No recent news
        }

        private string ClassifySentiment(decimal sentimentScore)
        {
            return sentimentScore switch
            {
                >= 0.8m => "VeryPositive",
                >= POSITIVE_SENTIMENT_THRESHOLD => "Positive",
                >= 0.5m => "Neutral",
                >= NEGATIVE_SENTIMENT_THRESHOLD => "Mixed",
                >= 0.2m => "Negative",
                _ => "VeryNegative"
            };
        }

        private string GenerateNewsReason(
            decimal sentimentScore,
            bool hasCatalyst,
            int newsCount24h,
            bool passed)
        {
            string sentiment = ClassifySentiment(sentimentScore);
            string catalystText = hasCatalyst ? " with catalyst event" : "";
            string activityText = newsCount24h switch
            {
                0 => "no recent news",
                1 => "1 news item today",
                _ => $"{newsCount24h} news items today"
            };

            if (passed)
            {
                return $"{sentiment} sentiment ({sentimentScore:F2}){catalystText}, {activityText}";
            }
            else
            {
                if (newsCount24h == 0)
                    return "Insufficient news activity for evaluation";
                else
                    return $"{sentiment} sentiment ({sentimentScore:F2}) below threshold, {activityText}";
            }
        }

        protected override TradingResult ValidateInput(MarketData marketData, TradingCriteria criteria)
        {
            var baseValidation = base.ValidateInput(marketData, criteria);
            if (!baseValidation.IsSuccess)
                return baseValidation;

            // News criteria specific validation can be added here if needed

            return TradingResult.Success();
        }
    }

    /// <summary>
    /// Interface for news data providers
    /// </summary>
    public interface INewsDataProvider
    {
        Task<NewsData> GetNewsDataAsync(string symbol);
    }

    /// <summary>
    /// News data model
    /// </summary>
    public class NewsData
    {
        public decimal SentimentScore { get; set; }
        public bool HasCatalyst { get; set; }
        public int NewsCount24Hours { get; set; }
        public int NewsCount7Days { get; set; }
        public string? LatestHeadline { get; set; }
        public DateTime? LatestNewsTime { get; set; }
    }

    /// <summary>
    /// Mock news provider for testing
    /// </summary>
    internal class MockNewsProvider : INewsDataProvider
    {
        private static readonly Random _random = new Random();

        public Task<NewsData> GetNewsDataAsync(string symbol)
        {
            // Simulate different scenarios based on symbol
            var data = symbol.ToUpperInvariant() switch
            {
                "AAPL" => new NewsData
                {
                    SentimentScore = 0.85m,
                    HasCatalyst = true,
                    NewsCount24Hours = 5,
                    NewsCount7Days = 23,
                    LatestHeadline = "Apple Announces Record-Breaking Quarter"
                },
                "TSLA" => new NewsData
                {
                    SentimentScore = 0.72m,
                    HasCatalyst = true,
                    NewsCount24Hours = 8,
                    NewsCount7Days = 45,
                    LatestHeadline = "Tesla Expands Production Capacity"
                },
                _ => new NewsData
                {
                    SentimentScore = 0.5m + (decimal)_random.NextDouble() * 0.3m,
                    HasCatalyst = _random.Next(100) < 20,
                    NewsCount24Hours = _random.Next(0, 4),
                    NewsCount7Days = _random.Next(0, 15),
                    LatestHeadline = null
                }
            };

            data.LatestNewsTime = DateTime.UtcNow.AddHours(-_random.Next(1, 24));
            return Task.FromResult(data);
        }
    }
}