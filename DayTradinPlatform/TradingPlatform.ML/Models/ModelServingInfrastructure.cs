// File: TradingPlatform.ML/Models/ModelServingInfrastructure.cs

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Models;
using TradingPlatform.ML.Interfaces;

namespace TradingPlatform.ML.Models
{
    /// <summary>
    /// Infrastructure for serving ML models in production
    /// </summary>
    public class ModelServingInfrastructure : CanonicalServiceBase
    {
        private readonly ConcurrentDictionary<string, IModelInstance> _loadedModels;
        private readonly ConcurrentDictionary<string, ModelMetrics> _modelMetrics;
        private readonly SemaphoreSlim _loadLock;
        private readonly Timer _metricsTimer;
        
        public ModelServingInfrastructure(
            IServiceProvider serviceProvider,
            ITradingLogger logger)
            : base(serviceProvider, logger, "ModelServingInfrastructure")
        {
            _loadedModels = new ConcurrentDictionary<string, IModelInstance>();
            _modelMetrics = new ConcurrentDictionary<string, ModelMetrics>();
            _loadLock = new SemaphoreSlim(1, 1);
            
            // Start metrics collection timer
            _metricsTimer = new Timer(
                CollectMetrics,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(1));
        }
        
        /// <summary>
        /// Load a model for serving
        /// </summary>
        public async Task<TradingResult<ModelServingInfo>> LoadModelAsync(
            string modelId,
            string modelPath,
            ModelType modelType,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    await _loadLock.WaitAsync(cancellationToken);
                    try
                    {
                        if (_loadedModels.ContainsKey(modelId))
                        {
                            LogWarning($"Model {modelId} is already loaded");
                            return TradingResult<ModelServingInfo>.Failure(
                                new Exception($"Model {modelId} is already loaded"));
                        }
                        
                        LogInfo($"Loading model {modelId} from {modelPath}",
                            additionalData: new { ModelType = modelType });
                        
                        // Create model instance based on type
                        IModelInstance modelInstance = modelType switch
                        {
                            ModelType.XGBoostPrice => await LoadXGBoostModel(modelPath, cancellationToken),
                            ModelType.LSTMPattern => await LoadLSTMModel(modelPath, cancellationToken),
                            ModelType.RandomForestRanking => await LoadRandomForestModel(modelPath, cancellationToken),
                            _ => throw new NotSupportedException($"Model type {modelType} not supported")
                        };
                        
                        // Warm up the model
                        await WarmUpModel(modelInstance, cancellationToken);
                        
                        // Register model
                        _loadedModels[modelId] = modelInstance;
                        _modelMetrics[modelId] = new ModelMetrics { ModelId = modelId };
                        
                        var servingInfo = new ModelServingInfo
                        {
                            ModelId = modelId,
                            ModelType = modelType,
                            LoadedAt = DateTime.UtcNow,
                            Version = modelInstance.Version,
                            IsActive = true,
                            EndpointUrl = $"/api/ml/predict/{modelId}"
                        };
                        
                        RecordServiceMetric("ModelLoaded", 1, new { modelId });
                        LogInfo($"Model {modelId} loaded successfully");
                        
                        return TradingResult<ModelServingInfo>.Success(servingInfo);
                    }
                    finally
                    {
                        _loadLock.Release();
                    }
                },
                nameof(LoadModelAsync));
        }
        
        /// <summary>
        /// Unload a model from serving
        /// </summary>
        public async Task<TradingResult<bool>> UnloadModelAsync(
            string modelId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (!_loadedModels.TryRemove(modelId, out var modelInstance))
                    {
                        LogWarning($"Model {modelId} not found");
                        return TradingResult<bool>.Failure(
                            new Exception($"Model {modelId} not found"));
                    }
                    
                    // Dispose model resources
                    if (modelInstance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    
                    _modelMetrics.TryRemove(modelId, out _);
                    
                    RecordServiceMetric("ModelUnloaded", 1, new { modelId });
                    LogInfo($"Model {modelId} unloaded successfully");
                    
                    return TradingResult<bool>.Success(true);
                },
                nameof(UnloadModelAsync));
        }
        
        /// <summary>
        /// Get prediction from a loaded model
        /// </summary>
        public async Task<TradingResult<TPrediction>> PredictAsync<TInput, TPrediction>(
            string modelId,
            TInput input,
            CancellationToken cancellationToken = default)
            where TInput : class
            where TPrediction : class
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (!_loadedModels.TryGetValue(modelId, out var modelInstance))
                    {
                        return TradingResult<TPrediction>.Failure(
                            new Exception($"Model {modelId} not found"));
                    }
                    
                    if (modelInstance is not IPredictiveModel<TInput, TPrediction> predictiveModel)
                    {
                        return TradingResult<TPrediction>.Failure(
                            new Exception($"Model {modelId} does not support input/output types"));
                    }
                    
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    
                    try
                    {
                        var result = await predictiveModel.PredictAsync(input, cancellationToken);
                        
                        // Update metrics
                        if (_modelMetrics.TryGetValue(modelId, out var metrics))
                        {
                            metrics.PredictionCount++;
                            metrics.TotalLatency += stopwatch.ElapsedMilliseconds;
                            metrics.LastPredictionTime = DateTime.UtcNow;
                            
                            if (result.IsSuccess)
                            {
                                metrics.SuccessCount++;
                            }
                            else
                            {
                                metrics.ErrorCount++;
                            }
                        }
                        
                        RecordServiceMetric($"Prediction.{modelId}.Latency", 
                            stopwatch.ElapsedMilliseconds);
                        
                        return result;
                    }
                    catch (Exception ex)
                    {
                        if (_modelMetrics.TryGetValue(modelId, out var metrics))
                        {
                            metrics.ErrorCount++;
                        }
                        
                        LogError($"Prediction failed for model {modelId}", ex);
                        return TradingResult<TPrediction>.Failure(ex);
                    }
                },
                nameof(PredictAsync));
        }
        
        /// <summary>
        /// Get batch predictions from a loaded model
        /// </summary>
        public async Task<TradingResult<List<TPrediction>>> PredictBatchAsync<TInput, TPrediction>(
            string modelId,
            List<TInput> inputs,
            CancellationToken cancellationToken = default)
            where TInput : class
            where TPrediction : class
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (!_loadedModels.TryGetValue(modelId, out var modelInstance))
                    {
                        return TradingResult<List<TPrediction>>.Failure(
                            new Exception($"Model {modelId} not found"));
                    }
                    
                    if (modelInstance is not IPredictiveModel<TInput, TPrediction> predictiveModel)
                    {
                        return TradingResult<List<TPrediction>>.Failure(
                            new Exception($"Model {modelId} does not support input/output types"));
                    }
                    
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    
                    try
                    {
                        var result = await predictiveModel.PredictBatchAsync(inputs, cancellationToken);
                        
                        // Update metrics
                        if (_modelMetrics.TryGetValue(modelId, out var metrics))
                        {
                            metrics.PredictionCount += inputs.Count;
                            metrics.TotalLatency += stopwatch.ElapsedMilliseconds;
                            metrics.LastPredictionTime = DateTime.UtcNow;
                            
                            if (result.IsSuccess)
                            {
                                metrics.SuccessCount += inputs.Count;
                            }
                            else
                            {
                                metrics.ErrorCount += inputs.Count;
                            }
                        }
                        
                        RecordServiceMetric($"BatchPrediction.{modelId}.Latency", 
                            stopwatch.ElapsedMilliseconds);
                        RecordServiceMetric($"BatchPrediction.{modelId}.Size", 
                            inputs.Count);
                        
                        return result;
                    }
                    catch (Exception ex)
                    {
                        if (_modelMetrics.TryGetValue(modelId, out var metrics))
                        {
                            metrics.ErrorCount += inputs.Count;
                        }
                        
                        LogError($"Batch prediction failed for model {modelId}", ex);
                        return TradingResult<List<TPrediction>>.Failure(ex);
                    }
                },
                nameof(PredictBatchAsync));
        }
        
        /// <summary>
        /// Get serving status for all models
        /// </summary>
        public async Task<TradingResult<ModelServingStatus>> GetServingStatusAsync()
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var modelStatuses = new List<ModelStatus>();
                    
                    foreach (var (modelId, modelInstance) in _loadedModels)
                    {
                        var metrics = _modelMetrics.GetValueOrDefault(modelId) ?? new ModelMetrics();
                        
                        var status = new ModelStatus
                        {
                            ModelId = modelId,
                            IsActive = true,
                            Version = modelInstance.Version,
                            LoadedAt = modelInstance.LoadedAt,
                            PredictionCount = metrics.PredictionCount,
                            SuccessRate = metrics.PredictionCount > 0 
                                ? (double)metrics.SuccessCount / metrics.PredictionCount 
                                : 0,
                            AverageLatencyMs = metrics.PredictionCount > 0
                                ? metrics.TotalLatency / metrics.PredictionCount
                                : 0,
                            LastPredictionTime = metrics.LastPredictionTime,
                            ErrorCount = metrics.ErrorCount
                        };
                        
                        modelStatuses.Add(status);
                    }
                    
                    var servingStatus = new ModelServingStatus
                    {
                        TotalModelsLoaded = _loadedModels.Count,
                        Models = modelStatuses,
                        SystemHealth = CalculateSystemHealth(modelStatuses),
                        LastHealthCheck = DateTime.UtcNow
                    };
                    
                    return TradingResult<ModelServingStatus>.Success(servingStatus);
                },
                nameof(GetServingStatusAsync));
        }
        
        /// <summary>
        /// Enable A/B testing between models
        /// </summary>
        public async Task<TradingResult<ABTestConfig>> ConfigureABTestAsync(
            string testId,
            string modelA,
            string modelB,
            double trafficSplitA = 0.5,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (!_loadedModels.ContainsKey(modelA) || !_loadedModels.ContainsKey(modelB))
                    {
                        return TradingResult<ABTestConfig>.Failure(
                            new Exception("Both models must be loaded for A/B testing"));
                    }
                    
                    var config = new ABTestConfig
                    {
                        TestId = testId,
                        ModelA = modelA,
                        ModelB = modelB,
                        TrafficSplitA = trafficSplitA,
                        StartTime = DateTime.UtcNow,
                        IsActive = true
                    };
                    
                    // Store A/B test configuration
                    // In production, this would be persisted
                    
                    LogInfo($"A/B test {testId} configured",
                        additionalData: config);
                    
                    return TradingResult<ABTestConfig>.Success(config);
                },
                nameof(ConfigureABTestAsync));
        }
        
        // Helper methods
        
        private async Task<IModelInstance> LoadXGBoostModel(string modelPath, CancellationToken cancellationToken)
        {
            // Load XGBoost model
            var mlContext = ServiceProvider.GetRequiredService<Microsoft.ML.MLContext>();
            var model = new XGBoostPriceModel(ServiceProvider, Logger, mlContext);
            
            var loadResult = await model.LoadAsync(modelPath, cancellationToken);
            if (!loadResult.IsSuccess)
            {
                throw new Exception($"Failed to load XGBoost model: {loadResult.Error?.Message}");
            }
            
            return new ModelInstance<PricePredictionInput, PricePrediction>
            {
                Model = model,
                Version = "1.0",
                LoadedAt = DateTime.UtcNow
            };
        }
        
        private async Task<IModelInstance> LoadLSTMModel(string modelPath, CancellationToken cancellationToken)
        {
            // Placeholder for LSTM model loading
            throw new NotImplementedException("LSTM model loading not yet implemented");
        }
        
        private async Task<IModelInstance> LoadRandomForestModel(string modelPath, CancellationToken cancellationToken)
        {
            // Placeholder for Random Forest model loading
            throw new NotImplementedException("Random Forest model loading not yet implemented");
        }
        
        private async Task WarmUpModel(IModelInstance modelInstance, CancellationToken cancellationToken)
        {
            LogInfo("Warming up model...");
            
            // Create dummy input for warm-up
            if (modelInstance is ModelInstance<PricePredictionInput, PricePrediction> priceModel)
            {
                var dummyInput = new PricePredictionInput
                {
                    Open = 100f,
                    High = 101f,
                    Low = 99f,
                    Close = 100f,
                    Volume = 1000000f,
                    RSI = 50f,
                    MACD = 0f,
                    VolumeRatio = 1f
                };
                
                // Run a few predictions to warm up
                for (int i = 0; i < 5; i++)
                {
                    await priceModel.Model.PredictAsync(dummyInput, cancellationToken);
                }
            }
            
            LogInfo("Model warm-up completed");
        }
        
        private void CollectMetrics(object? state)
        {
            try
            {
                foreach (var (modelId, metrics) in _modelMetrics)
                {
                    if (metrics.PredictionCount > 0)
                    {
                        var avgLatency = metrics.TotalLatency / metrics.PredictionCount;
                        var successRate = (double)metrics.SuccessCount / metrics.PredictionCount;
                        
                        RecordServiceMetric($"Model.{modelId}.AvgLatency", avgLatency);
                        RecordServiceMetric($"Model.{modelId}.SuccessRate", successRate);
                        RecordServiceMetric($"Model.{modelId}.PredictionCount", metrics.PredictionCount);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Error collecting model metrics", ex);
            }
        }
        
        private string CalculateSystemHealth(List<ModelStatus> modelStatuses)
        {
            if (!modelStatuses.Any()) return "NoModels";
            
            var avgSuccessRate = modelStatuses.Average(m => m.SuccessRate);
            var avgLatency = modelStatuses.Average(m => m.AverageLatencyMs);
            
            if (avgSuccessRate > 0.95 && avgLatency < 50)
                return "Healthy";
            else if (avgSuccessRate > 0.8 && avgLatency < 100)
                return "Good";
            else if (avgSuccessRate > 0.6)
                return "Degraded";
            else
                return "Unhealthy";
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _metricsTimer?.Dispose();
                _loadLock?.Dispose();
                
                // Unload all models
                foreach (var modelId in _loadedModels.Keys.ToList())
                {
                    UnloadModelAsync(modelId).Wait();
                }
            }
            
            base.Dispose(disposing);
        }
    }
    
    // Supporting classes
    
    public interface IModelInstance
    {
        string Version { get; }
        DateTime LoadedAt { get; }
    }
    
    public class ModelInstance<TInput, TPrediction> : IModelInstance
        where TInput : class
        where TPrediction : class
    {
        public IPredictiveModel<TInput, TPrediction> Model { get; set; } = null!;
        public string Version { get; set; } = string.Empty;
        public DateTime LoadedAt { get; set; }
    }
    
    public class ModelServingInfo
    {
        public string ModelId { get; set; } = string.Empty;
        public ModelType ModelType { get; set; }
        public DateTime LoadedAt { get; set; }
        public string Version { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string EndpointUrl { get; set; } = string.Empty;
    }
    
    public class ModelMetrics
    {
        public string ModelId { get; set; } = string.Empty;
        public long PredictionCount { get; set; }
        public long SuccessCount { get; set; }
        public long ErrorCount { get; set; }
        public double TotalLatency { get; set; }
        public DateTime? LastPredictionTime { get; set; }
    }
    
    public class ModelServingStatus
    {
        public int TotalModelsLoaded { get; set; }
        public List<ModelStatus> Models { get; set; } = new();
        public string SystemHealth { get; set; } = string.Empty;
        public DateTime LastHealthCheck { get; set; }
    }
    
    public class ModelStatus
    {
        public string ModelId { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string Version { get; set; } = string.Empty;
        public DateTime LoadedAt { get; set; }
        public long PredictionCount { get; set; }
        public double SuccessRate { get; set; }
        public double AverageLatencyMs { get; set; }
        public DateTime? LastPredictionTime { get; set; }
        public long ErrorCount { get; set; }
    }
    
    public class ABTestConfig
    {
        public string TestId { get; set; } = string.Empty;
        public string ModelA { get; set; } = string.Empty;
        public string ModelB { get; set; } = string.Empty;
        public double TrafficSplitA { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsActive { get; set; }
    }
    
    public enum ModelType
    {
        XGBoostPrice,
        LSTMPattern,
        RandomForestRanking
    }
}