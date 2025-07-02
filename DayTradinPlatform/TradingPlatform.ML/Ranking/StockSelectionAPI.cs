using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Common;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.ML.Common;
using TradingPlatform.ML.Interfaces;

namespace TradingPlatform.ML.Ranking
{
    public class StockSelectionAPI : CanonicalServiceBase
    {
        private readonly IRankingScoreCalculator _scoreCalculator;
        private readonly IMarketDataService _marketDataService;
        private readonly IScreeningEngine _screeningEngine;
        private readonly IModelPerformanceMonitor _performanceMonitor;
        private readonly Dictionary<string, SelectionStrategy> _strategies;

        public StockSelectionAPI(
            IRankingScoreCalculator scoreCalculator,
            IMarketDataService marketDataService,
            IScreeningEngine screeningEngine,
            IModelPerformanceMonitor performanceMonitor,
            ILogger<StockSelectionAPI> logger)
            : base(logger)
        {
            _scoreCalculator = scoreCalculator ?? throw new ArgumentNullException(nameof(scoreCalculator));
            _marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));
            _screeningEngine = screeningEngine ?? throw new ArgumentNullException(nameof(screeningEngine));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _strategies = new Dictionary<string, SelectionStrategy>();
            
            RegisterDefaultStrategies();
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

        public async Task<TradingResult<StockSelectionResult>> SelectTopStocksAsync(
            SelectionCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            
            try
            {
                // Get universe of stocks
                var universeResult = await GetStockUniverseAsync(criteria, cancellationToken);
                if (!universeResult.IsSuccess)
                {
                    return TradingResult<StockSelectionResult>.Failure(universeResult.ErrorMessage);
                }

                var stockUniverse = universeResult.Data;
                _logger.LogInformation($"Stock universe contains {stockUniverse.Count} symbols");

                // Get market context
                var marketContext = await GetMarketContextAsync(cancellationToken);

                // Prepare ranking data for each stock
                var rankingDataTasks = stockUniverse.Select(async symbol =>
                {
                    var dataResult = await PrepareStockRankingDataAsync(symbol, cancellationToken);
                    return dataResult.IsSuccess ? dataResult.Data : null;
                }).ToList();

                var rankingDataResults = await Task.WhenAll(rankingDataTasks);
                var validRankingData = rankingDataResults.Where(d => d != null).ToList();

                _logger.LogInformation($"Prepared ranking data for {validRankingData.Count} stocks");

                // Apply pre-ranking filters
                if (criteria.PreFilters != null)
                {
                    validRankingData = ApplyPreFilters(validRankingData, criteria.PreFilters);
                    _logger.LogInformation($"After pre-filtering: {validRankingData.Count} stocks remain");
                }

                // Rank the stocks
                var rankingOptions = new RankingOptions
                {
                    ScoreWeights = criteria.ScoreWeights,
                    Filters = criteria.PostFilters,
                    MaxResults = criteria.TopN * 2 // Get extra for post-filtering
                };

                var rankingResult = await _scoreCalculator.RankStocksAsync(
                    validRankingData,
                    marketContext,
                    rankingOptions,
                    cancellationToken);

                if (!rankingResult.IsSuccess)
                {
                    return TradingResult<StockSelectionResult>.Failure(rankingResult.ErrorMessage);
                }

                var rankedStocks = rankingResult.Data;

                // Apply selection strategy
                var strategy = GetStrategy(criteria.Strategy);
                var selectedStocks = await strategy.SelectStocksAsync(
                    rankedStocks,
                    criteria,
                    marketContext,
                    cancellationToken);

                // Limit to TopN
                if (selectedStocks.Count > criteria.TopN)
                {
                    selectedStocks = selectedStocks.Take(criteria.TopN).ToList();
                }

                // Build selection result
                var result = new StockSelectionResult
                {
                    Timestamp = DateTime.UtcNow,
                    SelectedStocks = selectedStocks.Select(s => new SelectedStock
                    {
                        Symbol = s.Symbol,
                        Rank = s.Rank,
                        Score = s.Score.CompositeScore,
                        Confidence = s.Score.Confidence,
                        Factors = s.Score.FactorContributions,
                        Metadata = new Dictionary<string, object>
                        {
                            ["Strategy"] = criteria.Strategy,
                            ["MarketRegime"] = marketContext.MarketRegime.ToString(),
                            ["AdjustedScore"] = s.Score.AdjustedScore
                        }
                    }).ToList(),
                    MarketContext = marketContext,
                    SelectionCriteria = criteria,
                    Statistics = CalculateSelectionStatistics(selectedStocks)
                };

                // Track performance
                await TrackSelectionPerformanceAsync(result, cancellationToken);

                LogMethodExit();
                return TradingResult<StockSelectionResult>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting top stocks");
                return TradingResult<StockSelectionResult>.Failure($"Failed to select stocks: {ex.Message}");
            }
        }

        public async Task<TradingResult<RebalanceRecommendation>> GetRebalanceRecommendationAsync(
            Portfolio currentPortfolio,
            SelectionCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            
            try
            {
                // Get new top stocks
                var selectionResult = await SelectTopStocksAsync(criteria, cancellationToken);
                if (!selectionResult.IsSuccess)
                {
                    return TradingResult<RebalanceRecommendation>.Failure(selectionResult.ErrorMessage);
                }

                var newSelections = selectionResult.Data.SelectedStocks;
                var currentHoldings = currentPortfolio.Holdings.Keys.ToHashSet();

                // Identify actions
                var toAdd = newSelections
                    .Where(s => !currentHoldings.Contains(s.Symbol))
                    .ToList();

                var toRemove = currentHoldings
                    .Where(h => !newSelections.Any(s => s.Symbol == h))
                    .ToList();

                var toReweight = newSelections
                    .Where(s => currentHoldings.Contains(s.Symbol))
                    .ToList();

                // Calculate position sizes
                var positionSizes = CalculatePositionSizes(
                    newSelections,
                    currentPortfolio.TotalValue,
                    criteria.PositionSizing);

                // Build recommendation
                var recommendation = new RebalanceRecommendation
                {
                    Timestamp = DateTime.UtcNow,
                    Actions = new List<RebalanceAction>()
                };

                // Add sell actions
                foreach (var symbol in toRemove)
                {
                    recommendation.Actions.Add(new RebalanceAction
                    {
                        Symbol = symbol,
                        ActionType = RebalanceActionType.Sell,
                        CurrentShares = currentPortfolio.Holdings[symbol].Shares,
                        TargetShares = 0,
                        Reason = "No longer in top selections"
                    });
                }

                // Add buy actions
                foreach (var stock in toAdd)
                {
                    var targetValue = positionSizes[stock.Symbol];
                    var currentPrice = (decimal)stock.Metadata.GetValueOrDefault("CurrentPrice", 0m);
                    var shares = currentPrice > 0 ? (int)(targetValue / currentPrice) : 0;
                    
                    recommendation.Actions.Add(new RebalanceAction
                    {
                        Symbol = stock.Symbol,
                        ActionType = RebalanceActionType.Buy,
                        CurrentShares = 0,
                        TargetShares = shares,
                        TargetValue = targetValue,
                        Score = stock.Score,
                        Reason = $"New top selection (rank {stock.Rank})"
                    });
                }

                // Add reweight actions
                foreach (var stock in toReweight)
                {
                    var currentHolding = currentPortfolio.Holdings[stock.Symbol];
                    var targetValue = positionSizes[stock.Symbol];
                    var currentPrice = (decimal)stock.Metadata.GetValueOrDefault("CurrentPrice", 0m);
                    var targetShares = currentPrice > 0 ? (int)(targetValue / currentPrice) : 0;
                    
                    if (Math.Abs(targetShares - currentHolding.Shares) > 0)
                    {
                        recommendation.Actions.Add(new RebalanceAction
                        {
                            Symbol = stock.Symbol,
                            ActionType = targetShares > currentHolding.Shares 
                                ? RebalanceActionType.Increase 
                                : RebalanceActionType.Decrease,
                            CurrentShares = currentHolding.Shares,
                            TargetShares = targetShares,
                            TargetValue = targetValue,
                            Score = stock.Score,
                            Reason = $"Reweight based on new rank ({stock.Rank})"
                        });
                    }
                }

                recommendation.ExpectedImpact = CalculateExpectedImpact(
                    currentPortfolio,
                    recommendation.Actions);

                LogMethodExit();
                return TradingResult<RebalanceRecommendation>.Success(recommendation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating rebalance recommendation");
                return TradingResult<RebalanceRecommendation>.Failure($"Failed to generate recommendation: {ex.Message}");
            }
        }

        public void RegisterStrategy(string name, SelectionStrategy strategy)
        {
            _strategies[name] = strategy ?? throw new ArgumentNullException(nameof(strategy));
            _logger.LogInformation($"Registered selection strategy: {name}");
        }

        private void RegisterDefaultStrategies()
        {
            // Momentum strategy
            RegisterStrategy("Momentum", new MomentumSelectionStrategy());
            
            // Value strategy
            RegisterStrategy("Value", new ValueSelectionStrategy());
            
            // Quality strategy
            RegisterStrategy("Quality", new QualitySelectionStrategy());
            
            // Multi-factor strategy
            RegisterStrategy("MultiFactor", new MultiFactorSelectionStrategy());
            
            // Risk-adjusted strategy
            RegisterStrategy("RiskAdjusted", new RiskAdjustedSelectionStrategy());
        }

        private SelectionStrategy GetStrategy(string strategyName)
        {
            if (!_strategies.TryGetValue(strategyName, out var strategy))
            {
                _logger.LogWarning($"Strategy '{strategyName}' not found, using default MultiFactor");
                strategy = _strategies["MultiFactor"];
            }
            return strategy;
        }

        private async Task<TradingResult<List<string>>> GetStockUniverseAsync(
            SelectionCriteria criteria,
            CancellationToken cancellationToken)
        {
            try
            {
                // Use screening engine if universe criteria specified
                if (criteria.UniverseCriteria != null)
                {
                    var screeningResult = await _screeningEngine.ScreenStocksAsync(
                        criteria.UniverseCriteria,
                        cancellationToken);
                    
                    if (screeningResult.IsSuccess)
                    {
                        var symbols = screeningResult.Data.Select(s => s.Symbol).ToList();
                        return TradingResult<List<string>>.Success(symbols);
                    }
                }

                // Otherwise use predefined universe
                if (criteria.Universe != null && criteria.Universe.Any())
                {
                    return TradingResult<List<string>>.Success(criteria.Universe);
                }

                // Default to S&P 500 or similar
                var defaultUniverse = await _marketDataService.GetIndexConstituentsAsync("SPX", cancellationToken);
                return TradingResult<List<string>>.Success(defaultUniverse);
            }
            catch (Exception ex)
            {
                return TradingResult<List<string>>.Failure($"Failed to get stock universe: {ex.Message}");
            }
        }

        private async Task<TradingResult<StockRankingData>> PrepareStockRankingDataAsync(
            string symbol,
            CancellationToken cancellationToken)
        {
            try
            {
                // Get market data
                var marketDataTask = _marketDataService.GetLatestQuoteAsync(symbol, cancellationToken);
                var historicalDataTask = _marketDataService.GetHistoricalDataAsync(
                    symbol, 
                    DateTime.UtcNow.AddDays(-90), 
                    DateTime.UtcNow, 
                    cancellationToken);
                var fundamentalDataTask = _marketDataService.GetFundamentalDataAsync(symbol, cancellationToken);

                await Task.WhenAll(marketDataTask, historicalDataTask, fundamentalDataTask);

                var rankingData = new StockRankingData
                {
                    Symbol = symbol,
                    MarketData = marketDataTask.Result,
                    HistoricalData = historicalDataTask.Result,
                    FundamentalData = fundamentalDataTask.Result,
                    Timestamp = DateTime.UtcNow
                };

                return TradingResult<StockRankingData>.Success(rankingData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to prepare ranking data for {symbol}: {ex.Message}");
                return TradingResult<StockRankingData>.Failure(ex.Message);
            }
        }

        private async Task<MarketContext> GetMarketContextAsync(CancellationToken cancellationToken)
        {
            // Get market indicators
            var vixTask = _marketDataService.GetLatestQuoteAsync("VIX", cancellationToken);
            var spxTask = _marketDataService.GetLatestQuoteAsync("SPX", cancellationToken);
            
            await Task.WhenAll(vixTask, spxTask);

            var vix = vixTask.Result?.Price ?? 20m;
            var marketVolatility = vix / 100;

            // Determine market regime
            var marketRegime = marketVolatility switch
            {
                < 0.15m => MarketRegime.Stable,
                < 0.25m => MarketRegime.Normal,
                < 0.35m => MarketRegime.Volatile,
                _ => MarketRegime.Crisis
            };

            return new MarketContext
            {
                Timestamp = DateTime.UtcNow,
                MarketRegime = marketRegime,
                MarketVolatility = marketVolatility,
                MarketTrend = DetermineMarketTrend(spxTask.Result),
                MarketLiquidity = 0.8m, // Would calculate from market breadth
                EconomicIndicators = new Dictionary<string, decimal>()
            };
        }

        private MarketTrend DetermineMarketTrend(Quote spxQuote)
        {
            if (spxQuote == null) return MarketTrend.Neutral;

            var changePercent = spxQuote.ChangePercent;
            
            return changePercent switch
            {
                > 0.5m => MarketTrend.StrongUp,
                > 0 => MarketTrend.Up,
                < -0.5m => MarketTrend.StrongDown,
                < 0 => MarketTrend.Down,
                _ => MarketTrend.Neutral
            };
        }

        private List<StockRankingData> ApplyPreFilters(
            List<StockRankingData> stocks,
            PreSelectionFilters filters)
        {
            var filtered = stocks.AsEnumerable();

            if (filters.MinPrice.HasValue)
            {
                filtered = filtered.Where(s => 
                    s.MarketData?.Price >= filters.MinPrice.Value);
            }

            if (filters.MaxPrice.HasValue)
            {
                filtered = filtered.Where(s => 
                    s.MarketData?.Price <= filters.MaxPrice.Value);
            }

            if (filters.MinAverageVolume.HasValue)
            {
                filtered = filtered.Where(s =>
                {
                    if (s.HistoricalData == null || !s.HistoricalData.Any()) return false;
                    var avgVolume = s.HistoricalData.Average(h => h.Volume);
                    return avgVolume >= filters.MinAverageVolume.Value;
                });
            }

            if (filters.MinMarketCap.HasValue)
            {
                filtered = filtered.Where(s => 
                    s.FundamentalData?.MarketCap >= filters.MinMarketCap.Value);
            }

            if (filters.RequiredExchange != null)
            {
                filtered = filtered.Where(s => 
                    s.MarketData?.Exchange == filters.RequiredExchange);
            }

            return filtered.ToList();
        }

        private Dictionary<string, decimal> CalculatePositionSizes(
            List<SelectedStock> stocks,
            decimal portfolioValue,
            PositionSizingStrategy strategy)
        {
            var sizes = new Dictionary<string, decimal>();
            
            switch (strategy)
            {
                case PositionSizingStrategy.Equal:
                    var equalSize = portfolioValue / stocks.Count;
                    foreach (var stock in stocks)
                    {
                        sizes[stock.Symbol] = equalSize;
                    }
                    break;
                    
                case PositionSizingStrategy.ScoreBased:
                    var totalScore = stocks.Sum(s => s.Score);
                    foreach (var stock in stocks)
                    {
                        sizes[stock.Symbol] = portfolioValue * (stock.Score / totalScore);
                    }
                    break;
                    
                case PositionSizingStrategy.RiskParity:
                    // Would implement risk parity calculation
                    goto case PositionSizingStrategy.Equal;
                    
                case PositionSizingStrategy.KellyCriterion:
                    // Would implement Kelly criterion
                    goto case PositionSizingStrategy.Equal;
            }
            
            return sizes;
        }

        private SelectionStatistics CalculateSelectionStatistics(List<RankedStock> selectedStocks)
        {
            if (!selectedStocks.Any())
            {
                return new SelectionStatistics();
            }

            return new SelectionStatistics
            {
                AverageScore = selectedStocks.Average(s => s.Score.CompositeScore),
                MinScore = selectedStocks.Min(s => s.Score.CompositeScore),
                MaxScore = selectedStocks.Max(s => s.Score.CompositeScore),
                AverageConfidence = selectedStocks.Average(s => s.Score.Confidence),
                ScoreStandardDeviation = CalculateStandardDeviation(
                    selectedStocks.Select(s => s.Score.CompositeScore)),
                SectorDistribution = CalculateSectorDistribution(selectedStocks)
            };
        }

        private decimal CalculateStandardDeviation(IEnumerable<decimal> values)
        {
            var list = values.ToList();
            if (list.Count < 2) return 0;

            var avg = list.Average();
            var sum = list.Sum(v => DecimalMathCanonical.Pow(v - avg, 2));
            return DecimalMathCanonical.Sqrt(sum / (list.Count - 1));
        }

        private Dictionary<string, int> CalculateSectorDistribution(List<RankedStock> stocks)
        {
            return stocks
                .Where(s => s.StockData?.FundamentalData?.Sector != null)
                .GroupBy(s => s.StockData.FundamentalData.Sector)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        private RebalanceImpact CalculateExpectedImpact(
            Portfolio portfolio,
            List<RebalanceAction> actions)
        {
            var totalTurnover = actions.Sum(a => 
                Math.Abs(a.TargetShares - a.CurrentShares) * 
                (decimal)(a.Metadata.GetValueOrDefault("Price", 0m) ?? 0m));

            var turnoverPercent = portfolio.TotalValue > 0 
                ? totalTurnover / portfolio.TotalValue 
                : 0;

            return new RebalanceImpact
            {
                TurnoverPercent = turnoverPercent,
                NumberOfTrades = actions.Count,
                EstimatedCost = totalTurnover * 0.001m, // 10 bps assumption
                ExpectedScoreImprovement = CalculateExpectedScoreImprovement(portfolio, actions)
            };
        }

        private decimal CalculateExpectedScoreImprovement(Portfolio portfolio, List<RebalanceAction> actions)
        {
            // Simplified calculation - would be more sophisticated in practice
            var buyActions = actions.Where(a => a.ActionType == RebalanceActionType.Buy).ToList();
            if (!buyActions.Any()) return 0;

            var avgNewScore = buyActions.Average(a => a.Score);
            return Math.Max(0, avgNewScore - 0.5m); // Assuming 0.5 as baseline
        }

        private async Task TrackSelectionPerformanceAsync(
            StockSelectionResult result,
            CancellationToken cancellationToken)
        {
            try
            {
                await _performanceMonitor.TrackSelectionAsync(
                    result.SelectionCriteria.Strategy,
                    result.SelectedStocks.Count,
                    result.Statistics.AverageScore,
                    result.Statistics.AverageConfidence,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to track selection performance: {ex.Message}");
            }
        }
    }

    // Supporting classes
    public class StockSelectionResult
    {
        public DateTime Timestamp { get; set; }
        public List<SelectedStock> SelectedStocks { get; set; }
        public MarketContext MarketContext { get; set; }
        public SelectionCriteria SelectionCriteria { get; set; }
        public SelectionStatistics Statistics { get; set; }
    }

    public class SelectedStock
    {
        public string Symbol { get; set; }
        public int Rank { get; set; }
        public decimal Score { get; set; }
        public decimal Confidence { get; set; }
        public Dictionary<string, decimal> Factors { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    public class SelectionCriteria
    {
        public string Strategy { get; set; } = "MultiFactor";
        public int TopN { get; set; } = 10;
        public List<string> Universe { get; set; }
        public ScreeningCriteria UniverseCriteria { get; set; }
        public PreSelectionFilters PreFilters { get; set; }
        public RankingFilters PostFilters { get; set; }
        public ScoreWeights ScoreWeights { get; set; }
        public PositionSizingStrategy PositionSizing { get; set; } = PositionSizingStrategy.Equal;
    }

    public class PreSelectionFilters
    {
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public long? MinAverageVolume { get; set; }
        public decimal? MinMarketCap { get; set; }
        public string RequiredExchange { get; set; }
    }

    public enum PositionSizingStrategy
    {
        Equal,
        ScoreBased,
        RiskParity,
        KellyCriterion
    }

    public class SelectionStatistics
    {
        public decimal AverageScore { get; set; }
        public decimal MinScore { get; set; }
        public decimal MaxScore { get; set; }
        public decimal AverageConfidence { get; set; }
        public decimal ScoreStandardDeviation { get; set; }
        public Dictionary<string, int> SectorDistribution { get; set; }
    }

    public class RebalanceRecommendation
    {
        public DateTime Timestamp { get; set; }
        public List<RebalanceAction> Actions { get; set; }
        public RebalanceImpact ExpectedImpact { get; set; }
    }

    public class RebalanceAction
    {
        public string Symbol { get; set; }
        public RebalanceActionType ActionType { get; set; }
        public int CurrentShares { get; set; }
        public int TargetShares { get; set; }
        public decimal TargetValue { get; set; }
        public decimal Score { get; set; }
        public string Reason { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public enum RebalanceActionType
    {
        Buy,
        Sell,
        Increase,
        Decrease
    }

    public class RebalanceImpact
    {
        public decimal TurnoverPercent { get; set; }
        public int NumberOfTrades { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal ExpectedScoreImprovement { get; set; }
    }

    public class Portfolio
    {
        public Dictionary<string, Holding> Holdings { get; set; }
        public decimal TotalValue { get; set; }
        public decimal Cash { get; set; }
    }

    public class Holding
    {
        public string Symbol { get; set; }
        public int Shares { get; set; }
        public decimal AverageCost { get; set; }
        public decimal CurrentValue { get; set; }
    }

    // Selection strategy base class
    public abstract class SelectionStrategy
    {
        public abstract Task<List<RankedStock>> SelectStocksAsync(
            List<RankedStock> rankedStocks,
            SelectionCriteria criteria,
            MarketContext marketContext,
            CancellationToken cancellationToken);
    }

    // Example strategy implementations
    public class MultiFactorSelectionStrategy : SelectionStrategy
    {
        public override Task<List<RankedStock>> SelectStocksAsync(
            List<RankedStock> rankedStocks,
            SelectionCriteria criteria,
            MarketContext marketContext,
            CancellationToken cancellationToken)
        {
            // Simply return top N by composite score
            var selected = rankedStocks
                .OrderByDescending(s => s.Score.CompositeScore)
                .Take(criteria.TopN)
                .ToList();
                
            return Task.FromResult(selected);
        }
    }

    public class MomentumSelectionStrategy : SelectionStrategy
    {
        public override Task<List<RankedStock>> SelectStocksAsync(
            List<RankedStock> rankedStocks,
            SelectionCriteria criteria,
            MarketContext marketContext,
            CancellationToken cancellationToken)
        {
            // Prioritize momentum factors
            var selected = rankedStocks
                .Where(s => s.Score.FactorContributions.GetValueOrDefault("Momentum", 0) > 0.6m)
                .OrderByDescending(s => s.Score.CompositeScore)
                .Take(criteria.TopN)
                .ToList();
                
            return Task.FromResult(selected);
        }
    }

    public class ValueSelectionStrategy : SelectionStrategy
    {
        public override Task<List<RankedStock>> SelectStocksAsync(
            List<RankedStock> rankedStocks,
            SelectionCriteria criteria,
            MarketContext marketContext,
            CancellationToken cancellationToken)
        {
            // Prioritize value factors
            var selected = rankedStocks
                .Where(s => s.Score.FactorContributions.GetValueOrDefault("Value", 0) > 0.7m)
                .OrderByDescending(s => s.Score.CompositeScore)
                .Take(criteria.TopN)
                .ToList();
                
            return Task.FromResult(selected);
        }
    }

    public class QualitySelectionStrategy : SelectionStrategy
    {
        public override Task<List<RankedStock>> SelectStocksAsync(
            List<RankedStock> rankedStocks,
            SelectionCriteria criteria,
            MarketContext marketContext,
            CancellationToken cancellationToken)
        {
            // Prioritize quality factors
            var selected = rankedStocks
                .Where(s => s.Score.FactorContributions.GetValueOrDefault("Quality", 0) > 0.8m)
                .OrderByDescending(s => s.Score.CompositeScore)
                .Take(criteria.TopN)
                .ToList();
                
            return Task.FromResult(selected);
        }
    }

    public class RiskAdjustedSelectionStrategy : SelectionStrategy
    {
        public override Task<List<RankedStock>> SelectStocksAsync(
            List<RankedStock> rankedStocks,
            SelectionCriteria criteria,
            MarketContext marketContext,
            CancellationToken cancellationToken)
        {
            // Adjust for market regime
            var selected = rankedStocks.ToList();
            
            if (marketContext.MarketRegime == MarketRegime.Volatile ||
                marketContext.MarketRegime == MarketRegime.Crisis)
            {
                // In volatile markets, prefer low-risk stocks
                selected = selected
                    .Where(s => s.Score.FactorContributions.GetValueOrDefault("Risk", 0) < 0.3m)
                    .OrderByDescending(s => s.Score.CompositeScore)
                    .ToList();
            }
            else
            {
                // In stable markets, standard selection
                selected = selected
                    .OrderByDescending(s => s.Score.CompositeScore)
                    .ToList();
            }
            
            return Task.FromResult(selected.Take(criteria.TopN).ToList());
        }
    }
}