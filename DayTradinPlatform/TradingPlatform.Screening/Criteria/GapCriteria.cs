// File: TradingPlatform.Screening.Criteria\GapCriteria.cs

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Screening.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Screening.Criteria
{
    /// <summary>
    /// Evaluates gap-based trading criteria using mathematically correct, standards-compliant logic.
    /// </summary>
    public class GapCriteria
    {
        private readonly ILogger _logger;

        public GapCriteria(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<CriteriaResult> EvaluateGapAsync(MarketData marketData, TradingCriteria criteria)
        {
            var result = new CriteriaResult
            {
                CriteriaName = "Gap",
                EvaluatedAt = DateTime.UtcNow
            };

            try
            {
                if (marketData.PreviousClose == 0m)
                {
                    result.Passed = false;
                    result.Score = 0m;
                    result.Reason = "Previous close is zero�cannot compute gap.";
                    result.Metrics["GapPercent"] = 0m;
                    return result;
                }

                var gapPercent = ((marketData.Open - marketData.PreviousClose) / marketData.PreviousClose) * 100m;
                result.Metrics["GapPercent"] = gapPercent;

                if (Math.Abs(gapPercent) >= criteria.MinimumGapPercent)
                {
                    result.Passed = true;
                    result.Score = Math.Min(Math.Abs(gapPercent) / (criteria.MinimumGapPercent * 2m), 1.0m);
                    result.Reason = $"Gap of {gapPercent:F2}% meets threshold ({criteria.MinimumGapPercent:F2}%).";
                }
                else
                {
                    result.Passed = false;
                    result.Score = Math.Max(0m, Math.Abs(gapPercent) / criteria.MinimumGapPercent * 0.6m);
                    result.Reason = $"Gap of {gapPercent:F2}% below threshold ({criteria.MinimumGapPercent:F2}%).";
                }

                TradingLogOrchestrator.Instance.LogInfo($"Gap evaluation for {marketData.Symbol}: Gap={gapPercent:F2}%, Score={result.Score:F2}, Passed={result.Passed}");
                return result;
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError(ex, $"Error evaluating gap criteria for {marketData.Symbol}");
                result.Passed = false;
                result.Score = 0m;
                result.Reason = $"Evaluation error: {ex.Message}";
                return result;
            }
        }
    }
}

// Total Lines: 54
