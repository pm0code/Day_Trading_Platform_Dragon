using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TradingPlatform.Core.Common;
using TradingPlatform.ML.Common;
using TradingPlatform.ML.Interfaces;
using TradingPlatform.ML.Models;
using TradingPlatform.ML.Ranking;

namespace TradingPlatform.ML.Tests
{
    public class RandomForestTests
    {
        private readonly Mock<ILogger<RandomForestRankingModel>> _loggerMock;
        private readonly RandomForestRankingModel _model;

        public RandomForestTests()
        {
            _loggerMock = new Mock<ILogger<RandomForestRankingModel>>();
            _model = new RandomForestRankingModel(_loggerMock.Object);
        }

        [Fact]
        public async Task TrainAsync_WithValidDataset_ReturnsSuccess()
        {
            // Arrange
            var dataset = CreateTestDataset(100);
            var options = new RankingTrainingOptions
            {
                NumberOfTrees = 50,
                MaxDepth = 5
            };

            // Act
            var result = await _model.TrainAsync(dataset, options);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.True(result.Data.TrainingMetrics["RMSE"] < 0.5);
            Assert.True(result.Data.TrainingMetrics["SpearmanCorrelation"] > 0.5);
        }

        [Fact]
        public async Task TrainAsync_WithEmptyDataset_ReturnsFailure()
        {
            // Arrange
            var dataset = CreateTestDataset(0);
            var options = new RankingTrainingOptions();

            // Act
            var result = await _model.TrainAsync(dataset, options);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("No data points", result.ErrorMessage);
        }

        [Fact]
        public async Task PredictAsync_WithTrainedModel_ReturnsValidScore()
        {
            // Arrange
            var dataset = CreateTestDataset(100);
            var options = new RankingTrainingOptions { NumberOfTrees = 10 };
            await _model.TrainAsync(dataset, options);

            var testFactors = CreateTestFactors();

            // Act
            var result = await _model.PredictAsync(testFactors);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.InRange(result.Data.Score, 0, 1);
            Assert.NotNull(result.Data.FeatureImportances);
        }

        [Fact]
        public async Task PredictAsync_WithoutTraining_ReturnsFailure()
        {
            // Arrange
            var testFactors = CreateTestFactors();

            // Act
            var result = await _model.PredictAsync(testFactors);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Model not trained", result.ErrorMessage);
        }

        [Fact]
        public async Task PredictBatchAsync_WithMultipleInputs_ReturnsCorrectCount()
        {
            // Arrange
            var dataset = CreateTestDataset(100);
            await _model.TrainAsync(dataset, new RankingTrainingOptions());

            var factorsList = new List<RankingFactors>();
            for (int i = 0; i < 10; i++)
            {
                factorsList.Add(CreateTestFactors());
            }

            // Act
            var result = await _model.PredictBatchAsync(factorsList);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(10, result.Data.Count);
            Assert.All(result.Data, p => Assert.InRange(p.Score, 0, 1));
        }

        [Fact]
        public async Task CrossValidateAsync_WithValidDataset_ReturnsConsistentResults()
        {
            // Arrange
            var dataset = CreateTestDataset(200);
            var options = new RankingTrainingOptions
            {
                NumberOfTrees = 20,
                MaxDepth = 4
            };
            var folds = 5;

            // Act
            var result = await _model.CrossValidateAsync(dataset, folds, options);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(folds, result.Data.FoldResults.Count);
            Assert.True(result.Data.ScoreStandardDeviation < 0.2); // Consistent across folds
            Assert.True(result.Data.AverageScore > 0.5); // Reasonable performance
        }

        [Fact]
        public async Task GetFeatureImportancesAsync_AfterTraining_ReturnsValidImportances()
        {
            // Arrange
            var dataset = CreateTestDataset(100);
            await _model.TrainAsync(dataset, new RankingTrainingOptions());

            // Act
            var result = await _model.GetFeatureImportancesAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotEmpty(result.Data);
            Assert.All(result.Data.Values, importance => Assert.InRange(importance, 0, 1));
            
            // Sum of importances should be close to 1
            var sum = result.Data.Values.Sum();
            Assert.InRange(sum, 0.9, 1.1);
        }

        [Fact]
        public async Task SaveAndLoadModel_PreservesModelState()
        {
            // Arrange
            var dataset = CreateTestDataset(100);
            var options = new RankingTrainingOptions { NumberOfTrees = 30 };
            await _model.TrainAsync(dataset, options);

            var testFactors = CreateTestFactors();
            var originalPrediction = await _model.PredictAsync(testFactors);
            
            var modelPath = $"test_model_{Guid.NewGuid()}.zip";

            try
            {
                // Act - Save
                var saveResult = await _model.SaveModelAsync(modelPath);
                Assert.True(saveResult.IsSuccess);

                // Create new model instance and load
                var loadedModel = new RandomForestRankingModel(_loggerMock.Object);
                var loadResult = await loadedModel.LoadModelAsync(modelPath);
                Assert.True(loadResult.IsSuccess);

                // Predict with loaded model
                var loadedPrediction = await loadedModel.PredictAsync(testFactors);

                // Assert
                Assert.True(loadedPrediction.IsSuccess);
                Assert.Equal(originalPrediction.Data.Score, loadedPrediction.Data.Score, 4);
            }
            finally
            {
                // Cleanup
                if (System.IO.File.Exists(modelPath))
                {
                    System.IO.File.Delete(modelPath);
                }
            }
        }

        [Theory]
        [InlineData(10, 3)]
        [InlineData(50, 5)]
        [InlineData(100, 10)]
        public async Task TrainAsync_WithDifferentTreeCounts_ScalesPerformance(
            int numberOfTrees, 
            int maxDepth)
        {
            // Arrange
            var dataset = CreateTestDataset(200);
            var options = new RankingTrainingOptions
            {
                NumberOfTrees = numberOfTrees,
                MaxDepth = maxDepth
            };

            // Act
            var startTime = DateTime.UtcNow;
            var result = await _model.TrainAsync(dataset, options);
            var trainingTime = DateTime.UtcNow - startTime;

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.Data.TrainingTime < TimeSpan.FromSeconds(10)); // Reasonable time
            
            // More trees should generally give better performance
            if (numberOfTrees >= 50)
            {
                Assert.True(result.Data.TrainingMetrics["SpearmanCorrelation"] > 0.6);
            }
        }

        [Fact]
        public async Task TrainAsync_WithEarlyStopping_StopsAppropriately()
        {
            // Arrange
            var dataset = CreateTestDataset(500);
            var options = new RankingTrainingOptions
            {
                NumberOfTrees = 200,
                UseEarlyStopping = true,
                EarlyStoppingRounds = 10
            };

            // Act
            var result = await _model.TrainAsync(dataset, options);

            // Assert
            Assert.True(result.IsSuccess);
            // Early stopping should prevent overfitting
            Assert.True(result.Data.ValidationMetrics["SpearmanCorrelation"] > 0.5);
        }

        // Helper methods
        private RankingDataset CreateTestDataset(int size)
        {
            var dataPoints = new List<RankingDataPoint>();
            var random = new Random(42); // Fixed seed for reproducibility

            for (int i = 0; i < size; i++)
            {
                var factors = CreateRandomFactors(random);
                var label = CalculateSyntheticLabel(factors, random);
                
                dataPoints.Add(new RankingDataPoint
                {
                    Symbol = $"TEST{i}",
                    Timestamp = DateTime.UtcNow.AddDays(-i),
                    Factors = factors,
                    Label = label
                });
            }

            return new RankingDataset
            {
                DataPoints = dataPoints,
                StartDate = DateTime.UtcNow.AddDays(-size),
                EndDate = DateTime.UtcNow,
                FeatureCount = 70
            };
        }

        private RankingFactors CreateTestFactors()
        {
            var random = new Random();
            return CreateRandomFactors(random);
        }

        private RankingFactors CreateRandomFactors(Random random)
        {
            return new RankingFactors
            {
                TechnicalFactors = new TechnicalFactors
                {
                    MomentumScore = (float)random.NextDouble(),
                    TrendStrength = (float)random.NextDouble(),
                    RelativeStrength = (float)random.NextDouble(),
                    VolumeProfile = (float)random.NextDouble(),
                    Volatility = (float)random.NextDouble(),
                    PriceEfficiency = (float)random.NextDouble(),
                    CompositeScore = (float)random.NextDouble(),
                    DataCompleteness = 0.9f + (float)random.NextDouble() * 0.1f
                },
                FundamentalFactors = new FundamentalFactors
                {
                    ValueScore = (float)random.NextDouble(),
                    GrowthScore = (float)random.NextDouble(),
                    ProfitabilityScore = (float)random.NextDouble(),
                    FinancialHealth = (float)random.NextDouble(),
                    EarningsQuality = (float)random.NextDouble(),
                    CompositeScore = (float)random.NextDouble(),
                    DataCompleteness = 0.8f + (float)random.NextDouble() * 0.2f
                },
                SentimentFactors = new SentimentFactors
                {
                    OverallSentiment = (float)random.NextDouble(),
                    SentimentMomentum = (float)random.NextDouble(),
                    NewsImpact = (float)random.NextDouble(),
                    SocialBuzz = (float)random.NextDouble(),
                    AnalystConsensus = (float)random.NextDouble(),
                    CompositeScore = (float)random.NextDouble(),
                    DataCompleteness = 0.7f + (float)random.NextDouble() * 0.3f
                },
                MicrostructureFactors = new MicrostructureFactors
                {
                    LiquidityScore = (float)random.NextDouble(),
                    SpreadEfficiency = (float)random.NextDouble(),
                    OrderFlowImbalance = (float)random.NextDouble(),
                    PriceImpact = (float)random.NextDouble(),
                    MarketDepth = (float)random.NextDouble(),
                    CompositeScore = (float)random.NextDouble(),
                    DataCompleteness = 0.85f + (float)random.NextDouble() * 0.15f
                },
                QualityFactors = new QualityFactors
                {
                    EarningsStability = (float)random.NextDouble(),
                    BalanceSheetStrength = (float)random.NextDouble(),
                    ManagementQuality = (float)random.NextDouble(),
                    CompetitiveAdvantage = (float)random.NextDouble(),
                    BusinessModelQuality = (float)random.NextDouble(),
                    CompositeQuality = (float)random.NextDouble(),
                    DataCompleteness = 0.9f + (float)random.NextDouble() * 0.1f
                },
                RiskFactors = new RiskFactors
                {
                    SystematicRisk = (float)random.NextDouble(),
                    IdiosyncraticRisk = (float)random.NextDouble(),
                    LiquidityRisk = (float)random.NextDouble(),
                    ConcentrationRisk = (float)random.NextDouble(),
                    TailRisk = (float)random.NextDouble(),
                    CompositeRisk = (float)random.NextDouble(),
                    DataCompleteness = 0.8f + (float)random.NextDouble() * 0.2f
                },
                DataQuality = 0.85f + (float)random.NextDouble() * 0.15f
            };
        }

        private float CalculateSyntheticLabel(RankingFactors factors, Random random)
        {
            // Create a synthetic label based on factor values with some noise
            var score = 
                factors.TechnicalFactors.MomentumScore * 0.25f +
                factors.FundamentalFactors.ValueScore * 0.20f +
                factors.SentimentFactors.OverallSentiment * 0.15f +
                factors.MicrostructureFactors.LiquidityScore * 0.15f +
                factors.QualityFactors.CompositeQuality * 0.15f +
                (1 - factors.RiskFactors.CompositeRisk) * 0.10f;

            // Add some noise
            score += (float)(random.NextDouble() * 0.1 - 0.05);

            return Math.Max(0, Math.Min(1, score));
        }
    }

    public class RankingScoreCalculatorTests
    {
        private readonly Mock<IRandomForestRankingModel> _modelMock;
        private readonly Mock<IMultiFactorFramework> _factorFrameworkMock;
        private readonly Mock<IModelPerformanceMonitor> _performanceMonitorMock;
        private readonly Mock<ILogger<RankingScoreCalculator>> _loggerMock;
        private readonly RankingScoreCalculator _calculator;

        public RankingScoreCalculatorTests()
        {
            _modelMock = new Mock<IRandomForestRankingModel>();
            _factorFrameworkMock = new Mock<IMultiFactorFramework>();
            _performanceMonitorMock = new Mock<IModelPerformanceMonitor>();
            _loggerMock = new Mock<ILogger<RankingScoreCalculator>>();
            
            _calculator = new RankingScoreCalculator(
                _modelMock.Object,
                _factorFrameworkMock.Object,
                _performanceMonitorMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task CalculateRankingScoreAsync_WithValidData_ReturnsScore()
        {
            // Arrange
            var stockData = CreateTestStockData();
            var marketContext = CreateTestMarketContext();
            var factors = CreateTestRankingFactors();
            var prediction = new RankingPrediction
            {
                Score = 0.75f,
                Confidence = 0.85f,
                FeatureImportances = new Dictionary<string, float>
                {
                    ["Momentum"] = 0.3f,
                    ["Value"] = 0.2f
                }
            };

            _factorFrameworkMock
                .Setup(x => x.ExtractFactors(It.IsAny<StockRankingData>(), It.IsAny<MarketContext>(), It.IsAny<FactorExtractionOptions>()))
                .Returns(factors);

            _modelMock
                .Setup(x => x.PredictAsync(It.IsAny<RankingFactors>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult<RankingPrediction>.Success(prediction));

            // Act
            var result = await _calculator.CalculateRankingScoreAsync(stockData, marketContext);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(stockData.Symbol, result.Data.Symbol);
            Assert.InRange(result.Data.CompositeScore, 0, 1);
            Assert.InRange(result.Data.Confidence, 0, 1);
            Assert.NotEmpty(result.Data.FactorContributions);
        }

        [Fact]
        public async Task RankStocksAsync_WithMultipleStocks_ReturnsSortedList()
        {
            // Arrange
            var stocks = new List<StockRankingData>();
            for (int i = 0; i < 5; i++)
            {
                stocks.Add(CreateTestStockData($"STOCK{i}"));
            }

            var marketContext = CreateTestMarketContext();
            var factors = CreateTestRankingFactors();

            _factorFrameworkMock
                .Setup(x => x.ExtractFactors(It.IsAny<StockRankingData>(), It.IsAny<MarketContext>(), It.IsAny<FactorExtractionOptions>()))
                .Returns(factors);

            // Setup different scores for each stock
            var scoreCounter = 0.9f;
            _modelMock
                .Setup(x => x.PredictAsync(It.IsAny<RankingFactors>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => 
                {
                    var score = scoreCounter;
                    scoreCounter -= 0.1f;
                    return TradingResult<RankingPrediction>.Success(new RankingPrediction 
                    { 
                        Score = score,
                        Confidence = 0.8f
                    });
                });

            // Act
            var result = await _calculator.RankStocksAsync(stocks, marketContext);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(5, result.Data.Count);
            Assert.True(result.Data[0].Score.CompositeScore > result.Data[1].Score.CompositeScore);
            Assert.Equal(1, result.Data[0].Rank);
            Assert.Equal(5, result.Data[4].Rank);
        }

        [Fact]
        public async Task CalculateRankingScoreAsync_WithCaching_ReturnsCachedResult()
        {
            // Arrange
            var stockData = CreateTestStockData();
            var marketContext = CreateTestMarketContext();
            var options = new RankingOptions { UseCache = true };
            var factors = CreateTestRankingFactors();
            var prediction = new RankingPrediction { Score = 0.8f };

            _factorFrameworkMock
                .Setup(x => x.ExtractFactors(It.IsAny<StockRankingData>(), It.IsAny<MarketContext>(), It.IsAny<FactorExtractionOptions>()))
                .Returns(factors);

            _modelMock
                .Setup(x => x.PredictAsync(It.IsAny<RankingFactors>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult<RankingPrediction>.Success(prediction));

            // Act - First call
            var result1 = await _calculator.CalculateRankingScoreAsync(stockData, marketContext, options);
            
            // Act - Second call (should use cache)
            var result2 = await _calculator.CalculateRankingScoreAsync(stockData, marketContext, options);

            // Assert
            Assert.True(result1.IsSuccess);
            Assert.True(result2.IsSuccess);
            Assert.Equal(result1.Data.CompositeScore, result2.Data.CompositeScore);
            
            // Verify model was called only once
            _modelMock.Verify(x => x.PredictAsync(It.IsAny<RankingFactors>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        private StockRankingData CreateTestStockData(string symbol = "TEST")
        {
            return new StockRankingData
            {
                Symbol = symbol,
                Timestamp = DateTime.UtcNow,
                MarketData = new MarketData
                {
                    Price = 100m,
                    Volume = 1000000,
                    ChangePercent = 1.5m
                },
                FundamentalData = new FundamentalData
                {
                    MarketCap = 1000000000m,
                    PERatio = 15m,
                    Sector = "Technology"
                }
            };
        }

        private MarketContext CreateTestMarketContext()
        {
            return new MarketContext
            {
                Timestamp = DateTime.UtcNow,
                MarketRegime = MarketRegime.Normal,
                MarketTrend = MarketTrend.Up,
                MarketVolatility = 0.2f,
                MarketLiquidity = 0.8f
            };
        }

        private RankingFactors CreateTestRankingFactors()
        {
            return new RankingFactors
            {
                TechnicalFactors = new TechnicalFactors { CompositeScore = 0.7f },
                FundamentalFactors = new FundamentalFactors { CompositeScore = 0.6f },
                SentimentFactors = new SentimentFactors { CompositeScore = 0.5f },
                MicrostructureFactors = new MicrostructureFactors { CompositeScore = 0.8f },
                QualityFactors = new QualityFactors { CompositeQuality = 0.7f },
                DataQuality = 0.9f
            };
        }
    }
}