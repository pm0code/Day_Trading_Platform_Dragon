namespace MarketAnalyzer.Infrastructure.AI.Models;

/// <summary>
/// Base class for all ML model predictions.
/// </summary>
public abstract class PredictionBase
{
    /// <summary>
    /// Gets or sets the model name that generated this prediction.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the prediction was made.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the inference time in milliseconds.
    /// </summary>
    public double InferenceTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the confidence score (0-1).
    /// </summary>
    public float Confidence { get; set; }
}

/// <summary>
/// Represents a generic model prediction result.
/// </summary>
public class ModelPrediction : PredictionBase
{
    /// <summary>
    /// Gets or sets the raw prediction values.
    /// </summary>
    public float[] Predictions { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Gets or sets the prediction labels if available.
    /// </summary>
    public string[]? Labels { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = new();
}

/// <summary>
/// Represents batch input for model inference.
/// </summary>
public class BatchInput
{
    /// <summary>
    /// Gets or sets the batch data.
    /// </summary>
    public float[][] Data { get; set; } = Array.Empty<float[]>();

    /// <summary>
    /// Gets or sets the input shape for each sample.
    /// </summary>
    public int[] InputShape { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Gets or sets the batch size.
    /// </summary>
    public int BatchSize => Data.Length;
}

/// <summary>
/// Represents batch prediction results.
/// </summary>
public class BatchPrediction : PredictionBase
{
    /// <summary>
    /// Gets or sets the predictions for each input in the batch.
    /// </summary>
    public float[][] Predictions { get; set; } = Array.Empty<float[]>();

    /// <summary>
    /// Gets or sets individual inference times.
    /// </summary>
    public double[] InferenceTimesMs { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Gets or sets the total batch processing time.
    /// </summary>
    public double TotalBatchTimeMs { get; set; }
}

/// <summary>
/// Represents price movement prediction.
/// MANDATORY: All price values use decimal for financial precision.
/// </summary>
public class PricePrediction : PredictionBase
{
    /// <summary>
    /// Gets or sets the stock symbol.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the predicted prices. MANDATORY: decimal precision.
    /// </summary>
    public decimal[] PredictedPrices { get; set; } = Array.Empty<decimal>();

    /// <summary>
    /// Gets or sets the prediction intervals in minutes.
    /// </summary>
    public int[] TimeHorizons { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Gets or sets the upper confidence bounds. MANDATORY: decimal precision.
    /// </summary>
    public decimal[] UpperBounds { get; set; } = Array.Empty<decimal>();

    /// <summary>
    /// Gets or sets the lower confidence bounds. MANDATORY: decimal precision.
    /// </summary>
    public decimal[] LowerBounds { get; set; } = Array.Empty<decimal>();

    /// <summary>
    /// Gets or sets the predicted direction (1: up, -1: down, 0: neutral).
    /// </summary>
    public int PredictedDirection { get; set; }

    /// <summary>
    /// Gets or sets the predicted volatility. MANDATORY: decimal precision.
    /// </summary>
    public decimal PredictedVolatility { get; set; }
}

/// <summary>
/// Represents sentiment analysis results.
/// </summary>
public class SentimentAnalysis : PredictionBase
{
    /// <summary>
    /// Gets or sets the sentiment score (-1 to 1).
    /// </summary>
    public float SentimentScore { get; set; }

    /// <summary>
    /// Gets or sets the sentiment label.
    /// </summary>
    public SentimentLabel Label { get; set; }

    /// <summary>
    /// Gets or sets individual class probabilities.
    /// </summary>
    public Dictionary<string, float> ClassProbabilities { get; } = new();

    /// <summary>
    /// Gets or sets detected entities.
    /// </summary>
    public List<string> DetectedEntities { get; } = new();

    /// <summary>
    /// Gets or sets key phrases.
    /// </summary>
    public List<string> KeyPhrases { get; } = new();
}

/// <summary>
/// Sentiment labels.
/// </summary>
public enum SentimentLabel
{
    VeryNegative = -2,
    Negative = -1,
    Neutral = 0,
    Positive = 1,
    VeryPositive = 2
}

/// <summary>
/// Represents pattern detection results.
/// </summary>
public class PatternDetection : PredictionBase
{
    /// <summary>
    /// Gets or sets detected patterns.
    /// </summary>
    public List<DetectedPattern> Patterns { get; } = new();

    /// <summary>
    /// Gets or sets the overall pattern strength (0-1).
    /// </summary>
    public float PatternStrength { get; set; }
}

/// <summary>
/// Represents a detected pattern.
/// </summary>
public class DetectedPattern
{
    /// <summary>
    /// Gets or sets the pattern type.
    /// </summary>
    public string PatternType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pattern confidence.
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Gets or sets the pattern location (start index).
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// Gets or sets the pattern location (end index).
    /// </summary>
    public int EndIndex { get; set; }

    /// <summary>
    /// Gets or sets additional pattern properties.
    /// </summary>
    public Dictionary<string, object> Properties { get; } = new();
}

/// <summary>
/// Represents trading signal predictions.
/// </summary>
public class TradingSignalPrediction : PredictionBase
{
    /// <summary>
    /// Gets or sets the primary signal.
    /// </summary>
    public TradingSignal Signal { get; set; }

    /// <summary>
    /// Gets or sets the signal strength (0-1).
    /// </summary>
    public float SignalStrength { get; set; }

    /// <summary>
    /// Gets or sets the recommended position size (0-1).
    /// </summary>
    public float RecommendedPositionSize { get; set; }

    /// <summary>
    /// Gets or sets the stop loss level. MANDATORY: decimal precision.
    /// </summary>
    public decimal? StopLossLevel { get; set; }

    /// <summary>
    /// Gets or sets the take profit level. MANDATORY: decimal precision.
    /// </summary>
    public decimal? TakeProfitLevel { get; set; }

    /// <summary>
    /// Gets or sets the time horizon in minutes.
    /// </summary>
    public int TimeHorizonMinutes { get; set; }

    /// <summary>
    /// Gets or sets supporting indicators.
    /// </summary>
    public Dictionary<string, float> SupportingIndicators { get; } = new();
}

/// <summary>
/// Trading signals.
/// </summary>
public enum TradingSignal
{
    StrongSell = -2,
    Sell = -1,
    Hold = 0,
    Buy = 1,
    StrongBuy = 2
}

/// <summary>
/// Represents a trading position for risk assessment.
/// </summary>
public class TradingPosition
{
    /// <summary>
    /// Gets or sets the symbol.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the position size.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the entry price. MANDATORY: decimal precision.
    /// </summary>
    public decimal EntryPrice { get; set; }

    /// <summary>
    /// Gets or sets the current price. MANDATORY: decimal precision.
    /// </summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>
    /// Gets or sets whether this is a long position.
    /// </summary>
    public bool IsLong { get; set; }

    /// <summary>
    /// Gets or sets the position open time.
    /// </summary>
    public DateTime OpenTime { get; set; }
}

/// <summary>
/// Represents current market conditions.
/// </summary>
public class MarketConditions
{
    /// <summary>
    /// Gets or sets the market volatility (VIX).
    /// </summary>
    public float MarketVolatility { get; set; }

    /// <summary>
    /// Gets or sets the market trend (-1 to 1).
    /// </summary>
    public float MarketTrend { get; set; }

    /// <summary>
    /// Gets or sets the sector performance.
    /// </summary>
    public Dictionary<string, float> SectorPerformance { get; } = new();

    /// <summary>
    /// Gets or sets the market breadth indicators.
    /// </summary>
    public Dictionary<string, float> BreadthIndicators { get; } = new();
}

/// <summary>
/// Represents risk assessment results.
/// </summary>
public class RiskAssessment : PredictionBase
{
    /// <summary>
    /// Gets or sets the overall risk score (0-100).
    /// </summary>
    public float RiskScore { get; set; }

    /// <summary>
    /// Gets or sets the risk level.
    /// </summary>
    public RiskLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the Value at Risk (VaR). MANDATORY: decimal precision.
    /// </summary>
    public decimal ValueAtRisk { get; set; }

    /// <summary>
    /// Gets or sets the Expected Shortfall (ES). MANDATORY: decimal precision.
    /// </summary>
    public decimal ExpectedShortfall { get; set; }

    /// <summary>
    /// Gets or sets individual risk factors.
    /// </summary>
    public Dictionary<string, float> RiskFactors { get; } = new();

    /// <summary>
    /// Gets or sets risk mitigation recommendations.
    /// </summary>
    public List<string> Recommendations { get; } = new();
}

/// <summary>
/// Risk levels.
/// </summary>
public enum RiskLevel
{
    VeryLow = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    VeryHigh = 5,
    Critical = 6
}

/// <summary>
/// Represents model information and statistics.
/// </summary>
public class ModelInfo
{
    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model type (ONNX, ML.NET, TorchSharp).
    /// </summary>
    public string ModelType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model size in MB.
    /// </summary>
    public double ModelSizeMB { get; set; }

    /// <summary>
    /// Gets or sets whether the model is loaded.
    /// </summary>
    public bool IsLoaded { get; set; }

    /// <summary>
    /// Gets or sets the load time.
    /// </summary>
    public DateTime? LoadTime { get; set; }

    /// <summary>
    /// Gets or sets the total inference count.
    /// </summary>
    public long TotalInferenceCount { get; set; }

    /// <summary>
    /// Gets or sets the average inference time in ms.
    /// </summary>
    public double AverageInferenceTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the 95th percentile inference time.
    /// </summary>
    public double P95InferenceTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the 99th percentile inference time.
    /// </summary>
    public double P99InferenceTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the input metadata.
    /// </summary>
    public Dictionary<string, string> InputMetadata { get; } = new();

    /// <summary>
    /// Gets or sets the output metadata.
    /// </summary>
    public Dictionary<string, string> OutputMetadata { get; } = new();
}

/// <summary>
/// Represents ML service health status.
/// </summary>
public class MLHealthStatus
{
    /// <summary>
    /// Gets or sets whether the service is healthy.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the loaded model count.
    /// </summary>
    public int LoadedModelCount { get; set; }

    /// <summary>
    /// Gets or sets the total model count.
    /// </summary>
    public int TotalModelCount { get; set; }

    /// <summary>
    /// Gets or sets the memory usage in MB.
    /// </summary>
    public double MemoryUsageMB { get; set; }

    /// <summary>
    /// Gets or sets the GPU memory usage if applicable.
    /// </summary>
    public double? GpuMemoryUsageMB { get; set; }

    /// <summary>
    /// Gets or sets the execution provider in use.
    /// </summary>
    public string ExecutionProvider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets individual model statuses.
    /// </summary>
    public Dictionary<string, bool> ModelStatuses { get; } = new();

    /// <summary>
    /// Gets or sets any error messages.
    /// </summary>
    public List<string> Errors { get; } = new();
}