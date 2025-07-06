using System.Text.Json.Serialization;
using TradingPlatform.Core.Models;

namespace TradingPlatform.AlternativeData.Models;

public enum AlternativeDataType
{
    SatelliteImagery,
    SocialMediaSentiment,
    EconomicIndicator,
    NewsAnalysis,
    WeatherData,
    TrafficData,
    CommodityFlow,
    CorporateActivity
}

public enum SentimentScore
{
    VeryNegative = -2,
    Negative = -1,
    Neutral = 0,
    Positive = 1,
    VeryPositive = 2
}

public enum ImageQuality
{
    Low,
    Medium,
    High,
    UltraHigh
}

public enum DataFrequency
{
    RealTime,
    Hourly,
    Daily,
    Weekly,
    Monthly
}

public record AlternativeDataSignal
{
    public required string SignalId { get; init; }
    public required AlternativeDataType DataType { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string Symbol { get; init; }
    public required decimal Confidence { get; init; }
    public required decimal SignalStrength { get; init; }
    public required string Source { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public decimal? PredictedPriceImpact { get; init; }
    public TimeSpan? PredictedDuration { get; init; }
    public string? Description { get; init; }
}

public record SatelliteDataPoint
{
    public required string ImageId { get; init; }
    public required DateTime CaptureTime { get; init; }
    public required decimal Latitude { get; init; }
    public required decimal Longitude { get; init; }
    public required ImageQuality Quality { get; init; }
    public required string SatelliteSource { get; init; }
    public required byte[] ImageData { get; init; }
    public required Dictionary<string, decimal> AnalysisResults { get; init; }
    public string? WeatherConditions { get; init; }
    public decimal? CloudCoverage { get; init; }
    public List<string>? DetectedFeatures { get; init; }
}

public record SocialMediaPost
{
    public required string PostId { get; init; }
    public required string Platform { get; init; }
    public required DateTime PostTime { get; init; }
    public required string Author { get; init; }
    public required string Content { get; init; }
    public required SentimentScore Sentiment { get; init; }
    public required decimal SentimentConfidence { get; init; }
    public required int Engagement { get; init; }
    public required int Followers { get; init; }
    public List<string>? Hashtags { get; init; }
    public List<string>? Mentions { get; init; }
    public List<string>? ExtractedSymbols { get; init; }
    public decimal? InfluenceScore { get; init; }
}

public record EconomicIndicatorData
{
    public required string IndicatorId { get; init; }
    public required string IndicatorName { get; init; }
    public required DateTime ReleaseTime { get; init; }
    public required decimal Value { get; init; }
    public required string Unit { get; init; }
    public required DataFrequency Frequency { get; init; }
    public decimal? PreviousValue { get; init; }
    public decimal? ExpectedValue { get; init; }
    public decimal? Surprise { get; init; }
    public required string Source { get; init; }
    public string? CountryCode { get; init; }
    public string? Category { get; init; }
}

public record AlternativeDataProvider
{
    public required string ProviderId { get; init; }
    public required string Name { get; init; }
    public required AlternativeDataType DataType { get; init; }
    public required string ApiEndpoint { get; init; }
    public required Dictionary<string, string> Configuration { get; init; }
    public required decimal CostPerRequest { get; init; }
    public required int RateLimit { get; init; }
    public required TimeSpan RateLimitWindow { get; init; }
    public required List<string> SupportedSymbols { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTime? LastUpdateTime { get; init; }
    public decimal? QualityScore { get; init; }
}

public record DataProcessingTask
{
    public required string TaskId { get; init; }
    public required AlternativeDataType DataType { get; init; }
    public required string AIModelName { get; init; }
    public required DateTime CreatedTime { get; init; }
    public required ProcessingStatus Status { get; init; }
    public required byte[] InputData { get; init; }
    public byte[]? ProcessedData { get; init; }
    public Dictionary<string, object>? Results { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan? ProcessingTime { get; init; }
    public decimal? ConfidenceScore { get; init; }
}

public enum ProcessingStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}

public record SatelliteAnalysisResult
{
    public required string ImageId { get; init; }
    public required DateTime AnalysisTime { get; init; }
    public required Dictionary<string, decimal> EconomicIndicators { get; init; }
    public required List<DetectedFeature> DetectedFeatures { get; init; }
    public required decimal OverallActivityScore { get; init; }
    public decimal? ChangeFromPrevious { get; init; }
    public string? TrendDirection { get; init; }
    public List<string>? AffectedSymbols { get; init; }
}

public record DetectedFeature
{
    public required string FeatureType { get; init; }
    public required decimal Confidence { get; init; }
    public required decimal X { get; init; }
    public required decimal Y { get; init; }
    public required decimal Width { get; init; }
    public required decimal Height { get; init; }
    public Dictionary<string, object>? Properties { get; init; }
}

public record SentimentAnalysisResult
{
    public required string PostId { get; init; }
    public required DateTime AnalysisTime { get; init; }
    public required SentimentScore OverallSentiment { get; init; }
    public required decimal SentimentConfidence { get; init; }
    public required Dictionary<string, decimal> EmotionScores { get; init; }
    public required List<string> KeyTopics { get; init; }
    public required List<EntityMention> EntityMentions { get; init; }
    public decimal? InfluenceWeight { get; init; }
    public List<string>? PredictedSymbols { get; init; }
}

public record EntityMention
{
    public required string Entity { get; init; }
    public required string EntityType { get; init; }
    public required decimal Confidence { get; init; }
    public required int StartPosition { get; init; }
    public required int EndPosition { get; init; }
    public SentimentScore? EntitySentiment { get; init; }
}

public record AlternativeDataConfiguration
{
    public required Dictionary<string, AlternativeDataProvider> Providers { get; init; }
    public required Dictionary<string, AIModelConfig> AIModels { get; init; }
    public required ProcessingSettings Processing { get; init; }
    public required CostSettings Cost { get; init; }
    public required QualitySettings Quality { get; init; }
}

public record AIModelConfig
{
    public required string ModelName { get; init; }
    public required string ModelType { get; init; }
    public required string ModelPath { get; init; }
    public required Dictionary<string, object> Parameters { get; init; }
    public required bool RequiresGPU { get; init; }
    public required int MaxBatchSize { get; init; }
    public required TimeSpan Timeout { get; init; }
    public decimal? AccuracyThreshold { get; init; }
}

public record ProcessingSettings
{
    public required int MaxConcurrentTasks { get; init; }
    public required TimeSpan TaskTimeout { get; init; }
    public required int RetryAttempts { get; init; }
    public required TimeSpan RetryDelay { get; init; }
    public required bool EnableBatching { get; init; }
    public required int BatchSize { get; init; }
    public required TimeSpan BatchTimeout { get; init; }
}

public record CostSettings
{
    public required decimal DailyBudget { get; init; }
    public required decimal MonthlyBudget { get; init; }
    public required decimal CostPerGPUHour { get; init; }
    public required Dictionary<string, decimal> ProviderCosts { get; init; }
    public required bool EnableCostControls { get; init; }
    public required decimal CostAlertThreshold { get; init; }
}

public record QualitySettings
{
    public required decimal MinConfidenceScore { get; init; }
    public required decimal MinSignalStrength { get; init; }
    public required int MinDataPoints { get; init; }
    public required TimeSpan MaxDataAge { get; init; }
    public required bool EnableQualityFiltering { get; init; }
    public required Dictionary<string, decimal> QualityThresholds { get; init; }
}

public record AlternativeDataRequest
{
    public required string RequestId { get; init; }
    public required AlternativeDataType DataType { get; init; }
    public required List<string> Symbols { get; init; }
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }
    public required string RequestedBy { get; init; }
    public Dictionary<string, object>? Parameters { get; init; }
    public decimal? MaxCost { get; init; }
    public int? Priority { get; init; }
}

public record AlternativeDataResponse
{
    public required string RequestId { get; init; }
    public required bool Success { get; init; }
    public required DateTime ResponseTime { get; init; }
    public required List<AlternativeDataSignal> Signals { get; init; }
    public required int TotalDataPoints { get; init; }
    public required decimal ProcessingCost { get; init; }
    public required TimeSpan ProcessingDuration { get; init; }
    public required Dictionary<string, object> Metadata { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string>? Warnings { get; init; }
}

public record DataProviderHealth
{
    public required string ProviderId { get; init; }
    public required bool IsHealthy { get; init; }
    public required DateTime LastCheckTime { get; init; }
    public required TimeSpan ResponseTime { get; init; }
    public required int RequestsInLastHour { get; init; }
    public required int FailuresInLastHour { get; init; }
    public required decimal SuccessRate { get; init; }
    public required decimal AverageCost { get; init; }
    public string? HealthIssue { get; init; }
}

public record AlternativeDataMetrics
{
    public required DateTime MetricsTime { get; init; }
    public required Dictionary<string, int> RequestsByDataType { get; init; }
    public required Dictionary<string, decimal> CostsByProvider { get; init; }
    public required Dictionary<string, decimal> QualityScoresByProvider { get; init; }
    public required Dictionary<string, TimeSpan> ProcessingTimesByModel { get; init; }
    public required decimal TotalDailyCost { get; init; }
    public required int TotalSignalsGenerated { get; init; }
    public required decimal AverageSignalConfidence { get; init; }
    public required int GPUUtilizationPercentage { get; init; }
}

public record SignalValidationResult
{
    public required string SignalId { get; init; }
    public required bool IsValid { get; init; }
    public required decimal ValidationScore { get; init; }
    public required List<string> ValidationChecks { get; init; }
    public required DateTime ValidationTime { get; init; }
    public List<string>? Issues { get; init; }
    public Dictionary<string, object>? ValidationMetadata { get; init; }
}

public record BacktestResult
{
    public required string SignalId { get; init; }
    public required string Symbol { get; init; }
    public required DateTime SignalTime { get; init; }
    public required decimal SignalStrength { get; init; }
    public required decimal ActualPriceMove { get; init; }
    public required decimal PredictedPriceMove { get; init; }
    public required bool WasCorrect { get; init; }
    public required decimal Accuracy { get; init; }
    public required TimeSpan Duration { get; init; }
    public decimal? ProfitLoss { get; init; }
}