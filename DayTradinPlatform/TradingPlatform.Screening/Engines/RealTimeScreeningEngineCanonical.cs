using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Screening.Models;

namespace TradingPlatform.Screening.Engines
{
    /// <summary>
    /// Canonical implementation of real-time screening engine.
    /// Leverages CanonicalEngine base class for standardized pipeline management and performance optimization.
    /// </summary>
    public class RealTimeScreeningEngineCanonical : CanonicalEngine<ScreeningRequest, ScreeningResult>
    {
        private readonly IMarketDataProvider _marketDataProvider;
        private readonly ScreeningOrchestrator _orchestrator;
        private readonly ConcurrentDictionary<string, ScreeningResult> _lastResults;
        private readonly ConcurrentDictionary<string, DateTime> _lastScreeningTimes;
        private readonly Subject<ScreeningResult> _screeningResultsSubject;
        
        #region Configuration Overrides

        protected override int MaxConcurrency => 50; // Parallel symbol processing
        protected override int InputQueueCapacity => 10000;
        protected override int OutputQueueCapacity => 10000;
        protected override int BatchSize => 100;
        protected override int ProcessingTimeoutSeconds => 30;
        protected override bool EnableBatching => true;
        protected override bool EnableParallelProcessing => true;

        #endregion

        #region Constructor

        public RealTimeScreeningEngineCanonical(
            IMarketDataProvider marketDataProvider,
            ScreeningOrchestrator orchestrator,
            ITradingLogger logger)
            : base(logger, "RealTimeScreeningEngine")
        {
            _marketDataProvider = marketDataProvider;
            _orchestrator = orchestrator;
            _lastResults = new ConcurrentDictionary<string, ScreeningResult>();
            _lastScreeningTimes = new ConcurrentDictionary<string, DateTime>();
            _screeningResultsSubject = new Subject<ScreeningResult>();
            
            LogMethodEntry(new { 
                MarketDataProvider = marketDataProvider.ProviderName,
                MaxConcurrency,
                BatchSize 
            });
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Screens multiple symbols based on the provided request
        /// </summary>
        public async Task<List<ScreeningResult>> ScreenSymbolsAsync(ScreeningRequest request)
        {
            return await ExecuteWithLoggingAsync(async () =>
            {
                LogInfo($"Starting batch screening for {request.Symbols.Count} symbols");
                var stopwatch = Stopwatch.StartNew();

                // Submit the screening request
                var submitResult = await SubmitAsync(request);
                if (!submitResult.IsSuccess)
                {
                    throw new InvalidOperationException($"Failed to submit screening request: {submitResult.Error?.Message}");
                }

                // Collect results
                var results = new List<ScreeningResult>();
                var timeout = TimeSpan.FromSeconds(ProcessingTimeoutSeconds * 2); // Allow more time for batch
                var cts = new CancellationTokenSource(timeout);

                try
                {
                    await foreach (var output in GetOutputReader().ReadAllAsync(cts.Token))
                    {
                        if (output.Symbol == request.RequestId) // End marker
                        {
                            break;
                        }
                        results.Add(output);
                    }
                }
                catch (OperationCanceledException)
                {
                    LogWarning($"Screening timeout reached after {timeout.TotalSeconds}s");
                }

                // Apply filtering and sorting
                var finalResults = results
                    .Where(r => r.MeetsCriteria || request.MaxResults <= 0)
                    .OrderByDescending(r => r.OverallScore)
                    .Take(request.MaxResults > 0 ? request.MaxResults : int.MaxValue)
                    .ToList();

                stopwatch.Stop();
                UpdateMetric("LastBatchScreeningDuration", stopwatch.ElapsedMilliseconds);
                UpdateMetric("LastBatchSymbolCount", request.Symbols.Count);
                UpdateMetric("LastBatchResultCount", finalResults.Count);
                
                LogInfo($"Batch screening completed: {finalResults.Count}/{request.Symbols.Count} symbols passed in {stopwatch.ElapsedMilliseconds}ms");

                return finalResults;
                
            }, "Screen symbols batch", 
               "Batch screening operation failed",
               "Check market data availability and screening criteria");
        }

        /// <summary>
        /// Starts real-time screening with periodic updates
        /// </summary>
        public IObservable<ScreeningResult> StartRealTimeScreeningAsync(ScreeningRequest request)
        {
            return Observable.Create<ScreeningResult>(async (observer, cancellationToken) =>
            {
                LogInfo($"Starting real-time screening for {request.Symbols.Count} symbols with {request.UpdateInterval.TotalSeconds}s interval");
                
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var results = await ScreenSymbolsAsync(request);
                        
                        foreach (var result in results)
                        {
                            // Check if result has changed significantly
                            if (HasSignificantChange(result))
                            {
                                observer.OnNext(result);
                                _screeningResultsSubject.OnNext(result);
                                _lastResults[result.Symbol] = result;
                                _lastScreeningTimes[result.Symbol] = DateTime.UtcNow;
                            }
                        }
                        
                        await Task.Delay(request.UpdateInterval, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    LogError("Error in real-time screening", ex,
                        operationContext: "Real-time screening loop",
                        userImpact: "Real-time updates stopped",
                        troubleshootingHints: "Check market data provider and network connectivity");
                    observer.OnError(ex);
                }
                finally
                {
                    observer.OnCompleted();
                }
            });
        }

        /// <summary>
        /// Gets observable stream of screening results
        /// </summary>
        public IObservable<ScreeningResult> ScreeningResults => _screeningResultsSubject.AsObservable();

        /// <summary>
        /// Gets the last screening result for a symbol
        /// </summary>
        public ScreeningResult? GetLastResult(string symbol)
        {
            return _lastResults.TryGetValue(symbol, out var result) ? result : null;
        }

        /// <summary>
        /// Gets screening performance metrics
        /// </summary>
        public ScreeningPerformanceMetrics GetPerformanceMetrics()
        {
            var metrics = GetMetrics();
            
            return new ScreeningPerformanceMetrics
            {
                TotalSymbolsProcessed = metrics.ContainsKey("TotalProcessed") 
                    ? Convert.ToInt64(metrics["TotalProcessed"]) : 0,
                AverageScreeningTimeMs = metrics.ContainsKey("AverageLatencyMs") 
                    ? Convert.ToDouble(metrics["AverageLatencyMs"]) : 0,
                ThroughputSymbolsPerSecond = metrics.ContainsKey("Throughput") 
                    ? Convert.ToDouble(metrics["Throughput"]) : 0,
                ActiveScreeningRequests = metrics.ContainsKey("InputQueueDepth") 
                    ? Convert.ToInt32(metrics["InputQueueDepth"]) : 0,
                LastUpdateTime = DateTime.UtcNow
            };
        }

        #endregion

        #region Protected Overrides

        protected override async Task<TradingResult<ScreeningResult>> ProcessItemAsync(
            ScreeningRequest input, 
            CancellationToken cancellationToken)
        {
            // For single symbol processing
            if (input.Symbols.Count == 1)
            {
                return await ProcessSingleSymbolAsync(input.Symbols[0], input.Criteria, cancellationToken);
            }
            
            // Should use batch processing for multiple symbols
            return TradingResult<ScreeningResult>.Failure(
                TradingError.ErrorCodes.ValidationFailed,
                "Use batch processing for multiple symbols");
        }

        protected override async Task<IEnumerable<TradingResult<ScreeningResult>>> ProcessBatchAsync(
            IEnumerable<ScreeningRequest> inputs,
            CancellationToken cancellationToken)
        {
            var results = new List<TradingResult<ScreeningResult>>();
            
            foreach (var request in inputs)
            {
                // Process each symbol in the request
                var tasks = request.Symbols.Select(symbol => 
                    ProcessSingleSymbolAsync(symbol, request.Criteria, cancellationToken));
                
                var batchResults = await Task.WhenAll(tasks);
                results.AddRange(batchResults);
                
                // Add end marker
                results.Add(TradingResult<ScreeningResult>.Success(
                    new ScreeningResult 
                    { 
                        Symbol = request.RequestId, // Use as end marker
                        Timestamp = DateTime.UtcNow 
                    }));
            }
            
            return results;
        }

        protected override Task StartPipelineAsync(CancellationToken cancellationToken)
        {
            LogInfo("Starting screening pipeline");
            
            // Could start additional background tasks here if needed
            // For example, pre-fetching market data, warming up caches, etc.
            
            return Task.CompletedTask;
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo("Initializing screening engine", new
            {
                MarketDataProvider = _marketDataProvider.ProviderName,
                ScreeningCriteria = _orchestrator.GetType().Name
            });
            
            return Task.CompletedTask;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo("Starting screening engine");
            UpdateMetric("EngineStartTime", DateTime.UtcNow);
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo("Stopping screening engine", new
            {
                TotalProcessed = _lastResults.Count,
                LastScreeningTime = _lastScreeningTimes.Values.DefaultIfEmpty().Max()
            });
            
            _screeningResultsSubject.OnCompleted();
            return Task.CompletedTask;
        }

        #endregion

        #region Private Methods

        private async Task<TradingResult<ScreeningResult>> ProcessSingleSymbolAsync(
            string symbol, 
            TradingCriteria criteria,
            CancellationToken cancellationToken)
        {
            try
            {
                // Fetch market data
                var marketData = await _marketDataProvider.GetRealTimeDataAsync(symbol);
                if (marketData == null)
                {
                    LogWarning($"No market data available for {symbol}");
                    return TradingResult<ScreeningResult>.Failure(
                        TradingError.ErrorCodes.MarketDataUnavailable,
                        $"No market data available for {symbol}");
                }

                // Perform screening evaluation
                var result = await _orchestrator.EvaluateAllAsync(marketData, criteria);
                if (result == null)
                {
                    return TradingResult<ScreeningResult>.Failure(
                        TradingError.ErrorCodes.SystemError,
                        $"Screening evaluation failed for {symbol}");
                }

                // Update metrics
                IncrementCounter("SymbolsScreened");
                if (result.MeetsCriteria)
                {
                    IncrementCounter("SymbolsPassed");
                }

                return TradingResult<ScreeningResult>.Success(result);
            }
            catch (Exception ex)
            {
                LogError($"Error screening symbol {symbol}", ex,
                    operationContext: $"Screening {symbol}",
                    userImpact: "Symbol screening failed",
                    troubleshootingHints: "Check market data and criteria configuration");
                
                return TradingResult<ScreeningResult>.Failure(
                    TradingError.ErrorCodes.SystemError,
                    $"Error screening {symbol}: {ex.Message}",
                    ex);
            }
        }

        private bool HasSignificantChange(ScreeningResult newResult)
        {
            if (!_lastResults.TryGetValue(newResult.Symbol, out var lastResult))
            {
                return true; // First time seeing this symbol
            }

            // Check if overall score changed significantly (>5%)
            var scoreDiff = Math.Abs(newResult.OverallScore - lastResult.OverallScore);
            if (scoreDiff / Math.Max(lastResult.OverallScore, 0.01m) > 0.05m)
            {
                return true;
            }

            // Check if criteria met status changed
            if (newResult.MeetsCriteria != lastResult.MeetsCriteria)
            {
                return true;
            }

            // Check if any individual criterion score changed significantly
            foreach (var kvp in newResult.CriteriaResults)
            {
                if (!lastResult.CriteriaResults.TryGetValue(kvp.Key, out var lastCriterion))
                {
                    return true;
                }

                if (kvp.Value.IsMet != lastCriterion.IsMet)
                {
                    return true;
                }

                var criteriaDiff = Math.Abs(kvp.Value.Score - lastCriterion.Score);
                if (criteriaDiff > 0.1m) // 10% change in individual criterion
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _screeningResultsSubject?.Dispose();
            }
            
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// Performance metrics for the screening engine
    /// </summary>
    public class ScreeningPerformanceMetrics
    {
        public long TotalSymbolsProcessed { get; set; }
        public double AverageScreeningTimeMs { get; set; }
        public double ThroughputSymbolsPerSecond { get; set; }
        public int ActiveScreeningRequests { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }
}