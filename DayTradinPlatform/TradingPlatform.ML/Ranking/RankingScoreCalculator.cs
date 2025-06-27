using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Common;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.ML.Common;
using TradingPlatform.ML.Interfaces;
using TradingPlatform.ML.Models;

namespace TradingPlatform.ML.Ranking
{
    public class RankingScoreCalculator : CanonicalServiceBase
    {
        private readonly IRandomForestRankingModel _rankingModel;
        private readonly IMultiFactorFramework _factorFramework;
        private readonly IModelPerformanceMonitor _performanceMonitor;
        private readonly ConcurrentDictionary<string, RankingScore> _scoreCache;
        private readonly object _cacheLock = new object();
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
        private DateTime _lastCacheCleanup = DateTime.UtcNow;

        public RankingScoreCalculator(
            IRandomForestRankingModel rankingModel,
            IMultiFactorFramework factorFramework,
            IModelPerformanceMonitor performanceMonitor,
            ILogger<RankingScoreCalculator> logger)
            : base(logger)
        {
            _rankingModel = rankingModel ?? throw new ArgumentNullException(nameof(rankingModel));
            _factorFramework = factorFramework ?? throw new ArgumentNullException(nameof(factorFramework));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _scoreCache = new ConcurrentDictionary<string, RankingScore>();
        }

        protected override Task<TradingResult> OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            _scoreCache.Clear();
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
            _scoreCache.Clear();
            LogMethodExit();
            return Task.FromResult(TradingResult.Success());
        }

        public async Task<TradingResult<RankingScore>> CalculateRankingScoreAsync(
            StockRankingData stockData,
            MarketContext marketContext,
            RankingOptions options = null,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            
            try
            {
                options ??= new RankingOptions();
                
                // Check cache if enabled
                if (options.UseCache)
                {
                    var cacheKey = GenerateCacheKey(stockData.Symbol, marketContext.Timestamp);
                    if (TryGetCachedScore(cacheKey, out var cachedScore))
                    {
                        _logger.LogDebug($"Returning cached ranking score for {stockData.Symbol}");
                        return TradingResult<RankingScore>.Success(cachedScore);
                    }
                }

                // Extract factors
                var factorsResult = await ExtractFactorsAsync(stockData, marketContext, cancellationToken);
                if (!factorsResult.IsSuccess)
                {
                    return TradingResult<RankingScore>.Failure(factorsResult.ErrorMessage);
                }

                var factors = factorsResult.Data;

                // Get model prediction
                var predictionResult = await _rankingModel.PredictAsync(factors, cancellationToken);
                if (!predictionResult.IsSuccess)
                {
                    return TradingResult<RankingScore>.Failure(predictionResult.ErrorMessage);
                }

                // Calculate composite score
                var compositeScore = CalculateCompositeScore(
                    predictionResult.Data,
                    factors,
                    options);

                // Apply market regime adjustments
                var adjustedScore = ApplyMarketRegimeAdjustments(
                    compositeScore,
                    marketContext,
                    options);

                // Calculate confidence metrics
                var confidence = CalculateConfidenceMetrics(
                    factors,
                    predictionResult.Data,
                    marketContext);

                var rankingScore = new RankingScore
                {
                    Symbol = stockData.Symbol,
                    Timestamp = marketContext.Timestamp,
                    RawScore = predictionResult.Data.Score,
                    AdjustedScore = adjustedScore,
                    CompositeScore = compositeScore,
                    Confidence = confidence,
                    FactorContributions = CalculateFactorContributions(factors, predictionResult.Data),
                    MarketRegime = marketContext.MarketRegime,
                    Metadata = new Dictionary<string, object>
                    {
                        ["VolatilityAdjustment"] = marketContext.MarketVolatility,
                        ["LiquidityScore"] = factors.MicrostructureFactors.LiquidityScore,
                        ["MomentumStrength"] = factors.TechnicalFactors.MomentumScore,
                        ["QualityScore"] = factors.QualityFactors.CompositeQuality
                    }
                };

                // Cache the result
                if (options.UseCache)
                {
                    var cacheKey = GenerateCacheKey(stockData.Symbol, marketContext.Timestamp);
                    CacheScore(cacheKey, rankingScore);
                }

                // Track performance
                await _performanceMonitor.TrackPredictionAsync(
                    "RankingScore",
                    stockData.Symbol,
                    rankingScore.CompositeScore,
                    confidence,
                    cancellationToken);

                LogMethodExit();
                return TradingResult<RankingScore>.Success(rankingScore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating ranking score for {stockData.Symbol}");
                return TradingResult<RankingScore>.Failure($"Failed to calculate ranking score: {ex.Message}");
            }
        }

        public async Task<TradingResult<List<RankedStock>>> RankStocksAsync(
            IEnumerable<StockRankingData> stocks,
            MarketContext marketContext,
            RankingOptions options = null,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            
            try
            {
                options ??= new RankingOptions();
                var rankedStocks = new ConcurrentBag<RankedStock>();
                
                // Process stocks in parallel
                var tasks = stocks.Select(async stock =>
                {
                    var scoreResult = await CalculateRankingScoreAsync(
                        stock,
                        marketContext,
                        options,
                        cancellationToken);
                    
                    if (scoreResult.IsSuccess)
                    {
                        rankedStocks.Add(new RankedStock
                        {
                            Symbol = stock.Symbol,
                            Score = scoreResult.Data,
                            StockData = stock
                        });
                    }
                    else
                    {
                        _logger.LogWarning($"Failed to calculate score for {stock.Symbol}: {scoreResult.ErrorMessage}");
                    }
                });

                await Task.WhenAll(tasks);

                // Sort by composite score
                var sortedStocks = rankedStocks
                    .OrderByDescending(s => s.Score.CompositeScore)
                    .ToList();

                // Apply rank normalization
                NormalizeRanks(sortedStocks);

                // Apply filters if specified
                if (options.Filters != null)
                {
                    sortedStocks = ApplyFilters(sortedStocks, options.Filters);
                }

                // Limit results if specified
                if (options.MaxResults > 0 && sortedStocks.Count > options.MaxResults)
                {
                    sortedStocks = sortedStocks.Take(options.MaxResults).ToList();
                }

                LogMethodExit();
                return TradingResult<List<RankedStock>>.Success(sortedStocks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ranking stocks");
                return TradingResult<List<RankedStock>>.Failure($"Failed to rank stocks: {ex.Message}");
            }
        }

        private async Task<TradingResult<RankingFactors>> ExtractFactorsAsync(
            StockRankingData stockData,
            MarketContext marketContext,
            CancellationToken cancellationToken)
        {
            LogMethodEntry();
            
            try
            {
                var extractionOptions = new FactorExtractionOptions
                {
                    IncludeSentiment = true,
                    IncludeOptionFlow = true,
                    IncludeMarketMicrostructure = true
                };

                var factors = _factorFramework.ExtractFactors(
                    stockData,
                    marketContext,
                    extractionOptions);

                LogMethodExit();
                return await Task.FromResult(TradingResult<RankingFactors>.Success(factors));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting factors for {stockData.Symbol}");
                return TradingResult<RankingFactors>.Failure($"Failed to extract factors: {ex.Message}");
            }
        }

        private decimal CalculateCompositeScore(
            RankingPrediction prediction,
            RankingFactors factors,
            RankingOptions options)
        {
            // Weight different components
            var weights = options.ScoreWeights ?? new ScoreWeights
            {
                ModelPrediction = 0.4m,
                TechnicalFactors = 0.2m,
                FundamentalFactors = 0.15m,
                SentimentFactors = 0.1m,
                MicrostructureFactors = 0.1m,
                QualityFactors = 0.05m
            };

            var composite = 
                prediction.Score * weights.ModelPrediction +
                factors.TechnicalFactors.CompositeScore * weights.TechnicalFactors +
                factors.FundamentalFactors.CompositeScore * weights.FundamentalFactors +
                factors.SentimentFactors.CompositeScore * weights.SentimentFactors +
                factors.MicrostructureFactors.CompositeScore * weights.MicrostructureFactors +
                factors.QualityFactors.CompositeQuality * weights.QualityFactors;

            return Math.Max(0, Math.Min(1, composite));
        }

        private decimal ApplyMarketRegimeAdjustments(
            decimal score,
            MarketContext marketContext,
            RankingOptions options)
        {
            var adjustedScore = score;

            // Volatility adjustment
            if (marketContext.MarketVolatility > 0.3m)
            {
                adjustedScore *= (1 - (marketContext.MarketVolatility - 0.3m) * 0.5m);
            }

            // Trend adjustment
            switch (marketContext.MarketRegime)
            {
                case MarketRegime.Bullish:
                    adjustedScore *= 1.1m;
                    break;
                case MarketRegime.Bearish:
                    adjustedScore *= 0.9m;
                    break;
                case MarketRegime.Volatile:
                    adjustedScore *= 0.85m;
                    break;
            }

            // Liquidity adjustment
            if (marketContext.MarketLiquidity < 0.3m)
            {
                adjustedScore *= 0.8m;
            }

            return Math.Max(0, Math.Min(1, adjustedScore));
        }

        private decimal CalculateConfidenceMetrics(
            RankingFactors factors,
            RankingPrediction prediction,
            MarketContext marketContext)
        {
            var confidence = 1.0m;

            // Factor completeness
            var factorCompleteness = CalculateFactorCompleteness(factors);
            confidence *= factorCompleteness;

            // Model confidence from prediction
            if (prediction.Confidence.HasValue)
            {
                confidence *= prediction.Confidence.Value;
            }

            // Market regime confidence
            switch (marketContext.MarketRegime)
            {
                case MarketRegime.Stable:
                    confidence *= 1.0m;
                    break;
                case MarketRegime.Volatile:
                    confidence *= 0.7m;
                    break;
                case MarketRegime.Crisis:
                    confidence *= 0.5m;
                    break;
            }

            // Data quality adjustment
            if (factors.DataQuality < 0.8m)
            {
                confidence *= factors.DataQuality;
            }

            return Math.Max(0, Math.Min(1, confidence));
        }

        private decimal CalculateFactorCompleteness(RankingFactors factors)
        {
            var completenessScores = new List<decimal>();

            // Check each factor category
            if (factors.TechnicalFactors != null)
                completenessScores.Add(factors.TechnicalFactors.DataCompleteness);
            
            if (factors.FundamentalFactors != null)
                completenessScores.Add(factors.FundamentalFactors.DataCompleteness);
            
            if (factors.SentimentFactors != null)
                completenessScores.Add(factors.SentimentFactors.DataCompleteness);
            
            if (factors.MicrostructureFactors != null)
                completenessScores.Add(factors.MicrostructureFactors.DataCompleteness);
            
            if (factors.QualityFactors != null)
                completenessScores.Add(factors.QualityFactors.DataCompleteness);

            return completenessScores.Any() ? completenessScores.Average() : 0.5m;
        }

        private Dictionary<string, decimal> CalculateFactorContributions(
            RankingFactors factors,
            RankingPrediction prediction)
        {
            var contributions = new Dictionary<string, decimal>();

            // Get feature importances if available
            if (prediction.FeatureImportances != null && prediction.FeatureImportances.Any())
            {
                foreach (var importance in prediction.FeatureImportances)
                {
                    contributions[importance.Key] = importance.Value;
                }
            }
            else
            {
                // Use default contributions based on factor scores
                contributions["Technical"] = factors.TechnicalFactors.CompositeScore * 0.25m;
                contributions["Fundamental"] = factors.FundamentalFactors.CompositeScore * 0.20m;
                contributions["Sentiment"] = factors.SentimentFactors.CompositeScore * 0.20m;
                contributions["Microstructure"] = factors.MicrostructureFactors.CompositeScore * 0.20m;
                contributions["Quality"] = factors.QualityFactors.CompositeQuality * 0.15m;
            }

            return contributions;
        }

        private void NormalizeRanks(List<RankedStock> stocks)
        {
            if (!stocks.Any()) return;

            var maxScore = stocks.Max(s => s.Score.CompositeScore);
            var minScore = stocks.Min(s => s.Score.CompositeScore);
            var range = maxScore - minScore;

            if (range > 0)
            {
                for (int i = 0; i < stocks.Count; i++)
                {
                    stocks[i].Rank = i + 1;
                    stocks[i].PercentileRank = (decimal)(stocks.Count - i) / stocks.Count;
                    stocks[i].NormalizedScore = (stocks[i].Score.CompositeScore - minScore) / range;
                }
            }
            else
            {
                // All scores are the same
                for (int i = 0; i < stocks.Count; i++)
                {
                    stocks[i].Rank = 1;
                    stocks[i].PercentileRank = 0.5m;
                    stocks[i].NormalizedScore = 0.5m;
                }
            }
        }

        private List<RankedStock> ApplyFilters(List<RankedStock> stocks, RankingFilters filters)
        {
            var filtered = stocks.AsEnumerable();

            if (filters.MinScore.HasValue)
            {
                filtered = filtered.Where(s => s.Score.CompositeScore >= filters.MinScore.Value);
            }

            if (filters.MinConfidence.HasValue)
            {
                filtered = filtered.Where(s => s.Score.Confidence >= filters.MinConfidence.Value);
            }

            if (filters.RequiredSectors != null && filters.RequiredSectors.Any())
            {
                filtered = filtered.Where(s => 
                    s.StockData.FundamentalData != null && 
                    filters.RequiredSectors.Contains(s.StockData.FundamentalData.Sector));
            }

            if (filters.ExcludedSectors != null && filters.ExcludedSectors.Any())
            {
                filtered = filtered.Where(s => 
                    s.StockData.FundamentalData == null || 
                    !filters.ExcludedSectors.Contains(s.StockData.FundamentalData.Sector));
            }

            if (filters.MinMarketCap.HasValue)
            {
                filtered = filtered.Where(s => 
                    s.StockData.FundamentalData != null && 
                    s.StockData.FundamentalData.MarketCap >= filters.MinMarketCap.Value);
            }

            if (filters.MinVolume.HasValue)
            {
                filtered = filtered.Where(s => 
                    s.StockData.MarketData != null && 
                    s.StockData.MarketData.Volume >= filters.MinVolume.Value);
            }

            return filtered.ToList();
        }

        private string GenerateCacheKey(string symbol, DateTime timestamp)
        {
            return $"{symbol}_{timestamp:yyyyMMddHHmm}";
        }

        private bool TryGetCachedScore(string key, out RankingScore score)
        {
            CleanupCacheIfNeeded();
            return _scoreCache.TryGetValue(key, out score);
        }

        private void CacheScore(string key, RankingScore score)
        {
            _scoreCache[key] = score;
            CleanupCacheIfNeeded();
        }

        private void CleanupCacheIfNeeded()
        {
            if (DateTime.UtcNow - _lastCacheCleanup > _cacheExpiration)
            {
                lock (_cacheLock)
                {
                    if (DateTime.UtcNow - _lastCacheCleanup > _cacheExpiration)
                    {
                        var cutoff = DateTime.UtcNow - _cacheExpiration;
                        var keysToRemove = _scoreCache
                            .Where(kvp => kvp.Value.Timestamp < cutoff)
                            .Select(kvp => kvp.Key)
                            .ToList();

                        foreach (var key in keysToRemove)
                        {
                            _scoreCache.TryRemove(key, out _);
                        }

                        _lastCacheCleanup = DateTime.UtcNow;
                    }
                }
            }
        }
    }

    public class RankingScore
    {
        public string Symbol { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal RawScore { get; set; }
        public decimal AdjustedScore { get; set; }
        public decimal CompositeScore { get; set; }
        public decimal Confidence { get; set; }
        public Dictionary<string, decimal> FactorContributions { get; set; }
        public MarketRegime MarketRegime { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    public class RankedStock
    {
        public string Symbol { get; set; }
        public RankingScore Score { get; set; }
        public StockRankingData StockData { get; set; }
        public int Rank { get; set; }
        public decimal PercentileRank { get; set; }
        public decimal NormalizedScore { get; set; }
    }

    public class RankingOptions
    {
        public bool UseCache { get; set; } = true;
        public ScoreWeights ScoreWeights { get; set; }
        public RankingFilters Filters { get; set; }
        public int MaxResults { get; set; }
    }

    public class ScoreWeights
    {
        public decimal ModelPrediction { get; set; } = 0.4m;
        public decimal TechnicalFactors { get; set; } = 0.2m;
        public decimal FundamentalFactors { get; set; } = 0.15m;
        public decimal SentimentFactors { get; set; } = 0.1m;
        public decimal MicrostructureFactors { get; set; } = 0.1m;
        public decimal QualityFactors { get; set; } = 0.05m;
    }

    public class RankingFilters
    {
        public decimal? MinScore { get; set; }
        public decimal? MinConfidence { get; set; }
        public List<string> RequiredSectors { get; set; }
        public List<string> ExcludedSectors { get; set; }
        public decimal? MinMarketCap { get; set; }
        public long? MinVolume { get; set; }
    }
}