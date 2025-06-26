// File: TradingPlatform.ML/Features/FeaturePipeline.cs

using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms;
using TradingPlatform.Core.Models;
using System.Collections.Concurrent;

namespace TradingPlatform.ML.Features
{
    /// <summary>
    /// Advanced feature engineering pipeline for ML models
    /// </summary>
    public class FeaturePipeline
    {
        private readonly MLContext _mlContext;
        private readonly ConcurrentDictionary<string, ITransformer> _cachedPipelines;
        
        public FeaturePipeline(MLContext mlContext)
        {
            _mlContext = mlContext ?? throw new ArgumentNullException(nameof(mlContext));
            _cachedPipelines = new ConcurrentDictionary<string, ITransformer>();
        }
        
        /// <summary>
        /// Build feature pipeline for price prediction
        /// </summary>
        public EstimatorChain<RegressionPredictionTransformer<Microsoft.ML.Trainers.FastTree.FastTreeRegressionModelParameters>> 
            BuildPricePredictionPipeline()
        {
            // Define feature columns
            var featureColumns = new[]
            {
                "Open", "High", "Low", "Close", "Volume",
                "RSI", "MACD", "BollingerUpper", "BollingerLower",
                "SMA20", "SMA50", "VolumeRatio", "VWAP",
                "PriceChangePercent", "DailyRange",
                "BidAskSpread", "RelativeSpread", "RealizedVolatility",
                "HourOfDay", "DayOfWeek", "IsMarketOpen", "MinutesFromOpen"
            };
            
            // Build pipeline
            var pipeline = _mlContext.Transforms.Concatenate("Features", featureColumns)
                // Handle missing values
                .Append(_mlContext.Transforms.ReplaceMissingValues("Features", 
                    replacementMode: MissingValueReplacingEstimator.ReplacementMode.Mean))
                // Normalize features
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                // Add polynomial features for non-linear relationships
                .Append(CreatePolynomialFeatures())
                // Feature selection to reduce dimensionality
                .Append(_mlContext.Transforms.SelectFeaturesBasedOnMutualInformation(
                    "Features", "NextPrice", maximumNumberOfFeatures: 50))
                // Add the trainer (will be replaced with XGBoost)
                .Append(_mlContext.Regression.Trainers.FastTree(
                    labelColumnName: "NextPrice",
                    featureColumnName: "Features",
                    numberOfTrees: 100,
                    minimumExampleCountPerLeaf: 10,
                    learningRate: 0.1));
            
            return pipeline;
        }
        
        /// <summary>
        /// Build feature pipeline for pattern recognition
        /// </summary>
        public EstimatorChain<ITransformer> BuildPatternRecognitionPipeline()
        {
            // This will be a more complex pipeline for LSTM
            // For now, create a basic pipeline structure
            var pipeline = _mlContext.Transforms.CustomMapping<PatternSequenceInput, PatternFeatures>(
                (input, output) => ExtractPatternFeatures(input, output),
                contractName: "PatternFeatureExtraction")
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"));
            
            return pipeline;
        }
        
        /// <summary>
        /// Build feature pipeline for stock ranking
        /// </summary>
        public EstimatorChain<ITransformer> BuildStockRankingPipeline()
        {
            var fundamentalFeatures = new[] 
            { 
                "MarketCap", "PriceToEarnings", "DividendYield", 
                "PriceToBook", "DebtToEquity" 
            };
            
            var technicalFeatures = new[] 
            { 
                "Beta", "VolatilityScore", "MomentumScore", 
                "RelativeStrength", "TechnicalScore" 
            };
            
            var performanceFeatures = new[] 
            { 
                "Return1Day", "Return5Day", "Return30Day",
                "AverageVolume", "VolumeRatio", "LiquidityScore"
            };
            
            var pipeline = _mlContext.Transforms.Concatenate("FundamentalFeatures", fundamentalFeatures)
                .Append(_mlContext.Transforms.Concatenate("TechnicalFeatures", technicalFeatures))
                .Append(_mlContext.Transforms.Concatenate("PerformanceFeatures", performanceFeatures))
                // Normalize each feature group separately
                .Append(_mlContext.Transforms.NormalizeMinMax("FundamentalFeatures"))
                .Append(_mlContext.Transforms.NormalizeMinMax("TechnicalFeatures"))
                .Append(_mlContext.Transforms.NormalizeMinMax("PerformanceFeatures"))
                // Combine all features
                .Append(_mlContext.Transforms.Concatenate("Features", 
                    "FundamentalFeatures", "TechnicalFeatures", "PerformanceFeatures"))
                // Add interaction features
                .Append(CreateInteractionFeatures());
            
            return pipeline;
        }
        
        /// <summary>
        /// Create polynomial features for capturing non-linear relationships
        /// </summary>
        private IEstimator<ITransformer> CreatePolynomialFeatures()
        {
            return _mlContext.Transforms.CustomMapping<PolynomialInput, PolynomialOutput>(
                (input, output) =>
                {
                    // Create squared terms for key features
                    output.RSI_Squared = input.RSI * input.RSI;
                    output.Volume_Squared = input.VolumeRatio * input.VolumeRatio;
                    output.Volatility_Squared = input.RealizedVolatility * input.RealizedVolatility;
                    
                    // Create interaction terms
                    output.RSI_Volume = input.RSI * input.VolumeRatio;
                    output.Price_Volume = input.PriceChangePercent * input.VolumeRatio;
                    output.Volatility_Spread = input.RealizedVolatility * input.RelativeSpread;
                },
                contractName: "PolynomialFeatures");
        }
        
        /// <summary>
        /// Create interaction features between different feature groups
        /// </summary>
        private IEstimator<ITransformer> CreateInteractionFeatures()
        {
            return _mlContext.Transforms.CustomMapping<InteractionInput, InteractionOutput>(
                (input, output) =>
                {
                    // Fundamental × Technical interactions
                    output.PE_Momentum = input.PriceToEarnings * input.MomentumScore;
                    output.MarketCap_Volatility = input.MarketCap * input.VolatilityScore;
                    
                    // Technical × Performance interactions
                    output.Beta_Returns = input.Beta * input.Return30Day;
                    output.RSI_Volume = input.RelativeStrength * input.VolumeRatio;
                    
                    // Composite risk score
                    output.RiskScore = (input.Beta * input.VolatilityScore * input.DebtToEquity) / 
                                      (input.MarketCap * 0.000001f); // Normalize by market cap
                },
                contractName: "InteractionFeatures");
        }
        
        /// <summary>
        /// Extract features from pattern sequences
        /// </summary>
        private void ExtractPatternFeatures(PatternSequenceInput input, PatternFeatures output)
        {
            // Extract statistical features from sequences
            var prices = input.PriceSequence.Select(p => p[3]).ToArray(); // Close prices
            var volumes = input.VolumeSequence.Select(v => v[0]).ToArray();
            
            // Price features
            output.PriceMean = prices.Average();
            output.PriceStd = CalculateStandardDeviation(prices);
            output.PriceMin = prices.Min();
            output.PriceMax = prices.Max();
            output.PriceRange = output.PriceMax - output.PriceMin;
            
            // Trend features
            output.TrendSlope = CalculateTrendSlope(prices);
            output.TrendStrength = CalculateTrendStrength(prices);
            
            // Volume features
            output.VolumeMean = volumes.Average();
            output.VolumeStd = CalculateStandardDeviation(volumes);
            output.VolumeAccumulation = CalculateVolumeAccumulation(prices, volumes);
            
            // Pattern complexity
            output.Complexity = CalculatePatternComplexity(prices);
            output.Fractality = CalculateFractality(prices);
        }
        
        /// <summary>
        /// Apply feature engineering transformations
        /// </summary>
        public IDataView ApplyFeatureEngineering(IDataView data, string pipelineType)
        {
            if (_cachedPipelines.TryGetValue(pipelineType, out var cachedPipeline))
            {
                return cachedPipeline.Transform(data);
            }
            
            IEstimator<ITransformer> pipeline = pipelineType switch
            {
                "PricePrediction" => BuildPricePredictionPipeline(),
                "PatternRecognition" => BuildPatternRecognitionPipeline(),
                "StockRanking" => BuildStockRankingPipeline(),
                _ => throw new ArgumentException($"Unknown pipeline type: {pipelineType}")
            };
            
            var transformer = pipeline.Fit(data);
            _cachedPipelines.TryAdd(pipelineType, transformer);
            
            return transformer.Transform(data);
        }
        
        // Helper methods
        private float CalculateStandardDeviation(float[] values)
        {
            var mean = values.Average();
            var sumSquares = values.Sum(v => (v - mean) * (v - mean));
            return (float)Math.Sqrt(sumSquares / values.Length);
        }
        
        private float CalculateTrendSlope(float[] prices)
        {
            // Simple linear regression slope
            var n = prices.Length;
            var sumX = 0f;
            var sumY = 0f;
            var sumXY = 0f;
            var sumX2 = 0f;
            
            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += prices[i];
                sumXY += i * prices[i];
                sumX2 += i * i;
            }
            
            return (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        }
        
        private float CalculateTrendStrength(float[] prices)
        {
            // R-squared of linear regression
            var slope = CalculateTrendSlope(prices);
            var mean = prices.Average();
            var intercept = mean - slope * (prices.Length - 1) / 2f;
            
            var ssTotal = prices.Sum(p => (p - mean) * (p - mean));
            var ssResidual = 0f;
            
            for (int i = 0; i < prices.Length; i++)
            {
                var predicted = intercept + slope * i;
                ssResidual += (prices[i] - predicted) * (prices[i] - predicted);
            }
            
            return 1f - (ssResidual / ssTotal);
        }
        
        private float CalculateVolumeAccumulation(float[] prices, float[] volumes)
        {
            // On-Balance Volume calculation
            var obv = 0f;
            for (int i = 1; i < prices.Length; i++)
            {
                if (prices[i] > prices[i - 1])
                    obv += volumes[i];
                else if (prices[i] < prices[i - 1])
                    obv -= volumes[i];
            }
            return obv;
        }
        
        private float CalculatePatternComplexity(float[] prices)
        {
            // Approximate entropy as complexity measure
            var m = 2; // Pattern length
            var r = 0.2f * CalculateStandardDeviation(prices); // Tolerance
            
            var phi_m = CalculateApproximateEntropy(prices, m, r);
            var phi_m1 = CalculateApproximateEntropy(prices, m + 1, r);
            
            return phi_m - phi_m1;
        }
        
        private float CalculateApproximateEntropy(float[] data, int m, float r)
        {
            var n = data.Length;
            var patterns = new List<float[]>();
            
            for (int i = 0; i <= n - m; i++)
            {
                var pattern = new float[m];
                Array.Copy(data, i, pattern, 0, m);
                patterns.Add(pattern);
            }
            
            var sum = 0.0;
            foreach (var pattern in patterns)
            {
                var count = patterns.Count(p => IsPatternSimilar(pattern, p, r));
                sum += Math.Log((double)count / (n - m + 1));
            }
            
            return (float)(sum / (n - m + 1));
        }
        
        private bool IsPatternSimilar(float[] p1, float[] p2, float tolerance)
        {
            for (int i = 0; i < p1.Length; i++)
            {
                if (Math.Abs(p1[i] - p2[i]) > tolerance)
                    return false;
            }
            return true;
        }
        
        private float CalculateFractality(float[] prices)
        {
            // Hurst exponent calculation (simplified)
            var returns = new float[prices.Length - 1];
            for (int i = 1; i < prices.Length; i++)
            {
                returns[i - 1] = (float)Math.Log(prices[i] / prices[i - 1]);
            }
            
            var mean = returns.Average();
            var std = CalculateStandardDeviation(returns);
            
            // Calculate R/S statistic
            var cumulativeDeviation = 0f;
            var maxDeviation = float.MinValue;
            var minDeviation = float.MaxValue;
            
            for (int i = 0; i < returns.Length; i++)
            {
                cumulativeDeviation += returns[i] - mean;
                maxDeviation = Math.Max(maxDeviation, cumulativeDeviation);
                minDeviation = Math.Min(minDeviation, cumulativeDeviation);
            }
            
            var range = maxDeviation - minDeviation;
            var rs = range / std;
            
            // Hurst exponent approximation
            return (float)(Math.Log(rs) / Math.Log(returns.Length));
        }
    }
    
    // Custom mapping classes
    public class PolynomialInput
    {
        public float RSI { get; set; }
        public float VolumeRatio { get; set; }
        public float RealizedVolatility { get; set; }
        public float PriceChangePercent { get; set; }
        public float RelativeSpread { get; set; }
    }
    
    public class PolynomialOutput
    {
        public float RSI_Squared { get; set; }
        public float Volume_Squared { get; set; }
        public float Volatility_Squared { get; set; }
        public float RSI_Volume { get; set; }
        public float Price_Volume { get; set; }
        public float Volatility_Spread { get; set; }
    }
    
    public class InteractionInput
    {
        public float PriceToEarnings { get; set; }
        public float MomentumScore { get; set; }
        public float MarketCap { get; set; }
        public float VolatilityScore { get; set; }
        public float Beta { get; set; }
        public float Return30Day { get; set; }
        public float RelativeStrength { get; set; }
        public float VolumeRatio { get; set; }
        public float DebtToEquity { get; set; }
    }
    
    public class InteractionOutput
    {
        public float PE_Momentum { get; set; }
        public float MarketCap_Volatility { get; set; }
        public float Beta_Returns { get; set; }
        public float RSI_Volume { get; set; }
        public float RiskScore { get; set; }
    }
    
    public class PatternSequenceInput
    {
        public float[][] PriceSequence { get; set; } = Array.Empty<float[]>();
        public float[][] VolumeSequence { get; set; } = Array.Empty<float[]>();
    }
    
    public class PatternFeatures
    {
        public float PriceMean { get; set; }
        public float PriceStd { get; set; }
        public float PriceMin { get; set; }
        public float PriceMax { get; set; }
        public float PriceRange { get; set; }
        public float TrendSlope { get; set; }
        public float TrendStrength { get; set; }
        public float VolumeMean { get; set; }
        public float VolumeStd { get; set; }
        public float VolumeAccumulation { get; set; }
        public float Complexity { get; set; }
        public float Fractality { get; set; }
    }
}