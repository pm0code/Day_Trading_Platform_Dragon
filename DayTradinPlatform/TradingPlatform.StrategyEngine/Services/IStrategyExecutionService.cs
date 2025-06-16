using TradingPlatform.StrategyEngine.Models;

namespace TradingPlatform.StrategyEngine.Services;

/// <summary>
/// Core strategy execution service interface for high-performance trading
/// Manages strategy lifecycle and real-time signal processing
/// </summary>
public interface IStrategyExecutionService
{
    /// <summary>
    /// Start background processing for Redis Streams and strategy execution
    /// </summary>
    Task StartBackgroundProcessingAsync();

    /// <summary>
    /// Get execution performance metrics
    /// </summary>
    Task<ExecutionMetrics> GetExecutionMetricsAsync();

    /// <summary>
    /// Get strategy engine health status
    /// </summary>
    Task<StrategyHealthStatus> GetHealthStatusAsync();

    /// <summary>
    /// Execute strategy signal processing
    /// </summary>
    Task<StrategyResult> ExecuteStrategyAsync(string strategyId, MarketConditions conditions);

    /// <summary>
    /// Get execution latency statistics
    /// </summary>
    Task<LatencyStats> GetLatencyStatsAsync();
}

/// <summary>
/// Strategy management interface
/// </summary>
public interface IStrategyManager
{
    /// <summary>
    /// Get all active strategies
    /// </summary>
    Task<StrategyInfo[]> GetActiveStrategiesAsync();

    /// <summary>
    /// Start a trading strategy
    /// </summary>
    Task<StrategyResult> StartStrategyAsync(string strategyId);

    /// <summary>
    /// Stop a trading strategy
    /// </summary>
    Task<StrategyResult> StopStrategyAsync(string strategyId);

    /// <summary>
    /// Get strategy configuration
    /// </summary>
    Task<StrategyConfig?> GetStrategyConfigAsync(string strategyId);

    /// <summary>
    /// Update strategy configuration
    /// </summary>
    Task<StrategyResult> UpdateStrategyConfigAsync(StrategyConfig config);
}

/// <summary>
/// Signal processing interface for real-time trading decisions
/// </summary>
public interface ISignalProcessor
{
    /// <summary>
    /// Process market data and generate trading signals
    /// </summary>
    Task<TradingSignal[]> ProcessMarketDataAsync(string symbol, MarketConditions conditions);

    /// <summary>
    /// Process manual signal request
    /// </summary>
    Task<StrategyResult> ProcessManualSignalAsync(SignalRequest request);

    /// <summary>
    /// Get recent signals for a strategy
    /// </summary>
    Task<TradingSignal[]> GetRecentSignalsAsync(string strategyId);

    /// <summary>
    /// Validate signal against risk parameters
    /// </summary>
    Task<RiskAssessment> ValidateSignalAsync(TradingSignal signal);
}

/// <summary>
/// Performance tracking interface for strategy analytics
/// </summary>
public interface IPerformanceTracker
{
    /// <summary>
    /// Get strategy performance metrics
    /// </summary>
    Task<StrategyPerformance?> GetStrategyPerformanceAsync(string strategyId);

    /// <summary>
    /// Update performance metrics after trade execution
    /// </summary>
    Task UpdatePerformanceAsync(string strategyId, decimal pnl, bool isWinning);

    /// <summary>
    /// Get portfolio-wide performance summary
    /// </summary>
    Task<PortfolioPerformance> GetPortfolioPerformanceAsync();

    /// <summary>
    /// Reset performance tracking for a strategy
    /// </summary>
    Task ResetStrategyPerformanceAsync(string strategyId);
}

// Supporting data models
public record LatencyStats(
    TimeSpan Average,
    TimeSpan P50,
    TimeSpan P95,
    TimeSpan P99,
    TimeSpan Max,
    int SampleCount,
    DateTime LastUpdated);

public record PortfolioPerformance(
    decimal TotalPnL,
    decimal DailyPnL,
    decimal MaxDrawdown,
    int ActiveStrategies,
    int TotalTrades,
    decimal WinRate,
    DateTimeOffset LastUpdate);