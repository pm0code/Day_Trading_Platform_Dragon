// File: TradingPlatform.ML/Models/ModelDefinitions.cs

using Microsoft.ML.Data;

namespace TradingPlatform.ML.Models
{
    /// <summary>
    /// ML dataset interface for standardized data handling
    /// </summary>
    public interface IMLDataset
    {
        string Name { get; }
        int SampleCount { get; }
        DateTime CreatedAt { get; }
        IDataView GetDataView();
    }
    
    /// <summary>
    /// Training options for ML models
    /// </summary>
    public class ModelTrainingOptions
    {
        public int MaxIterations { get; set; } = 100;
        public double LearningRate { get; set; } = 0.1;
        public int BatchSize { get; set; } = 32;
        public double ValidationSplit { get; set; } = 0.2;
        public bool EarlyStopping { get; set; } = true;
        public int EarlyStoppingPatience { get; set; } = 10;
        public string? ModelSavePath { get; set; }
        public bool UseGpu { get; set; } = true;
        public Dictionary<string, object> HyperParameters { get; set; } = new();
    }
    
    /// <summary>
    /// Result of model training
    /// </summary>
    public class ModelTrainingResult
    {
        public string ModelId { get; set; } = string.Empty;
        public TimeSpan TrainingDuration { get; set; }
        public int EpochsTrained { get; set; }
        public double FinalLoss { get; set; }
        public double ValidationAccuracy { get; set; }
        public Dictionary<string, double> Metrics { get; set; } = new();
        public List<TrainingHistory> History { get; set; } = new();
    }
    
    /// <summary>
    /// Training history entry
    /// </summary>
    public class TrainingHistory
    {
        public int Epoch { get; set; }
        public double Loss { get; set; }
        public double ValidationLoss { get; set; }
        public Dictionary<string, double> Metrics { get; set; } = new();
        public TimeSpan Duration { get; set; }
    }
    
    /// <summary>
    /// Model evaluation result
    /// </summary>
    public class ModelEvaluationResult
    {
        public double Accuracy { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public double F1Score { get; set; }
        public double RootMeanSquaredError { get; set; }
        public double MeanAbsoluteError { get; set; }
        public double R2Score { get; set; }
        public ConfusionMatrix? ConfusionMatrix { get; set; }
        public Dictionary<string, double> CustomMetrics { get; set; } = new();
    }
    
    /// <summary>
    /// Confusion matrix for classification models
    /// </summary>
    public class ConfusionMatrix
    {
        public int[,] Matrix { get; set; } = new int[0, 0];
        public string[] Classes { get; set; } = Array.Empty<string>();
        
        public int TruePositives => Matrix[1, 1];
        public int TrueNegatives => Matrix[0, 0];
        public int FalsePositives => Matrix[0, 1];
        public int FalseNegatives => Matrix[1, 0];
    }
    
    /// <summary>
    /// Prediction confidence information
    /// </summary>
    public class PredictionConfidence
    {
        public double Confidence { get; set; }
        public Dictionary<string, double> ClassProbabilities { get; set; } = new();
        public double PredictionInterval { get; set; }
        public double StandardError { get; set; }
    }
    
    /// <summary>
    /// SHAP explanation for model predictions
    /// </summary>
    public class ShapExplanation
    {
        public Dictionary<string, double> FeatureContributions { get; set; } = new();
        public double BaseValue { get; set; }
        public double PredictedValue { get; set; }
        public string[] TopFeatures { get; set; } = Array.Empty<string>();
    }
    
    /// <summary>
    /// Input features for XGBoost price prediction model
    /// </summary>
    public class PricePredictionInput
    {
        [LoadColumn(0)]
        public float Open { get; set; }
        
        [LoadColumn(1)]
        public float High { get; set; }
        
        [LoadColumn(2)]
        public float Low { get; set; }
        
        [LoadColumn(3)]
        public float Close { get; set; }
        
        [LoadColumn(4)]
        public float Volume { get; set; }
        
        [LoadColumn(5)]
        public float RSI { get; set; }
        
        [LoadColumn(6)]
        public float MACD { get; set; }
        
        [LoadColumn(7)]
        public float BollingerUpper { get; set; }
        
        [LoadColumn(8)]
        public float BollingerLower { get; set; }
        
        [LoadColumn(9)]
        public float MovingAverage20 { get; set; }
        
        [LoadColumn(10)]
        public float MovingAverage50 { get; set; }
        
        [LoadColumn(11)]
        public float VolumeRatio { get; set; }
        
        [LoadColumn(12)]
        public float PriceChangePercent { get; set; }
        
        [LoadColumn(13)]
        public float MarketCap { get; set; }
        
        [LoadColumn(14)]
        public float DayOfWeek { get; set; }
        
        [LoadColumn(15)]
        public float HourOfDay { get; set; }
    }
    
    /// <summary>
    /// Output prediction for price model
    /// </summary>
    public class PricePrediction
    {
        [ColumnName("Score")]
        public float PredictedPrice { get; set; }
        
        public float PriceChangePercent { get; set; }
        
        public string Direction => PriceChangePercent > 0 ? "UP" : "DOWN";
        
        public float Confidence { get; set; }
    }
    
    /// <summary>
    /// Pattern recognition input for LSTM
    /// </summary>
    public class PatternRecognitionInput
    {
        public float[][] PriceSequence { get; set; } = Array.Empty<float[]>();
        public float[][] VolumeSequence { get; set; } = Array.Empty<float[]>();
        public float[] TechnicalIndicators { get; set; } = Array.Empty<float>();
        public int SequenceLength { get; set; }
        public string Symbol { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Pattern recognition output
    /// </summary>
    public class PatternRecognitionOutput
    {
        public string PatternType { get; set; } = string.Empty;
        public float PatternStrength { get; set; }
        public float ProbabilityBullish { get; set; }
        public float ProbabilityBearish { get; set; }
        public float ExpectedMove { get; set; }
        public int PatternDuration { get; set; }
    }
    
    /// <summary>
    /// Stock ranking input for Random Forest
    /// </summary>
    public class StockRankingInput
    {
        public string Symbol { get; set; } = string.Empty;
        public float MarketCap { get; set; }
        public float PriceToEarnings { get; set; }
        public float DividendYield { get; set; }
        public float Beta { get; set; }
        public float VolatilityScore { get; set; }
        public float MomentumScore { get; set; }
        public float TechnicalScore { get; set; }
        public float SentimentScore { get; set; }
        public float LiquidityScore { get; set; }
        public float RelativeStrength { get; set; }
    }
    
    /// <summary>
    /// Stock ranking output
    /// </summary>
    public class StockRankingOutput
    {
        public string Symbol { get; set; } = string.Empty;
        public float RankingScore { get; set; }
        public int Rank { get; set; }
        public string Category { get; set; } = string.Empty; // "Strong Buy", "Buy", "Hold", "Sell"
        public Dictionary<string, float> FactorScores { get; set; } = new();
    }
}