using System;
using System.Collections.Generic;

namespace TradingPlatform.ML.Models
{
    /// <summary>
    /// Result of a single model prediction
    /// </summary>
    public class ModelPrediction
    {
        /// <summary>
        /// Gets or sets the model name used for prediction
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the prediction values
        /// </summary>
        public float[] Predictions { get; set; } = Array.Empty<float>();

        /// <summary>
        /// Gets or sets the confidence scores if available
        /// </summary>
        public float[]? Confidences { get; set; }

        /// <summary>
        /// Gets or sets the inference time in milliseconds
        /// </summary>
        public double InferenceTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the prediction
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Result of batch model predictions
    /// </summary>
    public class ModelPredictionBatch
    {
        /// <summary>
        /// Gets or sets the model name used for predictions
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the batch of predictions
        /// </summary>
        public List<ModelPrediction> Predictions { get; set; } = new();

        /// <summary>
        /// Gets or sets the total batch inference time in milliseconds
        /// </summary>
        public double TotalInferenceTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the average inference time per item
        /// </summary>
        public double AverageInferenceTimeMs => 
            Predictions.Count > 0 ? TotalInferenceTimeMs / Predictions.Count : 0;

        /// <summary>
        /// Gets or sets the batch size
        /// </summary>
        public int BatchSize => Predictions.Count;
    }

    /// <summary>
    /// Model metadata information
    /// </summary>
    public class ModelMetadata
    {
        /// <summary>
        /// Gets or sets the model name
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the model version
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Gets or sets the model description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the input tensor metadata
        /// </summary>
        public List<TensorMetadata> InputMetadata { get; set; } = new();

        /// <summary>
        /// Gets or sets the output tensor metadata
        /// </summary>
        public List<TensorMetadata> OutputMetadata { get; set; } = new();

        /// <summary>
        /// Gets or sets when the model was loaded
        /// </summary>
        public DateTime LoadedAt { get; set; }

        /// <summary>
        /// Gets or sets the model file size in bytes
        /// </summary>
        public long ModelSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets custom properties
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new();
    }

    /// <summary>
    /// Tensor metadata information
    /// </summary>
    public class TensorMetadata
    {
        /// <summary>
        /// Gets or sets the tensor name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the tensor shape
        /// </summary>
        public int[] Shape { get; set; } = Array.Empty<int>();

        /// <summary>
        /// Gets or sets the element type
        /// </summary>
        public string ElementType { get; set; } = "float32";

        /// <summary>
        /// Gets or sets whether this tensor is optional
        /// </summary>
        public bool IsOptional { get; set; }

        /// <summary>
        /// Gets the total number of elements
        /// </summary>
        public long TotalElements
        {
            get
            {
                long total = 1;
                foreach (var dim in Shape)
                {
                    if (dim > 0) total *= dim;
                }
                return total;
            }
        }
    }

    /// <summary>
    /// Model warmup statistics
    /// </summary>
    public class WarmupStatistics
    {
        /// <summary>
        /// Gets or sets the model name
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of warmup iterations performed
        /// </summary>
        public int Iterations { get; set; }

        /// <summary>
        /// Gets or sets the minimum inference time observed
        /// </summary>
        public double MinInferenceTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the maximum inference time observed
        /// </summary>
        public double MaxInferenceTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the average inference time
        /// </summary>
        public double AverageInferenceTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the standard deviation of inference times
        /// </summary>
        public double StdDevInferenceTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the total warmup duration
        /// </summary>
        public double TotalWarmupTimeMs { get; set; }
    }

    /// <summary>
    /// Model performance metrics
    /// </summary>
    public class ModelPerformanceMetrics
    {
        /// <summary>
        /// Gets or sets the model name
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of inferences
        /// </summary>
        public long TotalInferences { get; set; }

        /// <summary>
        /// Gets or sets the number of successful inferences
        /// </summary>
        public long SuccessfulInferences { get; set; }

        /// <summary>
        /// Gets or sets the total latency in milliseconds
        /// </summary>
        public double TotalLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the minimum latency observed
        /// </summary>
        public double MinLatencyMs { get; set; } = double.MaxValue;

        /// <summary>
        /// Gets or sets the maximum latency observed
        /// </summary>
        public double MaxLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the target latency in milliseconds
        /// </summary>
        public double TargetLatencyMs { get; set; } = 100;

        /// <summary>
        /// Gets or sets when metrics were last updated
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Gets the average latency
        /// </summary>
        public double AverageLatencyMs => 
            TotalInferences > 0 ? TotalLatencyMs / TotalInferences : 0;

        /// <summary>
        /// Gets the success rate
        /// </summary>
        public double SuccessRate => 
            TotalInferences > 0 ? (double)SuccessfulInferences / TotalInferences : 0;
    }

    /// <summary>
    /// ML health report
    /// </summary>
    public class MLHealthReport
    {
        /// <summary>
        /// Gets or sets the report timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets model-specific metrics
        /// </summary>
        public List<ModelPerformanceMetrics> ModelMetrics { get; set; } = new();

        /// <summary>
        /// Gets or sets system health information
        /// </summary>
        public SystemHealthInfo SystemHealth { get; set; } = new();

        /// <summary>
        /// Gets or sets any warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Gets or sets any errors
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Gets whether the system is healthy
        /// </summary>
        public bool IsHealthy => Errors.Count == 0 && Warnings.Count == 0;
    }

    /// <summary>
    /// System health information
    /// </summary>
    public class SystemHealthInfo
    {
        /// <summary>
        /// Gets or sets GPU utilization percentage
        /// </summary>
        public double GpuUtilization { get; set; }

        /// <summary>
        /// Gets or sets GPU memory usage in bytes
        /// </summary>
        public long GpuMemoryUsedBytes { get; set; }

        /// <summary>
        /// Gets or sets total GPU memory in bytes
        /// </summary>
        public long GpuMemoryTotalBytes { get; set; }

        /// <summary>
        /// Gets or sets CPU utilization percentage
        /// </summary>
        public double CpuUtilization { get; set; }

        /// <summary>
        /// Gets or sets system memory usage in bytes
        /// </summary>
        public long SystemMemoryUsedBytes { get; set; }

        /// <summary>
        /// Gets or sets whether GPU is available
        /// </summary>
        public bool IsGpuAvailable { get; set; }

        /// <summary>
        /// Gets GPU memory utilization percentage
        /// </summary>
        public double GpuMemoryUtilization => 
            GpuMemoryTotalBytes > 0 ? (double)GpuMemoryUsedBytes / GpuMemoryTotalBytes * 100 : 0;
    }

    /// <summary>
    /// Order book snapshot for ML processing
    /// </summary>
    public class OrderBookSnapshot
    {
        /// <summary>
        /// Gets or sets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the symbol
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets bid levels
        /// </summary>
        public List<PriceLevel> Bids { get; set; } = new();

        /// <summary>
        /// Gets or sets ask levels
        /// </summary>
        public List<PriceLevel> Asks { get; set; } = new();

        /// <summary>
        /// Gets the spread
        /// </summary>
        public decimal Spread => Asks.Count > 0 && Bids.Count > 0 ? 
            Asks[0].Price - Bids[0].Price : 0;

        /// <summary>
        /// Gets the mid price
        /// </summary>
        public decimal MidPrice => Asks.Count > 0 && Bids.Count > 0 ? 
            (Asks[0].Price + Bids[0].Price) / 2 : 0;

        /// <summary>
        /// Gets the order book imbalance
        /// </summary>
        public decimal Imbalance => TotalBidVolume + TotalAskVolume > 0 ?
            (TotalBidVolume - TotalAskVolume) / (TotalBidVolume + TotalAskVolume) : 0;

        /// <summary>
        /// Gets total bid volume
        /// </summary>
        public decimal TotalBidVolume { get; set; }

        /// <summary>
        /// Gets total ask volume
        /// </summary>
        public decimal TotalAskVolume { get; set; }
    }

    /// <summary>
    /// Price level in order book
    /// </summary>
    public class PriceLevel
    {
        /// <summary>
        /// Gets or sets the price
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Gets or sets the volume
        /// </summary>
        public decimal Volume { get; set; }

        /// <summary>
        /// Gets or sets the number of orders
        /// </summary>
        public int OrderCount { get; set; }
    }

    /// <summary>
    /// Order book prediction result
    /// </summary>
    public class OrderBookPrediction
    {
        /// <summary>
        /// Gets or sets predicted next bid price
        /// </summary>
        public float NextBidPrice { get; set; }

        /// <summary>
        /// Gets or sets predicted next ask price
        /// </summary>
        public float NextAskPrice { get; set; }

        /// <summary>
        /// Gets or sets predicted price direction
        /// </summary>
        public Direction PriceDirection { get; set; }

        /// <summary>
        /// Gets or sets volatility forecast
        /// </summary>
        public float VolatilityForecast { get; set; }

        /// <summary>
        /// Gets or sets liquidity score
        /// </summary>
        public float LiquidityScore { get; set; }

        /// <summary>
        /// Gets or sets prediction confidence
        /// </summary>
        public float Confidence { get; set; }

        /// <summary>
        /// Gets an empty prediction
        /// </summary>
        public static OrderBookPrediction Empty => new();
    }

    /// <summary>
    /// Price direction
    /// </summary>
    public enum Direction
    {
        Down = -1,
        Neutral = 0,
        Up = 1
    }

    /// <summary>
    /// Price impact prediction
    /// </summary>
    public class PriceImpactPrediction
    {
        /// <summary>
        /// Gets or sets the expected price impact in basis points
        /// </summary>
        public decimal ExpectedImpactBps { get; set; }

        /// <summary>
        /// Gets or sets the temporary impact component
        /// </summary>
        public decimal TemporaryImpactBps { get; set; }

        /// <summary>
        /// Gets or sets the permanent impact component
        /// </summary>
        public decimal PermanentImpactBps { get; set; }

        /// <summary>
        /// Gets or sets the expected execution price
        /// </summary>
        public decimal ExpectedExecutionPrice { get; set; }

        /// <summary>
        /// Gets or sets the confidence interval
        /// </summary>
        public ConfidenceInterval ImpactConfidenceInterval { get; set; } = new();
    }

    /// <summary>
    /// Confidence interval
    /// </summary>
    public class ConfidenceInterval
    {
        /// <summary>
        /// Gets or sets the lower bound
        /// </summary>
        public decimal Lower { get; set; }

        /// <summary>
        /// Gets or sets the upper bound
        /// </summary>
        public decimal Upper { get; set; }

        /// <summary>
        /// Gets or sets the confidence level (e.g., 0.95 for 95%)
        /// </summary>
        public decimal ConfidenceLevel { get; set; } = 0.95m;
    }

    /// <summary>
    /// Social media post for sentiment analysis
    /// </summary>
    public class SocialPost
    {
        /// <summary>
        /// Gets or sets the post ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the text content
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the author
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the source platform
        /// </summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets engagement metrics
        /// </summary>
        public EngagementMetrics Engagement { get; set; } = new();
    }

    /// <summary>
    /// Social media engagement metrics
    /// </summary>
    public class EngagementMetrics
    {
        /// <summary>
        /// Gets or sets the number of likes
        /// </summary>
        public int Likes { get; set; }

        /// <summary>
        /// Gets or sets the number of shares/retweets
        /// </summary>
        public int Shares { get; set; }

        /// <summary>
        /// Gets or sets the number of comments
        /// </summary>
        public int Comments { get; set; }

        /// <summary>
        /// Gets or sets the reach/impressions
        /// </summary>
        public int Reach { get; set; }
    }

    /// <summary>
    /// News article for sentiment analysis
    /// </summary>
    public class NewsArticle
    {
        /// <summary>
        /// Gets or sets the article ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the headline
        /// </summary>
        public string Headline { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the article body
        /// </summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the author
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the publish timestamp
        /// </summary>
        public DateTime PublishedAt { get; set; }

        /// <summary>
        /// Gets or sets related symbols
        /// </summary>
        public List<string> RelatedSymbols { get; set; } = new();
    }

    /// <summary>
    /// Sentiment analysis result
    /// </summary>
    public class SentimentAnalysis
    {
        /// <summary>
        /// Gets or sets the analyzed symbol
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the bullish sentiment score (0-1)
        /// </summary>
        public float BullishScore { get; set; }

        /// <summary>
        /// Gets or sets the neutral sentiment score (0-1)
        /// </summary>
        public float NeutralScore { get; set; }

        /// <summary>
        /// Gets or sets the bearish sentiment score (0-1)
        /// </summary>
        public float BearishScore { get; set; }

        /// <summary>
        /// Gets or sets the momentum score (-1 to 1)
        /// </summary>
        public float MomentumScore { get; set; }

        /// <summary>
        /// Gets or sets the sample size
        /// </summary>
        public int SampleSize { get; set; }

        /// <summary>
        /// Gets or sets the analysis time window
        /// </summary>
        public TimeSpan TimeWindow { get; set; }

        /// <summary>
        /// Gets or sets whether unusual activity was detected
        /// </summary>
        public bool UnusualActivity { get; set; }

        /// <summary>
        /// Gets the overall sentiment
        /// </summary>
        public string OverallSentiment
        {
            get
            {
                if (BullishScore > BearishScore && BullishScore > NeutralScore)
                    return "Bullish";
                if (BearishScore > BullishScore && BearishScore > NeutralScore)
                    return "Bearish";
                return "Neutral";
            }
        }
    }

    /// <summary>
    /// News sentiment analysis result
    /// </summary>
    public class NewsSentiment : SentimentAnalysis
    {
        /// <summary>
        /// Gets or sets the credibility score
        /// </summary>
        public float CredibilityScore { get; set; }

        /// <summary>
        /// Gets or sets the relevance score
        /// </summary>
        public float RelevanceScore { get; set; }

        /// <summary>
        /// Gets or sets key topics extracted
        /// </summary>
        public List<string> KeyTopics { get; set; } = new();

        /// <summary>
        /// Gets or sets entities mentioned
        /// </summary>
        public List<string> Entities { get; set; } = new();
    }
}