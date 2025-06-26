using System;
using System.Collections.Generic;

namespace TradingPlatform.Backtesting.Models
{
    /// <summary>
    /// Comprehensive result of a backtest run
    /// </summary>
    public class BacktestResult
    {
        /// <summary>
        /// Unique identifier for this backtest
        /// </summary>
        public string BacktestId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Strategy name and version
        /// </summary>
        public string StrategyName { get; set; }
        public string StrategyVersion { get; set; }

        /// <summary>
        /// Backtest time period
        /// </summary>
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan Duration => EndDate - StartDate;

        /// <summary>
        /// Initial and final capital
        /// </summary>
        public decimal InitialCapital { get; set; }
        public decimal FinalCapital { get; set; }

        /// <summary>
        /// Return metrics
        /// </summary>
        public decimal TotalReturn { get; set; }
        public decimal TotalReturnPercent { get; set; }
        public decimal AnnualizedReturn { get; set; }
        public decimal CompoundAnnualGrowthRate { get; set; }

        /// <summary>
        /// Risk metrics
        /// </summary>
        public decimal SharpeRatio { get; set; }
        public decimal SortinoRatio { get; set; }
        public decimal CalmarRatio { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal MaxDrawdownPercent { get; set; }
        public DateTime MaxDrawdownStart { get; set; }
        public DateTime MaxDrawdownEnd { get; set; }
        public int MaxDrawdownDuration { get; set; }
        public decimal AverageDrawdown { get; set; }
        public decimal Volatility { get; set; }
        public decimal DownsideDeviation { get; set; }
        public decimal ValueAtRisk95 { get; set; }
        public decimal ConditionalValueAtRisk95 { get; set; }

        /// <summary>
        /// Trade statistics
        /// </summary>
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public decimal WinRate { get; set; }
        public decimal AverageWin { get; set; }
        public decimal AverageLoss { get; set; }
        public decimal LargestWin { get; set; }
        public decimal LargestLoss { get; set; }
        public decimal ProfitFactor { get; set; }
        public decimal ExpectedValue { get; set; }
        public decimal AverageTradeReturn { get; set; }
        public decimal AverageTradeDuration { get; set; }
        public int MaxConsecutiveWins { get; set; }
        public int MaxConsecutiveLosses { get; set; }

        /// <summary>
        /// Position statistics
        /// </summary>
        public int MaxConcurrentPositions { get; set; }
        public decimal AveragePositionSize { get; set; }
        public decimal MaxPositionSize { get; set; }
        public decimal TotalExposure { get; set; }

        /// <summary>
        /// Cost analysis
        /// </summary>
        public decimal TotalCommissions { get; set; }
        public decimal TotalSlippage { get; set; }
        public decimal TotalTransactionCosts { get; set; }
        public decimal CostPerTrade { get; set; }

        /// <summary>
        /// Time-based metrics
        /// </summary>
        public decimal TimeInMarketPercent { get; set; }
        public Dictionary<string, decimal> MonthlyReturns { get; set; } = new();
        public Dictionary<string, decimal> YearlyReturns { get; set; } = new();

        /// <summary>
        /// Benchmark comparison
        /// </summary>
        public decimal BenchmarkReturn { get; set; }
        public decimal Alpha { get; set; }
        public decimal Beta { get; set; }
        public decimal InformationRatio { get; set; }
        public decimal TrackingError { get; set; }

        /// <summary>
        /// Equity curve data
        /// </summary>
        public List<EquityPoint> EquityCurve { get; set; } = new();

        /// <summary>
        /// Detailed trade log
        /// </summary>
        public List<Trade> Trades { get; set; } = new();

        /// <summary>
        /// Position history
        /// </summary>
        public List<PositionSnapshot> PositionHistory { get; set; } = new();

        /// <summary>
        /// Performance by symbol
        /// </summary>
        public Dictionary<string, SymbolPerformance> SymbolPerformance { get; set; } = new();

        /// <summary>
        /// Execution statistics
        /// </summary>
        public decimal AverageSlippagePercent { get; set; }
        public decimal TotalRejectedOrders { get; set; }
        public decimal FillRatePercent { get; set; }

        /// <summary>
        /// Metadata
        /// </summary>
        public DateTime BacktestRunTime { get; set; }
        public TimeSpan BacktestDuration { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Point on the equity curve
    /// </summary>
    public class EquityPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal Equity { get; set; }
        public decimal Cash { get; set; }
        public decimal PositionValue { get; set; }
        public decimal DrawdownPercent { get; set; }
        public decimal CumulativeReturn { get; set; }
    }

    /// <summary>
    /// Individual trade record
    /// </summary>
    public class Trade
    {
        public string TradeId { get; set; }
        public string Symbol { get; set; }
        public TradeDirection Direction { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal ExitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal ProfitLoss { get; set; }
        public decimal ProfitLossPercent { get; set; }
        public decimal Commission { get; set; }
        public decimal Slippage { get; set; }
        public string EntryReason { get; set; }
        public string ExitReason { get; set; }
        public TimeSpan Duration { get; set; }
        public decimal MaxAdverseExcursion { get; set; }
        public decimal MaxFavorableExcursion { get; set; }
    }

    /// <summary>
    /// Performance metrics for individual symbol
    /// </summary>
    public class SymbolPerformance
    {
        public string Symbol { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public decimal TotalProfitLoss { get; set; }
        public decimal WinRate { get; set; }
        public decimal AverageReturn { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal MaxDrawdown { get; set; }
    }

    /// <summary>
    /// Walk-forward analysis result
    /// </summary>
    public class WalkForwardResult
    {
        public List<WalkForwardPeriod> Periods { get; set; } = new();
        public BacktestResult CombinedOutOfSample { get; set; }
        public decimal AverageInSampleSharpe { get; set; }
        public decimal AverageOutOfSampleSharpe { get; set; }
        public decimal EfficiencyRatio { get; set; }
        public bool IsRobust { get; set; }
        public Dictionary<string, object> OptimalParameters { get; set; } = new();
    }

    /// <summary>
    /// Monte Carlo simulation result
    /// </summary>
    public class MonteCarloResult
    {
        public int NumberOfSimulations { get; set; }
        public BacktestResult OriginalResult { get; set; }
        public decimal MedianReturn { get; set; }
        public decimal MeanReturn { get; set; }
        public decimal StandardDeviation { get; set; }
        public Dictionary<double, decimal> ConfidenceIntervals { get; set; } = new();
        public decimal ProbabilityOfProfit { get; set; }
        public decimal ProbabilityOfRuin { get; set; }
        public decimal MaxDrawdownAtConfidence95 { get; set; }
        public List<decimal> SimulatedReturns { get; set; } = new();
    }

    public enum TradeDirection
    {
        Long,
        Short
    }
}