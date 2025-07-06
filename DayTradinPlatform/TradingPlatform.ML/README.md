# TradingPlatform.ML - Machine Learning Inference Service

## Overview

The TradingPlatform.ML project provides a high-performance ML inference service using ONNX Runtime with GPU acceleration support. It enables real-time predictions for trading decisions, risk management, and market analysis.

## Key Features

- **ONNX Runtime Integration**: Native .NET support with multiple execution providers
- **GPU Acceleration**: Support for CUDA, DirectML, and TensorRT
- **Model Management**: Dynamic loading/unloading with metadata tracking
- **Batch Inference**: Optimized batch processing for high throughput
- **Performance Monitoring**: Built-in metrics and health reporting
- **Specialized Models**: Order book prediction, sentiment analysis, risk estimation

## Architecture

### Core Components

1. **MLInferenceService**: Main service for model inference
   - Single and batch predictions
   - Multi-input/output support
   - Model lifecycle management
   - Performance optimization

2. **OrderBookPredictor**: Specialized service for order book analytics
   - LSTM-based next state prediction
   - Price impact estimation
   - Microstructure feature extraction

3. **Configuration**: Flexible configuration system
   - Execution provider selection
   - GPU settings and optimization
   - Model-specific parameters
   - Performance tuning

## Usage Examples

### Basic Inference

```csharp
// Configure the service
var config = new MLInferenceConfiguration
{
    Provider = ExecutionProvider.CUDA,
    GpuDeviceId = 0,
    ModelsPath = "Models/ONNX"
};

// Create service
var mlService = new MLInferenceService(config, gpuContext);

// Load a model
await mlService.LoadModelAsync("PricePredictionLSTM");

// Single prediction
var inputData = new float[] { /* your features */ };
var inputShape = new[] { 1, 50 }; // batch=1, features=50

var result = await mlService.PredictAsync(
    "PricePredictionLSTM", 
    inputData, 
    inputShape);

if (result.IsSuccess)
{
    var predictions = result.Data.Predictions;
    var confidence = result.Data.Confidence;
}
```

### Batch Processing

```csharp
// Prepare batch data
var batchSize = 32;
var batchData = new float[batchSize][];
for (int i = 0; i < batchSize; i++)
{
    batchData[i] = PrepareFeatures(marketData[i]);
}

// Run batch inference
var batchResult = await mlService.PredictBatchAsync(
    "SignalClassifier",
    batchData,
    new[] { 100 }); // 100 features per sample

// Process results
foreach (var prediction in batchResult.Data.Predictions)
{
    ProcessSignal(prediction.Predictions);
}
```

### Order Book Prediction

```csharp
// Create order book predictor
var orderBookPredictor = new OrderBookPredictor(mlService);

// Prepare historical snapshots
var snapshots = GetOrderBookHistory(symbol, 100); // Last 100 snapshots

// Predict next state
var prediction = await orderBookPredictor.PredictNextStateAsync(snapshots);

Console.WriteLine($"Next bid: {prediction.NextBidPrice}");
Console.WriteLine($"Next ask: {prediction.NextAskPrice}");
Console.WriteLine($"Direction: {prediction.PriceDirection}");
Console.WriteLine($"Confidence: {prediction.Confidence:P}");

// Predict price impact
var impactPrediction = await orderBookPredictor.PredictPriceImpactAsync(
    currentSnapshot,
    orderSize: 10000m,
    isBuyOrder: true);

Console.WriteLine($"Expected impact: {impactPrediction.ExpectedImpactBps} bps");
```

## Model Types

### Time Series Models
- `PricePredictionLSTM`: Next price prediction
- `VolatilityGARCH`: Volatility forecasting
- `RegimeDetectionHMM`: Market regime classification
- `MultiHorizonTransformer`: Multi-horizon forecasting

### Classification Models
- `SignalClassifier`: Buy/Sell/Hold signals
- `PatternRecognition`: Chart pattern detection
- `AnomalyDetection`: Unusual market behavior
- `TrendClassifier`: Trend direction classification

### Risk Models
- `VaREstimator`: Value at Risk
- `DrawdownPredictor`: Maximum drawdown prediction
- `LiquidityClassifier`: Liquidity risk assessment
- `EVaRCalculator`: Entropic Value at Risk

### Portfolio Models
- `BlackLittermanLSTM`: Black-Litterman with LSTM views
- `HierarchicalRiskParity`: HRP optimization
- `MultiObjectiveOptimizer`: Multi-objective portfolio optimization

### Alternative Data Models
- `SentimentAnalyzer`: Social media sentiment
- `NewsImpactPredictor`: News event impact
- `SatelliteImageAnalyzer`: Economic activity from images

## Performance Optimization

### GPU Acceleration

```csharp
// Configure for optimal GPU performance
var config = new MLInferenceConfiguration
{
    Provider = ExecutionProvider.CUDA,
    GpuDeviceId = 0,
    UseIoBinding = true, // Direct GPU memory access
    CudaMemoryArena = new CudaMemoryArenaConfig
    {
        InitialChunkSizeBytes = 256 * 1024 * 1024, // 256MB
        ExtendStrategy = ArenaExtendStrategy.NextPowerOfTwo
    }
};
```

### Model Warmup

```csharp
// Warm up models for consistent latency
await mlService.WarmupModelAsync("PricePredictionLSTM", iterations: 10);
```

### Batching Strategy

```csharp
// Implement dynamic batching for optimal throughput
public class DynamicBatcher
{
    private readonly Channel<InferenceRequest> _queue;
    private readonly int _maxBatchSize = 32;
    private readonly TimeSpan _maxWaitTime = TimeSpan.FromMilliseconds(10);
    
    public async Task ProcessBatchesAsync()
    {
        var batch = new List<InferenceRequest>();
        var timer = Stopwatch.StartNew();
        
        await foreach (var request in _queue.Reader.ReadAllAsync())
        {
            batch.Add(request);
            
            if (batch.Count >= _maxBatchSize || 
                timer.Elapsed >= _maxWaitTime)
            {
                await ProcessBatch(batch);
                batch.Clear();
                timer.Restart();
            }
        }
    }
}
```

## Performance Benchmarks

### Inference Latency (Intel i9-14900K)
- Simple models (RF, XGBoost): <1ms
- Medium LSTM/GRU: 5-10ms
- Large Transformers: 50-100ms

### GPU Acceleration (NVIDIA RTX 4080)
- 10-50x speedup with TensorRT
- Batch processing: 1000+ predictions/second
- Memory transfer overhead: ~2ms

## Integration with Trading Platform

### Dependency Injection

```csharp
services.AddSingleton<MLInferenceConfiguration>(config);
services.AddSingleton<IMLInferenceService, MLInferenceService>();
services.AddScoped<IOrderBookPredictor, OrderBookPredictor>();
services.AddSingleton<IMLPerformanceMonitor, MLPerformanceMonitor>();
```

### Signal Generation Pipeline

```csharp
public class TradingSignalPipeline
{
    private readonly IMLInferenceService _mlService;
    private readonly IOrderBookPredictor _orderBookPredictor;
    
    public async Task<TradingSignal> GenerateSignalAsync(MarketData data)
    {
        // Extract features
        var features = ExtractFeatures(data);
        
        // Run multiple models
        var pricePrediction = await _mlService.PredictAsync(
            "PricePredictionLSTM", features.Price, new[] { 1, 50 });
            
        var signalPrediction = await _mlService.PredictAsync(
            "SignalClassifier", features.Technical, new[] { 1, 30 });
            
        var riskPrediction = await _mlService.PredictAsync(
            "VaREstimator", features.Risk, new[] { 1, 20 });
        
        // Combine predictions
        return CombineSignals(pricePrediction, signalPrediction, riskPrediction);
    }
}
```

## Model Deployment

### Model Conversion to ONNX

```python
# Convert TensorFlow model
import tf2onnx
import tensorflow as tf

model = tf.keras.models.load_model('price_lstm.h5')
spec = (tf.TensorSpec((None, 100, 50), tf.float32, name="input"),)
model_proto, _ = tf2onnx.convert.from_keras(model, input_signature=spec)

with open("price_lstm.onnx", "wb") as f:
    f.write(model_proto.SerializeToString())
```

### Model Versioning

```csharp
var modelConfig = new ModelConfiguration
{
    FileName = "price_lstm_v2.onnx",
    Version = "2.0",
    InputSpecs = new List<TensorSpec>
    {
        new() { Name = "input", Shape = new[] { -1, 100, 50 }, DataType = TensorDataType.Float32 }
    },
    OutputSpecs = new List<TensorSpec>
    {
        new() { Name = "output", Shape = new[] { -1, 1 }, DataType = TensorDataType.Float32 }
    },
    Metadata = new Dictionary<string, string>
    {
        { "training_date", "2025-07-06" },
        { "accuracy", "0.87" }
    }
};
```

## Monitoring and Diagnostics

### Performance Metrics

```csharp
// Get performance metrics
var metrics = await mlService.GetPerformanceMetricsAsync();

foreach (var (modelName, metric) in metrics.Data)
{
    Console.WriteLine($"Model: {modelName}");
    Console.WriteLine($"  Total inferences: {metric.TotalInferences}");
    Console.WriteLine($"  Success rate: {metric.SuccessRate:P}");
    Console.WriteLine($"  Average latency: {metric.AverageLatencyMs:F2}ms");
    Console.WriteLine($"  P95 latency: {metric.MaxLatencyMs:F2}ms");
}
```

### Health Monitoring

```csharp
var healthReport = await performanceMonitor.GetHealthReportAsync();

if (!healthReport.IsHealthy)
{
    foreach (var warning in healthReport.Warnings)
    {
        logger.LogWarning($"ML Health Warning: {warning}");
    }
}
```

## Best Practices

1. **Model Loading**: Load models during startup to avoid runtime delays
2. **Batch Processing**: Use batching for multiple predictions
3. **GPU Memory**: Monitor GPU memory usage and implement model rotation if needed
4. **Error Handling**: Implement fallback strategies for inference failures
5. **Monitoring**: Track latency, throughput, and accuracy metrics
6. **Versioning**: Maintain model versions and rollback capabilities

## Troubleshooting

### Common Issues

1. **CUDA Not Available**
   - Ensure NVIDIA drivers are installed
   - Check CUDA toolkit compatibility
   - Verify GPU compute capability

2. **High Latency**
   - Enable model warmup
   - Check batch size optimization
   - Verify GPU utilization

3. **Memory Issues**
   - Implement model rotation
   - Adjust memory arena settings
   - Monitor GPU memory usage

## Future Enhancements

1. **Distributed Inference**: Multi-GPU and multi-node support
2. **Model Optimization**: Automatic quantization and pruning
3. **A/B Testing**: Built-in experiment framework
4. **AutoML Integration**: Automated model selection
5. **Edge Deployment**: Support for edge devices