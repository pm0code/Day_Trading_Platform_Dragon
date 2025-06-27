// File: TradingPlatform.ML/Tests/ModelValidationTests.cs

using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;
using TradingPlatform.ML.Data;
using TradingPlatform.ML.Interfaces;
using TradingPlatform.ML.Models;
using TradingPlatform.ML.Training;
using Xunit;

namespace TradingPlatform.ML.Tests
{
    /// <summary>
    /// Integration tests for model validation and backtesting
    /// </summary>
    public class ModelValidationTests : CanonicalTestBase
    {
        private readonly ModelValidator _validator;
        private readonly BacktestingEngine _backtestingEngine;
        private readonly IServiceProvider _serviceProvider;
        
        public ModelValidationTests()
        {
            var services = new ServiceCollection();
            
            // Register ML services
            services.AddSingleton<Microsoft.ML.MLContext>();
            services.AddTransient<ITradingLogger, TestTradingLogger>();
            services.AddTransient<ModelValidator>();
            services.AddTransient<BacktestingEngine>();
            
            _serviceProvider = services.BuildServiceProvider();
            _validator = _serviceProvider.GetRequiredService<ModelValidator>();
            _backtestingEngine = _serviceProvider.GetRequiredService<BacktestingEngine>();
        }
        
        [Fact]
        public async Task WalkForwardAnalysis_WithValidData_ShouldProduceStableResults()
        {
            // Arrange
            var dataset = GenerateTestMarketDataset(5000);
            var model = new MockPriceModel();
            
            var options = new WalkForwardOptions
            {
                WindowCount = 5,
                TrainWindowSize = 500,
                TestWindowSize = 100,
                StepSize = 100,
                TrainingIterations = 10
            };
            
            // Act
            var result = await _validator.PerformWalkForwardAnalysisAsync(
                model, dataset, options);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(5, result.Value.Windows.Count);
            Assert.True(result.Value.StabilityScore > 0.7);
            Assert.True(result.Value.OverfittingScore < 0.3);
            Assert.True(result.Value.AverageDirectionalAccuracy > 0.5);
        }
        
        [Fact]
        public async Task Backtesting_WithProfitableStrategy_ShouldGeneratePositiveReturns()
        {
            // Arrange
            var historicalData = GenerateTestMarketData(1000);
            var model = new MockPriceModel(bias: 0.001f); // Slight positive bias
            
            var options = new BacktestOptions
            {
                StrategyName = "Test Strategy",
                LookbackPeriod = 50,
                PositionSizePercent = 0.1m,
                BuyThreshold = 0.5f,
                SellThreshold = 0.5f,
                ConfidenceThreshold = 0.6f
            };
            
            // Act
            var result = await _backtestingEngine.RunBacktestAsync(
                model, historicalData, options);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.Value.TotalReturn > 0);
            Assert.True(result.Value.PerformanceMetrics.SharpeRatio > 0);
            Assert.True(result.Value.PerformanceMetrics.WinRate > 0.4);
            Assert.True(result.Value.PerformanceMetrics.MaxDrawdown < 0.2);
        }
        
        [Fact]
        public async Task ComparativeBacktest_WithMultipleModels_ShouldIdentifyBestModel()
        {
            // Arrange
            var historicalData = GenerateTestMarketData(1000);
            
            var models = new Dictionary<string, IPredictiveModel<PricePredictionInput, PricePrediction>>
            {
                ["Conservative"] = new MockPriceModel(bias: 0.0005f, confidence: 0.7f),
                ["Aggressive"] = new MockPriceModel(bias: 0.002f, confidence: 0.5f),
                ["Balanced"] = new MockPriceModel(bias: 0.001f, confidence: 0.6f)
            };
            
            var options = new BacktestOptions
            {
                LookbackPeriod = 50,
                PositionSizePercent = 0.1m
            };
            
            // Act
            var result = await _backtestingEngine.RunComparativeBacktestAsync(
                models, historicalData, options);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.ModelResults.Count);
            Assert.NotEmpty(result.Value.BestModel);
            Assert.Contains(result.Value.BestModel, models.Keys);
        }
        
        [Fact]
        public async Task MarketConditionValidation_ShouldIdentifyModelStrengthsWeaknesses()
        {
            // Arrange
            var model = new MockPriceModel();
            
            var conditionDatasets = new Dictionary<MarketCondition, MarketDataset>
            {
                [MarketCondition.Bullish] = GenerateBullishMarketDataset(1000),
                [MarketCondition.Bearish] = GenerateBearishMarketDataset(1000),
                [MarketCondition.Volatile] = GenerateVolatileMarketDataset(1000),
                [MarketCondition.Stable] = GenerateStableMarketDataset(1000)
            };
            
            // Act
            var result = await _validator.ValidateAcrossMarketConditionsAsync(
                model, conditionDatasets);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(4, result.Value.ConditionResults.Count);
            Assert.True(result.Value.ConsistencyScore > 0.5);
            Assert.True(result.Value.RobustnessScore > 0.5);
        }
        
        [Fact]
        public async Task SensitivityAnalysis_ShouldIdentifyImportantFeatures()
        {
            // Arrange
            var model = new MockPriceModel();
            var baselineInput = CreateBaselinePredictionInput();
            
            var options = new SensitivityOptions
            {
                PerturbationLevels = new[] { -0.1, -0.05, 0.05, 0.1 }
            };
            
            // Act
            var result = await _validator.PerformSensitivityAnalysisAsync(
                model, baselineInput, options);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotEmpty(result.Value.FeatureSensitivities);
            Assert.NotEmpty(result.Value.MostSensitiveFeature);
            Assert.NotEmpty(result.Value.LeastSensitiveFeature);
            Assert.NotEqual(result.Value.MostSensitiveFeature, result.Value.LeastSensitiveFeature);
        }
        
        // Helper methods
        
        private MarketDataset GenerateTestMarketDataset(int count)
        {
            return new MarketDataset
            {
                Symbol = "TEST",
                Data = GenerateTestMarketData(count),
                StartDate = DateTime.UtcNow.AddDays(-count),
                EndDate = DateTime.UtcNow
            };
        }
        
        private List<MarketDataSnapshot> GenerateTestMarketData(int count)
        {
            var data = new List<MarketDataSnapshot>();
            var random = new Random(42); // Fixed seed for reproducibility
            var basePrice = 100m;
            
            for (int i = 0; i < count; i++)
            {
                var change = (decimal)(random.NextDouble() * 4 - 2); // -2% to +2%
                basePrice *= (1 + change / 100);
                
                var high = basePrice * (1 + (decimal)random.NextDouble() * 0.01m);
                var low = basePrice * (1 - (decimal)random.NextDouble() * 0.01m);
                var close = low + (high - low) * (decimal)random.NextDouble();
                
                data.Add(new MarketDataSnapshot
                {
                    Symbol = "TEST",
                    Timestamp = DateTime.UtcNow.AddMinutes(-count + i),
                    Open = basePrice,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = random.Next(1000000, 10000000),
                    Source = "Test"
                });
            }
            
            return data;
        }
        
        private MarketDataset GenerateBullishMarketDataset(int count)
        {
            var dataset = GenerateTestMarketDataset(count);
            // Add upward bias
            for (int i = 0; i < dataset.Data.Count; i++)
            {
                dataset.Data[i].Close *= (1 + 0.0001m * i);
            }
            return dataset;
        }
        
        private MarketDataset GenerateBearishMarketDataset(int count)
        {
            var dataset = GenerateTestMarketDataset(count);
            // Add downward bias
            for (int i = 0; i < dataset.Data.Count; i++)
            {
                dataset.Data[i].Close *= (1 - 0.0001m * i);
            }
            return dataset;
        }
        
        private MarketDataset GenerateVolatileMarketDataset(int count)
        {
            var dataset = GenerateTestMarketDataset(count);
            var random = new Random(42);
            // Increase volatility
            for (int i = 0; i < dataset.Data.Count; i++)
            {
                var multiplier = 1 + (decimal)(random.NextDouble() * 0.1 - 0.05);
                dataset.Data[i].High *= multiplier * 1.02m;
                dataset.Data[i].Low *= multiplier * 0.98m;
            }
            return dataset;
        }
        
        private MarketDataset GenerateStableMarketDataset(int count)
        {
            var dataset = GenerateTestMarketDataset(count);
            // Reduce volatility
            var avg = dataset.Data.Average(d => d.Close);
            for (int i = 0; i < dataset.Data.Count; i++)
            {
                var diff = dataset.Data[i].Close - avg;
                dataset.Data[i].Close = avg + diff * 0.3m;
            }
            return dataset;
        }
        
        private PricePredictionInput CreateBaselinePredictionInput()
        {
            return new PricePredictionInput
            {
                Open = 100f,
                High = 102f,
                Low = 99f,
                Close = 101f,
                Volume = 1000000f,
                RSI = 50f,
                MACD = 0f,
                BollingerUpper = 103f,
                BollingerLower = 97f,
                MovingAverage20 = 100f,
                MovingAverage50 = 99f,
                VolumeRatio = 1f,
                PriceChangePercent = 1f,
                MarketCap = 1000000000f,
                DayOfWeek = 3f,
                HourOfDay = 10
            };
        }
    }
    
    // Mock implementations for testing
    
    public class MockPriceModel : IPredictiveModel<PricePredictionInput, PricePrediction>, IMLModel
    {
        private readonly decimal _bias;
        private readonly decimal _confidence;
        private readonly Random _random = new Random(42);
        
        public MockPriceModel(decimal bias = 0, decimal confidence = 0.65m)
        {
            _bias = bias;
            _confidence = confidence;
        }
        
        public Task<TradingResult<PricePrediction>> PredictAsync(
            PricePredictionInput input, 
            CancellationToken cancellationToken = default)
        {
            var changePercent = (decimal)(_random.NextDouble() * 2 - 1) + _bias;
            var predictedPrice = input.Close * (1 + changePercent / 100);
            
            var prediction = new PricePrediction
            {
                PredictedPrice = predictedPrice,
                PriceChangePercent = changePercent,
                Confidence = _confidence + (decimal)(_random.NextDouble() * 0.2 - 0.1),
                PredictionTime = DateTime.UtcNow
            };
            
            return Task.FromResult(TradingResult<PricePrediction>.Success(prediction));
        }
        
        public Task<TradingResult<List<PricePrediction>>> PredictBatchAsync(
            List<PricePredictionInput> inputs,
            CancellationToken cancellationToken = default)
        {
            var predictions = new List<PricePrediction>();
            foreach (var input in inputs)
            {
                var result = PredictAsync(input, cancellationToken).Result;
                if (result.IsSuccess)
                    predictions.Add(result.Value);
            }
            return Task.FromResult(TradingResult<List<PricePrediction>>.Success(predictions));
        }
        
        public Task<TradingResult<ModelTrainingResult>> TrainAsync(
            IMLDataset dataset,
            ModelTrainingOptions options,
            CancellationToken cancellationToken = default)
        {
            // Mock training
            var result = new ModelTrainingResult
            {
                ModelId = Guid.NewGuid().ToString(),
                TrainingStartTime = DateTime.UtcNow.AddMinutes(-5),
                TrainingEndTime = DateTime.UtcNow,
                Metrics = new Dictionary<string, decimal>
                {
                    ["RMSE"] = 0.02,
                    ["MAE"] = 0.015,
                    ["R2"] = 0.85
                }
            };
            
            return Task.FromResult(TradingResult<ModelTrainingResult>.Success(result));
        }
        
        public Task<TradingResult<ModelEvaluationResult>> EvaluateAsync(
            IMLDataset dataset,
            CancellationToken cancellationToken = default)
        {
            // Mock evaluation
            var result = new ModelEvaluationResult
            {
                RootMeanSquaredError = 0.025,
                MeanAbsoluteError = 0.018,
                R2Score = 0.82,
                CustomMetrics = new Dictionary<string, decimal>
                {
                    ["DirectionalAccuracy"] = 0.65
                }
            };
            
            return Task.FromResult(TradingResult<ModelEvaluationResult>.Success(result));
        }
    }
    
    public class TestTradingLogger : ITradingLogger
    {
        public void LogInfo(string message, string? source = null, object? additionalData = null) { }
        public void LogWarning(string message, string? source = null, object? additionalData = null) { }
        public void LogError(string message, Exception? exception = null, string? source = null, object? additionalData = null) { }
        public void LogDebug(string message, string? source = null, object? additionalData = null) { }
        public void LogCritical(string message, Exception? exception = null, string? source = null, object? additionalData = null) { }
        public void LogPerformance(string operation, long durationMs, string? source = null, object? additionalData = null) { }
        public void LogTrade(string symbol, string action, decimal quantity, decimal price, string? source = null, object? additionalData = null) { }
        public void LogRisk(string riskType, decimal value, string? source = null, object? additionalData = null) { }
        public void LogMethodEntry(string methodName, object? parameters = null, string? callerName = null) { }
        public void LogMethodExit(string methodName, object? result = null, string? callerName = null) { }
    }
    
    public abstract class CanonicalTestBase
    {
        protected CanonicalTestBase() { }
    }
    
    public interface IMLDataset
    {
        string Name { get; }
        int Count { get; }
    }
}