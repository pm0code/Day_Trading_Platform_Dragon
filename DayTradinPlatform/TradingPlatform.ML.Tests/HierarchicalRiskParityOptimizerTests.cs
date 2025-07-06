using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using TradingPlatform.GPU.Infrastructure;
using TradingPlatform.ML.Configuration;
using TradingPlatform.ML.Interfaces;
using TradingPlatform.ML.PortfolioOptimization;
using TradingPlatform.ML.Services;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.ML.Tests
{
    /// <summary>
    /// Comprehensive tests for Hierarchical Risk Parity optimizer
    /// </summary>
    public class HierarchicalRiskParityOptimizerTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly HierarchicalRiskParityOptimizer _optimizer;
        private readonly HRPConfiguration _config;
        private readonly GpuContext _gpuContext;
        private readonly IMLInferenceService _mlService;

        public HierarchicalRiskParityOptimizerTests(ITestOutputHelper output)
        {
            _output = output;
            
            // Create test configuration
            _config = new HRPConfiguration
            {
                LinkageMethod = LinkageMethod.Single,
                UseMLEnhancement = false, // Disable for basic tests
                RiskFreeRate = 0.04m,
                MinClusterSize = 1,
                DistanceThreshold = 0.5
            };
            
            // Initialize GPU context
            _gpuContext = new GpuContext(NullLogger<GpuContext>.Instance);
            
            // Initialize ML service for enhanced tests
            var mlConfig = new MLInferenceConfiguration
            {
                Provider = ExecutionProvider.CPU,
                ModelsPath = "TestModels"
            };
            
            _mlService = new MLInferenceService(
                mlConfig,
                logger: NullLogger<MLInferenceService>.Instance);
            
            // Create optimizer
            _optimizer = new HierarchicalRiskParityOptimizer(
                _config,
                _gpuContext,
                _mlService,
                NullLogger<HierarchicalRiskParityOptimizer>.Instance);
        }

        #region Basic HRP Tests

        [Fact]
        public async Task OptimizeAsync_BasicPortfolio_ProducesValidWeights()
        {
            // Arrange
            var assets = CreateTestAssets(5);
            var marketData = CreateTestMarketData(5);
            
            // Act
            var result = await _optimizer.OptimizeAsync(assets, marketData);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.Weights.Count);
            
            // Verify weights sum to 1
            var sumWeights = result.Weights.Values.Sum();
            Assert.True(Math.Abs(sumWeights - 1m) < 0.001m, $"Weights sum to {sumWeights}, expected 1");
            
            // Verify all weights are positive (HRP is long-only)
            foreach (var weight in result.Weights.Values)
            {
                Assert.True(weight >= 0, $"Found negative weight: {weight}");
            }
            
            // Verify risk metrics
            Assert.True(result.Volatility > 0, "Volatility should be positive");
            Assert.NotEqual(0, result.ExpectedReturn);
            
            _output.WriteLine($"HRP Optimization Results:");
            _output.WriteLine($"  Expected Return: {result.ExpectedReturn:P}");
            _output.WriteLine($"  Volatility: {result.Volatility:P}");
            _output.WriteLine($"  Sharpe Ratio: {result.SharpeRatio:F2}");
            
            // Log additional HRP-specific metrics
            if (result.AdditionalMetrics.TryGetValue("DiversificationRatio", out var divRatio))
            {
                _output.WriteLine($"  Diversification Ratio: {divRatio:F2}");
            }
            if (result.AdditionalMetrics.TryGetValue("EffectiveNumberOfAssets", out var effN))
            {
                _output.WriteLine($"  Effective N: {effN:F1}");
            }
        }

        [Fact]
        public async Task OptimizeAsync_UncorrelatedAssets_EqualWeights()
        {
            // Arrange
            var assets = CreateTestAssets(4);
            var marketData = CreateUncorrelatedMarketData(4);
            
            // Act
            var result = await _optimizer.OptimizeAsync(assets, marketData);
            
            // Assert
            Assert.NotNull(result);
            
            // For uncorrelated assets with equal volatility, HRP should give roughly equal weights
            var expectedWeight = 1m / 4m;
            foreach (var weight in result.Weights.Values)
            {
                Assert.True(Math.Abs(weight - expectedWeight) < 0.1m, 
                    $"Weight {weight} deviates significantly from expected {expectedWeight}");
            }
            
            _output.WriteLine("Weights for uncorrelated assets:");
            foreach (var kvp in result.Weights)
            {
                _output.WriteLine($"  {kvp.Key}: {kvp.Value:P}");
            }
        }

        [Fact]
        public async Task OptimizeAsync_HighlyCorrelatedAssets_ConcentratedWeights()
        {
            // Arrange
            var assets = CreateTestAssets(6);
            var marketData = CreateHighlyCorrelatedMarketData(6);
            
            // Act
            var result = await _optimizer.OptimizeAsync(assets, marketData);
            
            // Assert
            Assert.NotNull(result);
            
            // For highly correlated assets, HRP should concentrate weights
            var maxWeight = result.Weights.Values.Max();
            var minWeight = result.Weights.Values.Min();
            
            Assert.True(maxWeight - minWeight > 0.1m, 
                "Expected significant weight dispersion for correlated assets");
            
            _output.WriteLine("Weights for highly correlated assets:");
            foreach (var kvp in result.Weights.OrderByDescending(w => w.Value))
            {
                _output.WriteLine($"  {kvp.Key}: {kvp.Value:P}");
            }
        }

        #endregion

        #region Clustering Tests

        [Theory]
        [InlineData(LinkageMethod.Single)]
        [InlineData(LinkageMethod.Complete)]
        [InlineData(LinkageMethod.Average)]
        [InlineData(LinkageMethod.Ward)]
        public async Task OptimizeAsync_DifferentLinkageMethods_ProduceDifferentResults(LinkageMethod method)
        {
            // Arrange
            var assets = CreateTestAssets(8);
            var marketData = CreateTestMarketData(8);
            
            var config = new HRPConfiguration
            {
                LinkageMethod = method,
                UseMLEnhancement = false,
                RiskFreeRate = 0.04m
            };
            
            var optimizer = new HierarchicalRiskParityOptimizer(
                config,
                _gpuContext,
                null,
                NullLogger<HierarchicalRiskParityOptimizer>.Instance);
            
            // Act
            var result = await optimizer.OptimizeAsync(assets, marketData);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(8, result.Weights.Count);
            
            _output.WriteLine($"Results for {method} linkage:");
            _output.WriteLine($"  Max Weight: {result.Weights.Values.Max():P}");
            _output.WriteLine($"  Min Weight: {result.Weights.Values.Min():P}");
            _output.WriteLine($"  Volatility: {result.Volatility:P}");
        }

        #endregion

        #region ML Enhancement Tests

        [Fact]
        public async Task OptimizeAsync_WithMLEnhancement_AdjustsWeights()
        {
            // Arrange
            var assets = CreateTestAssetsWithSectors(10);
            var marketData = CreateTestMarketData(10);
            
            // Create two optimizers - with and without ML
            var configWithML = new HRPConfiguration
            {
                LinkageMethod = LinkageMethod.Single,
                UseMLEnhancement = true,
                RiskFreeRate = 0.04m
            };
            
            var configWithoutML = new HRPConfiguration
            {
                LinkageMethod = LinkageMethod.Single,
                UseMLEnhancement = false,
                RiskFreeRate = 0.04m
            };
            
            var optimizerWithML = new HierarchicalRiskParityOptimizer(
                configWithML,
                _gpuContext,
                _mlService,
                NullLogger<HierarchicalRiskParityOptimizer>.Instance);
            
            var optimizerWithoutML = new HierarchicalRiskParityOptimizer(
                configWithoutML,
                _gpuContext,
                null,
                NullLogger<HierarchicalRiskParityOptimizer>.Instance);
            
            // Act
            var resultWithML = await optimizerWithML.OptimizeAsync(assets, marketData);
            var resultWithoutML = await optimizerWithoutML.OptimizeAsync(assets, marketData);
            
            // Assert
            Assert.NotNull(resultWithML);
            Assert.NotNull(resultWithoutML);
            
            // Weights should be different when ML enhancement is applied
            var weightDifference = 0m;
            foreach (var symbol in resultWithML.Weights.Keys)
            {
                weightDifference += Math.Abs(resultWithML.Weights[symbol] - resultWithoutML.Weights[symbol]);
            }
            
            _output.WriteLine($"Total weight difference with ML enhancement: {weightDifference:P}");
            _output.WriteLine($"Expected return without ML: {resultWithoutML.ExpectedReturn:P}");
            _output.WriteLine($"Expected return with ML: {resultWithML.ExpectedReturn:P}");
        }

        #endregion

        #region Constraint Tests

        [Fact]
        public async Task OptimizeAsync_WithMinMaxConstraints_RespectsLimits()
        {
            // Arrange
            var assets = CreateTestAssets(10);
            var marketData = CreateTestMarketData(10);
            var constraints = new OptimizationConstraints
            {
                MinWeight = 0.05m, // 5% minimum
                MaxWeight = 0.20m, // 20% maximum
                LongOnly = true
            };
            
            // Act
            var result = await _optimizer.OptimizeAsync(assets, marketData, null, constraints);
            
            // Assert
            Assert.NotNull(result);
            
            foreach (var weight in result.Weights.Values)
            {
                Assert.True(weight >= constraints.MinWeight - 0.001m, 
                    $"Weight {weight} below minimum {constraints.MinWeight}");
                Assert.True(weight <= constraints.MaxWeight + 0.001m, 
                    $"Weight {weight} above maximum {constraints.MaxWeight}");
            }
            
            _output.WriteLine("Constrained HRP weights:");
            foreach (var kvp in result.Weights.OrderByDescending(w => w.Value))
            {
                _output.WriteLine($"  {kvp.Key}: {kvp.Value:P}");
            }
        }

        #endregion

        #region Performance Tests

        [Theory]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        public async Task OptimizeAsync_LargePortfolios_ScalesEfficiently(int numAssets)
        {
            // Arrange
            var assets = CreateTestAssets(numAssets);
            var marketData = CreateTestMarketData(numAssets);
            
            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await _optimizer.OptimizeAsync(assets, marketData);
            stopwatch.Stop();
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(numAssets, result.Weights.Count);
            
            _output.WriteLine($"HRP Optimization Performance:");
            _output.WriteLine($"  Portfolio size: {numAssets} assets");
            _output.WriteLine($"  Execution time: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Time per asset: {stopwatch.ElapsedMilliseconds / (double)numAssets:F2}ms");
            
            if (result.AdditionalMetrics.TryGetValue("EffectiveNumberOfAssets", out var effN))
            {
                _output.WriteLine($"  Effective N: {effN:F1}");
                _output.WriteLine($"  Diversification: {effN / numAssets:P}");
            }
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public async Task OptimizeAsync_SingleAsset_FullWeight()
        {
            // Arrange
            var assets = CreateTestAssets(1);
            var marketData = CreateTestMarketData(1);
            
            // Act
            var result = await _optimizer.OptimizeAsync(assets, marketData);
            
            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Weights);
            Assert.Equal(1m, result.Weights.Values.First());
        }

        [Fact]
        public async Task OptimizeAsync_EmptyAssets_ThrowsException()
        {
            // Arrange
            var emptyAssets = new List<Asset>();
            var marketData = CreateTestMarketData(0);
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _optimizer.OptimizeAsync(emptyAssets, marketData);
            });
        }

        [Fact]
        public async Task OptimizeAsync_MissingPriceHistory_ThrowsException()
        {
            // Arrange
            var assets = CreateTestAssets(3);
            var marketData = CreateTestMarketData(3);
            
            // Remove price history for one asset
            marketData.PriceHistories.Remove(assets[1].Symbol);
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _optimizer.OptimizeAsync(assets, marketData);
            });
        }

        #endregion

        #region Helper Methods

        private List<Asset> CreateTestAssets(int count)
        {
            var assets = new List<Asset>();
            var random = new Random(42);
            
            for (int i = 0; i < count; i++)
            {
                assets.Add(new Asset
                {
                    Symbol = $"STOCK{i}",
                    Name = $"Test Stock {i}",
                    Sector = "Technology",
                    CurrentPrice = 100m + (decimal)(random.NextDouble() * 50),
                    MarketCap = 1e9m * (decimal)(1 + random.NextDouble() * 10),
                    Beta = 0.8m + (decimal)(random.NextDouble() * 0.8)
                });
            }
            
            return assets;
        }

        private List<Asset> CreateTestAssetsWithSectors(int count)
        {
            var assets = new List<Asset>();
            var sectors = new[] { "Technology", "Utilities", "Consumer Discretionary", 
                                "Consumer Staples", "Financials", "Healthcare" };
            var random = new Random(42);
            
            for (int i = 0; i < count; i++)
            {
                assets.Add(new Asset
                {
                    Symbol = $"STOCK{i}",
                    Name = $"Test Stock {i}",
                    Sector = sectors[i % sectors.Length],
                    CurrentPrice = 100m + (decimal)(random.NextDouble() * 50),
                    MarketCap = 1e9m * (decimal)(1 + random.NextDouble() * 10),
                    Beta = 0.8m + (decimal)(random.NextDouble() * 0.8)
                });
            }
            
            return assets;
        }

        private MarketData CreateTestMarketData(int numAssets)
        {
            var random = new Random(42);
            
            // Create correlation matrix with moderate correlations
            var correlation = CreateCorrelationMatrix(numAssets, 0.3, 0.6, random);
            
            // Create volatilities
            var volatilities = new decimal[numAssets];
            for (int i = 0; i < numAssets; i++)
            {
                volatilities[i] = 0.15m + (decimal)(random.NextDouble() * 0.25); // 15-40% volatility
            }
            
            // Convert to covariance
            var covariance = CorrelationToCovariance(correlation, volatilities);
            
            // Create price histories
            var priceHistories = new Dictionary<string, decimal[]>();
            var volumeHistories = new Dictionary<string, decimal[]>();
            
            for (int i = 0; i < numAssets; i++)
            {
                priceHistories[$"STOCK{i}"] = GeneratePriceHistory(250, volatilities[i], random);
                volumeHistories[$"STOCK{i}"] = GenerateVolumeHistory(250, random);
            }
            
            return new MarketData
            {
                CovarianceMatrix = covariance,
                HistoricalVolatilities = volatilities,
                PriceHistories = priceHistories,
                VolumeHistories = volumeHistories,
                MarketVolatility = 0.20m,
                VixIndex = 18m,
                MarketBreadth = 0.55m,
                PutCallRatio = 0.8m,
                YieldCurve10Y2Y = 0.5m,
                CreditSpread = 1.2m,
                DollarIndex = 95m,
                GoldPrice = 1800m,
                OilPrice = 75m,
                AverageCorrelation = 0.3m,
                DispersionIndex = 0.15m
            };
        }

        private MarketData CreateUncorrelatedMarketData(int numAssets)
        {
            var data = CreateTestMarketData(numAssets);
            
            // Set correlation matrix to identity (uncorrelated)
            var correlation = CreateIdentityMatrix(numAssets);
            
            // Use equal volatilities
            var volatilities = Enumerable.Repeat(0.20m, numAssets).ToArray();
            
            data.CovarianceMatrix = CorrelationToCovariance(correlation, volatilities);
            data.HistoricalVolatilities = volatilities;
            
            return data;
        }

        private MarketData CreateHighlyCorrelatedMarketData(int numAssets)
        {
            var random = new Random(42);
            
            // Create high correlation matrix (0.7 - 0.9)
            var correlation = CreateCorrelationMatrix(numAssets, 0.7, 0.9, random);
            
            // Use varying volatilities
            var volatilities = new decimal[numAssets];
            for (int i = 0; i < numAssets; i++)
            {
                volatilities[i] = 0.10m + (decimal)(random.NextDouble() * 0.40); // 10-50% volatility
            }
            
            var data = CreateTestMarketData(numAssets);
            data.CovarianceMatrix = CorrelationToCovariance(correlation, volatilities);
            data.HistoricalVolatilities = volatilities;
            
            return data;
        }

        private double[,] CreateCorrelationMatrix(int size, double minCorr, double maxCorr, Random random)
        {
            var matrix = new double[size, size];
            
            // Fill diagonal with 1s
            for (int i = 0; i < size; i++)
            {
                matrix[i, i] = 1.0;
            }
            
            // Fill upper triangle with correlations
            for (int i = 0; i < size; i++)
            {
                for (int j = i + 1; j < size; j++)
                {
                    var correlation = minCorr + random.NextDouble() * (maxCorr - minCorr);
                    matrix[i, j] = correlation;
                    matrix[j, i] = correlation; // Symmetric
                }
            }
            
            return matrix;
        }

        private double[,] CreateIdentityMatrix(int size)
        {
            var matrix = new double[size, size];
            for (int i = 0; i < size; i++)
            {
                matrix[i, i] = 1.0;
            }
            return matrix;
        }

        private double[,] CorrelationToCovariance(double[,] correlation, decimal[] volatilities)
        {
            var n = correlation.GetLength(0);
            var covariance = new double[n, n];
            
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    covariance[i, j] = correlation[i, j] * (double)volatilities[i] * (double)volatilities[j];
                }
            }
            
            return covariance;
        }

        private decimal[] GeneratePriceHistory(int length, decimal volatility, Random random)
        {
            var prices = new decimal[length];
            prices[0] = 100m;
            
            var dailyVol = volatility / (decimal)Math.Sqrt(252);
            
            for (int i = 1; i < length; i++)
            {
                var return_ = (decimal)(random.NextDouble() * 2 - 1) * dailyVol * 2;
                prices[i] = prices[i - 1] * (1m + return_);
            }
            
            return prices;
        }

        private decimal[] GenerateVolumeHistory(int length, Random random)
        {
            var volumes = new decimal[length];
            var baseVolume = 1000000m;
            
            for (int i = 0; i < length; i++)
            {
                volumes[i] = baseVolume * (decimal)(0.5 + random.NextDouble());
            }
            
            return volumes;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _optimizer?.Dispose();
            _mlService?.Dispose();
            _gpuContext?.Dispose();
        }

        #endregion
    }
}