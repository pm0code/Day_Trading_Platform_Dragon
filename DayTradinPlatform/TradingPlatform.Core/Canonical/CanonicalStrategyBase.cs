using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Base class for all canonical strategy implementations.
    /// Provides lifecycle management, signal generation, and performance tracking.
    /// </summary>
    public abstract class CanonicalStrategyBase : CanonicalServiceBase
    {
        private readonly string _strategyId;
        private readonly Dictionary<string, decimal> _parameters;
        private long _signalsGenerated;
        private long _tradesExecuted;
        private decimal _totalPnL;
        private readonly object _metricsLock = new();

        /// <summary>
        /// Gets the unique strategy identifier
        /// </summary>
        public string StrategyId => _strategyId;

        /// <summary>
        /// Gets the strategy name
        /// </summary>
        public abstract string StrategyName { get; }

        /// <summary>
        /// Gets the strategy description
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Gets the supported symbols for this strategy
        /// </summary>
        public abstract string[] SupportedSymbols { get; }

        protected CanonicalStrategyBase(
            ITradingLogger logger,
            string strategyId,
            Dictionary<string, decimal>? parameters = null)
            : base(logger, $"Strategy_{strategyId}")
        {
            _strategyId = strategyId ?? throw new ArgumentNullException(nameof(strategyId));
            _parameters = parameters ?? new Dictionary<string, decimal>();
        }

        #region Abstract Methods

        /// <summary>
        /// Evaluates market conditions and generates trading signals
        /// </summary>
        protected abstract Task<TradingResult<TradingSignal>> GenerateSignalAsync(
            string symbol,
            MarketData marketData,
            PositionInfo? currentPosition,
            CancellationToken cancellationToken);

        /// <summary>
        /// Validates strategy parameters
        /// </summary>
        protected abstract TradingResult ValidateParameters(Dictionary<string, decimal> parameters);

        /// <summary>
        /// Gets default strategy parameters
        /// </summary>
        protected abstract Dictionary<string, decimal> GetDefaultParameters();

        /// <summary>
        /// Calculates position size based on strategy rules
        /// </summary>
        protected abstract Task<decimal> CalculatePositionSizeAsync(
            string symbol,
            decimal accountBalance,
            decimal riskPercentage,
            MarketData marketData);

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets whether the strategy is currently running
        /// </summary>
        public bool IsRunning => ServiceState == ServiceState.Running;

        #endregion

        #region Strategy Execution

        /// <summary>
        /// Processes market data and generates signals
        /// </summary>
        public async Task<TradingResult<TradingSignal>> ProcessMarketDataAsync(
            string symbol,
            MarketData marketData,
            PositionInfo? currentPosition,
            CancellationToken cancellationToken = default)
        {
            if (ServiceState != ServiceState.Running)
            {
                return TradingResult<TradingSignal>.Failure(
                    "STRATEGY_NOT_RUNNING",
                    $"Strategy {_strategyId} is not in running state");
            }

            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    LogDebug($"Processing market data for {symbol}",
                        new { Symbol = symbol, Price = marketData.Price, Volume = marketData.Volume });

                    // Generate signal
                    var signalResult = await GenerateSignalAsync(symbol, marketData, currentPosition, cancellationToken);
                    
                    if (signalResult.IsSuccess && signalResult.Value != null)
                    {
                        Interlocked.Increment(ref _signalsGenerated);
                        UpdateMetric("SignalsGenerated", _signalsGenerated);
                        
                        LogInfo($"Signal generated for {symbol}: {signalResult.Value.SignalType}",
                            additionalData: new
                            {
                                Symbol = symbol,
                                SignalType = signalResult.Value.SignalType,
                                Price = signalResult.Value.Price,
                                Confidence = signalResult.Value.Confidence
                            });
                    }

                    return signalResult;
                },
                nameof(ProcessMarketDataAsync));
        }

        /// <summary>
        /// Updates strategy parameters
        /// </summary>
        public TradingResult UpdateParameters(Dictionary<string, decimal> newParameters)
        {
            var validationResult = ValidateParameters(newParameters);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            lock (_metricsLock)
            {
                _parameters.Clear();
                foreach (var kvp in newParameters)
                {
                    _parameters[kvp.Key] = kvp.Value;
                }
            }

            LogInfo($"Strategy parameters updated",
                additionalData: new { StrategyId = _strategyId, Parameters = newParameters });

            return TradingResult.Success();
        }

        /// <summary>
        /// Records trade execution
        /// </summary>
        public void RecordTradeExecution(string symbol, decimal pnl)
        {
            lock (_metricsLock)
            {
                _tradesExecuted++;
                _totalPnL += pnl;
            }

            UpdateMetric("TradesExecuted", _tradesExecuted);
            UpdateMetric("TotalPnL", _totalPnL);

            LogInfo($"Trade executed for {symbol}",
                additionalData: new
                {
                    Symbol = symbol,
                    PnL = pnl,
                    TotalPnL = _totalPnL,
                    TradeCount = _tradesExecuted
                });
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets a strategy parameter value
        /// </summary>
        protected decimal GetParameter(string name, decimal defaultValue = 0m)
        {
            return _parameters.TryGetValue(name, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Checks if strategy supports a symbol
        /// </summary>
        protected bool SupportsSymbol(string symbol)
        {
            return SupportedSymbols.Contains(symbol, StringComparer.OrdinalIgnoreCase) ||
                   SupportedSymbols.Contains("*"); // Wildcard for all symbols
        }

        /// <summary>
        /// Calculates risk-adjusted position size
        /// </summary>
        protected decimal CalculateRiskAdjustedSize(
            decimal baseSize,
            decimal volatility,
            decimal maxRiskPerTrade)
        {
            if (volatility <= 0) return baseSize;

            // Adjust size based on volatility
            var volatilityMultiplier = 1m / (1m + volatility);
            var adjustedSize = baseSize * volatilityMultiplier;

            // Ensure we don't exceed max risk
            var maxSize = maxRiskPerTrade / volatility;
            return Math.Min(adjustedSize, maxSize);
        }

        #endregion

        #region Lifecycle Implementation

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            // Load default parameters if none provided
            if (_parameters.Count == 0)
            {
                var defaults = GetDefaultParameters();
                foreach (var kvp in defaults)
                {
                    _parameters[kvp.Key] = kvp.Value;
                }
            }

            // Validate parameters
            var validationResult = ValidateParameters(_parameters);
            if (!validationResult.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Invalid strategy parameters: {validationResult.Error?.Message}");
            }

            LogInfo($"Strategy {StrategyName} initialized",
                additionalData: new
                {
                    StrategyId = _strategyId,
                    Parameters = _parameters,
                    SupportedSymbols = SupportedSymbols
                });

            await Task.CompletedTask;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            _signalsGenerated = 0;
            _tradesExecuted = 0;
            _totalPnL = 0m;

            LogInfo($"Strategy {StrategyName} started");
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Strategy {StrategyName} stopped",
                additionalData: new
                {
                    SignalsGenerated = _signalsGenerated,
                    TradesExecuted = _tradesExecuted,
                    TotalPnL = _totalPnL
                });
            return Task.CompletedTask;
        }

        protected override Task<(bool IsHealthy, string Message, Dictionary<string, object>? Details)> 
            OnCheckHealthAsync(CancellationToken cancellationToken)
        {
            var details = new Dictionary<string, object>
            {
                ["SignalsGenerated"] = _signalsGenerated,
                ["TradesExecuted"] = _tradesExecuted,
                ["TotalPnL"] = _totalPnL,
                ["Parameters"] = _parameters
            };

            return Task.FromResult((true, "Strategy is healthy", details));
        }

        #endregion

        #region Metrics

        public override IReadOnlyDictionary<string, object> GetMetrics()
        {
            var baseMetrics = base.GetMetrics().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            lock (_metricsLock)
            {
                baseMetrics["StrategyId"] = _strategyId;
                baseMetrics["StrategyName"] = StrategyName;
                baseMetrics["SignalsGenerated"] = _signalsGenerated;
                baseMetrics["TradesExecuted"] = _tradesExecuted;
                baseMetrics["TotalPnL"] = _totalPnL;
                baseMetrics["WinRate"] = _tradesExecuted > 0 ? 
                    (decimal)_parameters.Count(p => p.Value > 0) / _tradesExecuted : 0m;
                baseMetrics["ParameterCount"] = _parameters.Count;
            }

            return baseMetrics;
        }

        #endregion
    }

    /// <summary>
    /// Trading signal generated by a strategy
    /// </summary>
    public record TradingSignal(
        string Id,
        string StrategyId,
        string Symbol,
        SignalType SignalType,
        decimal Price,
        decimal Quantity,
        decimal Confidence,
        string Reason,
        DateTime Timestamp,
        Dictionary<string, object>? Metadata = null);

    /// <summary>
    /// Current position information
    /// </summary>
    public record PositionInfo(
        string Symbol,
        decimal Quantity,
        decimal AveragePrice,
        decimal CurrentPrice,
        decimal UnrealizedPnL,
        DateTime OpenTime);

    /// <summary>
    /// Market data snapshot
    /// </summary>
    public record MarketData(
        string Symbol,
        decimal Price,
        decimal Bid,
        decimal Ask,
        decimal Volume,
        decimal High,
        decimal Low,
        decimal Open,
        decimal Close,
        DateTime Timestamp);

    /// <summary>
    /// Signal type enumeration
    /// </summary>
    public enum SignalType
    {
        Buy,
        Sell,
        Hold,
        StopLoss,
        TakeProfit,
        ClosePosition
    }
}