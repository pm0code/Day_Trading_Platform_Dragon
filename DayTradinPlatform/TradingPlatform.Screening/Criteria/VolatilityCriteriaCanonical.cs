// File: TradingPlatform.Screening.Criteria\VolatilityCriteriaCanonical.cs

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
    /// Canonical implementation of volatility-based trading criteria evaluation.
    /// Evaluates stocks based on Average True Range (ATR), price swings, and intraday movement potential.
    /// </summary>
    public class VolatilityCriteriaCanonical : CanonicalCriteriaEvaluator<TradingCriteria>
    {
        private const decimal MINIMUM_PASSING_SCORE = 70m;
        private const decimal OPTIMAL_ATR_MULTIPLIER = 2.0m;
        private const decimal HIGH_VOLATILITY_MULTIPLIER = 3.0m;
        private const decimal PRICE_BASED_ATR_PERCENTAGE = 0.02m; // 2% of price as baseline

        protected override string CriteriaName => "Volatility";

        public VolatilityCriteriaCanonical(IServiceProvider serviceProvider)
            : base(serviceProvider, serviceProvider.GetRequiredService<ITradingLogger>(), "VolatilityCriteriaEvaluator")
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
                    // Extract ATR from market data
                    decimal atr = ExtractATR(marketData);
                    decimal atrPercentage = marketData.Price > 0 ? (atr / marketData.Price) * 100 : 0;

                    // Calculate daily range if available
                    decimal dailyRange = 0m;
                    decimal dailyRangePercentage = 0m;
                    if (marketData.High > 0 && marketData.Low > 0)
                    {
                        dailyRange = marketData.High - marketData.Low;
                        dailyRangePercentage = marketData.Low > 0 ? (dailyRange / marketData.Low) * 100 : 0;
                    }

                    // Record metrics
                    result.Metrics["ATR"] = atr;
                    result.Metrics["ATRPercentage"] = atrPercentage;
                    result.Metrics["MinimumATR"] = criteria.MinimumATR;
                    result.Metrics["DailyRange"] = dailyRange;
                    result.Metrics["DailyRangePercentage"] = dailyRangePercentage;
                    result.Metrics["High"] = marketData.High;
                    result.Metrics["Low"] = marketData.Low;

                    // Calculate ATR score
                    decimal atrScore = CalculateATRScore(atr, criteria.MinimumATR);

                    // Calculate price-based volatility score
                    decimal priceVolatilityScore = CalculatePriceVolatilityScore(
                        atrPercentage,
                        marketData.Price);

                    // Calculate intraday movement score
                    decimal intradayScore = CalculateIntradayScore(
                        dailyRangePercentage,
                        atrPercentage);

                    // Calculate weighted final score
                    decimal finalScore = CalculateWeightedScore(
                        (atrScore, 0.5m),              // ATR is primary factor
                        (priceVolatilityScore, 0.3m),  // Price-relative volatility
                        (intradayScore, 0.2m)           // Intraday movement potential
                    );

                    // Set results
                    result.Score = Math.Round(finalScore, 2);
                    result.Passed = result.Score >= MINIMUM_PASSING_SCORE && atr >= criteria.MinimumATR;

                    // Additional metrics
                    result.Metrics["ATRScore"] = Math.Round(atrScore, 2);
                    result.Metrics["PriceVolatilityScore"] = Math.Round(priceVolatilityScore, 2);
                    result.Metrics["IntradayScore"] = Math.Round(intradayScore, 2);
                    result.Metrics["VolatilityRating"] = GetVolatilityRating(atrPercentage);

                    // Generate reason
                    result.Reason = GenerateVolatilityReason(
                        atr,
                        atrPercentage,
                        dailyRangePercentage,
                        result.Passed,
                        criteria);

                    // Record performance metrics
                    RecordMetric($"VolatilityLevel.{GetVolatilityLevel(atrPercentage)}", 1);
                    if (atrPercentage >= HIGH_VOLATILITY_MULTIPLIER * PRICE_BASED_ATR_PERCENTAGE * 100)
                    {
                        RecordMetric("HighVolatilityStocks", 1);
                    }

                    _logger.LogDebug(
                        $"Volatility evaluation completed for {marketData.Symbol}",
                        new
                        {
                            Symbol = marketData.Symbol,
                            ATR = atr,
                            ATRPercentage = atrPercentage,
                            Score = result.Score,
                            Passed = result.Passed
                        });

                    return TradingResult<CriteriaResult>.Success(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error evaluating volatility criteria for {marketData.Symbol}", ex);
                    return TradingResult<CriteriaResult>.Failure($"Volatility evaluation error: {ex.Message}", ex);
                }
            });
        }

        private decimal ExtractATR(MarketData marketData)
        {
            // Try to get ATR from various possible sources
            
            // Check if MarketData has an ATR property
            var atrProperty = marketData.GetType().GetProperty("ATR");
            if (atrProperty != null)
            {
                var atrValue = atrProperty.GetValue(marketData);
                if (atrValue != null)
                {
                    return Convert.ToDecimal(atrValue);
                }
            }

            // Check Metrics dictionary if available
            if (marketData is IHasMetrics metricsData && metricsData.Metrics.ContainsKey("ATR"))
            {
                return Convert.ToDecimal(metricsData.Metrics["ATR"]);
            }

            // Calculate simple volatility estimate from high/low if ATR not available
            if (marketData.High > 0 && marketData.Low > 0)
            {
                // Simple ATR approximation: daily range
                return marketData.High - marketData.Low;
            }

            // Fallback: estimate based on typical price movement (2% of price)
            return marketData.Price * PRICE_BASED_ATR_PERCENTAGE;
        }

        private decimal CalculateATRScore(decimal atr, decimal minimumATR)
        {
            if (minimumATR <= 0) return 100m; // No minimum requirement

            decimal ratio = atr / minimumATR;

            if (ratio >= OPTIMAL_ATR_MULTIPLIER)
                return 100m;
            else if (ratio >= 1.0m)
                return 70m + (30m * ((ratio - 1m) / (OPTIMAL_ATR_MULTIPLIER - 1m)));
            else
                return 70m * ratio;
        }

        private decimal CalculatePriceVolatilityScore(decimal atrPercentage, decimal price)
        {
            // Ideal volatility range for day trading: 2-5% of price
            decimal idealMinPercentage = 2m;
            decimal idealMaxPercentage = 5m;

            if (atrPercentage >= idealMinPercentage && atrPercentage <= idealMaxPercentage)
            {
                // Perfect range - highest score
                decimal midpoint = (idealMinPercentage + idealMaxPercentage) / 2;
                decimal distanceFromMid = Math.Abs(atrPercentage - midpoint);
                decimal maxDistance = (idealMaxPercentage - idealMinPercentage) / 2;
                return 90m + (10m * (1m - distanceFromMid / maxDistance));
            }
            else if (atrPercentage < idealMinPercentage)
            {
                // Too low volatility
                return 70m * (atrPercentage / idealMinPercentage);
            }
            else
            {
                // Too high volatility - gradually decrease score
                decimal excess = atrPercentage - idealMaxPercentage;
                return Math.Max(50m, 90m - (excess * 5m));
            }
        }

        private decimal CalculateIntradayScore(decimal dailyRangePercentage, decimal atrPercentage)
        {
            if (dailyRangePercentage <= 0) return 50m; // No data, neutral score

            // Compare daily range to ATR - looking for stocks that move
            decimal rangeToAtrRatio = atrPercentage > 0 ? dailyRangePercentage / atrPercentage : 0;

            if (rangeToAtrRatio >= 0.8m && rangeToAtrRatio <= 1.5m)
            {
                // Good intraday movement relative to average
                return 100m;
            }
            else if (rangeToAtrRatio < 0.8m)
            {
                // Lower than usual movement
                return 100m * rangeToAtrRatio / 0.8m;
            }
            else
            {
                // Excessive movement
                return Math.Max(60m, 100m - ((rangeToAtrRatio - 1.5m) * 20m));
            }
        }

        private string GetVolatilityRating(decimal atrPercentage)
        {
            return atrPercentage switch
            {
                < 1m => "Very Low",
                < 2m => "Low",
                < 3m => "Moderate",
                < 5m => "High",
                < 7m => "Very High",
                _ => "Extreme"
            };
        }

        private string GetVolatilityLevel(decimal atrPercentage)
        {
            return atrPercentage switch
            {
                < 2m => "Low",
                < 5m => "Medium",
                _ => "High"
            };
        }

        private string GenerateVolatilityReason(
            decimal atr, 
            decimal atrPercentage, 
            decimal dailyRangePercentage,
            bool passed, 
            TradingCriteria criteria)
        {
            string volatilityRating = GetVolatilityRating(atrPercentage);

            if (passed)
            {
                if (atrPercentage >= 3m && atrPercentage <= 5m)
                    return $"Optimal volatility: ATR ${atr:F2} ({atrPercentage:F1}% of price) - {volatilityRating} rating";
                else
                    return $"Good volatility: ATR ${atr:F2} ({atrPercentage:F1}% of price) - {volatilityRating} rating";
            }
            else
            {
                if (atr < criteria.MinimumATR)
                    return $"ATR ${atr:F2} below minimum ${criteria.MinimumATR:F2} - {volatilityRating} volatility";
                else
                    return $"Volatility score too low: ATR ${atr:F2} ({atrPercentage:F1}% of price) - {volatilityRating} rating";
            }
        }

        protected override TradingResult ValidateInput(MarketData marketData, TradingCriteria criteria)
        {
            var baseValidation = base.ValidateInput(marketData, criteria);
            if (!baseValidation.IsSuccess)
                return baseValidation;

            if (criteria.MinimumATR < 0)
                return TradingResult.Failure($"Minimum ATR cannot be negative: {criteria.MinimumATR}");

            return TradingResult.Success();
        }
    }

    /// <summary>
    /// Interface to support metrics dictionary on MarketData if needed
    /// </summary>
    public interface IHasMetrics
    {
        Dictionary<string, object> Metrics { get; }
    }
}