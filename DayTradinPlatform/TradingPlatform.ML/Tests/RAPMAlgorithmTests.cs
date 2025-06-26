using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using TradingPlatform.Core.Common;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Canonical;
using TradingPlatform.ML.Algorithms.RAPM;
using TradingPlatform.ML.Common;
using TradingPlatform.ML.Interfaces;

namespace TradingPlatform.ML.Tests
{
    /// <summary>
    /// Comprehensive unit tests for RAPM algorithm implementation
    /// Validates all canonical patterns, extensive logging, and no exceptions
    /// </summary>
    public class RAPMAlgorithmTests
    {
        private readonly Mock<RiskMeasures> _riskMeasuresMock;
        private readonly Mock<ProfitOptimizationEngine> _optimizationEngineMock;
        private readonly Mock<IPositionSizingService> _positionSizingMock;
        private readonly Mock<IPortfolioRebalancer> _rebalancerMock;
        private readonly Mock<IMarketDataService> _marketDataServiceMock;
        private readonly Mock<IRankingScoreCalculator> _rankingCalculatorMock;
        private readonly Mock<IModelPerformanceMonitor> _performanceMonitorMock;
        private readonly Mock<ICanonicalLogger> _loggerMock;
        private readonly RAPMConfiguration _testConfig;
        private readonly RAPMAlgorithm _algorithm;

        public RAPMAlgorithmTests()
        {
            _riskMeasuresMock = new Mock<RiskMeasures>(MockBehavior.Strict, new Mock<ICanonicalLogger>().Object);
            _optimizationEngineMock = new Mock<ProfitOptimizationEngine>(MockBehavior.Strict);
            _positionSizingMock = new Mock<IPositionSizingService>(MockBehavior.Strict);
            _rebalancerMock = new Mock<IPortfolioRebalancer>(MockBehavior.Strict);
            _marketDataServiceMock = new Mock<IMarketDataService>(MockBehavior.Strict);
            _rankingCalculatorMock = new Mock<IRankingScoreCalculator>(MockBehavior.Strict);
            _performanceMonitorMock = new Mock<IModelPerformanceMonitor>(MockBehavior.Strict);
            _loggerMock = new Mock<ICanonicalLogger>(MockBehavior.Strict);
            
            _testConfig = new RAPMConfiguration
            {
                TargetSharpeRatio = 1.5f,
                MaxAssets = 30,
                BaseRiskBudget = 0.15f
            };

            SetupLoggerMock();
            
            _algorithm = new RAPMAlgorithm(
                _riskMeasuresMock.Object,
                _optimizationEngineMock.Object,
                _positionSizingMock.Object,
                _rebalancerMock.Object,
                _marketDataServiceMock.Object,
                _rankingCalculatorMock.Object,
                _performanceMonitorMock.Object,
                _testConfig,
                _loggerMock.Object);
        }

        private void SetupLoggerMock()
        {
            _loggerMock.Setup(x => x.LogMethodEntry(It.IsAny<string>()));
            _loggerMock.Setup(x => x.LogMethodExit(It.IsAny<string>()));
            _loggerMock.Setup(x => x.LogInfo(It.IsAny<string>()));
            _loggerMock.Setup(x => x.LogDebug(It.IsAny<string>()));
            _loggerMock.Setup(x => x.LogWarning(It.IsAny<string>()));
            _loggerMock.Setup(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()));
        }

        [Fact]
        public async Task OptimizePortfolioAsync_WithValidInputs_ReturnsSuccessfulResult()
        {
            // Arrange
            var candidateSymbols = new List<string> { "AAPL", "GOOGL", "MSFT", "AMZN", "META" };
            var marketContext = CreateTestMarketContext();
            var rankedStocks = CreateTestRankedStocks(candidateSymbols);
            var expectedReturns = CreateTestExpectedReturns(candidateSymbols);
            var covariance = CreateTestCovariance(candidateSymbols.Count);
            var optimizationResult = CreateTestOptimizationResult(candidateSymbols);

            SetupMocksForSuccessfulOptimization(rankedStocks, expectedReturns, covariance, optimizationResult);

            // Act
            var result = await _algorithm.OptimizePortfolioAsync(candidateSymbols, marketContext);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(candidateSymbols.Count, result.Data.Weights.Count);
            Assert.True(result.Data.ExpectedSharpeRatio >= 1.5f); // Research benchmark
            Assert.True(result.Data.ConditionalValueAtRisk > 0);
            
            // Verify logging
            VerifyLoggingCalls();
        }

        [Fact]
        public async Task OptimizePortfolioAsync_WithCrisisRegime_AdjustsRiskBudget()
        {
            // Arrange
            var candidateSymbols = new List<string> { "AAPL", "GOOGL" };
            var marketContext = new MarketContext
            {
                MarketRegime = MarketRegime.Crisis,
                MarketVolatility = 0.4f,
                Timestamp = DateTime.UtcNow
            };

            SetupMocksForCrisisScenario();

            // Act
            var result = await _algorithm.OptimizePortfolioAsync(candidateSymbols, marketContext);

            // Assert
            Assert.True(result.IsSuccess);
            
            // Verify crisis adjustments were applied
            _optimizationEngineMock.Verify(x => x.OptimizePortfolioAsync(
                It.IsAny<ExpectedReturns>(),
                It.IsAny<CovarianceMatrix>(),
                It.Is<OptimizationConstraints>(c => c.RiskBudget < _testConfig.BaseRiskBudget),
                It.IsAny<OptimizationMethod>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task OptimizePortfolioAsync_WithRankingFailure_ReturnsFailure()
        {
            // Arrange
            var candidateSymbols = new List<string> { "INVALID" };
            var marketContext = CreateTestMarketContext();

            _rankingCalculatorMock
                .Setup(x => x.RankStocksAsync(
                    It.IsAny<IEnumerable<StockRankingData>>(),
                    It.IsAny<MarketContext>(),
                    It.IsAny<RankingOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult<List<RankedStock>>.Failure("Ranking failed"));

            // Act
            var result = await _algorithm.OptimizePortfolioAsync(candidateSymbols, marketContext);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Ranking failed", result.ErrorMessage);
            
            // Verify error logging
            _loggerMock.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task InitializeAsync_ValidatesConfiguration_LogsWarningForLowSharpe()
        {
            // Arrange
            var lowSharpeConfig = new RAPMConfiguration { TargetSharpeRatio = 1.0f };
            var algorithm = new RAPMAlgorithm(
                _riskMeasuresMock.Object,
                _optimizationEngineMock.Object,
                _positionSizingMock.Object,
                _rebalancerMock.Object,
                _marketDataServiceMock.Object,
                _rankingCalculatorMock.Object,
                _performanceMonitorMock.Object,
                lowSharpeConfig,
                _loggerMock.Object);

            // Act
            var result = await algorithm.InitializeAsync();

            // Assert
            Assert.True(result.IsSuccess);
            _loggerMock.Verify(x => x.LogWarning(
                It.Is<string>(s => s.Contains("Target Sharpe ratio below research benchmark"))), 
                Times.Once);
        }

        [Theory]
        [InlineData(MarketRegime.Crisis, 0.075f)]    // 50% reduction
        [InlineData(MarketRegime.Volatile, 0.105f)]  // 30% reduction
        [InlineData(MarketRegime.Bearish, 0.12f)]    // 20% reduction
        [InlineData(MarketRegime.Bullish, 0.18f)]    // 20% increase
        public void AdjustRiskBudgetForRegime_ReturnsCorrectAdjustment(MarketRegime regime, float expectedBudget)
        {
            // Arrange
            var marketContext = new MarketContext { MarketRegime = regime };
            
            // Act - using reflection to test private method
            var method = typeof(RAPMAlgorithm).GetMethod("AdjustRiskBudgetForRegime", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (float)method.Invoke(_algorithm, new object[] { marketContext });

            // Assert
            Assert.Equal(expectedBudget, result, 3);
        }

        // Helper methods for test setup

        private MarketContext CreateTestMarketContext()
        {
            return new MarketContext
            {
                MarketRegime = MarketRegime.Normal,
                MarketVolatility = 0.15f,
                MarketTrend = MarketTrend.Up,
                Timestamp = DateTime.UtcNow
            };
        }

        private List<RankedStock> CreateTestRankedStocks(List<string> symbols)
        {
            return symbols.Select((s, i) => new RankedStock
            {
                Symbol = s,
                Rank = i + 1,
                Score = new RankingScore
                {
                    Symbol = s,
                    CompositeScore = 0.8f - (i * 0.1f),
                    Confidence = 0.85f
                }
            }).ToList();
        }

        private ExpectedReturns CreateTestExpectedReturns(List<string> symbols)
        {
            var returns = new ExpectedReturns
            {
                Symbols = symbols,
                Returns = new Dictionary<string, float>(),
                Confidence = new Dictionary<string, float>(),
                Method = ReturnEstimationMethod.MachineLearning,
                Timestamp = DateTime.UtcNow
            };

            foreach (var symbol in symbols)
            {
                returns.Returns[symbol] = 0.08f + (float)(new Random().NextDouble() * 0.04);
                returns.Confidence[symbol] = 0.7f + (float)(new Random().NextDouble() * 0.2);
            }

            return returns;
        }

        private CovarianceMatrix CreateTestCovariance(int size)
        {
            var matrix = new float[size, size];
            var random = new Random(42);

            // Create positive semi-definite matrix
            for (int i = 0; i < size; i++)
            {
                matrix[i, i] = 0.04f; // Diagonal (variance)
                for (int j = i + 1; j < size; j++)
                {
                    matrix[i, j] = (float)(random.NextDouble() * 0.01);
                    matrix[j, i] = matrix[i, j]; // Symmetric
                }
            }

            return new CovarianceMatrix
            {
                Values = matrix,
                Method = CovarianceMethod.LedoitWolf,
                Timestamp = DateTime.UtcNow
            };
        }

        private OptimizationResult CreateTestOptimizationResult(List<string> symbols)
        {
            var weights = new Dictionary<string, float>();
            float remaining = 1.0f;

            foreach (var symbol in symbols)
            {
                var weight = remaining / (symbols.Count - weights.Count);
                weights[symbol] = weight;
                remaining -= weight;
            }

            return new OptimizationResult
            {
                Weights = weights,
                ExpectedReturn = 0.12f,
                ExpectedRisk = 0.08f,
                SharpeRatio = 1.75f,
                Method = ObjectiveFunction.MinimumCVaR
            };
        }

        private void SetupMocksForSuccessfulOptimization(
            List<RankedStock> rankedStocks,
            ExpectedReturns expectedReturns,
            CovarianceMatrix covariance,
            OptimizationResult optimizationResult)
        {
            _rankingCalculatorMock
                .Setup(x => x.RankStocksAsync(
                    It.IsAny<IEnumerable<StockRankingData>>(),
                    It.IsAny<MarketContext>(),
                    It.IsAny<RankingOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult<List<RankedStock>>.Success(rankedStocks));

            _optimizationEngineMock
                .Setup(x => x.CalculateExpectedReturnsAsync(
                    It.IsAny<List<string>>(),
                    It.IsAny<ReturnEstimationMethod>(),
                    It.IsAny<MarketContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult<ExpectedReturns>.Success(expectedReturns));

            _optimizationEngineMock
                .Setup(x => x.EstimateCovarianceMatrix(
                    It.IsAny<Dictionary<string, float[]>>(),
                    It.IsAny<CovarianceMethod>()))
                .Returns(TradingResult<float[,]>.Success(covariance.Values));

            _optimizationEngineMock
                .Setup(x => x.OptimizePortfolioAsync(
                    It.IsAny<ExpectedReturns>(),
                    It.IsAny<CovarianceMatrix>(),
                    It.IsAny<OptimizationConstraints>(),
                    It.IsAny<OptimizationMethod>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(TradingResult<OptimizationResult>.Success(optimizationResult));

            _performanceMonitorMock
                .Setup(x => x.TrackOptimizationAsync(
                    It.IsAny<string>(),
                    It.IsAny<float>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Setup risk measures calculations
            _riskMeasuresMock
                .Setup(x => x.CalculateVaR(It.IsAny<float[]>(), It.IsAny<float>(), It.IsAny<VaRMethod>()))
                .Returns(TradingResult<float>.Success(0.05f));

            _riskMeasuresMock
                .Setup(x => x.CalculateCVaR(It.IsAny<float[]>(), It.IsAny<float>(), It.IsAny<VaRMethod>()))
                .Returns(TradingResult<float>.Success(0.08f));

            _riskMeasuresMock
                .Setup(x => x.CalculateMaxDrawdown(It.IsAny<float[]>()))
                .Returns(TradingResult<float>.Success(0.15f));

            _riskMeasuresMock
                .Setup(x => x.CalculateConcentrationRisk(It.IsAny<float[]>()))
                .Returns(TradingResult<float>.Success(0.2f));
        }

        private void SetupMocksForCrisisScenario()
        {
            // Setup basic mocks
            SetupMocksForSuccessfulOptimization(
                CreateTestRankedStocks(new List<string> { "AAPL", "GOOGL" }),
                CreateTestExpectedReturns(new List<string> { "AAPL", "GOOGL" }),
                CreateTestCovariance(2),
                CreateTestOptimizationResult(new List<string> { "AAPL", "GOOGL" }));
        }

        private void VerifyLoggingCalls()
        {
            // Verify method entry/exit logging
            _loggerMock.Verify(x => x.LogMethodEntry(It.IsAny<string>()), Times.AtLeastOnce);
            _loggerMock.Verify(x => x.LogMethodExit(It.IsAny<string>()), Times.AtLeastOnce);
            
            // Verify info logging
            _loggerMock.Verify(x => x.LogInfo(It.IsAny<string>()), Times.AtLeastOnce);
            
            // Verify debug logging for detailed information
            _loggerMock.Verify(x => x.LogDebug(It.IsAny<string>()), Times.AtLeastOnce);
        }
    }

    /// <summary>
    /// Tests for RiskMeasures class
    /// </summary>
    public class RiskMeasuresTests
    {
        private readonly Mock<ICanonicalLogger> _loggerMock;
        private readonly RiskMeasures _riskMeasures;

        public RiskMeasuresTests()
        {
            _loggerMock = new Mock<ICanonicalLogger>();
            SetupLoggerMock();
            _riskMeasures = new RiskMeasures(_loggerMock.Object);
        }

        private void SetupLoggerMock()
        {
            _loggerMock.Setup(x => x.LogMethodEntry(It.IsAny<string>()));
            _loggerMock.Setup(x => x.LogMethodExit(It.IsAny<string>()));
            _loggerMock.Setup(x => x.LogInfo(It.IsAny<string>()));
            _loggerMock.Setup(x => x.LogDebug(It.IsAny<string>()));
            _loggerMock.Setup(x => x.LogWarning(It.IsAny<string>()));
            _loggerMock.Setup(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()));
        }

        [Fact]
        public void CalculateVaR_WithValidReturns_ReturnsCorrectValue()
        {
            // Arrange
            var returns = GenerateNormalReturns(1000, 0, 0.02f);

            // Act
            var result = _riskMeasures.CalculateVaR(returns, 0.95f, VaRMethod.Historical);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.Data > 0);
            Assert.True(result.Data < 0.1f); // Reasonable VaR for 2% volatility
            
            // Verify logging
            _loggerMock.Verify(x => x.LogDebug(It.IsAny<string>()), Times.AtLeastOnce);
            _loggerMock.Verify(x => x.LogInfo(It.IsAny<string>()), Times.AtLeastOnce);
        }

        [Fact]
        public void CalculateVaR_WithNullReturns_ReturnsFailure()
        {
            // Act
            var result = _riskMeasures.CalculateVaR(null, 0.95f);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("null or empty", result.ErrorMessage);
            
            // Verify warning was logged
            _loggerMock.Verify(x => x.LogWarning(It.IsAny<string>()), Times.Once);
        }

        [Theory]
        [InlineData(0.0f)]
        [InlineData(1.0f)]
        [InlineData(-0.1f)]
        [InlineData(1.1f)]
        public void CalculateVaR_WithInvalidConfidenceLevel_ReturnsFailure(float confidence)
        {
            // Arrange
            var returns = new float[] { 0.01f, -0.02f, 0.03f };

            // Act
            var result = _riskMeasures.CalculateVaR(returns, confidence);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("between 0 and 1", result.ErrorMessage);
        }

        [Fact]
        public void CalculateCVaR_ShouldBeGreaterThanVaR()
        {
            // Arrange
            var returns = GenerateNormalReturns(1000, 0, 0.02f);

            // Act
            var varResult = _riskMeasures.CalculateVaR(returns, 0.95f);
            var cvarResult = _riskMeasures.CalculateCVaR(returns, 0.95f);

            // Assert
            Assert.True(varResult.IsSuccess);
            Assert.True(cvarResult.IsSuccess);
            Assert.True(cvarResult.Data >= varResult.Data); // CVaR >= VaR always
        }

        [Fact]
        public void CalculateMaxDrawdown_WithDecreasingPrices_ReturnsCorrectValue()
        {
            // Arrange
            var prices = new float[] { 100, 110, 105, 90, 95, 85, 90, 100 };

            // Act
            var result = _riskMeasures.CalculateMaxDrawdown(prices);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(0.227f, result.Data, 3); // (110-85)/110 = 0.227
        }

        [Fact]
        public async Task RunStressTestAsync_WithMultipleScenarios_IdentifiesWorstCase()
        {
            // Arrange
            var portfolioWeights = new float[] { 0.5f, 0.3f, 0.2f };
            var assetReturns = new float[,] { { 0.01f, 0.02f, -0.01f }, { -0.02f, 0.01f, 0.03f } };
            var scenarios = new List<StressScenario>
            {
                new StressScenario { Name = "Market Crash", MarketShock = -0.20f },
                new StressScenario { Name = "Tech Bubble", AssetShocks = new[] { -0.30f, -0.10f, -0.05f } },
                new StressScenario { Name = "Minor Correction", MarketShock = -0.05f }
            };

            // Act
            var result = await _riskMeasures.RunStressTestAsync(portfolioWeights, assetReturns, scenarios);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("Tech Bubble", result.Data.WorstScenario);
            Assert.True(result.Data.WorstCaseLoss > result.Data.AverageStressLoss);
            
            // Verify detailed logging for each scenario
            _loggerMock.Verify(x => x.LogDebug(It.Is<string>(s => s.Contains("Evaluating scenario"))), 
                Times.Exactly(scenarios.Count));
        }

        private float[] GenerateNormalReturns(int count, float mean, float stdDev)
        {
            var returns = new float[count];
            var random = new Random(42);

            for (int i = 0; i < count; i++)
            {
                // Box-Muller transform
                double u1 = 1.0 - random.NextDouble();
                double u2 = 1.0 - random.NextDouble();
                double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                returns[i] = mean + stdDev * (float)normal;
            }

            return returns;
        }
    }
}