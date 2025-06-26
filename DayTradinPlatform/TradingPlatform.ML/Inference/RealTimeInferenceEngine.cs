// File: TradingPlatform.ML/Inference/RealTimeInferenceEngine.cs

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Performance;
using TradingPlatform.Foundation.Models;
using TradingPlatform.ML.Features;
using TradingPlatform.ML.Interfaces;
using TradingPlatform.ML.Models;

namespace TradingPlatform.ML.Inference
{
    /// <summary>
    /// High-performance real-time inference engine with <50ms latency target
    /// </summary>
    public class RealTimeInferenceEngine : CanonicalServiceBase
    {
        private readonly ModelServingInfrastructure _modelServing;
        private readonly FeatureEngineering _featureEngineering;
        private readonly HighPerformancePool<PricePredictionInput> _inputPool;
        private readonly HighPerformancePool<InferenceRequest> _requestPool;
        private readonly ConcurrentDictionary<string, FeatureCache> _featureCaches;
        private readonly Channel<InferenceRequest> _inferenceQueue;
        private readonly LatencyTracker _latencyTracker;
        private readonly Task _processingTask;
        private readonly CancellationTokenSource _shutdownToken;
        
        // Performance settings
        private const int MaxConcurrentInferences = 100;
        private const int FeatureCacheSize = 10000;
        private const int BatchSize = 50;
        private const int MaxQueueSize = 1000;
        
        public RealTimeInferenceEngine(
            IServiceProvider serviceProvider,
            ITradingLogger logger,
            ModelServingInfrastructure modelServing,
            FeatureEngineering featureEngineering)
            : base(serviceProvider, logger, "RealTimeInferenceEngine")
        {
            _modelServing = modelServing;
            _featureEngineering = featureEngineering;
            
            // Initialize object pools
            _inputPool = new HighPerformancePool<PricePredictionInput>(
                factory: () => new PricePredictionInput(),
                resetAction: input => ResetPredictionInput(input),
                initialSize: 100,
                maxSize: 1000);
            
            _requestPool = new HighPerformancePool<InferenceRequest>(
                factory: () => new InferenceRequest(),
                resetAction: req => req.Reset(),
                initialSize: 100,
                maxSize: 1000);
            
            _featureCaches = new ConcurrentDictionary<string, FeatureCache>();
            _latencyTracker = new LatencyTracker(bufferSize: 10000);
            
            // Create bounded channel for inference requests
            _inferenceQueue = Channel.CreateBounded<InferenceRequest>(new BoundedChannelOptions(MaxQueueSize)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
            
            _shutdownToken = new CancellationTokenSource();
            _processingTask = Task.Run(ProcessInferenceRequests);
            
            LogInfo("Real-time inference engine initialized",
                additionalData: new
                {
                    MaxConcurrentInferences,
                    BatchSize,
                    TargetLatency = "<50ms"
                });
        }
        
        /// <summary>
        /// Perform real-time inference with <50ms target latency
        /// </summary>
        public async Task<TradingResult<PricePrediction>> InferAsync(
            string modelId,
            MarketDataSnapshot marketData,
            List<MarketDataSnapshot>? historicalData = null,
            CancellationToken cancellationToken = default)
        {
            var latencyScope = _latencyTracker.StartMeasurement();
            
            try
            {
                // Get request from pool
                var request = _requestPool.Rent();
                request.Id = Guid.NewGuid().ToString();
                request.ModelId = modelId;
                request.MarketData = marketData;
                request.HistoricalData = historicalData;
                request.Timestamp = DateTime.UtcNow;
                
                // Create response channel
                var responseChannel = Channel.CreateUnbounded<InferenceResponse>(
                    new UnboundedChannelOptions
                    {
                        SingleReader = true,
                        SingleWriter = true
                    });
                
                request.ResponseChannel = responseChannel.Writer;
                
                // Submit request
                if (!await _inferenceQueue.Writer.TryWriteAsync(request, cancellationToken))
                {
                    _requestPool.Return(request);
                    return TradingResult<PricePrediction>.Failure(
                        new Exception("Inference queue is full"));
                }
                
                // Wait for response
                var response = await responseChannel.Reader.ReadAsync(cancellationToken);
                
                // Return request to pool
                _requestPool.Return(request);
                
                // Record latency
                var latency = latencyScope.Complete();
                RecordServiceMetric($"Inference.{modelId}.Latency", latency);
                
                if (latency > 50)
                {
                    LogWarning($"Inference latency exceeded target: {latency}ms",
                        additionalData: new { ModelId = modelId, Latency = latency });
                }
                
                if (response.Success)
                {
                    return TradingResult<PricePrediction>.Success(response.Prediction!);
                }
                else
                {
                    return TradingResult<PricePrediction>.Failure(
                        new Exception(response.Error ?? "Inference failed"));
                }
            }
            catch (Exception ex)
            {
                LogError("Real-time inference failed", ex,
                    additionalData: new { ModelId = modelId });
                return TradingResult<PricePrediction>.Failure(ex);
            }
        }
        
        /// <summary>
        /// Perform batch inference for efficiency
        /// </summary>
        public async Task<TradingResult<List<PricePrediction>>> InferBatchAsync(
            string modelId,
            List<MarketDataSnapshot> marketDataList,
            CancellationToken cancellationToken = default)
        {
            var latencyScope = _latencyTracker.StartMeasurement();
            
            try
            {
                // Extract features in parallel
                var inputs = new List<PricePredictionInput>();
                var tasks = marketDataList.Select(async data =>
                {
                    var input = await ExtractFeaturesAsync(data.Symbol, data, null);
                    return input;
                });
                
                var results = await Task.WhenAll(tasks);
                inputs.AddRange(results.Where(r => r != null)!);
                
                // Perform batch prediction
                var predictions = await _modelServing.PredictBatchAsync<PricePredictionInput, PricePrediction>(
                    modelId, inputs, cancellationToken);
                
                // Record latency
                var latency = latencyScope.Complete();
                RecordServiceMetric($"BatchInference.{modelId}.Latency", latency);
                RecordServiceMetric($"BatchInference.{modelId}.Size", marketDataList.Count);
                
                // Return inputs to pool
                foreach (var input in inputs)
                {
                    _inputPool.Return(input);
                }
                
                return predictions;
            }
            catch (Exception ex)
            {
                LogError("Batch inference failed", ex,
                    additionalData: new { ModelId = modelId, BatchSize = marketDataList.Count });
                return TradingResult<List<PricePrediction>>.Failure(ex);
            }
        }
        
        /// <summary>
        /// Warm up model for optimal performance
        /// </summary>
        public async Task<TradingResult<bool>> WarmUpModelAsync(
            string modelId,
            int iterations = 100,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    LogInfo($"Warming up model {modelId} with {iterations} iterations");
                    
                    var dummyData = new MarketDataSnapshot
                    {
                        Symbol = "WARMUP",
                        Timestamp = DateTime.UtcNow,
                        Open = 100m,
                        High = 101m,
                        Low = 99m,
                        Close = 100m,
                        Volume = 1000000
                    };
                    
                    var warmupTasks = new List<Task>();
                    var batchSize = Math.Min(10, iterations);
                    
                    for (int i = 0; i < iterations; i += batchSize)
                    {
                        var batch = Enumerable.Range(0, Math.Min(batchSize, iterations - i))
                            .Select(_ => dummyData)
                            .ToList();
                        
                        warmupTasks.Add(InferBatchAsync(modelId, batch, cancellationToken));
                        
                        if (warmupTasks.Count >= 10)
                        {
                            await Task.WhenAll(warmupTasks);
                            warmupTasks.Clear();
                        }
                    }
                    
                    if (warmupTasks.Any())
                    {
                        await Task.WhenAll(warmupTasks);
                    }
                    
                    LogInfo($"Model {modelId} warm-up completed");
                    return TradingResult<bool>.Success(true);
                },
                nameof(WarmUpModelAsync));
        }
        
        /// <summary>
        /// Get inference performance metrics
        /// </summary>
        public async Task<TradingResult<InferencePerformanceMetrics>> GetPerformanceMetricsAsync()
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var latencyStats = _latencyTracker.GetStatistics();
                    
                    var metrics = new InferencePerformanceMetrics
                    {
                        AverageLatencyMs = latencyStats.Average,
                        P50LatencyMs = latencyStats.P50,
                        P95LatencyMs = latencyStats.P95,
                        P99LatencyMs = latencyStats.P99,
                        MaxLatencyMs = latencyStats.Max,
                        TotalInferences = latencyStats.Count,
                        InferencesPerSecond = CalculateInferencesPerSecond(),
                        QueueDepth = _inferenceQueue.Reader.Count,
                        CacheHitRate = CalculateCacheHitRate(),
                        PoolUtilization = new PoolUtilization
                        {
                            InputPoolUsage = _inputPool.GetUtilization(),
                            RequestPoolUsage = _requestPool.GetUtilization()
                        }
                    };
                    
                    return TradingResult<InferencePerformanceMetrics>.Success(metrics);
                },
                nameof(GetPerformanceMetricsAsync));
        }
        
        // Processing loop
        
        private async Task ProcessInferenceRequests()
        {
            var semaphore = new SemaphoreSlim(MaxConcurrentInferences);
            var batch = new List<InferenceRequest>(BatchSize);
            var batchTimer = new Timer(_ => ProcessBatch(), null, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10));
            
            try
            {
                await foreach (var request in _inferenceQueue.Reader.ReadAllAsync(_shutdownToken.Token))
                {
                    batch.Add(request);
                    
                    if (batch.Count >= BatchSize)
                    {
                        ProcessBatch();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            finally
            {
                batchTimer?.Dispose();
            }
            
            void ProcessBatch()
            {
                if (batch.Count == 0) return;
                
                var currentBatch = batch.ToList();
                batch.Clear();
                
                _ = Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await ProcessInferenceBatch(currentBatch);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
            }
        }
        
        private async Task ProcessInferenceBatch(List<InferenceRequest> requests)
        {
            try
            {
                // Group by model ID for efficient batch processing
                var groupedRequests = requests.GroupBy(r => r.ModelId);
                
                foreach (var group in groupedRequests)
                {
                    var modelId = group.Key;
                    var modelRequests = group.ToList();
                    
                    // Extract features for all requests
                    var featureTasks = modelRequests.Select(async req =>
                    {
                        var features = await ExtractFeaturesAsync(
                            req.MarketData.Symbol,
                            req.MarketData,
                            req.HistoricalData);
                        return (req, features);
                    });
                    
                    var featuresResults = await Task.WhenAll(featureTasks);
                    
                    // Filter successful feature extractions
                    var validInputs = featuresResults
                        .Where(r => r.features != null)
                        .ToList();
                    
                    if (validInputs.Any())
                    {
                        // Batch prediction
                        var inputs = validInputs.Select(v => v.features!).ToList();
                        var predictions = await _modelServing.PredictBatchAsync<PricePredictionInput, PricePrediction>(
                            modelId, inputs, CancellationToken.None);
                        
                        // Send responses
                        if (predictions.IsSuccess && predictions.Value != null)
                        {
                            for (int i = 0; i < validInputs.Count && i < predictions.Value.Count; i++)
                            {
                                var (req, _) = validInputs[i];
                                var response = new InferenceResponse
                                {
                                    RequestId = req.Id,
                                    Success = true,
                                    Prediction = predictions.Value[i],
                                    ProcessingTime = (DateTime.UtcNow - req.Timestamp).TotalMilliseconds
                                };
                                
                                await req.ResponseChannel.WriteAsync(response);
                            }
                        }
                        else
                        {
                            // Send error responses
                            foreach (var (req, _) in validInputs)
                            {
                                var response = new InferenceResponse
                                {
                                    RequestId = req.Id,
                                    Success = false,
                                    Error = predictions.Error?.Message ?? "Prediction failed",
                                    ProcessingTime = (DateTime.UtcNow - req.Timestamp).TotalMilliseconds
                                };
                                
                                await req.ResponseChannel.WriteAsync(response);
                            }
                        }
                        
                        // Return features to pool
                        foreach (var input in inputs)
                        {
                            _inputPool.Return(input);
                        }
                    }
                    
                    // Handle failed feature extractions
                    var failedRequests = modelRequests.Except(validInputs.Select(v => v.req));
                    foreach (var req in failedRequests)
                    {
                        var response = new InferenceResponse
                        {
                            RequestId = req.Id,
                            Success = false,
                            Error = "Feature extraction failed",
                            ProcessingTime = (DateTime.UtcNow - req.Timestamp).TotalMilliseconds
                        };
                        
                        await req.ResponseChannel.WriteAsync(response);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Error processing inference batch", ex);
                
                // Send error responses to all requests
                foreach (var req in requests)
                {
                    try
                    {
                        var response = new InferenceResponse
                        {
                            RequestId = req.Id,
                            Success = false,
                            Error = ex.Message,
                            ProcessingTime = (DateTime.UtcNow - req.Timestamp).TotalMilliseconds
                        };
                        
                        await req.ResponseChannel.WriteAsync(response);
                    }
                    catch
                    {
                        // Best effort
                    }
                }
            }
        }
        
        private async Task<PricePredictionInput?> ExtractFeaturesAsync(
            string symbol,
            MarketDataSnapshot currentData,
            List<MarketDataSnapshot>? historicalData)
        {
            try
            {
                // Check cache first
                var cacheKey = $"{symbol}:{currentData.Timestamp:yyyyMMddHHmmss}";
                if (_featureCaches.TryGetValue(symbol, out var cache))
                {
                    if (cache.TryGet(cacheKey, out var cachedFeatures))
                    {
                        RecordServiceMetric("FeatureCache.Hit", 1);
                        return cachedFeatures;
                    }
                }
                
                RecordServiceMetric("FeatureCache.Miss", 1);
                
                // Get input from pool
                var input = _inputPool.Rent();
                
                // Extract basic features
                input.Open = (float)currentData.Open;
                input.High = (float)currentData.High;
                input.Low = (float)currentData.Low;
                input.Close = (float)currentData.Close;
                input.Volume = (float)currentData.Volume;
                input.DayOfWeek = (float)currentData.Timestamp.DayOfWeek;
                input.HourOfDay = currentData.Timestamp.Hour;
                
                // Extract advanced features if historical data available
                if (historicalData != null && historicalData.Count >= 50)
                {
                    var allData = new List<MarketDataSnapshot>(historicalData) { currentData };
                    var features = await Task.Run(() => 
                        _featureEngineering.ExtractPricePredictionFeatures(allData));
                    
                    // Copy features
                    input.RSI = features.RSI;
                    input.MACD = features.MACD;
                    input.BollingerUpper = features.BollingerUpper;
                    input.BollingerLower = features.BollingerLower;
                    input.MovingAverage20 = features.MovingAverage20;
                    input.MovingAverage50 = features.MovingAverage50;
                    input.VolumeRatio = features.VolumeRatio;
                    input.PriceChangePercent = features.PriceChangePercent;
                    input.Volatility = features.Volatility;
                    input.BidAskSpread = features.BidAskSpread;
                    input.VolumeWeightedPrice = features.VolumeWeightedPrice;
                    input.PricePosition = features.PricePosition;
                    input.TrendStrength = features.TrendStrength;
                    input.MomentumScore = features.MomentumScore;
                    input.PatternComplexity = features.PatternComplexity;
                    input.Fractality = features.Fractality;
                    input.MarketRegime = features.MarketRegime;
                }
                else
                {
                    // Use default values for missing features
                    input.RSI = 50f;
                    input.MACD = 0f;
                    input.VolumeRatio = 1f;
                    input.PriceChangePercent = 0f;
                }
                
                // Update cache
                if (!_featureCaches.ContainsKey(symbol))
                {
                    _featureCaches[symbol] = new FeatureCache(FeatureCacheSize);
                }
                _featureCaches[symbol].Add(cacheKey, input);
                
                return input;
            }
            catch (Exception ex)
            {
                LogError($"Feature extraction failed for {symbol}", ex);
                return null;
            }
        }
        
        private void ResetPredictionInput(PricePredictionInput input)
        {
            input.Open = 0;
            input.High = 0;
            input.Low = 0;
            input.Close = 0;
            input.Volume = 0;
            input.RSI = 50;
            input.MACD = 0;
            input.BollingerUpper = 0;
            input.BollingerLower = 0;
            input.MovingAverage20 = 0;
            input.MovingAverage50 = 0;
            input.VolumeRatio = 1;
            input.PriceChangePercent = 0;
            input.Volatility = 0;
            input.BidAskSpread = 0;
            input.VolumeWeightedPrice = 0;
            input.PricePosition = 0;
            input.TrendStrength = 0;
            input.MomentumScore = 0;
            input.PatternComplexity = 0;
            input.Fractality = 0;
            input.MarketRegime = 0;
            input.MarketCap = 0;
            input.Beta = 0;
            input.PriceToEarnings = 0;
            input.DayOfWeek = 0;
            input.HourOfDay = 0;
        }
        
        private double CalculateInferencesPerSecond()
        {
            var stats = _latencyTracker.GetStatistics();
            if (stats.Count == 0) return 0;
            
            var timeSpan = DateTime.UtcNow - stats.FirstMeasurement;
            return stats.Count / Math.Max(1, timeSpan.TotalSeconds);
        }
        
        private double CalculateCacheHitRate()
        {
            var totalHits = 0L;
            var totalMisses = 0L;
            
            foreach (var cache in _featureCaches.Values)
            {
                totalHits += cache.HitCount;
                totalMisses += cache.MissCount;
            }
            
            var total = totalHits + totalMisses;
            return total > 0 ? (double)totalHits / total : 0;
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _shutdownToken?.Cancel();
                _processingTask?.Wait(TimeSpan.FromSeconds(5));
                _shutdownToken?.Dispose();
                _inputPool?.Dispose();
                _requestPool?.Dispose();
            }
            
            base.Dispose(disposing);
        }
    }
    
    // Supporting classes
    
    internal class InferenceRequest
    {
        public string Id { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public MarketDataSnapshot MarketData { get; set; } = null!;
        public List<MarketDataSnapshot>? HistoricalData { get; set; }
        public DateTime Timestamp { get; set; }
        public ChannelWriter<InferenceResponse> ResponseChannel { get; set; } = null!;
        
        public void Reset()
        {
            Id = string.Empty;
            ModelId = string.Empty;
            MarketData = null!;
            HistoricalData = null;
            ResponseChannel = null!;
        }
    }
    
    internal class InferenceResponse
    {
        public string RequestId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public PricePrediction? Prediction { get; set; }
        public string? Error { get; set; }
        public double ProcessingTime { get; set; }
    }
    
    internal class FeatureCache
    {
        private readonly int _maxSize;
        private readonly Dictionary<string, PricePredictionInput> _cache;
        private readonly Queue<string> _lruQueue;
        private readonly object _lock = new object();
        
        public long HitCount { get; private set; }
        public long MissCount { get; private set; }
        
        public FeatureCache(int maxSize)
        {
            _maxSize = maxSize;
            _cache = new Dictionary<string, PricePredictionInput>(maxSize);
            _lruQueue = new Queue<string>(maxSize);
        }
        
        public bool TryGet(string key, out PricePredictionInput? value)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out value))
                {
                    HitCount++;
                    return true;
                }
                
                MissCount++;
                value = null;
                return false;
            }
        }
        
        public void Add(string key, PricePredictionInput value)
        {
            lock (_lock)
            {
                if (_cache.ContainsKey(key))
                    return;
                
                if (_cache.Count >= _maxSize)
                {
                    // Remove oldest
                    if (_lruQueue.TryDequeue(out var oldestKey))
                    {
                        _cache.Remove(oldestKey);
                    }
                }
                
                _cache[key] = value;
                _lruQueue.Enqueue(key);
            }
        }
    }
    
    public class InferencePerformanceMetrics
    {
        public double AverageLatencyMs { get; set; }
        public double P50LatencyMs { get; set; }
        public double P95LatencyMs { get; set; }
        public double P99LatencyMs { get; set; }
        public double MaxLatencyMs { get; set; }
        public long TotalInferences { get; set; }
        public double InferencesPerSecond { get; set; }
        public int QueueDepth { get; set; }
        public double CacheHitRate { get; set; }
        public PoolUtilization PoolUtilization { get; set; } = new();
    }
    
    public class PoolUtilization
    {
        public double InputPoolUsage { get; set; }
        public double RequestPoolUsage { get; set; }
    }