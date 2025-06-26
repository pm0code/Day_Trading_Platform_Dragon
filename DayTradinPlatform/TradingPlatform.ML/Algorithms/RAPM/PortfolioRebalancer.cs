using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Common;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Canonical;
using TradingPlatform.ML.Common;

namespace TradingPlatform.ML.Algorithms.RAPM
{
    /// <summary>
    /// Portfolio rebalancing service with transaction cost optimization
    /// Based on research showing importance of controlling turnover
    /// </summary>
    public class PortfolioRebalancer : CanonicalServiceBase, IPortfolioRebalancer
    {
        private readonly IMarketDataService _marketDataService;
        private readonly IOrderManagementService _orderManagement;
        private readonly RiskMeasures _riskMeasures;

        public PortfolioRebalancer(
            IMarketDataService marketDataService,
            IOrderManagementService orderManagement,
            RiskMeasures riskMeasures,
            ICanonicalLogger logger)
            : base(logger)
        {
            _marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));
            _orderManagement = orderManagement ?? throw new ArgumentNullException(nameof(orderManagement));
            _riskMeasures = riskMeasures ?? throw new ArgumentNullException(nameof(riskMeasures));
        }

        protected override Task<TradingResult> OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            LogMethodExit();
            return Task.FromResult(TradingResult.Success());
        }

        protected override Task<TradingResult> OnStartAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            LogMethodExit();
            return Task.FromResult(TradingResult.Success());
        }

        protected override Task<TradingResult> OnStopAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            LogMethodExit();
            return Task.FromResult(TradingResult.Success());
        }

        /// <summary>
        /// Determine if rebalancing is needed based on drift, risk limits, or regime change
        /// </summary>
        public async Task<TradingResult<RebalanceDecision>> ShouldRebalanceAsync(
            Portfolio currentPortfolio,
            Dictionary<string, float> targetWeights,
            MarketContext marketContext,
            RebalanceConfiguration config = null,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            
            try
            {
                config ??= new RebalanceConfiguration();
                var decision = new RebalanceDecision
                {
                    ShouldRebalance = false,
                    Reasons = new List<string>(),
                    Urgency = RebalanceUrgency.Low
                };

                // Check time-based trigger
                if (currentPortfolio.LastRebalanceDate.HasValue)
                {
                    var daysSinceRebalance = (DateTime.UtcNow - currentPortfolio.LastRebalanceDate.Value).Days;
                    if (daysSinceRebalance >= config.MinRebalanceDays)
                    {
                        decision.TimeBasedTrigger = true;
                        decision.Reasons.Add($"Time trigger: {daysSinceRebalance} days since last rebalance");
                    }
                }

                // Check drift from target weights
                var driftResult = await CalculatePortfolioDriftAsync(
                    currentPortfolio,
                    targetWeights,
                    cancellationToken);

                if (driftResult.IsSuccess && driftResult.Data.MaxDrift > config.DriftThreshold)
                {
                    decision.DriftTrigger = true;
                    decision.Reasons.Add($"Drift trigger: Max drift {driftResult.Data.MaxDrift:P} exceeds threshold");
                    decision.Urgency = RebalanceUrgency.Medium;
                }

                // Check risk limits
                var riskCheckResult = await CheckRiskLimitsAsync(
                    currentPortfolio,
                    marketContext,
                    config,
                    cancellationToken);

                if (riskCheckResult.IsSuccess && riskCheckResult.Data.RiskLimitBreached)
                {
                    decision.RiskTrigger = true;
                    decision.Reasons.Add($"Risk trigger: {riskCheckResult.Data.BreachedMetric} exceeds limit");
                    decision.Urgency = RebalanceUrgency.High;
                }

                // Check regime change
                if (currentPortfolio.LastMarketRegime != marketContext.MarketRegime)
                {
                    decision.RegimeChangeTrigger = true;
                    decision.Reasons.Add($"Regime change: {currentPortfolio.LastMarketRegime} → {marketContext.MarketRegime}");
                    
                    if (marketContext.MarketRegime == MarketRegime.Crisis)
                    {
                        decision.Urgency = RebalanceUrgency.Critical;
                    }
                }

                // Final decision
                decision.ShouldRebalance = decision.TimeBasedTrigger || 
                                         decision.DriftTrigger || 
                                         decision.RiskTrigger || 
                                         decision.RegimeChangeTrigger;

                LogMethodExit();
                return TradingResult<RebalanceDecision>.Success(decision);
            }
            catch (Exception ex)
            {
                LogError("Error evaluating rebalance decision", ex);
                return TradingResult<RebalanceDecision>.Failure($"Failed to evaluate rebalance: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculate optimal rebalancing trades minimizing transaction costs
        /// </summary>
        public async Task<TradingResult<RebalancePlan>> CalculateRebalancePlanAsync(
            Portfolio currentPortfolio,
            Dictionary<string, float> targetWeights,
            RebalanceConfiguration config = null,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            
            try
            {
                config ??= new RebalanceConfiguration();
                var plan = new RebalancePlan
                {
                    Timestamp = DateTime.UtcNow,
                    Trades = new List<RebalanceTrade>(),
                    EstimatedCosts = new TransactionCostEstimate()
                };

                // Get current market prices
                var pricesResult = await GetCurrentPricesAsync(
                    currentPortfolio.Holdings.Keys.Union(targetWeights.Keys).ToList(),
                    cancellationToken);

                if (!pricesResult.IsSuccess)
                {
                    return TradingResult<RebalancePlan>.Failure(pricesResult.ErrorMessage);
                }

                var prices = pricesResult.Data;

                // Calculate current weights
                var currentWeights = CalculateCurrentWeights(currentPortfolio, prices);

                // Optimize rebalancing with transaction costs
                var optimizedTrades = OptimizeRebalancingTrades(
                    currentWeights,
                    targetWeights,
                    prices,
                    currentPortfolio.TotalValue,
                    config);

                plan.Trades = optimizedTrades;

                // Estimate transaction costs
                plan.EstimatedCosts = EstimateTransactionCosts(optimizedTrades, config);

                // Calculate expected improvement
                plan.ExpectedImprovement = CalculateExpectedImprovement(
                    currentWeights,
                    targetWeights,
                    plan.EstimatedCosts.TotalCost,
                    currentPortfolio.TotalValue);

                // Set recommended action
                if (plan.ExpectedImprovement > config.MinImprovementThreshold)
                {
                    plan.RecommendedAction = RebalanceAction.FullRebalance;
                }
                else if (plan.Trades.Any(t => Math.Abs(t.WeightChange) > config.PartialRebalanceThreshold))
                {
                    plan.RecommendedAction = RebalanceAction.PartialRebalance;
                }
                else
                {
                    plan.RecommendedAction = RebalanceAction.NoAction;
                }

                LogMethodExit();
                return TradingResult<RebalancePlan>.Success(plan);
            }
            catch (Exception ex)
            {
                LogError("Error calculating rebalance plan", ex);
                return TradingResult<RebalancePlan>.Failure($"Failed to calculate plan: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute rebalancing trades with smart order routing
        /// </summary>
        public async Task<TradingResult<RebalanceExecutionResult>> ExecuteRebalanceAsync(
            RebalancePlan plan,
            ExecutionConfiguration config = null,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            
            try
            {
                config ??= new ExecutionConfiguration();
                var result = new RebalanceExecutionResult
                {
                    StartTime = DateTime.UtcNow,
                    ExecutedTrades = new List<ExecutedTrade>(),
                    Status = RebalanceStatus.InProgress
                };

                // Sort trades by priority (sells first, then buys)
                var sortedTrades = plan.Trades
                    .OrderBy(t => t.TradeType == TradeType.Buy ? 1 : 0)
                    .ThenByDescending(t => Math.Abs(t.WeightChange))
                    .ToList();

                // Execute trades with error handling
                foreach (var trade in sortedTrades)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.Status = RebalanceStatus.Cancelled;
                        break;
                    }

                    var executionResult = await ExecuteSingleTradeAsync(trade, config, cancellationToken);
                    
                    if (executionResult.IsSuccess)
                    {
                        result.ExecutedTrades.Add(executionResult.Data);
                    }
                    else
                    {
                        LogWarning($"Failed to execute trade for {trade.Symbol}: {executionResult.ErrorMessage}");
                        
                        if (config.StopOnError)
                        {
                            result.Status = RebalanceStatus.Failed;
                            result.ErrorMessage = executionResult.ErrorMessage;
                            break;
                        }
                    }
                }

                // Calculate actual costs
                result.ActualCosts = CalculateActualCosts(result.ExecutedTrades);
                result.EndTime = DateTime.UtcNow;
                
                if (result.Status == RebalanceStatus.InProgress)
                {
                    result.Status = result.ExecutedTrades.Count == plan.Trades.Count 
                        ? RebalanceStatus.Completed 
                        : RebalanceStatus.PartiallyCompleted;
                }

                LogMethodExit();
                return TradingResult<RebalanceExecutionResult>.Success(result);
            }
            catch (Exception ex)
            {
                LogError("Error executing rebalance", ex);
                return TradingResult<RebalanceExecutionResult>.Failure($"Failed to execute rebalance: {ex.Message}");
            }
        }

        /// <summary>
        /// Dynamic rebalancing based on market conditions
        /// Research shows adaptive approaches outperform fixed schedules
        /// </summary>
        public async Task<TradingResult<DynamicRebalanceStrategy>> GetDynamicStrategyAsync(
            Portfolio portfolio,
            MarketContext marketContext,
            HistoricalPerformance performance,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            
            try
            {
                var strategy = new DynamicRebalanceStrategy
                {
                    Timestamp = DateTime.UtcNow,
                    MarketRegime = marketContext.MarketRegime
                };

                // Adjust parameters based on market regime
                switch (marketContext.MarketRegime)
                {
                    case MarketRegime.Crisis:
                        strategy.RebalanceFrequency = RebalanceFrequency.Daily;
                        strategy.DriftThreshold = 0.02f; // 2% - tighter control
                        strategy.TransactionCostSensitivity = 0.5f; // Less sensitive to costs
                        break;
                        
                    case MarketRegime.Volatile:
                        strategy.RebalanceFrequency = RebalanceFrequency.Weekly;
                        strategy.DriftThreshold = 0.05f; // 5%
                        strategy.TransactionCostSensitivity = 0.7f;
                        break;
                        
                    case MarketRegime.Stable:
                        strategy.RebalanceFrequency = RebalanceFrequency.Monthly;
                        strategy.DriftThreshold = 0.10f; // 10% - allow more drift
                        strategy.TransactionCostSensitivity = 1.0f; // Cost sensitive
                        break;
                        
                    default:
                        strategy.RebalanceFrequency = RebalanceFrequency.BiWeekly;
                        strategy.DriftThreshold = 0.07f;
                        strategy.TransactionCostSensitivity = 0.8f;
                        break;
                }

                // Adjust based on recent performance
                if (performance.RecentSharpeRatio < 0.5f)
                {
                    // Poor performance - rebalance more frequently
                    strategy.RebalanceFrequency = IncreaseFrequency(strategy.RebalanceFrequency);
                    strategy.DriftThreshold *= 0.8f;
                }

                // Volatility-based adjustment
                if (marketContext.MarketVolatility > 0.25f)
                {
                    strategy.UseVolatilityTargeting = true;
                    strategy.TargetVolatility = 0.12f; // 12% target in high vol environments
                }

                LogMethodExit();
                return TradingResult<DynamicRebalanceStrategy>.Success(strategy);
            }
            catch (Exception ex)
            {
                LogError("Error determining dynamic strategy", ex);
                return TradingResult<DynamicRebalanceStrategy>.Failure($"Failed to determine strategy: {ex.Message}");
            }
        }

        // Helper methods

        private async Task<TradingResult<DriftAnalysis>> CalculatePortfolioDriftAsync(
            Portfolio portfolio,
            Dictionary<string, float> targetWeights,
            CancellationToken cancellationToken)
        {
            var drift = new DriftAnalysis
            {
                AssetDrifts = new Dictionary<string, float>()
            };

            // Get current prices
            var pricesResult = await GetCurrentPricesAsync(portfolio.Holdings.Keys.ToList(), cancellationToken);
            if (!pricesResult.IsSuccess)
            {
                return TradingResult<DriftAnalysis>.Failure(pricesResult.ErrorMessage);
            }

            var currentWeights = CalculateCurrentWeights(portfolio, pricesResult.Data);

            // Calculate drift for each asset
            foreach (var target in targetWeights)
            {
                float currentWeight = currentWeights.GetValueOrDefault(target.Key, 0);
                float driftAmount = Math.Abs(currentWeight - target.Value);
                drift.AssetDrifts[target.Key] = driftAmount;
            }

            drift.MaxDrift = drift.AssetDrifts.Values.Max();
            drift.TotalDrift = drift.AssetDrifts.Values.Sum();

            return TradingResult<DriftAnalysis>.Success(drift);
        }

        private async Task<TradingResult<RiskLimitCheck>> CheckRiskLimitsAsync(
            Portfolio portfolio,
            MarketContext marketContext,
            RebalanceConfiguration config,
            CancellationToken cancellationToken)
        {
            var check = new RiskLimitCheck();

            // Calculate current portfolio risk metrics
            var returns = await GetPortfolioReturnsAsync(portfolio, cancellationToken);
            if (!returns.IsSuccess)
            {
                return TradingResult<RiskLimitCheck>.Failure(returns.ErrorMessage);
            }

            // Check VaR limit
            var varResult = _riskMeasures.CalculateVaR(returns.Data, 0.95f);
            if (varResult.IsSuccess && varResult.Data > config.MaxVaR)
            {
                check.RiskLimitBreached = true;
                check.BreachedMetric = "VaR";
                check.CurrentValue = varResult.Data;
                check.Limit = config.MaxVaR;
            }

            // Check volatility limit
            float volatility = CalculateVolatility(returns.Data);
            if (volatility > config.MaxVolatility)
            {
                check.RiskLimitBreached = true;
                check.BreachedMetric = "Volatility";
                check.CurrentValue = volatility;
                check.Limit = config.MaxVolatility;
            }

            return TradingResult<RiskLimitCheck>.Success(check);
        }

        private Dictionary<string, float> CalculateCurrentWeights(
            Portfolio portfolio,
            Dictionary<string, decimal> prices)
        {
            var weights = new Dictionary<string, float>();
            decimal totalValue = 0;

            // Calculate total portfolio value
            foreach (var holding in portfolio.Holdings)
            {
                if (prices.TryGetValue(holding.Key, out var price))
                {
                    totalValue += holding.Value.Shares * price;
                }
            }

            // Calculate weights
            foreach (var holding in portfolio.Holdings)
            {
                if (prices.TryGetValue(holding.Key, out var price))
                {
                    decimal value = holding.Value.Shares * price;
                    weights[holding.Key] = (float)(value / totalValue);
                }
            }

            return weights;
        }

        private List<RebalanceTrade> OptimizeRebalancingTrades(
            Dictionary<string, float> currentWeights,
            Dictionary<string, float> targetWeights,
            Dictionary<string, decimal> prices,
            decimal portfolioValue,
            RebalanceConfiguration config)
        {
            var trades = new List<RebalanceTrade>();

            // Calculate weight changes
            var allSymbols = currentWeights.Keys.Union(targetWeights.Keys).ToList();
            
            foreach (var symbol in allSymbols)
            {
                float currentWeight = currentWeights.GetValueOrDefault(symbol, 0);
                float targetWeight = targetWeights.GetValueOrDefault(symbol, 0);
                float weightChange = targetWeight - currentWeight;

                // Apply minimum trade threshold to reduce small trades
                if (Math.Abs(weightChange) < config.MinTradeThreshold)
                {
                    continue;
                }

                // Calculate trade value
                decimal tradeValue = (decimal)weightChange * portfolioValue;
                int shares = (int)(tradeValue / prices[symbol]);

                if (shares != 0)
                {
                    trades.Add(new RebalanceTrade
                    {
                        Symbol = symbol,
                        TradeType = shares > 0 ? TradeType.Buy : TradeType.Sell,
                        Shares = Math.Abs(shares),
                        EstimatedPrice = prices[symbol],
                        CurrentWeight = currentWeight,
                        TargetWeight = targetWeight,
                        WeightChange = weightChange
                    });
                }
            }

            return trades;
        }

        private TransactionCostEstimate EstimateTransactionCosts(
            List<RebalanceTrade> trades,
            RebalanceConfiguration config)
        {
            var estimate = new TransactionCostEstimate();

            foreach (var trade in trades)
            {
                decimal tradeValue = trade.Shares * trade.EstimatedPrice;
                
                // Linear costs (spread)
                decimal spreadCost = tradeValue * config.SpreadCostBps / 10000m;
                estimate.SpreadCost += spreadCost;

                // Market impact (square-root model)
                decimal turnover = (decimal)Math.Abs(trade.WeightChange);
                decimal marketImpact = tradeValue * config.MarketImpactCoefficient * 
                                     (decimal)Math.Sqrt((double)turnover);
                estimate.MarketImpact += marketImpact;

                // Fixed costs
                estimate.CommissionCost += config.CommissionPerTrade;
            }

            estimate.TotalCost = estimate.SpreadCost + estimate.MarketImpact + estimate.CommissionCost;
            return estimate;
        }

        private float CalculateExpectedImprovement(
            Dictionary<string, float> currentWeights,
            Dictionary<string, float> targetWeights,
            decimal transactionCost,
            decimal portfolioValue)
        {
            // Simplified calculation - would use more sophisticated model in practice
            float trackingError = 0;
            
            foreach (var symbol in targetWeights.Keys)
            {
                float current = currentWeights.GetValueOrDefault(symbol, 0);
                float diff = targetWeights[symbol] - current;
                trackingError += diff * diff;
            }
            
            trackingError = (float)Math.Sqrt(trackingError);
            
            // Expected improvement from reducing tracking error minus costs
            float costDrag = (float)(transactionCost / portfolioValue);
            return trackingError * 0.5f - costDrag; // Assume 50% of tracking error translates to return
        }

        private async Task<TradingResult<Dictionary<string, decimal>>> GetCurrentPricesAsync(
            List<string> symbols,
            CancellationToken cancellationToken)
        {
            // In practice, would fetch from market data service
            var prices = new Dictionary<string, decimal>();
            var random = new Random();
            
            foreach (var symbol in symbols)
            {
                prices[symbol] = (decimal)(90 + random.NextDouble() * 20); // $90-$110
            }

            return await Task.FromResult(TradingResult<Dictionary<string, decimal>>.Success(prices));
        }

        private async Task<TradingResult<float[]>> GetPortfolioReturnsAsync(
            Portfolio portfolio,
            CancellationToken cancellationToken)
        {
            // Simplified - would calculate actual portfolio returns
            var returns = new float[252];
            var random = new Random();
            
            for (int i = 0; i < returns.Length; i++)
            {
                returns[i] = (float)(random.NextDouble() * 0.04 - 0.02);
            }

            return await Task.FromResult(TradingResult<float[]>.Success(returns));
        }

        private float CalculateVolatility(float[] returns)
        {
            float mean = returns.Average();
            float variance = returns.Select(r => (r - mean) * (r - mean)).Average();
            return (float)Math.Sqrt(variance) * (float)Math.Sqrt(252); // Annualized
        }

        private async Task<TradingResult<ExecutedTrade>> ExecuteSingleTradeAsync(
            RebalanceTrade trade,
            ExecutionConfiguration config,
            CancellationToken cancellationToken)
        {
            // In practice, would execute through order management system
            var executed = new ExecutedTrade
            {
                Symbol = trade.Symbol,
                TradeType = trade.TradeType,
                RequestedShares = trade.Shares,
                ExecutedShares = trade.Shares,
                ExecutionPrice = trade.EstimatedPrice * (1 + (decimal)(new Random().NextDouble() * 0.001 - 0.0005)),
                ExecutionTime = DateTime.UtcNow,
                Status = ExecutionStatus.Completed
            };

            return await Task.FromResult(TradingResult<ExecutedTrade>.Success(executed));
        }

        private decimal CalculateActualCosts(List<ExecutedTrade> trades)
        {
            decimal totalCost = 0;
            
            foreach (var trade in trades)
            {
                decimal slippage = Math.Abs(trade.ExecutionPrice - trade.ExpectedPrice) * trade.ExecutedShares;
                totalCost += slippage;
            }

            return totalCost;
        }

        private RebalanceFrequency IncreaseFrequency(RebalanceFrequency current)
        {
            return current switch
            {
                RebalanceFrequency.Monthly => RebalanceFrequency.BiWeekly,
                RebalanceFrequency.BiWeekly => RebalanceFrequency.Weekly,
                RebalanceFrequency.Weekly => RebalanceFrequency.Daily,
                _ => current
            };
        }
    }

    // Supporting interfaces and classes

    public interface IPortfolioRebalancer
    {
        Task<TradingResult<RebalanceDecision>> ShouldRebalanceAsync(
            Portfolio currentPortfolio,
            Dictionary<string, float> targetWeights,
            MarketContext marketContext,
            RebalanceConfiguration config = null,
            CancellationToken cancellationToken = default);

        Task<TradingResult<RebalancePlan>> CalculateRebalancePlanAsync(
            Portfolio currentPortfolio,
            Dictionary<string, float> targetWeights,
            RebalanceConfiguration config = null,
            CancellationToken cancellationToken = default);

        Task<TradingResult<RebalanceExecutionResult>> ExecuteRebalanceAsync(
            RebalancePlan plan,
            ExecutionConfiguration config = null,
            CancellationToken cancellationToken = default);

        Task<TradingResult<DynamicRebalanceStrategy>> GetDynamicStrategyAsync(
            Portfolio portfolio,
            MarketContext marketContext,
            HistoricalPerformance performance,
            CancellationToken cancellationToken = default);
    }

    public interface IOrderManagementService
    {
        Task<TradingResult<OrderResult>> SubmitOrderAsync(Order order, CancellationToken cancellationToken);
        Task<TradingResult<OrderStatus>> GetOrderStatusAsync(string orderId, CancellationToken cancellationToken);
        Task<TradingResult> CancelOrderAsync(string orderId, CancellationToken cancellationToken);
    }

    public class RebalanceDecision
    {
        public bool ShouldRebalance { get; set; }
        public List<string> Reasons { get; set; }
        public RebalanceUrgency Urgency { get; set; }
        public bool TimeBasedTrigger { get; set; }
        public bool DriftTrigger { get; set; }
        public bool RiskTrigger { get; set; }
        public bool RegimeChangeTrigger { get; set; }
    }

    public enum RebalanceUrgency
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class RebalanceConfiguration
    {
        // Timing
        public int MinRebalanceDays { get; set; } = 30;
        
        // Drift thresholds
        public float DriftThreshold { get; set; } = 0.05f; // 5%
        public float PartialRebalanceThreshold { get; set; } = 0.03f; // 3%
        
        // Risk limits
        public float MaxVaR { get; set; } = 0.05f; // 5% VaR
        public float MaxVolatility { get; set; } = 0.20f; // 20% annual
        
        // Transaction costs
        public decimal SpreadCostBps { get; set; } = 5; // 5 basis points
        public decimal MarketImpactCoefficient { get; set; } = 0.1m;
        public decimal CommissionPerTrade { get; set; } = 1m;
        
        // Optimization
        public float MinTradeThreshold { get; set; } = 0.005f; // 0.5% minimum trade
        public float MinImprovementThreshold { get; set; } = 0.001f; // 0.1% minimum improvement
    }

    public class RebalancePlan
    {
        public DateTime Timestamp { get; set; }
        public List<RebalanceTrade> Trades { get; set; }
        public TransactionCostEstimate EstimatedCosts { get; set; }
        public float ExpectedImprovement { get; set; }
        public RebalanceAction RecommendedAction { get; set; }
    }

    public class RebalanceTrade
    {
        public string Symbol { get; set; }
        public TradeType TradeType { get; set; }
        public int Shares { get; set; }
        public decimal EstimatedPrice { get; set; }
        public float CurrentWeight { get; set; }
        public float TargetWeight { get; set; }
        public float WeightChange { get; set; }
    }

    public enum TradeType
    {
        Buy,
        Sell
    }

    public enum RebalanceAction
    {
        NoAction,
        PartialRebalance,
        FullRebalance
    }

    public class TransactionCostEstimate
    {
        public decimal SpreadCost { get; set; }
        public decimal MarketImpact { get; set; }
        public decimal CommissionCost { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class ExecutionConfiguration
    {
        public bool UseSmartRouting { get; set; } = true;
        public bool StopOnError { get; set; } = false;
        public int MaxRetries { get; set; } = 3;
        public TimeSpan OrderTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }

    public class RebalanceExecutionResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<ExecutedTrade> ExecutedTrades { get; set; }
        public decimal ActualCosts { get; set; }
        public RebalanceStatus Status { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ExecutedTrade
    {
        public string Symbol { get; set; }
        public TradeType TradeType { get; set; }
        public int RequestedShares { get; set; }
        public int ExecutedShares { get; set; }
        public decimal ExpectedPrice { get; set; }
        public decimal ExecutionPrice { get; set; }
        public DateTime ExecutionTime { get; set; }
        public ExecutionStatus Status { get; set; }
    }

    public enum RebalanceStatus
    {
        Pending,
        InProgress,
        Completed,
        PartiallyCompleted,
        Failed,
        Cancelled
    }

    public enum ExecutionStatus
    {
        Pending,
        Submitted,
        PartiallyFilled,
        Completed,
        Rejected,
        Cancelled
    }

    public class DynamicRebalanceStrategy
    {
        public DateTime Timestamp { get; set; }
        public RebalanceFrequency RebalanceFrequency { get; set; }
        public float DriftThreshold { get; set; }
        public float TransactionCostSensitivity { get; set; }
        public bool UseVolatilityTargeting { get; set; }
        public float TargetVolatility { get; set; }
        public MarketRegime MarketRegime { get; set; }
    }

    public enum RebalanceFrequency
    {
        Daily,
        Weekly,
        BiWeekly,
        Monthly,
        Quarterly
    }

    public class DriftAnalysis
    {
        public Dictionary<string, float> AssetDrifts { get; set; }
        public float MaxDrift { get; set; }
        public float TotalDrift { get; set; }
    }

    public class RiskLimitCheck
    {
        public bool RiskLimitBreached { get; set; }
        public string BreachedMetric { get; set; }
        public float CurrentValue { get; set; }
        public float Limit { get; set; }
    }

    public class HistoricalPerformance
    {
        public float RecentSharpeRatio { get; set; }
        public float RecentVolatility { get; set; }
        public decimal RecentMaxDrawdown { get; set; }
        public int DaysSinceLastRebalance { get; set; }
    }

    public class Order
    {
        public string Symbol { get; set; }
        public OrderType Type { get; set; }
        public int Quantity { get; set; }
        public decimal? LimitPrice { get; set; }
        public TimeInForce TimeInForce { get; set; }
    }

    public enum OrderType
    {
        Market,
        Limit,
        Stop,
        StopLimit
    }

    public enum TimeInForce
    {
        Day,
        GTC,
        IOC,
        FOK
    }

    public class OrderResult
    {
        public string OrderId { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime SubmittedAt { get; set; }
    }

    public class OrderStatus
    {
        public string OrderId { get; set; }
        public ExecutionStatus Status { get; set; }
        public int FilledQuantity { get; set; }
        public decimal AveragePrice { get; set; }
    }
}