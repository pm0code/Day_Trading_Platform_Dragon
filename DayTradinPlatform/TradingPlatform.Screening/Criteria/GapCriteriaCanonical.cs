// File: TradingPlatform.Screening.Criteria\GapCriteriaCanonical.cs

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
    /// Canonical implementation of gap-based trading criteria evaluation.
    /// Evaluates stocks based on opening gap percentage, gap direction, and gap fill potential.
    /// </summary>
    public class GapCriteriaCanonical : CanonicalCriteriaEvaluator<TradingCriteria>
    {
        private const decimal MINIMUM_PASSING_SCORE = 70m;
        private const decimal OPTIMAL_GAP_MIN = 2.0m;  // 2% gap
        private const decimal OPTIMAL_GAP_MAX = 5.0m;  // 5% gap
        private const decimal EXTREME_GAP_THRESHOLD = 10.0m; // 10% gap
        private const decimal GAP_FILL_PROBABILITY_THRESHOLD = 3.0m; // Gaps > 3% less likely to fill

        protected override string CriteriaName => "Gap";

        public GapCriteriaCanonical(IServiceProvider serviceProvider)
            : base(serviceProvider, serviceProvider.GetRequiredService<ITradingLogger>(), "GapCriteriaEvaluator")
        {
        }

        protected override async Task<TradingResult<CriteriaResult>> EvaluateCriteriaAsync(
            MarketData marketData,
            TradingCriteria criteria,
            CriteriaResult result)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Calculate gap percentage
                    decimal gapPercent = CalculateGapPercentage(marketData.Open, marketData.PreviousClose);
                    decimal absGapPercent = Math.Abs(gapPercent);
                    bool isGapUp = gapPercent > 0;

                    // Calculate gap fill metrics
                    decimal gapFillPercentage = CalculateGapFillPercentage(
                        marketData.Open,
                        marketData.PreviousClose,
                        marketData.Low,
                        marketData.High,
                        isGapUp);

                    // Record metrics
                    result.Metrics["GapPercent"] = gapPercent;
                    result.Metrics["AbsoluteGapPercent"] = absGapPercent;
                    result.Metrics["MinimumGapPercent"] = criteria.MinimumGapPercent;
                    result.Metrics["GapDirection"] = isGapUp ? "Up" : "Down";
                    result.Metrics["GapSize"] = marketData.Open - marketData.PreviousClose;
                    result.Metrics["PreviousClose"] = marketData.PreviousClose;
                    result.Metrics["Open"] = marketData.Open;
                    result.Metrics["GapFillPercentage"] = gapFillPercentage;

                    // Check if gap meets minimum threshold
                    if (absGapPercent < criteria.MinimumGapPercent)
                    {
                        result.Passed = false;
                        result.Score = CalculateSubThresholdScore(absGapPercent, criteria.MinimumGapPercent);
                        result.Reason = $"Gap of {gapPercent:F2}% below minimum threshold of {criteria.MinimumGapPercent:F2}%";
                        RecordMetric("BelowThresholdGaps", 1);
                        return TradingResult<CriteriaResult>.Success(result);
                    }

                    // Calculate component scores
                    decimal gapSizeScore = CalculateGapSizeScore(absGapPercent, criteria.MinimumGapPercent);
                    decimal gapQualityScore = CalculateGapQualityScore(absGapPercent);
                    decimal gapFillScore = CalculateGapFillScore(gapFillPercentage, absGapPercent);

                    // Calculate weighted final score
                    decimal finalScore = CalculateWeightedScore(
                        (gapSizeScore, 0.4m),     // Gap size relative to threshold
                        (gapQualityScore, 0.4m),  // Gap quality (optimal range)
                        (gapFillScore, 0.2m)      // Gap fill consideration
                    );

                    // Set results
                    result.Score = Math.Round(finalScore, 2);
                    result.Passed = result.Score >= MINIMUM_PASSING_SCORE;

                    // Additional metrics
                    result.Metrics["GapSizeScore"] = Math.Round(gapSizeScore, 2);
                    result.Metrics["GapQualityScore"] = Math.Round(gapQualityScore, 2);
                    result.Metrics["GapFillScore"] = Math.Round(gapFillScore, 2);
                    result.Metrics["GapType"] = ClassifyGapType(absGapPercent);

                    // Generate reason
                    result.Reason = GenerateGapReason(
                        gapPercent,
                        absGapPercent,
                        gapFillPercentage,
                        result.Passed);

                    // Record performance metrics
                    RecordMetric($"GapDirection.{(isGapUp ? "Up" : "Down")}", 1);
                    RecordMetric($"GapType.{ClassifyGapType(absGapPercent)}", 1);
                    
                    if (absGapPercent >= EXTREME_GAP_THRESHOLD)
                    {
                        RecordMetric("ExtremeGaps", 1);
                    }

                    _logger.LogDebug(
                        $"Gap evaluation completed for {marketData.Symbol}",
                        new
                        {
                            Symbol = marketData.Symbol,
                            GapPercent = gapPercent,
                            GapDirection = isGapUp ? "Up" : "Down",
                            Score = result.Score,
                            Passed = result.Passed
                        });

                    return TradingResult<CriteriaResult>.Success(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error evaluating gap criteria for {marketData.Symbol}", ex);
                    return TradingResult<CriteriaResult>.Failure($"Gap evaluation error: {ex.Message}", ex);
                }
            });
        }

        private decimal CalculateGapPercentage(decimal open, decimal previousClose)
        {
            if (previousClose == 0) return 0m;
            return ((open - previousClose) / previousClose) * 100m;
        }

        private decimal CalculateGapFillPercentage(
            decimal open,
            decimal previousClose,
            decimal low,
            decimal high,
            bool isGapUp)
        {
            if (previousClose == 0) return 0m;

            decimal gapSize = Math.Abs(open - previousClose);
            if (gapSize == 0) return 100m; // No gap to fill

            decimal fillAmount = 0m;

            if (isGapUp)
            {
                // Gap up: check if low reached back toward previous close
                if (low <= previousClose)
                    fillAmount = gapSize; // Complete fill
                else if (low < open)
                    fillAmount = open - low;
            }
            else
            {
                // Gap down: check if high reached back toward previous close
                if (high >= previousClose)
                    fillAmount = gapSize; // Complete fill
                else if (high > open)
                    fillAmount = high - open;
            }

            return (fillAmount / gapSize) * 100m;
        }

        private decimal CalculateSubThresholdScore(decimal absGapPercent, decimal minimumGapPercent)
        {
            // Give partial credit for gaps below threshold
            return Math.Min(60m, (absGapPercent / minimumGapPercent) * 60m);
        }

        private decimal CalculateGapSizeScore(decimal absGapPercent, decimal minimumGapPercent)
        {
            decimal ratio = absGapPercent / minimumGapPercent;

            if (ratio >= 2.0m)
                return 100m; // Double the minimum threshold
            else if (ratio >= 1.5m)
                return 85m + (15m * ((ratio - 1.5m) / 0.5m));
            else
                return 70m + (15m * ((ratio - 1.0m) / 0.5m));
        }

        private decimal CalculateGapQualityScore(decimal absGapPercent)
        {
            // Optimal gap range is 2-5%
            if (absGapPercent >= OPTIMAL_GAP_MIN && absGapPercent <= OPTIMAL_GAP_MAX)
            {
                // Perfect range - highest score
                decimal midpoint = (OPTIMAL_GAP_MIN + OPTIMAL_GAP_MAX) / 2;
                decimal distanceFromMid = Math.Abs(absGapPercent - midpoint);
                decimal maxDistance = (OPTIMAL_GAP_MAX - OPTIMAL_GAP_MIN) / 2;
                return 90m + (10m * (1m - distanceFromMid / maxDistance));
            }
            else if (absGapPercent < OPTIMAL_GAP_MIN)
            {
                // Below optimal range
                return 70m + (20m * (absGapPercent / OPTIMAL_GAP_MIN));
            }
            else if (absGapPercent <= EXTREME_GAP_THRESHOLD)
            {
                // Above optimal but not extreme
                decimal excess = absGapPercent - OPTIMAL_GAP_MAX;
                decimal range = EXTREME_GAP_THRESHOLD - OPTIMAL_GAP_MAX;
                return 90m - (20m * (excess / range));
            }
            else
            {
                // Extreme gap - risky
                return Math.Max(50m, 70m - ((absGapPercent - EXTREME_GAP_THRESHOLD) * 2m));
            }
        }

        private decimal CalculateGapFillScore(decimal gapFillPercentage, decimal absGapPercent)
        {
            // Partial fills can indicate strength or weakness depending on gap size
            if (absGapPercent <= GAP_FILL_PROBABILITY_THRESHOLD)
            {
                // Smaller gaps: no fill is better (shows strength)
                return 100m - (gapFillPercentage * 0.3m);
            }
            else
            {
                // Larger gaps: some fill is healthy (shows support/resistance)
                if (gapFillPercentage >= 20m && gapFillPercentage <= 50m)
                    return 100m; // Healthy partial fill
                else if (gapFillPercentage < 20m)
                    return 80m + (gapFillPercentage); // Some fill is good
                else
                    return Math.Max(60m, 100m - ((gapFillPercentage - 50m) * 0.8m)); // Too much fill
            }
        }

        private string ClassifyGapType(decimal absGapPercent)
        {
            return absGapPercent switch
            {
                < 1m => "Minimal",
                < 2m => "Small",
                < 5m => "Moderate",
                < 10m => "Large",
                _ => "Extreme"
            };
        }

        private string GenerateGapReason(
            decimal gapPercent,
            decimal absGapPercent,
            decimal gapFillPercentage,
            bool passed)
        {
            string direction = gapPercent > 0 ? "up" : "down";
            string gapType = ClassifyGapType(absGapPercent);
            string fillStatus = gapFillPercentage switch
            {
                0m => "unfilled",
                < 25m => "minimally filled",
                < 75m => "partially filled",
                < 100m => "mostly filled",
                _ => "completely filled"
            };

            if (passed)
            {
                return $"{gapType} gap {direction} {absGapPercent:F2}% ({fillStatus})";
            }
            else
            {
                return $"Gap {direction} {absGapPercent:F2}% insufficient for trading";
            }
        }

        protected override TradingResult ValidateInput(MarketData marketData, TradingCriteria criteria)
        {
            var baseValidation = base.ValidateInput(marketData, criteria);
            if (!baseValidation.IsSuccess)
                return baseValidation;

            if (marketData.PreviousClose <= 0)
                return TradingResult.Failure($"Previous close must be positive: {marketData.PreviousClose}");

            if (marketData.Open <= 0)
                return TradingResult.Failure($"Open price must be positive: {marketData.Open}");

            if (criteria.MinimumGapPercent < 0)
                return TradingResult.Failure($"Minimum gap percent cannot be negative: {criteria.MinimumGapPercent}");

            return TradingResult.Success();
        }
    }
}