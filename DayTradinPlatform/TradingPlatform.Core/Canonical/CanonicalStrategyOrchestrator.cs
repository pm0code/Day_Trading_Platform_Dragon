using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Base class for canonical strategy orchestration and management.
    /// Coordinates multiple strategies, signal aggregation, and execution flow.
    /// </summary>
    public abstract class CanonicalStrategyOrchestrator : CanonicalServiceBase
    {
        private readonly ConcurrentDictionary<string, CanonicalStrategyBase> _strategies;
        private readonly ConcurrentDictionary<string, StrategyPerformance> _performances;
        private readonly SemaphoreSlim _orchestrationLock;
        private long _totalSignalsProcessed;
        private long _totalOrdersGenerated;

        protected CanonicalStrategyOrchestrator(
            ITradingLogger logger,
            string orchestratorName)
            : base(logger, orchestratorName)
        {
            _strategies = new ConcurrentDictionary<string, CanonicalStrategyBase>();
            _performances = new ConcurrentDictionary<string, StrategyPerformance>();
            _orchestrationLock = new SemaphoreSlim(1, 1);
        }

        #region Abstract Methods

        /// <summary>
        /// Aggregates signals from multiple strategies
        /// </summary>
        protected abstract Task<TradingResult<AggregatedSignal>> AggregateSignalsAsync(
            string symbol,
            IEnumerable<TradingSignal> signals,
            CancellationToken cancellationToken);

        /// <summary>
        /// Performs risk assessment on aggregated signal
        /// </summary>
        protected abstract Task<TradingResult<RiskAssessment>> AssessSignalRiskAsync(
            AggregatedSignal signal,
            PortfolioState portfolio,
            CancellationToken cancellationToken);

        /// <summary>
        /// Converts validated signal to order request
        /// </summary>
        protected abstract Task<TradingResult<OrderRequest>> GenerateOrderAsync(
            AggregatedSignal signal,
            RiskAssessment risk,
            CancellationToken cancellationToken);

        /// <summary>
        /// Gets the portfolio state
        /// </summary>
        protected abstract Task<PortfolioState> GetPortfolioStateAsync(CancellationToken cancellationToken);

        #endregion

        #region Strategy Management

        /// <summary>
        /// Registers a strategy with the orchestrator
        /// </summary>
        public async Task<TradingResult> RegisterStrategyAsync(
            CanonicalStrategyBase strategy,
            CancellationToken cancellationToken = default)
        {
            if (strategy == null)
            {
                return TradingResult.Failure("INVALID_STRATEGY", "Strategy cannot be null");
            }

            await _orchestrationLock.WaitAsync(cancellationToken);
            try
            {
                if (_strategies.ContainsKey(strategy.StrategyId))
                {
                    return TradingResult.Failure("DUPLICATE_STRATEGY", 
                        $"Strategy {strategy.StrategyId} is already registered");
                }

                // Initialize strategy
                if (!await strategy.InitializeAsync(cancellationToken))
                {
                    return TradingResult.Failure("INIT_FAILED", 
                        $"Failed to initialize strategy {strategy.StrategyId}");
                }

                _strategies[strategy.StrategyId] = strategy;
                _performances[strategy.StrategyId] = new StrategyPerformance(strategy.StrategyId);

                LogInfo($"Strategy registered: {strategy.StrategyName}",
                    additionalData: new { StrategyId = strategy.StrategyId });

                return TradingResult.Success();
            }
            finally
            {
                _orchestrationLock.Release();
            }
        }

        /// <summary>
        /// Starts a registered strategy
        /// </summary>
        public async Task<TradingResult> StartStrategyAsync(
            string strategyId,
            CancellationToken cancellationToken = default)
        {
            if (!_strategies.TryGetValue(strategyId, out var strategy))
            {
                return TradingResult.Failure("STRATEGY_NOT_FOUND", 
                    $"Strategy {strategyId} not found");
            }

            if (!await strategy.StartAsync(cancellationToken))
            {
                return TradingResult.Failure("START_FAILED", 
                    $"Failed to start strategy {strategyId}");
            }

            return TradingResult.Success();
        }

        /// <summary>
        /// Stops a running strategy
        /// </summary>
        public async Task<TradingResult> StopStrategyAsync(
            string strategyId,
            CancellationToken cancellationToken = default)
        {
            if (!_strategies.TryGetValue(strategyId, out var strategy))
            {
                return TradingResult.Failure("STRATEGY_NOT_FOUND", 
                    $"Strategy {strategyId} not found");
            }

            if (!await strategy.StopAsync(cancellationToken))
            {
                return TradingResult.Failure("STOP_FAILED", 
                    $"Failed to stop strategy {strategyId}");
            }

            return TradingResult.Success();
        }

        #endregion

        #region Signal Processing

        /// <summary>
        /// Processes market data through all active strategies
        /// </summary>
        public async Task<TradingResult<OrderRequest>> ProcessMarketDataAsync(
            string symbol,
            MarketData marketData,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    // Get portfolio state
                    var portfolio = await GetPortfolioStateAsync(cancellationToken);
                    var currentPosition = portfolio.Positions.FirstOrDefault(p => p.Symbol == symbol);

                    // Collect signals from all running strategies
                    var signals = new List<TradingSignal>();
                    var activeStrategies = _strategies.Values
                        .Where(s => s.ServiceState == ServiceState.Running)
                        .ToList();

                    LogDebug($"Processing market data for {symbol} through {activeStrategies.Count} strategies");

                    foreach (var strategy in activeStrategies)
                    {
                        try
                        {
                            var signalResult = await strategy.ProcessMarketDataAsync(
                                symbol, 
                                marketData, 
                                currentPosition, 
                                cancellationToken);

                            if (signalResult.IsSuccess && signalResult.Value != null)
                            {
                                signals.Add(signalResult.Value);
                                UpdateStrategyPerformance(strategy.StrategyId, signalResult.Value);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError($"Strategy {strategy.StrategyId} failed to process market data", ex);
                        }
                    }

                    Interlocked.Add(ref _totalSignalsProcessed, signals.Count);

                    if (!signals.Any())
                    {
                        return TradingResult<OrderRequest>.Failure("NO_SIGNALS", 
                            "No signals generated from active strategies");
                    }

                    // Aggregate signals
                    var aggregationResult = await AggregateSignalsAsync(symbol, signals, cancellationToken);
                    if (!aggregationResult.IsSuccess)
                    {
                        return TradingResult<OrderRequest>.Failure(aggregationResult.Error!);
                    }

                    // Assess risk
                    var riskResult = await AssessSignalRiskAsync(
                        aggregationResult.Value, 
                        portfolio, 
                        cancellationToken);
                    
                    if (!riskResult.IsSuccess)
                    {
                        return TradingResult<OrderRequest>.Failure(riskResult.Error!);
                    }

                    if (!riskResult.Value.IsAcceptable)
                    {
                        LogWarning($"Signal rejected due to risk assessment",
                            additionalData: new { Symbol = symbol, Risk = riskResult.Value });
                        return TradingResult<OrderRequest>.Failure("RISK_REJECTED", 
                            "Signal rejected by risk assessment");
                    }

                    // Generate order
                    var orderResult = await GenerateOrderAsync(
                        aggregationResult.Value, 
                        riskResult.Value, 
                        cancellationToken);

                    if (orderResult.IsSuccess)
                    {
                        Interlocked.Increment(ref _totalOrdersGenerated);
                        LogInfo($"Order generated for {symbol}",
                            additionalData: new
                            {
                                Symbol = symbol,
                                OrderType = orderResult.Value.OrderType,
                                Quantity = orderResult.Value.Quantity,
                                Price = orderResult.Value.Price
                            });
                    }

                    return orderResult;
                },
                nameof(ProcessMarketDataAsync));
        }

        #endregion

        #region Performance Tracking

        private void UpdateStrategyPerformance(string strategyId, TradingSignal signal)
        {
            if (_performances.TryGetValue(strategyId, out var performance))
            {
                performance.RecordSignal(signal);
                UpdateMetric($"Strategy_{strategyId}_Signals", performance.SignalCount);
            }
        }

        /// <summary>
        /// Records trade execution result
        /// </summary>
        public void RecordTradeExecution(string strategyId, string symbol, decimal pnl)
        {
            if (_strategies.TryGetValue(strategyId, out var strategy))
            {
                strategy.RecordTradeExecution(symbol, pnl);
            }

            if (_performances.TryGetValue(strategyId, out var performance))
            {
                performance.RecordTrade(pnl);
                UpdateMetric($"Strategy_{strategyId}_PnL", performance.TotalPnL);
            }
        }

        /// <summary>
        /// Gets performance metrics for all strategies
        /// </summary>
        public IReadOnlyDictionary<string, StrategyPerformance> GetPerformanceMetrics()
        {
            return _performances.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        #endregion

        #region Lifecycle Implementation

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Strategy orchestrator initializing");
            await Task.CompletedTask;
        }

        protected override async Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Strategy orchestrator starting");
            
            // Start all registered strategies
            var startTasks = _strategies.Values.Select(s => s.StartAsync(cancellationToken));
            await Task.WhenAll(startTasks);
            
            LogInfo($"Started {_strategies.Count} strategies");
        }

        protected override async Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo($"Strategy orchestrator stopping");
            
            // Stop all running strategies
            var stopTasks = _strategies.Values
                .Where(s => s.ServiceState == ServiceState.Running)
                .Select(s => s.StopAsync(cancellationToken));
            
            await Task.WhenAll(stopTasks);
            
            LogInfo($"Strategy orchestrator stopped",
                additionalData: new
                {
                    TotalSignals = _totalSignalsProcessed,
                    TotalOrders = _totalOrdersGenerated,
                    ActiveStrategies = _strategies.Count
                });
        }

        #endregion

        #region Metrics

        public override IReadOnlyDictionary<string, object> GetMetrics()
        {
            var baseMetrics = base.GetMetrics().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            baseMetrics["TotalStrategies"] = _strategies.Count;
            baseMetrics["ActiveStrategies"] = _strategies.Count(s => s.Value.ServiceState == ServiceState.Running);
            baseMetrics["TotalSignalsProcessed"] = _totalSignalsProcessed;
            baseMetrics["TotalOrdersGenerated"] = _totalOrdersGenerated;
            
            // Aggregate performance metrics
            var totalPnL = _performances.Values.Sum(p => p.TotalPnL);
            var totalTrades = _performances.Values.Sum(p => p.TradeCount);
            var winRate = totalTrades > 0 ? 
                _performances.Values.Sum(p => p.WinCount) / (decimal)totalTrades : 0m;

            baseMetrics["TotalPnL"] = totalPnL;
            baseMetrics["TotalTrades"] = totalTrades;
            baseMetrics["OverallWinRate"] = winRate;

            return baseMetrics;
        }

        #endregion

        #region Disposal

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _orchestrationLock?.Dispose();
                
                // Dispose all strategies
                foreach (var strategy in _strategies.Values)
                {
                    strategy.Dispose();
                }
            }
            
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// Aggregated signal from multiple strategies
    /// </summary>
    public record AggregatedSignal(
        string Symbol,
        SignalType ConsensusSignal,
        decimal AveragePrice,
        decimal TotalQuantity,
        decimal Confidence,
        string[] ContributingStrategies,
        Dictionary<string, object> Metadata);

    /// <summary>
    /// Risk assessment result
    /// </summary>
    public record RiskAssessment(
        bool IsAcceptable,
        decimal RiskScore,
        decimal MaxPositionSize,
        decimal StopLoss,
        decimal TakeProfit,
        string[] RiskFactors);

    /// <summary>
    /// Order request generated from signal
    /// </summary>
    public record OrderRequest(
        string Symbol,
        OrderType OrderType,
        OrderSide Side,
        decimal Quantity,
        decimal Price,
        decimal? StopLoss,
        decimal? TakeProfit,
        string StrategyId,
        Dictionary<string, object> Metadata);

    /// <summary>
    /// Portfolio state snapshot
    /// </summary>
    public record PortfolioState(
        decimal AccountBalance,
        decimal BuyingPower,
        decimal TotalEquity,
        PositionInfo[] Positions,
        decimal UnrealizedPnL,
        decimal RealizedPnL);

    /// <summary>
    /// Strategy performance tracking
    /// </summary>
    public class StrategyPerformance
    {
        private readonly object _lock = new();
        
        public string StrategyId { get; }
        public long SignalCount { get; private set; }
        public long TradeCount { get; private set; }
        public long WinCount { get; private set; }
        public decimal TotalPnL { get; private set; }
        public DateTime LastSignal { get; private set; }
        public DateTime LastTrade { get; private set; }

        public StrategyPerformance(string strategyId)
        {
            StrategyId = strategyId;
            LastSignal = DateTime.MinValue;
            LastTrade = DateTime.MinValue;
        }

        public void RecordSignal(TradingSignal signal)
        {
            lock (_lock)
            {
                SignalCount++;
                LastSignal = signal.Timestamp;
            }
        }

        public void RecordTrade(decimal pnl)
        {
            lock (_lock)
            {
                TradeCount++;
                TotalPnL += pnl;
                if (pnl > 0) WinCount++;
                LastTrade = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Order type enumeration
    /// </summary>
    public enum OrderType
    {
        Market,
        Limit,
        Stop,
        StopLimit
    }

    /// <summary>
    /// Order side enumeration
    /// </summary>
    public enum OrderSide
    {
        Buy,
        Sell
    }
}