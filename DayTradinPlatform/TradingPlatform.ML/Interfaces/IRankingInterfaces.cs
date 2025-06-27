using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Common;
using TradingPlatform.ML.Common;
using TradingPlatform.ML.Ranking;

namespace TradingPlatform.ML.Interfaces
{
    public interface IRandomForestRankingModel
    {
        Task<TradingResult<ModelTrainingResult>> TrainAsync(
            RankingDataset dataset,
            RankingTrainingOptions options,
            CancellationToken cancellationToken = default);

        Task<TradingResult<RankingPrediction>> PredictAsync(
            RankingFactors factors,
            CancellationToken cancellationToken = default);

        Task<TradingResult<List<RankingPrediction>>> PredictBatchAsync(
            List<RankingFactors> factorsList,
            CancellationToken cancellationToken = default);

        Task<TradingResult<Dictionary<string, decimal>>> GetFeatureImportancesAsync(
            CancellationToken cancellationToken = default);

        Task<TradingResult<CrossValidationResult>> CrossValidateAsync(
            RankingDataset dataset,
            int folds,
            RankingTrainingOptions options,
            CancellationToken cancellationToken = default);

        Task<TradingResult> SaveModelAsync(
            string path,
            CancellationToken cancellationToken = default);

        Task<TradingResult> LoadModelAsync(
            string path,
            CancellationToken cancellationToken = default);
    }

    public interface IMultiFactorFramework
    {
        RankingFactors ExtractFactors(
            StockRankingData stockData,
            MarketContext marketContext,
            FactorExtractionOptions options);

        Task<RankingFactors> ExtractFactorsAsync(
            StockRankingData stockData,
            MarketContext marketContext,
            FactorExtractionOptions options,
            CancellationToken cancellationToken = default);

        Dictionary<string, decimal> CalculateFactorScores(RankingFactors factors);
        
        void RegisterCustomFactor(string name, ICustomFactor customFactor);
    }

    public interface IRankingScoreCalculator
    {
        Task<TradingResult<RankingScore>> CalculateRankingScoreAsync(
            StockRankingData stockData,
            MarketContext marketContext,
            RankingOptions options = null,
            CancellationToken cancellationToken = default);

        Task<TradingResult<List<RankedStock>>> RankStocksAsync(
            IEnumerable<StockRankingData> stocks,
            MarketContext marketContext,
            RankingOptions options = null,
            CancellationToken cancellationToken = default);
    }

    public interface IStockSelectionAPI
    {
        Task<TradingResult<StockSelectionResult>> SelectTopStocksAsync(
            SelectionCriteria criteria,
            CancellationToken cancellationToken = default);

        Task<TradingResult<RebalanceRecommendation>> GetRebalanceRecommendationAsync(
            Portfolio currentPortfolio,
            SelectionCriteria criteria,
            CancellationToken cancellationToken = default);

        void RegisterStrategy(string name, SelectionStrategy strategy);
    }

    public interface ICustomFactor
    {
        string Name { get; }
        string Category { get; }
        decimal Calculate(StockRankingData stockData, MarketContext marketContext);
    }

    // Additional data classes for the interfaces
    public class RankingPrediction
    {
        public decimal Score { get; set; }
        public decimal? Confidence { get; set; }
        public Dictionary<string, decimal> FeatureImportances { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class RankingDataset
    {
        public List<RankingDataPoint> DataPoints { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int FeatureCount { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    public class RankingDataPoint
    {
        public string Symbol { get; set; }
        public DateTime Timestamp { get; set; }
        public RankingFactors Factors { get; set; }
        public decimal Label { get; set; } // Future performance or rank
        public Dictionary<string, object> Metadata { get; set; }
    }

    public class RankingTrainingOptions
    {
        public int NumberOfTrees { get; set; } = 100;
        public int MaxDepth { get; set; } = 10;
        public decimal LearningRate { get; set; } = 0.1m;
        public decimal SubsampleFraction { get; set; } = 0.8m;
        public int MinSamplesPerLeaf { get; set; } = 20;
        public string LossFunction { get; set; } = "RankingLoss";
        public bool UseEarlyStopping { get; set; } = true;
        public int EarlyStoppingRounds { get; set; } = 10;
        public Dictionary<string, object> AdditionalParameters { get; set; }
    }

    public class CrossValidationResult
    {
        public List<FoldResult> FoldResults { get; set; }
        public decimal AverageScore { get; set; }
        public decimal ScoreStandardDeviation { get; set; }
        public Dictionary<string, decimal> AverageMetrics { get; set; }
        public TimeSpan TotalTrainingTime { get; set; }
    }

    public class FoldResult
    {
        public int FoldNumber { get; set; }
        public decimal Score { get; set; }
        public Dictionary<string, decimal> Metrics { get; set; }
        public TimeSpan TrainingTime { get; set; }
    }

    public class FactorExtractionOptions
    {
        public bool IncludeTechnical { get; set; } = true;
        public bool IncludeFundamental { get; set; } = true;
        public bool IncludeSentiment { get; set; } = true;
        public bool IncludeMicrostructure { get; set; } = true;
        public bool IncludeQuality { get; set; } = true;
        public bool IncludeRisk { get; set; } = true;
        public bool IncludeOptionFlow { get; set; } = false;
        public bool IncludeMarketMicrostructure { get; set; } = false;
        public List<string> CustomFactors { get; set; }
    }

    public class StockRankingData
    {
        public string Symbol { get; set; }
        public DateTime Timestamp { get; set; }
        public MarketData MarketData { get; set; }
        public List<HistoricalData> HistoricalData { get; set; }
        public FundamentalData FundamentalData { get; set; }
        public SentimentData SentimentData { get; set; }
        public OptionFlowData OptionFlowData { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; }
    }

    public class MarketData
    {
        public decimal Price { get; set; }
        public long Volume { get; set; }
        public decimal Bid { get; set; }
        public decimal Ask { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Open { get; set; }
        public decimal PreviousClose { get; set; }
        public decimal ChangePercent { get; set; }
        public string Exchange { get; set; }
    }

    public class HistoricalData
    {
        public DateTime Date { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
        public decimal AdjustedClose { get; set; }
    }

    public class FundamentalData
    {
        public decimal MarketCap { get; set; }
        public decimal PERatio { get; set; }
        public decimal EPS { get; set; }
        public decimal DividendYield { get; set; }
        public decimal Beta { get; set; }
        public decimal Revenue { get; set; }
        public decimal NetIncome { get; set; }
        public decimal ROE { get; set; }
        public decimal ROA { get; set; }
        public decimal DebtToEquity { get; set; }
        public string Sector { get; set; }
        public string Industry { get; set; }
    }

    public class SentimentData
    {
        public decimal OverallSentiment { get; set; }
        public decimal NewsSentiment { get; set; }
        public decimal SocialSentiment { get; set; }
        public int MentionCount { get; set; }
        public decimal SentimentMomentum { get; set; }
        public Dictionary<string, decimal> SourceSentiments { get; set; }
    }

    public class OptionFlowData
    {
        public decimal CallVolume { get; set; }
        public decimal PutVolume { get; set; }
        public decimal PutCallRatio { get; set; }
        public decimal ImpliedVolatility { get; set; }
        public decimal OptionVolumeMomentum { get; set; }
        public List<UnusualOptionActivity> UnusualActivities { get; set; }
    }

    public class UnusualOptionActivity
    {
        public DateTime Timestamp { get; set; }
        public string OptionType { get; set; }
        public decimal Strike { get; set; }
        public DateTime Expiry { get; set; }
        public long Volume { get; set; }
        public decimal Premium { get; set; }
    }

    public class MarketContext
    {
        public DateTime Timestamp { get; set; }
        public MarketRegime MarketRegime { get; set; }
        public MarketTrend MarketTrend { get; set; }
        public decimal MarketVolatility { get; set; }
        public decimal MarketLiquidity { get; set; }
        public Dictionary<string, decimal> EconomicIndicators { get; set; }
        public Dictionary<string, object> AdditionalContext { get; set; }
    }

    public enum MarketRegime
    {
        Stable,
        Normal,
        Volatile,
        Bullish,
        Bearish,
        Crisis
    }

    public enum MarketTrend
    {
        StrongUp,
        Up,
        Neutral,
        Down,
        StrongDown
    }

    // Factor structure classes
    public class RankingFactors
    {
        public TechnicalFactors TechnicalFactors { get; set; }
        public FundamentalFactors FundamentalFactors { get; set; }
        public SentimentFactors SentimentFactors { get; set; }
        public MicrostructureFactors MicrostructureFactors { get; set; }
        public QualityFactors QualityFactors { get; set; }
        public RiskFactors RiskFactors { get; set; }
        public Dictionary<string, decimal> CustomFactors { get; set; }
        public decimal DataQuality { get; set; }
        public decimal[] FeatureVector { get; set; }
    }

    public class TechnicalFactors
    {
        public decimal MomentumScore { get; set; }
        public decimal TrendStrength { get; set; }
        public decimal RelativeStrength { get; set; }
        public decimal VolumeProfile { get; set; }
        public decimal Volatility { get; set; }
        public decimal PriceEfficiency { get; set; }
        public decimal CompositeScore { get; set; }
        public decimal DataCompleteness { get; set; }
    }

    public class FundamentalFactors
    {
        public decimal ValueScore { get; set; }
        public decimal GrowthScore { get; set; }
        public decimal ProfitabilityScore { get; set; }
        public decimal FinancialHealth { get; set; }
        public decimal EarningsQuality { get; set; }
        public decimal CompositeScore { get; set; }
        public decimal DataCompleteness { get; set; }
    }

    public class SentimentFactors
    {
        public decimal OverallSentiment { get; set; }
        public decimal SentimentMomentum { get; set; }
        public decimal NewsImpact { get; set; }
        public decimal SocialBuzz { get; set; }
        public decimal AnalystConsensus { get; set; }
        public decimal CompositeScore { get; set; }
        public decimal DataCompleteness { get; set; }
    }

    public class MicrostructureFactors
    {
        public decimal LiquidityScore { get; set; }
        public decimal SpreadEfficiency { get; set; }
        public decimal OrderFlowImbalance { get; set; }
        public decimal PriceImpact { get; set; }
        public decimal MarketDepth { get; set; }
        public decimal CompositeScore { get; set; }
        public decimal DataCompleteness { get; set; }
    }

    public class QualityFactors
    {
        public decimal EarningsStability { get; set; }
        public decimal BalanceSheetStrength { get; set; }
        public decimal ManagementQuality { get; set; }
        public decimal CompetitiveAdvantage { get; set; }
        public decimal BusinessModelQuality { get; set; }
        public decimal CompositeQuality { get; set; }
        public decimal DataCompleteness { get; set; }
    }

    public class RiskFactors
    {
        public decimal SystematicRisk { get; set; }
        public decimal IdiosyncraticRisk { get; set; }
        public decimal LiquidityRisk { get; set; }
        public decimal ConcentrationRisk { get; set; }
        public decimal TailRisk { get; set; }
        public decimal CompositeRisk { get; set; }
        public decimal DataCompleteness { get; set; }
    }
}