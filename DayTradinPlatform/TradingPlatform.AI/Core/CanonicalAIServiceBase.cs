using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.AI.Models;

namespace TradingPlatform.AI.Core;

/// <summary>
/// Canonical base class for all AI services implementing standardized patterns
/// Provides common functionality for AI model lifecycle, inference, and performance monitoring
/// </summary>
public abstract class CanonicalAIServiceBase<TInput, TOutput> : CanonicalServiceBase
    where TInput : class 
    where TOutput : class
{
    protected readonly Dictionary<string, AIModelMetadata> _loadedModels;
    protected readonly AIModelConfiguration _configuration;
    protected readonly object _modelLock = new();

    protected CanonicalAIServiceBase(
        ITradingLogger logger, 
        string serviceName,
        AIModelConfiguration configuration) : base(logger, serviceName)
    {
        _loadedModels = new Dictionary<string, AIModelMetadata>();
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Canonical AI inference method with standardized error handling and performance monitoring
    /// </summary>
    public virtual async Task<TradingResult<TOutput>> InferAsync(TInput input, string? modelName = null)
    {
        LogMethodEntry();

        try
        {
            // Validate input
            var validationResult = await ValidateInputAsync(input);
            if (!validationResult.Success)
            {
                LogWarning($"Input validation failed: {validationResult.ErrorMessage}");
                return TradingResult<TOutput>.Failure(
                    "AI_INPUT_VALIDATION_FAILED",
                    validationResult.ErrorMessage ?? "Invalid input",
                    "The provided input does not meet the requirements for AI inference");
            }

            // Select model
            var selectedModel = await SelectOptimalModelAsync(input, modelName);
            if (!selectedModel.Success || selectedModel.Data == null)
            {
                LogError("Failed to select optimal AI model for inference");
                return TradingResult<TOutput>.Failure(
                    "AI_MODEL_SELECTION_FAILED",
                    selectedModel.ErrorMessage ?? "Model selection failed",
                    "Unable to select an appropriate AI model for the given input");
            }

            // Load model if not already loaded
            var modelLoadResult = await EnsureModelLoadedAsync(selectedModel.Data);
            if (!modelLoadResult.Success)
            {
                LogError($"Failed to load AI model: {selectedModel.Data.ModelName}");
                return TradingResult<TOutput>.Failure(
                    "AI_MODEL_LOAD_FAILED",
                    modelLoadResult.ErrorMessage ?? "Model loading failed",
                    "Unable to load the required AI model into memory");
            }

            // Perform inference with performance monitoring
            var inferenceStartTime = DateTime.UtcNow;
            var inferenceResult = await PerformInferenceAsync(input, selectedModel.Data);
            var inferenceLatency = DateTime.UtcNow - inferenceStartTime;

            if (!inferenceResult.Success || inferenceResult.Data == null)
            {
                LogError($"AI inference failed for model: {selectedModel.Data.ModelName}");
                return TradingResult<TOutput>.Failure(
                    "AI_INFERENCE_FAILED",
                    inferenceResult.ErrorMessage ?? "Inference failed",
                    "AI model inference completed but did not produce valid results");
            }

            // Post-process and validate output
            var postProcessResult = await PostProcessOutputAsync(inferenceResult.Data, selectedModel.Data);
            if (!postProcessResult.Success || postProcessResult.Data == null)
            {
                LogError("AI output post-processing failed");
                return TradingResult<TOutput>.Failure(
                    "AI_OUTPUT_PROCESSING_FAILED",
                    postProcessResult.ErrorMessage ?? "Output processing failed",
                    "AI inference completed but output processing failed");
            }

            // Update model performance metrics
            await UpdateModelPerformanceAsync(selectedModel.Data, inferenceLatency, true);

            LogInfo($"AI inference completed successfully: Model={selectedModel.Data.ModelName}, " +
                   $"Latency={inferenceLatency.TotalMilliseconds:F2}ms, " +
                   $"Confidence={GetOutputConfidence(postProcessResult.Data):P2}");

            return TradingResult<TOutput>.Success(postProcessResult.Data);
        }
        catch (Exception ex)
        {
            LogError("AI inference operation failed with exception", ex);
            return TradingResult<TOutput>.Failure(
                "AI_INFERENCE_EXCEPTION",
                ex.Message,
                "An unexpected error occurred during AI inference processing");
        }
        finally
        {
            LogMethodExit();
        }
    }

    /// <summary>
    /// Canonical batch inference for high-throughput scenarios
    /// </summary>
    public virtual async Task<TradingResult<List<TOutput>>> InferBatchAsync(
        List<TInput> inputs, string? modelName = null, int batchSize = 32)
    {
        LogMethodEntry();

        try
        {
            if (inputs?.Any() != true)
            {
                LogWarning("Empty or null input batch provided");
                return TradingResult<List<TOutput>>.Success(new List<TOutput>());
            }

            var results = new List<TOutput>();
            var batches = inputs.Chunk(batchSize);

            foreach (var batch in batches)
            {
                var batchResults = await ProcessBatchAsync(batch.ToList(), modelName);
                if (batchResults.Success && batchResults.Data != null)
                {
                    results.AddRange(batchResults.Data);
                }
                else
                {
                    LogWarning($"Batch processing failed: {batchResults.ErrorMessage}");
                    // Continue with next batch instead of failing entirely
                }
            }

            LogInfo($"Batch inference completed: {results.Count}/{inputs.Count} successful inferences");

            return TradingResult<List<TOutput>>.Success(results);
        }
        catch (Exception ex)
        {
            LogError("Batch AI inference failed", ex);
            return TradingResult<List<TOutput>>.Failure(
                "AI_BATCH_INFERENCE_FAILED",
                ex.Message,
                "Batch AI inference processing encountered an error");
        }
        finally
        {
            LogMethodExit();
        }
    }

    /// <summary>
    /// Get AI model health and performance metrics
    /// </summary>
    public virtual async Task<TradingResult<AIServiceHealth>> GetServiceHealthAsync()
    {
        LogMethodEntry();

        try
        {
            var loadedModelCount = 0;
            var totalInferences = 0L;
            var averageLatency = TimeSpan.Zero;
            var issues = new List<string>();

            lock (_modelLock)
            {
                loadedModelCount = _loadedModels.Count;
                totalInferences = _loadedModels.Values.Sum(m => m.InferenceCount);
                
                if (_loadedModels.Values.Any())
                {
                    averageLatency = TimeSpan.FromTicks(
                        (long)_loadedModels.Values.Average(m => m.AverageLatency.Ticks));
                }

                // Check for performance issues
                foreach (var model in _loadedModels.Values)
                {
                    if (model.AverageLatency > _configuration.PerformanceThresholds.MaxLatency)
                    {
                        issues.Add($"Model {model.ModelName} exceeds latency threshold: {model.AverageLatency.TotalMilliseconds:F2}ms");
                    }

                    if (model.ErrorRate > _configuration.PerformanceThresholds.MaxErrorRate)
                    {
                        issues.Add($"Model {model.ModelName} exceeds error rate threshold: {model.ErrorRate:P2}");
                    }
                }
            }

            var health = new AIServiceHealth
            {
                ServiceName = ServiceName,
                IsHealthy = issues.Count == 0,
                LoadedModels = loadedModelCount,
                TotalInferences = totalInferences,
                AverageLatency = averageLatency,
                Issues = issues,
                LastHealthCheck = DateTime.UtcNow,
                MemoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024)
            };

            await Task.CompletedTask; // Maintain async signature

            return TradingResult<AIServiceHealth>.Success(health);
        }
        catch (Exception ex)
        {
            LogError("Failed to get AI service health", ex);
            return TradingResult<AIServiceHealth>.Failure(
                "AI_HEALTH_CHECK_FAILED",
                ex.Message,
                "Unable to retrieve AI service health information");
        }
        finally
        {
            LogMethodExit();
        }
    }

    // Abstract methods to be implemented by derived classes
    protected abstract Task<TradingResult<bool>> ValidateInputAsync(TInput input);
    protected abstract Task<TradingResult<AIModelMetadata>> SelectOptimalModelAsync(TInput input, string? modelName);
    protected abstract Task<TradingResult<bool>> EnsureModelLoadedAsync(AIModelMetadata model);
    protected abstract Task<TradingResult<TOutput>> PerformInferenceAsync(TInput input, AIModelMetadata model);
    protected abstract Task<TradingResult<TOutput>> PostProcessOutputAsync(TOutput rawOutput, AIModelMetadata model);
    protected abstract decimal GetOutputConfidence(TOutput output);

    // Virtual methods with default implementations
    protected virtual async Task<TradingResult<List<TOutput>>> ProcessBatchAsync(List<TInput> batch, string? modelName)
    {
        LogMethodEntry();

        try
        {
            var tasks = batch.Select(input => InferAsync(input, modelName));
            var results = await Task.WhenAll(tasks);

            var successfulResults = results
                .Where(r => r.Success && r.Data != null)
                .Select(r => r.Data!)
                .ToList();

            return TradingResult<List<TOutput>>.Success(successfulResults);
        }
        catch (Exception ex)
        {
            LogError("Failed to process batch", ex);
            return TradingResult<List<TOutput>>.Failure(
                "BATCH_PROCESSING_FAILED",
                ex.Message,
                "Unable to process inference batch");
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected virtual async Task UpdateModelPerformanceAsync(
        AIModelMetadata model, TimeSpan latency, bool success)
    {
        LogMethodEntry();

        try
        {
            lock (_modelLock)
            {
                model.InferenceCount++;
                
                if (success)
                {
                    model.SuccessfulInferences++;
                }
                else
                {
                    model.FailedInferences++;
                }

                // Update rolling average latency
                var totalLatencyTicks = model.AverageLatency.Ticks * (model.InferenceCount - 1) + latency.Ticks;
                model.AverageLatency = TimeSpan.FromTicks(totalLatencyTicks / model.InferenceCount);

                // Update error rate
                model.ErrorRate = (decimal)model.FailedInferences / model.InferenceCount;

                model.LastUsed = DateTime.UtcNow;
            }

            await Task.CompletedTask; // Maintain async signature
        }
        catch (Exception ex)
        {
            LogError($"Failed to update performance metrics for model {model.ModelName}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected virtual async Task<TradingResult<bool>> UnloadUnusedModelsAsync()
    {
        LogMethodEntry();

        try
        {
            var modelsToUnload = new List<string>();
            var cutoffTime = DateTime.UtcNow - _configuration.ModelCacheSettings.UnloadAfterInactivity;

            lock (_modelLock)
            {
                foreach (var kvp in _loadedModels)
                {
                    if (kvp.Value.LastUsed < cutoffTime && kvp.Value.CanUnload)
                    {
                        modelsToUnload.Add(kvp.Key);
                    }
                }

                foreach (var modelName in modelsToUnload)
                {
                    if (_loadedModels.TryGetValue(modelName, out var model))
                    {
                        model.Dispose();
                        _loadedModels.Remove(modelName);
                    }
                }
            }

            if (modelsToUnload.Any())
            {
                LogInfo($"Unloaded {modelsToUnload.Count} inactive AI models: {string.Join(", ", modelsToUnload)}");
                
                // Force garbage collection after unloading models
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            await Task.CompletedTask; // Maintain async signature

            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Failed to unload unused AI models", ex);
            return TradingResult<bool>.Failure(
                "MODEL_UNLOAD_FAILED",
                ex.Message,
                "Unable to unload inactive AI models from memory");
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_modelLock)
            {
                foreach (var model in _loadedModels.Values)
                {
                    model.Dispose();
                }
                _loadedModels.Clear();
            }
        }
        
        base.Dispose(disposing);
    }
}