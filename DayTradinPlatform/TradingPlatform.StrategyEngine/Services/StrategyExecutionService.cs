using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TradingPlatform.Messaging.Events;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.StrategyEngine.Models;

namespace TradingPlatform.StrategyEngine.Services;

/// <summary>
/// High-performance strategy execution service for on-premise trading workstation
/// Processes market data events and executes trading strategies with sub-millisecond targets
/// </summary>
public class StrategyExecutionService : IStrategyExecutionService
{
    private readonly IMessageBus _messageBus;
    private readonly IStrategyManager _strategyManager;
    private readonly ISignalProcessor _signalProcessor;
    private readonly IPerformanceTracker _performanceTracker;
    private readonly ILogger<StrategyExecutionService> _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;
    
    // Performance tracking
    private long _signalsProcessed;
    private long _totalExecutions;
    private readonly List<TimeSpan> _executionLatencies = new();
    private readonly object _metricsLock = new();
    private readonly DateTime _startTime = DateTime.UtcNow;

    public StrategyExecutionService(
        IMessageBus messageBus,
        IStrategyManager strategyManager,
        ISignalProcessor signalProcessor,
        IPerformanceTracker performanceTracker,
        ILogger<StrategyExecutionService> logger)
    {
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        _strategyManager = strategyManager ?? throw new ArgumentNullException(nameof(strategyManager));
        _signalProcessor = signalProcessor ?? throw new ArgumentNullException(nameof(signalProcessor));
        _performanceTracker = performanceTracker ?? throw new ArgumentNullException(nameof(performanceTracker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task StartBackgroundProcessingAsync()
    {
        _logger.LogInformation("Starting background Redis Streams processing for strategy execution");

        try
        {
            // Subscribe to market data events for strategy processing
            await _messageBus.SubscribeAsync<MarketDataEvent>("market-data", 
                "strategy-group", "strategy-consumer", 
                HandleMarketDataEvent, _cancellationTokenSource.Token);

            // Subscribe to strategy control events from Gateway
            await _messageBus.SubscribeAsync<StrategyEvent>("strategies", 
                "strategy-group", "control-consumer", 
                HandleStrategyControlEvent, _cancellationTokenSource.Token);

            _logger.LogInformation("Background processing started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting background processing");
            throw;
        }
    }

    public async Task<ExecutionMetrics> GetExecutionMetricsAsync()
    {
        await Task.CompletedTask;
        
        lock (_metricsLock)
        {
            var uptime = DateTime.UtcNow - _startTime;
            var averageLatency = _executionLatencies.Count > 0 ?
                TimeSpan.FromTicks((long)_executionLatencies.Average(l => l.Ticks)) : TimeSpan.Zero;
            var maxLatency = _executionLatencies.Count > 0 ?
                _executionLatencies.Max() : TimeSpan.Zero;

            return new ExecutionMetrics(
                _signalsProcessed,
                averageLatency,
                maxLatency,
                0, // Active strategies - to be implemented
                _totalExecutions,
                uptime,
                DateTimeOffset.UtcNow);
        }
    }

    public async Task<StrategyHealthStatus> GetHealthStatusAsync()
    {
        try
        {
            var isHealthy = await _messageBus.IsHealthyAsync();
            var activeStrategies = await _strategyManager.GetActiveStrategiesAsync();
            
            var issues = new List<string>();
            
            // Check for performance issues
            var latencyStats = await GetLatencyStatsAsync();
            if (latencyStats.Average.TotalMilliseconds > 50) // Target: <50ms execution
                issues.Add($"High execution latency: {latencyStats.Average.TotalMilliseconds:F1}ms");
            
            if (activeStrategies.Length == 0)
                issues.Add("No active strategies running");

            return new StrategyHealthStatus(
                isHealthy && issues.Count == 0,
                isHealthy ? "Healthy" : "Degraded",
                activeStrategies.Length,
                _signalsProcessed,
                latencyStats.Average,
                DateTime.UtcNow,
                issues.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health status");
            return new StrategyHealthStatus(false, "Error", 0, 0, TimeSpan.Zero, DateTime.UtcNow, 
                new[] { ex.Message });
        }
    }

    public async Task<StrategyResult> ExecuteStrategyAsync(string strategyId, MarketConditions conditions)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Get strategy configuration
            var strategyConfig = await _strategyManager.GetStrategyConfigAsync(strategyId);
            if (strategyConfig == null)
            {
                return new StrategyResult(false, $"Strategy {strategyId} not found");
            }

            if (!strategyConfig.IsEnabled)
            {
                return new StrategyResult(false, $"Strategy {strategyId} is disabled");
            }

            // Process market data and generate signals
            var signals = await _signalProcessor.ProcessMarketDataAsync(conditions.Symbol, conditions);
            
            if (signals.Length > 0)
            {
                _logger.LogInformation("Generated {SignalCount} signals for strategy {StrategyId} on {Symbol}", 
                    signals.Length, strategyId, conditions.Symbol);

                // Process each signal
                foreach (var signal in signals)
                {
                    // Validate signal against risk parameters
                    var riskAssessment = await _signalProcessor.ValidateSignalAsync(signal);
                    
                    if (riskAssessment.IsAcceptable)
                    {
                        // Publish signal for order execution
                        var signalEvent = new StrategyEvent
                        {
                            StrategyName = strategyId,
                            Signal = signal.SignalType.ToString(),
                            Source = "StrategyEngine"
                        };

                        await _messageBus.PublishAsync("strategies", signalEvent);
                        
                        Interlocked.Increment(ref _signalsProcessed);
                    }
                    else
                    {
                        _logger.LogWarning("Signal rejected by risk assessment for {StrategyId}: {Reason}", 
                            strategyId, "Risk limits exceeded");
                    }
                }
            }

            stopwatch.Stop();
            RecordExecutionLatency(stopwatch.Elapsed);
            Interlocked.Increment(ref _totalExecutions);

            // Log performance for strategy execution latency tracking
            if (stopwatch.Elapsed.TotalMilliseconds > 45) // Target: <45ms strategy execution
            {
                _logger.LogWarning("Strategy execution exceeded 45ms target: {ElapsedMilliseconds}ms for {StrategyId}",
                    stopwatch.Elapsed.TotalMilliseconds, strategyId);
            }

            _logger.LogDebug("Strategy {StrategyId} executed in {ElapsedMicroseconds}μs", 
                strategyId, stopwatch.Elapsed.TotalMicroseconds);

            return new StrategyResult(true, $"Strategy {strategyId} executed successfully", 
                null, new Dictionary<string, object> 
                { 
                    ["SignalCount"] = signals.Length,
                    ["ExecutionTime"] = stopwatch.Elapsed
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error executing strategy {StrategyId}", strategyId);
            return new StrategyResult(false, ex.Message, "EXECUTION_ERROR");
        }
    }

    public async Task<LatencyStats> GetLatencyStatsAsync()
    {
        await Task.CompletedTask;
        
        lock (_metricsLock)
        {
            if (_executionLatencies.Count == 0)
            {
                return new LatencyStats(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 
                    TimeSpan.Zero, TimeSpan.Zero, 0, DateTime.UtcNow);
            }

            var sorted = _executionLatencies.OrderBy(l => l.Ticks).ToArray();
            var average = TimeSpan.FromTicks((long)sorted.Average(l => l.Ticks));
            var p50 = sorted[sorted.Length / 2];
            var p95 = sorted[(int)(sorted.Length * 0.95)];
            var p99 = sorted[(int)(sorted.Length * 0.99)];
            var max = sorted.Last();

            return new LatencyStats(average, p50, p95, p99, max, sorted.Length, DateTime.UtcNow);
        }
    }

    // Private helper methods
    private async Task HandleMarketDataEvent(MarketDataEvent marketDataEvent)
    {
        try
        {
            _logger.LogDebug("Processing market data event for {Symbol}", marketDataEvent.Symbol);

            // Create market conditions from market data
            var conditions = new MarketConditions(
                marketDataEvent.Symbol,
                0.02m, // Mock volatility - to be calculated from actual data
                marketDataEvent.Volume,
                0.0m, // Mock price change - to be calculated
                TrendDirection.Unknown, // To be determined by technical analysis
                50.0m, // Mock RSI
                0.0m, // Mock MACD
                DateTimeOffset.UtcNow);

            // Get active strategies that trade this symbol
            var activeStrategies = await _strategyManager.GetActiveStrategiesAsync();
            
            foreach (var strategy in activeStrategies.Where(s => s.Status == StrategyStatus.Running))
            {
                // Execute strategy for this market data
                await ExecuteStrategyAsync(strategy.Id, conditions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling market data event for {Symbol}", marketDataEvent.Symbol);
        }
    }

    private async Task HandleStrategyControlEvent(StrategyEvent strategyEvent)
    {
        try
        {
            _logger.LogInformation("Processing strategy control event: {Signal} for {StrategyName}", 
                strategyEvent.Signal, strategyEvent.StrategyName);

            switch (strategyEvent.Signal?.ToLowerInvariant())
            {
                case "start":
                    await _strategyManager.StartStrategyAsync(strategyEvent.StrategyName);
                    break;
                case "stop":
                    await _strategyManager.StopStrategyAsync(strategyEvent.StrategyName);
                    break;
                default:
                    _logger.LogWarning("Unknown strategy control signal: {Signal}", strategyEvent.Signal);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling strategy control event for {StrategyName}", 
                strategyEvent.StrategyName);
        }
    }

    private void RecordExecutionLatency(TimeSpan latency)
    {
        lock (_metricsLock)
        {
            _executionLatencies.Add(latency);
            
            // Keep only last 1000 samples for memory efficiency
            if (_executionLatencies.Count > 1000)
            {
                _executionLatencies.RemoveAt(0);
            }
        }
    }
}