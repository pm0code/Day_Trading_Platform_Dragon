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

        Task<TradingResult<Dictionary<string, float>>> GetFeatureImportancesAsync(
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

        Dictionary<string, float> CalculateFactorScores(RankingFactors factors);
        
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
        float Calculate(StockRankingData stockData, MarketContext marketContext);
    }

    // Additional data classes for the interfaces
    public class RankingPrediction
    {
        public float Score { get; set; }
        public float? Confidence { get; set; }
        public Dictionary<string, float> FeatureImportances { get; set; }
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
        public float Label { get; set; } // Future performance or rank
        public Dictionary<string, object> Metadata { get; set; }
    }

    public class RankingTrainingOptions
    {
        public int NumberOfTrees { get; set; } = 100;
        public int MaxDepth { get; set; } = 10;
        public float LearningRate { get; set; } = 0.1f;
        public float SubsampleFraction { get; set; } = 0.8f;
        public int MinSamplesPerLeaf { get; set; } = 20;
        public string LossFunction { get; set; } = "RankingLoss";
        public bool UseEarlyStopping { get; set; } = true;
        public int EarlyStoppingRounds { get; set; } = 10;
        public Dictionary<string, object> AdditionalParameters { get; set; }
    }

    public class CrossValidationResult
    {
        public List<FoldResult> FoldResults { get; set; }
        public float AverageScore { get; set; }
        public float ScoreStandardDeviation { get; set; }
        public Dictionary<string, float> AverageMetrics { get; set; }
        public TimeSpan TotalTrainingTime { get; set; }
    }

    public class FoldResult
    {
        public int FoldNumber { get; set; }
        public float Score { get; set; }
        public Dictionary<string, float> Metrics { get; set; }
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
        public float OverallSentiment { get; set; }
        public float NewsSentiment { get; set; }
        public float SocialSentiment { get; set; }
        public int MentionCount { get; set; }
        public float SentimentMomentum { get; set; }
        public Dictionary<string, float> SourceSentiments { get; set; }
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
        public float MarketVolatility { get; set; }
        public float MarketLiquidity { get; set; }
        public Dictionary<string, float> EconomicIndicators { get; set; }
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
        public Dictionary<string, float> CustomFactors { get; set; }
        public float DataQuality { get; set; }
        public float[] FeatureVector { get; set; }
    }

    public class TechnicalFactors
    {
        public float MomentumScore { get; set; }
        public float TrendStrength { get; set; }
        public float RelativeStrength { get; set; }
        public float VolumeProfile { get; set; }
        public float Volatility { get; set; }
        public float PriceEfficiency { get; set; }
        public float CompositeScore { get; set; }
        public float DataCompleteness { get; set; }
    }

    public class FundamentalFactors
    {
        public float ValueScore { get; set; }
        public float GrowthScore { get; set; }
        public float ProfitabilityScore { get; set; }
        public float FinancialHealth { get; set; }
        public float EarningsQuality { get; set; }
        public float CompositeScore { get; set; }
        public float DataCompleteness { get; set; }
    }

    public class SentimentFactors
    {
        public float OverallSentiment { get; set; }
        public float SentimentMomentum { get; set; }
        public float NewsImpact { get; set; }
        public float SocialBuzz { get; set; }
        public float AnalystConsensus { get; set; }
        public float CompositeScore { get; set; }
        public float DataCompleteness { get; set; }
    }

    public class MicrostructureFactors
    {
        public float LiquidityScore { get; set; }
        public float SpreadEfficiency { get; set; }
        public float OrderFlowImbalance { get; set; }
        public float PriceImpact { get; set; }
        public float MarketDepth { get; set; }
        public float CompositeScore { get; set; }
        public float DataCompleteness { get; set; }
    }

    public class QualityFactors
    {
        public float EarningsStability { get; set; }
        public float BalanceSheetStrength { get; set; }
        public float ManagementQuality { get; set; }
        public float CompetitiveAdvantage { get; set; }
        public float BusinessModelQuality { get; set; }
        public float CompositeQuality { get; set; }
        public float DataCompleteness { get; set; }
    }

    public class RiskFactors
    {
        public float SystematicRisk { get; set; }
        public float IdiosyncraticRisk { get; set; }
        public float LiquidityRisk { get; set; }
        public float ConcentrationRisk { get; set; }
        public float TailRisk { get; set; }
        public float CompositeRisk { get; set; }
        public float DataCompleteness { get; set; }
    }
}