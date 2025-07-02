// File: TradingPlatform.ML/Data/MLDatasetBuilder.cs

using Microsoft.ML;
using Microsoft.ML.Data;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Models;
using TradingPlatform.ML.Features;
using TradingPlatform.ML.Models;

namespace TradingPlatform.ML.Data
{
    /// <summary>
    /// Builds ML.NET compatible datasets from market data
    /// </summary>
    public class MLDatasetBuilder
    {
        private readonly MLContext _mlContext;
        private readonly DataPreprocessor _preprocessor;
        private readonly FeaturePipeline _featurePipeline;
        
        public MLDatasetBuilder(MLContext mlContext)
        {
            _mlContext = mlContext ?? throw new ArgumentNullException(nameof(mlContext));
            _preprocessor = new DataPreprocessor(mlContext);
            _featurePipeline = new FeaturePipeline(mlContext);
        }
        
        /// <summary>
        /// Build dataset for XGBoost price prediction
        /// </summary>
        public MLDataset BuildPricePredictionDataset(
            MarketDataset marketDataset,
            DatasetOptions options = null)
        {
            options ??= DatasetOptions.Default;
            
            // Preprocess data
            var processedData = _preprocessor.PreprocessForPricePrediction(
                marketDataset.Data, 
                marketDataset.Symbol,
                options.LookbackPeriod);
            
            // Apply feature engineering
            var engineeredData = _featurePipeline.ApplyFeatureEngineering(
                processedData, "PricePrediction");
            
            // Create train/validation/test splits
            var splits = CreateDataSplits(engineeredData, options);
            
            return new MLDataset
            {
                Name = $"PricePrediction_{marketDataset.Symbol}_{DateTime.UtcNow:yyyyMMdd}",
                Type = MLDatasetType.PricePrediction,
                Symbol = marketDataset.Symbol,
                TrainData = splits.train,
                ValidationData = splits.validation,
                TestData = splits.test,
                FeatureColumns = GetPricePredictionFeatures(),
                LabelColumn = "NextPrice",
                CreatedAt = DateTime.UtcNow,
                Options = options,
                SampleCount = CountSamples(engineeredData)
            };
        }
        
        /// <summary>
        /// Build dataset for LSTM pattern recognition
        /// </summary>
        public MLDataset BuildPatternRecognitionDataset(
            MarketDataset marketDataset,
            DatasetOptions options = null)
        {
            options ??= DatasetOptions.Default;
            
            // Preprocess sequential data
            var processedData = _preprocessor.PreprocessForPatternRecognition(
                marketDataset.Data,
                options.SequenceLength,
                options.StepSize);
            
            // Apply feature engineering
            var engineeredData = _featurePipeline.ApplyFeatureEngineering(
                processedData, "PatternRecognition");
            
            // Create splits
            var splits = CreateDataSplits(engineeredData, options);
            
            return new MLDataset
            {
                Name = $"PatternRecognition_{marketDataset.Symbol}_{DateTime.UtcNow:yyyyMMdd}",
                Type = MLDatasetType.PatternRecognition,
                Symbol = marketDataset.Symbol,
                TrainData = splits.train,
                ValidationData = splits.validation,
                TestData = splits.test,
                FeatureColumns = GetPatternFeatures(),
                LabelColumn = "PatternLabel",
                CreatedAt = DateTime.UtcNow,
                Options = options,
                SampleCount = CountSamples(engineeredData)
            };
        }
        
        /// <summary>
        /// Build dataset for stock ranking
        /// </summary>
        public MLDataset BuildStockRankingDataset(
            IList<StockFundamentals> fundamentals,
            Dictionary<string, MarketMetrics> marketMetrics,
            DatasetOptions options = null)
        {
            options ??= DatasetOptions.Default;
            
            // Preprocess ranking data
            var processedData = _preprocessor.PreprocessForStockRanking(
                fundamentals, marketMetrics);
            
            // Apply feature engineering
            var engineeredData = _featurePipeline.ApplyFeatureEngineering(
                processedData, "StockRanking");
            
            // For ranking, we typically don't split the same way
            // Instead, we might use time-based splits or cross-validation
            var allData = engineeredData;
            
            return new MLDataset
            {
                Name = $"StockRanking_{DateTime.UtcNow:yyyyMMdd}",
                Type = MLDatasetType.StockRanking,
                Symbol = "MULTI",
                TrainData = allData,
                ValidationData = allData, // Will be handled differently in training
                TestData = allData,
                FeatureColumns = GetRankingFeatures(),
                LabelColumn = "RankingScore",
                CreatedAt = DateTime.UtcNow,
                Options = options,
                SampleCount = CountSamples(allData)
            };
        }
        
        /// <summary>
        /// Create train/validation/test splits
        /// </summary>
        private (IDataView train, IDataView validation, IDataView test) CreateDataSplits(
            IDataView data, 
            DatasetOptions options)
        {
            // For time series, we use chronological splits
            var totalRows = CountSamples(data);
            var trainSize = (int)(totalRows * options.TrainRatio);
            var validationSize = (int)(totalRows * options.ValidationRatio);
            
            // Create row ranges
            var trainData = _mlContext.Data.TakeRows(data, trainSize);
            var remainingData = _mlContext.Data.SkipRows(data, trainSize);
            var validationData = _mlContext.Data.TakeRows(remainingData, validationSize);
            var testData = _mlContext.Data.SkipRows(remainingData, validationSize);
            
            // Cache in memory for performance
            trainData = _mlContext.Data.Cache(trainData);
            validationData = _mlContext.Data.Cache(validationData);
            testData = _mlContext.Data.Cache(testData);
            
            return (trainData, validationData, testData);
        }
        
        /// <summary>
        /// Augment dataset with synthetic samples
        /// </summary>
        public IDataView AugmentDataset(IDataView data, AugmentationOptions options)
        {
            var augmentedData = data;
            
            if (options.AddNoise)
            {
                augmentedData = AddGaussianNoise(augmentedData, options.NoiseLevel);
            }
            
            if (options.AddTimeShift)
            {
                augmentedData = AddTimeShiftedSamples(augmentedData, options.TimeShiftSteps);
            }
            
            if (options.AddSyntheticSamples)
            {
                augmentedData = GenerateSyntheticSamples(augmentedData, options.SyntheticRatio);
            }
            
            return augmentedData;
        }
        
        /// <summary>
        /// Balance dataset for classification tasks
        /// </summary>
        public IDataView BalanceDataset(IDataView data, string labelColumn)
        {
            // Count samples per class
            var classCounts = GetClassDistribution(data, labelColumn);
            
            // Find minority class
            var minCount = classCounts.Values.Min();
            
            // Undersample majority classes or oversample minority
            var balancedData = data;
            
            foreach (var (className, count) in classCounts)
            {
                if (count > minCount * 1.5) // If significantly imbalanced
                {
                    // Undersample this class
                    var classData = _mlContext.Data.FilterRowsByColumn(
                        data, labelColumn, className, className);
                    var sampledData = _mlContext.Data.TakeRows(classData, minCount);
                    
                    // Combine with other classes
                    // (Implementation depends on ML.NET version)
                }
            }
            
            return balancedData;
        }
        
        /// <summary>
        /// Add Gaussian noise for regularization
        /// </summary>
        private IDataView AddGaussianNoise(IDataView data, decimal noiseLevel)
        {
            var pipeline = _mlContext.Transforms.CustomMapping<NoiseInput, NoiseOutput>(
                (input, output) =>
                {
                    output.Features = new decimal[input.Features.Length];
                    
                    for (int i = 0; i < input.Features.Length; i++)
                    {
                        var noise = (DecimalRandomCanonical.Instance.NextDecimal() - 0.5m) * noiseLevel;
                        output.Features[i] = input.Features[i] + noise;
                    }
                },
                contractName: "AddNoise");
            
            return pipeline.Fit(data).Transform(data);
        }
        
        /// <summary>
        /// Add time-shifted samples
        /// </summary>
        private IDataView AddTimeShiftedSamples(IDataView data, int shiftSteps)
        {
            // Implementation would shift features by N time steps
            // This helps model learn temporal invariance
            return data;
        }
        
        /// <summary>
        /// Generate synthetic samples using SMOTE-like technique
        /// </summary>
        private IDataView GenerateSyntheticSamples(IDataView data, decimal syntheticRatio)
        {
            // Implementation would generate synthetic samples
            // between existing samples in feature space
            return data;
        }
        
        /// <summary>
        /// Get class distribution for balanced training
        /// </summary>
        private Dictionary<string, int> GetClassDistribution(IDataView data, string labelColumn)
        {
            var distribution = new Dictionary<string, int>();
            
            var column = data.GetColumn<string>(labelColumn);
            foreach (var label in column)
            {
                if (!distribution.ContainsKey(label))
                    distribution[label] = 0;
                distribution[label]++;
            }
            
            return distribution;
        }
        
        /// <summary>
        /// Count samples in dataset
        /// </summary>
        private int CountSamples(IDataView data)
        {
            return (int)data.GetRowCount() ?? 0;
        }
        
        /// <summary>
        /// Get feature column names for price prediction
        /// </summary>
        private string[] GetPricePredictionFeatures()
        {
            return new[]
            {
                "Open", "High", "Low", "Close", "Volume",
                "RSI", "MACD", "BollingerUpper", "BollingerLower",
                "SMA20", "SMA50", "VolumeRatio", "VWAP",
                "PriceChangePercent", "DailyRange",
                "BidAskSpread", "RelativeSpread", "RealizedVolatility",
                "HourOfDay", "DayOfWeek", "IsMarketOpen", "MinutesFromOpen"
            };
        }
        
        /// <summary>
        /// Get feature column names for pattern recognition
        /// </summary>
        private string[] GetPatternFeatures()
        {
            return new[]
            {
                "PriceMean", "PriceStd", "PriceMin", "PriceMax", "PriceRange",
                "TrendSlope", "TrendStrength", "VolumeMean", "VolumeStd",
                "VolumeAccumulation", "Complexity", "Fractality"
            };
        }
        
        /// <summary>
        /// Get feature column names for stock ranking
        /// </summary>
        private string[] GetRankingFeatures()
        {
            return new[]
            {
                "MarketCap", "PriceToEarnings", "DividendYield", "PriceToBook", "DebtToEquity",
                "Beta", "VolatilityScore", "MomentumScore", "RelativeStrength",
                "AverageVolume", "VolumeRatio", "LiquidityScore",
                "Return1Day", "Return5Day", "Return30Day",
                "TechnicalScore", "FundamentalScore", "SentimentScore"
            };
        }
    }
    
    /// <summary>
    /// ML dataset with splits and metadata
    /// </summary>
    public class MLDataset : IMLDataset
    {
        public string Name { get; set; } = string.Empty;
        public MLDatasetType Type { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public IDataView TrainData { get; set; } = null!;
        public IDataView ValidationData { get; set; } = null!;
        public IDataView TestData { get; set; } = null!;
        public string[] FeatureColumns { get; set; } = Array.Empty<string>();
        public string LabelColumn { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DatasetOptions Options { get; set; } = null!;
        public int SampleCount { get; set; }
        
        public IDataView GetDataView() => TrainData;
    }
    
    /// <summary>
    /// Dataset configuration options
    /// </summary>
    public class DatasetOptions
    {
        public decimal TrainRatio { get; set; } = 0.7m;
        public decimal ValidationRatio { get; set; } = 0.15m;
        public decimal TestRatio { get; set; } = 0.15m;
        public int LookbackPeriod { get; set; } = 50;
        public int SequenceLength { get; set; } = 60;
        public int StepSize { get; set; } = 1;
        public bool Normalize { get; set; } = true;
        public bool RemoveOutliers { get; set; } = true;
        public decimal OutlierThreshold { get; set; } = 3.0m; // Standard deviations
        
        public static DatasetOptions Default => new();
    }
    
    /// <summary>
    /// Data augmentation options
    /// </summary>
    public class AugmentationOptions
    {
        public bool AddNoise { get; set; } = true;
        public decimal NoiseLevel { get; set; } = 0.01m;
        public bool AddTimeShift { get; set; } = true;
        public int TimeShiftSteps { get; set; } = 5;
        public bool AddSyntheticSamples { get; set; } = false;
        public decimal SyntheticRatio { get; set; } = 0.2m;
    }
    
    /// <summary>
    /// Dataset type enumeration
    /// </summary>
    public enum MLDatasetType
    {
        PricePrediction,
        PatternRecognition,
        StockRanking,
        RiskPrediction,
        VolumeForecasting
    }
    
    // Custom mapping types
    public class NoiseInput
    {
        public decimal[] Features { get; set; } = Array.Empty<decimal>();
    }
    
    public class NoiseOutput
    {
        public decimal[] Features { get; set; } = Array.Empty<decimal>();
    }
}