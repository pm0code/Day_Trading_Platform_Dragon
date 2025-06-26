// File: TradingPlatform.ML/Services/CanonicalMLService.cs

using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Models;
using TradingPlatform.ML.Interfaces;
using TradingPlatform.ML.Models;

namespace TradingPlatform.ML.Services
{
    /// <summary>
    /// Canonical base class for ML services providing consistent patterns
    /// </summary>
    public abstract class CanonicalMLService<TModel> : CanonicalServiceBase
        where TModel : IMLModel
    {
        protected readonly Dictionary<string, TModel> _loadedModels = new();
        protected readonly object _modelLock = new();
        
        protected CanonicalMLService(
            IServiceProvider serviceProvider, 
            ITradingLogger logger, 
            string serviceName) 
            : base(serviceProvider, logger, serviceName)
        {
        }
        
        /// <summary>
        /// Train a model with comprehensive monitoring
        /// </summary>
        protected async Task<TradingResult<ModelTrainingResult>> TrainModelAsync(
            TModel model,
            IMLDataset dataset,
            ModelTrainingOptions options,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    LogInfo($"Starting {model.ModelType} training",
                        additionalData: new { 
                            ModelId = model.ModelId,
                            DatasetSize = dataset.SampleCount,
                            Options = options
                        });
                    
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    
                    // Validate dataset
                    if (dataset.SampleCount < options.BatchSize)
                    {
                        return TradingResult<ModelTrainingResult>.Failure(
                            new TradingError("ML001", "Dataset too small for training"));
                    }
                    
                    // Train model
                    var result = await model.TrainAsync(dataset, options, cancellationToken);
                    
                    if (result.IsSuccess)
                    {
                        stopwatch.Stop();
                        
                        // Record metrics
                        RecordServiceMetric("Model.Training.Duration", stopwatch.ElapsedMilliseconds);
                        RecordServiceMetric($"Model.{model.ModelType}.Accuracy", result.Value.ValidationAccuracy);
                        RecordServiceMetric($"Model.{model.ModelType}.Loss", result.Value.FinalLoss);
                        
                        LogInfo($"{model.ModelType} training completed successfully",
                            additionalData: new {
                                Duration = stopwatch.Elapsed,
                                Accuracy = result.Value.ValidationAccuracy,
                                Loss = result.Value.FinalLoss
                            });
                        
                        // Cache trained model
                        lock (_modelLock)
                        {
                            _loadedModels[model.ModelId] = model;
                        }
                    }
                    else
                    {
                        LogError($"{model.ModelType} training failed", null,
                            "ML training", result.Error?.Message ?? "Unknown error");
                    }
                    
                    return result;
                },
                $"Train{model.ModelType}");
        }
        
        /// <summary>
        /// Make predictions with latency tracking
        /// </summary>
        protected async Task<TradingResult<TOutput>> PredictAsync<TInput, TOutput>(
            IPredictiveModel<TInput, TOutput> model,
            TInput input,
            CancellationToken cancellationToken = default)
            where TInput : class
            where TOutput : class
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    
                    var result = await model.PredictAsync(input, cancellationToken);
                    
                    stopwatch.Stop();
                    
                    // Track prediction latency
                    RecordServiceMetric($"Model.{model.ModelType}.PredictionLatency", stopwatch.ElapsedMilliseconds);
                    
                    if (stopwatch.ElapsedMilliseconds > 50) // Alert if >50ms
                    {
                        LogWarning($"{model.ModelType} prediction latency exceeded threshold",
                            additionalData: new { Latency = stopwatch.ElapsedMilliseconds });
                    }
                    
                    return result;
                },
                $"Predict{model.ModelType}");
        }
        
        /// <summary>
        /// Load model with caching
        /// </summary>
        protected async Task<TradingResult<TModel>> LoadModelAsync(
            string modelId,
            string path,
            Func<Task<TModel>> modelFactory,
            CancellationToken cancellationToken = default)
        {
            // Check cache first
            lock (_modelLock)
            {
                if (_loadedModels.TryGetValue(modelId, out var cachedModel))
                {
                    LogInfo($"Model {modelId} loaded from cache");
                    return TradingResult<TModel>.Success(cachedModel);
                }
            }
            
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var model = await modelFactory();
                    var loadResult = await model.LoadAsync(path, cancellationToken);
                    
                    if (loadResult.IsSuccess)
                    {
                        lock (_modelLock)
                        {
                            _loadedModels[modelId] = model;
                        }
                        
                        LogInfo($"Model {modelId} loaded successfully",
                            additionalData: new { Path = path, ModelType = model.ModelType });
                        
                        return TradingResult<TModel>.Success(model);
                    }
                    
                    return TradingResult<TModel>.Failure(loadResult.Error);
                },
                $"LoadModel_{modelId}");
        }
        
        /// <summary>
        /// Evaluate model performance
        /// </summary>
        protected async Task<TradingResult<ModelEvaluationResult>> EvaluateModelAsync(
            TModel model,
            IMLDataset testData,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    LogInfo($"Evaluating {model.ModelType} model",
                        additionalData: new { ModelId = model.ModelId, TestDataSize = testData.SampleCount });
                    
                    var result = await model.EvaluateAsync(testData, cancellationToken);
                    
                    if (result.IsSuccess)
                    {
                        // Record evaluation metrics
                        RecordServiceMetric($"Model.{model.ModelType}.Evaluation.Accuracy", result.Value.Accuracy);
                        RecordServiceMetric($"Model.{model.ModelType}.Evaluation.F1Score", result.Value.F1Score);
                        RecordServiceMetric($"Model.{model.ModelType}.Evaluation.RMSE", result.Value.RootMeanSquaredError);
                        
                        LogInfo($"{model.ModelType} evaluation completed",
                            additionalData: result.Value);
                    }
                    
                    return result;
                },
                $"Evaluate{model.ModelType}");
        }
        
        /// <summary>
        /// Clear model cache
        /// </summary>
        public void ClearModelCache()
        {
            lock (_modelLock)
            {
                var count = _loadedModels.Count;
                _loadedModels.Clear();
                LogInfo($"Cleared {count} models from cache");
            }
        }
        
        /// <summary>
        /// Get loaded models info
        /// </summary>
        public Dictionary<string, string> GetLoadedModelsInfo()
        {
            lock (_modelLock)
            {
                return _loadedModels.ToDictionary(
                    kvp => kvp.Key,
                    kvp => $"{kvp.Value.ModelType} v{kvp.Value.Version}");
            }
        }
    }
}