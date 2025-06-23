using System.Collections.Concurrent;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.StrategyEngine.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.StrategyEngine.Services;

/// <summary>
/// Strategy lifecycle management service for trading strategies
/// Manages strategy configuration, state, and execution control
/// </summary>
public class StrategyManager : IStrategyManager
{
    private readonly ITradingLogger _logger;
    private readonly ConcurrentDictionary<string, StrategyInfo> _activeStrategies;
    private readonly ConcurrentDictionary<string, StrategyConfig> _strategyConfigs;

    public StrategyManager(ITradingLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeStrategies = new ConcurrentDictionary<string, StrategyInfo>();
        _strategyConfigs = new ConcurrentDictionary<string, StrategyConfig>();

        // Initialize default strategies
        InitializeDefaultStrategies();
    }

    public async Task<StrategyInfo[]> GetActiveStrategiesAsync()
    {
        await Task.CompletedTask;
        return _activeStrategies.Values.ToArray();
    }

    public async Task<StrategyResult> StartStrategyAsync(string strategyId)
    {
        try
        {
            var config = await GetStrategyConfigAsync(strategyId);
            if (config == null)
            {
                return new StrategyResult(false, $"Strategy configuration not found for {strategyId}");
            }

            if (!config.IsEnabled)
            {
                return new StrategyResult(false, $"Strategy {strategyId} is disabled");
            }

            // Check if already running
            if (_activeStrategies.TryGetValue(strategyId, out var existingStrategy) &&
                existingStrategy.Status == StrategyStatus.Running)
            {
                return new StrategyResult(false, $"Strategy {strategyId} is already running");
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

            // Simulate strategy startup process
            await Task.Delay(100); // Brief startup delay

            // Update status to running
            var runningStrategy = strategyInfo with { Status = StrategyStatus.Running };
            _activeStrategies.TryUpdate(strategyId, runningStrategy, strategyInfo);

            TradingLogOrchestrator.Instance.LogInfo($"Strategy {strategyId} ({config.Name}) started successfully");

            return new StrategyResult(true, $"Strategy {strategyId} started successfully");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error starting strategy {strategyId}", ex);
            return new StrategyResult(false, ex.Message, "START_ERROR");
        }
    }

    public async Task<StrategyResult> StopStrategyAsync(string strategyId)
    {
        try
        {
            if (!_activeStrategies.TryGetValue(strategyId, out var strategy))
            {
                return new StrategyResult(false, $"Strategy {strategyId} is not active");
            }

            if (strategy.Status == StrategyStatus.Stopped)
            {
                return new StrategyResult(false, $"Strategy {strategyId} is already stopped");
            }

            // Update status to stopping
            var stoppingStrategy = strategy with { Status = StrategyStatus.Stopping };
            _activeStrategies.TryUpdate(strategyId, stoppingStrategy, strategy);

            // Simulate strategy shutdown process
            await Task.Delay(50); // Brief shutdown delay

            // Update status to stopped
            var stoppedStrategy = stoppingStrategy with { Status = StrategyStatus.Stopped };
            _activeStrategies.TryUpdate(strategyId, stoppedStrategy, stoppingStrategy);

            TradingLogOrchestrator.Instance.LogInfo($"Strategy {strategyId} ({strategy.Name}) stopped successfully");

            return new StrategyResult(true, $"Strategy {strategyId} stopped successfully");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error stopping strategy {strategyId}", ex);
            return new StrategyResult(false, ex.Message, "STOP_ERROR");
        }
    }

    public async Task<StrategyConfig?> GetStrategyConfigAsync(string strategyId)
    {
        await Task.CompletedTask;
        return _strategyConfigs.TryGetValue(strategyId, out var config) ? config : null;
    }

    public async Task<StrategyResult> UpdateStrategyConfigAsync(StrategyConfig config)
    {
        try
        {
            // Validate configuration
            if (string.IsNullOrEmpty(config.StrategyId))
            {
                return new StrategyResult(false, "Strategy ID cannot be empty");
            }

            if (string.IsNullOrEmpty(config.Name))
            {
                return new StrategyResult(false, "Strategy name cannot be empty");
            }

            // Update configuration
            _strategyConfigs.AddOrUpdate(config.StrategyId, config, (k, v) => config);

            TradingLogOrchestrator.Instance.LogInfo($"Strategy configuration updated for {config.StrategyId}");

            await Task.CompletedTask;
            return new StrategyResult(true, $"Configuration updated for strategy {config.StrategyId}");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error updating strategy configuration for {config.StrategyId}", ex);
            return new StrategyResult(false, ex.Message, "CONFIG_UPDATE_ERROR");
        }
    }

    // Additional management methods

    /// <summary>
    /// Get strategy by ID
    /// </summary>
    public async Task<StrategyInfo?> GetStrategyAsync(string strategyId)
    {
        await Task.CompletedTask;
        return _activeStrategies.TryGetValue(strategyId, out var strategy) ? strategy : null;
    }

    /// <summary>
    /// Update strategy performance metrics
    /// </summary>
    public async Task UpdateStrategyMetricsAsync(string strategyId, decimal pnlChange, int tradeCount)
    {
        try
        {
            if (_activeStrategies.TryGetValue(strategyId, out var strategy))
            {
                var updatedStrategy = strategy with
                {
                    PnL = strategy.PnL + pnlChange,
                    TradeCount = strategy.TradeCount + tradeCount
                };

                _activeStrategies.TryUpdate(strategyId, updatedStrategy, strategy);

                TradingLogOrchestrator.Instance.LogInfo($"Updated metrics for strategy {strategyId}: PnL={updatedStrategy.PnL}, Trades={updatedStrategy.TradeCount}");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error updating strategy metrics for {strategyId}", ex);
        }
    }

    /// <summary>
    /// Get strategies by status
    /// </summary>
    public async Task<StrategyInfo[]> GetStrategiesByStatusAsync(StrategyStatus status)
    {
        await Task.CompletedTask;
        return _activeStrategies.Values.Where(s => s.Status == status).ToArray();
    }

    /// <summary>
    /// Pause a running strategy
    /// </summary>
    public async Task<StrategyResult> PauseStrategyAsync(string strategyId)
    {
        try
        {
            if (!_activeStrategies.TryGetValue(strategyId, out var strategy))
            {
                return new StrategyResult(false, $"Strategy {strategyId} not found");
            }

            if (strategy.Status != StrategyStatus.Running)
            {
                return new StrategyResult(false, $"Strategy {strategyId} is not running");
            }

            var pausedStrategy = strategy with { Status = StrategyStatus.Paused };
            _activeStrategies.TryUpdate(strategyId, pausedStrategy, strategy);

            TradingLogOrchestrator.Instance.LogInfo($"Strategy {strategyId} paused");

            await Task.CompletedTask;
            return new StrategyResult(true, $"Strategy {strategyId} paused successfully");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error pausing strategy {strategyId}", ex);
            return new StrategyResult(false, ex.Message, "PAUSE_ERROR");
        }
    }

    /// <summary>
    /// Resume a paused strategy
    /// </summary>
    public async Task<StrategyResult> ResumeStrategyAsync(string strategyId)
    {
        try
        {
            if (!_activeStrategies.TryGetValue(strategyId, out var strategy))
            {
                return new StrategyResult(false, $"Strategy {strategyId} not found");
            }

            if (strategy.Status != StrategyStatus.Paused)
            {
                return new StrategyResult(false, $"Strategy {strategyId} is not paused");
            }

            var runningStrategy = strategy with { Status = StrategyStatus.Running };
            _activeStrategies.TryUpdate(strategyId, runningStrategy, strategy);

            TradingLogOrchestrator.Instance.LogInfo($"Strategy {strategyId} resumed");

            await Task.CompletedTask;
            return new StrategyResult(true, $"Strategy {strategyId} resumed successfully");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error resuming strategy {strategyId}", ex);
            return new StrategyResult(false, ex.Message, "RESUME_ERROR");
        }
    }

    // Private helper methods
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

        // Momentum Strategy
        var momentumConfig = new StrategyConfig(
            "momentum-breakout",
            "Momentum Breakout Strategy",
            false, // Disabled by default
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

        TradingLogOrchestrator.Instance.LogInfo($"Initialized {_strategyConfigs.Count} default strategy configurations");
    }
}