// File: TradingPlatform.Screening.Criteria\PriceCriteria.cs

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Screening.Models;

namespace TradingPlatform.Screening.Criteria
{
    /// <summary>
    /// Evaluates price-based trading criteria using mathematically correct, standards-compliant logic.
    /// </summary>
    public class PriceCriteria
    {
        private readonly ILogger _logger;

        public PriceCriteria(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<CriteriaResult> EvaluatePriceAsync(MarketData marketData, TradingCriteria criteria)
        {
            var result = new CriteriaResult
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

                _logger.LogTrace($"Price evaluation for {marketData.Symbol}: Price=${price:F2}, Score={result.Score:F2}, Passed={result.Passed}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error evaluating price criteria for {marketData.Symbol}");
                result.Passed = false;
                result.Score = 0m;
                result.Reason = $"Evaluation error: {ex.Message}";
                return result;
            }
        }
    }
}

// Total Lines: 62
