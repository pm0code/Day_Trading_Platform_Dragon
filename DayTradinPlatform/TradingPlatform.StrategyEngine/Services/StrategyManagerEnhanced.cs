using System.Collections.Concurrent;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Models;
using TradingPlatform.StrategyEngine.Models;

namespace TradingPlatform.StrategyEngine.Services;

/// <summary>
/// Enhanced strategy manager with comprehensive MCP-compliant logging and operation tracking
/// Provides complete strategy lifecycle management with event codes and performance monitoring
/// </summary>
public class StrategyManagerEnhanced : CanonicalServiceBaseEnhanced, IStrategyManager
{
    private readonly ConcurrentDictionary<string, StrategyInfo> _activeStrategies;
    private readonly ConcurrentDictionary<string, StrategyConfig> _strategyConfigs;
    private readonly ConcurrentDictionary<string, StrategyPerformanceMetrics> _performanceMetrics;
    private readonly Timer _healthCheckTimer;
    private readonly Timer _performanceUpdateTimer;

    // Performance thresholds for monitoring (using decimal for financial precision)
    protected virtual decimal AlertDrawdownThreshold => 0.05m;  // Alert on 5% drawdown
    protected virtual decimal AlertPnLThreshold => 1000.00m;    // Alert on $1k+ PnL moves (exact decimal)
    protected virtual int MaxConcurrentStrategies => 10;        // Maximum active strategies
    protected virtual TimeSpan HealthCheckInterval => TimeSpan.FromMinutes(1);
    
    // Financial calculation precision settings
    protected virtual int FinancialDecimalPlaces => 4;          // 4 decimal places for financial calculations
    protected virtual MidpointRounding RoundingStrategy => MidpointRounding.ToEven; // Banker's rounding

    public StrategyManagerEnhanced(ITradingLogger? logger = null) 
        : base(logger, "StrategyManager")
    {
        _activeStrategies = new ConcurrentDictionary<string, StrategyInfo>();
        _strategyConfigs = new ConcurrentDictionary<string, StrategyConfig>();
        _performanceMetrics = new ConcurrentDictionary<string, StrategyPerformanceMetrics>();

        // Initialize health check timer
        _healthCheckTimer = new Timer(PerformHealthCheck, null, HealthCheckInterval, HealthCheckInterval);
        
        // Initialize performance update timer
        _performanceUpdateTimer = new Timer(UpdatePerformanceMetrics, null, 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        InitializeDefaultStrategies();
        
        LogInfo("STRATEGY_MANAGER_INITIALIZED", "Enhanced strategy manager initialized successfully",
            additionalData: new
            {
                MaxConcurrentStrategies,
                AlertDrawdownThreshold,
                AlertPnLThreshold,
                HealthCheckIntervalMs = HealthCheckInterval.TotalMilliseconds
            });
    }

    #region IStrategyManager Implementation

    /// <summary>
    /// Gets all active strategies with comprehensive status information
    /// </summary>
    public async Task<StrategyInfo[]> GetActiveStrategiesAsync()
    {
        return await TrackOperationAsync("GetActiveStrategies", async () =>
        {
            LogDebug("STRATEGY_GET_ACTIVE_START", "Retrieving active strategies");
            
            var strategies = _activeStrategies.Values.ToArray();
            
            LogInfo("STRATEGY_GET_ACTIVE_COMPLETE", $"Retrieved {strategies.Length} active strategies",
                additionalData: new
                {
                    ActiveCount = strategies.Length,
                    RunningCount = strategies.Count(s => s.Status == StrategyStatus.Running),
                    PausedCount = strategies.Count(s => s.Status == StrategyStatus.Paused),
                    StoppedCount = strategies.Count(s => s.Status == StrategyStatus.Stopped)
                });

            return TradingResult<StrategyInfo[]>.Success(strategies);
        });
    }

    /// <summary>
    /// Starts a strategy with comprehensive validation and monitoring
    /// </summary>
    public async Task<StrategyResult> StartStrategyAsync(string strategyId)
    {
        return await TrackOperationAsync($"StartStrategy-{strategyId}", async () =>
        {
            LogInfo("STRATEGY_START_INITIATED", $"Starting strategy: {strategyId}");

            // Validate strategy configuration
            if (!_strategyConfigs.TryGetValue(strategyId, out var config))
            {
                LogWarning("STRATEGY_CONFIG_NOT_FOUND", $"Strategy configuration not found: {strategyId}");
                return TradingResult<StrategyResult>.Failure("CONFIG_NOT_FOUND", 
                    $"Strategy configuration not found for {strategyId}");
            }

            if (!config.IsEnabled)
            {
                LogWarning("STRATEGY_DISABLED", $"Strategy is disabled: {strategyId}");
                return TradingResult<StrategyResult>.Failure("STRATEGY_DISABLED", 
                    $"Strategy {strategyId} is disabled");
            }

            // Check concurrent strategy limits
            var runningCount = _activeStrategies.Values.Count(s => s.Status == StrategyStatus.Running);
            if (runningCount >= MaxConcurrentStrategies)
            {
                LogWarning("STRATEGY_LIMIT_EXCEEDED", $"Maximum concurrent strategies reached: {runningCount}");
                return TradingResult<StrategyResult>.Failure("LIMIT_EXCEEDED", 
                    $"Maximum concurrent strategies ({MaxConcurrentStrategies}) already running");
            }

            // Check if already running
            if (_activeStrategies.TryGetValue(strategyId, out var existingStrategy) &&
                existingStrategy.Status == StrategyStatus.Running)
            {
                LogWarning("STRATEGY_ALREADY_RUNNING", $"Strategy already running: {strategyId}");
                return TradingResult<StrategyResult>.Failure("ALREADY_RUNNING", 
                    $"Strategy {strategyId} is already running");
            }

            // Create new strategy instance
            var strategyInfo = new StrategyInfo(
                strategyId,
                config.Name,
                StrategyStatus.Starting,
                DateTimeOffset.UtcNow,
                0.0m, // Initial PnL
                0,    // Initial trade count
                config.Parameters);

            _activeStrategies.AddOrUpdate(strategyId, strategyInfo, (k, v) => strategyInfo);

            // Initialize performance metrics
            _performanceMetrics.AddOrUpdate(strategyId, 
                new StrategyPerformanceMetrics(strategyId), 
                (k, v) => new StrategyPerformanceMetrics(strategyId));

            LogInfo("STRATEGY_STARTING", $"Strategy startup initiated: {strategyId}", 
                additionalData: new { StrategyName = config.Name, Parameters = config.Parameters });

            // Simulate strategy startup process with validation
            await Task.Delay(150); // Realistic startup delay

            // Validate strategy dependencies and resources
            var validationResult = await ValidateStrategyDependencies(strategyId, config);
            if (!validationResult.IsSuccess)
            {
                LogError("STRATEGY_VALIDATION_FAILED", $"Strategy validation failed: {strategyId}", 
                    additionalData: new { Error = validationResult.Error?.Message });

                // Remove from active strategies
                _activeStrategies.TryRemove(strategyId, out _);
                _performanceMetrics.TryRemove(strategyId, out _);

                return TradingResult<StrategyResult>.Failure("VALIDATION_FAILED", 
                    validationResult.Error?.Message ?? "Strategy validation failed");
            }

            // Update status to running
            var runningStrategy = strategyInfo with { Status = StrategyStatus.Running };
            _activeStrategies.TryUpdate(strategyId, runningStrategy, strategyInfo);

            LogInfo("STRATEGY_STARTED_SUCCESSFULLY", $"Strategy started successfully: {strategyId}", 
                additionalData: new
                {
                    StrategyName = config.Name,
                    ActiveStrategies = _activeStrategies.Count,
                    RunningStrategies = _activeStrategies.Values.Count(s => s.Status == StrategyStatus.Running)
                });

            return TradingResult<StrategyResult>.Success(
                new StrategyResult(true, $"Strategy {strategyId} started successfully"));
        });
    }

    /// <summary>
    /// Stops a strategy with graceful shutdown and performance summary
    /// </summary>
    public async Task<StrategyResult> StopStrategyAsync(string strategyId)
    {
        return await TrackOperationAsync($"StopStrategy-{strategyId}", async () =>
        {
            LogInfo("STRATEGY_STOP_INITIATED", $"Stopping strategy: {strategyId}");

            if (!_activeStrategies.TryGetValue(strategyId, out var strategy))
            {
                LogWarning("STRATEGY_NOT_ACTIVE", $"Strategy not active: {strategyId}");
                return TradingResult<StrategyResult>.Failure("NOT_ACTIVE", 
                    $"Strategy {strategyId} is not active");
            }

            if (strategy.Status == StrategyStatus.Stopped)
            {
                LogWarning("STRATEGY_ALREADY_STOPPED", $"Strategy already stopped: {strategyId}");
                return TradingResult<StrategyResult>.Failure("ALREADY_STOPPED", 
                    $"Strategy {strategyId} is already stopped");
            }

            // Update status to stopping
            var stoppingStrategy = strategy with { Status = StrategyStatus.Stopping };
            _activeStrategies.TryUpdate(strategyId, stoppingStrategy, strategy);

            LogInfo("STRATEGY_STOPPING", $"Strategy shutdown initiated: {strategyId}");

            // Graceful shutdown process
            await Task.Delay(100); // Shutdown delay

            // Generate performance summary
            var performanceSummary = GeneratePerformanceSummary(strategyId);

            // Update status to stopped
            var stoppedStrategy = stoppingStrategy with { Status = StrategyStatus.Stopped };
            _activeStrategies.TryUpdate(strategyId, stoppedStrategy, stoppingStrategy);

            LogInfo("STRATEGY_STOPPED_SUCCESSFULLY", $"Strategy stopped successfully: {strategyId}", 
                additionalData: new
                {
                    StrategyName = strategy.Name,
                    FinalPnL = strategy.PnL,
                    TotalTrades = strategy.TradeCount,
                    RuntimeMinutes = (DateTimeOffset.UtcNow - strategy.StartedAt).TotalMinutes,
                    PerformanceSummary = performanceSummary
                });

            return TradingResult<StrategyResult>.Success(
                new StrategyResult(true, $"Strategy {strategyId} stopped successfully"));
        });
    }

    /// <summary>
    /// Gets strategy configuration with validation
    /// </summary>
    public async Task<StrategyConfig?> GetStrategyConfigAsync(string strategyId)
    {
        return await TrackOperationAsync($"GetConfig-{strategyId}", async () =>
        {
            LogDebug("STRATEGY_GET_CONFIG", $"Retrieving configuration for strategy: {strategyId}");
            
            var config = _strategyConfigs.TryGetValue(strategyId, out var value) ? value : null;
            
            if (config == null)
            {
                LogWarning("STRATEGY_CONFIG_NOT_FOUND", $"Configuration not found: {strategyId}");
            }

            return TradingResult<StrategyConfig?>.Success(config);
        });
    }

    /// <summary>
    /// Updates strategy configuration with validation and change tracking
    /// </summary>
    public async Task<StrategyResult> UpdateStrategyConfigAsync(StrategyConfig config)
    {
        return await TrackOperationAsync($"UpdateConfig-{config.StrategyId}", async () =>
        {
            LogInfo("STRATEGY_CONFIG_UPDATE_INITIATED", $"Updating configuration: {config.StrategyId}");

            // Validate configuration
            var validationResult = ValidateStrategyConfig(config);
            if (!validationResult.IsSuccess)
            {
                LogWarning("STRATEGY_CONFIG_VALIDATION_FAILED", 
                    $"Configuration validation failed: {config.StrategyId}",
                    additionalData: new { Error = validationResult.Error?.Message });

                return TradingResult<StrategyResult>.Failure("VALIDATION_FAILED", 
                    validationResult.Error?.Message ?? "Configuration validation failed");
            }

            // Track configuration changes
            var oldConfig = _strategyConfigs.TryGetValue(config.StrategyId, out var existing) ? existing : null;
            var changes = oldConfig != null ? DetectConfigurationChanges(oldConfig, config) : new string[] { "New configuration" };

            // Update configuration
            _strategyConfigs.AddOrUpdate(config.StrategyId, config, (k, v) => config);

            LogInfo("STRATEGY_CONFIG_UPDATED_SUCCESSFULLY", 
                $"Configuration updated successfully: {config.StrategyId}",
                additionalData: new
                {
                    StrategyName = config.Name,
                    IsEnabled = config.IsEnabled,
                    Changes = changes,
                    ParameterCount = config.Parameters.Count
                });

            return TradingResult<StrategyResult>.Success(
                new StrategyResult(true, $"Configuration updated for strategy {config.StrategyId}"));
        });
    }

    #endregion

    #region Enhanced Strategy Management

    /// <summary>
    /// Gets strategy by ID with detailed information
    /// </summary>
    public async Task<StrategyInfo?> GetStrategyAsync(string strategyId)
    {
        return await TrackOperationAsync($"GetStrategy-{strategyId}", async () =>
        {
            LogDebug("STRATEGY_GET_DETAILS", $"Retrieving strategy details: {strategyId}");
            
            var strategy = _activeStrategies.TryGetValue(strategyId, out var value) ? value : null;
            
            return TradingResult<StrategyInfo?>.Success(strategy);
        });
    }

    /// <summary>
    /// Updates strategy performance metrics with comprehensive tracking
    /// </summary>
    public async Task UpdateStrategyMetricsAsync(string strategyId, decimal pnlChange, int tradeCount)
    {
        await TrackOperationAsync($"UpdateMetrics-{strategyId}", async () =>
        {
            if (!_activeStrategies.TryGetValue(strategyId, out var strategy))
            {
                LogWarning("STRATEGY_METRICS_UPDATE_FAILED", $"Strategy not found for metrics update: {strategyId}");
                return TradingResult<bool>.Failure("STRATEGY_NOT_FOUND", "Strategy not found");
            }

            var oldPnL = strategy.PnL;
            var newPnL = oldPnL + pnlChange;
            var newTradeCount = strategy.TradeCount + tradeCount;

            var updatedStrategy = strategy with
            {
                PnL = newPnL,
                TradeCount = newTradeCount
            };

            _activeStrategies.TryUpdate(strategyId, updatedStrategy, strategy);

            // Update performance metrics
            if (_performanceMetrics.TryGetValue(strategyId, out var metrics))
            {
                var updatedMetrics = metrics.UpdateMetrics(pnlChange, tradeCount);
                _performanceMetrics.TryUpdate(strategyId, updatedMetrics, metrics);
            }

            // Check for significant PnL changes (using decimal.Abs for financial precision)
            if (decimal.Abs(pnlChange) >= AlertPnLThreshold)
            {
                LogWarning("STRATEGY_SIGNIFICANT_PNL_CHANGE", 
                    $"Significant PnL change detected: {strategyId}",
                    additionalData: new
                    {
                        PnLChange = pnlChange,
                        OldPnL = oldPnL,
                        NewPnL = newPnL,
                        AlertThreshold = AlertPnLThreshold
                    });
            }

            // Check for drawdown alerts
            var maxPnL = metrics?.MaxPnL ?? newPnL;
            if (newPnL < maxPnL)
            {
                var drawdown = (maxPnL - newPnL) / decimal.Max(decimal.Abs(maxPnL), 1.00m);
                if (drawdown >= AlertDrawdownThreshold)
                {
                    LogWarning("STRATEGY_DRAWDOWN_ALERT", 
                        $"Drawdown alert for strategy: {strategyId}",
                        additionalData: new
                        {
                            CurrentDrawdown = drawdown,
                            AlertThreshold = AlertDrawdownThreshold,
                            MaxPnL = maxPnL,
                            CurrentPnL = newPnL
                        });
                }
            }

            LogDebug("STRATEGY_METRICS_UPDATED", $"Metrics updated for strategy: {strategyId}",
                additionalData: new
                {
                    PnLChange = pnlChange,
                    NewPnL = newPnL,
                    TradeCountChange = tradeCount,
                    NewTradeCount = newTradeCount
                });

            return TradingResult<bool>.Success(true);
        });
    }

    /// <summary>
    /// Gets strategies by status with filtering
    /// </summary>
    public async Task<StrategyInfo[]> GetStrategiesByStatusAsync(StrategyStatus status)
    {
        return await TrackOperationAsync($"GetStrategiesByStatus-{status}", async () =>
        {
            LogDebug("STRATEGY_GET_BY_STATUS", $"Retrieving strategies with status: {status}");
            
            var strategies = _activeStrategies.Values.Where(s => s.Status == status).ToArray();
            
            LogDebug("STRATEGY_GET_BY_STATUS_COMPLETE", 
                $"Retrieved {strategies.Length} strategies with status {status}");

            return TradingResult<StrategyInfo[]>.Success(strategies);
        });
    }

    /// <summary>
    /// Pauses a running strategy with state preservation
    /// </summary>
    public async Task<StrategyResult> PauseStrategyAsync(string strategyId)
    {
        return await TrackOperationAsync($"PauseStrategy-{strategyId}", async () =>
        {
            LogInfo("STRATEGY_PAUSE_INITIATED", $"Pausing strategy: {strategyId}");

            if (!_activeStrategies.TryGetValue(strategyId, out var strategy))
            {
                LogWarning("STRATEGY_NOT_FOUND_FOR_PAUSE", $"Strategy not found: {strategyId}");
                return TradingResult<StrategyResult>.Failure("STRATEGY_NOT_FOUND", 
                    $"Strategy {strategyId} not found");
            }

            if (strategy.Status != StrategyStatus.Running)
            {
                LogWarning("STRATEGY_NOT_RUNNING_FOR_PAUSE", 
                    $"Strategy not running, cannot pause: {strategyId}");
                return TradingResult<StrategyResult>.Failure("NOT_RUNNING", 
                    $"Strategy {strategyId} is not running");
            }

            var pausedStrategy = strategy with { Status = StrategyStatus.Paused };
            _activeStrategies.TryUpdate(strategyId, pausedStrategy, strategy);

            LogInfo("STRATEGY_PAUSED_SUCCESSFULLY", $"Strategy paused successfully: {strategyId}",
                additionalData: new { StrategyName = strategy.Name });

            return TradingResult<StrategyResult>.Success(
                new StrategyResult(true, $"Strategy {strategyId} paused successfully"));
        });
    }

    /// <summary>
    /// Resumes a paused strategy with state restoration
    /// </summary>
    public async Task<StrategyResult> ResumeStrategyAsync(string strategyId)
    {
        return await TrackOperationAsync($"ResumeStrategy-{strategyId}", async () =>
        {
            LogInfo("STRATEGY_RESUME_INITIATED", $"Resuming strategy: {strategyId}");

            if (!_activeStrategies.TryGetValue(strategyId, out var strategy))
            {
                LogWarning("STRATEGY_NOT_FOUND_FOR_RESUME", $"Strategy not found: {strategyId}");
                return TradingResult<StrategyResult>.Failure("STRATEGY_NOT_FOUND", 
                    $"Strategy {strategyId} not found");
            }

            if (strategy.Status != StrategyStatus.Paused)
            {
                LogWarning("STRATEGY_NOT_PAUSED_FOR_RESUME", 
                    $"Strategy not paused, cannot resume: {strategyId}");
                return TradingResult<StrategyResult>.Failure("NOT_PAUSED", 
                    $"Strategy {strategyId} is not paused");
            }

            var runningStrategy = strategy with { Status = StrategyStatus.Running };
            _activeStrategies.TryUpdate(strategyId, runningStrategy, strategy);

            LogInfo("STRATEGY_RESUMED_SUCCESSFULLY", $"Strategy resumed successfully: {strategyId}",
                additionalData: new { StrategyName = strategy.Name });

            return TradingResult<StrategyResult>.Success(
                new StrategyResult(true, $"Strategy {strategyId} resumed successfully"));
        });
    }

    #endregion

    #region Health Monitoring

    /// <summary>
    /// Performs comprehensive health check of all strategies
    /// </summary>
    private void PerformHealthCheck(object? state)
    {
        try
        {
            LogDebug("STRATEGY_HEALTH_CHECK_START", "Starting strategy health check");

            var strategies = _activeStrategies.Values.ToArray();
            var healthIssues = new List<string>();

            foreach (var strategy in strategies)
            {
                // Check for stuck strategies
                var runtime = DateTimeOffset.UtcNow - strategy.StartedAt;
                if (strategy.Status == StrategyStatus.Starting && runtime > TimeSpan.FromMinutes(5))
                {
                    healthIssues.Add($"Strategy {strategy.Id} stuck in starting state for {runtime.TotalMinutes:F1} minutes");
                }

                if (strategy.Status == StrategyStatus.Stopping && runtime > TimeSpan.FromMinutes(2))
                {
                    healthIssues.Add($"Strategy {strategy.Id} stuck in stopping state for {runtime.TotalMinutes:F1} minutes");
                }

                // Check performance metrics
                if (_performanceMetrics.TryGetValue(strategy.Id, out var metrics))
                {
                    if (metrics.ConsecutiveLosses >= 5)
                    {
                        healthIssues.Add($"Strategy {strategy.Id} has {metrics.ConsecutiveLosses} consecutive losses");
                    }

                    var currentDrawdown = metrics.CurrentDrawdown;
                    if (currentDrawdown >= AlertDrawdownThreshold * 2) // Double the alert threshold
                    {
                        healthIssues.Add($"Strategy {strategy.Id} experiencing high drawdown: {currentDrawdown:P2}");
                    }
                }
            }

            if (healthIssues.Any())
            {
                LogWarning("STRATEGY_HEALTH_ISSUES_DETECTED", 
                    $"Health issues detected in {healthIssues.Count} strategies",
                    additionalData: new { Issues = healthIssues });
            }
            else
            {
                LogDebug("STRATEGY_HEALTH_CHECK_COMPLETE", 
                    $"Health check completed successfully for {strategies.Length} strategies");
            }
        }
        catch (Exception ex)
        {
            LogError("STRATEGY_HEALTH_CHECK_ERROR", "Error during strategy health check", ex);
        }
    }

    /// <summary>
    /// Updates performance metrics for all active strategies
    /// </summary>
    private void UpdatePerformanceMetrics(object? state)
    {
        try
        {
            var strategies = _activeStrategies.Values.Where(s => s.Status == StrategyStatus.Running).ToArray();
            
            LogDebug("STRATEGY_PERFORMANCE_UPDATE", 
                $"Updating performance metrics for {strategies.Length} running strategies");

            foreach (var strategy in strategies)
            {
                if (_performanceMetrics.TryGetValue(strategy.Id, out var metrics))
                {
                    // Update runtime statistics
                    var updatedMetrics = metrics.UpdateRuntime();
                    _performanceMetrics.TryUpdate(strategy.Id, updatedMetrics, metrics);
                }
            }
        }
        catch (Exception ex)
        {
            LogError("STRATEGY_PERFORMANCE_UPDATE_ERROR", "Error updating performance metrics", ex);
        }
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Validates strategy dependencies and resources
    /// </summary>
    private async Task<TradingResult<bool>> ValidateStrategyDependencies(string strategyId, StrategyConfig config)
    {
        await Task.CompletedTask; // Placeholder for async validation

        // Validate parameters
        if (config.Parameters.Any(p => p.Value == null))
        {
            return TradingResult<bool>.Failure("INVALID_PARAMETERS", "Strategy has null parameters");
        }

        // Validate universe
        if (!config.Universe.Any())
        {
            return TradingResult<bool>.Failure("EMPTY_UNIVERSE", "Strategy universe is empty");
        }

        // Validate risk limits
        if (config.RiskLimits.MaxPositionSize <= 0)
        {
            return TradingResult<bool>.Failure("INVALID_RISK_LIMITS", "Invalid risk limits configuration");
        }

        return TradingResult<bool>.Success(true);
    }

    /// <summary>
    /// Validates strategy configuration
    /// </summary>
    private TradingResult<bool> ValidateStrategyConfig(StrategyConfig config)
    {
        if (string.IsNullOrEmpty(config.StrategyId))
        {
            return TradingResult<bool>.Failure("EMPTY_STRATEGY_ID", "Strategy ID cannot be empty");
        }

        if (string.IsNullOrEmpty(config.Name))
        {
            return TradingResult<bool>.Failure("EMPTY_STRATEGY_NAME", "Strategy name cannot be empty");
        }

        if (config.Parameters == null)
        {
            return TradingResult<bool>.Failure("NULL_PARAMETERS", "Strategy parameters cannot be null");
        }

        return TradingResult<bool>.Success(true);
    }

    /// <summary>
    /// Detects changes between old and new configuration
    /// </summary>
    private string[] DetectConfigurationChanges(StrategyConfig oldConfig, StrategyConfig newConfig)
    {
        var changes = new List<string>();

        if (oldConfig.Name != newConfig.Name)
            changes.Add($"Name: {oldConfig.Name} -> {newConfig.Name}");

        if (oldConfig.IsEnabled != newConfig.IsEnabled)
            changes.Add($"Enabled: {oldConfig.IsEnabled} -> {newConfig.IsEnabled}");

        if (!oldConfig.Parameters.SequenceEqual(newConfig.Parameters))
            changes.Add("Parameters modified");

        if (!oldConfig.Universe.SequenceEqual(newConfig.Universe))
            changes.Add("Universe modified");

        if (!Equals(oldConfig.RiskLimits, newConfig.RiskLimits))
            changes.Add("Risk limits modified");

        return changes.ToArray();
    }

    /// <summary>
    /// Generates performance summary for a strategy
    /// </summary>
    private object GeneratePerformanceSummary(string strategyId)
    {
        if (!_performanceMetrics.TryGetValue(strategyId, out var metrics))
        {
            return new { Status = "No performance data available" };
        }

        return new
        {
            TotalPnL = metrics.TotalPnL,
            TradeCount = metrics.TradeCount,
            WinRate = metrics.WinRate,
            MaxDrawdown = metrics.MaxDrawdown,
            ConsecutiveLosses = metrics.ConsecutiveLosses,
            AverageTradeSize = metrics.AverageTradeSize,
            LastTradeTime = metrics.LastTradeTime
        };
    }

    /// <summary>
    /// Initializes default strategy configurations
    /// </summary>
    private void InitializeDefaultStrategies()
    {
        // Golden Rules Strategy
        var goldenRulesConfig = new StrategyConfig(
            "golden-rules-momentum",
            "Golden Rules Momentum Strategy",
            true,
            new Dictionary<string, object>
            {
                ["MovingAveragePeriod"] = 20,
                ["RSIThreshold"] = 70,
                ["VolumeMultiplier"] = 1.5,
                ["MaxPositions"] = 3
            },
            new RiskLimits(10000.0m, -500.0m, 0.02m, 3, 0.02m),
            new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA" });

        // Gap Trading Strategy
        var gapConfig = new StrategyConfig(
            "gap-reversal",
            "Gap Reversal Strategy",
            true,
            new Dictionary<string, object>
            {
                ["MinGapPercent"] = 2.0,
                ["MaxGapPercent"] = 8.0,
                ["ReversalConfirmation"] = true,
                ["HoldingPeriodMinutes"] = 30
            },
            new RiskLimits(5000.0m, -300.0m, 0.01m, 2, 0.03m),
            new[] { "SPY", "QQQ", "IWM" });

        // Momentum Strategy (disabled by default)
        var momentumConfig = new StrategyConfig(
            "momentum-breakout",
            "Momentum Breakout Strategy",
            false,
            new Dictionary<string, object>
            {
                ["BreakoutThreshold"] = 3.0,
                ["VolumeConfirmation"] = true,
                ["TimeframeMinutes"] = 5,
                ["StopLossPercent"] = 2.0
            },
            new RiskLimits(15000.0m, -750.0m, 0.03m, 5, 0.02m),
            new[] { "NVDA", "AMD", "INTC", "MU" });

        _strategyConfigs.TryAdd(goldenRulesConfig.StrategyId, goldenRulesConfig);
        _strategyConfigs.TryAdd(gapConfig.StrategyId, gapConfig);
        _strategyConfigs.TryAdd(momentumConfig.StrategyId, momentumConfig);

        LogInfo("STRATEGY_DEFAULT_CONFIGS_INITIALIZED", 
            $"Initialized {_strategyConfigs.Count} default strategy configurations",
            additionalData: new
            {
                Strategies = _strategyConfigs.Keys.ToArray(),
                EnabledCount = _strategyConfigs.Values.Count(c => c.IsEnabled)
            });
    }

    #endregion

    #region IDisposable

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            LogInfo("STRATEGY_MANAGER_DISPOSING", "Disposing strategy manager");
            
            _healthCheckTimer?.Dispose();
            _performanceUpdateTimer?.Dispose();
            
            // Stop all running strategies
            var runningStrategies = _activeStrategies.Values
                .Where(s => s.Status == StrategyStatus.Running)
                .ToArray();

            foreach (var strategy in runningStrategies)
            {
                try
                {
                    StopStrategyAsync(strategy.Id).Wait(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    LogError("STRATEGY_DISPOSE_ERROR", $"Error stopping strategy {strategy.Id}", ex);
                }
            }
            
            LogInfo("STRATEGY_MANAGER_DISPOSED", "Strategy manager disposed successfully");
        }
        
        base.Dispose(disposing);
    }

    #endregion
}

/// <summary>
/// Enhanced performance metrics tracking for strategies
/// </summary>
public record StrategyPerformanceMetrics
{
    public string StrategyId { get; init; } = string.Empty;
    public decimal TotalPnL { get; init; } = 0m;
    public decimal MaxPnL { get; init; } = 0m;
    public decimal MaxDrawdown { get; init; } = 0m;
    public decimal CurrentDrawdown { get; init; } = 0m;
    public int TradeCount { get; init; } = 0;
    public int WinCount { get; init; } = 0;
    public int ConsecutiveLosses { get; init; } = 0;
    public decimal WinRate => TradeCount > 0 ? (decimal)WinCount / TradeCount : 0m;
    public decimal AverageTradeSize { get; init; } = 0m;
    public DateTime LastTradeTime { get; init; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public StrategyPerformanceMetrics(string strategyId)
    {
        StrategyId = strategyId;
        CreatedAt = DateTime.UtcNow;
        LastTradeTime = DateTime.UtcNow;
    }

    public StrategyPerformanceMetrics UpdateMetrics(decimal pnlChange, int tradeChange)
    {
        var newPnL = TotalPnL + pnlChange;
        var newTradeCount = TradeCount + tradeChange;
        var newWinCount = pnlChange > 0 ? WinCount + 1 : WinCount;
        var newConsecutiveLosses = pnlChange < 0 ? ConsecutiveLosses + 1 : 0;
        var newMaxPnL = decimal.Max(MaxPnL, newPnL);
        var newDrawdown = newMaxPnL > 0 ? (newMaxPnL - newPnL) / newMaxPnL : 0m;
        var newMaxDrawdown = decimal.Max(MaxDrawdown, newDrawdown);
        var newAverageTradeSize = newTradeCount > 0 ? newPnL / newTradeCount : 0m;

        return this with
        {
            TotalPnL = newPnL,
            MaxPnL = newMaxPnL,
            MaxDrawdown = newMaxDrawdown,
            CurrentDrawdown = newDrawdown,
            TradeCount = newTradeCount,
            WinCount = newWinCount,
            ConsecutiveLosses = newConsecutiveLosses,
            AverageTradeSize = newAverageTradeSize,
            LastTradeTime = DateTime.UtcNow
        };
    }

    public StrategyPerformanceMetrics UpdateRuntime()
    {
        return this with { LastTradeTime = DateTime.UtcNow };
    }
}