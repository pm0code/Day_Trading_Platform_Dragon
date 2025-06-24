using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical base class for all processing engines in the trading platform.
    /// Provides standardized patterns for data processing, pipeline management, and performance optimization.
    /// </summary>
    public abstract class CanonicalEngine<TInput, TOutput> : CanonicalServiceBase 
        where TInput : class 
        where TOutput : class
    {
        #region Engine Configuration

        protected virtual int MaxConcurrency => Environment.ProcessorCount;
        protected virtual int InputQueueCapacity => 10000;
        protected virtual int OutputQueueCapacity => 10000;
        protected virtual int BatchSize => 100;
        protected virtual int ProcessingTimeoutSeconds => 30;
        protected virtual bool EnableBatching => true;
        protected virtual bool EnableParallelProcessing => true;

        #endregion

        #region Engine Infrastructure

        private readonly Channel<TInput> _inputChannel;
        private readonly Channel<TOutput> _outputChannel;
        private readonly CancellationTokenSource _processingCts;
        private readonly SemaphoreSlim _processingThrottle;
        private readonly List<Task> _processingTasks;
        private readonly ConcurrentDictionary<string, EngineMetrics> _pipelineMetrics;
        private readonly Timer _metricsTimer;

        private long _totalProcessed;
        private long _totalFailed;
        private long _totalSkipped;
        private readonly Stopwatch _engineStopwatch;

        #endregion

        #region Constructor

        protected CanonicalEngine(
            ITradingLogger logger,
            string engineName)
            : base(logger, engineName)
        {
            // Initialize channels with bounded capacity for backpressure
            var inputOptions = new BoundedChannelOptions(InputQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };
            _inputChannel = Channel.CreateBounded<TInput>(inputOptions);

            var outputOptions = new BoundedChannelOptions(OutputQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };
            _outputChannel = Channel.CreateBounded<TOutput>(outputOptions);

            _processingCts = new CancellationTokenSource();
            _processingThrottle = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
            _processingTasks = new List<Task>();
            _pipelineMetrics = new ConcurrentDictionary<string, EngineMetrics>();
            _engineStopwatch = new Stopwatch();

            // Start metrics collection timer
            _metricsTimer = new Timer(
                CollectMetrics,
                null,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10));

            LogMethodEntry(new { engineName, maxConcurrency = MaxConcurrency, batchingEnabled = EnableBatching });
        }

        #endregion

        #region Engine Lifecycle

        protected override async Task OnStartAsync(CancellationToken cancellationToken)
        {
            _engineStopwatch.Start();
            
            // Start processing workers
            for (int i = 0; i < MaxConcurrency; i++)
            {
                var workerId = i;
                _processingTasks.Add(Task.Run(() => ProcessingWorker(workerId, _processingCts.Token)));
            }

            // Start pipeline stages
            await StartPipelineAsync(cancellationToken);

            LogInfo($"{ComponentName} engine started with {MaxConcurrency} workers");
        }

        protected override async Task OnStopAsync(CancellationToken cancellationToken)
        {
            _engineStopwatch.Stop();

            // Signal completion
            _inputChannel.Writer.TryComplete();
            
            // Cancel processing
            _processingCts.Cancel();

            // Wait for workers to complete
            try
            {
                await Task.WhenAll(_processingTasks);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }

            _outputChannel.Writer.TryComplete();

            LogInfo($"{ComponentName} engine stopped. Total processed: {_totalProcessed}, Failed: {_totalFailed}");
        }

        #endregion

        #region Input/Output Methods

        /// <summary>
        /// Submits an item for processing
        /// </summary>
        public async Task<TradingResult> SubmitAsync(
            TInput input,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string methodName = "")
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                ValidateNotNull(input, nameof(input));

                if (ServiceState != ServiceState.Running)
                {
                    return TradingResult.Failure(
                        TradingError.ErrorCodes.ServiceUnavailable,
                        $"{ComponentName} is not running. Current state: {ServiceState}");
                }

                var submitted = await _inputChannel.Writer.WaitToWriteAsync(cancellationToken);
                if (!submitted)
                {
                    return TradingResult.Failure(
                        TradingError.ErrorCodes.ServiceUnavailable,
                        "Input channel is closed");
                }

                await _inputChannel.Writer.WriteAsync(input, cancellationToken);
                UpdateMetric("InputQueueDepth", _inputChannel.Reader.Count);
                
                return TradingResult.Success();

            }, $"Submit item to {ComponentName}",
               "Failed to submit item for processing",
               "Check engine state and queue capacity",
               methodName);
        }

        /// <summary>
        /// Submits multiple items for processing
        /// </summary>
        public async Task<TradingResult<int>> SubmitBatchAsync(
            IEnumerable<TInput> inputs,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string methodName = "")
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                ValidateNotNull(inputs, nameof(inputs));

                var items = inputs.ToList();
                if (!items.Any())
                {
                    return TradingResult<int>.Success(0);
                }

                if (ServiceState != ServiceState.Running)
                {
                    return TradingResult<int>.Failure(
                        TradingError.ErrorCodes.ServiceUnavailable,
                        $"{ComponentName} is not running. Current state: {ServiceState}");
                }

                var submitted = 0;
                foreach (var input in items)
                {
                    if (await _inputChannel.Writer.WaitToWriteAsync(cancellationToken))
                    {
                        await _inputChannel.Writer.WriteAsync(input, cancellationToken);
                        submitted++;
                    }
                    else
                    {
                        break;
                    }
                }

                UpdateMetric("InputQueueDepth", _inputChannel.Reader.Count);
                
                if (submitted < items.Count)
                {
                    LogWarning($"Partial batch submission: {submitted}/{items.Count} items",
                        impact: "Some items were not queued for processing");
                }

                return TradingResult<int>.Success(submitted);

            }, $"Submit batch of {inputs.Count()} items to {ComponentName}",
               "Failed to submit batch for processing",
               "Check engine state and queue capacity",
               methodName);
        }

        /// <summary>
        /// Reads processed output
        /// </summary>
        public async Task<TradingResult<TOutput>> ReadOutputAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (await _outputChannel.Reader.WaitToReadAsync(cancellationToken))
                {
                    if (_outputChannel.Reader.TryRead(out var output))
                    {
                        UpdateMetric("OutputQueueDepth", _outputChannel.Reader.Count);
                        return TradingResult<TOutput>.Success(output);
                    }
                }

                return TradingResult<TOutput>.Failure(
                    TradingError.ErrorCodes.ServiceUnavailable,
                    "No output available");
            }
            catch (OperationCanceledException)
            {
                return TradingResult<TOutput>.Failure(
                    TradingError.ErrorCodes.TimeoutExceeded,
                    "Read operation was cancelled");
            }
        }

        /// <summary>
        /// Gets the output channel reader for direct consumption
        /// </summary>
        public ChannelReader<TOutput> GetOutputReader() => _outputChannel.Reader;

        #endregion

        #region Processing Pipeline

        private async Task ProcessingWorker(int workerId, CancellationToken cancellationToken)
        {
            LogDebug($"Worker {workerId} started");
            var workerMetrics = new EngineMetrics();

            try
            {
                if (EnableBatching)
                {
                    await ProcessBatchedAsync(workerId, workerMetrics, cancellationToken);
                }
                else
                {
                    await ProcessSingleAsync(workerId, workerMetrics, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                LogError($"Worker {workerId} failed", ex,
                    operationContext: $"Processing worker {workerId}",
                    userImpact: "Reduced processing capacity",
                    troubleshootingHints: "Check worker logs and restart engine if needed");
            }
            finally
            {
                LogDebug($"Worker {workerId} stopped. Processed: {workerMetrics.ProcessedCount}");
            }
        }

        private async Task ProcessSingleAsync(
            int workerId,
            EngineMetrics metrics,
            CancellationToken cancellationToken)
        {
            await foreach (var input in _inputChannel.Reader.ReadAllAsync(cancellationToken))
            {
                await _processingThrottle.WaitAsync(cancellationToken);
                
                try
                {
                    var result = await ProcessItemWithMetricsAsync(input, metrics, cancellationToken);
                    
                    if (result.IsSuccess && result.Value != null)
                    {
                        await _outputChannel.Writer.WriteAsync(result.Value, cancellationToken);
                    }
                }
                finally
                {
                    _processingThrottle.Release();
                }
            }
        }

        private async Task ProcessBatchedAsync(
            int workerId,
            EngineMetrics metrics,
            CancellationToken cancellationToken)
        {
            var batch = new List<TInput>(BatchSize);
            
            await foreach (var input in _inputChannel.Reader.ReadAllAsync(cancellationToken))
            {
                batch.Add(input);
                
                if (batch.Count >= BatchSize || !await HasMoreInputAsync())
                {
                    await ProcessBatchWithMetricsAsync(batch, metrics, cancellationToken);
                    batch.Clear();
                }
            }

            // Process remaining items
            if (batch.Count > 0)
            {
                await ProcessBatchWithMetricsAsync(batch, metrics, cancellationToken);
            }
        }

        private async Task<bool> HasMoreInputAsync()
        {
            // Check if more items are immediately available
            await Task.Delay(10); // Small delay to allow batching
            return _inputChannel.Reader.TryPeek(out _);
        }

        private async Task<TradingResult<TOutput>> ProcessItemWithMetricsAsync(
            TInput input,
            EngineMetrics metrics,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(ProcessingTimeoutSeconds));

                var result = await ProcessItemAsync(input, cts.Token);
                
                stopwatch.Stop();
                metrics.RecordProcessing(stopwatch.Elapsed, result.IsSuccess);
                
                if (result.IsSuccess)
                {
                    Interlocked.Increment(ref _totalProcessed);
                }
                else
                {
                    Interlocked.Increment(ref _totalFailed);
                    LogDebug($"Processing failed: {result.Error?.Message}");
                }

                return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                metrics.RecordTimeout();
                Interlocked.Increment(ref _totalFailed);
                
                return TradingResult<TOutput>.Failure(
                    TradingError.ErrorCodes.TimeoutExceeded,
                    $"Processing timeout exceeded ({ProcessingTimeoutSeconds}s)");
            }
            catch (Exception ex)
            {
                metrics.RecordError();
                Interlocked.Increment(ref _totalFailed);
                
                return TradingResult<TOutput>.Failure(
                    TradingError.ErrorCodes.SystemError,
                    "Processing failed with exception",
                    ex);
            }
        }

        private async Task ProcessBatchWithMetricsAsync(
            List<TInput> batch,
            EngineMetrics metrics,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(ProcessingTimeoutSeconds * 2)); // More time for batches

                var results = await ProcessBatchAsync(batch, cts.Token);
                
                stopwatch.Stop();
                
                foreach (var result in results)
                {
                    metrics.RecordProcessing(stopwatch.Elapsed / batch.Count, result.IsSuccess);
                    
                    if (result.IsSuccess)
                    {
                        Interlocked.Increment(ref _totalProcessed);
                        if (result.Value != null)
                        {
                            await _outputChannel.Writer.WriteAsync(result.Value, cancellationToken);
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref _totalFailed);
                    }
                }
            }
            catch (Exception ex)
            {
                metrics.RecordError();
                Interlocked.Add(ref _totalFailed, batch.Count);
                
                LogError($"Batch processing failed for {batch.Count} items", ex,
                    operationContext: "Batch processing",
                    userImpact: $"{batch.Count} items failed to process");
            }
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Processes a single item
        /// </summary>
        protected abstract Task<TradingResult<TOutput>> ProcessItemAsync(
            TInput input,
            CancellationToken cancellationToken);

        /// <summary>
        /// Processes a batch of items (default implementation processes individually)
        /// </summary>
        protected virtual async Task<IEnumerable<TradingResult<TOutput>>> ProcessBatchAsync(
            IEnumerable<TInput> inputs,
            CancellationToken cancellationToken)
        {
            // Default implementation processes items individually
            var tasks = inputs.Select(input => ProcessItemAsync(input, cancellationToken));
            return await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Starts any additional pipeline stages
        /// </summary>
        protected virtual Task StartPipelineAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        #endregion

        #region Metrics

        private void CollectMetrics(object? state)
        {
            try
            {
                var throughput = _engineStopwatch.Elapsed.TotalSeconds > 0
                    ? _totalProcessed / _engineStopwatch.Elapsed.TotalSeconds
                    : 0;

                UpdateMetric("Throughput", throughput);
                UpdateMetric("TotalProcessed", _totalProcessed);
                UpdateMetric("TotalFailed", _totalFailed);
                UpdateMetric("TotalSkipped", _totalSkipped);
                UpdateMetric("InputQueueDepth", _inputChannel.Reader.Count);
                UpdateMetric("OutputQueueDepth", _outputChannel.Reader.Count);
                UpdateMetric("ProcessingThreads", MaxConcurrency - _processingThrottle.CurrentCount);

                if (_totalProcessed > 0)
                {
                    UpdateMetric("SuccessRate", (double)(_totalProcessed - _totalFailed) / _totalProcessed);
                }

                // Log performance warning if queues are getting full
                var inputUtilization = (double)_inputChannel.Reader.Count / InputQueueCapacity;
                var outputUtilization = (double)_outputChannel.Reader.Count / OutputQueueCapacity;

                if (inputUtilization > 0.8)
                {
                    LogWarning($"Input queue is {inputUtilization:P0} full",
                        impact: "May start rejecting new items",
                        troubleshooting: "Increase processing capacity or reduce input rate");
                }

                if (outputUtilization > 0.8)
                {
                    LogWarning($"Output queue is {outputUtilization:P0} full",
                        impact: "Processing may slow down",
                        troubleshooting: "Ensure output is being consumed");
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to collect metrics", ex);
            }
        }

        public override IReadOnlyDictionary<string, object> GetMetrics()
        {
            var metrics = base.GetMetrics().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            // Add engine-specific metrics
            metrics["Engine.MaxConcurrency"] = MaxConcurrency;
            metrics["Engine.BatchSize"] = BatchSize;
            metrics["Engine.BatchingEnabled"] = EnableBatching;
            metrics["Engine.ParallelProcessingEnabled"] = EnableParallelProcessing;
            metrics["Engine.ProcessingTimeoutSeconds"] = ProcessingTimeoutSeconds;
            
            // Add pipeline metrics
            foreach (var kvp in _pipelineMetrics)
            {
                metrics[$"Pipeline.{kvp.Key}.ProcessedCount"] = kvp.Value.ProcessedCount;
                metrics[$"Pipeline.{kvp.Key}.ErrorCount"] = kvp.Value.ErrorCount;
                metrics[$"Pipeline.{kvp.Key}.AverageLatencyMs"] = kvp.Value.AverageLatencyMs;
            }
            
            return metrics;
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _processingCts?.Cancel();
                _processingCts?.Dispose();
                _processingThrottle?.Dispose();
                _metricsTimer?.Dispose();
            }
            
            base.Dispose(disposing);
        }

        #endregion

        #region Helper Classes

        private class EngineMetrics
        {
            private long _processedCount;
            private long _errorCount;
            private long _timeoutCount;
            private double _totalLatencyMs;
            private readonly object _lock = new object();

            public long ProcessedCount => _processedCount;
            public long ErrorCount => _errorCount;
            public long TimeoutCount => _timeoutCount;
            
            public double AverageLatencyMs
            {
                get
                {
                    lock (_lock)
                    {
                        return _processedCount > 0 ? _totalLatencyMs / _processedCount : 0;
                    }
                }
            }

            public void RecordProcessing(TimeSpan duration, bool success)
            {
                lock (_lock)
                {
                    if (success)
                    {
                        _processedCount++;
                    }
                    else
                    {
                        _errorCount++;
                    }
                    _totalLatencyMs += duration.TotalMilliseconds;
                }
            }

            public void RecordError()
            {
                Interlocked.Increment(ref _errorCount);
            }

            public void RecordTimeout()
            {
                Interlocked.Increment(ref _timeoutCount);
                Interlocked.Increment(ref _errorCount);
            }
        }

        #endregion
    }
}