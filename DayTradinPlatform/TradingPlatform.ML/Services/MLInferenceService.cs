using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.GPU.Infrastructure;
using TradingPlatform.ML.Configuration;
using TradingPlatform.ML.Interfaces;
using TradingPlatform.ML.Models;

namespace TradingPlatform.ML.Services
{
    /// <summary>
    /// ML inference service using ONNX Runtime with GPU acceleration support
    /// </summary>
    public class MLInferenceService : CanonicalServiceBaseEnhanced, IMLInferenceService
    {
        private readonly ConcurrentDictionary<string, InferenceSession> _modelSessions;
        private readonly ConcurrentDictionary<string, ModelMetadata> _modelMetadata;
        private readonly MLInferenceConfiguration _config;
        private readonly GpuContext? _gpuContext;
        private readonly IMLPerformanceMonitor _performanceMonitor;
        private readonly string _modelsBasePath;
        private SessionOptions? _sessionOptions;

        public MLInferenceService(
            MLInferenceConfiguration config,
            GpuContext? gpuContext = null,
            IMLPerformanceMonitor? performanceMonitor = null,
            ITradingLogger? logger = null)
            : base(logger, "MLInferenceService")
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _gpuContext = gpuContext;
            _performanceMonitor = performanceMonitor ?? new DefaultMLPerformanceMonitor();
            _modelSessions = new ConcurrentDictionary<string, InferenceSession>();
            _modelMetadata = new ConcurrentDictionary<string, ModelMetadata>();
            
            // Set up models base path
            _modelsBasePath = Path.IsPathRooted(_config.ModelsPath) 
                ? _config.ModelsPath 
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _config.ModelsPath);
            
            InitializeExecutionProviders();
        }

        private void InitializeExecutionProviders()
        {
            LogInfo("ML_INFERENCE_INIT", "Initializing ML inference execution providers");
            
            // Check available providers
            var availableProviders = OrtEnv.Instance().GetAvailableProviders();
            
            LogInfo("ML_PROVIDERS_AVAILABLE", "Available execution providers", 
                additionalData: new { Providers = availableProviders });
            
            // Create session options
            _sessionOptions = CreateSessionOptions();
        }

        public async Task<TradingResult<ModelPrediction>> PredictAsync(
            string modelName,
            float[] inputData,
            int[] inputShape)
        {
            return await TrackOperationAsync($"Predict-{modelName}", async () =>
            {
                ValidateInput(modelName, inputData, inputShape);
                
                var session = await GetOrLoadModelAsync(modelName);
                
                // Create input tensor
                var inputTensor = new DenseTensor<float>(inputData, inputShape);
                var inputMetadata = session.InputMetadata;
                var inputName = inputMetadata.Keys.First();
                
                var inputs = new List<NamedOnnxValue> 
                { 
                    NamedOnnxValue.CreateFromTensor(inputName, inputTensor) 
                };
                
                // Run inference
                var stopwatch = Stopwatch.StartNew();
                ModelPrediction prediction;
                
                try
                {
                    using var results = session.Run(inputs);
                    stopwatch.Stop();
                    
                    // Extract output
                    var outputTensor = results.First();
                    var output = outputTensor.AsTensor<float>().ToArray();
                    
                    prediction = new ModelPrediction
                    {
                        ModelName = modelName,
                        Predictions = output,
                        InferenceTimeMs = stopwatch.ElapsedMilliseconds,
                        Timestamp = DateTime.UtcNow
                    };
                    
                    // Record successful inference
                    _performanceMonitor.RecordInference(modelName, stopwatch.ElapsedMilliseconds, true);
                    
                    LogDebug("ML_INFERENCE_COMPLETE", $"Model {modelName} inference completed",
                        additionalData: new 
                        { 
                            ModelName = modelName,
                            InferenceTimeMs = stopwatch.ElapsedMilliseconds,
                            InputShape = inputShape,
                            OutputLength = output.Length
                        });
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _performanceMonitor.RecordInference(modelName, stopwatch.ElapsedMilliseconds, false);
                    throw new InvalidOperationException($"Inference failed for model {modelName}", ex);
                }
                
                return TradingResult<ModelPrediction>.Success(prediction);
            });
        }

        public async Task<TradingResult<ModelPredictionBatch>> PredictBatchAsync(
            string modelName,
            float[][] batchData,
            int[] inputShape)
        {
            return await TrackOperationAsync($"PredictBatch-{modelName}", async () =>
            {
                if (batchData == null || batchData.Length == 0)
                    throw new ArgumentException("Batch data cannot be null or empty");
                
                var batchSize = batchData.Length;
                var batchShape = new int[inputShape.Length + 1];
                batchShape[0] = batchSize;
                Array.Copy(inputShape, 0, batchShape, 1, inputShape.Length);
                
                // Flatten batch data
                var flattenedData = new float[batchSize * batchData[0].Length];
                for (int i = 0; i < batchSize; i++)
                {
                    Array.Copy(batchData[i], 0, flattenedData, i * batchData[i].Length, batchData[i].Length);
                }
                
                var session = await GetOrLoadModelAsync(modelName);
                
                // Create batch tensor
                var inputTensor = new DenseTensor<float>(flattenedData, batchShape);
                var inputName = session.InputMetadata.Keys.First();
                
                var inputs = new List<NamedOnnxValue> 
                { 
                    NamedOnnxValue.CreateFromTensor(inputName, inputTensor) 
                };
                
                // Run batch inference
                var stopwatch = Stopwatch.StartNew();
                var predictions = new List<ModelPrediction>();
                
                try
                {
                    using var results = session.Run(inputs);
                    stopwatch.Stop();
                    
                    var outputTensor = results.First().AsTensor<float>();
                    var outputArray = outputTensor.ToArray();
                    var outputPerItem = outputArray.Length / batchSize;
                    
                    // Split results by batch
                    for (int i = 0; i < batchSize; i++)
                    {
                        var itemPredictions = new float[outputPerItem];
                        Array.Copy(outputArray, i * outputPerItem, itemPredictions, 0, outputPerItem);
                        
                        predictions.Add(new ModelPrediction
                        {
                            ModelName = modelName,
                            Predictions = itemPredictions,
                            InferenceTimeMs = stopwatch.ElapsedMilliseconds / batchSize,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    
                    _performanceMonitor.RecordInference(modelName, stopwatch.ElapsedMilliseconds, true);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _performanceMonitor.RecordInference(modelName, stopwatch.ElapsedMilliseconds, false);
                    throw new InvalidOperationException($"Batch inference failed for model {modelName}", ex);
                }
                
                var batchResult = new ModelPredictionBatch
                {
                    ModelName = modelName,
                    Predictions = predictions,
                    TotalInferenceTimeMs = stopwatch.ElapsedMilliseconds
                };
                
                LogDebug("ML_BATCH_INFERENCE_COMPLETE", $"Batch inference completed for {modelName}",
                    additionalData: new 
                    { 
                        ModelName = modelName,
                        BatchSize = batchSize,
                        TotalTimeMs = stopwatch.ElapsedMilliseconds,
                        AvgTimeMs = batchResult.AverageInferenceTimeMs
                    });
                
                return TradingResult<ModelPredictionBatch>.Success(batchResult);
            });
        }

        public async Task<TradingResult<Dictionary<string, float[]>>> PredictMultiInputAsync(
            string modelName,
            Dictionary<string, float[]> inputs)
        {
            return await TrackOperationAsync($"PredictMultiInput-{modelName}", async () =>
            {
                if (inputs == null || inputs.Count == 0)
                    throw new ArgumentException("Inputs cannot be null or empty");
                
                var session = await GetOrLoadModelAsync(modelName);
                
                // Create input tensors
                var namedInputs = new List<NamedOnnxValue>();
                foreach (var kvp in inputs)
                {
                    var inputMeta = session.InputMetadata[kvp.Key];
                    var shape = inputMeta.Dimensions.ToArray();
                    var tensor = new DenseTensor<float>(kvp.Value, shape);
                    namedInputs.Add(NamedOnnxValue.CreateFromTensor(kvp.Key, tensor));
                }
                
                // Run inference
                var stopwatch = Stopwatch.StartNew();
                Dictionary<string, float[]> outputDict;
                
                try
                {
                    using var results = session.Run(namedInputs);
                    stopwatch.Stop();
                    
                    outputDict = new Dictionary<string, float[]>();
                    foreach (var result in results)
                    {
                        var outputArray = result.AsTensor<float>().ToArray();
                        outputDict[result.Name] = outputArray;
                    }
                    
                    _performanceMonitor.RecordInference(modelName, stopwatch.ElapsedMilliseconds, true);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _performanceMonitor.RecordInference(modelName, stopwatch.ElapsedMilliseconds, false);
                    throw new InvalidOperationException($"Multi-input inference failed for model {modelName}", ex);
                }
                
                LogDebug("ML_MULTI_INPUT_COMPLETE", $"Multi-input inference completed for {modelName}",
                    additionalData: new 
                    { 
                        ModelName = modelName,
                        InputCount = inputs.Count,
                        OutputCount = outputDict.Count,
                        InferenceTimeMs = stopwatch.ElapsedMilliseconds
                    });
                
                return TradingResult<Dictionary<string, float[]>>.Success(outputDict);
            });
        }

        public async Task<TradingResult<ModelMetadata>> LoadModelAsync(string modelName, string? modelPath = null)
        {
            return await TrackOperationAsync($"LoadModel-{modelName}", async () =>
            {
                if (_modelSessions.ContainsKey(modelName))
                {
                    LogWarning("ML_MODEL_ALREADY_LOADED", $"Model {modelName} is already loaded");
                    return TradingResult<ModelMetadata>.Success(_modelMetadata[modelName]);
                }
                
                var fullPath = modelPath ?? GetModelPath(modelName);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"Model file not found: {fullPath}");
                }
                
                var fileInfo = new FileInfo(fullPath);
                var session = await Task.Run(() => new InferenceSession(fullPath, _sessionOptions));
                
                // Extract metadata
                var metadata = new ModelMetadata
                {
                    ModelName = modelName,
                    LoadedAt = DateTime.UtcNow,
                    ModelSizeBytes = fileInfo.Length,
                    InputMetadata = ExtractTensorMetadata(session.InputMetadata),
                    OutputMetadata = ExtractTensorMetadata(session.OutputMetadata)
                };
                
                // Add model-specific configuration if available
                if (_config.ModelConfigs.TryGetValue(modelName, out var modelConfig))
                {
                    metadata.Version = modelConfig.Version;
                    metadata.Properties = modelConfig.Metadata;
                }
                
                _modelSessions[modelName] = session;
                _modelMetadata[modelName] = metadata;
                
                LogInfo("ML_MODEL_LOADED", $"Model {modelName} loaded successfully",
                    additionalData: new 
                    { 
                        ModelName = modelName,
                        Path = fullPath,
                        SizeMB = fileInfo.Length / (1024.0 * 1024.0),
                        InputCount = metadata.InputMetadata.Count,
                        OutputCount = metadata.OutputMetadata.Count
                    });
                
                // Perform warmup if configured
                if (_config.WarmupIterations > 0)
                {
                    await WarmupModelAsync(modelName, _config.WarmupIterations);
                }
                
                return TradingResult<ModelMetadata>.Success(metadata);
            });
        }

        public async Task<TradingResult> UnloadModelAsync(string modelName)
        {
            return await TrackOperationAsync($"UnloadModel-{modelName}", async () =>
            {
                if (_modelSessions.TryRemove(modelName, out var session))
                {
                    _modelMetadata.TryRemove(modelName, out _);
                    session.Dispose();
                    
                    LogInfo("ML_MODEL_UNLOADED", $"Model {modelName} unloaded successfully");
                    return TradingResult.Success();
                }
                
                LogWarning("ML_MODEL_NOT_FOUND", $"Model {modelName} not found for unloading");
                return TradingResult.Success();
            });
        }

        public async Task<TradingResult<ModelMetadata>> GetModelMetadataAsync(string modelName)
        {
            return await Task.Run(() =>
            {
                if (_modelMetadata.TryGetValue(modelName, out var metadata))
                {
                    return TradingResult<ModelMetadata>.Success(metadata);
                }
                
                return TradingResult<ModelMetadata>.Failure(
                    TradingError.NotFound($"Model {modelName} not loaded"));
            });
        }

        public async Task<TradingResult<WarmupStatistics>> WarmupModelAsync(string modelName, int? iterations = null)
        {
            return await TrackOperationAsync($"WarmupModel-{modelName}", async () =>
            {
                var session = await GetOrLoadModelAsync(modelName);
                var warmupIterations = iterations ?? _config.WarmupIterations;
                
                LogInfo("ML_MODEL_WARMUP_START", $"Starting warmup for model {modelName}",
                    additionalData: new { ModelName = modelName, Iterations = warmupIterations });
                
                var latencies = new List<double>();
                var metadata = _modelMetadata[modelName];
                
                // Create dummy input based on model metadata
                var inputMeta = metadata.InputMetadata.First();
                var dummyInput = CreateDummyInput(inputMeta);
                
                var totalStopwatch = Stopwatch.StartNew();
                
                for (int i = 0; i < warmupIterations; i++)
                {
                    var iterStopwatch = Stopwatch.StartNew();
                    
                    var inputs = new List<NamedOnnxValue> 
                    { 
                        NamedOnnxValue.CreateFromTensor(inputMeta.Name, dummyInput) 
                    };
                    
                    using var _ = session.Run(inputs);
                    
                    iterStopwatch.Stop();
                    latencies.Add(iterStopwatch.Elapsed.TotalMilliseconds);
                }
                
                totalStopwatch.Stop();
                
                var stats = new WarmupStatistics
                {
                    ModelName = modelName,
                    Iterations = warmupIterations,
                    MinInferenceTimeMs = latencies.Min(),
                    MaxInferenceTimeMs = latencies.Max(),
                    AverageInferenceTimeMs = latencies.Average(),
                    StdDevInferenceTimeMs = CalculateStandardDeviation(latencies),
                    TotalWarmupTimeMs = totalStopwatch.Elapsed.TotalMilliseconds
                };
                
                LogInfo("ML_MODEL_WARMUP_COMPLETE", $"Warmup completed for model {modelName}",
                    additionalData: new 
                    { 
                        ModelName = modelName,
                        MinMs = stats.MinInferenceTimeMs,
                        MaxMs = stats.MaxInferenceTimeMs,
                        AvgMs = stats.AverageInferenceTimeMs,
                        TotalMs = stats.TotalWarmupTimeMs
                    });
                
                return TradingResult<WarmupStatistics>.Success(stats);
            });
        }

        public async Task<TradingResult<Dictionary<string, ModelPerformanceMetrics>>> GetPerformanceMetricsAsync()
        {
            var metrics = await _performanceMonitor.GetHealthReportAsync();
            var metricsDict = metrics.ModelMetrics.ToDictionary(m => m.ModelName);
            return TradingResult<Dictionary<string, ModelPerformanceMetrics>>.Success(metricsDict);
        }

        public bool IsModelLoaded(string modelName)
        {
            return _modelSessions.ContainsKey(modelName);
        }

        public IReadOnlyList<string> GetLoadedModels()
        {
            return _modelSessions.Keys.ToList();
        }

        private async Task<InferenceSession> GetOrLoadModelAsync(string modelName)
        {
            if (_modelSessions.TryGetValue(modelName, out var session))
            {
                return session;
            }
            
            // Auto-load model if not loaded
            var result = await LoadModelAsync(modelName);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Failed to load model {modelName}: {result.Error?.Message}");
            }
            
            return _modelSessions[modelName];
        }

        private SessionOptions CreateSessionOptions()
        {
            var options = new SessionOptions();
            
            // Configure based on available hardware
            if (_gpuContext?.IsGpuAvailable == true)
            {
                ConfigureGpuExecution(options);
            }
            else
            {
                ConfigureCpuExecution(options);
            }
            
            // Common optimizations
            options.GraphOptimizationLevel = ConvertOptimizationLevel(_config.OptimizationLevel);
            options.EnableMemoryPattern = _config.EnableMemoryPattern;
            options.EnableProfiling = _config.EnableProfiling;
            
            return options;
        }

        private void ConfigureGpuExecution(SessionOptions options)
        {
            var availableProviders = OrtEnv.Instance().GetAvailableProviders();
            
            switch (_config.Provider)
            {
                case ExecutionProvider.CUDA:
                    if (availableProviders.Contains("CUDAExecutionProvider"))
                    {
                        options.AppendExecutionProvider_CUDA(_config.GpuDeviceId);
                        LogInfo("ML_PROVIDER_CUDA", "Configured CUDA execution provider");
                    }
                    break;
                    
                case ExecutionProvider.DirectML:
                    if (availableProviders.Contains("DmlExecutionProvider"))
                    {
                        options.AppendExecutionProvider_DML(_config.GpuDeviceId);
                        LogInfo("ML_PROVIDER_DML", "Configured DirectML execution provider");
                    }
                    break;
                    
                case ExecutionProvider.TensorRT:
                    if (_config.EnableTensorRT && availableProviders.Contains("TensorrtExecutionProvider"))
                    {
                        // TensorRT requires additional configuration
                        LogInfo("ML_PROVIDER_TENSORRT", "TensorRT provider requires additional setup");
                    }
                    break;
            }
            
            // Always add CPU as fallback
            options.AppendExecutionProvider_CPU(0);
        }

        private void ConfigureCpuExecution(SessionOptions options)
        {
            var physicalCores = Environment.ProcessorCount / 2; // Assuming hyperthreading
            var threadsToUse = Math.Min(physicalCores, 16); // Cap at 16 threads
            
            if (_config.IntraOpNumThreads > 0)
                options.IntraOpNumThreads = _config.IntraOpNumThreads;
            else
                options.IntraOpNumThreads = threadsToUse;
            
            if (_config.InterOpNumThreads > 0)
                options.InterOpNumThreads = _config.InterOpNumThreads;
            else
                options.InterOpNumThreads = Math.Max(2, threadsToUse / 4);
            
            LogInfo("ML_PROVIDER_CPU", $"Configured CPU execution with {options.IntraOpNumThreads} threads");
        }

        private GraphOptimizationLevel ConvertOptimizationLevel(GraphOptimizationLevel configLevel)
        {
            return configLevel switch
            {
                Configuration.GraphOptimizationLevel.None => GraphOptimizationLevel.ORT_DISABLE_ALL,
                Configuration.GraphOptimizationLevel.Basic => GraphOptimizationLevel.ORT_ENABLE_BASIC,
                Configuration.GraphOptimizationLevel.Extended => GraphOptimizationLevel.ORT_ENABLE_EXTENDED,
                Configuration.GraphOptimizationLevel.All => GraphOptimizationLevel.ORT_ENABLE_ALL,
                _ => GraphOptimizationLevel.ORT_ENABLE_ALL
            };
        }

        private List<TensorMetadata> ExtractTensorMetadata(IReadOnlyDictionary<string, NodeMetadata> nodeMetadata)
        {
            var metadata = new List<TensorMetadata>();
            
            foreach (var kvp in nodeMetadata)
            {
                var nodeMeta = kvp.Value;
                metadata.Add(new TensorMetadata
                {
                    Name = kvp.Key,
                    Shape = nodeMeta.Dimensions.Select(d => (int)d).ToArray(),
                    ElementType = nodeMeta.ElementType.ToString(),
                    IsOptional = false // ONNX Runtime doesn't provide this info directly
                });
            }
            
            return metadata;
        }

        private DenseTensor<float> CreateDummyInput(TensorMetadata inputMeta)
        {
            // Calculate total elements
            var totalElements = 1;
            var shape = new List<int>();
            
            foreach (var dim in inputMeta.Shape)
            {
                var actualDim = dim > 0 ? dim : 1; // Replace dynamic dimensions with 1
                totalElements *= actualDim;
                shape.Add(actualDim);
            }
            
            // Create dummy data
            var dummyData = new float[totalElements];
            var random = new Random(42);
            for (int i = 0; i < totalElements; i++)
            {
                dummyData[i] = (float)random.NextDouble();
            }
            
            return new DenseTensor<float>(dummyData, shape.ToArray());
        }

        private string GetModelPath(string modelName)
        {
            // Check model-specific configuration
            if (_config.ModelConfigs.TryGetValue(modelName, out var modelConfig) && 
                !string.IsNullOrEmpty(modelConfig.FileName))
            {
                return Path.Combine(_modelsBasePath, modelConfig.FileName);
            }
            
            // Default to modelName.onnx
            return Path.Combine(_modelsBasePath, $"{modelName}.onnx");
        }

        private void ValidateInput(string modelName, float[] inputData, int[] inputShape)
        {
            if (string.IsNullOrEmpty(modelName))
                throw new ArgumentException("Model name cannot be null or empty");
            
            if (inputData == null || inputData.Length == 0)
                throw new ArgumentException("Input data cannot be null or empty");
            
            if (inputShape == null || inputShape.Length == 0)
                throw new ArgumentException("Input shape cannot be null or empty");
            
            // Validate shape matches data
            var expectedElements = 1;
            foreach (var dim in inputShape)
            {
                if (dim <= 0)
                    throw new ArgumentException("Input shape dimensions must be positive");
                expectedElements *= dim;
            }
            
            if (expectedElements != inputData.Length)
            {
                throw new ArgumentException(
                    $"Input data length ({inputData.Length}) does not match shape ({expectedElements} elements)");
            }
        }

        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count <= 1) return 0;
            
            var avg = values.Average();
            var sum = values.Sum(d => Math.Pow(d - avg, 2));
            return Math.Sqrt(sum / (values.Count - 1));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var session in _modelSessions.Values)
                {
                    session?.Dispose();
                }
                _modelSessions.Clear();
                _modelMetadata.Clear();
                _sessionOptions?.Dispose();
            }
            
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Default implementation of ML performance monitor
    /// </summary>
    internal class DefaultMLPerformanceMonitor : IMLPerformanceMonitor
    {
        private readonly ConcurrentDictionary<string, ModelPerformanceMetrics> _metrics = new();

        public void RecordInference(string modelName, double latencyMs, bool success)
        {
            _metrics.AddOrUpdate(modelName,
                new ModelPerformanceMetrics
                {
                    ModelName = modelName,
                    TotalInferences = 1,
                    SuccessfulInferences = success ? 1 : 0,
                    TotalLatencyMs = latencyMs,
                    MinLatencyMs = latencyMs,
                    MaxLatencyMs = latencyMs,
                    LastUpdated = DateTime.UtcNow
                },
                (key, existing) =>
                {
                    existing.TotalInferences++;
                    if (success) existing.SuccessfulInferences++;
                    existing.TotalLatencyMs += latencyMs;
                    existing.MinLatencyMs = Math.Min(existing.MinLatencyMs, latencyMs);
                    existing.MaxLatencyMs = Math.Max(existing.MaxLatencyMs, latencyMs);
                    existing.LastUpdated = DateTime.UtcNow;
                    return existing;
                });
        }

        public async Task<MLHealthReport> GetHealthReportAsync()
        {
            return await Task.Run(() => new MLHealthReport
            {
                Timestamp = DateTime.UtcNow,
                ModelMetrics = _metrics.Values.ToList(),
                SystemHealth = new SystemHealthInfo()
            });
        }

        public async Task<bool> IsModelHealthyAsync(string modelName)
        {
            return await Task.Run(() =>
            {
                if (_metrics.TryGetValue(modelName, out var metrics))
                {
                    return metrics.SuccessRate > 0.95 && 
                           metrics.AverageLatencyMs < metrics.TargetLatencyMs * 1.5;
                }
                return true; // Assume healthy if no metrics
            });
        }
    }
}