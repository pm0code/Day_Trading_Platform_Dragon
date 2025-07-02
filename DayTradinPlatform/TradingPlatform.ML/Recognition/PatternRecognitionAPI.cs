// File: TradingPlatform.ML/Recognition/PatternRecognitionAPI.cs

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Models;
using TradingPlatform.Core.Performance;
using TradingPlatform.Foundation.Models;
using TradingPlatform.ML.Data;
using TradingPlatform.ML.Inference;
using TradingPlatform.ML.Models;

namespace TradingPlatform.ML.Recognition
{
    /// <summary>
    /// High-level API for pattern recognition
    /// </summary>
    public class PatternRecognitionAPI : CanonicalServiceBase
    {
        private readonly ModelServingInfrastructure _modelServing;
        private readonly RealTimeInferenceEngine _inferenceEngine;
        private readonly SequenceDataPreparation _dataPreparation;
        private readonly HighPerformancePool<PatternRequest> _requestPool;
        private readonly ConcurrentDictionary<string, PatternSubscription> _subscriptions;
        private readonly LatencyTracker _latencyTracker;
        
        private const string DefaultModelId = "lstm_pattern_v1";
        
        public PatternRecognitionAPI(
            IServiceProvider serviceProvider,
            ITradingLogger logger,
            ModelServingInfrastructure modelServing,
            RealTimeInferenceEngine inferenceEngine)
            : base(serviceProvider, logger, "PatternRecognitionAPI")
        {
            _modelServing = modelServing;
            _inferenceEngine = inferenceEngine;
            _dataPreparation = new SequenceDataPreparation();
            _subscriptions = new ConcurrentDictionary<string, PatternSubscription>();
            _latencyTracker = new LatencyTracker(bufferSize: 10000);
            
            _requestPool = new HighPerformancePool<PatternRequest>(
                factory: () => new PatternRequest(),
                resetAction: req => req.Reset(),
                initialSize: 100,
                maxSize: 1000);
        }
        
        /// <summary>
        /// Recognize patterns in real-time data
        /// </summary>
        public async Task<TradingResult<PatternRecognitionResult>> RecognizePatternAsync(
            string symbol,
            List<MarketDataSnapshot> recentData,
            PatternRecognitionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var latencyScope = _latencyTracker.StartMeasurement();
            
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    options ??= new PatternRecognitionOptions();
                    
                    // Validate input data
                    if (recentData.Count < options.MinDataPoints)
                    {
                        return TradingResult<PatternRecognitionResult>.Failure(
                            new Exception($"Insufficient data: {recentData.Count} < {options.MinDataPoints}"));
                    }
                    
                    // Prepare sequence
                    var sequence = _dataPreparation.PrepareRealtimeSequence(
                        recentData,
                        new SequencePreparationOptions
                        {
                            NormalizationType = options.NormalizationType,
                            IncludeTechnicalIndicators = options.IncludeTechnicalIndicators
                        });
                    
                    // Get pattern prediction
                    var modelId = options.ModelId ?? DefaultModelId;
                    var predictionResult = await GetPatternPredictionAsync(
                        modelId, sequence, cancellationToken);
                    
                    if (!predictionResult.IsSuccess)
                    {
                        return TradingResult<PatternRecognitionResult>.Failure(
                            predictionResult.Error);
                    }
                    
                    var prediction = predictionResult.Value;
                    
                    // Get pattern analysis if requested
                    PatternAnalysis? analysis = null;
                    if (options.IncludeAnalysis)
                    {
                        var model = await GetOrLoadModelAsync(modelId, cancellationToken);
                        if (model != null)
                        {
                            var analysisResult = await model.AnalyzePatternAsync(
                                sequence, cancellationToken);
                            if (analysisResult.IsSuccess)
                            {
                                analysis = analysisResult.Value;
                            }
                        }
                    }
                    
                    // Create result
                    var result = new PatternRecognitionResult
                    {
                        Symbol = symbol,
                        Timestamp = DateTime.UtcNow,
                        Pattern = prediction.PredictedPattern,
                        PatternStrength = analysis?.PatternStrength ?? prediction.Confidence,
                        PredictedPriceChange = prediction.PredictedPriceChange,
                        PredictedDirection = prediction.PredictedDirection,
                        Confidence = prediction.Confidence,
                        TimeHorizon = prediction.TimeHorizon,
                        Analysis = analysis,
                        Alerts = GenerateAlerts(prediction, options),
                        RecommendedActions = GenerateRecommendations(prediction, analysis, options)
                    };
                    
                    // Record metrics
                    var latency = latencyScope.Complete();
                    RecordServiceMetric($"PatternRecognition.{symbol}.Latency", latency);
                    RecordServiceMetric($"PatternRecognition.{symbol}.Confidence", prediction.Confidence);
                    
                    // Trigger alerts if configured
                    if (result.Alerts.Any() && options.EnableAlerts)
                    {
                        await TriggerAlertsAsync(result, cancellationToken);
                    }
                    
                    LogInfo($"Pattern recognized for {symbol}",
                        additionalData: new
                        {
                            Pattern = result.Pattern,
                            Confidence = result.Confidence,
                            Latency = latency
                        });
                    
                    return TradingResult<PatternRecognitionResult>.Success(result);
                },
                nameof(RecognizePatternAsync));
        }
        
        /// <summary>
        /// Batch pattern recognition
        /// </summary>
        public async Task<TradingResult<List<PatternRecognitionResult>>> RecognizePatternsAsync(
            Dictionary<string, List<MarketDataSnapshot>> symbolData,
            PatternRecognitionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var results = new ConcurrentBag<PatternRecognitionResult>();
                    var tasks = new List<Task>();
                    
                    var semaphore = new SemaphoreSlim(10); // Limit concurrent recognitions
                    
                    foreach (var (symbol, data) in symbolData)
                    {
                        tasks.Add(Task.Run(async () =>
                        {
                            await semaphore.WaitAsync(cancellationToken);
                            try
                            {
                                var result = await RecognizePatternAsync(
                                    symbol, data, options, cancellationToken);
                                
                                if (result.IsSuccess)
                                {
                                    results.Add(result.Value);
                                }
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }));
                    }
                    
                    await Task.WhenAll(tasks);
                    
                    var sortedResults = results
                        .OrderByDescending(r => r.PatternStrength)
                        .ToList();
                    
                    RecordServiceMetric("BatchPatternRecognition.Count", sortedResults.Count);
                    
                    return TradingResult<List<PatternRecognitionResult>>.Success(sortedResults);
                },
                nameof(RecognizePatternsAsync));
        }
        
        /// <summary>
        /// Subscribe to real-time pattern detection
        /// </summary>
        public async Task<TradingResult<string>> SubscribeToPatternDetectionAsync(
            string symbol,
            PatternSubscriptionOptions options,
            Func<PatternRecognitionResult, Task> callback,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var subscriptionId = Guid.NewGuid().ToString();
                    
                    var subscription = new PatternSubscription
                    {
                        SubscriptionId = subscriptionId,
                        Symbol = symbol,
                        Options = options,
                        Callback = callback,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    
                    _subscriptions[subscriptionId] = subscription;
                    
                    // Start monitoring task
                    _ = Task.Run(async () => await MonitorPatternAsync(
                        subscription, cancellationToken));
                    
                    LogInfo($"Pattern subscription created for {symbol}",
                        additionalData: new { SubscriptionId = subscriptionId });
                    
                    return TradingResult<string>.Success(subscriptionId);
                },
                nameof(SubscribeToPatternDetectionAsync));
        }
        
        /// <summary>
        /// Unsubscribe from pattern detection
        /// </summary>
        public async Task<TradingResult<bool>> UnsubscribeAsync(
            string subscriptionId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (_subscriptions.TryRemove(subscriptionId, out var subscription))
                    {
                        subscription.IsActive = false;
                        LogInfo($"Pattern subscription removed: {subscriptionId}");
                        return TradingResult<bool>.Success(true);
                    }
                    
                    return TradingResult<bool>.Failure(
                        new Exception($"Subscription {subscriptionId} not found"));
                },
                nameof(UnsubscribeAsync));
        }
        
        /// <summary>
        /// Get historical pattern analysis
        /// </summary>
        public async Task<TradingResult<HistoricalPatternAnalysis>> GetHistoricalPatternsAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            PatternType? patternFilter = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    // In production, this would query a pattern database
                    var analysis = new HistoricalPatternAnalysis
                    {
                        Symbol = symbol,
                        StartDate = startDate,
                        EndDate = endDate,
                        TotalPatternsFound = 42,
                        PatternDistribution = new Dictionary<PatternType, int>
                        {
                            [PatternType.Trending] = 15,
                            [PatternType.Breakout] = 8,
                            [PatternType.Consolidation] = 12,
                            [PatternType.Volatile] = 7
                        },
                        SuccessRates = new Dictionary<PatternType, decimal>
                        {
                            [PatternType.Trending] = 0.73m,
                            [PatternType.Breakout] = 0.65m,
                            [PatternType.Consolidation] = 0.58m,
                            [PatternType.Volatile] = 0.45m
                        },
                        AverageDuration = new Dictionary<PatternType, TimeSpan>
                        {
                            [PatternType.Trending] = TimeSpan.FromHours(4),
                            [PatternType.Breakout] = TimeSpan.FromHours(2),
                            [PatternType.Consolidation] = TimeSpan.FromHours(6),
                            [PatternType.Volatile] = TimeSpan.FromHours(3)
                        }
                    };
                    
                    return TradingResult<HistoricalPatternAnalysis>.Success(analysis);
                },
                nameof(GetHistoricalPatternsAsync));
        }
        
        // Helper methods
        
        private async Task<TradingResult<PatternPrediction>> GetPatternPredictionAsync(
            string modelId,
            PatternSequence sequence,
            CancellationToken cancellationToken)
        {
            // Try to use serving infrastructure first
            var servingResult = await _modelServing.PredictAsync<PatternSequence, PatternPrediction>(
                modelId, sequence, cancellationToken);
            
            if (servingResult.IsSuccess)
            {
                return servingResult;
            }
            
            // Fallback to direct model if serving fails
            var model = await GetOrLoadModelAsync(modelId, cancellationToken);
            if (model != null)
            {
                return await model.PredictAsync(sequence, cancellationToken);
            }
            
            return TradingResult<PatternPrediction>.Failure(
                new Exception($"Model {modelId} not available"));
        }
        
        private async Task<LSTMPatternModel?> GetOrLoadModelAsync(
            string modelId,
            CancellationToken cancellationToken)
        {
            // In production, this would manage model instances
            var model = ServiceProvider.GetService<LSTMPatternModel>();
            if (model != null)
            {
                // Ensure model is loaded
                var modelPath = $"models/{modelId}";
                if (Directory.Exists(modelPath))
                {
                    await model.LoadAsync(modelPath, cancellationToken);
                }
            }
            
            return model;
        }
        
        private List<PatternAlert> GenerateAlerts(
            PatternPrediction prediction,
            PatternRecognitionOptions options)
        {
            var alerts = new List<PatternAlert>();
            
            // High confidence pattern alert
            if (prediction.Confidence > options.HighConfidenceThreshold)
            {
                alerts.Add(new PatternAlert
                {
                    Type = AlertType.HighConfidencePattern,
                    Severity = AlertSeverity.High,
                    Message = $"High confidence {prediction.PredictedPattern} pattern detected",
                    Confidence = prediction.Confidence
                });
            }
            
            // Large price movement alert
            if (Math.Abs(prediction.PredictedPriceChange) > options.SignificantMoveThreshold)
            {
                alerts.Add(new PatternAlert
                {
                    Type = AlertType.SignificantPriceMove,
                    Severity = AlertSeverity.Medium,
                    Message = $"Predicted {prediction.PredictedPriceChange:F2}% price change",
                    PredictedChange = prediction.PredictedPriceChange
                });
            }
            
            // Breakout/breakdown alert
            if (prediction.PredictedPattern == PatternType.Breakout ||
                prediction.PredictedPattern == PatternType.Breakdown)
            {
                alerts.Add(new PatternAlert
                {
                    Type = AlertType.BreakoutPattern,
                    Severity = AlertSeverity.High,
                    Message = $"{prediction.PredictedPattern} pattern forming",
                    Pattern = prediction.PredictedPattern
                });
            }
            
            return alerts;
        }
        
        private List<TradingRecommendation> GenerateRecommendations(
            PatternPrediction prediction,
            PatternAnalysis? analysis,
            PatternRecognitionOptions options)
        {
            var recommendations = new List<TradingRecommendation>();
            
            // Entry recommendation
            if (analysis?.RecommendedAction == RecommendedAction.Buy ||
                (prediction.PredictedDirection > 0 && prediction.Confidence > options.TradingThreshold))
            {
                recommendations.Add(new TradingRecommendation
                {
                    Action = "BUY",
                    Confidence = prediction.Confidence,
                    Reasoning = $"{prediction.PredictedPattern} pattern with {prediction.PredictedPriceChange:F2}% upside",
                    RiskLevel = analysis?.RiskAssessment?.RiskLevel ?? "Medium",
                    SuggestedStopLoss = analysis?.RiskAssessment?.RecommendedStopLoss ?? -2.0m,
                    TimeHorizon = $"{prediction.TimeHorizon} periods"
                });
            }
            else if (analysis?.RecommendedAction == RecommendedAction.Sell ||
                     (prediction.PredictedDirection < 0 && prediction.Confidence > options.TradingThreshold))
            {
                recommendations.Add(new TradingRecommendation
                {
                    Action = "SELL",
                    Confidence = prediction.Confidence,
                    Reasoning = $"{prediction.PredictedPattern} pattern with {Math.Abs(prediction.PredictedPriceChange):F2}% downside",
                    RiskLevel = analysis?.RiskAssessment?.RiskLevel ?? "Medium",
                    SuggestedStopLoss = analysis?.RiskAssessment?.RecommendedStopLoss ?? 2.0m,
                    TimeHorizon = $"{prediction.TimeHorizon} periods"
                });
            }
            else
            {
                recommendations.Add(new TradingRecommendation
                {
                    Action = "HOLD",
                    Confidence = prediction.Confidence,
                    Reasoning = "Pattern confidence below trading threshold",
                    RiskLevel = "Low"
                });
            }
            
            return recommendations;
        }
        
        private async Task TriggerAlertsAsync(
            PatternRecognitionResult result,
            CancellationToken cancellationToken)
        {
            // In production, this would send notifications
            foreach (var alert in result.Alerts)
            {
                LogWarning($"Pattern Alert: {result.Symbol} - {alert.Message}",
                    additionalData: alert);
                
                RecordServiceMetric($"PatternAlert.{alert.Type}", 1);
            }
        }
        
        private async Task MonitorPatternAsync(
            PatternSubscription subscription,
            CancellationToken cancellationToken)
        {
            var dataBuffer = new List<MarketDataSnapshot>();
            var lastCheck = DateTime.UtcNow;
            
            while (subscription.IsActive && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // In production, this would get real-time data
                    await Task.Delay(subscription.Options.CheckInterval, cancellationToken);
                    
                    // Check if enough time has passed
                    if (DateTime.UtcNow - lastCheck < subscription.Options.CheckInterval)
                        continue;
                    
                    lastCheck = DateTime.UtcNow;
                    
                    // Get latest data (simulated)
                    var latestData = GenerateSimulatedData(subscription.Symbol);
                    dataBuffer.AddRange(latestData);
                    
                    // Keep buffer size manageable
                    if (dataBuffer.Count > subscription.Options.BufferSize)
                    {
                        dataBuffer = dataBuffer
                            .Skip(dataBuffer.Count - subscription.Options.BufferSize)
                            .ToList();
                    }
                    
                    // Check if we have enough data
                    if (dataBuffer.Count >= subscription.Options.MinDataPoints)
                    {
                        // Run pattern recognition
                        var result = await RecognizePatternAsync(
                            subscription.Symbol,
                            dataBuffer,
                            subscription.Options.RecognitionOptions,
                            cancellationToken);
                        
                        if (result.IsSuccess)
                        {
                            // Check if pattern meets criteria
                            if (ShouldNotify(result.Value, subscription.Options))
                            {
                                await subscription.Callback(result.Value);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Error monitoring pattern for {subscription.Symbol}", ex);
                }
            }
        }
        
        private List<MarketDataSnapshot> GenerateSimulatedData(string symbol)
        {
            // In production, this would be real market data
            var data = new List<MarketDataSnapshot>();
            var basePrice = 100m;
            
            for (int i = 0; i < 5; i++)
            {
                var change = DecimalRandomCanonical.Instance.NextDecimal() * 2m - 1m;
                basePrice *= (1 + change / 100);
                
                data.Add(new MarketDataSnapshot
                {
                    Symbol = symbol,
                    Timestamp = DateTime.UtcNow.AddMinutes(-5 + i),
                    Open = basePrice,
                    High = basePrice * 1.001m,
                    Low = basePrice * 0.999m,
                    Close = basePrice,
                    Volume = DecimalRandomCanonical.Instance.NextInt(100000, 1000000)
                });
            }
            
            return data;
        }
        
        private bool ShouldNotify(
            PatternRecognitionResult result,
            PatternSubscriptionOptions options)
        {
            // Check confidence threshold
            if (result.Confidence < options.MinConfidence)
                return false;
            
            // Check pattern filter
            if (options.PatternFilter != null && !options.PatternFilter.Contains(result.Pattern))
                return false;
            
            // Check for alerts
            if (options.RequireAlerts && !result.Alerts.Any())
                return false;
            
            return true;
        }
    }
    
    // Supporting classes
    
    public class PatternRecognitionResult
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public PatternType Pattern { get; set; }
        public decimal PatternStrength { get; set; }
        public decimal PredictedPriceChange { get; set; }
        public int PredictedDirection { get; set; }
        public decimal Confidence { get; set; }
        public int TimeHorizon { get; set; }
        public PatternAnalysis? Analysis { get; set; }
        public List<PatternAlert> Alerts { get; set; } = new();
        public List<TradingRecommendation> RecommendedActions { get; set; } = new();
    }
    
    public class PatternRecognitionOptions
    {
        public string? ModelId { get; set; }
        public int MinDataPoints { get; set; } = 60;
        public NormalizationType NormalizationType { get; set; } = NormalizationType.MinMax;
        public bool IncludeTechnicalIndicators { get; set; } = true;
        public bool IncludeAnalysis { get; set; } = true;
        public bool EnableAlerts { get; set; } = true;
        public decimal HighConfidenceThreshold { get; set; } = 0.8m;
        public decimal SignificantMoveThreshold { get; set; } = 2.0m;
        public decimal TradingThreshold { get; set; } = 0.65m;
    }
    
    public class PatternSubscriptionOptions
    {
        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);
        public int MinDataPoints { get; set; } = 60;
        public int BufferSize { get; set; } = 100;
        public decimal MinConfidence { get; set; } = 0.6m;
        public List<PatternType>? PatternFilter { get; set; }
        public bool RequireAlerts { get; set; } = false;
        public PatternRecognitionOptions RecognitionOptions { get; set; } = new();
    }
    
    internal class PatternSubscription
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public PatternSubscriptionOptions Options { get; set; } = null!;
        public Func<PatternRecognitionResult, Task> Callback { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }
    
    internal class PatternRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public List<MarketDataSnapshot>? Data { get; set; }
        public PatternRecognitionOptions? Options { get; set; }
        
        public void Reset()
        {
            Symbol = string.Empty;
            Data = null;
            Options = null;
        }
    }
    
    public class PatternAlert
    {
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public decimal PredictedChange { get; set; }
        public PatternType Pattern { get; set; }
    }
    
    public class TradingRecommendation
    {
        public string Action { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = string.Empty;
        public decimal SuggestedStopLoss { get; set; }
        public string TimeHorizon { get; set; } = string.Empty;
    }
    
    public class HistoricalPatternAnalysis
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalPatternsFound { get; set; }
        public Dictionary<PatternType, int> PatternDistribution { get; set; } = new();
        public Dictionary<PatternType, decimal> SuccessRates { get; set; } = new();
        public Dictionary<PatternType, TimeSpan> AverageDuration { get; set; } = new();
    }
    
    public enum AlertType
    {
        HighConfidencePattern,
        SignificantPriceMove,
        BreakoutPattern,
        VolumeAnomaly,
        TrendReversal
    }
    
    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}