// File: TradingPlatform.Screening.Engines\ScreeningOrchestrator.cs

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Screening.Interfaces;
using TradingPlatform.Screening.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Screening.Engines
{
    /// <summary>
    /// Orchestrates the evaluation of all trading criteria and aggregates results in a mathematically correct, standards-compliant manner.
    /// </summary>
    public class ScreeningOrchestrator
    {
        private readonly IEnumerable<ICriteriaEvaluator> _criteriaEvaluators;
        private readonly ITradingLogger _logger;

        public ScreeningOrchestrator(IEnumerable<ICriteriaEvaluator> criteriaEvaluators, ITradingLogger logger)
        {
            _criteriaEvaluators = criteriaEvaluators;
            _logger = logger;
        }

        /// <summary>
        /// Evaluates all criteria for a symbol and returns an aggregated ScreeningResult.
        /// </summary>
        public async Task<ScreeningResult> EvaluateAllAsync(MarketData marketData, TradingCriteria criteria)
        {
            var result = new ScreeningResult
            {
                Symbol = marketData.Symbol,
                MarketData = marketData,
                ScreenedAt = DateTime.UtcNow
            };

            try
            {
                var criteriaResults = new List<CriteriaResult>();

                foreach (var evaluator in _criteriaEvaluators)
                {
                    var evalResult = await evaluator.EvaluateAsync(marketData, criteria);
                    if (evalResult != null)
                        criteriaResults.Add(evalResult);
                }

                result.CriteriaResults = criteriaResults;
                result.OverallScore = CalculateOverallScore(criteriaResults);
                result.MeetsCriteria = DetermineIfCriteriaMet(criteriaResults, criteria);
                result.AlertLevel = DetermineAlertLevel(result.OverallScore, criteriaResults);
                result.ConfidenceLevel = CalculateConfidenceLevel(criteriaResults);
                result.RecommendedAction = DetermineRecommendedAction(result, marketData);

                result.PassedCriteria = criteriaResults.Where(c => c.Passed).Select(c => c.CriteriaName).ToList();
                result.FailedCriteria = criteriaResults.Where(c => !c.Passed).Select(c => c.CriteriaName).ToList();

                result.AdditionalMetrics["CriteriaCount"] = criteriaResults.Count;
                result.AdditionalMetrics["PassedCount"] = result.PassedCriteria.Count;

                TradingLogOrchestrator.Instance.LogInfo($"Orchestrated evaluation for {marketData.Symbol}: Score={result.OverallScore:F2}, Passed={result.MeetsCriteria}");
                return result;
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Error orchestrating criteria evaluation for {marketData.Symbol}", ex);
                result.MeetsCriteria = false;
                result.OverallScore = 0m;
                result.CriteriaResults = new List<CriteriaResult>
                {
                    new CriteriaResult
                    {
                        CriteriaName = "Error",
                        Passed = false,
                        Score = 0m,
                        Reason = ex.Message
                    }
                };
                return result;
            }
        }

        private decimal CalculateOverallScore(List<CriteriaResult> criteriaResults)
        {
            if (!criteriaResults.Any()) return 0m;
            // Simple average, can be replaced with weighted logic as needed
            return criteriaResults.Average(r => r.Score);
        }

        private bool DetermineIfCriteriaMet(List<CriteriaResult> criteriaResults, TradingCriteria criteria)
        {
            // Example: at least 2 core criteria must pass and overall score >= 0.7
            var corePassed = criteriaResults.Count(r => r.Passed);
            var overallScore = CalculateOverallScore(criteriaResults);
            return corePassed >= 2 && overallScore >= 0.7m;
        }

        private AlertLevel DetermineAlertLevel(decimal overallScore, List<CriteriaResult> criteriaResults)
        {
            var passedCount = criteriaResults.Count(r => r.Passed);
            if (overallScore >= 0.90m && passedCount >= 4) return AlertLevel.Critical;
            if (overallScore >= 0.80m && passedCount >= 3) return AlertLevel.High;
            if (overallScore >= 0.70m && passedCount >= 2) return AlertLevel.Medium;
            if (overallScore >= 0.60m) return AlertLevel.Low;
            return AlertLevel.None;
        }

        private decimal CalculateConfidenceLevel(List<CriteriaResult> criteriaResults)
        {
            var passedCount = criteriaResults.Count(r => r.Passed);
            var totalCount = criteriaResults.Count;
            var averageScore = criteriaResults.Average(r => r.Score);
            var passRate = totalCount > 0 ? (decimal)passedCount / totalCount : 0m;
            return (passRate * 0.6m) + (averageScore * 0.4m);
        }

        private string DetermineRecommendedAction(ScreeningResult result, MarketData marketData)
        {
            if (!result.MeetsCriteria) return "IGNORE";
            if (result.OverallScore >= 0.85m && result.AlertLevel >= AlertLevel.High)
                return marketData.ChangePercent > 0m ? "BUY" : "SELL";
            if (result.OverallScore >= 0.70m)
                return "WATCH";
            return "IGNORE";
        }
    }
}

// Total Lines: 95
