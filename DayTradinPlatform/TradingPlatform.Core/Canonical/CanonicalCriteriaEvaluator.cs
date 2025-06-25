// File: TradingPlatform.Core.Canonical\CanonicalCriteriaEvaluator.cs

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Foundation;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Models;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical base class for all criteria evaluators, providing standardized evaluation logic
    /// with comprehensive logging, error handling, and performance tracking.
    /// </summary>
    public abstract class CanonicalCriteriaEvaluator<TCriteria> : CanonicalServiceBase
        where TCriteria : class
    {
        private readonly Dictionary<string, object> _evaluationMetrics = new();
        private readonly SemaphoreSlim _evaluationSemaphore;
        private readonly int _maxConcurrentEvaluations;
        private long _totalEvaluations;
        private long _successfulEvaluations;
        private long _failedEvaluations;
        private readonly Stopwatch _uptimeStopwatch = Stopwatch.StartNew();

        protected CanonicalCriteriaEvaluator(
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
        /// Evaluates the criteria with canonical error handling and monitoring
        /// </summary>
        public async Task<CriteriaResult> EvaluateAsync(MarketData marketData, TradingCriteria criteria)
        {
            try
            {
                var result = await PerformEvaluationAsync(marketData, criteria);
                return result.IsSuccess ? result.Value : new CriteriaResult
                {
                    CriteriaName = CriteriaName,
                    EvaluatedAt = DateTime.UtcNow,
                    Passed = false,
                    Score = 0m,
                    Reason = result.Error?.Message ?? "Evaluation failed"
                };
            }
            catch (Exception ex)
            {
                LogError($"Error evaluating {CriteriaName}", ex);
                return new CriteriaResult
                {
                    CriteriaName = CriteriaName,
                    EvaluatedAt = DateTime.UtcNow,
                    Passed = false,
                    Score = 0m,
                    Reason = $"Evaluation failed: {ex.Message}"
                };
            }
        }

        private async Task<TradingResult<CriteriaResult>> PerformEvaluationAsync(
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
                    return TradingResult<CriteriaResult>.Failure(new TradingError("VALIDATION_FAILED", validationResult.Error?.Message ?? "Validation failed"));
                }

                // Create criteria result
                var result = new CriteriaResult
                {
                    CriteriaName = CriteriaName,
                    EvaluatedAt = DateTime.UtcNow,
                    Symbol = marketData.Symbol
                };

                // Perform the actual evaluation
                var evaluationResult = await EvaluateCriteriaAsync(marketData, criteria, result);
                
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
                            Passed = result.Passed,
                            Score = result.Score,
                            MetricsCount = result.Metrics.Count
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
                return TradingResult<CriteriaResult>.Failure(new TradingError("EVALUATION_ERROR", $"Evaluation error: {ex.Message}", ex));
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
        protected abstract Task<TradingResult<CriteriaResult>> EvaluateCriteriaAsync(
            MarketData marketData,
            TradingCriteria criteria,
            CriteriaResult result);

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