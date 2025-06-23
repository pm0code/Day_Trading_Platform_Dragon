using TradingPlatform.PaperTrading.Models;
using System.Collections.Concurrent;

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
namespace TradingPlatform.PaperTrading.Services;

public class ExecutionAnalytics : IExecutionAnalytics
{
    private readonly ITradingLogger _logger;
    private readonly ConcurrentQueue<Execution> _executions = new();
    private readonly ConcurrentDictionary<string, List<Execution>> _executionsBySymbol = new();

    public ExecutionAnalytics(ITradingLogger logger)
    {
        _logger = logger;
    }

    public async Task<PerformanceMetrics> GetPerformanceMetricsAsync()
    {
        try
        {
            var executions = _executions.ToArray();
            if (!executions.Any())
            {
                return CreateEmptyPerformanceMetrics();
            }

            var trades = GroupExecutionsIntoTrades(executions);
            var tradePnLs = trades.Select(CalculateTradePnL).ToArray();
            
            var totalReturn = tradePnLs.Sum();
            var winningTrades = tradePnLs.Where(pnl => pnl > 0).ToArray();
            var losingTrades = tradePnLs.Where(pnl => pnl < 0).ToArray();
            
            var winRate = trades.Any() ? (decimal)winningTrades.Length / trades.Count : 0m;
            var averageWin = winningTrades.Any() ? winningTrades.Average() : 0m;
            var averageLoss = losingTrades.Any() ? Math.Abs(losingTrades.Average()) : 0m;
            var profitFactor = averageLoss > 0 ? averageWin / averageLoss : 0m;
            
            var returns = CalculateDailyReturns(tradePnLs);
            var sharpeRatio = CalculateSharpeRatio(returns);
            var maxDrawdown = CalculateMaxDrawdown(tradePnLs);
            var dailyReturn = returns.Any() ? returns.Last() : 0m;

            var periodStart = executions.Min(e => e.ExecutionTime);
            var periodEnd = executions.Max(e => e.ExecutionTime);

            return await Task.FromResult(new PerformanceMetrics(
                TotalReturn: totalReturn,
                DailyReturn: dailyReturn,
                SharpeRatio: sharpeRatio,
                MaxDrawdown: maxDrawdown,
                WinRate: winRate,
                AverageWin: averageWin,
                AverageLoss: averageLoss,
                ProfitFactor: profitFactor,
                TotalTrades: trades.Count,
                WinningTrades: winningTrades.Length,
                LosingTrades: losingTrades.Length,
                PeriodStart: periodStart,
                PeriodEnd: periodEnd
            ));
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error calculating performance metrics", ex);
            return CreateEmptyPerformanceMetrics();
        }
    }

    public async Task<Models.ExecutionAnalytics> GetExecutionAnalyticsAsync()
    {
        try
        {
            var executions = _executions.ToArray();
            if (!executions.Any())
            {
                return CreateEmptyExecutionAnalytics();
            }

            var averageSlippage = executions.Average(e => e.Slippage);
            var totalCommissions = executions.Sum(e => e.Commission);
            var averageExecutionTime = CalculateAverageExecutionTime(executions);
            var fillRate = CalculateFillRate(executions);
            
            var slippageBySymbol = CalculateSlippageBySymbol(executions);
            var venueMetrics = CalculateVenueMetrics(executions);
            
            var periodStart = executions.Min(e => e.ExecutionTime);
            var periodEnd = executions.Max(e => e.ExecutionTime);

            return await Task.FromResult(new Models.ExecutionAnalytics(
                averageSlippage,
                totalCommissions,
                averageExecutionTime,
                fillRate,
                slippageBySymbol,
                venueMetrics,
                periodStart,
                periodEnd
            ));
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error calculating execution analytics", ex);
            return CreateEmptyExecutionAnalytics();
        }
    }

    public async Task<IEnumerable<Execution>> GetExecutionHistoryAsync()
    {
        return await Task.FromResult(_executions.ToArray().OrderByDescending(e => e.ExecutionTime));
    }

    public async Task<IEnumerable<Execution>> GetExecutionsBySymbolAsync(string symbol)
    {
        if (_executionsBySymbol.TryGetValue(symbol, out var executions))
        {
            return await Task.FromResult(executions.OrderByDescending(e => e.ExecutionTime));
        }
        
        return await Task.FromResult(Array.Empty<Execution>());
    }

    public async Task RecordExecutionAsync(Execution execution)
    {
        try
        {
            _executions.Enqueue(execution);
            
            _executionsBySymbol.AddOrUpdate(execution.Symbol,
                new List<Execution> { execution },
                (key, existingList) =>
                {
                    existingList.Add(execution);
                    return existingList;
                });

            TradingLogOrchestrator.Instance.LogInfo($"Recorded execution: {execution.ExecutionId} for {execution.Symbol} {execution.Quantity}@{execution.Price}");
                
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error recording execution {execution.ExecutionId}", ex);
        }
    }

    private List<List<Execution>> GroupExecutionsIntoTrades(Execution[] executions)
    {
        var trades = new List<List<Execution>>();
        var executionsBySymbol = executions.GroupBy(e => e.Symbol);

        foreach (var symbolGroup in executionsBySymbol)
        {
            var symbolExecutions = symbolGroup.OrderBy(e => e.ExecutionTime).ToList();
            var currentTrade = new List<Execution>();
            var netPosition = 0m;

            foreach (var execution in symbolExecutions)
            {
                currentTrade.Add(execution);
                
                var quantity = execution.Side == OrderSide.Buy ? execution.Quantity : -execution.Quantity;
                netPosition += quantity;

                // Trade complete when position returns to flat
                if (netPosition == 0 && currentTrade.Count > 1)
                {
                    trades.Add(new List<Execution>(currentTrade));
                    currentTrade.Clear();
                }
            }

            // Add incomplete trade if it exists
            if (currentTrade.Any())
            {
                trades.Add(currentTrade);
            }
        }

        return trades;
    }

    private decimal CalculateTradePnL(List<Execution> trade)
    {
        var totalBought = 0m;
        var totalSold = 0m;
        var totalCommissions = trade.Sum(e => e.Commission);

        foreach (var execution in trade)
        {
            if (execution.Side == OrderSide.Buy)
            {
                totalBought += execution.Quantity * execution.Price;
            }
            else
            {
                totalSold += execution.Quantity * execution.Price;
            }
        }

        return totalSold - totalBought - totalCommissions;
    }

    private decimal[] CalculateDailyReturns(decimal[] tradePnLs)
    {
        if (!tradePnLs.Any()) return Array.Empty<decimal>();

        var dailyReturns = new List<decimal>();
        var cumulativePnL = 0m;
        var initialCapital = 100000m; // Assume $100k starting capital

        foreach (var pnl in tradePnLs)
        {
            cumulativePnL += pnl;
            var totalCapital = initialCapital + cumulativePnL;
            var dailyReturn = totalCapital > 0 ? pnl / totalCapital : 0m;
            dailyReturns.Add(dailyReturn);
        }

        return dailyReturns.ToArray();
    }

    private decimal CalculateSharpeRatio(decimal[] returns)
    {
        if (returns.Length < 2) return 0m;

        var avgReturn = returns.Average();
        var variance = returns.Select(r => (r - avgReturn) * (r - avgReturn)).Average();
        var stdDev = TradingPlatform.Common.Mathematics.TradingMath.Sqrt(variance);

        if (stdDev == 0) return 0m;

        var riskFreeRate = 0.02m / 252m; // 2% annual risk-free rate, daily
        var excessReturn = avgReturn - riskFreeRate;

        return (excessReturn / stdDev) * TradingPlatform.Common.Mathematics.TradingMath.Sqrt(252m); // Annualized
    }

    private decimal CalculateMaxDrawdown(decimal[] tradePnLs)
    {
        if (!tradePnLs.Any()) return 0m;

        var peak = 0m;
        var maxDrawdown = 0m;
        var cumulativePnL = 0m;

        foreach (var pnl in tradePnLs)
        {
            cumulativePnL += pnl;
            
            if (cumulativePnL > peak)
            {
                peak = cumulativePnL;
            }
            else
            {
                var drawdown = (peak - cumulativePnL) / Math.Max(peak, 1m);
                maxDrawdown = Math.Max(maxDrawdown, drawdown);
            }
        }

        return maxDrawdown;
    }

    private TimeSpan CalculateAverageExecutionTime(Execution[] executions)
    {
        // Simulate execution time based on order characteristics
        var averageLatencyMs = executions.Average(e => 
        {
            // Simulate different latencies for different venues
            return e.VenueId switch
            {
                "NASDAQ" => 0.05,
                "NYSE" => 0.08,
                "ARCA" => 0.06,
                "IEX" => 0.35, // IEX has intentional delay
                _ => 0.10
            };
        });

        return TimeSpan.FromMilliseconds(averageLatencyMs);
    }

    private decimal CalculateFillRate(Execution[] executions)
    {
        // For paper trading, assume 100% fill rate
        // In real trading, this would track partial fills vs. full fills
        return executions.Any() ? 1.0m : 0m;
    }

    private IEnumerable<SlippageMetric> CalculateSlippageBySymbol(Execution[] executions)
    {
        return executions
            .GroupBy(e => e.Symbol)
            .Select(g => new SlippageMetric(
                Symbol: g.Key,
                AverageSlippage: g.Average(e => e.Slippage),
                MaxSlippage: g.Max(e => e.Slippage),
                MinSlippage: g.Min(e => e.Slippage),
                TradeCount: g.Count()
            ));
    }

    private IEnumerable<VenueMetric> CalculateVenueMetrics(Execution[] executions)
    {
        return executions
            .GroupBy(e => e.VenueId)
            .Select(g => new VenueMetric(
                VenueId: g.Key,
                AverageLatency: TimeSpan.FromMilliseconds(g.Key switch
                {
                    "NASDAQ" => 0.05,
                    "NYSE" => 0.08,
                    "ARCA" => 0.06,
                    "IEX" => 0.35,
                    _ => 0.10
                }),
                FillRate: 1.0m, // 100% for paper trading
                OrderCount: g.Count()
            ));
    }

    private PerformanceMetrics CreateEmptyPerformanceMetrics()
    {
        return new PerformanceMetrics(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0, 0, 0, DateTime.UtcNow, DateTime.UtcNow);
    }

    private Models.ExecutionAnalytics CreateEmptyExecutionAnalytics()
    {
        return new Models.ExecutionAnalytics(0m, 0m, TimeSpan.Zero, 0m, 
            Array.Empty<SlippageMetric>(), Array.Empty<VenueMetric>(), DateTime.UtcNow, DateTime.UtcNow);
    }
}