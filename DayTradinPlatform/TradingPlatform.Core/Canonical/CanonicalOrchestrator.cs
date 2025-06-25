using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical base class for orchestrators that coordinate multiple evaluators or processors.
    /// Provides standardized patterns for aggregation, scoring, and decision making.
    /// </summary>
    public abstract class CanonicalOrchestrator<TEvaluator, TInput, TOutput, TResult> : CanonicalServiceBase
        where TEvaluator : class
        where TInput : class
        where TOutput : class
        where TResult : class
    {
        #region Configuration

        protected virtual int MaxConcurrentEvaluations => Environment.ProcessorCount * 2;
        protected virtual int EvaluationTimeoutSeconds => 30;
        protected virtual bool EnableParallelEvaluation => true;
        protected virtual bool ContinueOnEvaluatorFailure => true;

        #endregion

        #region Infrastructure

        protected readonly IEnumerable<TEvaluator> Evaluators;
        private readonly SemaphoreSlim _evaluationThrottle;
        private long _totalEvaluations;
        private long _successfulEvaluations;
        private long _failedEvaluations;
        private long _partialEvaluations;

        #endregion

        #region Constructor

        protected CanonicalOrchestrator(
            IEnumerable<TEvaluator> evaluators,
            ITradingLogger logger,
            string orchestratorName)
            : base(logger, orchestratorName)
        {
            Evaluators = evaluators ?? throw new ArgumentNullException(nameof(evaluators));
            _evaluationThrottle = new SemaphoreSlim(MaxConcurrentEvaluations, MaxConcurrentEvaluations);

            LogMethodEntry(new { evaluatorCount = evaluators.Count(), orchestratorName });
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Orchestrates evaluation across all evaluators and returns aggregated result
        /// </summary>
        public async Task<TradingResult<TResult>> OrchestratAsync(
            TInput input,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string methodName = "")
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    ValidateNotNull(input, nameof(input));

                    var stopwatch = Stopwatch.StartNew();
                    var evaluationResults = new List<TOutput>();
                    var failedEvaluations = new List<string>();

                    try
                    {
                        // Evaluate using all evaluators
                        if (EnableParallelEvaluation)
                        {
                            var results = await EvaluateInParallelAsync(input, cancellationToken);
                            evaluationResults.AddRange(results.successfulResults);
                            failedEvaluations.AddRange(results.failedEvaluators);
                        }
                        else
                        {
                            var results = await EvaluateSequentiallyAsync(input, cancellationToken);
                            evaluationResults.AddRange(results.successfulResults);
                            failedEvaluations.AddRange(results.failedEvaluators);
                        }

                        stopwatch.Stop();

                        // Check if we have minimum results
                        if (!evaluationResults.Any() && !ContinueOnEvaluatorFailure)
                        {
                            Interlocked.Increment(ref _failedEvaluations);
                            return TradingResult<TResult>.Failure(
                                new TradingError(
                                    TradingError.ErrorCodes.SystemError,
                                    "No evaluators produced results",
                                    null,
                                    null,
                                    null,
                                    new Dictionary<string, object>
                                    {
                                        ["failedEvaluators"] = failedEvaluations,
                                        ["evaluationTimeMs"] = stopwatch.ElapsedMilliseconds
                                    }));
                        }

                        // Aggregate results
                        var aggregatedResult = await AggregateResultsAsync(
                            input,
                            evaluationResults,
                            failedEvaluations,
                            cancellationToken);

                        if (aggregatedResult.IsSuccess)
                        {
                            // Record metrics
                            Interlocked.Increment(ref _totalEvaluations);
                            if (failedEvaluations.Any())
                            {
                                Interlocked.Increment(ref _partialEvaluations);
                            }
                            else
                            {
                                Interlocked.Increment(ref _successfulEvaluations);
                            }

                            UpdateMetric("LastEvaluationTimeMs", stopwatch.ElapsedMilliseconds);
                            UpdateMetric("EvaluatorsUsed", evaluationResults.Count);
                            UpdateMetric("EvaluatorsFailed", failedEvaluations.Count);

                            LogDebug($"Orchestration completed in {stopwatch.ElapsedMilliseconds}ms with {evaluationResults.Count} results");
                        }

                        return aggregatedResult;
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref _failedEvaluations);
                        throw;
                    }
                },
                methodName,
                incrementOperationCounter: true);
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Evaluates an evaluator and returns its result
        /// </summary>
        protected abstract Task<TradingResult<TOutput>> EvaluateWithEvaluatorAsync(
            TEvaluator evaluator,
            TInput input,
            CancellationToken cancellationToken);

        /// <summary>
        /// Aggregates individual evaluator results into final result
        /// </summary>
        protected abstract Task<TradingResult<TResult>> AggregateResultsAsync(
            TInput input,
            IList<TOutput> evaluationResults,
            IList<string> failedEvaluators,
            CancellationToken cancellationToken);

        /// <summary>
        /// Gets the name/identifier for an evaluator (for logging)
        /// </summary>
        protected abstract string GetEvaluatorName(TEvaluator evaluator);

        /// <summary>
        /// Determines if evaluation should continue after an evaluator failure
        /// </summary>
        protected virtual bool ShouldContinueAfterFailure(string evaluatorName, TradingError error)
        {
            return ContinueOnEvaluatorFailure;
        }

        /// <summary>
        /// Calculate a weighted score from multiple scores
        /// </summary>
        protected decimal CalculateWeightedScore(IEnumerable<(decimal score, decimal weight)> scores)
        {
            var scoreList = scores.ToList();
            if (!scoreList.Any()) return 0m;

            var totalWeight = scoreList.Sum(s => s.weight);
            if (totalWeight == 0) return 0m;

            return scoreList.Sum(s => s.score * s.weight) / totalWeight;
        }

        /// <summary>
        /// Calculate a simple average score
        /// </summary>
        protected decimal CalculateAverageScore(IEnumerable<decimal> scores)
        {
            var scoreList = scores.ToList();
            return scoreList.Any() ? scoreList.Average() : 0m;
        }

        #endregion

        #region Private Methods

        private async Task<(List<TOutput> successfulResults, List<string> failedEvaluators)> EvaluateInParallelAsync(
            TInput input,
            CancellationToken cancellationToken)
        {
            var successfulResults = new List<TOutput>();
            var failedEvaluators = new List<string>();
            var lockObj = new object();

            var tasks = Evaluators.Select(async evaluator =>
            {
                await _evaluationThrottle.WaitAsync(cancellationToken);
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(EvaluationTimeoutSeconds));

                    var evaluatorName = GetEvaluatorName(evaluator);
                    var result = await EvaluateWithEvaluatorAsync(evaluator, input, cts.Token);

                    lock (lockObj)
                    {
                        if (result.IsSuccess && result.Value != null)
                        {
                            successfulResults.Add(result.Value);
                        }
                        else
                        {
                            failedEvaluators.Add(evaluatorName);
                            if (!ShouldContinueAfterFailure(evaluatorName, result.Error!))
                            {
                                cts.Cancel();
                            }
                        }
                    }
                }
                finally
                {
                    _evaluationThrottle.Release();
                }
            });

            await Task.WhenAll(tasks);
            return (successfulResults, failedEvaluators);
        }

        private async Task<(List<TOutput> successfulResults, List<string> failedEvaluators)> EvaluateSequentiallyAsync(
            TInput input,
            CancellationToken cancellationToken)
        {
            var successfulResults = new List<TOutput>();
            var failedEvaluators = new List<string>();

            foreach (var evaluator in Evaluators)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(EvaluationTimeoutSeconds));

                var evaluatorName = GetEvaluatorName(evaluator);
                var result = await EvaluateWithEvaluatorAsync(evaluator, input, cts.Token);

                if (result.IsSuccess && result.Value != null)
                {
                    successfulResults.Add(result.Value);
                }
                else
                {
                    failedEvaluators.Add(evaluatorName);
                    if (!ShouldContinueAfterFailure(evaluatorName, result.Error!))
                    {
                        break;
                    }
                }
            }

            return (successfulResults, failedEvaluators);
        }

        #endregion

        #region Metrics

        public override IReadOnlyDictionary<string, object> GetMetrics()
        {
            var metrics = base.GetMetrics().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Add orchestrator-specific metrics
            metrics["Orchestrator.TotalEvaluations"] = _totalEvaluations;
            metrics["Orchestrator.SuccessfulEvaluations"] = _successfulEvaluations;
            metrics["Orchestrator.FailedEvaluations"] = _failedEvaluations;
            metrics["Orchestrator.PartialEvaluations"] = _partialEvaluations;
            metrics["Orchestrator.EvaluatorCount"] = Evaluators.Count();
            metrics["Orchestrator.MaxConcurrency"] = MaxConcurrentEvaluations;
            metrics["Orchestrator.ParallelEnabled"] = EnableParallelEvaluation;

            if (_totalEvaluations > 0)
            {
                metrics["Orchestrator.SuccessRate"] = (double)_successfulEvaluations / _totalEvaluations;
                metrics["Orchestrator.PartialRate"] = (double)_partialEvaluations / _totalEvaluations;
            }

            return metrics;
        }

        #endregion

        #region Lifecycle

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Initializing {ComponentName} with {Evaluators.Count()} evaluators");
            return Task.CompletedTask;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo($"{ComponentName} started with parallel evaluation: {EnableParallelEvaluation}");
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo($"{ComponentName} stopped. Total evaluations: {_totalEvaluations}");
            return Task.CompletedTask;
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _evaluationThrottle?.Dispose();
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}