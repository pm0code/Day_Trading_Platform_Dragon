// File: TradingPlatform.Screening.Criteria\VolumeCriteriaCanonical.cs

using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Models;
using TradingPlatform.Screening.Models;

namespace TradingPlatform.Screening.Criteria
{
    /// <summary>
    /// Canonical implementation of volume-based trading criteria evaluation.
    /// Evaluates stocks based on absolute volume, relative volume, and liquidity metrics.
    /// </summary>
    public class VolumeCriteriaCanonical : CanonicalCriteriaEvaluator<TradingCriteria>
    {
        private const decimal VOLUME_SCORE_WEIGHT = 0.6m;
        private const decimal RELATIVE_VOLUME_SCORE_WEIGHT = 0.4m;
        private const decimal MINIMUM_PASSING_SCORE = 70m;
        private const decimal HIGH_VOLUME_MULTIPLIER = 2.0m;
        private const decimal EXTREME_VOLUME_MULTIPLIER = 5.0m;

        protected override string CriteriaName => "Volume";

        public VolumeCriteriaCanonical(IServiceProvider serviceProvider)
            : base(serviceProvider, serviceProvider.GetRequiredService<ITradingLogger>(), "VolumeCriteriaEvaluator")
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
                    // Calculate relative volume
                    decimal relativeVolume = marketData.AverageDailyVolume > 0
                        ? (decimal)marketData.Volume / marketData.AverageDailyVolume
                        : 0m;

                    // Record metrics
                    result.Metrics["CurrentVolume"] = marketData.Volume;
                    result.Metrics["MinimumVolume"] = criteria.MinimumVolume;
                    result.Metrics["AverageDailyVolume"] = marketData.AverageDailyVolume;
                    result.Metrics["RelativeVolume"] = relativeVolume;
                    result.Metrics["MinimumRelativeVolume"] = criteria.MinimumRelativeVolume;

                    // Calculate absolute volume score
                    decimal volumeScore = CalculateAbsoluteVolumeScore(
                        marketData.Volume,
                        criteria.MinimumVolume);

                    // Calculate relative volume score
                    decimal relativeVolumeScore = CalculateRelativeVolumeScore(
                        relativeVolume,
                        criteria.MinimumRelativeVolume);

                    // Calculate liquidity score (bonus for consistent high volume)
                    decimal liquidityScore = CalculateLiquidityScore(
                        marketData.Volume,
                        marketData.AverageDailyVolume);

                    // Calculate weighted final score
                    decimal finalScore = CalculateWeightedScore(
                        (volumeScore, VOLUME_SCORE_WEIGHT),
                        (relativeVolumeScore, RELATIVE_VOLUME_SCORE_WEIGHT),
                        (liquidityScore, 0.2m) // Bonus weight for liquidity
                    );

                    // Normalize to 0-100 scale
                    finalScore = Math.Min(100m, finalScore);

                    // Set results
                    result.Score = Math.Round(finalScore, 2);
                    result.Passed = result.Score >= MINIMUM_PASSING_SCORE;

                    // Additional metrics
                    result.Metrics["VolumeScore"] = Math.Round(volumeScore, 2);
                    result.Metrics["RelativeVolumeScore"] = Math.Round(relativeVolumeScore, 2);
                    result.Metrics["LiquidityScore"] = Math.Round(liquidityScore, 2);
                    result.Metrics["WeightedScore"] = Math.Round(finalScore, 2);

                    // Generate reason
                    result.Reason = GenerateVolumeReason(
                        marketData.Volume,
                        relativeVolume,
                        result.Passed,
                        criteria);

                    // Record performance metrics
                    if (relativeVolume >= EXTREME_VOLUME_MULTIPLIER)
                    {
                        RecordMetric("ExtremeVolumeEvents", 1);
                    }
                    else if (relativeVolume >= HIGH_VOLUME_MULTIPLIER)
                    {
                        RecordMetric("HighVolumeEvents", 1);
                    }

                    RecordMetric($"VolumeRange.{GetVolumeRange(marketData.Volume)}", 1);

                    _logger.LogDebug(
                        $"Volume evaluation completed for {marketData.Symbol}",
                        new
                        {
                            Symbol = marketData.Symbol,
                            Volume = marketData.Volume,
                            RelativeVolume = relativeVolume,
                            Score = result.Score,
                            Passed = result.Passed
                        });

                    return TradingResult<CriteriaResult>.Success(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error evaluating volume criteria for {marketData.Symbol}", ex);
                    return TradingResult<CriteriaResult>.Failure($"Volume evaluation error: {ex.Message}", ex);
                }
            });
        }

        private decimal CalculateAbsoluteVolumeScore(long currentVolume, long minimumVolume)
        {
            if (currentVolume <= 0) return 0m;
            if (minimumVolume <= 0) return 100m; // No minimum requirement

            decimal ratio = (decimal)currentVolume / minimumVolume;

            if (ratio >= HIGH_VOLUME_MULTIPLIER)
                return 100m;
            else if (ratio >= 1.0m)
                return 70m + (30m * ((ratio - 1m) / (HIGH_VOLUME_MULTIPLIER - 1m)));
            else
                return 70m * ratio;
        }

        private decimal CalculateRelativeVolumeScore(decimal relativeVolume, decimal minimumRelativeVolume)
        {
            if (minimumRelativeVolume <= 0) return 100m; // No relative requirement

            if (relativeVolume >= minimumRelativeVolume * 2)
                return 100m;
            else if (relativeVolume >= minimumRelativeVolume)
                return 70m + (30m * ((relativeVolume - minimumRelativeVolume) / minimumRelativeVolume));
            else
                return 70m * (relativeVolume / minimumRelativeVolume);
        }

        private decimal CalculateLiquidityScore(long currentVolume, long averageVolume)
        {
            if (averageVolume <= 0) return 50m; // Neutral if no average available

            // High liquidity if current volume is close to or above average
            decimal ratio = (decimal)currentVolume / averageVolume;

            if (ratio >= 1.5m)
                return 100m; // Excellent liquidity
            else if (ratio >= 0.8m)
                return 80m + (20m * ((ratio - 0.8m) / 0.7m));
            else
                return 80m * (ratio / 0.8m);
        }

        private string GenerateVolumeReason(long volume, decimal relativeVolume, bool passed, TradingCriteria criteria)
        {
            if (passed)
            {
                if (relativeVolume >= EXTREME_VOLUME_MULTIPLIER)
                    return $"Extreme volume surge: {volume:N0} shares ({relativeVolume:F1}x average)";
                else if (relativeVolume >= HIGH_VOLUME_MULTIPLIER)
                    return $"High volume activity: {volume:N0} shares ({relativeVolume:F1}x average)";
                else
                    return $"Good volume: {volume:N0} shares ({relativeVolume:F1}x average)";
            }
            else
            {
                var issues = new List<string>();
                
                if (volume < criteria.MinimumVolume)
                    issues.Add($"Volume {volume:N0} below minimum {criteria.MinimumVolume:N0}");
                
                if (relativeVolume < criteria.MinimumRelativeVolume)
                    issues.Add($"Relative volume {relativeVolume:F1}x below minimum {criteria.MinimumRelativeVolume:F1}x");

                return string.Join("; ", issues);
            }
        }

        private string GetVolumeRange(long volume)
        {
            return volume switch
            {
                < 100_000 => "VeryLow",
                < 500_000 => "Low",
                < 1_000_000 => "Moderate",
                < 5_000_000 => "High",
                < 10_000_000 => "VeryHigh",
                _ => "Extreme"
            };
        }

        protected override TradingResult ValidateInput(MarketData marketData, TradingCriteria criteria)
        {
            var baseValidation = base.ValidateInput(marketData, criteria);
            if (!baseValidation.IsSuccess)
                return baseValidation;

            if (marketData.Volume < 0)
                return TradingResult.Failure($"Volume cannot be negative: {marketData.Volume}");

            if (marketData.AverageDailyVolume < 0)
                return TradingResult.Failure($"Average daily volume cannot be negative: {marketData.AverageDailyVolume}");

            if (criteria.MinimumVolume < 0)
                return TradingResult.Failure($"Minimum volume cannot be negative: {criteria.MinimumVolume}");

            if (criteria.MinimumRelativeVolume < 0)
                return TradingResult.Failure($"Minimum relative volume cannot be negative: {criteria.MinimumRelativeVolume}");

            return TradingResult.Success();
        }
    }
}