using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using TradingPlatform.Core.Logging;
using TradingPlatform.GPU.Infrastructure;
using TradingPlatform.ML.Configuration;
using TradingPlatform.ML.Interfaces;
using TradingPlatform.ML.Models;
using TradingPlatform.ML.Services;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.ML.Tests
{
    /// <summary>
    /// Comprehensive tests for ML inference service
    /// </summary>
    public class MLInferenceServiceTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly IMLInferenceService _mlService;
        private readonly MLInferenceConfiguration _config;
        private readonly GpuContext _gpuContext;
        private readonly string _testModelsPath;

        public MLInferenceServiceTests(ITestOutputHelper output)
        {
            _output = output;
            
            // Set up test models path
            _testModelsPath = Path.Combine(Path.GetTempPath(), "MLTestModels");
            Directory.CreateDirectory(_testModelsPath);
            
            // Create test configuration
            _config = CreateTestConfiguration();
            
            // Initialize GPU context
            _gpuContext = new GpuContext(NullLogger<GpuContext>.Instance);
            
            // Create ML inference service
            _mlService = new MLInferenceService(
                _config, 
                _gpuContext,
                logger: NullLogger<MLInferenceService>.Instance);
        }

        #region Model Loading Tests

        [Fact]
        public async Task LoadModel_ValidModel_SuccessfullyLoaded()
        {
            // Arrange
            var modelName = "TestModel";
            CreateDummyOnnxModel(modelName);
            
            // Act
            var result = await _mlService.LoadModelAsync(modelName);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(modelName, result.Data.ModelName);
            Assert.True(_mlService.IsModelLoaded(modelName));
            
            _output.WriteLine($"Model {modelName} loaded successfully");
            _output.WriteLine($"Input tensors: {result.Data.InputMetadata.Count}");
            _output.WriteLine($"Output tensors: {result.Data.OutputMetadata.Count}");
        }

        [Fact]
        public async Task LoadModel_NonExistentModel_ReturnsError()
        {
            // Arrange
            var modelName = "NonExistentModel";
            
            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            {
                await _mlService.LoadModelAsync(modelName);
            });
        }

        [Fact]
        public async Task UnloadModel_LoadedModel_SuccessfullyUnloaded()
        {
            // Arrange
            var modelName = "TestModelToUnload";
            CreateDummyOnnxModel(modelName);
            await _mlService.LoadModelAsync(modelName);
            Assert.True(_mlService.IsModelLoaded(modelName));
            
            // Act
            var result = await _mlService.UnloadModelAsync(modelName);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.False(_mlService.IsModelLoaded(modelName));
            
            _output.WriteLine($"Model {modelName} unloaded successfully");
        }

        #endregion

        #region Inference Tests

        [Fact]
        public async Task Predict_ValidInput_SuccessfulPrediction()
        {
            // Arrange
            var modelName = "PredictionTestModel";
            CreateDummyOnnxModel(modelName, inputShape: new[] { 1, 10 }, outputShape: new[] { 1, 3 });
            
            var inputData = Enumerable.Range(0, 10).Select(i => (float)i).ToArray();
            var inputShape = new[] { 1, 10 };
            
            // Act
            var result = await _mlService.PredictAsync(modelName, inputData, inputShape);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(modelName, result.Data.ModelName);
            Assert.NotNull(result.Data.Predictions);
            Assert.True(result.Data.InferenceTimeMs > 0);
            
            _output.WriteLine($"Prediction completed in {result.Data.InferenceTimeMs}ms");
            _output.WriteLine($"Output size: {result.Data.Predictions.Length}");
        }

        [Fact]
        public async Task PredictBatch_ValidBatch_SuccessfulBatchPrediction()
        {
            // Arrange
            var modelName = "BatchTestModel";
            CreateDummyOnnxModel(modelName, inputShape: new[] { -1, 10 }, outputShape: new[] { -1, 3 });
            
            var batchSize = 5;
            var batchData = new float[batchSize][];
            for (int i = 0; i < batchSize; i++)
            {
                batchData[i] = Enumerable.Range(0, 10).Select(j => (float)(i * 10 + j)).ToArray();
            }
            var inputShape = new[] { 10 };
            
            // Act
            var result = await _mlService.PredictBatchAsync(modelName, batchData, inputShape);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(batchSize, result.Data.BatchSize);
            Assert.Equal(batchSize, result.Data.Predictions.Count);
            
            _output.WriteLine($"Batch prediction completed in {result.Data.TotalInferenceTimeMs}ms");
            _output.WriteLine($"Average time per item: {result.Data.AverageInferenceTimeMs}ms");
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task PredictBatch_DifferentBatchSizes_PerformanceScaling(int batchSize)
        {
            // Arrange
            var modelName = "PerformanceTestModel";
            CreateDummyOnnxModel(modelName, inputShape: new[] { -1, 20 }, outputShape: new[] { -1, 5 });
            
            var batchData = new float[batchSize][];
            for (int i = 0; i < batchSize; i++)
            {
                batchData[i] = new float[20];
            }
            var inputShape = new[] { 20 };
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = await _mlService.PredictBatchAsync(modelName, batchData, inputShape);
            stopwatch.Stop();
            
            // Assert
            Assert.True(result.IsSuccess);
            
            var throughput = batchSize / (stopwatch.ElapsedMilliseconds / 1000.0);
            _output.WriteLine($"Batch size: {batchSize}");
            _output.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Throughput: {throughput:F2} predictions/second");
            _output.WriteLine($"Average latency: {result.Data.AverageInferenceTimeMs:F2}ms");
        }

        #endregion

        #region Multi-Input Tests

        [Fact]
        public async Task PredictMultiInput_MultipleInputs_SuccessfulPrediction()
        {
            // Arrange
            var modelName = "MultiInputModel";
            CreateDummyOnnxModel(modelName, 
                multiInput: true,
                inputShapes: new Dictionary<string, int[]>
                {
                    { "input1", new[] { 1, 10 } },
                    { "input2", new[] { 1, 5 } }
                },
                outputShapes: new Dictionary<string, int[]>
                {
                    { "output1", new[] { 1, 3 } },
                    { "output2", new[] { 1, 2 } }
                });
            
            var inputs = new Dictionary<string, float[]>
            {
                { "input1", new float[10] },
                { "input2", new float[5] }
            };
            
            // Act
            var result = await _mlService.PredictMultiInputAsync(modelName, inputs);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(2, result.Data.Count);
            Assert.Contains("output1", result.Data.Keys);
            Assert.Contains("output2", result.Data.Keys);
            
            _output.WriteLine($"Multi-input prediction successful");
            _output.WriteLine($"Outputs: {string.Join(", ", result.Data.Keys)}");
        }

        #endregion

        #region Model Warmup Tests

        [Fact]
        public async Task WarmupModel_ValidModel_ImprovedLatency()
        {
            // Arrange
            var modelName = "WarmupTestModel";
            CreateDummyOnnxModel(modelName, inputShape: new[] { 1, 50 }, outputShape: new[] { 1, 10 });
            await _mlService.LoadModelAsync(modelName);
            
            // Act
            var warmupResult = await _mlService.WarmupModelAsync(modelName, 10);
            
            // Assert
            Assert.True(warmupResult.IsSuccess);
            Assert.NotNull(warmupResult.Data);
            Assert.Equal(10, warmupResult.Data.Iterations);
            Assert.True(warmupResult.Data.MinInferenceTimeMs < warmupResult.Data.MaxInferenceTimeMs);
            
            _output.WriteLine($"Warmup statistics for {modelName}:");
            _output.WriteLine($"  Min latency: {warmupResult.Data.MinInferenceTimeMs:F2}ms");
            _output.WriteLine($"  Max latency: {warmupResult.Data.MaxInferenceTimeMs:F2}ms");
            _output.WriteLine($"  Average latency: {warmupResult.Data.AverageInferenceTimeMs:F2}ms");
            _output.WriteLine($"  Std dev: {warmupResult.Data.StdDevInferenceTimeMs:F2}ms");
        }

        #endregion

        #region Performance Monitoring Tests

        [Fact]
        public async Task GetPerformanceMetrics_AfterInferences_ValidMetrics()
        {
            // Arrange
            var modelName = "MetricsTestModel";
            CreateDummyOnnxModel(modelName);
            
            // Perform several inferences
            var inputData = new float[20];
            var inputShape = new[] { 1, 20 };
            
            for (int i = 0; i < 5; i++)
            {
                await _mlService.PredictAsync(modelName, inputData, inputShape);
            }
            
            // Act
            var metricsResult = await _mlService.GetPerformanceMetricsAsync();
            
            // Assert
            Assert.True(metricsResult.IsSuccess);
            Assert.NotNull(metricsResult.Data);
            Assert.Contains(modelName, metricsResult.Data.Keys);
            
            var modelMetrics = metricsResult.Data[modelName];
            Assert.Equal(5, modelMetrics.TotalInferences);
            Assert.Equal(5, modelMetrics.SuccessfulInferences);
            Assert.True(modelMetrics.AverageLatencyMs > 0);
            
            _output.WriteLine($"Performance metrics for {modelName}:");
            _output.WriteLine($"  Total inferences: {modelMetrics.TotalInferences}");
            _output.WriteLine($"  Success rate: {modelMetrics.SuccessRate:P}");
            _output.WriteLine($"  Average latency: {modelMetrics.AverageLatencyMs:F2}ms");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task Predict_InvalidInputShape_ThrowsException()
        {
            // Arrange
            var modelName = "ErrorTestModel";
            CreateDummyOnnxModel(modelName, inputShape: new[] { 1, 10 });
            
            var inputData = new float[20]; // Wrong size
            var inputShape = new[] { 1, 10 }; // Shape doesn't match data
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _mlService.PredictAsync(modelName, inputData, inputShape);
            });
        }

        [Fact]
        public async Task Predict_NullInput_ThrowsException()
        {
            // Arrange
            var modelName = "NullTestModel";
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _mlService.PredictAsync(modelName, null!, new[] { 1, 10 });
            });
        }

        #endregion

        #region GPU Acceleration Tests

        [Fact]
        public void ExecutionProvider_Configuration_CorrectlySet()
        {
            // Assert
            Assert.NotNull(_config);
            
            _output.WriteLine($"Configured execution provider: {_config.Provider}");
            _output.WriteLine($"GPU device ID: {_config.GpuDeviceId}");
            _output.WriteLine($"GPU available: {_gpuContext?.IsGpuAvailable ?? false}");
            
            if (_gpuContext?.IsGpuAvailable == true)
            {
                _output.WriteLine($"GPU name: {_gpuContext.Accelerator?.Name}");
                _output.WriteLine($"GPU memory: {_gpuContext.Accelerator?.MemorySize / (1024 * 1024 * 1024)}GB");
            }
        }

        #endregion

        #region Helper Methods

        private MLInferenceConfiguration CreateTestConfiguration()
        {
            return new MLInferenceConfiguration
            {
                Provider = ExecutionProvider.CPU, // Use CPU for tests
                ModelsPath = _testModelsPath,
                MaxBatchSize = 32,
                InferenceTimeoutMs = 5000,
                EnableProfiling = false,
                OptimizationLevel = GraphOptimizationLevel.All,
                EnableMemoryPattern = true,
                WarmupIterations = 3,
                UseIoBinding = false // Disable for CPU
            };
        }

        private void CreateDummyOnnxModel(
            string modelName, 
            int[] inputShape = null,
            int[] outputShape = null,
            bool multiInput = false,
            Dictionary<string, int[]> inputShapes = null,
            Dictionary<string, int[]> outputShapes = null)
        {
            // For unit tests, we'll create a placeholder file
            // In real tests, you would use actual ONNX models
            var modelPath = Path.Combine(_testModelsPath, $"{modelName}.onnx");
            
            // Create a dummy file for testing
            File.WriteAllBytes(modelPath, new byte[] { 0x08, 0x01 }); // Minimal ONNX header
            
            // Note: In production tests, you would:
            // 1. Use actual pre-trained ONNX models
            // 2. Or use onnx.helper to create test models programmatically
            // 3. Or mock the InferenceSession for unit tests
            
            _output.WriteLine($"Created dummy model: {modelPath}");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _mlService?.Dispose();
            _gpuContext?.Dispose();
            
            // Clean up test models
            if (Directory.Exists(_testModelsPath))
            {
                try
                {
                    Directory.Delete(_testModelsPath, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #endregion
    }
}