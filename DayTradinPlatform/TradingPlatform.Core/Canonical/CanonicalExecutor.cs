using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical base class for execution-related components in the trading platform.
    /// Provides standardized patterns for order execution, risk checks, and performance monitoring.
    /// </summary>
    public abstract class CanonicalExecutor<TRequest, TResult> : CanonicalServiceBase
        where TRequest : class
        where TResult : class
    {
        #region Configuration

        protected virtual int MaxConcurrentExecutions => 100;
        protected virtual int ExecutionTimeoutSeconds => 5;
        protected virtual bool EnablePreTradeRiskChecks => true;
        protected virtual bool EnablePostTradeAnalytics => true;
        protected virtual bool EnableExecutionThrottling => true;

        #endregion

        #region Infrastructure

        private readonly SemaphoreSlim _executionThrottle;
        private long _totalExecutions;
        private long _successfulExecutions;
        private long _failedExecutions;
        private long _rejectedExecutions;
        private readonly Stopwatch _executionTimer = new();

        #endregion

        #region Constructor

        protected CanonicalExecutor(
            ITradingLogger logger,
            string executorName)
            : base(logger, executorName)
        {
            _executionThrottle = new SemaphoreSlim(MaxConcurrentExecutions, MaxConcurrentExecutions);
            LogMethodEntry(new { executorName, maxConcurrentExecutions = MaxConcurrentExecutions });
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Executes a trading request with full lifecycle management
        /// </summary>
        public async Task<TradingResult<TResult>> ExecuteAsync(
            TRequest request,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string methodName = "")
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    ValidateNotNull(request, nameof(request));

                    var stopwatch = Stopwatch.StartNew();
                    _executionTimer.Restart();

                    try
                    {
                        // Pre-trade validation
                        if (EnablePreTradeRiskChecks)
                        {
                            var validationResult = await ValidatePreTradeAsync(request, cancellationToken);
                            if (!validationResult.IsSuccess)
                            {
                                Interlocked.Increment(ref _rejectedExecutions);
                                UpdateMetric("RejectedExecutions", _rejectedExecutions);
                                return TradingResult<TResult>.Failure(validationResult.Error!);
                            }
                        }

                        // Throttle concurrent executions
                        if (EnableExecutionThrottling)
                        {
                            await _executionThrottle.WaitAsync(cancellationToken);
                        }

                        try
                        {
                            // Execute with timeout
                            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                            cts.CancelAfter(TimeSpan.FromSeconds(ExecutionTimeoutSeconds));

                            var result = await PerformExecutionAsync(request, cts.Token);

                            if (result.IsSuccess)
                            {
                                Interlocked.Increment(ref _successfulExecutions);
                                
                                // Post-trade analytics
                                if (EnablePostTradeAnalytics && result.Value != null)
                                {
                                    await RecordExecutionAnalyticsAsync(request, result.Value, stopwatch.Elapsed);
                                }
                            }
                            else
                            {
                                Interlocked.Increment(ref _failedExecutions);
                            }

                            Interlocked.Increment(ref _totalExecutions);
                            UpdateExecutionMetrics(stopwatch.Elapsed);

                            return result;
                        }
                        finally
                        {
                            if (EnableExecutionThrottling)
                            {
                                _executionThrottle.Release();
                            }
                        }
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        Interlocked.Increment(ref _failedExecutions);
                        return TradingResult<TResult>.Failure(
                            TradingError.ErrorCodes.TimeoutExceeded,
                            $"Execution timeout exceeded ({ExecutionTimeoutSeconds}s)");
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref _failedExecutions);
                        throw;
                    }
                },
                methodName,
                incrementOperationCounter: true);
        }

        #endregion

        #region Protected Abstract Methods

        /// <summary>
        /// Performs the actual execution logic
        /// </summary>
        protected abstract Task<TradingResult<TResult>> PerformExecutionAsync(
            TRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Validates the request before execution
        /// </summary>
        protected abstract Task<TradingResult> ValidatePreTradeAsync(
            TRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Records analytics after successful execution
        /// </summary>
        protected virtual Task RecordExecutionAnalyticsAsync(
            TRequest request,
            TResult result,
            TimeSpan executionTime)
        {
            // Default implementation - override for specific analytics
            LogDebug($"Execution completed in {executionTime.TotalMilliseconds:F2}ms");
            return Task.CompletedTask;
        }

        #endregion

        #region Protected Utility Methods

        /// <summary>
        /// Calculates execution price with market impact
        /// </summary>
        protected decimal CalculateExecutionPrice(
            decimal basePrice,
            decimal quantity,
            decimal marketImpactFactor = 0.0001m)
        {
            var impact = quantity * marketImpactFactor;
            return basePrice * (1 + impact);
        }

        /// <summary>
        /// Calculates slippage between expected and actual prices
        /// </summary>
        protected decimal CalculateSlippage(
            decimal expectedPrice,
            decimal actualPrice,
            bool isBuyOrder)
        {
            if (expectedPrice == 0) return 0;
            
            var slippage = (actualPrice - expectedPrice) / expectedPrice;
            return isBuyOrder ? slippage : -slippage;
        }

        /// <summary>
        /// Validates quantity against lot size requirements
        /// </summary>
        protected bool IsValidLotSize(decimal quantity, decimal lotSize = 1m)
        {
            return quantity > 0 && quantity % lotSize == 0;
        }

        #endregion

        #region Private Methods

        private void UpdateExecutionMetrics(TimeSpan executionTime)
        {
            UpdateMetric("TotalExecutions", _totalExecutions);
            UpdateMetric("SuccessfulExecutions", _successfulExecutions);
            UpdateMetric("FailedExecutions", _failedExecutions);
            UpdateMetric("RejectedExecutions", _rejectedExecutions);
            UpdateMetric("LastExecutionTimeMs", executionTime.TotalMilliseconds);
            
            if (_totalExecutions > 0)
            {
                UpdateMetric("SuccessRate", (double)_successfulExecutions / _totalExecutions);
                UpdateMetric("RejectionRate", (double)_rejectedExecutions / (_totalExecutions + _rejectedExecutions));
            }

            // Update rolling average execution time
            var avgKey = "AverageExecutionTimeMs";
            var metrics = GetMetrics();
            var currentAvg = metrics.ContainsKey(avgKey) ? (double)metrics[avgKey] : 0;
            var newAvg = (currentAvg * (_totalExecutions - 1) + executionTime.TotalMilliseconds) / _totalExecutions;
            UpdateMetric(avgKey, newAvg);
        }

        #endregion

        #region Metrics

        public override IReadOnlyDictionary<string, object> GetMetrics()
        {
            var metrics = base.GetMetrics().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Add executor-specific metrics
            metrics["Executor.MaxConcurrentExecutions"] = MaxConcurrentExecutions;
            metrics["Executor.CurrentConcurrency"] = MaxConcurrentExecutions - _executionThrottle.CurrentCount;
            metrics["Executor.ExecutionTimeoutSeconds"] = ExecutionTimeoutSeconds;
            metrics["Executor.PreTradeRiskChecksEnabled"] = EnablePreTradeRiskChecks;
            metrics["Executor.PostTradeAnalyticsEnabled"] = EnablePostTradeAnalytics;

            // Calculate throughput
            if (_executionTimer.IsRunning && _executionTimer.Elapsed.TotalSeconds > 0)
            {
                metrics["Executor.ThroughputPerSecond"] = _totalExecutions / _executionTimer.Elapsed.TotalSeconds;
            }

            return metrics;
        }

        #endregion

        #region Lifecycle

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Initializing {ComponentName} executor with max concurrency: {MaxConcurrentExecutions}");
            return Task.CompletedTask;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            _executionTimer.Start();
            LogInfo($"{ComponentName} executor started");
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            _executionTimer.Stop();
            LogInfo($"{ComponentName} executor stopped. Total executions: {_totalExecutions}");
            return Task.CompletedTask;
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _executionThrottle?.Dispose();
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}