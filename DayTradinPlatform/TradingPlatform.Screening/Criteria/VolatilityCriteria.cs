// File: TradingPlatform.Screening.Criteria\VolatilityCriteria.cs

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Screening.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Screening.Criteria
{
    /// <summary>
    /// Evaluates volatility-based trading criteria using mathematically correct, standards-compliant logic.
    /// </summary>
    public class VolatilityCriteria
    {
        private readonly ITradingLogger _logger;

        public VolatilityCriteria(ITradingLogger logger)
        {
            _logger = logger;
        }

        public Task<CriteriaResult> EvaluateVolatilityAsync(MarketData marketData, TradingCriteria criteria)
        {
            var result = new CriteriaResult
            {
                CriteriaName = "Volatility",
                EvaluatedAt = DateTime.UtcNow
            };

            try
            {
                decimal atr = 0m;
                if (marketData is null)
                {
                    result.Passed = false;
                    result.Score = 0m;
                    result.Reason = "Market data unavailable.";
                    return Task.FromResult(result);
                }

                // Example: ATR is supplied in MarketData.Metrics dictionary (MVP stub)
                if (marketData is MarketData md && md.GetType().GetProperty("ATR") != null)
                {
                    var atrObj = md.GetType().GetProperty("ATR")?.GetValue(md, null);
                    if (atrObj is decimal atrVal) atr = atrVal;
                    else if (atrObj is double atrD) atr = (decimal)atrD;
                    else if (atrObj is float atrF) atr = (decimal)atrF;
                    else if (atrObj is long atrL) atr = atrL;
                    else if (atrObj is int atrI) atr = atrI;
                }

                result.Metrics["ATR"] = atr;

                if (atr >= criteria.MinimumATR)
                {
                    result.Passed = true;
                    result.Score = Math.Min(atr / (criteria.MinimumATR * 2m), 1.0m);
                    result.Reason = $"ATR ${atr:F2} meets threshold (${criteria.MinimumATR:F2}).";
                }
                else
                {
                    result.Passed = false;
                    result.Score = Math.Max(0m, atr / criteria.MinimumATR * 0.6m);
                    result.Reason = $"ATR ${atr:F2} below threshold (${criteria.MinimumATR:F2}).";
                }

                TradingLogOrchestrator.Instance.LogInfo($"Volatility evaluation for {marketData.Symbol}: ATR=${atr:F2}, Score={result.Score:F2}, Passed={result.Passed}");
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Error evaluating volatility criteria for {marketData?.Symbol ?? "N/A"}", ex);
                result.Passed = false;
                result.Score = 0m;
                result.Reason = $"Evaluation error: {ex.Message}";
                return Task.FromResult(result);
            }
        }
    }
}

// Total Lines: 61
