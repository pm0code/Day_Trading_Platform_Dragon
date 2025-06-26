using System;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Common;

namespace TradingPlatform.Backtesting.Interfaces
{
    /// <summary>
    /// Core interface for the backtesting engine
    /// </summary>
    public interface IBacktestEngine
    {
        /// <summary>
        /// Run a backtest with the specified strategy and parameters
        /// </summary>
        Task<TradingResult<BacktestResult>> RunBacktestAsync(
            IBacktestStrategy strategy,
            BacktestParameters parameters,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Run multiple backtests in parallel for parameter optimization
        /// </summary>
        Task<TradingResult<BacktestOptimizationResult>> OptimizeStrategyAsync(
            IBacktestStrategy strategy,
            BacktestParameters baseParameters,
            ParameterOptimizationSettings optimizationSettings,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Observable stream of backtest progress updates
        /// </summary>
        IObservable<BacktestProgress> Progress { get; }

        /// <summary>
        /// Observable stream of backtest events for debugging
        /// </summary>
        IObservable<BacktestEvent> Events { get; }

        /// <summary>
        /// Validate strategy and parameters before running backtest
        /// </summary>
        Task<TradingResult> ValidateBacktestAsync(
            IBacktestStrategy strategy,
            BacktestParameters parameters);

        /// <summary>
        /// Get estimated time for backtest completion
        /// </summary>
        TimeSpan EstimateBacktestDuration(
            BacktestParameters parameters,
            int strategyComplexity = 1);
    }

    /// <summary>
    /// Extended interface for advanced backtesting features
    /// </summary>
    public interface IAdvancedBacktestEngine : IBacktestEngine
    {
        /// <summary>
        /// Run walk-forward analysis
        /// </summary>
        Task<TradingResult<WalkForwardResult>> RunWalkForwardAnalysisAsync(
            IBacktestStrategy strategy,
            WalkForwardParameters parameters,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Run Monte Carlo simulation
        /// </summary>
        Task<TradingResult<MonteCarloResult>> RunMonteCarloSimulationAsync(
            BacktestResult baseResult,
            MonteCarloParameters parameters,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Run out-of-sample testing
        /// </summary>
        Task<TradingResult<BacktestResult>> RunOutOfSampleTestAsync(
            IBacktestStrategy strategy,
            BacktestParameters inSampleParameters,
            DateTime outOfSampleStart,
            DateTime outOfSampleEnd,
            CancellationToken cancellationToken = default);
    }
}