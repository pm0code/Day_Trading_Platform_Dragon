using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.StrategyEngine.Models;

namespace TradingPlatform.StrategyEngine.Services
{
    /// <summary>
    /// Canonical implementation of strategy management and orchestration.
    /// Manages strategy lifecycle, signal aggregation, and execution coordination.
    /// </summary>
    public class StrategyManagerCanonical : CanonicalStrategyOrchestrator, IStrategyManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMarketDataService _marketDataService;
        private readonly IPortfolioService _portfolioService;
        private readonly IRiskManagementService _riskService;
        
        private decimal _confidenceThreshold = 0.6m;
        private decimal _maxPortfolioRisk = 0.02m; // 2% max risk

        public StrategyManagerCanonical(IServiceProvider serviceProvider)
            : base(serviceProvider.GetRequiredService<ITradingLogger>(), "StrategyManager")
        {
            _serviceProvider = serviceProvider;
            _marketDataService = serviceProvider.GetRequiredService<IMarketDataService>();
            _portfolioService = serviceProvider.GetRequiredService<IPortfolioService>();
            _riskService = serviceProvider.GetRequiredService<IRiskManagementService>();
        }

        #region IStrategyManager Implementation

        public async Task<StrategyInfo[]> GetActiveStrategiesAsync()
        {
            var strategies = GetRegisteredStrategies();
            var infos = new List<StrategyInfo>();
            var performances = GetPerformanceMetrics();

            foreach (var strategy in strategies)
            {
                var performance = performances.TryGetValue(strategy.StrategyId, out var perf) 
                    ? perf 
                    : null;

                infos.Add(new StrategyInfo(
                    Id: strategy.StrategyId,
                    Name: strategy.StrategyName,
                    Status: MapServiceStateToStrategyStatus(strategy.ServiceState),
                    StartedAt: DateTimeOffset.UtcNow, // TODO: Track actual start time
                    PnL: performance?.TotalPnL ?? 0m,
                    TradeCount: (int)(performance?.TradeCount ?? 0),
                    Parameters: strategy.GetMetrics()
                        .Where(kvp => kvp.Key.StartsWith("Parameter_"))
                        .ToDictionary(kvp => kvp.Key.Replace("Parameter_", ""), kvp => kvp.Value)
                ));
            }

            return await Task.FromResult(infos.ToArray());
        }

        public async Task<StrategyResult> StartStrategyAsync(string strategyId)
        {
            var result = await StartStrategyAsync(strategyId, CancellationToken.None);
            
            return result.IsSuccess
                ? new StrategyResult(true, $"Strategy {strategyId} started successfully")
                : new StrategyResult(false, result.Error?.Message ?? "Failed to start strategy", 
                    result.Error?.ErrorCode);
        }

        public async Task<StrategyResult> StopStrategyAsync(string strategyId)
        {
            var result = await base.StopStrategyAsync(strategyId, CancellationToken.None);
            
            return result.IsSuccess
                ? new StrategyResult(true, $"Strategy {strategyId} stopped successfully")
                : new StrategyResult(false, result.Error?.Message ?? "Failed to stop strategy", 
                    result.Error?.ErrorCode);
        }

        public async Task<StrategyResult> UpdateStrategyParametersAsync(
            string strategyId, 
            Dictionary<string, object> parameters)
        {
            var strategy = GetRegisteredStrategies()
                .FirstOrDefault(s => s.StrategyId == strategyId);
            
            if (strategy == null)
            {
                return new StrategyResult(false, $"Strategy {strategyId} not found", "STRATEGY_NOT_FOUND");
            }

            // Convert parameters to decimal dictionary
            var decimalParams = new Dictionary<string, decimal>();
            foreach (var kvp in parameters)
            {
                if (kvp.Value is IConvertible convertible)
                {
                    try
                    {
                        decimalParams[kvp.Key] = Convert.ToDecimal(convertible);
                    }
                    catch
                    {
                        return new StrategyResult(false, 
                            $"Invalid parameter value for {kvp.Key}: {kvp.Value}", 
                            "INVALID_PARAMETER");
                    }
                }
            }

            var updateResult = strategy.UpdateParameters(decimalParams);
            
            return updateResult.IsSuccess
                ? new StrategyResult(true, "Parameters updated successfully")
                : new StrategyResult(false, updateResult.Error?.Message ?? "Failed to update parameters", 
                    updateResult.Error?.ErrorCode);
        }

        public async Task<StrategyPerformance> GetStrategyPerformanceAsync(string strategyId)
        {
            var performances = GetPerformanceMetrics();
            
            if (!performances.TryGetValue(strategyId, out var performance))
            {
                return new StrategyPerformance(
                    StrategyId: strategyId,
                    TotalPnL: 0m,
                    UnrealizedPnL: 0m,
                    TotalTrades: 0,
                    WinningTrades: 0,
                    LosingTrades: 0,
                    WinRate: 0m,
                    SharpeRatio: 0m,
                    MaxDrawdown: 0m,
                    ActiveDuration: TimeSpan.Zero,
                    LastUpdate: DateTimeOffset.UtcNow
                );
            }

            // Calculate additional metrics
            var winRate = performance.TradeCount > 0 
                ? (decimal)performance.WinCount / performance.TradeCount 
                : 0m;
            
            var losingTrades = performance.TradeCount - performance.WinCount;
            
            return await Task.FromResult(new StrategyPerformance(
                StrategyId: strategyId,
                TotalPnL: performance.TotalPnL,
                UnrealizedPnL: 0m, // TODO: Get from portfolio service
                TotalTrades: (int)performance.TradeCount,
                WinningTrades: (int)performance.WinCount,
                LosingTrades: (int)losingTrades,
                WinRate: winRate,
                SharpeRatio: 0m, // TODO: Calculate Sharpe ratio
                MaxDrawdown: 0m, // TODO: Track max drawdown
                ActiveDuration: DateTime.UtcNow - performance.LastTrade,
                LastUpdate: DateTimeOffset.UtcNow
            ));
        }

        public async Task<TradingSignal?> GenerateSignalAsync(SignalRequest request)
        {
            var strategy = GetRegisteredStrategies()
                .FirstOrDefault(s => s.StrategyId == request.StrategyId);
            
            if (strategy == null)
            {
                LogWarning($"Strategy {request.StrategyId} not found");
                return null;
            }

            // Get current market data
            var marketData = await _marketDataService.GetLatestDataAsync(request.Symbol);
            if (marketData == null)
            {
                LogWarning($"No market data available for {request.Symbol}");
                return null;
            }

            // Get current position
            var portfolio = await GetPortfolioStateAsync(CancellationToken.None);
            var position = portfolio.Positions.FirstOrDefault(p => p.Symbol == request.Symbol);

            // Process through strategy
            var signalResult = await strategy.ProcessMarketDataAsync(
                request.Symbol,
                marketData,
                position,
                CancellationToken.None);

            if (!signalResult.IsSuccess || signalResult.Value == null)
            {
                return null;
            }

            // Convert internal signal to public model
            return new TradingSignal(
                Id: signalResult.Value.Id,
                StrategyId: signalResult.Value.StrategyId,
                Symbol: signalResult.Value.Symbol,
                SignalType: MapSignalType(signalResult.Value.SignalType),
                Price: signalResult.Value.Price,
                Quantity: (int)signalResult.Value.Quantity,
                Confidence: signalResult.Value.Confidence,
                Reason: signalResult.Value.Reason,
                CreatedAt: DateTimeOffset.FromDateTime(signalResult.Value.Timestamp),
                Metadata: signalResult.Value.Metadata
            );
        }

        public async Task<StrategyHealthStatus> GetHealthStatusAsync()
        {
            var health = await CheckHealthAsync(CancellationToken.None);
            var strategies = GetRegisteredStrategies();
            var activeCount = strategies.Count(s => s.ServiceState == ServiceState.Running);
            var metrics = GetMetrics();

            var issues = new List<string>();
            if (!health.IsHealthy)
            {
                issues.Add(health.HealthMessage);
            }

            if (activeCount == 0)
            {
                issues.Add("No active strategies");
            }

            var recentSignals = metrics.TryGetValue("TotalSignalsProcessed", out var signals) 
                ? Convert.ToInt64(signals) 
                : 0L;

            return new StrategyHealthStatus(
                IsHealthy: health.IsHealthy && activeCount > 0,
                Status: health.HealthMessage,
                ActiveStrategies: activeCount,
                RecentSignals: recentSignals,
                AverageLatency: TimeSpan.Zero, // TODO: Track latency
                LastExecution: DateTime.UtcNow,
                Issues: issues.ToArray()
            );
        }

        #endregion

        #region Orchestrator Implementation

        protected override async Task<TradingResult<AggregatedSignal>> AggregateSignalsAsync(
            string symbol,
            IEnumerable<TradingSignal> signals,
            CancellationToken cancellationToken)
        {
            var signalList = signals.ToList();
            
            if (!signalList.Any())
            {
                return TradingResult<AggregatedSignal>.Failure("NO_SIGNALS", "No signals to aggregate");
            }

            // Group signals by type
            var signalGroups = signalList.GroupBy(s => s.SignalType).ToList();
            
            // Find consensus signal (most common)
            var consensusGroup = signalGroups.OrderByDescending(g => g.Count()).First();
            var consensusSignal = consensusGroup.Key;
            
            // Calculate weighted average price and total quantity
            decimal totalWeight = 0m;
            decimal weightedPrice = 0m;
            decimal totalQuantity = 0m;
            
            foreach (var signal in consensusGroup)
            {
                var weight = signal.Confidence;
                totalWeight += weight;
                weightedPrice += signal.Price * weight;
                totalQuantity += signal.Quantity;
            }
            
            var averagePrice = totalWeight > 0 ? weightedPrice / totalWeight : consensusGroup.Average(s => s.Price);
            var confidence = consensusGroup.Average(s => s.Confidence);
            
            // Only proceed if confidence meets threshold
            if (confidence < _confidenceThreshold)
            {
                return TradingResult<AggregatedSignal>.Failure("LOW_CONFIDENCE", 
                    $"Aggregated confidence {confidence:P0} below threshold {_confidenceThreshold:P0}");
            }

            var aggregated = new AggregatedSignal(
                Symbol: symbol,
                ConsensusSignal: consensusSignal,
                AveragePrice: averagePrice,
                TotalQuantity: totalQuantity,
                Confidence: confidence,
                ContributingStrategies: signalList.Select(s => s.StrategyId).Distinct().ToArray(),
                Metadata: new Dictionary<string, object>
                {
                    ["SignalCount"] = signalList.Count,
                    ["ConsensusCount"] = consensusGroup.Count(),
                    ["SignalTypes"] = signalGroups.Select(g => g.Key.ToString()).ToArray()
                }
            );

            return await Task.FromResult(TradingResult<AggregatedSignal>.Success(aggregated));
        }

        protected override async Task<TradingResult<RiskAssessment>> AssessSignalRiskAsync(
            AggregatedSignal signal,
            PortfolioState portfolio,
            CancellationToken cancellationToken)
        {
            // Calculate position sizing based on risk
            var riskAmount = portfolio.TotalEquity * _maxPortfolioRisk;
            var stopLossPercentage = 0.02m; // 2% stop loss
            var maxPositionSize = riskAmount / stopLossPercentage;
            
            // Calculate stop loss and take profit levels
            var stopLoss = signal.ConsensusSignal == SignalType.Buy
                ? signal.AveragePrice * (1 - stopLossPercentage)
                : signal.AveragePrice * (1 + stopLossPercentage);
                
            var takeProfitMultiplier = 2m; // 2:1 risk/reward ratio
            var takeProfit = signal.ConsensusSignal == SignalType.Buy
                ? signal.AveragePrice + (signal.AveragePrice - stopLoss) * takeProfitMultiplier
                : signal.AveragePrice - (stopLoss - signal.AveragePrice) * takeProfitMultiplier;

            // Check portfolio risk
            var currentRisk = await _riskService.GetCurrentRiskExposureAsync();
            var riskFactors = new List<string>();
            
            if (currentRisk > 0.8m)
            {
                riskFactors.Add("High portfolio risk exposure");
            }
            
            // Check position concentration
            var positionValue = signal.TotalQuantity * signal.AveragePrice;
            var concentrationRisk = positionValue / portfolio.TotalEquity;
            
            if (concentrationRisk > 0.1m) // 10% concentration limit
            {
                riskFactors.Add("Position concentration too high");
            }
            
            var riskScore = (currentRisk + concentrationRisk) / 2m;
            var isAcceptable = riskScore < 0.5m && !riskFactors.Any();
            
            return TradingResult<RiskAssessment>.Success(new RiskAssessment(
                IsAcceptable: isAcceptable,
                RiskScore: riskScore,
                MaxPositionSize: maxPositionSize,
                StopLoss: stopLoss,
                TakeProfit: takeProfit,
                RiskFactors: riskFactors.ToArray()
            ));
        }

        protected override async Task<TradingResult<OrderRequest>> GenerateOrderAsync(
            AggregatedSignal signal,
            RiskAssessment risk,
            CancellationToken cancellationToken)
        {
            // Determine order side
            var side = signal.ConsensusSignal switch
            {
                SignalType.Buy => OrderSide.Buy,
                SignalType.Sell => OrderSide.Sell,
                SignalType.StopLoss => OrderSide.Sell,
                SignalType.TakeProfit => OrderSide.Sell,
                _ => throw new InvalidOperationException($"Cannot generate order for signal type {signal.ConsensusSignal}")
            };
            
            // Adjust quantity based on risk assessment
            var quantity = Math.Min(signal.TotalQuantity, risk.MaxPositionSize / signal.AveragePrice);
            quantity = Math.Floor(quantity); // Round down to whole shares
            
            if (quantity <= 0)
            {
                return TradingResult<OrderRequest>.Failure("INVALID_QUANTITY", 
                    "Risk-adjusted quantity is zero or negative");
            }

            var order = new OrderRequest(
                Symbol: signal.Symbol,
                OrderType: OrderType.Limit,
                Side: side,
                Quantity: quantity,
                Price: signal.AveragePrice,
                StopLoss: risk.StopLoss,
                TakeProfit: risk.TakeProfit,
                StrategyId: string.Join(",", signal.ContributingStrategies),
                Metadata: new Dictionary<string, object>
                {
                    ["Confidence"] = signal.Confidence,
                    ["RiskScore"] = risk.RiskScore,
                    ["SignalCount"] = signal.ContributingStrategies.Length
                }
            );

            return await Task.FromResult(TradingResult<OrderRequest>.Success(order));
        }

        protected override async Task<PortfolioState> GetPortfolioStateAsync(CancellationToken cancellationToken)
        {
            var portfolio = await _portfolioService.GetPortfolioAsync();
            
            var positions = portfolio.Positions.Select(p => new PositionInfo(
                Symbol: p.Symbol,
                Quantity: p.Quantity,
                AveragePrice: p.AveragePrice,
                CurrentPrice: p.CurrentPrice,
                UnrealizedPnL: p.UnrealizedPnL,
                OpenTime: p.OpenTime
            )).ToArray();
            
            return new PortfolioState(
                AccountBalance: portfolio.CashBalance,
                BuyingPower: portfolio.BuyingPower,
                TotalEquity: portfolio.TotalEquity,
                Positions: positions,
                UnrealizedPnL: portfolio.UnrealizedPnL,
                RealizedPnL: portfolio.RealizedPnL
            );
        }

        #endregion

        #region Helper Methods

        private IEnumerable<CanonicalStrategyBase> GetRegisteredStrategies()
        {
            // Use reflection to access protected _strategies dictionary
            var strategiesField = typeof(CanonicalStrategyOrchestrator)
                .GetField("_strategies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (strategiesField?.GetValue(this) is System.Collections.Concurrent.ConcurrentDictionary<string, CanonicalStrategyBase> strategies)
            {
                return strategies.Values;
            }
            
            return Enumerable.Empty<CanonicalStrategyBase>();
        }

        private static StrategyStatus MapServiceStateToStrategyStatus(ServiceState state)
        {
            return state switch
            {
                ServiceState.Created => StrategyStatus.Stopped,
                ServiceState.Initializing => StrategyStatus.Starting,
                ServiceState.Initialized => StrategyStatus.Starting,
                ServiceState.Starting => StrategyStatus.Starting,
                ServiceState.Running => StrategyStatus.Running,
                ServiceState.Stopping => StrategyStatus.Stopping,
                ServiceState.Stopped => StrategyStatus.Stopped,
                ServiceState.Failed => StrategyStatus.Error,
                _ => StrategyStatus.Stopped
            };
        }

        private static Models.SignalType MapSignalType(Core.Canonical.SignalType signalType)
        {
            return signalType switch
            {
                Core.Canonical.SignalType.Buy => Models.SignalType.Buy,
                Core.Canonical.SignalType.Sell => Models.SignalType.Sell,
                Core.Canonical.SignalType.Hold => Models.SignalType.Hold,
                Core.Canonical.SignalType.StopLoss => Models.SignalType.StopLoss,
                Core.Canonical.SignalType.TakeProfit => Models.SignalType.TakeProfit,
                _ => Models.SignalType.Hold
            };
        }

        #endregion
    }

    // Placeholder interfaces for dependencies
    public interface IMarketDataService
    {
        Task<MarketData?> GetLatestDataAsync(string symbol);
    }

    public interface IPortfolioService
    {
        Task<Portfolio> GetPortfolioAsync();
    }
    
    public interface IRiskManagementService
    {
        Task<decimal> GetCurrentRiskExposureAsync();
    }

    public record Portfolio(
        decimal CashBalance,
        decimal BuyingPower,
        decimal TotalEquity,
        decimal UnrealizedPnL,
        decimal RealizedPnL,
        Position[] Positions);

    public record Position(
        string Symbol,
        decimal Quantity,
        decimal AveragePrice,
        decimal CurrentPrice,
        decimal UnrealizedPnL,
        DateTime OpenTime);
}