using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MarketAnalyzer.Domain.Entities;
using MarketAnalyzer.Foundation;
using MarketAnalyzer.Infrastructure.AI.Configuration;
using MarketAnalyzer.Infrastructure.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MarketAnalyzer.Infrastructure.AI.Services;

/// <summary>
/// ONNX Runtime-based ML inference service following industry best practices.
/// Uses Microsoft.ML.OnnxRuntime for high-performance inference across CPU/GPU.
/// </summary>
public class MLInferenceService : CanonicalServiceBase, IMLInferenceService
{
    private readonly AIOptions _options;
    private readonly ConcurrentDictionary<string, InferenceSession> _modelSessions;
    private readonly ConcurrentDictionary<string, ModelStatistics> _modelStats;
    private readonly SemaphoreSlim _loadSemaphore;
    private SessionOptions? _sessionOptions;

    public MLInferenceService(
        IOptions<AIOptions> options,
        ILogger<MLInferenceService> logger)
        : base(logger)
    {
        LogMethodEntry();
        try
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _modelSessions = new ConcurrentDictionary<string, InferenceSession>();
            _modelStats = new ConcurrentDictionary<string, ModelStatistics>();
            _loadSemaphore = new SemaphoreSlim(1, 1);

            LogInfo($"MLInferenceService initialized with execution provider: {_options.PreferredExecutionProvider}");
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize MLInferenceService", ex);
            throw;
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<bool>> OnInitializeAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        try
        {
            // Initialize ONNX Runtime session options
            _sessionOptions = CreateSessionOptions();

            // Log available execution providers
            var availableProviders = OrtEnv.Instance().GetAvailableProviders();
            LogInfo($"Available ONNX Runtime execution providers: {string.Join(", ", availableProviders)}");

            // Preload models if configured
            if (_options.PreloadModelsOnStartup && _options.ModelsToPreload.Any())
            {
                foreach (var modelName in _options.ModelsToPreload)
                {
                    var result = await PreloadModelAsync(modelName, cancellationToken).ConfigureAwait(false);
                    if (!result.IsSuccess)
                    {
                        LogWarning($"Failed to preload model {modelName}: {result.Error?.Message}");
                    }
                }
            }

            UpdateMetric("InitializationSuccessCount", GetMetricValue("InitializationSuccessCount") + 1);
            LogInfo("MLInferenceService initialized successfully");
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Exception during MLInferenceService initialization", ex);
            return TradingResult<bool>.Failure("INIT_EXCEPTION", $"Initialization failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<bool>> OnStartAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        try
        {
            UpdateMetric("StartSuccessCount", GetMetricValue("StartSuccessCount") + 1);
            LogInfo("MLInferenceService started successfully");
            await Task.CompletedTask.ConfigureAwait(false);
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Exception during MLInferenceService start", ex);
            return TradingResult<bool>.Failure("START_EXCEPTION", $"Start failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<bool>> OnStopAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        try
        {
            // Dispose all loaded models
            foreach (var kvp in _modelSessions)
            {
                kvp.Value?.Dispose();
            }
            _modelSessions.Clear();
            await Task.CompletedTask.ConfigureAwait(false);

            UpdateMetric("StopSuccessCount", GetMetricValue("StopSuccessCount") + 1);
            LogInfo("MLInferenceService stopped successfully");
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Exception during MLInferenceService stop", ex);
            return TradingResult<bool>.Failure("STOP_EXCEPTION", $"Stop failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<ModelPrediction>> PredictAsync(
        string modelName,
        float[] inputData,
        int[] inputShape,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return TradingResult<ModelPrediction>.Failure("INVALID_MODEL_NAME", "Model name cannot be null or empty");
            }

            if (inputData == null || inputData.Length == 0)
            {
                return TradingResult<ModelPrediction>.Failure("INVALID_INPUT_DATA", "Input data cannot be null or empty");
            }

            var session = await GetOrLoadModelAsync(modelName, cancellationToken).ConfigureAwait(false);
            if (session == null)
            {
                return TradingResult<ModelPrediction>.Failure("MODEL_LOAD_FAILED", $"Failed to load model: {modelName}");
            }

            // Create input tensor
            var inputTensor = new DenseTensor<float>(inputData, inputShape);
            var inputName = session.InputMetadata.Keys.First();
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };

            // Run inference with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(_options.InferenceTimeoutMs));

            var stopwatch = Stopwatch.StartNew();
            var inferenceTask = Task.Run(() => session.Run(inputs), cts.Token);
            var results = await inferenceTask.ConfigureAwait(false);
            stopwatch.Stop();

            // Extract output
            var output = results.First().AsTensor<float>().ToArray();
            results.Dispose();

            // Update statistics
            UpdateModelStatistics(modelName, stopwatch.ElapsedMilliseconds);

            var prediction = new ModelPrediction
            {
                ModelName = modelName,
                Predictions = output,
                InferenceTimeMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow,
                Confidence = CalculateConfidence(output)
            };

            UpdateMetric("TotalInferences", GetMetricValue("TotalInferences") + 1);
            LogInfo($"Inference completed for model {modelName} in {stopwatch.ElapsedMilliseconds}ms");
            return TradingResult<ModelPrediction>.Success(prediction);
        }
        catch (OperationCanceledException)
        {
            LogError($"Inference timeout for model {modelName}");
            return TradingResult<ModelPrediction>.Failure("INFERENCE_TIMEOUT", $"Inference exceeded timeout of {_options.InferenceTimeoutMs}ms");
        }
        catch (Exception ex)
        {
            LogError($"Inference error for model {modelName}", ex);
            return TradingResult<ModelPrediction>.Failure("INFERENCE_ERROR", $"Inference failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<BatchPrediction>> PredictBatchAsync(
        string modelName,
        BatchInput batchInputs,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            if (batchInputs == null || batchInputs.BatchSize == 0)
            {
                return TradingResult<BatchPrediction>.Failure("INVALID_BATCH", "Batch input cannot be null or empty");
            }

            var session = await GetOrLoadModelAsync(modelName, cancellationToken).ConfigureAwait(false);
            if (session == null)
            {
                return TradingResult<BatchPrediction>.Failure("MODEL_LOAD_FAILED", $"Failed to load model: {modelName}");
            }

            var batchStopwatch = Stopwatch.StartNew();
            var predictions = new float[batchInputs.BatchSize][];
            var inferenceTimesMs = new double[batchInputs.BatchSize];

            // Process batch
            for (int i = 0; i < batchInputs.BatchSize; i++)
            {
                var itemStopwatch = Stopwatch.StartNew();
                
                var inputTensor = new DenseTensor<float>(batchInputs.Data[i], batchInputs.InputShape);
                var inputName = session.InputMetadata.Keys.First();
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
                };

                using var results = session.Run(inputs);
                predictions[i] = results.First().AsTensor<float>().ToArray();
                
                itemStopwatch.Stop();
                inferenceTimesMs[i] = itemStopwatch.ElapsedMilliseconds;
            }

            batchStopwatch.Stop();

            var batchPrediction = new BatchPrediction
            {
                ModelName = modelName,
                Predictions = predictions,
                InferenceTimesMs = inferenceTimesMs,
                TotalBatchTimeMs = batchStopwatch.ElapsedMilliseconds,
                InferenceTimeMs = inferenceTimesMs.Average(),
                Timestamp = DateTime.UtcNow,
                Confidence = predictions.Select(p => CalculateConfidence(p)).Average()
            };

            UpdateMetric("TotalBatchInferences", GetMetricValue("TotalBatchInferences") + 1);
            LogInfo($"Batch inference completed for model {modelName}: {batchInputs.BatchSize} items in {batchStopwatch.ElapsedMilliseconds}ms");
            return TradingResult<BatchPrediction>.Success(batchPrediction);
        }
        catch (Exception ex)
        {
            LogError($"Batch inference error for model {modelName}", ex);
            return TradingResult<BatchPrediction>.Failure("BATCH_INFERENCE_ERROR", $"Batch inference failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<PricePrediction>> PredictPriceMovementAsync(
        string symbol,
        decimal[] historicalPrices,
        int horizon,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            // Convert decimal to float for ONNX inference
            var inputData = historicalPrices.Select(p => (float)p).ToArray();
            var inputShape = new[] { 1, historicalPrices.Length };

            var result = await PredictAsync("price_movement_lstm", inputData, inputShape, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return TradingResult<PricePrediction>.Failure(result.Error!.Code, result.Error.Message);
            }

            // Convert predictions back to decimal
            var predictions = result.Value!.Predictions;
            var predictedPrices = predictions.Take(horizon).Select(p => (decimal)p).ToArray();

            var pricePrediction = new PricePrediction
            {
                ModelName = "price_movement_lstm",
                Symbol = symbol,
                PredictedPrices = predictedPrices,
                TimeHorizons = Enumerable.Range(1, horizon).ToArray(),
                PredictedDirection = predictedPrices.Last() > historicalPrices.Last() ? 1 : -1,
                PredictedVolatility = CalculateVolatility(predictedPrices),
                InferenceTimeMs = result.Value.InferenceTimeMs,
                Timestamp = DateTime.UtcNow,
                Confidence = result.Value.Confidence
            };

            LogInfo($"Price prediction completed for {symbol}: {horizon} periods");
            return TradingResult<PricePrediction>.Success(pricePrediction);
        }
        catch (Exception ex)
        {
            LogError($"Price prediction error for {symbol}", ex);
            return TradingResult<PricePrediction>.Failure("PRICE_PREDICTION_ERROR", $"Price prediction failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<SentimentAnalysis>> AnalyzeSentimentAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            // This would typically involve text tokenization and embedding
            // For now, returning a placeholder implementation
            await Task.Delay(10, cancellationToken).ConfigureAwait(false); // Simulate processing

            var sentiment = new SentimentAnalysis
            {
                ModelName = "sentiment_bert",
                SentimentScore = 0.0f, // Placeholder
                Label = SentimentLabel.Neutral,
                InferenceTimeMs = 15,
                Timestamp = DateTime.UtcNow,
                Confidence = 0.8f
            };
            
            // Populate probabilities
            sentiment.ClassProbabilities["VeryNegative"] = 0.1f;
            sentiment.ClassProbabilities["Negative"] = 0.2f;
            sentiment.ClassProbabilities["Neutral"] = 0.4f;
            sentiment.ClassProbabilities["Positive"] = 0.2f;
            sentiment.ClassProbabilities["VeryPositive"] = 0.1f;

            LogInfo($"Sentiment analysis completed: {sentiment.Label}");
            return TradingResult<SentimentAnalysis>.Success(sentiment);
        }
        catch (Exception ex)
        {
            LogError("Sentiment analysis error", ex);
            return TradingResult<SentimentAnalysis>.Failure("SENTIMENT_ERROR", $"Sentiment analysis failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<PatternDetection>> DetectPatternsAsync(
        float[,] priceData,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            // Flatten 2D array for ONNX input
            var rows = priceData.GetLength(0);
            var cols = priceData.GetLength(1);
            var flatData = new float[rows * cols];
            Buffer.BlockCopy(priceData, 0, flatData, 0, flatData.Length * sizeof(float));

            var result = await PredictAsync("pattern_detection_cnn", flatData, new[] { 1, rows, cols }, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return TradingResult<PatternDetection>.Failure(result.Error!.Code, result.Error.Message);
            }

            var patternDetection = new PatternDetection
            {
                ModelName = "pattern_detection_cnn",
                PatternStrength = result.Value!.Predictions.Max(),
                InferenceTimeMs = result.Value.InferenceTimeMs,
                Timestamp = DateTime.UtcNow,
                Confidence = result.Value.Confidence
            };
            
            // Populate patterns
            var patterns = ExtractPatterns(result.Value.Predictions);
            foreach (var pattern in patterns)
            {
                patternDetection.Patterns.Add(pattern);
            }

            LogInfo($"Pattern detection completed: {patternDetection.Patterns.Count} patterns found");
            return TradingResult<PatternDetection>.Success(patternDetection);
        }
        catch (Exception ex)
        {
            LogError("Pattern detection error", ex);
            return TradingResult<PatternDetection>.Failure("PATTERN_ERROR", $"Pattern detection failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<TradingSignalPrediction>> GenerateTradingSignalsAsync(
        MarketQuote marketData,
        Dictionary<string, decimal> technicalIndicators,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            // Prepare input features
            var features = new List<float>
            {
                (float)marketData.CurrentPrice,
                (float)marketData.DayOpen,
                (float)marketData.DayHigh,
                (float)marketData.DayLow,
                (float)marketData.Volume
            };

            // Add technical indicators
            foreach (var indicator in technicalIndicators.Values)
            {
                features.Add((float)indicator);
            }

            var result = await PredictAsync("trading_signals_ensemble", features.ToArray(), new[] { 1, features.Count }, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return TradingResult<TradingSignalPrediction>.Failure(result.Error!.Code, result.Error.Message);
            }

            var signalPrediction = new TradingSignalPrediction
            {
                ModelName = "trading_signals_ensemble",
                Signal = DetermineSignal(result.Value!.Predictions[0]),
                SignalStrength = Math.Abs(result.Value.Predictions[0]),
                RecommendedPositionSize = CalculatePositionSize(result.Value.Predictions[0]),
                TimeHorizonMinutes = 15,
                InferenceTimeMs = result.Value.InferenceTimeMs,
                Timestamp = DateTime.UtcNow,
                Confidence = result.Value.Confidence
            };

            LogInfo($"Trading signal generated for {marketData.Symbol}: {signalPrediction.Signal}");
            return TradingResult<TradingSignalPrediction>.Success(signalPrediction);
        }
        catch (Exception ex)
        {
            LogError($"Trading signal generation error for {marketData.Symbol}", ex);
            return TradingResult<TradingSignalPrediction>.Failure("SIGNAL_ERROR", $"Signal generation failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<RiskAssessment>> AssessRiskAsync(
        TradingPosition position,
        MarketConditions marketConditions,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            // Prepare risk features
            var features = new float[]
            {
                (float)position.CurrentPrice,
                (float)position.EntryPrice,
                position.Quantity,
                position.IsLong ? 1f : -1f,
                (float)(DateTime.UtcNow - position.OpenTime).TotalMinutes,
                marketConditions.MarketVolatility,
                marketConditions.MarketTrend
            };

            var result = await PredictAsync("risk_assessment_xgboost", features, new[] { 1, features.Length }, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return TradingResult<RiskAssessment>.Failure(result.Error!.Code, result.Error.Message);
            }

            var riskScore = result.Value!.Predictions[0] * 100; // Convert to 0-100 scale
            var riskAssessment = new RiskAssessment
            {
                ModelName = "risk_assessment_xgboost",
                RiskScore = riskScore,
                Level = DetermineRiskLevel(riskScore),
                ValueAtRisk = CalculateVaR(position, marketConditions),
                ExpectedShortfall = CalculateExpectedShortfall(position, marketConditions),
                InferenceTimeMs = result.Value.InferenceTimeMs,
                Timestamp = DateTime.UtcNow,
                Confidence = result.Value.Confidence
            };

            LogInfo($"Risk assessment completed for {position.Symbol}: {riskAssessment.Level}");
            return TradingResult<RiskAssessment>.Success(riskAssessment);
        }
        catch (Exception ex)
        {
            LogError($"Risk assessment error for {position.Symbol}", ex);
            return TradingResult<RiskAssessment>.Failure("RISK_ERROR", $"Risk assessment failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<bool>> PreloadModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            await _loadSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_modelSessions.ContainsKey(modelName))
                {
                    LogInfo($"Model {modelName} already loaded");
                    return TradingResult<bool>.Success(true);
                }

                var modelPath = Path.Combine(_options.ModelStoragePath, $"{modelName}.onnx");
                if (!File.Exists(modelPath))
                {
                    return TradingResult<bool>.Failure("MODEL_NOT_FOUND", $"Model file not found: {modelPath}");
                }

                var stopwatch = Stopwatch.StartNew();
                InferenceSession? session = null;
                try
                {
#pragma warning disable CA2000 // Dispose objects before losing scope - session is stored in _modelSessions and disposed in Dispose method
                    session = new InferenceSession(modelPath, _sessionOptions);
#pragma warning restore CA2000
                    stopwatch.Stop();

                    // Warm up the model if configured
                    if (_options.ModelWarmUpIterations > 0)
                    {
                        await WarmUpModelAsync(modelName, session, cancellationToken).ConfigureAwait(false);
                    }

                    _modelSessions[modelName] = session;
                    _modelStats[modelName] = new ModelStatistics { LoadTimeMs = stopwatch.ElapsedMilliseconds };
                }
                catch
                {
                    session?.Dispose();
                    throw;
                }

                LogInfo($"Model {modelName} loaded successfully in {stopwatch.ElapsedMilliseconds}ms");
                UpdateMetric("LoadedModels", _modelSessions.Count);
                return TradingResult<bool>.Success(true);
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to preload model {modelName}", ex);
            return TradingResult<bool>.Failure("PRELOAD_ERROR", $"Model preload failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public TradingResult<bool> UnloadModel(string modelName)
    {
        LogMethodEntry();
        try
        {
            if (_modelSessions.TryRemove(modelName, out var session))
            {
                session?.Dispose();
                _modelStats.TryRemove(modelName, out _);
                UpdateMetric("LoadedModels", _modelSessions.Count);
                LogInfo($"Model {modelName} unloaded successfully");
                return TradingResult<bool>.Success(true);
            }

            return TradingResult<bool>.Failure("MODEL_NOT_LOADED", $"Model {modelName} is not loaded");
        }
        catch (Exception ex)
        {
            LogError($"Failed to unload model {modelName}", ex);
            return TradingResult<bool>.Failure("UNLOAD_ERROR", $"Model unload failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public TradingResult<ModelInfo> GetModelInfo(string modelName)
    {
        LogMethodEntry();
        try
        {
            if (!_modelSessions.TryGetValue(modelName, out var session))
            {
                return TradingResult<ModelInfo>.Failure("MODEL_NOT_LOADED", $"Model {modelName} is not loaded");
            }

            var stats = _modelStats.GetValueOrDefault(modelName) ?? new ModelStatistics();
            var modelInfo = new ModelInfo
            {
                ModelName = modelName,
                ModelType = "ONNX",
                IsLoaded = true,
                LoadTime = stats.LoadTime,
                TotalInferenceCount = stats.InferenceCount,
                AverageInferenceTimeMs = stats.AverageInferenceTimeMs,
                P95InferenceTimeMs = stats.P95InferenceTimeMs,
                P99InferenceTimeMs = stats.P99InferenceTimeMs
            };
            
            // Populate metadata
            foreach (var kvp in session.InputMetadata)
            {
                modelInfo.InputMetadata[kvp.Key] = $"{kvp.Value.OnnxValueType} {string.Join("x", kvp.Value.Dimensions)}";
            }
            
            foreach (var kvp in session.OutputMetadata)
            {
                modelInfo.OutputMetadata[kvp.Key] = $"{kvp.Value.OnnxValueType} {string.Join("x", kvp.Value.Dimensions)}";
            }

            LogInfo($"Retrieved info for model {modelName}");
            return TradingResult<ModelInfo>.Success(modelInfo);
        }
        catch (Exception ex)
        {
            LogError($"Failed to get model info for {modelName}", ex);
            return TradingResult<ModelInfo>.Failure("INFO_ERROR", $"Failed to get model info: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<MLHealthStatus>> GetMLHealthAsync()
    {
        LogMethodEntry();
        try
        {
            var memoryInfo = GC.GetTotalMemory(false) / (1024 * 1024); // Convert to MB
            
            var health = new MLHealthStatus
            {
                IsHealthy = Health == ServiceHealth.Running,
                LoadedModelCount = _modelSessions.Count,
                TotalModelCount = Directory.GetFiles(_options.ModelStoragePath, "*.onnx").Length,
                MemoryUsageMB = memoryInfo,
                ExecutionProvider = _options.PreferredExecutionProvider.ToString()
            };
            
            // Populate model statuses
            foreach (var kvp in _modelSessions)
            {
                health.ModelStatuses[kvp.Key] = true;
            }

            LogInfo($"ML health check: {health.LoadedModelCount}/{health.TotalModelCount} models loaded");
            await Task.CompletedTask.ConfigureAwait(false);
            return TradingResult<MLHealthStatus>.Success(health);
        }
        catch (Exception ex)
        {
            LogError("ML health check failed", ex);
            return TradingResult<MLHealthStatus>.Failure("HEALTH_CHECK_ERROR", $"Health check failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    private SessionOptions CreateSessionOptions()
    {
        LogMethodEntry();
        try
        {
#pragma warning disable CA2000 // Dispose objects before losing scope - options is returned and managed by caller
            var options = new SessionOptions();
#pragma warning restore CA2000

            // Configure based on execution provider
            switch (_options.PreferredExecutionProvider)
            {
                case ExecutionProvider.CUDA:
                    ConfigureCudaExecution(options);
                    break;
                case ExecutionProvider.DirectML:
                    ConfigureDirectMLExecution(options);
                    break;
                case ExecutionProvider.TensorRT:
                    ConfigureTensorRTExecution(options);
                    break;
                default:
                    ConfigureCpuExecution(options);
                    break;
            }

            // Common optimizations
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            options.EnableMemoryPattern = _options.EnableMemoryPattern;
            options.EnableProfiling = _options.EnableProfiling;

            return options;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private void ConfigureCpuExecution(SessionOptions options)
    {
        LogMethodEntry();
        try
        {
            options.IntraOpNumThreads = _options.IntraOpNumThreads;
            options.InterOpNumThreads = _options.InterOpNumThreads;
            
            if (_options.EnableCpuSpinning)
            {
                options.AddSessionConfigEntry("session.intra_op.allow_spinning", "1");
            }

            LogInfo($"Configured CPU execution: {_options.IntraOpNumThreads} intra-op threads");
        }
        finally
        {
            LogMethodExit();
        }
    }

    private void ConfigureCudaExecution(SessionOptions options)
    {
        LogMethodEntry();
        try
        {
            var availableProviders = OrtEnv.Instance().GetAvailableProviders();
            if (availableProviders.Contains("CUDAExecutionProvider"))
            {
                options.AppendExecutionProvider_CUDA(_options.GpuDeviceId);
                LogInfo($"Configured CUDA execution on device {_options.GpuDeviceId}");
            }
            else
            {
                LogWarning("CUDA execution provider not available, falling back to CPU");
                ConfigureCpuExecution(options);
            }
        }
        finally
        {
            LogMethodExit();
        }
    }

    private void ConfigureDirectMLExecution(SessionOptions options)
    {
        LogMethodEntry();
        try
        {
            options.AppendExecutionProvider_DML(_options.GpuDeviceId);
            LogInfo($"Configured DirectML execution on device {_options.GpuDeviceId}");
        }
        finally
        {
            LogMethodExit();
        }
    }

    private void ConfigureTensorRTExecution(SessionOptions options)
    {
        LogMethodEntry();
        try
        {
            // TensorRT requires additional configuration
            var availableProviders = OrtEnv.Instance().GetAvailableProviders();
            if (availableProviders.Contains("TensorrtExecutionProvider"))
            {
                options.AppendExecutionProvider_Tensorrt(_options.GpuDeviceId);
                LogInfo($"Configured TensorRT execution on device {_options.GpuDeviceId}");
            }
            else
            {
                LogWarning("TensorRT execution provider not available, falling back to CUDA");
                ConfigureCudaExecution(options);
            }
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async Task<InferenceSession?> GetOrLoadModelAsync(string modelName, CancellationToken cancellationToken)
    {
        LogMethodEntry();
        try
        {
            if (_modelSessions.TryGetValue(modelName, out var session))
            {
                return session;
            }

            var result = await PreloadModelAsync(modelName, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? _modelSessions.GetValueOrDefault(modelName) : null;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async Task WarmUpModelAsync(string modelName, InferenceSession session, CancellationToken cancellationToken)
    {
        LogMethodEntry();
        try
        {
            LogInfo($"Warming up model {modelName} with {_options.ModelWarmUpIterations} iterations");

            // Get input shape from metadata
            var inputMeta = session.InputMetadata.First();
            var inputShape = inputMeta.Value.Dimensions.Select(d => (int)Math.Max(d, 1)).ToArray();
            var inputSize = inputShape.Aggregate(1, (a, b) => a * b);
            var dummyInput = new float[inputSize];

            for (int i = 0; i < _options.ModelWarmUpIterations; i++)
            {
                var inputTensor = new DenseTensor<float>(dummyInput, inputShape);
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(inputMeta.Key, inputTensor)
                };

                using var results = await Task.Run(() => session.Run(inputs), cancellationToken).ConfigureAwait(false);
            }

            LogInfo($"Model {modelName} warm-up completed");
        }
        catch (Exception ex)
        {
            LogWarning($"Model warm-up failed for {modelName}: {ex.Message}");
        }
        finally
        {
            LogMethodExit();
        }
    }

    private void UpdateModelStatistics(string modelName, double inferenceTimeMs)
    {
        LogMethodEntry();
        try
        {
            var stats = _modelStats.GetOrAdd(modelName, _ => new ModelStatistics());
            stats.UpdateInferenceTime(inferenceTimeMs);
        }
        finally
        {
            LogMethodExit();
        }
    }

    private float CalculateConfidence(float[] predictions)
    {
        LogMethodEntry();
        try
        {
            if (predictions.Length == 0) return 0f;

            // For classification: use softmax probability
            // For regression: use normalized score
            var max = predictions.Max();
            var min = predictions.Min();
            var range = max - min;

            return range > 0 ? (predictions[0] - min) / range : 0.5f;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private decimal CalculateVolatility(decimal[] prices)
    {
        LogMethodEntry();
        try
        {
            if (prices.Length < 2) return 0m;

            var returns = new decimal[prices.Length - 1];
            for (int i = 1; i < prices.Length; i++)
            {
                returns[i - 1] = (prices[i] - prices[i - 1]) / prices[i - 1];
            }

            var mean = returns.Average();
            var variance = returns.Select(r => (r - mean) * (r - mean)).Average();
            return (decimal)Math.Sqrt((double)variance);
        }
        finally
        {
            LogMethodExit();
        }
    }

    private List<DetectedPattern> ExtractPatterns(float[] predictions)
    {
        LogMethodEntry();
        try
        {
            var patterns = new List<DetectedPattern>();
            var threshold = 0.5f;

            for (int i = 0; i < predictions.Length; i++)
            {
                if (predictions[i] > threshold)
                {
                    patterns.Add(new DetectedPattern
                    {
                        PatternType = DeterminePatternType(i),
                        Confidence = predictions[i],
                        StartIndex = i,
                        EndIndex = i + 10 // Placeholder
                    });
                }
            }

            return patterns;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private string DeterminePatternType(int index)
    {
        LogMethodEntry();
        try
        {
            // Placeholder pattern mapping
            var patterns = new[] { "HeadAndShoulders", "DoubleTop", "TriangleBreakout", "FlagPattern", "WedgePattern" };
            return patterns[index % patterns.Length];
        }
        finally
        {
            LogMethodExit();
        }
    }

    private TradingSignal DetermineSignal(float prediction)
    {
        LogMethodEntry();
        try
        {
            return prediction switch
            {
                < -0.5f => TradingSignal.StrongSell,
                < -0.1f => TradingSignal.Sell,
                < 0.1f => TradingSignal.Hold,
                < 0.5f => TradingSignal.Buy,
                _ => TradingSignal.StrongBuy
            };
        }
        finally
        {
            LogMethodExit();
        }
    }

    private float CalculatePositionSize(float signalStrength)
    {
        LogMethodEntry();
        try
        {
            // Kelly Criterion simplified
            return Math.Min(Math.Abs(signalStrength), 0.25f);
        }
        finally
        {
            LogMethodExit();
        }
    }

    private Models.RiskLevel DetermineRiskLevel(float riskScore)
    {
        LogMethodEntry();
        try
        {
            return riskScore switch
            {
                < 20 => Models.RiskLevel.VeryLow,
                < 40 => Models.RiskLevel.Low,
                < 60 => Models.RiskLevel.Medium,
                < 80 => Models.RiskLevel.High,
                < 90 => Models.RiskLevel.VeryHigh,
                _ => Models.RiskLevel.Critical
            };
        }
        finally
        {
            LogMethodExit();
        }
    }

    private decimal CalculateVaR(TradingPosition position, MarketConditions marketConditions)
    {
        LogMethodEntry();
        try
        {
            // Simplified VaR calculation
            var positionValue = position.CurrentPrice * position.Quantity;
            var volatility = (decimal)marketConditions.MarketVolatility;
            // var confidenceLevel = 0.95m; // Currently unused, keeping for future implementation
            var zScore = 1.645m; // 95% confidence

            return positionValue * volatility * zScore;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private decimal CalculateExpectedShortfall(TradingPosition position, MarketConditions marketConditions)
    {
        LogMethodEntry();
        try
        {
            // ES is typically 20-30% worse than VaR
            var var = CalculateVaR(position, marketConditions);
            return var * 1.25m;
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
            _sessionOptions?.Dispose();
            _loadSemaphore?.Dispose();
            
            foreach (var session in _modelSessions.Values)
            {
                session?.Dispose();
            }
            _modelSessions.Clear();
        }
        
        base.Dispose(disposing);
    }

    /// <summary>
    /// Internal class for tracking model statistics.
    /// </summary>
    private class ModelStatistics
    {
        private readonly List<double> _inferenceTimes = new();
        private readonly object _lock = new();

        public double LoadTimeMs { get; set; }
        public DateTime? LoadTime { get; set; } = DateTime.UtcNow;
        public long InferenceCount => _inferenceTimes.Count;
        public double AverageInferenceTimeMs => _inferenceTimes.Any() ? _inferenceTimes.Average() : 0;
        public double P95InferenceTimeMs => GetPercentile(0.95);
        public double P99InferenceTimeMs => GetPercentile(0.99);

        public void UpdateInferenceTime(double timeMs)
        {
            lock (_lock)
            {
                _inferenceTimes.Add(timeMs);
                if (_inferenceTimes.Count > 1000)
                {
                    _inferenceTimes.RemoveAt(0);
                }
            }
        }

        private double GetPercentile(double percentile)
        {
            lock (_lock)
            {
                if (!_inferenceTimes.Any()) return 0;
                var sorted = _inferenceTimes.OrderBy(t => t).ToList();
                var index = (int)(sorted.Count * percentile);
                return sorted[Math.Min(index, sorted.Count - 1)];
            }
        }
    }
}