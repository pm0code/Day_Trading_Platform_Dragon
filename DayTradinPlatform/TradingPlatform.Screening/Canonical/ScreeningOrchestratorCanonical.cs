using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Screening.Interfaces;
using TradingPlatform.Screening.Models;

namespace TradingPlatform.Screening.Canonical
{
    /// <summary>
    /// Canonical implementation of the screening orchestrator that coordinates multiple criteria evaluators.
    /// </summary>
    public class ScreeningOrchestratorCanonical : CanonicalOrchestrator<ICriteriaEvaluator, 
        (MarketData marketData, TradingCriteria criteria), CriteriaResult, ScreeningResult>
    {
        private readonly IServiceProvider _serviceProvider;

        public ScreeningOrchestratorCanonical(
            IServiceProvider serviceProvider,
            IEnumerable<ICriteriaEvaluator> criteriaEvaluators,
            ITradingLogger logger)
            : base(criteriaEvaluators, logger, "ScreeningOrchestrator")
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Evaluates all criteria for a symbol and returns an aggregated ScreeningResult.
        /// </summary>
        public async Task<ScreeningResult> EvaluateAllAsync(MarketData marketData, TradingCriteria criteria)
        {
            var result = await OrchestratAsync((marketData, criteria));
            
            if (result.IsSuccess && result.Value != null)
            {
                return result.Value;
            }

            // Return error result
            return new ScreeningResult
            {
                Symbol = marketData.Symbol,
                MarketData = marketData,
                ScreenedAt = DateTime.UtcNow,
                MeetsCriteria = false,
                OverallScore = 0m,
                CriteriaResults = new List<CriteriaResult>
                {
                    new CriteriaResult
                    {
                        CriteriaName = "Error",
                        Passed = false,
                        Score = 0m,
                        Reason = result.Error?.Message ?? "Unknown error"
                    }
                }
            };
        }

        protected override async Task<TradingResult<CriteriaResult>> EvaluateWithEvaluatorAsync(
            ICriteriaEvaluator evaluator,
            (MarketData marketData, TradingCriteria criteria) input,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await evaluator.EvaluateAsync(input.marketData, input.criteria);
                
                if (result != null)
                {
                    return TradingResult<CriteriaResult>.Success(result);
                }

                return TradingResult<CriteriaResult>.Failure(
                    TradingError.ErrorCodes.ValidationError,
                    $"Evaluator {GetEvaluatorName(evaluator)} returned null result");
            }
            catch (Exception ex)
            {
                return TradingResult<CriteriaResult>.Failure(
                    TradingError.ErrorCodes.SystemError,
                    $"Evaluator {GetEvaluatorName(evaluator)} failed",
                    ex);
            }
        }

        protected override async Task<TradingResult<ScreeningResult>> AggregateResultsAsync(
            (MarketData marketData, TradingCriteria criteria) input,
            IList<CriteriaResult> evaluationResults,
            IList<string> failedEvaluators,
            CancellationToken cancellationToken)
        {
            var result = new ScreeningResult
            {
                Symbol = input.marketData.Symbol,
                MarketData = input.marketData,
                ScreenedAt = DateTime.UtcNow,
                CriteriaResults = evaluationResults.ToList()
            };

            // Calculate scores and determine results
            result.OverallScore = CalculateOverallScore(evaluationResults);
            result.MeetsCriteria = DetermineIfCriteriaMet(evaluationResults, input.criteria);
            result.AlertLevel = DetermineAlertLevel(result.OverallScore, evaluationResults);
            result.ConfidenceLevel = CalculateConfidenceLevel(evaluationResults, failedEvaluators);
            result.RecommendedAction = DetermineRecommendedAction(result, input.marketData);

            // Set passed/failed criteria lists
            result.PassedCriteria = evaluationResults
                .Where(c => c.Passed)
                .Select(c => c.CriteriaName)
                .ToList();
            
            result.FailedCriteria = evaluationResults
                .Where(c => !c.Passed)
                .Select(c => c.CriteriaName)
                .ToList();

            // Add failed evaluators to failed criteria
            result.FailedCriteria.AddRange(failedEvaluators);

            // Set additional metrics
            result.AdditionalMetrics["CriteriaCount"] = evaluationResults.Count;
            result.AdditionalMetrics["PassedCount"] = result.PassedCriteria.Count;
            result.AdditionalMetrics["FailedEvaluatorCount"] = failedEvaluators.Count;
            result.AdditionalMetrics["TotalEvaluatorCount"] = Evaluators.Count();

            LogInfo($"Orchestrated evaluation for {input.marketData.Symbol}: " +
                   $"Score={result.OverallScore:F2}, Passed={result.MeetsCriteria}, " +
                   $"Alert={result.AlertLevel}, Confidence={result.ConfidenceLevel:F2}");

            return TradingResult<ScreeningResult>.Success(result);
        }

        protected override string GetEvaluatorName(ICriteriaEvaluator evaluator)
        {
            return evaluator.GetType().Name.Replace("CriteriaCanonical", "")
                                           .Replace("Criteria", "");
        }

        #region Scoring Logic

        private decimal CalculateOverallScore(IList<CriteriaResult> criteriaResults)
        {
            if (!criteriaResults.Any()) return 0m;

            // Define weights for different criteria
            var weights = new Dictionary<string, decimal>
            {
                ["Price"] = 0.25m,
                ["Volume"] = 0.25m,
                ["Volatility"] = 0.20m,
                ["Gap"] = 0.15m,
                ["News"] = 0.15m
            };

            var weightedScores = criteriaResults.Select(r => 
            {
                var weight = weights.ContainsKey(r.CriteriaName) 
                    ? weights[r.CriteriaName] 
                    : 1m / criteriaResults.Count;
                return (r.Score, weight);
            });

            return CalculateWeightedScore(weightedScores);
        }

        private bool DetermineIfCriteriaMet(IList<CriteriaResult> criteriaResults, TradingCriteria criteria)
        {
            // Require at least 3 core criteria to pass
            var corePassed = criteriaResults.Count(r => r.Passed);
            var overallScore = CalculateOverallScore(criteriaResults);
            
            // Must have majority of criteria passing and score above threshold
            var majorityPassed = corePassed >= Math.Ceiling(criteriaResults.Count / 2.0);
            var scoreThresholdMet = overallScore >= 0.7m;
            
            return majorityPassed && scoreThresholdMet;
        }

        private AlertLevel DetermineAlertLevel(decimal overallScore, IList<CriteriaResult> criteriaResults)
        {
            var passedCount = criteriaResults.Count(r => r.Passed);
            var totalCount = criteriaResults.Count;
            var passRate = totalCount > 0 ? (decimal)passedCount / totalCount : 0m;

            // Critical: Very high score with most criteria passing
            if (overallScore >= 0.90m && passRate >= 0.8m) 
                return AlertLevel.Critical;
            
            // High: High score with good pass rate
            if (overallScore >= 0.80m && passRate >= 0.6m) 
                return AlertLevel.High;
            
            // Medium: Decent score with moderate pass rate
            if (overallScore >= 0.70m && passRate >= 0.5m) 
                return AlertLevel.Medium;
            
            // Low: Some potential but not strong
            if (overallScore >= 0.60m && passRate >= 0.4m) 
                return AlertLevel.Low;
            
            return AlertLevel.None;
        }

        private decimal CalculateConfidenceLevel(IList<CriteriaResult> criteriaResults, IList<string> failedEvaluators)
        {
            var totalEvaluators = Evaluators.Count();
            var successfulEvaluators = totalEvaluators - failedEvaluators.Count;
            var evaluatorCompletionRate = totalEvaluators > 0 
                ? (decimal)successfulEvaluators / totalEvaluators 
                : 0m;

            var passedCount = criteriaResults.Count(r => r.Passed);
            var totalCount = criteriaResults.Count;
            var passRate = totalCount > 0 ? (decimal)passedCount / totalCount : 0m;
            
            var averageScore = criteriaResults.Any() 
                ? criteriaResults.Average(r => r.Score) 
                : 0m;

            // Confidence based on:
            // - 40% evaluator completion rate
            // - 30% criteria pass rate  
            // - 30% average score
            return (evaluatorCompletionRate * 0.4m) + (passRate * 0.3m) + (averageScore * 0.3m);
        }

        private string DetermineRecommendedAction(ScreeningResult result, MarketData marketData)
        {
            if (!result.MeetsCriteria) 
                return "IGNORE";

            // Strong buy/sell signals
            if (result.OverallScore >= 0.85m && result.AlertLevel >= AlertLevel.High)
            {
                // If news criteria passed with high score, give more weight to direction
                var newsCriteria = result.CriteriaResults.FirstOrDefault(c => c.CriteriaName == "News");
                if (newsCriteria?.Passed == true && newsCriteria.Score >= 0.8m)
                {
                    return marketData.ChangePercent > 0m ? "STRONG_BUY" : "STRONG_SELL";
                }
                
                return marketData.ChangePercent > 0m ? "BUY" : "SELL";
            }

            // Watch signals
            if (result.OverallScore >= 0.70m && result.AlertLevel >= AlertLevel.Medium)
            {
                return marketData.ChangePercent > 0m ? "WATCH_LONG" : "WATCH_SHORT";
            }

            return "WATCH";
        }

        #endregion

        #region Configuration Overrides

        protected override int MaxConcurrentEvaluations => 10;
        protected override int EvaluationTimeoutSeconds => 10;
        protected override bool EnableParallelEvaluation => true;
        protected override bool ContinueOnEvaluatorFailure => true;

        #endregion
    }
}