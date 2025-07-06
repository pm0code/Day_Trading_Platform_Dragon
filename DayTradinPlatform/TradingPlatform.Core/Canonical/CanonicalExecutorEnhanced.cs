using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Enhanced canonical base class for execution-related components with MCP standards compliance.
    /// Provides event code logging, operation tracking, and comprehensive execution monitoring.
    /// </summary>
    public abstract class CanonicalExecutorEnhanced<TRequest, TResult> : CanonicalServiceBaseEnhanced
        where TRequest : class
        where TResult : class
    {
        #region Configuration

        protected virtual int MaxConcurrentExecutions => 100;
        protected virtual int ExecutionTimeoutSeconds => 5;
        protected virtual bool EnablePreTradeRiskChecks => true;
        protected virtual bool EnablePostTradeAnalytics => true;
        protected virtual bool EnableExecutionThrottling => true;
        protected virtual TimeSpan ExecutionWarningThreshold => TimeSpan.FromMilliseconds(100);
        protected virtual TimeSpan ExecutionCriticalThreshold => TimeSpan.FromMilliseconds(500);

        #endregion

        #region Infrastructure

        private readonly SemaphoreSlim _executionThrottle;
        private long _totalExecutions;
        private long _successfulExecutions;
        private long _failedExecutions;
        private long _rejectedExecutions;
        private long _timeoutExecutions;
        private readonly Stopwatch _executionTimer = new();

        #endregion

        #region Constructor

        protected CanonicalExecutorEnhanced(string executorName)
            : base(executorName, createChildLogger: true)
        {
            _executionThrottle = new SemaphoreSlim(MaxConcurrentExecutions, MaxConcurrentExecutions);
            
            Logger.LogEvent(
                TradingLogOrchestratorEnhanced.COMPONENT_INITIALIZED,
                $"Executor '{executorName}' initialized",
                new 
                { 
                    ExecutorName = executorName,
                    MaxConcurrentExecutions,
                    ExecutionTimeoutSeconds,
                    PreTradeRiskChecks = EnablePreTradeRiskChecks,
                    PostTradeAnalytics = EnablePostTradeAnalytics
                });
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Executes a trading request with full lifecycle management and operation tracking
        /// </summary>
        public async Task<TradingResult<TResult>> ExecuteAsync(
            TRequest request,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string methodName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            ValidateNotNull(request, nameof(request));

            var operationName = $"{ServiceName}.Execute[{typeof(TRequest).Name}]";
            
            return await TrackOperationAsync(
                operationName,
                async () =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    _executionTimer.Restart();

                    try
                    {
                        // Pre-trade validation with event logging
                        if (EnablePreTradeRiskChecks)
                        {
                            Logger.LogEvent(
                                TradingLogOrchestratorEnhanced.OPERATION_STARTED,
                                "Starting pre-trade risk checks",
                                new { RequestType = typeof(TRequest).Name });

                            var validationResult = await ValidatePreTradeAsync(request, cancellationToken);
                            
                            if (!validationResult.IsSuccess)
                            {
                                Interlocked.Increment(ref _rejectedExecutions);
                                UpdateMetric("RejectedExecutions", _rejectedExecutions);
                                
                                Logger.LogEvent(
                                    TradingLogOrchestratorEnhanced.TRADE_REJECTED,
                                    "Trade rejected by pre-trade validation",
                                    new 
                                    { 
                                        RequestType = typeof(TRequest).Name,
                                        Reason = validationResult.Error?.Message,
                                        ErrorCode = validationResult.Error?.Code
                                    },
                                    LogLevel.Warning);
                                
                                return TradingResult<TResult>.Failure(validationResult.Error!);
                            }
                        }

                        // Throttle concurrent executions
                        if (EnableExecutionThrottling)
                        {
                            var throttleWaitStart = Stopwatch.StartNew();
                            await _executionThrottle.WaitAsync(cancellationToken);
                            
                            if (throttleWaitStart.ElapsedMilliseconds > 100)
                            {
                                Logger.LogEvent(
                                    TradingLogOrchestratorEnhanced.LATENCY_SPIKE,
                                    "Execution throttle wait time exceeded threshold",
                                    new { WaitTimeMs = throttleWaitStart.ElapsedMilliseconds },
                                    LogLevel.Warning);
                            }
                        }

                        try
                        {
                            // Execute with timeout
                            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                            cts.CancelAfter(TimeSpan.FromSeconds(ExecutionTimeoutSeconds));

                            var result = await MonitorPerformanceAsync(
                                "PerformExecution",
                                async () => await PerformExecutionAsync(request, cts.Token),
                                ExecutionWarningThreshold);

                            if (result.IsSuccess)
                            {
                                Interlocked.Increment(ref _successfulExecutions);
                                
                                Logger.LogEvent(
                                    TradingLogOrchestratorEnhanced.TRADE_EXECUTED,
                                    "Trade executed successfully",
                                    new 
                                    { 
                                        RequestType = typeof(TRequest).Name,
                                        ResultType = typeof(TResult).Name,
                                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                                    });
                                
                                // Post-trade analytics
                                if (EnablePostTradeAnalytics && result.Value != null)
                                {
                                    await RecordExecutionAnalyticsAsync(request, result.Value, stopwatch.Elapsed);
                                }
                            }
                            else
                            {
                                Interlocked.Increment(ref _failedExecutions);
                                
                                Logger.LogEvent(
                                    TradingLogOrchestratorEnhanced.OPERATION_FAILED,
                                    "Trade execution failed",
                                    new 
                                    { 
                                        RequestType = typeof(TRequest).Name,
                                        Error = result.Error?.Message,
                                        ErrorCode = result.Error?.Code
                                    },
                                    LogLevel.Error);
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
                        Interlocked.Increment(ref _timeoutExecutions);
                        
                        Logger.LogEvent(
                            TradingLogOrchestratorEnhanced.OPERATION_TIMEOUT,
                            $"Execution timeout exceeded ({ExecutionTimeoutSeconds}s)",
                            new 
                            { 
                                RequestType = typeof(TRequest).Name,
                                TimeoutSeconds = ExecutionTimeoutSeconds,
                                ElapsedMs = stopwatch.ElapsedMilliseconds
                            },
                            LogLevel.Error);
                        
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
                new { Request = request },
                methodName,
                sourceFilePath,
                sourceLineNumber);
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
        protected virtual async Task RecordExecutionAnalyticsAsync(
            TRequest request,
            TResult result,
            TimeSpan executionTime)
        {
            // Log execution analytics with event code
            Logger.LogEvent(
                TradingLogOrchestratorEnhanced.OPERATION_COMPLETED,
                "Execution analytics recorded",
                new 
                { 
                    RequestType = typeof(TRequest).Name,
                    ResultType = typeof(TResult).Name,
                    ExecutionTimeMs = executionTime.TotalMilliseconds,
                    ExecutionTimeMicroseconds = executionTime.TotalMicroseconds
                });
            
            await Task.CompletedTask;
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
            var executionPrice = basePrice * (1 + impact);
            
            Logger.LogDebug(
                "Calculated execution price with market impact",
                new 
                { 
                    BasePrice = basePrice,
                    Quantity = quantity,
                    MarketImpact = impact,
                    ExecutionPrice = executionPrice
                });
            
            return executionPrice;
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
            var adjustedSlippage = isBuyOrder ? slippage : -slippage;
            
            // Log significant slippage
            if (Math.Abs(adjustedSlippage) > 0.001m) // 0.1%
            {
                Logger.LogEvent(
                    TradingLogOrchestratorEnhanced.LATENCY_SPIKE,
                    "Significant slippage detected",
                    new 
                    { 
                        ExpectedPrice = expectedPrice,
                        ActualPrice = actualPrice,
                        SlippagePercent = adjustedSlippage * 100,
                        IsBuyOrder = isBuyOrder
                    },
                    LogLevel.Warning);
            }
            
            return adjustedSlippage;
        }

        /// <summary>
        /// Validates quantity against lot size requirements
        /// </summary>
        protected bool IsValidLotSize(decimal quantity, decimal lotSize = 1m)
        {
            var isValid = quantity > 0 && quantity % lotSize == 0;
            
            if (!isValid)
            {
                Logger.LogDebug(
                    "Invalid lot size detected",
                    new { Quantity = quantity, LotSize = lotSize });
            }
            
            return isValid;
        }

        #endregion

        #region Private Methods

        private void UpdateExecutionMetrics(TimeSpan executionTime)
        {
            UpdateMetric("TotalExecutions", _totalExecutions);
            UpdateMetric("SuccessfulExecutions", _successfulExecutions);
            UpdateMetric("FailedExecutions", _failedExecutions);
            UpdateMetric("RejectedExecutions", _rejectedExecutions);
            UpdateMetric("TimeoutExecutions", _timeoutExecutions);
            UpdateMetric("LastExecutionTimeMs", executionTime.TotalMilliseconds);
            UpdateMetric("LastExecutionTimeMicroseconds", executionTime.TotalMicroseconds);
            
            if (_totalExecutions > 0)
            {
                var successRate = (double)_successfulExecutions / _totalExecutions;
                var rejectionRate = (double)_rejectedExecutions / (_totalExecutions + _rejectedExecutions);
                
                UpdateMetric("SuccessRate", successRate);
                UpdateMetric("RejectionRate", rejectionRate);
                
                // Log performance degradation if success rate drops
                if (successRate < 0.95 && _totalExecutions > 100)
                {
                    Logger.LogEvent(
                        TradingLogOrchestratorEnhanced.PERFORMANCE_DEGRADATION,
                        "Execution success rate below threshold",
                        new 
                        { 
                            SuccessRate = successRate,
                            TotalExecutions = _totalExecutions,
                            FailedExecutions = _failedExecutions
                        },
                        LogLevel.Warning);
                }
            }

            // Update rolling average execution time
            var avgKey = "AverageExecutionTimeMs";
            var metrics = GetMetrics();
            var currentAvg = metrics.ContainsKey(avgKey) ? (double)metrics[avgKey] : 0;
            var newAvg = (currentAvg * (_totalExecutions - 1) + executionTime.TotalMilliseconds) / _totalExecutions;
            UpdateMetric(avgKey, newAvg);
            
            // Check for performance degradation
            if (executionTime > ExecutionCriticalThreshold)
            {
                Logger.LogEvent(
                    TradingLogOrchestratorEnhanced.LATENCY_SPIKE,
                    "Critical execution latency threshold exceeded",
                    new 
                    { 
                        ExecutionTimeMs = executionTime.TotalMilliseconds,
                        ThresholdMs = ExecutionCriticalThreshold.TotalMilliseconds
                    },
                    LogLevel.Error);
            }
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
            metrics["Executor.TimeoutExecutions"] = _timeoutExecutions;

            // Calculate throughput
            if (_executionTimer.IsRunning && _executionTimer.Elapsed.TotalSeconds > 0)
            {
                var throughput = _totalExecutions / _executionTimer.Elapsed.TotalSeconds;
                metrics["Executor.ThroughputPerSecond"] = throughput;
                
                // Check for throughput drop
                if (throughput < 10 && _totalExecutions > 1000)
                {
                    Logger.LogEvent(
                        TradingLogOrchestratorEnhanced.THROUGHPUT_DROP,
                        "Executor throughput below expected levels",
                        new { ThroughputPerSecond = throughput },
                        LogLevel.Warning);
                }
            }

            return metrics;
        }

        #endregion

        #region Lifecycle

        protected override async Task<TradingResult<bool>> OnInitializeAsync(CancellationToken cancellationToken)
        {
            Logger.LogEvent(
                TradingLogOrchestratorEnhanced.COMPONENT_INITIALIZED,
                $"Initializing {ServiceName} executor",
                new { MaxConcurrency = MaxConcurrentExecutions });
            
            return await Task.FromResult(TradingResult<bool>.Success(true));
        }

        protected override async Task<TradingResult<bool>> OnStartAsync(CancellationToken cancellationToken)
        {
            _executionTimer.Start();
            
            Logger.LogEvent(
                TradingLogOrchestratorEnhanced.SYSTEM_STARTUP,
                $"{ServiceName} executor started",
                new { ExecutorType = GetType().Name });
            
            return await Task.FromResult(TradingResult<bool>.Success(true));
        }

        protected override async Task<TradingResult<bool>> OnStopAsync(CancellationToken cancellationToken)
        {
            _executionTimer.Stop();
            
            Logger.LogEvent(
                TradingLogOrchestratorEnhanced.SYSTEM_SHUTDOWN,
                $"{ServiceName} executor stopped",
                new 
                { 
                    TotalExecutions = _totalExecutions,
                    SuccessfulExecutions = _successfulExecutions,
                    FailedExecutions = _failedExecutions,
                    RejectedExecutions = _rejectedExecutions,
                    TimeoutExecutions = _timeoutExecutions,
                    TotalRuntimeSeconds = _executionTimer.Elapsed.TotalSeconds
                });
            
            return await Task.FromResult(TradingResult<bool>.Success(true));
        }

        protected override async Task<Dictionary<string, HealthCheckEntry>> OnCheckHealthAsync()
        {
            var checks = new Dictionary<string, HealthCheckEntry>();
            
            // Check execution throttle availability
            var throttleAvailable = _executionThrottle.CurrentCount;
            checks["execution_throttle"] = new HealthCheckEntry
            {
                Status = throttleAvailable > 0 ? HealthStatus.Healthy : HealthStatus.Degraded,
                Description = $"Execution slots available: {throttleAvailable}/{MaxConcurrentExecutions}",
                Data = new Dictionary<string, object>
                {
                    ["AvailableSlots"] = throttleAvailable,
                    ["MaxSlots"] = MaxConcurrentExecutions
                }
            };
            
            // Check success rate
            if (_totalExecutions > 0)
            {
                var successRate = (double)_successfulExecutions / _totalExecutions;
                checks["success_rate"] = new HealthCheckEntry
                {
                    Status = successRate >= 0.95 ? HealthStatus.Healthy : 
                             successRate >= 0.90 ? HealthStatus.Degraded : HealthStatus.Unhealthy,
                    Description = $"Success rate: {successRate:P2}",
                    Data = new Dictionary<string, object>
                    {
                        ["SuccessRate"] = successRate,
                        ["TotalExecutions"] = _totalExecutions,
                        ["FailedExecutions"] = _failedExecutions
                    }
                };
            }
            
            return await Task.FromResult(checks);
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