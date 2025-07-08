namespace MarketAnalyzer.Infrastructure.AI.Models;

/// <summary>
/// Represents AI model performance metrics.
/// </summary>
public class AIModelPerformance
{
    /// <summary>
    /// Gets or sets the model accuracy (0-1).
    /// </summary>
    public decimal Accuracy { get; set; }

    /// <summary>
    /// Gets or sets the model precision (0-1).
    /// </summary>
    public decimal Precision { get; set; }

    /// <summary>
    /// Gets or sets the model recall (0-1).
    /// </summary>
    public decimal Recall { get; set; }

    /// <summary>
    /// Gets or sets the F1 score (0-1).
    /// </summary>
    public decimal F1Score { get; set; }

    /// <summary>
    /// Gets or sets the average inference time in milliseconds.
    /// </summary>
    public double AverageInferenceTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the memory usage in bytes.
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets the number of parameters in the model.
    /// </summary>
    public long ParameterCount { get; set; }

    /// <summary>
    /// Gets or sets the model size in bytes.
    /// </summary>
    public long ModelSizeBytes { get; set; }
}

/// <summary>
/// Represents model metadata and information.
/// </summary>
public class AIModelMetadata
{
    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model author.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model creation date.
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the training dataset information.
    /// </summary>
    public string TrainingDataset { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model framework (ONNX, TensorFlow, PyTorch).
    /// </summary>
    public string Framework { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the input data format.
    /// </summary>
    public string InputFormat { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the output data format.
    /// </summary>
    public string OutputFormat { get; set; } = string.Empty;

    /// <summary>
    /// Gets the model-specific tags.
    /// </summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Gets the custom model properties.
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();
}

/// <summary>
/// Represents price prediction results.
/// </summary>
public class PricePredictionResult
{
    /// <summary>
    /// Gets or sets the symbol being predicted.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the predicted prices for future time periods.
    /// </summary>
    public decimal[] PredictedPrices { get; set; } = Array.Empty<decimal>();

    /// <summary>
    /// Gets or sets the upper confidence bounds.
    /// </summary>
    public decimal[] UpperBounds { get; set; } = Array.Empty<decimal>();

    /// <summary>
    /// Gets or sets the lower confidence bounds.
    /// </summary>
    public decimal[] LowerBounds { get; set; } = Array.Empty<decimal>();

    /// <summary>
    /// Gets or sets the prediction confidence (0-1).
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Gets or sets the prediction horizon (number of periods).
    /// </summary>
    public int Horizon { get; set; }

    /// <summary>
    /// Gets or sets the time frame for each prediction period.
    /// </summary>
    public TimeSpan TimeFrame { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when prediction was made.
    /// </summary>
    public DateTime PredictionTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the model used for prediction.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets the additional prediction metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Represents sentiment analysis results.
/// </summary>
public class SentimentAnalysisResult
{
    /// <summary>
    /// Gets or sets the overall sentiment score (-1 to 1).
    /// </summary>
    public decimal SentimentScore { get; set; }

    /// <summary>
    /// Gets or sets the sentiment classification.
    /// </summary>
    public SentimentClassification Classification { get; set; }

    /// <summary>
    /// Gets or sets the confidence in the sentiment analysis (0-1).
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Gets or sets the positive sentiment probability (0-1).
    /// </summary>
    public decimal PositiveProbability { get; set; }

    /// <summary>
    /// Gets or sets the negative sentiment probability (0-1).
    /// </summary>
    public decimal NegativeProbability { get; set; }

    /// <summary>
    /// Gets or sets the neutral sentiment probability (0-1).
    /// </summary>
    public decimal NeutralProbability { get; set; }

    /// <summary>
    /// Gets the key phrases that influenced the sentiment.
    /// </summary>
    public List<string> KeyPhrases { get; init; } = new();

    /// <summary>
    /// Gets or sets the source text analyzed.
    /// </summary>
    public string SourceText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the analysis timestamp.
    /// </summary>
    public DateTime AnalysisTimestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Sentiment classification types.
/// </summary>
public enum SentimentClassification
{
    /// <summary>
    /// Strong negative sentiment.
    /// </summary>
    StronglyNegative = -2,

    /// <summary>
    /// Negative sentiment.
    /// </summary>
    Negative = -1,

    /// <summary>
    /// Neutral sentiment.
    /// </summary>
    Neutral = 0,

    /// <summary>
    /// Positive sentiment.
    /// </summary>
    Positive = 1,

    /// <summary>
    /// Strong positive sentiment.
    /// </summary>
    StronglyPositive = 2
}

/// <summary>
/// Represents pattern recognition results.
/// </summary>
public class PatternRecognitionResult
{
    /// <summary>
    /// Gets or sets the detected pattern type.
    /// </summary>
    public PatternType PatternType { get; set; }

    /// <summary>
    /// Gets or sets the pattern confidence (0-1).
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Gets or sets the pattern strength (0-1).
    /// </summary>
    public decimal Strength { get; set; }

    /// <summary>
    /// Gets or sets the pattern start price.
    /// </summary>
    public decimal StartPrice { get; set; }

    /// <summary>
    /// Gets or sets the pattern end price.
    /// </summary>
    public decimal EndPrice { get; set; }

    /// <summary>
    /// Gets or sets the pattern start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the pattern end time.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets or sets the expected price target if pattern completes.
    /// </summary>
    public decimal? PriceTarget { get; set; }

    /// <summary>
    /// Gets or sets the stop loss level.
    /// </summary>
    public decimal? StopLoss { get; set; }

    /// <summary>
    /// Gets or sets the symbol where pattern was detected.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timeframe for pattern analysis.
    /// </summary>
    public string TimeFrame { get; set; } = string.Empty;

    /// <summary>
    /// Gets the pattern-specific properties.
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();
}

/// <summary>
/// Types of trading patterns.
/// </summary>
public enum PatternType
{
    /// <summary>
    /// Head and shoulders pattern.
    /// </summary>
    HeadAndShoulders,

    /// <summary>
    /// Double top pattern.
    /// </summary>
    DoubleTop,

    /// <summary>
    /// Double bottom pattern.
    /// </summary>
    DoubleBottom,

    /// <summary>
    /// Triangle pattern.
    /// </summary>
    Triangle,

    /// <summary>
    /// Flag pattern.
    /// </summary>
    Flag,

    /// <summary>
    /// Wedge pattern.
    /// </summary>
    Wedge,

    /// <summary>
    /// Cup and handle pattern.
    /// </summary>
    CupAndHandle,

    /// <summary>
    /// Support level.
    /// </summary>
    Support,

    /// <summary>
    /// Resistance level.
    /// </summary>
    Resistance,

    /// <summary>
    /// Trend line.
    /// </summary>
    TrendLine,

    /// <summary>
    /// Channel pattern.
    /// </summary>
    Channel
}

/// <summary>
/// Represents trading signal generation results.
/// </summary>
public class TradingSignalResult
{
    /// <summary>
    /// Gets or sets the signal type.
    /// </summary>
    public SignalType SignalType { get; set; }

    /// <summary>
    /// Gets or sets the signal strength (0-1).
    /// </summary>
    public decimal Strength { get; set; }

    /// <summary>
    /// Gets or sets the signal confidence (0-1).
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Gets or sets the entry price recommendation.
    /// </summary>
    public decimal EntryPrice { get; set; }

    /// <summary>
    /// Gets or sets the stop loss price.
    /// </summary>
    public decimal StopLoss { get; set; }

    /// <summary>
    /// Gets or sets the take profit price.
    /// </summary>
    public decimal TakeProfit { get; set; }

    /// <summary>
    /// Gets or sets the position size recommendation (0-1 of portfolio).
    /// </summary>
    public decimal PositionSize { get; set; }

    /// <summary>
    /// Gets or sets the signal expiry time.
    /// </summary>
    public DateTime ExpiryTime { get; set; }

    /// <summary>
    /// Gets or sets the symbol for the signal.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timeframe for the signal.
    /// </summary>
    public string TimeFrame { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reasoning behind the signal.
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// Gets the supporting indicators for the signal.
    /// </summary>
    public List<string> SupportingIndicators { get; init; } = new();

    /// <summary>
    /// Gets or sets the risk-reward ratio.
    /// </summary>
    public decimal RiskRewardRatio { get; set; }
}

/// <summary>
/// Types of trading signals.
/// </summary>
public enum SignalType
{
    /// <summary>
    /// Strong buy signal.
    /// </summary>
    StrongBuy,

    /// <summary>
    /// Buy signal.
    /// </summary>
    Buy,

    /// <summary>
    /// Hold signal.
    /// </summary>
    Hold,

    /// <summary>
    /// Sell signal.
    /// </summary>
    Sell,

    /// <summary>
    /// Strong sell signal.
    /// </summary>
    StrongSell
}

/// <summary>
/// Represents risk assessment results.
/// </summary>
public class RiskAssessmentResult
{
    /// <summary>
    /// Gets or sets the overall risk score (0-100).
    /// </summary>
    public decimal RiskScore { get; set; }

    /// <summary>
    /// Gets or sets the risk classification.
    /// </summary>
    public RiskLevel RiskLevel { get; set; }

    /// <summary>
    /// Gets or sets the Value at Risk (VaR) at 95% confidence.
    /// </summary>
    public decimal ValueAtRisk { get; set; }

    /// <summary>
    /// Gets or sets the Expected Shortfall (Conditional VaR).
    /// </summary>
    public decimal ExpectedShortfall { get; set; }

    /// <summary>
    /// Gets or sets the maximum drawdown estimate.
    /// </summary>
    public decimal MaxDrawdown { get; set; }

    /// <summary>
    /// Gets or sets the volatility estimate.
    /// </summary>
    public decimal Volatility { get; set; }

    /// <summary>
    /// Gets or sets the beta coefficient (market sensitivity).
    /// </summary>
    public decimal Beta { get; set; }

    /// <summary>
    /// Gets or sets the Sharpe ratio.
    /// </summary>
    public decimal SharpeRatio { get; set; }

    /// <summary>
    /// Gets the individual risk factors.
    /// </summary>
    public Dictionary<string, decimal> RiskFactors { get; init; } = new();

    /// <summary>
    /// Gets the risk mitigation recommendations.
    /// </summary>
    public List<string> Recommendations { get; init; } = new();

    /// <summary>
    /// Gets or sets the assessment timestamp.
    /// </summary>
    public DateTime AssessmentTimestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Risk level classifications.
/// </summary>
public enum RiskLevel
{
    /// <summary>
    /// Very low risk.
    /// </summary>
    VeryLow,

    /// <summary>
    /// Low risk.
    /// </summary>
    Low,

    /// <summary>
    /// Moderate risk.
    /// </summary>
    Moderate,

    /// <summary>
    /// High risk.
    /// </summary>
    High,

    /// <summary>
    /// Very high risk.
    /// </summary>
    VeryHigh,

    /// <summary>
    /// Extreme risk.
    /// </summary>
    Extreme
}