// File: TradingPlatform.ML/Data/DataPreprocessor.cs

using Microsoft.ML;
using Microsoft.ML.Data;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;
using TradingPlatform.ML.Features;
using TradingPlatform.ML.Models;

namespace TradingPlatform.ML.Data
{
    /// <summary>
    /// Data preprocessing and normalization for ML models
    /// </summary>
    public class DataPreprocessor
    {
        private readonly MLContext _mlContext;
        private readonly Dictionary<string, (double min, double max)> _normalizationParams;
        private readonly object _lockObject = new();
        
        public DataPreprocessor(MLContext mlContext)
        {
            _mlContext = mlContext ?? throw new ArgumentNullException(nameof(mlContext));
            _normalizationParams = new Dictionary<string, (double min, double max)>();
        }
        
        /// <summary>
        /// Preprocess market data for XGBoost price prediction
        /// </summary>
        public IDataView PreprocessForPricePrediction(
            IList<MarketDataSnapshot> marketData,
            string symbol,
            int lookbackPeriod = 50)
        {
            if (marketData.Count < lookbackPeriod + 1)
                throw new ArgumentException($"Insufficient data. Need at least {lookbackPeriod + 1} data points.");
            
            var features = new List<PricePredictionFeatures>();
            
            // Extract features for each valid data point
            for (int i = lookbackPeriod; i < marketData.Count - 1; i++)
            {
                try
                {
                    var technicalFeatures = FeatureEngineering.ExtractTechnicalFeatures(marketData, i);
                    var microFeatures = FeatureEngineering.ExtractMicrostructureFeatures(marketData[i], marketData);
                    var timeFeatures = FeatureEngineering.ExtractTimeFeatures(marketData[i].Timestamp);
                    
                    var feature = new PricePredictionFeatures
                    {
                        // Price features
                        Open = technicalFeatures.Open,
                        High = technicalFeatures.High,
                        Low = technicalFeatures.Low,
                        Close = technicalFeatures.Close,
                        Volume = technicalFeatures.Volume,
                        
                        // Technical indicators
                        RSI = technicalFeatures.RSI,
                        MACD = technicalFeatures.MACD,
                        BollingerUpper = technicalFeatures.BollingerUpper,
                        BollingerLower = technicalFeatures.BollingerLower,
                        SMA20 = technicalFeatures.SMA20,
                        SMA50 = technicalFeatures.SMA50,
                        
                        // Volume features
                        VolumeRatio = technicalFeatures.VolumeRatio,
                        VWAP = technicalFeatures.VWAP,
                        
                        // Price change features
                        PriceChangePercent = technicalFeatures.PriceChangePercent,
                        DailyRange = technicalFeatures.DailyRange,
                        
                        // Microstructure features
                        BidAskSpread = microFeatures.BidAskSpread,
                        RelativeSpread = microFeatures.RelativeSpread,
                        RealizedVolatility = microFeatures.RealizedVolatility,
                        
                        // Time features
                        HourOfDay = timeFeatures.HourOfDay,
                        DayOfWeek = timeFeatures.DayOfWeek,
                        IsMarketOpen = timeFeatures.IsMarketOpen ? 1f : 0f,
                        MinutesFromOpen = timeFeatures.MinutesFromOpen,
                        
                        // Target variable (next period's price)
                        NextPrice = (float)marketData[i + 1].Close,
                        NextPriceChange = (float)((marketData[i + 1].Close - marketData[i].Close) / marketData[i].Close * 100)
                    };
                    
                    features.Add(feature);
                }
                catch (Exception ex)
                {
                    // Log and skip problematic data points
                    Console.WriteLine($"Error processing data at index {i}: {ex.Message}");
                }
            }
            
            // Convert to IDataView
            var dataView = _mlContext.Data.LoadFromEnumerable(features);
            
            // Apply normalization
            return NormalizeFeatures(dataView);
        }
        
        /// <summary>
        /// Preprocess sequential data for LSTM pattern recognition
        /// </summary>
        public IDataView PreprocessForPatternRecognition(
            IList<MarketDataSnapshot> marketData,
            int sequenceLength = 60,
            int stepSize = 1)
        {
            var sequences = new List<PatternSequence>();
            
            for (int i = 0; i <= marketData.Count - sequenceLength - 1; i += stepSize)
            {
                var priceSequence = new float[sequenceLength][];
                var volumeSequence = new float[sequenceLength][];
                
                for (int j = 0; j < sequenceLength; j++)
                {
                    var idx = i + j;
                    priceSequence[j] = new float[]
                    {
                        (float)marketData[idx].Open,
                        (float)marketData[idx].High,
                        (float)marketData[idx].Low,
                        (float)marketData[idx].Close
                    };
                    
                    volumeSequence[j] = new float[]
                    {
                        (float)marketData[idx].Volume,
                        (float)(marketData[idx].BidSize ?? 0),
                        (float)(marketData[idx].AskSize ?? 0)
                    };
                }
                
                // Calculate pattern label based on future price movement
                var currentPrice = marketData[i + sequenceLength - 1].Close;
                var futurePrice = marketData[i + sequenceLength].Close;
                var priceChange = (futurePrice - currentPrice) / currentPrice;
                
                sequences.Add(new PatternSequence
                {
                    PriceSequence = priceSequence,
                    VolumeSequence = volumeSequence,
                    PatternLabel = ClassifyPattern(priceChange),
                    PriceChangePercent = (float)(priceChange * 100)
                });
            }
            
            return _mlContext.Data.LoadFromEnumerable(sequences);
        }
        
        /// <summary>
        /// Preprocess data for stock ranking
        /// </summary>
        public IDataView PreprocessForStockRanking(
            IList<StockFundamentals> fundamentals,
            Dictionary<string, MarketMetrics> marketMetrics)
        {
            var rankingFeatures = new List<StockRankingFeatures>();
            
            foreach (var stock in fundamentals)
            {
                if (!marketMetrics.TryGetValue(stock.Symbol, out var metrics))
                    continue;
                
                var features = new StockRankingFeatures
                {
                    Symbol = stock.Symbol,
                    
                    // Fundamental features
                    MarketCap = (float)stock.MarketCap,
                    PriceToEarnings = (float)stock.PriceToEarnings,
                    DividendYield = (float)stock.DividendYield,
                    PriceToBook = (float)stock.PriceToBook,
                    DebtToEquity = (float)stock.DebtToEquity,
                    
                    // Technical features
                    Beta = (float)metrics.Beta,
                    VolatilityScore = (float)metrics.Volatility,
                    MomentumScore = (float)metrics.Momentum,
                    RelativeStrength = (float)metrics.RelativeStrength,
                    
                    // Volume and liquidity
                    AverageVolume = (float)metrics.AverageVolume,
                    VolumeRatio = (float)metrics.VolumeRatio,
                    LiquidityScore = (float)metrics.LiquidityScore,
                    
                    // Performance metrics
                    Return1Day = (float)metrics.Return1Day,
                    Return5Day = (float)metrics.Return5Day,
                    Return30Day = (float)metrics.Return30Day,
                    
                    // Composite scores
                    TechnicalScore = CalculateTechnicalScore(metrics),
                    FundamentalScore = CalculateFundamentalScore(stock),
                    SentimentScore = (float)metrics.SentimentScore
                };
                
                rankingFeatures.Add(features);
            }
            
            return _mlContext.Data.LoadFromEnumerable(rankingFeatures);
        }
        
        /// <summary>
        /// Normalize features using min-max scaling
        /// </summary>
        private IDataView NormalizeFeatures(IDataView data)
        {
            var pipeline = _mlContext.Transforms.NormalizeMinMax("Features", "Features");
            var model = pipeline.Fit(data);
            return model.Transform(data);
        }
        
        /// <summary>
        /// Apply saved normalization parameters
        /// </summary>
        public float[] ApplyNormalization(float[] features, string[] featureNames)
        {
            if (features.Length != featureNames.Length)
                throw new ArgumentException("Features and feature names must have same length");
            
            var normalized = new float[features.Length];
            
            lock (_lockObject)
            {
                for (int i = 0; i < features.Length; i++)
                {
                    if (_normalizationParams.TryGetValue(featureNames[i], out var param))
                    {
                        normalized[i] = (float)((features[i] - param.min) / (param.max - param.min));
                    }
                    else
                    {
                        normalized[i] = features[i]; // No normalization available
                    }
                }
            }
            
            return normalized;
        }
        
        /// <summary>
        /// Save normalization parameters for inference
        /// </summary>
        public void SaveNormalizationParameters(Dictionary<string, (double min, double max)> parameters)
        {
            lock (_lockObject)
            {
                _normalizationParams.Clear();
                foreach (var kvp in parameters)
                {
                    _normalizationParams[kvp.Key] = kvp.Value;
                }
            }
        }
        
        private string ClassifyPattern(decimal priceChange)
        {
            return priceChange switch
            {
                > 0.02m => "StrongBullish",
                > 0.005m => "Bullish",
                > -0.005m => "Neutral",
                > -0.02m => "Bearish",
                _ => "StrongBearish"
            };
        }
        
        private float CalculateTechnicalScore(MarketMetrics metrics)
        {
            // Weighted combination of technical indicators
            var momentum = Math.Max(0, Math.Min(1, (metrics.Momentum + 1) / 2));
            var rsi = Math.Max(0, Math.Min(1, metrics.RSI / 100));
            var volatility = Math.Max(0, Math.Min(1, 1 - metrics.Volatility));
            
            return (float)(momentum * 0.4 + rsi * 0.3 + volatility * 0.3);
        }
        
        private float CalculateFundamentalScore(StockFundamentals stock)
        {
            // Simple scoring based on fundamental ratios
            var peScore = stock.PriceToEarnings > 0 && stock.PriceToEarnings < 30 ? 1 : 0;
            var pbScore = stock.PriceToBook > 0 && stock.PriceToBook < 5 ? 1 : 0;
            var deScore = stock.DebtToEquity < 2 ? 1 : 0;
            var divScore = stock.DividendYield > 0.02 ? 1 : 0;
            
            return (peScore + pbScore + deScore + divScore) / 4f;
        }
    }
    
    /// <summary>
    /// Features for price prediction model
    /// </summary>
    public class PricePredictionFeatures
    {
        [LoadColumn(0)] public float Open { get; set; }
        [LoadColumn(1)] public float High { get; set; }
        [LoadColumn(2)] public float Low { get; set; }
        [LoadColumn(3)] public float Close { get; set; }
        [LoadColumn(4)] public float Volume { get; set; }
        [LoadColumn(5)] public float RSI { get; set; }
        [LoadColumn(6)] public float MACD { get; set; }
        [LoadColumn(7)] public float BollingerUpper { get; set; }
        [LoadColumn(8)] public float BollingerLower { get; set; }
        [LoadColumn(9)] public float SMA20 { get; set; }
        [LoadColumn(10)] public float SMA50 { get; set; }
        [LoadColumn(11)] public float VolumeRatio { get; set; }
        [LoadColumn(12)] public float VWAP { get; set; }
        [LoadColumn(13)] public float PriceChangePercent { get; set; }
        [LoadColumn(14)] public float DailyRange { get; set; }
        [LoadColumn(15)] public float BidAskSpread { get; set; }
        [LoadColumn(16)] public float RelativeSpread { get; set; }
        [LoadColumn(17)] public float RealizedVolatility { get; set; }
        [LoadColumn(18)] public float HourOfDay { get; set; }
        [LoadColumn(19)] public float DayOfWeek { get; set; }
        [LoadColumn(20)] public float IsMarketOpen { get; set; }
        [LoadColumn(21)] public float MinutesFromOpen { get; set; }
        
        [LoadColumn(22)] public float NextPrice { get; set; } // Target
        [LoadColumn(23)] public float NextPriceChange { get; set; } // Alternative target
    }
    
    /// <summary>
    /// Pattern sequence for LSTM
    /// </summary>
    public class PatternSequence
    {
        public float[][] PriceSequence { get; set; } = Array.Empty<float[]>();
        public float[][] VolumeSequence { get; set; } = Array.Empty<float[]>();
        public string PatternLabel { get; set; } = string.Empty;
        public float PriceChangePercent { get; set; }
    }
    
    /// <summary>
    /// Features for stock ranking
    /// </summary>
    public class StockRankingFeatures
    {
        public string Symbol { get; set; } = string.Empty;
        
        // Fundamental features
        public float MarketCap { get; set; }
        public float PriceToEarnings { get; set; }
        public float DividendYield { get; set; }
        public float PriceToBook { get; set; }
        public float DebtToEquity { get; set; }
        
        // Technical features
        public float Beta { get; set; }
        public float VolatilityScore { get; set; }
        public float MomentumScore { get; set; }
        public float RelativeStrength { get; set; }
        
        // Volume and liquidity
        public float AverageVolume { get; set; }
        public float VolumeRatio { get; set; }
        public float LiquidityScore { get; set; }
        
        // Performance
        public float Return1Day { get; set; }
        public float Return5Day { get; set; }
        public float Return30Day { get; set; }
        
        // Composite scores
        public float TechnicalScore { get; set; }
        public float FundamentalScore { get; set; }
        public float SentimentScore { get; set; }
    }
    
    /// <summary>
    /// Stock fundamentals data
    /// </summary>
    public class StockFundamentals
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal MarketCap { get; set; }
        public decimal PriceToEarnings { get; set; }
        public decimal DividendYield { get; set; }
        public decimal PriceToBook { get; set; }
        public decimal DebtToEquity { get; set; }
    }
    
    /// <summary>
    /// Market metrics for ranking
    /// </summary>
    public class MarketMetrics
    {
        public decimal Beta { get; set; }
        public decimal Volatility { get; set; }
        public decimal Momentum { get; set; }
        public decimal RelativeStrength { get; set; }
        public decimal RSI { get; set; }
        public decimal AverageVolume { get; set; }
        public decimal VolumeRatio { get; set; }
        public decimal LiquidityScore { get; set; }
        public decimal Return1Day { get; set; }
        public decimal Return5Day { get; set; }
        public decimal Return30Day { get; set; }
        public decimal SentimentScore { get; set; }
    }
}