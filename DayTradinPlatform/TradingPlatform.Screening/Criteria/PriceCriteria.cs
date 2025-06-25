// File: TradingPlatform.Screening.Criteria\PriceCriteria.cs

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Screening.Models;
using ScreeningCriteriaResult = TradingPlatform.Screening.Models.CriteriaResult;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Screening.Criteria
{
    /// <summary>
    /// Evaluates price-based trading criteria using mathematically correct, standards-compliant logic.
    /// </summary>
    public class PriceCriteria
    {
        private readonly ITradingLogger _logger;

        public PriceCriteria(ITradingLogger logger)
        {
            _logger = logger;
        }

        public Task<ScreeningCriteriaResult> EvaluatePriceAsync(MarketData marketData, TradingCriteria criteria)
        {
            var result = new ScreeningCriteriaResult
            {
                CriteriaName = "Price",
                EvaluatedAt = DateTime.UtcNow
            };

            try
            {
                decimal price = marketData.Price;
                result.Metrics["Price"] = price;
                result.Metrics["MinPrice"] = criteria.MinimumPrice;
                result.Metrics["MaxPrice"] = criteria.MaximumPrice;

                bool inRange = price >= criteria.MinimumPrice && price <= criteria.MaximumPrice;
                bool pennyStock = price < 5.00m;

                if (!criteria.EnablePennyStocks && pennyStock)
                {
                    result.Passed = false;
                    result.Score = 0m;
                    result.Reason = $"Price ${price:F2} is a penny stock and penny stocks are disabled.";
                }
                else if (inRange)
                {
                    decimal range = criteria.MaximumPrice - criteria.MinimumPrice;
                    decimal midpoint = criteria.MinimumPrice + range / 2m;
                    decimal distanceFromMid = Math.Abs(price - midpoint);
                    decimal score = 1.0m - (distanceFromMid / (range / 2m));
                    result.Passed = true;
                    result.Score = Math.Max(0.7m, Math.Min(1.0m, score));
                    result.Reason = $"Price ${price:F2} within range (${criteria.MinimumPrice:F2}�${criteria.MaximumPrice:F2}).";
                }
                else
                {
                    result.Passed = false;
                    result.Score = 0m;
                    if (price < criteria.MinimumPrice)
                        result.Reason = $"Price ${price:F2} below minimum (${criteria.MinimumPrice:F2}).";
                    else
                        result.Reason = $"Price ${price:F2} above maximum (${criteria.MaximumPrice:F2}).";
                }

                TradingLogOrchestrator.Instance.LogInfo($"Price evaluation for {marketData.Symbol}: Price=${price:F2}, Score={result.Score:F2}, Passed={result.Passed}");
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Error evaluating price criteria for {marketData.Symbol}", ex);
                result.Passed = false;
                result.Score = 0m;
                result.Reason = $"Evaluation error: {ex.Message}";
                return Task.FromResult(result);
            }
        }
    }
}

// Total Lines: 62
