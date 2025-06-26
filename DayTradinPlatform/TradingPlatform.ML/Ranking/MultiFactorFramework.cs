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
        private readonly Dictionary<string, double> _factorWeights;
        
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
                rankings[i].Percentile = (double)(rankings.Count - i) / rankings.Count * 100;
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
                stock, peers, s => 1.0 / (s.Fundamentals?.PriceToEarnings ?? double.MaxValue));
            factors.QualityPercentile = CalculatePercentileRank(
                stock, peers, s => s.Fundamentals?.ReturnOnEquity ?? 0);
            
            return factors;
        }
        
        // Calculation helper methods
        
        private double CalculateReturn(List<MarketDataSnapshot> prices, int days)
        {
            if (prices.Count < days + 1) return 0;
            
            var currentPrice = prices.Last().Close;
            var pastPrice = prices[prices.Count - days - 1].Close;
            
            return (double)((currentPrice - pastPrice) / pastPrice * 100);
        }
        
        private double CalculateVolatility(List<MarketDataSnapshot> prices, int days)
        {
            if (prices.Count < days + 1) return 0;
            
            var returns = new List<double>();
            for (int i = prices.Count - days; i < prices.Count; i++)
            {
                var ret = (double)((prices[i].Close - prices[i - 1].Close) / prices[i - 1].Close);
                returns.Add(ret);
            }
            
            var mean = returns.Average();
            var sumSquares = returns.Sum(r => Math.Pow(r - mean, 2));
            
            return Math.Sqrt(sumSquares / (returns.Count - 1)) * Math.Sqrt(252); // Annualized
        }
        
        private double CalculateRSI(List<MarketDataSnapshot> prices, int period)
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
            return 100 - (100 / (1 + (double)rs));
        }
        
        private decimal CalculateMA(List<MarketDataSnapshot> prices, int period)
        {
            if (prices.Count < period) return prices.Last().Close;
            
            return prices.Skip(prices.Count - period).Average(p => p.Close);
        }
        
        private double CalculatePercentileRank<T>(
            StockRankingData stock,
            List<StockRankingData> peers,
            Func<StockRankingData, double> metricFunc)
        {
            var allStocks = new List<StockRankingData>(peers) { stock };
            var values = allStocks.Select(metricFunc).OrderBy(v => v).ToList();
            var stockValue = metricFunc(stock);
            
            var rank = values.IndexOf(stockValue) + 1;
            return (double)rank / values.Count * 100;
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
        
        private Dictionary<string, double> InitializeDefaultWeights()
        {
            return _factorDefinitions.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.DefaultWeight);
        }
        
        // Scoring methods
        
        private double CalculateEqualWeightScore(RankingFactors factors)
        {
            var scores = new List<double>();
            
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
        
        private double CalculateMomentumScore(RankingFactors factors)
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
        
        private double NormalizeValue(double value, double min, double max)
        {
            if (max - min == 0) return 0.5;
            return Math.Max(0, Math.Min(1, (value - min) / (max - min)));
        }
        
        private string CategorizeStock(RankingFactors factors, double score)
        {
            if (score > 0.8) return "Strong Buy";
            if (score > 0.6) return "Buy";
            if (score > 0.4) return "Hold";
            if (score > 0.2) return "Sell";
            return "Strong Sell";
        }
        
        // Stub methods for complex calculations
        private double CalculateAdjustedMomentum(List<MarketDataSnapshot> prices) => 0;
        private double CalculateDownsideVolatility(List<MarketDataSnapshot> prices, int days) => 0;
        private double CalculateMACDSignal(List<MarketDataSnapshot> prices) => 0;
        private double CalculateBollingerPosition(List<MarketDataSnapshot> prices, int period) => 0;
        private double CalculateTrendStrength(List<MarketDataSnapshot> prices, int period) => 0;
        private double CalculateVolumeRatio(List<MarketDataSnapshot> prices, int period) => 0;
        private double CalculateVolumeVolatility(List<MarketDataSnapshot> prices, int period) => 0;
        private double CalculatePEGRatio(FundamentalData fundamentals) => 0;
        private double CalculateGrowthAcceleration(List<double> growthHistory) => 0;
        private double CalculateEarningsQuality(FundamentalData fundamentals) => 0;
        private double CalculateGrowthStability(List<double> history) => 0;
        private double CalculateAssetQuality(FundamentalData fundamentals) => 0;
        private double CalculateDebtQuality(FundamentalData fundamentals) => 0;
        private double CalculateCapitalAllocationScore(FundamentalData fundamentals) => 0;
        private double CalculateShareBuybackScore(FundamentalData fundamentals) => 0;
        private double CalculateDownsideBeta(List<MarketDataSnapshot> prices, List<double> marketReturns) => 0;
        private double CalculateEarningsVolatility(FundamentalData fundamentals) => 0;
        private double CalculateRevenueVolatility(FundamentalData fundamentals) => 0;
        private double CalculateMaxDrawdown(List<MarketDataSnapshot> prices) => 0;
        private double CalculateSkewness(List<MarketDataSnapshot> prices) => 0;
        private double CalculateKurtosis(List<MarketDataSnapshot> prices) => 0;
        private double CalculateAltmanZScore(FundamentalData fundamentals) => 0;
        private double CalculateDistressRisk(FundamentalData fundamentals) => 0;
        private double CalculateLiquidityRisk(StockRankingData stock) => 0;
        private double CalculateValueScore(RankingFactors factors) => 0;
        private double CalculateQualityScore(RankingFactors factors) => 0;
        private double CalculateLowRiskScore(RankingFactors factors) => 0;
        private double CalculateCustomScore(RankingFactors factors, Dictionary<string, double> weights) => 0;
        private Dictionary<string, double> GetIndividualFactorScores(RankingFactors factors) => new();
        private Dictionary<string, double> CalculateCompositeScores(RankingFactors factors) => new();
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
        public Dictionary<string, double> CompositeScores { get; set; } = new();
    }
    
    public class TechnicalFactors
    {
        // Momentum
        public double Momentum1M { get; set; }
        public double Momentum3M { get; set; }
        public double Momentum6M { get; set; }
        public double Momentum12M { get; set; }
        public double AdjustedMomentum { get; set; }
        
        // Volatility
        public double Volatility20D { get; set; }
        public double Volatility60D { get; set; }
        public double DownsideVolatility { get; set; }
        
        // Technical indicators
        public double RSI { get; set; }
        public double MACD { get; set; }
        public double BollingerPosition { get; set; }
        
        // Trend
        public double TrendStrength { get; set; }
        public double PriceToMA50 { get; set; }
        public double PriceToMA200 { get; set; }
        
        // Volume
        public double VolumeRatio { get; set; }
        public double VolumeVolatility { get; set; }
    }
    
    public class FundamentalFactors
    {
        // Valuation
        public double PriceToEarnings { get; set; }
        public double PriceToBook { get; set; }
        public double PriceToSales { get; set; }
        public double EVToEBITDA { get; set; }
        public double PEGRatio { get; set; }
        
        // Growth
        public double RevenueGrowth { get; set; }
        public double EarningsGrowth { get; set; }
        public double RevenueGrowthAcceleration { get; set; }
        
        // Profitability
        public double ROE { get; set; }
        public double ROA { get; set; }
        public double ROIC { get; set; }
        public double GrossMargin { get; set; }
        public double OperatingMargin { get; set; }
        public double NetMargin { get; set; }
        
        // Financial health
        public double CurrentRatio { get; set; }
        public double DebtToEquity { get; set; }
        public double InterestCoverage { get; set; }
        public double FreeCashFlowYield { get; set; }
        
        // Efficiency
        public double AssetTurnover { get; set; }
        public double InventoryTurnover { get; set; }
        
        // Relative
        public double RelativePE { get; set; }
        public double RelativeGrowth { get; set; }
    }
    
    public class SentimentFactors
    {
        // News
        public double NewsScore { get; set; }
        public int NewsVolume { get; set; }
        public double NewsVelocity { get; set; }
        
        // Social
        public double SocialScore { get; set; }
        public int SocialVolume { get; set; }
        public double SocialEngagement { get; set; }
        
        // Analyst
        public double AnalystRating { get; set; }
        public double AnalystDispersion { get; set; }
        public int RecentUpgrades { get; set; }
        public int RecentDowngrades { get; set; }
        
        // Options
        public double PutCallRatio { get; set; }
        public double ImpliedVolatility { get; set; }
        public double SkewIndex { get; set; }
        
        // Insider
        public double InsiderBuyRatio { get; set; }
        public double InsiderNetActivity { get; set; }
    }
    
    public class MicrostructureFactors
    {
        // Liquidity
        public double BidAskSpread { get; set; }
        public double EffectiveSpread { get; set; }
        public double MarketDepth { get; set; }
        public double TurnoverRatio { get; set; }
        
        // Price impact
        public double KyleLambda { get; set; }
        public double AmihudIlliquidity { get; set; }
        
        // Trading patterns
        public double IntraVolatility { get; set; }
        public double CloseToCloseVol { get; set; }
        public double VolumeClockRatio { get; set; }
        
        // Order flow
        public double OrderImbalance { get; set; }
        public double TradeSize { get; set; }
        public double BlockVolume { get; set; }
    }
    
    public class QualityFactors
    {
        // Earnings quality
        public double EarningsQuality { get; set; }
        public double AccrualRatio { get; set; }
        public double CashConversion { get; set; }
        
        // Growth quality
        public double GrowthStability { get; set; }
        public double SalesGrowthStability { get; set; }
        
        // Balance sheet
        public double AssetQuality { get; set; }
        public double DebtQuality { get; set; }
        public double WorkingCapitalEfficiency { get; set; }
        
        // Management
        public double CapitalAllocationScore { get; set; }
        public double DividendConsistency { get; set; }
        public double ShareBuybackScore { get; set; }
        
        // Competitive position
        public double MarketShareTrend { get; set; }
        public double CompetitiveAdvantageScore { get; set; }
    }
    
    public class RiskFactors
    {
        // Market risk
        public double Beta { get; set; }
        public double DownsideBeta { get; set; }
        public double CorrelationToMarket { get; set; }
        
        // Specific risk
        public double IdiosyncraticVolatility { get; set; }
        public double EarningsVolatility { get; set; }
        public double RevenueVolatility { get; set; }
        
        // Tail risk
        public double ValueAtRisk { get; set; }
        public double ConditionalVaR { get; set; }
        public double MaxDrawdown { get; set; }
        public double Skewness { get; set; }
        public double Kurtosis { get; set; }
        
        // Financial risk
        public double BankruptcyScore { get; set; }
        public double DistressRisk { get; set; }
        public double LiquidityRisk { get; set; }
    }
    
    public class CrossSectionalFactors
    {
        public double RelativeMomentum { get; set; }
        public double RelativeValuation { get; set; }
        public double RelativeProfitability { get; set; }
        public double MomentumPercentile { get; set; }
        public double ValuePercentile { get; set; }
        public double QualityPercentile { get; set; }
    }
    
    public class StockRanking
    {
        public string Symbol { get; set; } = string.Empty;
        public double OverallScore { get; set; }
        public Dictionary<string, double> FactorScores { get; set; } = new();
        public int Rank { get; set; }
        public double Percentile { get; set; }
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
        public List<double> MarketReturns { get; set; } = new();
        public List<StockRankingData>? PeerData { get; set; }
        public SectorAverages? SectorAverages { get; set; }
        public MarketRegime CurrentRegime { get; set; }
    }
    
    public class FactorDefinition
    {
        public string Name { get; set; } = string.Empty;
        public FactorCategory Category { get; set; }
        public double DefaultWeight { get; set; }
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
        public double PriceToEarnings { get; set; }
        public double PriceToBook { get; set; }
        public double PriceToSales { get; set; }
        public double EVToEBITDA { get; set; }
        public double RevenueGrowthYoY { get; set; }
        public double EarningsGrowthYoY { get; set; }
        public double ReturnOnEquity { get; set; }
        public double ReturnOnAssets { get; set; }
        public double ReturnOnInvestedCapital { get; set; }
        public double GrossMargin { get; set; }
        public double OperatingMargin { get; set; }
        public double NetMargin { get; set; }
        public double CurrentRatio { get; set; }
        public double DebtToEquity { get; set; }
        public double InterestCoverage { get; set; }
        public double FreeCashFlow { get; set; }
        public double MarketCap { get; set; }
        public double AssetTurnover { get; set; }
        public double InventoryTurnover { get; set; }
        public double AccrualRatio { get; set; }
        public double OperatingCashFlow { get; set; }
        public double NetIncome { get; set; }
        public double WorkingCapital { get; set; }
        public double Revenue { get; set; }
        public double SharesOutstanding { get; set; }
        public double DividendConsistencyScore { get; set; }
        public double MoatScore { get; set; }
        public double MarketShareTrend { get; set; }
        public List<double> EarningsHistory { get; set; } = new();
        public List<double> RevenueHistory { get; set; } = new();
        public List<double> RevenueGrowthHistory { get; set; } = new();
    }
    
    public class SentimentData
    {
        public double AverageNewsScore { get; set; }
        public int NewsArticleCount { get; set; }
        public double NewsVelocity { get; set; }
        public double SocialMediaScore { get; set; }
        public int SocialMentions { get; set; }
        public double EngagementRate { get; set; }
        public double AverageAnalystRating { get; set; }
        public double RatingDispersion { get; set; }
        public int RecentUpgrades { get; set; }
        public int RecentDowngrades { get; set; }
        public double PutCallRatio { get; set; }
        public double ImpliedVolatility { get; set; }
        public double OptionSkew { get; set; }
        public double InsiderBuyingRatio { get; set; }
        public double InsiderNetPurchases { get; set; }
    }
    
    public class MicrostructureData
    {
        public double AverageBidAskSpread { get; set; }
        public double EffectiveSpread { get; set; }
        public double MarketDepth { get; set; }
        public double DailyVolume { get; set; }
        public double KyleLambda { get; set; }
        public double AmihudRatio { get; set; }
        public double IntradayVolatility { get; set; }
        public double CloseToCloseVolatility { get; set; }
        public double VolumeClockRatio { get; set; }
        public double OrderImbalance { get; set; }
        public double AverageTradeSize { get; set; }
        public double BlockTradeVolume { get; set; }
    }
    
    public class RiskMetrics
    {
        public double Beta { get; set; }
        public double MarketCorrelation { get; set; }
        public double IdiosyncraticVol { get; set; }
        public double VaR95 { get; set; }
        public double CVaR95 { get; set; }
    }
    
    public class SectorAverages
    {
        public double AveragePE { get; set; }
        public double AverageGrowth { get; set; }
    }
}