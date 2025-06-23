using System.Collections.Concurrent;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.StrategyEngine.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.StrategyEngine.Services;

/// <summary>
/// Performance tracking service for strategy analytics and reporting
/// Provides real-time performance metrics and portfolio analytics
/// </summary>
public class PerformanceTracker : IPerformanceTracker
{
    private readonly ITradingLogger _logger;
    private readonly ConcurrentDictionary<string, StrategyPerformance> _strategyPerformances;
    private readonly ConcurrentDictionary<string, List<TradeRecord>> _tradeHistory;
    private readonly Timer _metricsUpdateTimer;

    public PerformanceTracker(ITradingLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _strategyPerformances = new ConcurrentDictionary<string, StrategyPerformance>();
        _tradeHistory = new ConcurrentDictionary<string, List<TradeRecord>>();
        
        // Update performance metrics every 30 seconds
        _metricsUpdateTimer = new Timer(UpdatePerformanceMetrics, null, 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task<StrategyPerformance?> GetStrategyPerformanceAsync(string strategyId)
    {
        await Task.CompletedTask;
        return _strategyPerformances.TryGetValue(strategyId, out var performance) ? performance : null;
    }

    public async Task UpdatePerformanceAsync(string strategyId, decimal pnl, bool isWinning)
    {
        try
        {
            var tradeRecord = new TradeRecord(
                Guid.NewGuid().ToString(),
                strategyId,
                pnl,
                isWinning,
                DateTimeOffset.UtcNow);

            // Add to trade history
            _tradeHistory.AddOrUpdate(strategyId,
                new List<TradeRecord> { tradeRecord },
                (key, existing) =>
                {
                    existing.Add(tradeRecord);
                    // Keep only last 1000 trades per strategy
                    if (existing.Count > 1000)
                    {
                        existing.RemoveAt(0);
                    }
                    return existing;
                });

            // Update or create performance metrics
            _strategyPerformances.AddOrUpdate(strategyId,
                CreateInitialPerformance(strategyId, pnl, isWinning),
                (key, existing) => UpdateExistingPerformance(existing, pnl, isWinning));

            TradingLogOrchestrator.Instance.LogInfo("Updated performance for strategy {StrategyId}: PnL={PnL}, IsWinning={IsWinning}", 
                strategyId, pnl, isWinning);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error updating performance for strategy {StrategyId}", strategyId, ex);
        }
    }

    public async Task<PortfolioPerformance> GetPortfolioPerformanceAsync()
    {
        try
        {
            var allPerformances = _strategyPerformances.Values.ToArray();
            
            if (allPerformances.Length == 0)
            {
                return new PortfolioPerformance(0.0m, 0.0m, 0.0m, 0, 0, 0.0m, DateTimeOffset.UtcNow);
            }

            var totalPnL = allPerformances.Sum(p => p.TotalPnL);
            var totalUnrealizedPnL = allPerformances.Sum(p => p.UnrealizedPnL);
            var maxDrawdown = allPerformances.Min(p => p.MaxDrawdown);
            var activeStrategies = allPerformances.Count(p => p.ActiveDuration > TimeSpan.Zero);
            var totalTrades = allPerformances.Sum(p => p.TotalTrades);
            
            var totalWinningTrades = allPerformances.Sum(p => p.WinningTrades);
            var winRate = totalTrades > 0 ? (decimal)totalWinningTrades / totalTrades : 0.0m;

            // Calculate daily PnL (trades from last 24 hours)
            var dailyPnL = CalculateDailyPnL();

            await Task.CompletedTask;
            return new PortfolioPerformance(
                totalPnL,
                dailyPnL,
                maxDrawdown,
                activeStrategies,
                totalTrades,
                winRate,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error calculating portfolio performance", ex);
            return new PortfolioPerformance(0.0m, 0.0m, 0.0m, 0, 0, 0.0m, DateTimeOffset.UtcNow);
        }
    }

    public async Task ResetStrategyPerformanceAsync(string strategyId)
    {
        try
        {
            _strategyPerformances.TryRemove(strategyId, out _);
            _tradeHistory.TryRemove(strategyId, out _);

            TradingLogOrchestrator.Instance.LogInfo("Reset performance tracking for strategy {StrategyId}", strategyId);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error resetting performance for strategy {StrategyId}", strategyId, ex);
        }
    }

    // Additional performance tracking methods

    /// <summary>
    /// Get detailed trade history for a strategy
    /// </summary>
    public async Task<TradeRecord[]> GetTradeHistoryAsync(string strategyId, int? limit = null)
    {
        await Task.CompletedTask;
        
        if (!_tradeHistory.TryGetValue(strategyId, out var trades))
        {
            return Array.Empty<TradeRecord>();
        }

        var orderedTrades = trades.OrderByDescending(t => t.Timestamp).ToArray();
        
        if (limit.HasValue && limit.Value > 0)
        {
            return orderedTrades.Take(limit.Value).ToArray();
        }

        return orderedTrades;
    }

    /// <summary>
    /// Get performance comparison between strategies
    /// </summary>
    public async Task<PerformanceComparison[]> GetStrategyComparisonAsync()
    {
        await Task.CompletedTask;
        
        return _strategyPerformances.Values
            .Select(p => new PerformanceComparison(
                p.StrategyId,
                p.TotalPnL,
                p.WinRate,
                p.SharpeRatio,
                p.MaxDrawdown,
                p.TotalTrades))
            .OrderByDescending(c => c.TotalPnL)
            .ToArray();
    }

    /// <summary>
    /// Calculate risk-adjusted returns
    /// </summary>
    public async Task<RiskAdjustedMetrics> GetRiskAdjustedMetricsAsync(string strategyId)
    {
        try
        {
            if (!_tradeHistory.TryGetValue(strategyId, out var trades) || trades.Count == 0)
            {
                return new RiskAdjustedMetrics(strategyId, 0.0m, 0.0m, 0.0m, 0.0m, DateTimeOffset.UtcNow);
            }

            var returns = trades.Select(t => t.PnL).ToArray();
            
            // Calculate volatility (standard deviation of returns)
            var averageReturn = returns.Average();
            var variance = returns.Select(r => Math.Pow((double)(r - averageReturn), 2)).Average();
            var volatility = (decimal)Math.Sqrt(variance);
            
            // Calculate Sharpe ratio (assuming risk-free rate of 0 for simplicity)
            var sharpeRatio = volatility > 0 ? averageReturn / volatility : 0.0m;
            
            // Calculate maximum drawdown
            var cumulativeReturns = new List<decimal> { 0 };
            var runningTotal = 0.0m;
            
            foreach (var trade in trades.OrderBy(t => t.Timestamp))
            {
                runningTotal += trade.PnL;
                cumulativeReturns.Add(runningTotal);
            }
            
            var maxDrawdown = 0.0m;
            var peak = 0.0m;
            
            foreach (var cumReturn in cumulativeReturns)
            {
                if (cumReturn > peak)
                {
                    peak = cumReturn;
                }
                else
                {
                    var drawdown = peak - cumReturn;
                    if (drawdown > maxDrawdown)
                    {
                        maxDrawdown = drawdown;
                    }
                }
            }

            await Task.CompletedTask;
            return new RiskAdjustedMetrics(
                strategyId,
                averageReturn,
                volatility,
                sharpeRatio,
                maxDrawdown,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error calculating risk-adjusted metrics for {StrategyId}", strategyId, ex);
            return new RiskAdjustedMetrics(strategyId, 0.0m, 0.0m, 0.0m, 0.0m, DateTimeOffset.UtcNow);
        }
    }

    // Private helper methods
    private StrategyPerformance CreateInitialPerformance(string strategyId, decimal pnl, bool isWinning)
    {
        return new StrategyPerformance(
            strategyId,
            pnl,
            0.0m, // No unrealized PnL initially
            1,    // First trade
            isWinning ? 1 : 0,
            isWinning ? 0 : 1,
            isWinning ? 1.0m : 0.0m,
            0.0m, // Sharpe ratio - needs more data
            pnl < 0 ? Math.Abs(pnl) : 0.0m, // Max drawdown
            TimeSpan.FromMinutes(1), // Assume 1 minute active duration
            DateTimeOffset.UtcNow);
    }

    private StrategyPerformance UpdateExistingPerformance(StrategyPerformance existing, decimal pnl, bool isWinning)
    {
        var newTotalPnL = existing.TotalPnL + pnl;
        var newTotalTrades = existing.TotalTrades + 1;
        var newWinningTrades = existing.WinningTrades + (isWinning ? 1 : 0);
        var newLosingTrades = existing.LosingTrades + (isWinning ? 0 : 1);
        var newWinRate = (decimal)newWinningTrades / newTotalTrades;
        
        // Update max drawdown if this trade creates a new low
        var newMaxDrawdown = existing.MaxDrawdown;
        if (newTotalPnL < existing.TotalPnL)
        {
            var drawdown = Math.Abs(newTotalPnL - existing.TotalPnL);
            if (drawdown > existing.MaxDrawdown)
            {
                newMaxDrawdown = drawdown;
            }
        }

        return existing with
        {
            TotalPnL = newTotalPnL,
            TotalTrades = newTotalTrades,
            WinningTrades = newWinningTrades,
            LosingTrades = newLosingTrades,
            WinRate = newWinRate,
            MaxDrawdown = newMaxDrawdown,
            LastUpdate = DateTimeOffset.UtcNow
        };
    }

    private decimal CalculateDailyPnL()
    {
        var cutoffTime = DateTimeOffset.UtcNow.AddHours(-24);
        var dailyTrades = _tradeHistory.Values
            .SelectMany(trades => trades)
            .Where(trade => trade.Timestamp >= cutoffTime);

        return dailyTrades.Sum(trade => trade.PnL);
    }

    private void UpdatePerformanceMetrics(object? state)
    {
        try
        {
            // Recalculate Sharpe ratios and other advanced metrics
            foreach (var kvp in _strategyPerformances.ToArray())
            {
                var strategyId = kvp.Key;
                var performance = kvp.Value;

                if (_tradeHistory.TryGetValue(strategyId, out var trades) && trades.Count >= 10)
                {
                    // Calculate rolling Sharpe ratio with at least 10 trades
                    var recentTrades = trades.TakeLast(30).ToArray(); // Last 30 trades
                    var returns = recentTrades.Select(t => (double)t.PnL).ToArray();
                    
                    if (returns.Length > 1)
                    {
                        var meanReturn = returns.Average();
                        var stdDev = Math.Sqrt(returns.Select(r => Math.Pow(r - meanReturn, 2)).Average());
                        var sharpeRatio = stdDev > 0 ? (decimal)(meanReturn / stdDev) : 0.0m;

                        var updatedPerformance = performance with { SharpeRatio = sharpeRatio };
                        _strategyPerformances.TryUpdate(strategyId, updatedPerformance, performance);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error updating performance metrics", ex);
        }
    }
}

// Supporting data models
public record TradeRecord(
    string Id,
    string StrategyId,
    decimal PnL,
    bool IsWinning,
    DateTimeOffset Timestamp);

public record PerformanceComparison(
    string StrategyId,
    decimal TotalPnL,
    decimal WinRate,
    decimal SharpeRatio,
    decimal MaxDrawdown,
    int TotalTrades);

public record RiskAdjustedMetrics(
    string StrategyId,
    decimal AverageReturn,
    decimal Volatility,
    decimal SharpeRatio,
    decimal MaxDrawdown,
    DateTimeOffset Timestamp);