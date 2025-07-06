using TradingPlatform.Foundation.Models;

namespace TradingPlatform.AI.Models;

/// <summary>
/// AI model metadata for lifecycle management and performance tracking
/// </summary>
public class AIModelMetadata : IDisposable
{
    public string ModelName { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty; // Prophet, AutoGluon, FinRL, N-BEATS, etc.
    public string Version { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime LoadedAt { get; set; }
    public DateTime LastUsed { get; set; }
    public long InferenceCount { get; set; }
    public long SuccessfulInferences { get; set; }
    public long FailedInferences { get; set; }
    public TimeSpan AverageLatency { get; set; }
    public decimal ErrorRate { get; set; }
    public long MemoryUsageBytes { get; set; }
    public bool IsGpuAccelerated { get; set; }
    public bool CanUnload { get; set; } = true;
    public AIModelCapabilities Capabilities { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();

    // Model-specific resources
    public object? ModelInstance { get; set; }
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            if (ModelInstance is IDisposable disposableModel)
            {
                disposableModel.Dispose();
            }
            
            ModelInstance = null;
            IsDisposed = true;
        }
    }
}

/// <summary>
/// AI model capabilities and supported operations
/// </summary>
public class AIModelCapabilities
{
    public List<string> SupportedInputTypes { get; set; } = new();
    public List<string> SupportedOutputTypes { get; set; } = new();
    public List<string> SupportedOperations { get; set; } = new();
    public int MaxBatchSize { get; set; } = 1;
    public bool RequiresGpu { get; set; }
    public bool SupportsStreaming { get; set; }
    public bool SupportsQuantization { get; set; }
    public TimeSpan MaxInferenceTime { get; set; } = TimeSpan.FromSeconds(30);
    public decimal MinConfidenceThreshold { get; set; } = 0.5m;
}

/// <summary>
/// AI model configuration for service initialization
/// </summary>
public class AIModelConfiguration
{
    public List<ModelDefinition> AvailableModels { get; set; } = new();
    public PerformanceThresholds PerformanceThresholds { get; set; } = new();
    public ModelCacheSettings ModelCacheSettings { get; set; } = new();
    public GPUConfiguration GpuConfiguration { get; set; } = new();
    public LoggingConfiguration LoggingConfiguration { get; set; } = new();
}

/// <summary>
/// Individual model definition for configuration
/// </summary>
public class ModelDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int Priority { get; set; } = 1;
    public AIModelCapabilities Capabilities { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Performance thresholds for AI service monitoring
/// </summary>
public class PerformanceThresholds
{
    public TimeSpan MaxLatency { get; set; } = TimeSpan.FromSeconds(5);
    public decimal MaxErrorRate { get; set; } = 0.05m; // 5%
    public long MaxMemoryUsageMB { get; set; } = 2048; // 2GB
    public int MaxConcurrentInferences { get; set; } = 10;
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Model caching and lifecycle settings
/// </summary>
public class ModelCacheSettings
{
    public int MaxCachedModels { get; set; } = 5;
    public TimeSpan UnloadAfterInactivity { get; set; } = TimeSpan.FromHours(1);
    public bool EnableAutomaticUnloading { get; set; } = true;
    public bool PreloadDefaultModels { get; set; } = true;
    public long MaxTotalMemoryMB { get; set; } = 8192; // 8GB
}

/// <summary>
/// GPU configuration for AI acceleration
/// </summary>
public class GPUConfiguration
{
    public bool EnableGpuAcceleration { get; set; } = true;
    public List<int> PreferredGpuDevices { get; set; } = new();
    public bool FallbackToCpu { get; set; } = true;
    public int GpuMemoryLimitMB { get; set; } = -1; // -1 = no limit
}

/// <summary>
/// AI service logging configuration
/// </summary>
public class LoggingConfiguration
{
    public bool EnablePerformanceLogging { get; set; } = true;
    public bool EnableInferenceLogging { get; set; } = false; // Can be verbose
    public bool EnableModelLifecycleLogging { get; set; } = true;
    public TimeSpan PerformanceLogInterval { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// AI service health information
/// </summary>
public class AIServiceHealth
{
    public string ServiceName { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public int LoadedModels { get; set; }
    public long TotalInferences { get; set; }
    public TimeSpan AverageLatency { get; set; }
    public List<string> Issues { get; set; } = new();
    public DateTime LastHealthCheck { get; set; }
    public long MemoryUsageMB { get; set; }
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}

/// <summary>
/// Financial time series data for AI models
/// </summary>
public class FinancialTimeSeriesData
{
    public string Symbol { get; set; } = string.Empty;
    public List<TimeSeriesPoint> DataPoints { get; set; } = new();
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Frequency { get; set; } = string.Empty; // Daily, Hourly, Minute, etc.
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Individual time series data point
/// </summary>
public class TimeSeriesPoint
{
    public DateTime Timestamp { get; set; }
    public decimal Value { get; set; }
    public decimal? Volume { get; set; }
    public decimal? Open { get; set; }
    public decimal? High { get; set; }
    public decimal? Low { get; set; }
    public decimal? Close { get; set; }
    public Dictionary<string, decimal> Features { get; set; } = new();
}

/// <summary>
/// AI model prediction with confidence and uncertainty
/// </summary>
public class AIPrediction
{
    public string ModelName { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;
    public DateTime PredictionTime { get; set; }
    public object PredictedValue { get; set; } = null!;
    public decimal Confidence { get; set; }
    public decimal? UpperBound { get; set; }
    public decimal? LowerBound { get; set; }
    public TimeSpan InferenceLatency { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Time series forecast result
/// </summary>
public class TimeSeriesForecast : AIPrediction
{
    public int ForecastHorizon { get; set; }
    public List<ForecastPoint> ForecastPoints { get; set; } = new();
    public TrendAnalysis? TrendAnalysis { get; set; }
    public SeasonalAnalysis? SeasonalAnalysis { get; set; }
    public List<ConfidenceInterval> ConfidenceIntervals { get; set; } = new();
}

/// <summary>
/// Individual forecast point
/// </summary>
public class ForecastPoint
{
    public DateTime Timestamp { get; set; }
    public decimal PredictedValue { get; set; }
    public decimal Confidence { get; set; }
    public decimal? UpperBound { get; set; }
    public decimal? LowerBound { get; set; }
    public Dictionary<string, decimal> Components { get; set; } = new(); // Trend, seasonal, etc.
}

/// <summary>
/// Trend analysis from time series models
/// </summary>
public class TrendAnalysis
{
    public string TrendDirection { get; set; } = string.Empty; // UP, DOWN, FLAT
    public decimal TrendStrength { get; set; }
    public decimal TrendSlope { get; set; }
    public List<TrendComponent> TrendComponents { get; set; } = new();
    public DateTime? TrendChangePoint { get; set; }
    public decimal TrendSignificance { get; set; }
}

/// <summary>
/// Trend component for detailed analysis
/// </summary>
public class TrendComponent
{
    public DateTime Timestamp { get; set; }
    public decimal TrendValue { get; set; }
    public decimal TrendContribution { get; set; }
}

/// <summary>
/// Seasonal analysis from time series models
/// </summary>
public class SeasonalAnalysis
{
    public List<SeasonalComponent> DailySeasonality { get; set; } = new();
    public List<SeasonalComponent> WeeklySeasonality { get; set; } = new();
    public List<SeasonalComponent> MonthlySeasonality { get; set; } = new();
    public List<SeasonalComponent> YearlySeasonality { get; set; } = new();
    public decimal SeasonalStrength { get; set; }
}

/// <summary>
/// Seasonal component for detailed analysis
/// </summary>
public class SeasonalComponent
{
    public string Period { get; set; } = string.Empty;
    public decimal SeasonalValue { get; set; }
    public decimal SeasonalContribution { get; set; }
    public decimal Significance { get; set; }
}

/// <summary>
/// Confidence interval for predictions
/// </summary>
public class ConfidenceInterval
{
    public DateTime Timestamp { get; set; }
    public decimal ConfidenceLevel { get; set; } // 0.95 for 95% confidence
    public decimal UpperBound { get; set; }
    public decimal LowerBound { get; set; }
    public decimal Width => UpperBound - LowerBound;
}

/// <summary>
/// Reinforcement learning environment state
/// </summary>
public class RLEnvironmentState
{
    public Dictionary<string, decimal> Features { get; set; } = new();
    public decimal Reward { get; set; }
    public bool IsTerminal { get; set; }
    public DateTime Timestamp { get; set; }
    public string StateId { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Reinforcement learning action
/// </summary>
public class RLAction
{
    public string ActionType { get; set; } = string.Empty;
    public Dictionary<string, decimal> Parameters { get; set; } = new();
    public decimal Confidence { get; set; }
    public string Rationale { get; set; } = string.Empty;
}

/// <summary>
/// Reinforcement learning training result
/// </summary>
public class RLTrainingResult
{
    public int Episodes { get; set; }
    public decimal FinalReward { get; set; }
    public decimal AverageReward { get; set; }
    public bool ConvergenceAchieved { get; set; }
    public TimeSpan TrainingTime { get; set; }
    public Dictionary<string, decimal> PolicyWeights { get; set; } = new();
    public List<decimal> RewardHistory { get; set; } = new();
}

/// <summary>
/// Pattern recognition result
/// </summary>
public class PatternRecognitionResult : AIPrediction
{
    public string PatternType { get; set; } = string.Empty;
    public decimal PatternStrength { get; set; }
    public DateTime PatternStart { get; set; }
    public DateTime PatternEnd { get; set; }
    public List<PatternFeature> Features { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public decimal StatisticalSignificance { get; set; }
}

/// <summary>
/// Individual pattern feature
/// </summary>
public class PatternFeature
{
    public string FeatureName { get; set; } = string.Empty;
    public decimal FeatureValue { get; set; }
    public decimal Importance { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Model ensemble prediction combining multiple models
/// </summary>
public class EnsemblePrediction : AIPrediction
{
    public List<AIPrediction> IndividualPredictions { get; set; } = new();
    public string EnsembleMethod { get; set; } = string.Empty; // WEIGHTED_AVERAGE, VOTING, STACKING
    public Dictionary<string, decimal> ModelWeights { get; set; } = new();
    public decimal EnsembleConfidence { get; set; }
    public decimal PredictionVariance { get; set; }
}

/// <summary>
/// Model performance metrics for monitoring
/// </summary>
public class ModelPerformanceMetrics
{
    public string ModelName { get; set; } = string.Empty;
    public DateTime EvaluationPeriodStart { get; set; }
    public DateTime EvaluationPeriodEnd { get; set; }
    public long TotalPredictions { get; set; }
    public decimal Accuracy { get; set; }
    public decimal Precision { get; set; }
    public decimal Recall { get; set; }
    public decimal F1Score { get; set; }
    public decimal MeanAbsoluteError { get; set; }
    public decimal RootMeanSquareError { get; set; }
    public TimeSpan AverageLatency { get; set; }
    public decimal ThroughputPerSecond { get; set; }
    public Dictionary<string, decimal> CustomMetrics { get; set; } = new();
}

/// <summary>
/// AI model training configuration
/// </summary>
public class ModelTrainingConfig
{
    public string ModelType { get; set; } = string.Empty;
    public Dictionary<string, object> Hyperparameters { get; set; } = new();
    public int Epochs { get; set; } = 100;
    public decimal LearningRate { get; set; } = 0.001m;
    public int BatchSize { get; set; } = 32;
    public decimal ValidationSplit { get; set; } = 0.2m;
    public List<string> Features { get; set; } = new();
    public string TargetVariable { get; set; } = string.Empty;
    public bool EnableEarlyStopping { get; set; } = true;
    public decimal EarlyStoppingPatience { get; set; } = 10;
}