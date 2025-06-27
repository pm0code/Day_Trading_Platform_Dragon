// File: TradingPlatform.ML/Ranking/MultiFactorFramework.cs

using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.ML.Ranking
{
    /// <summary>
    /// Multi-factor framework for stock ranking
    /// </summary>
    public class MultiFactorFramework
    {
        private readonly Dictionary<string, FactorDefinition> _factorDefinitions;
        private readonly Dictionary<string, decimal> _factorWeights;
        
        public MultiFactorFramework()
        {
            _factorDefinitions = InitializeFactorDefinitions();
            _factorWeights = InitializeDefaultWeights();
        }
        
        /// <summary>
        /// Extract all ranking factors for a stock
        /// </summary>
        public RankingFactors ExtractFactors(
            StockRankingData stockData,
            MarketContext marketContext,
            FactorExtractionOptions options)
        {
            var factors = new RankingFactors
            {
                Symbol = stockData.Symbol,
                Timestamp = DateTime.UtcNow
            };
            
            // Technical factors
            factors.TechnicalFactors = ExtractTechnicalFactors(stockData, options);
            
            // Fundamental factors
            factors.FundamentalFactors = ExtractFundamentalFactors(stockData, marketContext);
            
            // Sentiment factors
            factors.SentimentFactors = ExtractSentimentFactors(stockData);
            
            // Market microstructure factors
            factors.MicrostructureFactors = ExtractMicrostructureFactors(stockData);
            
            // Quality and risk factors
            factors.QualityFactors = ExtractQualityFactors(stockData);
            factors.RiskFactors = ExtractRiskFactors(stockData, marketContext);
            
            // Cross-sectional factors (relative to peers)
            if (options.IncludeCrossSectional && marketContext.PeerData != null)
            {
                factors.CrossSectionalFactors = ExtractCrossSectionalFactors(
                    stockData, marketContext.PeerData);
            }
            
            // Calculate composite scores
            factors.CompositeScores = CalculateCompositeScores(factors);
            
            return factors;
        }
        
        /// <summary>
        /// Rank stocks based on multiple factors
        /// </summary>
        public List<StockRanking> RankStocks(
            List<RankingFactors> stockFactors,
            RankingStrategy strategy)
        {
            var rankings = new List<StockRanking>();
            
            // Normalize factors across stocks
            NormalizeFactors(stockFactors);
            
            // Calculate ranking scores based on strategy
            foreach (var factors in stockFactors)
            {
                var score = strategy switch
                {
                    RankingStrategy.EqualWeight => CalculateEqualWeightScore(factors),
                    RankingStrategy.MomentumFocus => CalculateMomentumScore(factors),
                    RankingStrategy.ValueFocus => CalculateValueScore(factors),
                    RankingStrategy.QualityFocus => CalculateQualityScore(factors),
                    RankingStrategy.LowRisk => CalculateLowRiskScore(factors),
                    RankingStrategy.Custom => CalculateCustomScore(factors, _factorWeights),
                    _ => CalculateEqualWeightScore(factors)
                };
                
                rankings.Add(new StockRanking
                {
                    Symbol = factors.Symbol,
                    OverallScore = score,
                    FactorScores = GetIndividualFactorScores(factors),
                    Rank = 0, // Will be set after sorting
                    Percentile = 0, // Will be calculated after sorting
                    Category = CategorizeStock(factors, score)
                });
            }
            
            // Sort and assign ranks
            rankings = rankings.OrderByDescending(r => r.OverallScore).ToList();
            for (int i = 0; i < rankings.Count; i++)
            {
                rankings[i].Rank = i + 1;
                rankings[i].Percentile = (decimal)(rankings.Count - i) / rankings.Count * 100;
            }
            
            return rankings;
        }
        
        // Factor extraction methods
        
        private TechnicalFactors ExtractTechnicalFactors(
            StockRankingData stock,
            FactorExtractionOptions options)
        {
            var factors = new TechnicalFactors();
            var prices = stock.PriceHistory;
            
            if (prices == null || prices.Count < options.MinDataPoints)
                return factors;
            
            // Momentum factors
            factors.Momentum1M = CalculateReturn(prices, 21);
            factors.Momentum3M = CalculateReturn(prices, 63);
            factors.Momentum6M = CalculateReturn(prices, 126);
            factors.Momentum12M = CalculateReturn(prices, 252);
            
            // Adjusted momentum (12M return excluding most recent month)
            factors.AdjustedMomentum = CalculateAdjustedMomentum(prices);
            
            // Volatility factors
            factors.Volatility20D = CalculateVolatility(prices, 20);
            factors.Volatility60D = CalculateVolatility(prices, 60);
            factors.DownsideVolatility = CalculateDownsideVolatility(prices, 60);
            
            // Technical indicators
            factors.RSI = CalculateRSI(prices, 14);
            factors.MACD = CalculateMACDSignal(prices);
            factors.BollingerPosition = CalculateBollingerPosition(prices, 20);
            
            // Trend strength
            factors.TrendStrength = CalculateTrendStrength(prices, 50);
            factors.PriceToMA50 = prices.Last().Close / CalculateMA(prices, 50);
            factors.PriceToMA200 = prices.Last().Close / CalculateMA(prices, 200);
            
            // Volume factors
            factors.VolumeRatio = CalculateVolumeRatio(prices, 20);
            factors.VolumeVolatility = CalculateVolumeVolatility(prices, 20);
            
            return factors;
        }
        
        private FundamentalFactors ExtractFundamentalFactors(
            StockRankingData stock,
            MarketContext context)
        {
            var factors = new FundamentalFactors();
            var fundamentals = stock.Fundamentals;
            
            if (fundamentals == null)
                return factors;
            
            // Valuation metrics
            factors.PriceToEarnings = fundamentals.PriceToEarnings;
            factors.PriceToBook = fundamentals.PriceToBook;
            factors.PriceToSales = fundamentals.PriceToSales;
            factors.EVToEBITDA = fundamentals.EVToEBITDA;
            factors.PEGRatio = CalculatePEGRatio(fundamentals);
            
            // Growth metrics
            factors.RevenueGrowth = fundamentals.RevenueGrowthYoY;
            factors.EarningsGrowth = fundamentals.EarningsGrowthYoY;
            factors.RevenueGrowthAcceleration = CalculateGrowthAcceleration(
                fundamentals.RevenueGrowthHistory);
            
            // Profitability metrics
            factors.ROE = fundamentals.ReturnOnEquity;
            factors.ROA = fundamentals.ReturnOnAssets;
            factors.ROIC = fundamentals.ReturnOnInvestedCapital;
            factors.GrossMargin = fundamentals.GrossMargin;
            factors.OperatingMargin = fundamentals.OperatingMargin;
            factors.NetMargin = fundamentals.NetMargin;
            
            // Financial health
            factors.CurrentRatio = fundamentals.CurrentRatio;
            factors.DebtToEquity = fundamentals.DebtToEquity;
            factors.InterestCoverage = fundamentals.InterestCoverage;
            factors.FreeCashFlowYield = fundamentals.FreeCashFlow / fundamentals.MarketCap;
            
            // Efficiency metrics
            factors.AssetTurnover = fundamentals.AssetTurnover;
            factors.InventoryTurnover = fundamentals.InventoryTurnover;
            
            // Relative valuation (vs sector)
            if (context.SectorAverages != null)
            {
                factors.RelativePE = fundamentals.PriceToEarnings / 
                    context.SectorAverages.AveragePE;
                factors.RelativeGrowth = fundamentals.EarningsGrowthYoY / 
                    context.SectorAverages.AverageGrowth;
            }
            
            return factors;
        }
        
        private SentimentFactors ExtractSentimentFactors(StockRankingData stock)
        {
            var factors = new SentimentFactors();
            var sentiment = stock.SentimentData;
            
            if (sentiment == null)
                return factors;
            
            // News sentiment
            factors.NewsScore = sentiment.AverageNewsScore;
            factors.NewsVolume = sentiment.NewsArticleCount;
            factors.NewsVelocity = sentiment.NewsVelocity; // Articles per day trend
            
            // Social media sentiment
            factors.SocialScore = sentiment.SocialMediaScore;
            factors.SocialVolume = sentiment.SocialMentions;
            factors.SocialEngagement = sentiment.EngagementRate;
            
            // Analyst sentiment
            factors.AnalystRating = sentiment.AverageAnalystRating;
            factors.AnalystDispersion = sentiment.RatingDispersion;
            factors.RecentUpgrades = sentiment.RecentUpgrades;
            factors.RecentDowngrades = sentiment.RecentDowngrades;
            
            // Options sentiment
            factors.PutCallRatio = sentiment.PutCallRatio;
            factors.ImpliedVolatility = sentiment.ImpliedVolatility;
            factors.SkewIndex = sentiment.OptionSkew;
            
            // Insider activity
            factors.InsiderBuyRatio = sentiment.InsiderBuyingRatio;
            factors.InsiderNetActivity = sentiment.InsiderNetPurchases;
            
            return factors;
        }
        
        private MicrostructureFactors ExtractMicrostructureFactors(StockRankingData stock)
        {
            var factors = new MicrostructureFactors();
            var microData = stock.MicrostructureData;
            
            if (microData == null)
                return factors;
            
            // Liquidity metrics
            factors.BidAskSpread = microData.AverageBidAskSpread;
            factors.EffectiveSpread = microData.EffectiveSpread;
            factors.MarketDepth = microData.MarketDepth;
            factors.TurnoverRatio = microData.DailyVolume / stock.Fundamentals?.SharesOutstanding ?? 1;
            
            // Price impact
            factors.KyleLambda = microData.KyleLambda; // Price impact coefficient
            factors.AmihudIlliquidity = microData.AmihudRatio;
            
            // Trading patterns
            factors.IntraVolatility = microData.IntradayVolatility;
            factors.CloseToCloseVol = microData.CloseToCloseVolatility;
            factors.VolumeClockRatio = microData.VolumeClockRatio; // Volume distribution
            
            // Order flow
            factors.OrderImbalance = microData.OrderImbalance;
            factors.TradeSize = microData.AverageTradeSize;
            factors.BlockVolume = microData.BlockTradeVolume;
            
            return factors;
        }
        
        private QualityFactors ExtractQualityFactors(StockRankingData stock)
        {
            var factors = new QualityFactors();
            var fundamentals = stock.Fundamentals;
            
            if (fundamentals == null)
                return factors;
            
            // Earnings quality
            factors.EarningsQuality = CalculateEarningsQuality(fundamentals);
            factors.AccrualRatio = fundamentals.AccrualRatio;
            factors.CashConversion = fundamentals.OperatingCashFlow / fundamentals.NetIncome;
            
            // Growth quality
            factors.GrowthStability = CalculateGrowthStability(fundamentals.EarningsHistory);
            factors.SalesGrowthStability = CalculateGrowthStability(fundamentals.RevenueHistory);
            
            // Balance sheet quality
            factors.AssetQuality = CalculateAssetQuality(fundamentals);
            factors.DebtQuality = CalculateDebtQuality(fundamentals);
            factors.WorkingCapitalEfficiency = fundamentals.WorkingCapital / fundamentals.Revenue;
            
            // Management quality
            factors.CapitalAllocationScore = CalculateCapitalAllocationScore(fundamentals);
            factors.DividendConsistency = fundamentals.DividendConsistencyScore;
            factors.ShareBuybackScore = CalculateShareBuybackScore(fundamentals);
            
            // Competitive position
            factors.MarketShareTrend = fundamentals.MarketShareTrend;
            factors.CompetitiveAdvantageScore = fundamentals.MoatScore;
            
            return factors;
        }
        
        private RiskFactors ExtractRiskFactors(
            StockRankingData stock,
            MarketContext context)
        {
            var factors = new RiskFactors();
            
            // Market risk
            factors.Beta = stock.RiskMetrics?.Beta ?? 1.0;
            factors.DownsideBeta = CalculateDownsideBeta(stock.PriceHistory, context.MarketReturns);
            factors.CorrelationToMarket = stock.RiskMetrics?.MarketCorrelation ?? 0.5;
            
            // Specific risk
            factors.IdiosyncraticVolatility = stock.RiskMetrics?.IdiosyncraticVol ?? 0;
            factors.EarningsVolatility = CalculateEarningsVolatility(stock.Fundamentals);
            factors.RevenueVolatility = CalculateRevenueVolatility(stock.Fundamentals);
            
            // Tail risk
            factors.ValueAtRisk = stock.RiskMetrics?.VaR95 ?? 0;
            factors.ConditionalVaR = stock.RiskMetrics?.CVaR95 ?? 0;
            factors.MaxDrawdown = CalculateMaxDrawdown(stock.PriceHistory);
            factors.Skewness = CalculateSkewness(stock.PriceHistory);
            factors.Kurtosis = CalculateKurtosis(stock.PriceHistory);
            
            // Financial risk
            factors.BankruptcyScore = CalculateAltmanZScore(stock.Fundamentals);
            factors.DistressRisk = CalculateDistressRisk(stock.Fundamentals);
            factors.LiquidityRisk = CalculateLiquidityRisk(stock);
            
            return factors;
        }
        
        private CrossSectionalFactors ExtractCrossSectionalFactors(
            StockRankingData stock,
            List<StockRankingData> peers)
        {
            var factors = new CrossSectionalFactors();
            
            // Calculate peer averages
            var peerMomentum = peers.Average(p => CalculateReturn(p.PriceHistory, 63));
            var peerPE = peers.Where(p => p.Fundamentals?.PriceToEarnings > 0)
                .Average(p => p.Fundamentals.PriceToEarnings);
            var peerROE = peers.Where(p => p.Fundamentals != null)
                .Average(p => p.Fundamentals.ReturnOnEquity);
            
            // Relative metrics
            factors.RelativeMomentum = CalculateReturn(stock.PriceHistory, 63) / peerMomentum;
            factors.RelativeValuation = stock.Fundamentals?.PriceToEarnings > 0 
                ? peerPE / stock.Fundamentals.PriceToEarnings : 0;
            factors.RelativeProfitability = stock.Fundamentals?.ReturnOnEquity / peerROE ?? 0;
            
            // Percentile rankings
            factors.MomentumPercentile = CalculatePercentileRank(
                stock, peers, s => CalculateReturn(s.PriceHistory, 63));
            factors.ValuePercentile = CalculatePercentileRank(
                stock, peers, s => 1.0m / (s.Fundamentals?.PriceToEarnings ?? decimal.MaxValue));
            factors.QualityPercentile = CalculatePercentileRank(
                stock, peers, s => s.Fundamentals?.ReturnOnEquity ?? 0);
            
            return factors;
        }
        
        // Calculation helper methods
        
        private decimal CalculateReturn(List<MarketDataSnapshot> prices, int days)
        {
            if (prices.Count < days + 1) return 0;
            
            var currentPrice = prices.Last().Close;
            var pastPrice = prices[prices.Count - days - 1].Close;
            
            return (currentPrice - pastPrice) / pastPrice * 100;
        }
        
        private decimal CalculateVolatility(List<MarketDataSnapshot> prices, int days)
        {
            if (prices.Count < days + 1) return 0;
            
            var returns = new List<decimal>();
            for (int i = prices.Count - days; i < prices.Count; i++)
            {
                var ret = (prices[i].Close - prices[i - 1].Close) / prices[i - 1].Close;
                returns.Add(ret);
            }
            
            var mean = returns.Average();
            var sumSquares = returns.Sum(r => Math.Pow(r - mean, 2));
            
            return Math.Sqrt(sumSquares / (returns.Count - 1)) * Math.Sqrt(252); // Annualized
        }
        
        private decimal CalculateRSI(List<MarketDataSnapshot> prices, int period)
        {
            if (prices.Count < period + 1) return 50;
            
            var gains = new List<decimal>();
            var losses = new List<decimal>();
            
            for (int i = prices.Count - period; i < prices.Count; i++)
            {
                var change = prices[i].Close - prices[i - 1].Close;
                if (change > 0)
                    gains.Add(change);
                else
                    losses.Add(-change);
            }
            
            var avgGain = gains.Any() ? gains.Average() : 0;
            var avgLoss = losses.Any() ? losses.Average() : 0;
            
            if (avgLoss == 0) return 100;
            
            var rs = avgGain / avgLoss;
            return 100 - (100 / (1 + rs));
        }
        
        private decimal CalculateMA(List<MarketDataSnapshot> prices, int period)
        {
            if (prices.Count < period) return prices.Last().Close;
            
            return prices.Skip(prices.Count - period).Average(p => p.Close);
        }
        
        private decimal CalculatePercentileRank<T>(
            StockRankingData stock,
            List<StockRankingData> peers,
            Func<StockRankingData, decimal> metricFunc)
        {
            var allStocks = new List<StockRankingData>(peers) { stock };
            var values = allStocks.Select(metricFunc).OrderBy(v => v).ToList();
            var stockValue = metricFunc(stock);
            
            var rank = values.IndexOf(stockValue) + 1;
            return (decimal)rank / values.Count * 100;
        }
        
        // Additional calculation methods would go here...
        
        private Dictionary<string, FactorDefinition> InitializeFactorDefinitions()
        {
            return new Dictionary<string, FactorDefinition>
            {
                ["Momentum"] = new FactorDefinition 
                { 
                    Name = "Momentum",
                    Category = FactorCategory.Technical,
                    DefaultWeight = 0.2,
                    Direction = 1 // Higher is better
                },
                ["Value"] = new FactorDefinition
                {
                    Name = "Value",
                    Category = FactorCategory.Fundamental,
                    DefaultWeight = 0.2,
                    Direction = -1 // Lower P/E is better
                },
                ["Quality"] = new FactorDefinition
                {
                    Name = "Quality",
                    Category = FactorCategory.Quality,
                    DefaultWeight = 0.2,
                    Direction = 1
                },
                ["LowVolatility"] = new FactorDefinition
                {
                    Name = "Low Volatility",
                    Category = FactorCategory.Risk,
                    DefaultWeight = 0.1,
                    Direction = -1 // Lower volatility is better
                },
                ["Growth"] = new FactorDefinition
                {
                    Name = "Growth",
                    Category = FactorCategory.Fundamental,
                    DefaultWeight = 0.15,
                    Direction = 1
                },
                ["Sentiment"] = new FactorDefinition
                {
                    Name = "Sentiment",
                    Category = FactorCategory.Sentiment,
                    DefaultWeight = 0.15,
                    Direction = 1
                }
            };
        }
        
        private Dictionary<string, decimal> InitializeDefaultWeights()
        {
            return _factorDefinitions.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.DefaultWeight);
        }
        
        // Scoring methods
        
        private decimal CalculateEqualWeightScore(RankingFactors factors)
        {
            var scores = new List<decimal>();
            
            // Add normalized scores from each category
            if (factors.TechnicalFactors != null)
                scores.Add(factors.CompositeScores["Technical"]);
            if (factors.FundamentalFactors != null)
                scores.Add(factors.CompositeScores["Fundamental"]);
            if (factors.SentimentFactors != null)
                scores.Add(factors.CompositeScores["Sentiment"]);
            if (factors.QualityFactors != null)
                scores.Add(factors.CompositeScores["Quality"]);
            
            return scores.Any() ? scores.Average() : 0;
        }
        
        private decimal CalculateMomentumScore(RankingFactors factors)
        {
            if (factors.TechnicalFactors == null) return 0;
            
            var tech = factors.TechnicalFactors;
            
            // Weighted momentum score
            return 0.3 * NormalizeValue(tech.Momentum1M, -20, 20) +
                   0.3 * NormalizeValue(tech.Momentum3M, -30, 30) +
                   0.2 * NormalizeValue(tech.Momentum6M, -40, 40) +
                   0.1 * NormalizeValue(tech.TrendStrength, 0, 1) +
                   0.1 * NormalizeValue(tech.RSI, 30, 70);
        }
        
        private decimal NormalizeValue(decimal value, decimal min, decimal max)
        {
            if (max - min == 0) return 0.5;
            return Math.Max(0, Math.Min(1, (value - min) / (max - min)));
        }
        
        private string CategorizeStock(RankingFactors factors, decimal score)
        {
            if (score > 0.8) return "Strong Buy";
            if (score > 0.6) return "Buy";
            if (score > 0.4) return "Hold";
            if (score > 0.2) return "Sell";
            return "Strong Sell";
        }
        
        // Stub methods for complex calculations
        private decimal CalculateAdjustedMomentum(List<MarketDataSnapshot> prices) => 0;
        private decimal CalculateDownsideVolatility(List<MarketDataSnapshot> prices, int days) => 0m;
        private decimal CalculateMACDSignal(List<MarketDataSnapshot> prices) => 0m;
        private decimal CalculateBollingerPosition(List<MarketDataSnapshot> prices, int period) => 0m;
        private decimal CalculateTrendStrength(List<MarketDataSnapshot> prices, int period) => 0m;
        private decimal CalculateVolumeRatio(List<MarketDataSnapshot> prices, int period) => 0m;
        private decimal CalculateVolumeVolatility(List<MarketDataSnapshot> prices, int period) => 0m;
        private decimal CalculatePEGRatio(FundamentalData fundamentals) => 0m;
        private decimal CalculateGrowthAcceleration(List<decimal> growthHistory) => 0m;
        private decimal CalculateEarningsQuality(FundamentalData fundamentals) => 0m;
        private decimal CalculateGrowthStability(List<decimal> history) => 0m;
        private decimal CalculateAssetQuality(FundamentalData fundamentals) => 0m;
        private decimal CalculateDebtQuality(FundamentalData fundamentals) => 0m;
        private decimal CalculateCapitalAllocationScore(FundamentalData fundamentals) => 0m;
        private decimal CalculateShareBuybackScore(FundamentalData fundamentals) => 0m;
        private decimal CalculateDownsideBeta(List<MarketDataSnapshot> prices, List<decimal> marketReturns) => 0m;
        private decimal CalculateEarningsVolatility(FundamentalData fundamentals) => 0m;
        private decimal CalculateRevenueVolatility(FundamentalData fundamentals) => 0m;
        private decimal CalculateMaxDrawdown(List<MarketDataSnapshot> prices) => 0m;
        private decimal CalculateSkewness(List<MarketDataSnapshot> prices) => 0m;
        private decimal CalculateKurtosis(List<MarketDataSnapshot> prices) => 0m;
        private decimal CalculateAltmanZScore(FundamentalData fundamentals) => 0m;
        private decimal CalculateDistressRisk(FundamentalData fundamentals) => 0m;
        private decimal CalculateLiquidityRisk(StockRankingData stock) => 0m;
        private decimal CalculateValueScore(RankingFactors factors) => 0m;
        private decimal CalculateQualityScore(RankingFactors factors) => 0m;
        private decimal CalculateLowRiskScore(RankingFactors factors) => 0m;
        private decimal CalculateCustomScore(RankingFactors factors, Dictionary<string, decimal> weights) => 0m;
        private Dictionary<string, decimal> GetIndividualFactorScores(RankingFactors factors) => new();
        private Dictionary<string, decimal> CalculateCompositeScores(RankingFactors factors) => new();
        private void NormalizeFactors(List<RankingFactors> factors) { }
    }
    
    // Supporting classes
    
    public class RankingFactors
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public TechnicalFactors? TechnicalFactors { get; set; }
        public FundamentalFactors? FundamentalFactors { get; set; }
        public SentimentFactors? SentimentFactors { get; set; }
        public MicrostructureFactors? MicrostructureFactors { get; set; }
        public QualityFactors? QualityFactors { get; set; }
        public RiskFactors? RiskFactors { get; set; }
        public CrossSectionalFactors? CrossSectionalFactors { get; set; }
        public Dictionary<string, decimal> CompositeScores { get; set; } = new();
    }
    
    public class TechnicalFactors
    {
        // Momentum
        public decimal Momentum1M { get; set; }
        public decimal Momentum3M { get; set; }
        public decimal Momentum6M { get; set; }
        public decimal Momentum12M { get; set; }
        public decimal AdjustedMomentum { get; set; }
        
        // Volatility
        public decimal Volatility20D { get; set; }
        public decimal Volatility60D { get; set; }
        public decimal DownsideVolatility { get; set; }
        
        // Technical indicators
        public decimal RSI { get; set; }
        public decimal MACD { get; set; }
        public decimal BollingerPosition { get; set; }
        
        // Trend
        public decimal TrendStrength { get; set; }
        public decimal PriceToMA50 { get; set; }
        public decimal PriceToMA200 { get; set; }
        
        // Volume
        public decimal VolumeRatio { get; set; }
        public decimal VolumeVolatility { get; set; }
    }
    
    public class FundamentalFactors
    {
        // Valuation
        public decimal PriceToEarnings { get; set; }
        public decimal PriceToBook { get; set; }
        public decimal PriceToSales { get; set; }
        public decimal EVToEBITDA { get; set; }
        public decimal PEGRatio { get; set; }
        
        // Growth
        public decimal RevenueGrowth { get; set; }
        public decimal EarningsGrowth { get; set; }
        public decimal RevenueGrowthAcceleration { get; set; }
        
        // Profitability
        public decimal ROE { get; set; }
        public decimal ROA { get; set; }
        public decimal ROIC { get; set; }
        public decimal GrossMargin { get; set; }
        public decimal OperatingMargin { get; set; }
        public decimal NetMargin { get; set; }
        
        // Financial health
        public decimal CurrentRatio { get; set; }
        public decimal DebtToEquity { get; set; }
        public decimal InterestCoverage { get; set; }
        public decimal FreeCashFlowYield { get; set; }
        
        // Efficiency
        public decimal AssetTurnover { get; set; }
        public decimal InventoryTurnover { get; set; }
        
        // Relative
        public decimal RelativePE { get; set; }
        public decimal RelativeGrowth { get; set; }
    }
    
    public class SentimentFactors
    {
        // News
        public decimal NewsScore { get; set; }
        public int NewsVolume { get; set; }
        public decimal NewsVelocity { get; set; }
        
        // Social
        public decimal SocialScore { get; set; }
        public int SocialVolume { get; set; }
        public decimal SocialEngagement { get; set; }
        
        // Analyst
        public decimal AnalystRating { get; set; }
        public decimal AnalystDispersion { get; set; }
        public int RecentUpgrades { get; set; }
        public int RecentDowngrades { get; set; }
        
        // Options
        public decimal PutCallRatio { get; set; }
        public decimal ImpliedVolatility { get; set; }
        public decimal SkewIndex { get; set; }
        
        // Insider
        public decimal InsiderBuyRatio { get; set; }
        public decimal InsiderNetActivity { get; set; }
    }
    
    public class MicrostructureFactors
    {
        // Liquidity
        public decimal BidAskSpread { get; set; }
        public decimal EffectiveSpread { get; set; }
        public decimal MarketDepth { get; set; }
        public decimal TurnoverRatio { get; set; }
        
        // Price impact
        public decimal KyleLambda { get; set; }
        public decimal AmihudIlliquidity { get; set; }
        
        // Trading patterns
        public decimal IntraVolatility { get; set; }
        public decimal CloseToCloseVol { get; set; }
        public decimal VolumeClockRatio { get; set; }
        
        // Order flow
        public decimal OrderImbalance { get; set; }
        public decimal TradeSize { get; set; }
        public decimal BlockVolume { get; set; }
    }
    
    public class QualityFactors
    {
        // Earnings quality
        public decimal EarningsQuality { get; set; }
        public decimal AccrualRatio { get; set; }
        public decimal CashConversion { get; set; }
        
        // Growth quality
        public decimal GrowthStability { get; set; }
        public decimal SalesGrowthStability { get; set; }
        
        // Balance sheet
        public decimal AssetQuality { get; set; }
        public decimal DebtQuality { get; set; }
        public decimal WorkingCapitalEfficiency { get; set; }
        
        // Management
        public decimal CapitalAllocationScore { get; set; }
        public decimal DividendConsistency { get; set; }
        public decimal ShareBuybackScore { get; set; }
        
        // Competitive position
        public decimal MarketShareTrend { get; set; }
        public decimal CompetitiveAdvantageScore { get; set; }
    }
    
    public class RiskFactors
    {
        // Market risk
        public decimal Beta { get; set; }
        public decimal DownsideBeta { get; set; }
        public decimal CorrelationToMarket { get; set; }
        
        // Specific risk
        public decimal IdiosyncraticVolatility { get; set; }
        public decimal EarningsVolatility { get; set; }
        public decimal RevenueVolatility { get; set; }
        
        // Tail risk
        public decimal ValueAtRisk { get; set; }
        public decimal ConditionalVaR { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal Skewness { get; set; }
        public decimal Kurtosis { get; set; }
        
        // Financial risk
        public decimal BankruptcyScore { get; set; }
        public decimal DistressRisk { get; set; }
        public decimal LiquidityRisk { get; set; }
    }
    
    public class CrossSectionalFactors
    {
        public decimal RelativeMomentum { get; set; }
        public decimal RelativeValuation { get; set; }
        public decimal RelativeProfitability { get; set; }
        public decimal MomentumPercentile { get; set; }
        public decimal ValuePercentile { get; set; }
        public decimal QualityPercentile { get; set; }
    }
    
    public class StockRanking
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal OverallScore { get; set; }
        public Dictionary<string, decimal> FactorScores { get; set; } = new();
        public int Rank { get; set; }
        public decimal Percentile { get; set; }
        public string Category { get; set; } = string.Empty;
    }
    
    public class StockRankingData
    {
        public string Symbol { get; set; } = string.Empty;
        public List<MarketDataSnapshot> PriceHistory { get; set; } = new();
        public FundamentalData? Fundamentals { get; set; }
        public SentimentData? SentimentData { get; set; }
        public MicrostructureData? MicrostructureData { get; set; }
        public RiskMetrics? RiskMetrics { get; set; }
    }
    
    public class MarketContext
    {
        public List<decimal> MarketReturns { get; set; } = new();
        public List<StockRankingData>? PeerData { get; set; }
        public SectorAverages? SectorAverages { get; set; }
        public MarketRegime CurrentRegime { get; set; }
    }
    
    public class FactorDefinition
    {
        public string Name { get; set; } = string.Empty;
        public FactorCategory Category { get; set; }
        public decimal DefaultWeight { get; set; }
        public int Direction { get; set; } // 1 = higher is better, -1 = lower is better
    }
    
    public class FactorExtractionOptions
    {
        public int MinDataPoints { get; set; } = 100;
        public bool IncludeCrossSectional { get; set; } = true;
        public bool NormalizeFactors { get; set; } = true;
    }
    
    public enum RankingStrategy
    {
        EqualWeight,
        MomentumFocus,
        ValueFocus,
        QualityFocus,
        LowRisk,
        Custom
    }
    
    public enum FactorCategory
    {
        Technical,
        Fundamental,
        Sentiment,
        Microstructure,
        Quality,
        Risk
    }
    
    public enum MarketRegime
    {
        Bull,
        Bear,
        Sideways,
        HighVolatility
    }
    
    // Placeholder classes for data structures
    public class FundamentalData
    {
        public decimal PriceToEarnings { get; set; }
        public decimal PriceToBook { get; set; }
        public decimal PriceToSales { get; set; }
        public decimal EVToEBITDA { get; set; }
        public decimal RevenueGrowthYoY { get; set; }
        public decimal EarningsGrowthYoY { get; set; }
        public decimal ReturnOnEquity { get; set; }
        public decimal ReturnOnAssets { get; set; }
        public decimal ReturnOnInvestedCapital { get; set; }
        public decimal GrossMargin { get; set; }
        public decimal OperatingMargin { get; set; }
        public decimal NetMargin { get; set; }
        public decimal CurrentRatio { get; set; }
        public decimal DebtToEquity { get; set; }
        public decimal InterestCoverage { get; set; }
        public decimal FreeCashFlow { get; set; }
        public decimal MarketCap { get; set; }
        public decimal AssetTurnover { get; set; }
        public decimal InventoryTurnover { get; set; }
        public decimal AccrualRatio { get; set; }
        public decimal OperatingCashFlow { get; set; }
        public decimal NetIncome { get; set; }
        public decimal WorkingCapital { get; set; }
        public decimal Revenue { get; set; }
        public decimal SharesOutstanding { get; set; }
        public decimal DividendConsistencyScore { get; set; }
        public decimal MoatScore { get; set; }
        public decimal MarketShareTrend { get; set; }
        public List<decimal> EarningsHistory { get; set; } = new();
        public List<decimal> RevenueHistory { get; set; } = new();
        public List<decimal> RevenueGrowthHistory { get; set; } = new();
    }
    
    public class SentimentData
    {
        public decimal AverageNewsScore { get; set; }
        public int NewsArticleCount { get; set; }
        public decimal NewsVelocity { get; set; }
        public decimal SocialMediaScore { get; set; }
        public int SocialMentions { get; set; }
        public decimal EngagementRate { get; set; }
        public decimal AverageAnalystRating { get; set; }
        public decimal RatingDispersion { get; set; }
        public int RecentUpgrades { get; set; }
        public int RecentDowngrades { get; set; }
        public decimal PutCallRatio { get; set; }
        public decimal ImpliedVolatility { get; set; }
        public decimal OptionSkew { get; set; }
        public decimal InsiderBuyingRatio { get; set; }
        public decimal InsiderNetPurchases { get; set; }
    }
    
    public class MicrostructureData
    {
        public decimal AverageBidAskSpread { get; set; }
        public decimal EffectiveSpread { get; set; }
        public decimal MarketDepth { get; set; }
        public decimal DailyVolume { get; set; }
        public decimal KyleLambda { get; set; }
        public decimal AmihudRatio { get; set; }
        public decimal IntradayVolatility { get; set; }
        public decimal CloseToCloseVolatility { get; set; }
        public decimal VolumeClockRatio { get; set; }
        public decimal OrderImbalance { get; set; }
        public decimal AverageTradeSize { get; set; }
        public decimal BlockTradeVolume { get; set; }
    }
    
    public class RiskMetrics
    {
        public decimal Beta { get; set; }
        public decimal MarketCorrelation { get; set; }
        public decimal IdiosyncraticVol { get; set; }
        public decimal VaR95 { get; set; }
        public decimal CVaR95 { get; set; }
    }
    
    public class SectorAverages
    {
        public decimal AveragePE { get; set; }
        public decimal AverageGrowth { get; set; }
    }
}