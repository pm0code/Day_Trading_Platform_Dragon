using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.PaperTrading.Models;

namespace TradingPlatform.PaperTrading.Services
{
    /// <summary>
    /// Canonical implementation of execution analytics for paper trading.
    /// Provides comprehensive performance metrics and trade analysis.
    /// </summary>
    public class ExecutionAnalyticsCanonical : CanonicalServiceBase, IExecutionAnalytics
    {
        #region Configuration

        protected virtual int MaxExecutionHistory => 10000; // Maximum executions to keep in history
        protected virtual int MaxSymbolHistory => 1000; // Maximum executions per symbol
        protected virtual decimal InitialCapital => 100_000m; // Starting capital for return calculations
        protected virtual decimal RiskFreeRate => 0.02m; // Annual risk-free rate for Sharpe calculation
        protected virtual int PerformanceUpdateIntervalMs => 5000; // Update performance metrics every 5 seconds

        #endregion

        #region Infrastructure

        private readonly ConcurrentQueue<Execution> _executions = new();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<Execution>> _executionsBySymbol = new();
        private readonly Timer _performanceUpdateTimer;
        private readonly object _metricsLock = new();
        
        private PerformanceMetrics _cachedPerformanceMetrics;
        private Models.ExecutionAnalytics _cachedExecutionAnalytics;
        private DateTime _lastMetricsUpdate = DateTime.MinValue;
        private long _totalExecutionsRecorded = 0;

        #endregion

        #region Constructor

        public ExecutionAnalyticsCanonical(ITradingLogger logger)
            : base(logger, "ExecutionAnalytics")
        {
            LogMethodEntry();
            
            _cachedPerformanceMetrics = CreateEmptyPerformanceMetrics();
            _cachedExecutionAnalytics = CreateEmptyExecutionAnalytics();
            
            _performanceUpdateTimer = new Timer(
                UpdateCachedMetrics,
                null,
                Timeout.Infinite,
                Timeout.Infinite);
        }

        #endregion

        #region IExecutionAnalytics Implementation

        public async Task<PerformanceMetrics> GetPerformanceMetricsAsync()
        {
            return await ExecuteServiceOperationAsync(async () =>
            {
                // Return cached metrics if recent
                if (_lastMetricsUpdate.AddMilliseconds(PerformanceUpdateIntervalMs) > DateTime.UtcNow)
                {
                    LogDebug("Returning cached performance metrics");
                    return _cachedPerformanceMetrics;
                }

                // Calculate fresh metrics
                var metrics = await CalculatePerformanceMetricsAsync();
                
                lock (_metricsLock)
                {
                    _cachedPerformanceMetrics = metrics;
                    _lastMetricsUpdate = DateTime.UtcNow;
                }

                UpdatePerformanceMetricTracking(metrics);
                
                return metrics;

            }, "Get performance metrics",
               incrementOperationCounter: true);
        }

        public async Task<Models.ExecutionAnalytics> GetExecutionAnalyticsAsync()
        {
            return await ExecuteServiceOperationAsync(async () =>
            {
                // Return cached analytics if recent
                if (_lastMetricsUpdate.AddMilliseconds(PerformanceUpdateIntervalMs) > DateTime.UtcNow)
                {
                    LogDebug("Returning cached execution analytics");
                    return _cachedExecutionAnalytics;
                }

                // Calculate fresh analytics
                var analytics = await CalculateExecutionAnalyticsAsync();
                
                lock (_metricsLock)
                {
                    _cachedExecutionAnalytics = analytics;
                }

                UpdateExecutionAnalyticsTracking(analytics);
                
                return analytics;

            }, "Get execution analytics",
               incrementOperationCounter: true);
        }

        public async Task<IEnumerable<Execution>> GetExecutionHistoryAsync()
        {
            return await ExecuteServiceOperationAsync(async () =>
            {
                var executions = _executions.ToArray()
                    .OrderByDescending(e => e.ExecutionTime)
                    .ToList();

                LogDebug($"Retrieved {executions.Count} executions from history");
                
                return await Task.FromResult(executions.AsEnumerable());

            }, "Get execution history",
               incrementOperationCounter: false); // Don't increment for read operations
        }

        public async Task<IEnumerable<Execution>> GetExecutionsBySymbolAsync(string symbol)
        {
            return await ExecuteServiceOperationAsync(async () =>
            {
                ValidateParameter(symbol, nameof(symbol), s => !string.IsNullOrWhiteSpace(s), "Symbol is required");

                if (_executionsBySymbol.TryGetValue(symbol, out var symbolExecutions))
                {
                    var executions = symbolExecutions.ToArray()
                        .OrderByDescending(e => e.ExecutionTime)
                        .ToList();

                    LogDebug($"Retrieved {executions.Count} executions for {symbol}");
                    
                    return await Task.FromResult(executions.AsEnumerable());
                }

                LogDebug($"No executions found for {symbol}");
                return await Task.FromResult(Array.Empty<Execution>());

            }, $"Get executions for {symbol}",
               incrementOperationCounter: false);
        }

        public async Task RecordExecutionAsync(Execution execution)
        {
            await ExecuteServiceOperationAsync(async () =>
            {
                ValidateNotNull(execution, nameof(execution));

                // Add to main queue
                _executions.Enqueue(execution);
                
                // Trim if exceeding max history
                while (_executions.Count > MaxExecutionHistory && _executions.TryDequeue(out _))
                {
                    // Remove oldest executions
                }

                // Add to symbol-specific queue
                var symbolQueue = _executionsBySymbol.GetOrAdd(execution.Symbol, 
                    _ => new ConcurrentQueue<Execution>());
                
                symbolQueue.Enqueue(execution);
                
                // Trim symbol queue if needed
                while (symbolQueue.Count > MaxSymbolHistory && symbolQueue.TryDequeue(out _))
                {
                    // Remove oldest executions for this symbol
                }

                Interlocked.Increment(ref _totalExecutionsRecorded);
                
                // Update metrics
                UpdateMetric("Executions.Total", _totalExecutionsRecorded);
                UpdateMetric("Executions.Symbols", _executionsBySymbol.Count);
                UpdateMetric($"Executions.{execution.Symbol}.Count", symbolQueue.Count);
                UpdateMetric($"Executions.{execution.Symbol}.LastPrice", execution.Price);
                UpdateMetric($"Executions.{execution.Symbol}.LastQuantity", execution.Quantity);

                LogInfo($"Recorded execution: {execution.ExecutionId} for {execution.Symbol} " +
                       $"{execution.Quantity}@{execution.Price:C} (slippage: {execution.Slippage:P4})");

                return TradingResult.Success();

            }, $"Record execution {execution.ExecutionId}",
               incrementOperationCounter: true);
        }

        #endregion

        #region Performance Calculations

        private async Task<PerformanceMetrics> CalculatePerformanceMetricsAsync()
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

            LogDebug($"Performance metrics calculated: Total trades={trades.Count}, " +
                    $"Win rate={winRate:P2}, Sharpe={sharpeRatio:F2}");

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

        private async Task<Models.ExecutionAnalytics> CalculateExecutionAnalyticsAsync()
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

            LogDebug($"Execution analytics calculated: Avg slippage={averageSlippage:P4}, " +
                    $"Total commissions=${totalCommissions:N2}");

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

        #endregion

        #region Trade Analysis

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
                    if (Math.Abs(netPosition) < 0.001m && currentTrade.Count > 1)
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

            foreach (var pnl in tradePnLs)
            {
                cumulativePnL += pnl;
                var totalCapital = InitialCapital + cumulativePnL;
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
            var stdDev = DecimalMath.Sqrt(variance);

            if (stdDev == 0) return 0m;

            var riskFreeRateDaily = RiskFreeRate / 252m; // Convert annual to daily
            var excessReturn = avgReturn - riskFreeRateDaily;

            // Annualize the Sharpe ratio
            return (excessReturn / stdDev) * DecimalMath.Sqrt(252m);
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
                else if (peak > 0)
                {
                    var drawdown = (peak - cumulativePnL) / peak;
                    maxDrawdown = Math.Max(maxDrawdown, drawdown);
                }
            }

            return maxDrawdown;
        }

        #endregion

        #region Analytics Calculations

        private TimeSpan CalculateAverageExecutionTime(Execution[] executions)
        {
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
                ))
                .OrderByDescending(s => s.TradeCount)
                .ToList();
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
                ))
                .OrderByDescending(v => v.OrderCount)
                .ToList();
        }

        #endregion

        #region Metric Updates

        private void UpdatePerformanceMetricTracking(PerformanceMetrics metrics)
        {
            UpdateMetric("Performance.TotalReturn", metrics.TotalReturn);
            UpdateMetric("Performance.DailyReturn", metrics.DailyReturn);
            UpdateMetric("Performance.SharpeRatio", metrics.SharpeRatio);
            UpdateMetric("Performance.MaxDrawdown", metrics.MaxDrawdown);
            UpdateMetric("Performance.WinRate", metrics.WinRate);
            UpdateMetric("Performance.ProfitFactor", metrics.ProfitFactor);
            UpdateMetric("Performance.TotalTrades", metrics.TotalTrades);
        }

        private void UpdateExecutionAnalyticsTracking(Models.ExecutionAnalytics analytics)
        {
            UpdateMetric("Analytics.AverageSlippage", analytics.AverageSlippage);
            UpdateMetric("Analytics.TotalCommissions", analytics.TotalCommissions);
            UpdateMetric("Analytics.FillRate", analytics.FillRate);
            UpdateMetric("Analytics.AverageExecutionTimeMs", analytics.AverageExecutionTime.TotalMilliseconds);
        }

        private void UpdateCachedMetrics(object? state)
        {
            try
            {
                var performanceTask = CalculatePerformanceMetricsAsync();
                var analyticsTask = CalculateExecutionAnalyticsAsync();

                Task.WaitAll(performanceTask, analyticsTask);

                lock (_metricsLock)
                {
                    _cachedPerformanceMetrics = performanceTask.Result;
                    _cachedExecutionAnalytics = analyticsTask.Result;
                    _lastMetricsUpdate = DateTime.UtcNow;
                }

                LogDebug("Updated cached metrics");
            }
            catch (Exception ex)
            {
                LogError("Error updating cached metrics", ex);
            }
        }

        #endregion

        #region Factory Methods

        private PerformanceMetrics CreateEmptyPerformanceMetrics()
        {
            return new PerformanceMetrics(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0, 0, 0, 
                DateTime.UtcNow, DateTime.UtcNow);
        }

        private Models.ExecutionAnalytics CreateEmptyExecutionAnalytics()
        {
            return new Models.ExecutionAnalytics(0m, 0m, TimeSpan.Zero, 0m,
                Array.Empty<SlippageMetric>(), Array.Empty<VenueMetric>(), 
                DateTime.UtcNow, DateTime.UtcNow);
        }

        #endregion

        #region Lifecycle

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo("Starting performance metric update timer");
            _performanceUpdateTimer.Change(
                TimeSpan.FromMilliseconds(PerformanceUpdateIntervalMs),
                TimeSpan.FromMilliseconds(PerformanceUpdateIntervalMs));
            
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo("Stopping performance metric updates");
            _performanceUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            return Task.CompletedTask;
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _performanceUpdateTimer?.Dispose();
            }
            
            base.Dispose(disposing);
        }

        #endregion

        #region Math Utilities

        private static class DecimalMath
        {
            public static decimal Sqrt(decimal value)
            {
                if (value < 0)
                    throw new ArgumentException("Cannot calculate square root of negative number");
                if (value == 0)
                    return 0;

                var x = value;
                var root = value / 2;
                
                for (int i = 0; i < 10; i++)
                {
                    root = (root + value / root) / 2;
                }
                
                return root;
            }
        }

        #endregion
    }
}