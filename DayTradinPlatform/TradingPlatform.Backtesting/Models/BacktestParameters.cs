using System;
using System.Collections.Generic;

namespace TradingPlatform.Backtesting.Models
{
    /// <summary>
    /// Parameters for configuring a backtest run
    /// </summary>
    public class BacktestParameters
    {
        /// <summary>
        /// Start date for the backtest
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// End date for the backtest
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Initial capital in USD
        /// </summary>
        public decimal InitialCapital { get; set; } = 100000m;

        /// <summary>
        /// Symbols to include in the backtest
        /// </summary>
        public List<string> Symbols { get; set; } = new();

        /// <summary>
        /// Data frequency for the backtest
        /// </summary>
        public DataFrequency DataFrequency { get; set; } = DataFrequency.Minute;

        /// <summary>
        /// Warmup period in days for indicators
        /// </summary>
        public int WarmupPeriodDays { get; set; } = 30;

        /// <summary>
        /// Commission per trade
        /// </summary>
        public decimal CommissionPerTrade { get; set; } = 4.95m;

        /// <summary>
        /// Commission per share (if applicable)
        /// </summary>
        public decimal CommissionPerShare { get; set; } = 0.005m;

        /// <summary>
        /// Minimum commission per trade
        /// </summary>
        public decimal MinimumCommission { get; set; } = 1.00m;

        /// <summary>
        /// Slippage model to use
        /// </summary>
        public SlippageModel SlippageModel { get; set; } = SlippageModel.Linear;

        /// <summary>
        /// Base slippage in basis points
        /// </summary>
        public decimal BaseSlippageBps { get; set; } = 10m;

        /// <summary>
        /// Whether to include extended hours trading
        /// </summary>
        public bool IncludeExtendedHours { get; set; } = false;

        /// <summary>
        /// Maximum position size as percentage of portfolio
        /// </summary>
        public decimal MaxPositionSizePercent { get; set; } = 0.20m;

        /// <summary>
        /// Maximum number of concurrent positions
        /// </summary>
        public int MaxConcurrentPositions { get; set; } = 10;

        /// <summary>
        /// Use margin trading
        /// </summary>
        public bool UseMargin { get; set; } = false;

        /// <summary>
        /// Maximum leverage if using margin
        /// </summary>
        public decimal MaxLeverage { get; set; } = 2.0m;

        /// <summary>
        /// Annual risk-free rate for Sharpe ratio calculation
        /// </summary>
        public decimal RiskFreeRate { get; set; } = 0.02m;

        /// <summary>
        /// Benchmark symbol for relative performance
        /// </summary>
        public string BenchmarkSymbol { get; set; } = "SPY";

        /// <summary>
        /// Random seed for reproducibility
        /// </summary>
        public int? RandomSeed { get; set; }

        /// <summary>
        /// Enable detailed logging
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;

        /// <summary>
        /// Save trade logs
        /// </summary>
        public bool SaveTradeLogs { get; set; } = true;

        /// <summary>
        /// Generate performance report
        /// </summary>
        public bool GenerateReport { get; set; } = true;

        /// <summary>
        /// Custom parameters for strategy
        /// </summary>
        public Dictionary<string, object> CustomParameters { get; set; } = new();
    }

    /// <summary>
    /// Data frequency for backtesting
    /// </summary>
    public enum DataFrequency
    {
        Tick,
        Second,
        Minute,
        FiveMinute,
        FifteenMinute,
        ThirtyMinute,
        Hour,
        Daily
    }

    /// <summary>
    /// Slippage model types
    /// </summary>
    public enum SlippageModel
    {
        None,
        Fixed,
        Linear,
        Square,
        Market,
        Custom
    }

    /// <summary>
    /// Walk-forward analysis parameters
    /// </summary>
    public class WalkForwardParameters : BacktestParameters
    {
        /// <summary>
        /// In-sample period length in days
        /// </summary>
        public int InSampleDays { get; set; } = 252;

        /// <summary>
        /// Out-of-sample period length in days
        /// </summary>
        public int OutOfSampleDays { get; set; } = 63;

        /// <summary>
        /// Step size in days for rolling window
        /// </summary>
        public int StepSizeDays { get; set; } = 21;

        /// <summary>
        /// Optimization metric to use
        /// </summary>
        public OptimizationMetric OptimizationMetric { get; set; } = OptimizationMetric.SharpeRatio;

        /// <summary>
        /// Minimum number of trades required in sample
        /// </summary>
        public int MinimumTrades { get; set; } = 30;
    }

    /// <summary>
    /// Monte Carlo simulation parameters
    /// </summary>
    public class MonteCarloParameters
    {
        /// <summary>
        /// Number of simulations to run
        /// </summary>
        public int NumberOfSimulations { get; set; } = 1000;

        /// <summary>
        /// Confidence levels for statistics
        /// </summary>
        public List<double> ConfidenceLevels { get; set; } = new() { 0.95, 0.99 };

        /// <summary>
        /// Randomize trade order
        /// </summary>
        public bool RandomizeTradeOrder { get; set; } = true;

        /// <summary>
        /// Randomize trade returns
        /// </summary>
        public bool RandomizeReturns { get; set; } = true;

        /// <summary>
        /// Apply bootstrapping
        /// </summary>
        public bool UseBootstrapping { get; set; } = true;

        /// <summary>
        /// Random seed for reproducibility
        /// </summary>
        public int? RandomSeed { get; set; }
    }

    /// <summary>
    /// Optimization metric for strategy selection
    /// </summary>
    public enum OptimizationMetric
    {
        TotalReturn,
        SharpeRatio,
        SortinoRatio,
        CalmarRatio,
        ProfitFactor,
        WinRate,
        MaxDrawdown,
        Custom
    }
}