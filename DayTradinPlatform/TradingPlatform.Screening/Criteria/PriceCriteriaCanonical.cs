// File: TradingPlatform.Screening.Criteria\PriceCriteriaCanonical.cs

using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Screening.Canonical;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Models;
using TradingPlatform.Screening.Models;
using ScreeningCriteriaResult = TradingPlatform.Screening.Models.CriteriaResult;

namespace TradingPlatform.Screening.Criteria
{
    /// <summary>
    /// Canonical implementation of price-based trading criteria evaluation.
    /// Evaluates stocks based on price range, penny stock rules, and optimal price positioning.
    /// </summary>
    public class PriceCriteriaCanonical : CanonicalScreeningCriteriaEvaluator
    {
        private const decimal PENNY_STOCK_THRESHOLD = 5.00m;
        private const decimal OPTIMAL_RANGE_FACTOR = 0.2m; // 20% from edges is optimal

        protected override string CriteriaName => "Price";

        public PriceCriteriaCanonical(IServiceProvider serviceProvider)
            : base(serviceProvider, serviceProvider.GetRequiredService<ITradingLogger>(), "PriceCriteriaEvaluator")
        {
        }

        protected override async Task<TradingResult<ScreeningCriteriaResult>> EvaluateCriteriaAsync(
            MarketData marketData,
            TradingCriteria criteria)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Create result object
                    var result = new ScreeningCriteriaResult
                    {
                        CriteriaName = CriteriaName,
                        EvaluatedAt = DateTime.UtcNow,
                        Symbol = marketData.Symbol,
                        Confidence = 95m // High confidence for price criteria
                    };

                    decimal price = marketData.Price;
                    
                    // Record metrics
                    result.Metrics["Price"] = price;
                    result.Metrics["MinPrice"] = criteria.MinimumPrice;
                    result.Metrics["MaxPrice"] = criteria.MaximumPrice;
                    result.Metrics["PennyStockThreshold"] = PENNY_STOCK_THRESHOLD;

                    // Check penny stock rule
                    bool isPennyStock = price < PENNY_STOCK_THRESHOLD;
                    result.Metrics["IsPennyStock"] = isPennyStock;

                    if (!criteria.EnablePennyStocks && isPennyStock)
                    {
                        result.Passed = false;
                        result.Score = 0m;
                        result.Reason = $"Price ${price:F2} is below penny stock threshold of ${PENNY_STOCK_THRESHOLD:F2}";
                        RecordMetric("PennyStockRejections", 1);
                        return TradingResult<ScreeningCriteriaResult>.Success(result);
                    }

                    // Check price range
                    bool inRange = price >= criteria.MinimumPrice && price <= criteria.MaximumPrice;
                    result.Metrics["InPriceRange"] = inRange;

                    if (!inRange)
                    {
                        result.Passed = false;
                        result.Score = 0m;
                        result.Reason = $"Price ${price:F2} is outside range ${criteria.MinimumPrice:F2}-${criteria.MaximumPrice:F2}";
                        RecordMetric("OutOfRangeRejections", 1);
                        return TradingResult<ScreeningCriteriaResult>.Success(result);
                    }

                    // Calculate score based on position within range
                    decimal range = criteria.MaximumPrice - criteria.MinimumPrice;
                    decimal optimalLow = criteria.MinimumPrice + (range * OPTIMAL_RANGE_FACTOR);
                    decimal optimalHigh = criteria.MaximumPrice - (range * OPTIMAL_RANGE_FACTOR);

                    decimal score;
                    string scoreReason;

                    if (price >= optimalLow && price <= optimalHigh)
                    {
                        // Price is in optimal zone - highest scores
                        decimal optimalRange = optimalHigh - optimalLow;
                        decimal positionInOptimal = (price - optimalLow) / optimalRange;
                        score = 80m + (20m * (1m - Math.Abs(0.5m - positionInOptimal) * 2m));
                        scoreReason = "in optimal range";
                    }
                    else if (price < optimalLow)
                    {
                        // Price is in lower zone
                        score = CalculateNormalizedScore(price, criteria.MinimumPrice, optimalLow) * 0.8m;
                        scoreReason = "in lower range";
                    }
                    else
                    {
                        // Price is in upper zone
                        score = CalculateNormalizedScore(price, optimalHigh, criteria.MaximumPrice, invertScore: true) * 0.8m;
                        scoreReason = "in upper range";
                    }

                    // Apply penny stock bonus/penalty
                    if (isPennyStock && criteria.EnablePennyStocks)
                    {
                        score *= 0.9m; // 10% penalty for penny stocks even when enabled
                        scoreReason += " (penny stock penalty applied)";
                    }

                    // Set final results
                    result.Passed = true;
                    result.Score = Math.Round(score, 2);
                    result.Reason = $"Price ${price:F2} is {scoreReason}";

                    // Set alert level based on score and confidence
                    result.AlertLevel = DetermineAlertLevel(result.Score, result.Confidence);

                    // Additional metrics
                    result.Metrics["OptimalLowPrice"] = optimalLow;
                    result.Metrics["OptimalHighPrice"] = optimalHigh;
                    result.Metrics["PriceRangePosition"] = (price - criteria.MinimumPrice) / range;
                    result.Metrics["ScoreReason"] = scoreReason;

                    RecordMetric("SuccessfulEvaluations", 1);
                    RecordMetric($"Score.{(int)(score / 10) * 10}-{(int)(score / 10) * 10 + 9}", 1);

                    Logger.LogDebug(
                        $"Price evaluation completed for {marketData.Symbol}",
                        new
                        {
                            Symbol = marketData.Symbol,
                            Price = price,
                            Score = result.Score,
                            Passed = result.Passed,
                            Reason = scoreReason
                        });

                    return TradingResult<ScreeningCriteriaResult>.Success(result);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error evaluating price criteria for {marketData.Symbol}", ex);
                    return TradingResult<ScreeningCriteriaResult>.Failure(new TradingError("EVALUATION_ERROR", $"Price evaluation error: {ex.Message}", ex));
                }
            });
        }

        protected override TradingResult ValidateInput(MarketData marketData, TradingCriteria criteria)
        {
            var baseValidation = base.ValidateInput(marketData, criteria);
            if (!baseValidation.IsSuccess)
                return baseValidation;

            if (criteria.MinimumPrice < 0)
                return TradingResult.Failure(new TradingError("INVALID_INPUT", $"Minimum price cannot be negative: {criteria.MinimumPrice}"));

            if (criteria.MaximumPrice <= criteria.MinimumPrice)
                return TradingResult.Failure(new TradingError("INVALID_INPUT", $"Maximum price ({criteria.MaximumPrice}) must be greater than minimum price ({criteria.MinimumPrice})"));

            return TradingResult.Success();
        }
    }
}