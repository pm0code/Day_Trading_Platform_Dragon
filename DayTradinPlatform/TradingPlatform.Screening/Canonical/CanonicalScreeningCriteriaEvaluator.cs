// File: TradingPlatform.Screening.Canonical\CanonicalScreeningCriteriaEvaluator.cs

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Foundation;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Models;
using TradingPlatform.Screening.Interfaces;
using TradingPlatform.Screening.Models;
using ScreeningCriteriaResult = TradingPlatform.Screening.Models.CriteriaResult;

namespace TradingPlatform.Screening.Canonical
{
    /// <summary>
    /// Canonical base class for screening criteria evaluators that bridges the gap between
    /// canonical TradingResult pattern and the ICriteriaEvaluator interface.
    /// </summary>
    public abstract class CanonicalScreeningCriteriaEvaluator : CanonicalServiceBase, Interfaces.ICriteriaEvaluator
    {
        private readonly Dictionary<string, object> _evaluationMetrics = new();
        private readonly SemaphoreSlim _evaluationSemaphore;
        private readonly int _maxConcurrentEvaluations;
        private long _totalEvaluations;
        private long _successfulEvaluations;
        private long _failedEvaluations;
        private readonly Stopwatch _uptimeStopwatch = Stopwatch.StartNew();

        protected CanonicalScreeningCriteriaEvaluator(
            IServiceProvider serviceProvider,
            ITradingLogger logger,
            string serviceName,
            int maxConcurrentEvaluations = 10)
            : base(logger, serviceName)
        {
            _maxConcurrentEvaluations = maxConcurrentEvaluations;
            _evaluationSemaphore = new SemaphoreSlim(maxConcurrentEvaluations, maxConcurrentEvaluations);
        }

        /// <summary>
        /// The name of the criteria being evaluated (e.g., "Price", "Volume", etc.)
        /// </summary>
        protected abstract string CriteriaName { get; }

        /// <summary>
        /// Validates the input before evaluation
        /// </summary>
        protected virtual TradingResult ValidateInput(MarketData marketData, TradingCriteria criteria)
        {
            if (marketData == null)
                return TradingResult.Failure(new TradingError("INVALID_INPUT", "Market data is null"));

            if (criteria == null)
                return TradingResult.Failure(new TradingError("INVALID_INPUT", "Trading criteria is null"));

            if (string.IsNullOrWhiteSpace(marketData.Symbol))
                return TradingResult.Failure(new TradingError("INVALID_INPUT", "Market data symbol is null or empty"));

            if (marketData.Price <= 0)
                return TradingResult.Failure(new TradingError("INVALID_INPUT", $"Invalid price: {marketData.Price}"));

            return TradingResult.Success();
        }

        /// <summary>
        /// Implements ICriteriaEvaluator.EvaluateAsync by adapting the canonical pattern
        /// </summary>
        public async Task<ScreeningCriteriaResult> EvaluateAsync(MarketData marketData, TradingCriteria criteria)
        {
            try
            {
                var result = await PerformEvaluationAsync(marketData, criteria);
                return result.IsSuccess ? result.Value : CreateFailureResult(result.Error);
            }
            catch (Exception ex)
            {
                LogError($"Error evaluating {CriteriaName}", ex);
                return CreateFailureResult(new TradingError("EVALUATION_ERROR", ex.Message, ex));
            }
        }

        private ScreeningCriteriaResult CreateFailureResult(TradingError? error)
        {
            return new ScreeningCriteriaResult
            {
                CriteriaName = CriteriaName,
                EvaluatedAt = DateTime.UtcNow,
                Passed = false,
                Score = 0m,
                Reason = error?.Message ?? "Evaluation failed",
                Confidence = 0m,
                AlertLevel = AlertLevel.None
            };
        }

        private async Task<TradingResult<ScreeningCriteriaResult>> PerformEvaluationAsync(
            MarketData marketData,
            TradingCriteria criteria,
            [CallerMemberName] string methodName = "")
        {
            await _evaluationSemaphore.WaitAsync();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                Interlocked.Increment(ref _totalEvaluations);

                // Validate input
                var validationResult = ValidateInput(marketData, criteria);
                if (!validationResult.IsSuccess)
                {
                    Interlocked.Increment(ref _failedEvaluations);
                    return TradingResult<ScreeningCriteriaResult>.Failure(validationResult.Error!);
                }

                // Perform the actual evaluation
                var evaluationResult = await EvaluateCriteriaAsync(marketData, criteria);
                
                if (evaluationResult.IsSuccess)
                {
                    Interlocked.Increment(ref _successfulEvaluations);
                    
                    // Log performance metrics
                    Logger.LogPerformance(
                        $"{CriteriaName} evaluation for {marketData.Symbol}",
                        stopwatch.Elapsed,
                        true,
                        throughput: CalculateThroughput(),
                        businessMetrics: new
                        {
                            Symbol = marketData.Symbol,
                            Passed = evaluationResult.Value.Passed,
                            Score = evaluationResult.Value.Score,
                            Confidence = evaluationResult.Value.Confidence,
                            AlertLevel = evaluationResult.Value.AlertLevel,
                            MetricsCount = evaluationResult.Value.Metrics.Count
                        });

                    return evaluationResult;
                }
                else
                {
                    Interlocked.Increment(ref _failedEvaluations);
                    LogError(
                        $"{CriteriaName} evaluation failed",
                        evaluationResult.Error?.Exception,
                        "Criteria evaluation",
                        "Evaluation result unavailable",
                        "Check criteria configuration",
                        new
                        {
                            Symbol = marketData.Symbol,
                            ErrorMessage = evaluationResult.Error?.Message
                        });

                    return evaluationResult;
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failedEvaluations);
                LogError($"{CriteriaName} evaluation error", ex, 
                    "Criteria evaluation",
                    "Evaluation failed",
                    "Check criteria configuration",
                    new { Symbol = marketData.Symbol });
                return TradingResult<ScreeningCriteriaResult>.Failure(new TradingError("EVALUATION_ERROR", $"Evaluation error: {ex.Message}", ex));
            }
            finally
            {
                _evaluationSemaphore.Release();
                UpdateMetric($"{CriteriaName}.EvaluationDuration", stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Implement the specific criteria evaluation logic
        /// </summary>
        protected abstract Task<TradingResult<ScreeningCriteriaResult>> EvaluateCriteriaAsync(
            MarketData marketData,
            TradingCriteria criteria);

        /// <summary>
        /// Calculate normalized score (0-100) based on value position within range
        /// </summary>
        protected decimal CalculateNormalizedScore(decimal value, decimal min, decimal max, bool invertScore = false)
        {
            if (max <= min)
                return 50m; // Default to neutral if range is invalid

            decimal normalizedPosition = (value - min) / (max - min);
            normalizedPosition = Math.Max(0m, Math.Min(1m, normalizedPosition)); // Clamp to [0,1]

            if (invertScore)
                normalizedPosition = 1m - normalizedPosition;

            return normalizedPosition * 100m;
        }

        /// <summary>
        /// Calculate weighted average of multiple scores
        /// </summary>
        protected decimal CalculateWeightedScore(params (decimal score, decimal weight)[] scoreWeights)
        {
            decimal totalWeight = scoreWeights.Sum(sw => sw.weight);
            if (totalWeight <= 0) return 0m;

            decimal weightedSum = scoreWeights.Sum(sw => sw.score * sw.weight);
            return weightedSum / totalWeight;
        }

        /// <summary>
        /// Determine alert level based on score and other factors
        /// </summary>
        protected AlertLevel DetermineAlertLevel(decimal score, decimal confidence)
        {
            var effectiveScore = score * (confidence / 100m);
            
            if (effectiveScore >= 80m)
                return AlertLevel.High;
            else if (effectiveScore >= 60m)
                return AlertLevel.Medium;
            else if (effectiveScore >= 40m)
                return AlertLevel.Low;
            else
                return AlertLevel.None;
        }

        private double CalculateThroughput()
        {
            var uptime = _uptimeStopwatch.Elapsed.TotalSeconds;
            return uptime > 0 ? _totalEvaluations / uptime : 0;
        }

        protected Dictionary<string, object?> GetCustomMetrics()
        {
            var baseMetrics = new Dictionary<string, object?>();
            
            baseMetrics["TotalEvaluations"] = _totalEvaluations;
            baseMetrics["SuccessfulEvaluations"] = _successfulEvaluations;
            baseMetrics["FailedEvaluations"] = _failedEvaluations;
            baseMetrics["SuccessRate"] = _totalEvaluations > 0 
                ? (double)_successfulEvaluations / _totalEvaluations 
                : 0.0;
            baseMetrics["EvaluationsPerSecond"] = CalculateThroughput();
            baseMetrics["MaxConcurrentEvaluations"] = _maxConcurrentEvaluations;
            baseMetrics["CurrentConcurrentEvaluations"] = _maxConcurrentEvaluations - _evaluationSemaphore.CurrentCount;
            
            // Add custom evaluation metrics
            foreach (var metric in _evaluationMetrics)
            {
                baseMetrics[$"Evaluation.{metric.Key}"] = metric.Value;
            }

            return baseMetrics;
        }

        protected void RecordMetric(string name, object value)
        {
            _evaluationMetrics[name] = value;
        }

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Initializing {CriteriaName} evaluator");
            await Task.CompletedTask;
        }

        protected override async Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Starting {CriteriaName} evaluator");
            _uptimeStopwatch.Start();
            await Task.CompletedTask;
        }

        protected override async Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Stopping {CriteriaName} evaluator");
            _uptimeStopwatch.Stop();
            await Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _evaluationSemaphore?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}