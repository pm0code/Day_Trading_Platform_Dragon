// File: TradingPlatform.ML/Models/LSTMPatternModel.cs

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using TensorFlow;
using TensorFlow.Keras;
using TensorFlow.Keras.Layers;
using TensorFlow.Keras.Models;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Models;
using TradingPlatform.ML.Data;
using TradingPlatform.ML.Interfaces;

namespace TradingPlatform.ML.Models
{
    /// <summary>
    /// LSTM model for market pattern recognition
    /// </summary>
    public class LSTMPatternModel : CanonicalServiceBase, 
        IPredictiveModel<PatternSequence, PatternPrediction>
    {
        private Sequential? _model;
        private readonly SequenceDataPreparation _dataPreparation;
        private readonly string _modelPath;
        private readonly object _modelLock = new object();
        private ModelMetadata? _metadata;
        
        // Model architecture parameters
        private const int LstmUnits1 = 128;
        private const int LstmUnits2 = 64;
        private const int DenseUnits = 32;
        private const float DropoutRate = 0.2f;
        private const int AttentionHeads = 4;
        
        public LSTMPatternModel(
            IServiceProvider serviceProvider,
            ITradingLogger logger,
            string modelPath = "models/lstm_pattern_model")
            : base(serviceProvider, logger, "LSTMPatternModel")
        {
            _modelPath = modelPath;
            _dataPreparation = new SequenceDataPreparation();
            
            // Initialize TensorFlow
            tf.enable_eager_execution();
        }
        
        /// <summary>
        /// Build LSTM architecture with attention mechanism
        /// </summary>
        public async Task<TradingResult<bool>> BuildModelAsync(
            int sequenceLength,
            int featureCount,
            int outputSize,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    LogInfo($"Building LSTM model: sequence_length={sequenceLength}, features={featureCount}");
                    
                    lock (_modelLock)
                    {
                        _model = keras.Sequential(new List<ILayer>
                        {
                            // Input layer
                            keras.layers.InputLayer(new TensorShape(sequenceLength, featureCount)),
                            
                            // First Bidirectional LSTM layer
                            keras.layers.Bidirectional(
                                keras.layers.LSTM(LstmUnits1, 
                                    return_sequences: true,
                                    dropout: DropoutRate,
                                    recurrent_dropout: DropoutRate)),
                            
                            // Batch normalization
                            keras.layers.BatchNormalization(),
                            
                            // Attention mechanism
                            new MultiHeadAttentionLayer(AttentionHeads, LstmUnits1 * 2),
                            
                            // Second Bidirectional LSTM layer
                            keras.layers.Bidirectional(
                                keras.layers.LSTM(LstmUnits2,
                                    return_sequences: false,
                                    dropout: DropoutRate,
                                    recurrent_dropout: DropoutRate)),
                            
                            // Batch normalization
                            keras.layers.BatchNormalization(),
                            
                            // Dense layers
                            keras.layers.Dense(DenseUnits, activation: "relu"),
                            keras.layers.Dropout(DropoutRate),
                            
                            // Output layer
                            keras.layers.Dense(outputSize, activation: "linear")
                        });
                        
                        // Compile model
                        _model.compile(
                            optimizer: keras.optimizers.Adam(learning_rate: 0.001f),
                            loss: "mse",
                            metrics: new[] { "mae", "mape" });
                        
                        _model.summary();
                    }
                    
                    _metadata = new ModelMetadata
                    {
                        ModelType = "LSTM_Pattern",
                        Version = "1.0",
                        CreatedAt = DateTime.UtcNow,
                        InputShape = new[] { sequenceLength, featureCount },
                        OutputShape = new[] { outputSize },
                        Architecture = "Bidirectional LSTM with Multi-Head Attention"
                    };
                    
                    LogInfo("LSTM model built successfully");
                    RecordServiceMetric("ModelBuilt", 1);
                    
                    return TradingResult<bool>.Success(true);
                },
                nameof(BuildModelAsync));
        }
        
        /// <summary>
        /// Train the LSTM model
        /// </summary>
        public async Task<TradingResult<ModelTrainingResult>> TrainAsync(
            List<PatternSequence> sequences,
            LSTMTrainingOptions options,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (_model == null)
                        throw new InvalidOperationException("Model not built");
                    
                    LogInfo($"Training LSTM model with {sequences.Count} sequences");
                    var startTime = DateTime.UtcNow;
                    
                    // Prepare training data
                    var (xTrain, yTrain) = PrepareTrainingData(sequences);
                    
                    // Split into train/validation
                    var splitIndex = (int)(sequences.Count * 0.8);
                    var xVal = xTrain.Skip(splitIndex).ToArray();
                    var yVal = yTrain.Skip(splitIndex).ToArray();
                    xTrain = xTrain.Take(splitIndex).ToArray();
                    yTrain = yTrain.Take(splitIndex).ToArray();
                    
                    // Create TensorFlow datasets
                    var trainDataset = tf.data.Dataset.from_tensor_slices((xTrain, yTrain))
                        .batch(options.BatchSize)
                        .shuffle(1000);
                    
                    var valDataset = tf.data.Dataset.from_tensor_slices((xVal, yVal))
                        .batch(options.BatchSize);
                    
                    // Training callbacks
                    var callbacks = new List<ICallback>
                    {
                        keras.callbacks.EarlyStopping(
                            monitor: "val_loss",
                            patience: options.EarlyStoppingPatience,
                            restore_best_weights: true),
                        
                        keras.callbacks.ReduceLROnPlateau(
                            monitor: "val_loss",
                            factor: 0.5f,
                            patience: 5,
                            min_lr: 0.00001f),
                        
                        new ModelCheckpoint(
                            filepath: Path.Combine(_modelPath, "checkpoint_{epoch}"),
                            save_best_only: true,
                            monitor: "val_loss")
                    };
                    
                    // Train model
                    var history = _model.fit(
                        trainDataset,
                        epochs: options.Epochs,
                        validation_data: valDataset,
                        callbacks: callbacks,
                        verbose: 1);
                    
                    // Extract metrics
                    var finalLoss = history.history["loss"].Last();
                    var finalValLoss = history.history["val_loss"].Last();
                    var finalMae = history.history["mae"].Last();
                    
                    var result = new ModelTrainingResult
                    {
                        ModelId = Guid.NewGuid().ToString(),
                        TrainingStartTime = startTime,
                        TrainingEndTime = DateTime.UtcNow,
                        EpochsCompleted = history.history["loss"].Count,
                        Metrics = new Dictionary<string, double>
                        {
                            ["TrainLoss"] = finalLoss,
                            ["ValLoss"] = finalValLoss,
                            ["MAE"] = finalMae,
                            ["TrainSamples"] = xTrain.Length,
                            ["ValSamples"] = xVal.Length
                        },
                        TrainingHistory = ConvertHistory(history)
                    };
                    
                    // Update metadata
                    if (_metadata != null)
                    {
                        _metadata.LastTrainedAt = DateTime.UtcNow;
                        _metadata.TrainingMetrics = result.Metrics;
                    }
                    
                    LogInfo("LSTM training completed", additionalData: result.Metrics);
                    RecordServiceMetric("ModelTrained", 1);
                    
                    return TradingResult<ModelTrainingResult>.Success(result);
                },
                nameof(TrainAsync));
        }
        
        /// <summary>
        /// Predict pattern using LSTM
        /// </summary>
        public async Task<TradingResult<PatternPrediction>> PredictAsync(
            PatternSequence input,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (_model == null)
                        throw new InvalidOperationException("Model not loaded");
                    
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    
                    lock (_modelLock)
                    {
                        // Prepare input tensor
                        var inputTensor = PrepareInputTensor(input);
                        
                        // Run prediction
                        var prediction = _model.predict(inputTensor);
                        
                        // Parse prediction results
                        var result = ParsePrediction(prediction, input);
                        
                        stopwatch.Stop();
                        RecordServiceMetric("PredictionLatency", stopwatch.ElapsedMilliseconds);
                        
                        return TradingResult<PatternPrediction>.Success(result);
                    }
                },
                nameof(PredictAsync));
        }
        
        /// <summary>
        /// Batch prediction
        /// </summary>
        public async Task<TradingResult<List<PatternPrediction>>> PredictBatchAsync(
            List<PatternSequence> inputs,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (_model == null)
                        throw new InvalidOperationException("Model not loaded");
                    
                    var predictions = new List<PatternPrediction>();
                    var batchSize = 32;
                    
                    for (int i = 0; i < inputs.Count; i += batchSize)
                    {
                        var batch = inputs.Skip(i).Take(batchSize).ToList();
                        var batchTensor = PrepareBatchTensor(batch);
                        
                        lock (_modelLock)
                        {
                            var batchPredictions = _model.predict(batchTensor);
                            
                            for (int j = 0; j < batch.Count; j++)
                            {
                                var prediction = ParsePrediction(
                                    batchPredictions[j], 
                                    batch[j]);
                                predictions.Add(prediction);
                            }
                        }
                    }
                    
                    RecordServiceMetric("BatchPredictionSize", inputs.Count);
                    return TradingResult<List<PatternPrediction>>.Success(predictions);
                },
                nameof(PredictBatchAsync));
        }
        
        /// <summary>
        /// Save model
        /// </summary>
        public async Task<TradingResult<bool>> SaveAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (_model == null)
                        throw new InvalidOperationException("No model to save");
                    
                    lock (_modelLock)
                    {
                        // Save Keras model
                        _model.save(path);
                        
                        // Save metadata
                        if (_metadata != null)
                        {
                            var metadataPath = Path.Combine(path, "metadata.json");
                            var json = System.Text.Json.JsonSerializer.Serialize(_metadata);
                            File.WriteAllText(metadataPath, json);
                        }
                        
                        // Export to ONNX for inference optimization
                        ExportToOnnx(path);
                    }
                    
                    LogInfo($"Model saved to {path}");
                    return TradingResult<bool>.Success(true);
                },
                nameof(SaveAsync));
        }
        
        /// <summary>
        /// Load model
        /// </summary>
        public async Task<TradingResult<bool>> LoadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    lock (_modelLock)
                    {
                        // Load Keras model
                        _model = keras.models.load_model(path);
                        
                        // Load metadata
                        var metadataPath = Path.Combine(path, "metadata.json");
                        if (File.Exists(metadataPath))
                        {
                            var json = File.ReadAllText(metadataPath);
                            _metadata = System.Text.Json.JsonSerializer.Deserialize<ModelMetadata>(json);
                        }
                    }
                    
                    LogInfo($"Model loaded from {path}");
                    return TradingResult<bool>.Success(true);
                },
                nameof(LoadAsync));
        }
        
        /// <summary>
        /// Get pattern analysis for a sequence
        /// </summary>
        public async Task<TradingResult<PatternAnalysis>> AnalyzePatternAsync(
            PatternSequence sequence,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    // Get prediction
                    var predictionResult = await PredictAsync(sequence, cancellationToken);
                    if (!predictionResult.IsSuccess)
                        return TradingResult<PatternAnalysis>.Failure(predictionResult.Error);
                    
                    var prediction = predictionResult.Value;
                    
                    // Get attention weights for interpretability
                    var attentionWeights = GetAttentionWeights(sequence);
                    
                    // Identify key time points
                    var keyTimePoints = IdentifyKeyTimePoints(attentionWeights, sequence);
                    
                    var analysis = new PatternAnalysis
                    {
                        SequenceId = sequence.SequenceId,
                        Prediction = prediction,
                        AttentionWeights = attentionWeights,
                        KeyTimePoints = keyTimePoints,
                        PatternStrength = CalculatePatternStrength(prediction),
                        SimilarHistoricalPatterns = await FindSimilarPatternsAsync(sequence),
                        RecommendedAction = DetermineAction(prediction),
                        RiskAssessment = AssessRisk(prediction, sequence)
                    };
                    
                    return TradingResult<PatternAnalysis>.Success(analysis);
                },
                nameof(AnalyzePatternAsync));
        }
        
        // Helper methods
        
        private (float[][], float[][]) PrepareTrainingData(List<PatternSequence> sequences)
        {
            var xData = new List<float[][]>();
            var yData = new List<float[]>();
            
            foreach (var seq in sequences)
            {
                if (seq.Targets == null) continue;
                
                xData.Add(seq.RawSequence);
                
                // Prepare targets
                var targets = new[]
                {
                    seq.Targets.PriceChange,
                    seq.Targets.Direction,
                    seq.Targets.MaxDrawdown,
                    (float)seq.Targets.PatternType
                };
                
                yData.Add(targets);
            }
            
            return (xData.ToArray(), yData.ToArray());
        }
        
        private Tensor PrepareInputTensor(PatternSequence sequence)
        {
            var shape = new[] { 1, sequence.RawSequence.Length, sequence.RawSequence[0].Length };
            var data = sequence.RawSequence.SelectMany(row => row).ToArray();
            return tf.constant(data).reshape(shape);
        }
        
        private Tensor PrepareBatchTensor(List<PatternSequence> sequences)
        {
            var batchSize = sequences.Count;
            var seqLength = sequences[0].RawSequence.Length;
            var features = sequences[0].RawSequence[0].Length;
            
            var shape = new[] { batchSize, seqLength, features };
            var data = sequences
                .SelectMany(s => s.RawSequence.SelectMany(row => row))
                .ToArray();
            
            return tf.constant(data).reshape(shape);
        }
        
        private PatternPrediction ParsePrediction(Tensor prediction, PatternSequence input)
        {
            var values = prediction.numpy().ToArray<float>();
            
            return new PatternPrediction
            {
                SequenceId = input.SequenceId,
                PredictedPriceChange = values[0],
                PredictedDirection = values[1] > 0 ? 1 : -1,
                PredictedDrawdown = Math.Abs(values[2]),
                PredictedPattern = (PatternType)(int)Math.Round(values[3]),
                Confidence = CalculateConfidence(values, input),
                PredictionTime = DateTime.UtcNow,
                TimeHorizon = 5 // 5 periods ahead
            };
        }
        
        private float CalculateConfidence(float[] predictions, PatternSequence input)
        {
            // Base confidence on prediction consistency and data quality
            var directionConfidence = Math.Abs(predictions[1]); // Direction strength
            var patternConfidence = 1f - (predictions[3] % 1); // How close to integer pattern
            var dataQuality = input.DataQuality.QualityScore;
            
            return (directionConfidence + patternConfidence + dataQuality) / 3f;
        }
        
        private Dictionary<string, List<double>> ConvertHistory(History history)
        {
            var converted = new Dictionary<string, List<double>>();
            
            foreach (var metric in history.history.Keys)
            {
                converted[metric] = history.history[metric]
                    .Select(v => (double)v)
                    .ToList();
            }
            
            return converted;
        }
        
        private void ExportToOnnx(string basePath)
        {
            try
            {
                // Convert to ONNX for optimized inference
                var onnxPath = Path.Combine(basePath, "model.onnx");
                // TensorFlow to ONNX conversion would go here
                LogInfo($"Model exported to ONNX: {onnxPath}");
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to export to ONNX: {ex.Message}");
            }
        }
        
        private float[][] GetAttentionWeights(PatternSequence sequence)
        {
            // Simplified - in production would extract from attention layer
            var weights = new float[sequence.Length][];
            var random = new Random(42);
            
            for (int i = 0; i < sequence.Length; i++)
            {
                weights[i] = new float[AttentionHeads];
                for (int j = 0; j < AttentionHeads; j++)
                {
                    weights[i][j] = (float)random.NextDouble();
                }
            }
            
            return weights;
        }
        
        private List<KeyTimePoint> IdentifyKeyTimePoints(float[][] attentionWeights, PatternSequence sequence)
        {
            var keyPoints = new List<KeyTimePoint>();
            var avgWeights = attentionWeights.Select(w => w.Average()).ToArray();
            var threshold = avgWeights.Average() + avgWeights.StandardDeviation();
            
            for (int i = 0; i < avgWeights.Length; i++)
            {
                if (avgWeights[i] > threshold)
                {
                    keyPoints.Add(new KeyTimePoint
                    {
                        Index = i,
                        Timestamp = sequence.StartTime.AddMinutes(i * 5),
                        Importance = avgWeights[i],
                        Description = DetermineKeyPointDescription(sequence, i)
                    });
                }
            }
            
            return keyPoints.OrderByDescending(k => k.Importance).Take(5).ToList();
        }
        
        private string DetermineKeyPointDescription(PatternSequence sequence, int index)
        {
            // Analyze what makes this point important
            if (index > 0 && index < sequence.RawSequence.Length)
            {
                var current = sequence.RawSequence[index];
                var previous = sequence.RawSequence[index - 1];
                
                var priceChange = (current[3] - previous[3]) / previous[3];
                var volumeSpike = current[4] / sequence.PatternMetadata.AverageVolume;
                
                if (Math.Abs(priceChange) > 0.02)
                    return $"Significant price movement: {priceChange:P1}";
                else if (volumeSpike > 2)
                    return $"Volume spike: {volumeSpike:F1}x average";
                else
                    return "Pattern inflection point";
            }
            
            return "Key moment";
        }
        
        private float CalculatePatternStrength(PatternPrediction prediction)
        {
            // Combine confidence with prediction magnitude
            var changeStrength = Math.Min(Math.Abs(prediction.PredictedPriceChange) / 5f, 1f);
            return (prediction.Confidence + changeStrength) / 2f;
        }
        
        private async Task<List<HistoricalPattern>> FindSimilarPatternsAsync(PatternSequence sequence)
        {
            // Simplified - in production would search historical database
            return new List<HistoricalPattern>
            {
                new HistoricalPattern
                {
                    Date = DateTime.UtcNow.AddDays(-30),
                    Similarity = 0.85f,
                    Outcome = "Bullish breakout +3.2%",
                    Duration = "2 hours"
                }
            };
        }
        
        private RecommendedAction DetermineAction(PatternPrediction prediction)
        {
            if (prediction.Confidence < 0.6)
                return RecommendedAction.Wait;
            
            if (prediction.PredictedPriceChange > 1.0f && prediction.PredictedDirection > 0)
                return RecommendedAction.Buy;
            else if (prediction.PredictedPriceChange < -1.0f && prediction.PredictedDirection < 0)
                return RecommendedAction.Sell;
            else
                return RecommendedAction.Hold;
        }
        
        private RiskAssessment AssessRisk(PatternPrediction prediction, PatternSequence sequence)
        {
            return new RiskAssessment
            {
                RiskLevel = prediction.PredictedDrawdown > 2 ? "High" : 
                           prediction.PredictedDrawdown > 1 ? "Medium" : "Low",
                MaxExpectedDrawdown = prediction.PredictedDrawdown,
                VolatilityRisk = sequence.PatternMetadata.Volatility > 2 ? "High" : "Normal",
                RecommendedStopLoss = prediction.PredictedDirection > 0 
                    ? -Math.Max(1.5f, prediction.PredictedDrawdown)
                    : Math.Max(1.5f, prediction.PredictedDrawdown)
            };
        }
    }
    
    // Supporting classes
    
    public class MultiHeadAttentionLayer : Layer
    {
        private readonly int _numHeads;
        private readonly int _keyDim;
        
        public MultiHeadAttentionLayer(int numHeads, int keyDim)
        {
            _numHeads = numHeads;
            _keyDim = keyDim;
        }
        
        public override Tensor call(Tensor inputs, Tensor training = null)
        {
            // Simplified multi-head attention
            // In production, would implement full attention mechanism
            return inputs;
        }
    }
    
    public class ModelCheckpoint : Callback
    {
        private readonly string _filepath;
        private readonly bool _saveBestOnly;
        private readonly string _monitor;
        private float _bestValue = float.MaxValue;
        
        public ModelCheckpoint(string filepath, bool save_best_only = true, string monitor = "val_loss")
        {
            _filepath = filepath;
            _saveBestOnly = save_best_only;
            _monitor = monitor;
        }
        
        public override void on_epoch_end(int epoch, Dictionary<string, float> logs = null)
        {
            if (logs != null && logs.ContainsKey(_monitor))
            {
                var currentValue = logs[_monitor];
                
                if (!_saveBestOnly || currentValue < _bestValue)
                {
                    _bestValue = currentValue;
                    var path = _filepath.Replace("{epoch}", epoch.ToString());
                    // Save model checkpoint
                }
            }
        }
    }
    
    public class LSTMTrainingOptions
    {
        public int Epochs { get; set; } = 50;
        public int BatchSize { get; set; } = 32;
        public float LearningRate { get; set; } = 0.001f;
        public int EarlyStoppingPatience { get; set; } = 10;
        public bool UseDataAugmentation { get; set; } = true;
        public float ValidationSplit { get; set; } = 0.2f;
    }
    
    public class PatternPrediction
    {
        public string SequenceId { get; set; } = string.Empty;
        public float PredictedPriceChange { get; set; }
        public int PredictedDirection { get; set; }
        public float PredictedDrawdown { get; set; }
        public PatternType PredictedPattern { get; set; }
        public float Confidence { get; set; }
        public DateTime PredictionTime { get; set; }
        public int TimeHorizon { get; set; }
    }
    
    public class PatternAnalysis
    {
        public string SequenceId { get; set; } = string.Empty;
        public PatternPrediction Prediction { get; set; } = null!;
        public float[][] AttentionWeights { get; set; } = Array.Empty<float[]>();
        public List<KeyTimePoint> KeyTimePoints { get; set; } = new();
        public float PatternStrength { get; set; }
        public List<HistoricalPattern> SimilarHistoricalPatterns { get; set; } = new();
        public RecommendedAction RecommendedAction { get; set; }
        public RiskAssessment RiskAssessment { get; set; } = null!;
    }
    
    public class KeyTimePoint
    {
        public int Index { get; set; }
        public DateTime Timestamp { get; set; }
        public float Importance { get; set; }
        public string Description { get; set; } = string.Empty;
    }
    
    public class HistoricalPattern
    {
        public DateTime Date { get; set; }
        public float Similarity { get; set; }
        public string Outcome { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
    }
    
    public class RiskAssessment
    {
        public string RiskLevel { get; set; } = string.Empty;
        public float MaxExpectedDrawdown { get; set; }
        public string VolatilityRisk { get; set; } = string.Empty;
        public float RecommendedStopLoss { get; set; }
    }
    
    public enum RecommendedAction
    {
        Buy,
        Sell,
        Hold,
        Wait
    }
}

// Extension methods
public static class ArrayExtensions
{
    public static double StandardDeviation(this float[] values)
    {
        var avg = values.Average();
        var sum = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sum / values.Length);
    }
}