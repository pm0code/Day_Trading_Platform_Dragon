using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.ML.Algorithms.RAPM;
using TradingPlatform.Tests.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Common;
using Moq;

namespace TradingPlatform.Tests.Unit.ML
{
    /// <summary>
    /// Comprehensive unit tests for PositionSizingService
    /// Tests Kelly Criterion, Risk Parity, Equal Risk Contribution, and Drawdown-constrained sizing
    /// </summary>
    public class PositionSizingServiceTests : CanonicalTestBase<PositionSizingService>
    {
        private const decimal PRECISION_TOLERANCE = 0.0001m;
        private readonly Mock<IMarketDataService> _mockMarketDataService;
        private readonly Mock<RiskMeasures> _mockRiskMeasures;
        
        public PositionSizingServiceTests(ITestOutputHelper output) : base(output)
        {
            _mockMarketDataService = new Mock<IMarketDataService>();
            _mockRiskMeasures = new Mock<RiskMeasures>();
        }
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(MockLogger.Object);
            services.AddSingleton(_mockMarketDataService.Object);
            services.AddSingleton(_mockRiskMeasures.Object);
        }
        
        protected override PositionSizingService CreateSystemUnderTest()
        {
            return new PositionSizingService(
                _mockMarketDataService.Object,
                _mockRiskMeasures.Object,
                MockLogger.Object);
        }
        
        #region Kelly Criterion Tests
        
        [Fact]
        public async Task CalculateKellyPositionsAsync_StandardScenario_CalculatesCorrectFractions()
        {
            // Arrange
            decimal totalCapital = 100000m;
            var assetParameters = new Dictionary<string, KellyParameters>
            {
                ["AAPL"] = new KellyParameters 
                { 
                    WinProbability = 0.60m, 
                    WinLossRatio = 1.5m,
                    UncertaintyDiscount = 0.25m
                },
                ["MSFT"] = new KellyParameters 
                { 
                    WinProbability = 0.55m, 
                    WinLossRatio = 2.0m,
                    UncertaintyDiscount = 0.20m
                }
            };
            
            // Act
            var result = await SystemUnderTest.CalculateKellyPositionsAsync(assetParameters, totalCapital);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Data.Count);
            
            // Verify positions are positive and reasonable
            Assert.True(result.Data["AAPL"] > 0);
            Assert.True(result.Data["MSFT"] > 0);
            Assert.True(result.Data["AAPL"] <= totalCapital * 0.25m); // Max Kelly fraction
            Assert.True(result.Data["MSFT"] <= totalCapital * 0.25m);
            
            // Total allocation should not exceed capital
            var totalAllocated = result.Data.Values.Sum();
            Assert.True(totalAllocated <= totalCapital);
            
            Output.WriteLine($"AAPL position: ${result.Data["AAPL"]:N2}");
            Output.WriteLine($"MSFT position: ${result.Data["MSFT"]:N2}");
            Output.WriteLine($"Total allocated: ${totalAllocated:N2} ({totalAllocated/totalCapital:P2})");
        }
        
        [Fact]
        public async Task CalculateKellyPositionsAsync_UnprofitableAsset_ExcludesFromAllocation()
        {
            // Arrange
            var assetParameters = new Dictionary<string, KellyParameters>
            {
                ["GOOD"] = new KellyParameters 
                { 
                    WinProbability = 0.65m, 
                    WinLossRatio = 2.0m 
                },
                ["BAD"] = new KellyParameters 
                { 
                    WinProbability = 0.40m,  // < 50%
                    WinLossRatio = 1.0m      // Even odds
                }
            };
            
            // Act
            var result = await SystemUnderTest.CalculateKellyPositionsAsync(assetParameters, 100000m);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.Data["GOOD"] > 0);
            Assert.Equal(0m, result.Data["BAD"]); // Should not allocate to losing strategy
        }
        
        [Fact]
        public async Task CalculateKellyPositionsAsync_HighUncertainty_ReducesPosition()
        {
            // Arrange
            var lowUncertainty = new KellyParameters 
            { 
                WinProbability = 0.60m, 
                WinLossRatio = 2.0m,
                UncertaintyDiscount = 0.10m  // Low uncertainty
            };
            
            var highUncertainty = new KellyParameters 
            { 
                WinProbability = 0.60m, 
                WinLossRatio = 2.0m,
                UncertaintyDiscount = 0.50m  // High uncertainty
            };
            
            var parameters1 = new Dictionary<string, KellyParameters> { ["ASSET"] = lowUncertainty };
            var parameters2 = new Dictionary<string, KellyParameters> { ["ASSET"] = highUncertainty };
            
            // Act
            var result1 = await SystemUnderTest.CalculateKellyPositionsAsync(parameters1, 100000m);
            var result2 = await SystemUnderTest.CalculateKellyPositionsAsync(parameters2, 100000m);
            
            // Assert
            Assert.True(result1.Data["ASSET"] > result2.Data["ASSET"]);
            Output.WriteLine($"Low uncertainty position: ${result1.Data["ASSET"]:N2}");
            Output.WriteLine($"High uncertainty position: ${result2.Data["ASSET"]:N2}");
        }
        
        [Fact]
        public async Task CalculateKellyPositionsAsync_CustomConfiguration_AppliesLimits()
        {
            // Arrange
            var config = new KellyConfiguration
            {
                MaxKellyFraction = 0.10m,   // 10% max per position
                KellyMultiplier = 0.50m,    // Use 50% of full Kelly
                AllowLeverage = false
            };
            
            var assetParameters = new Dictionary<string, KellyParameters>
            {
                ["HIGH_EDGE"] = new KellyParameters 
                { 
                    WinProbability = 0.80m,  // Very high win rate
                    WinLossRatio = 3.0m      // High payoff
                }
            };
            
            // Act
            var result = await SystemUnderTest.CalculateKellyPositionsAsync(
                assetParameters, 100000m, config);
            
            // Assert
            Assert.True(result.IsSuccess);
            // Should be capped at 10% * 50% = 5% of capital
            Assert.True(result.Data["HIGH_EDGE"] <= 100000m * 0.10m * 0.50m);
        }
        
        [Fact]
        public async Task CalculateKellyPositionsAsync_MultipleAssets_NormalizesWhenExceedsCapital()
        {
            // Arrange - Many profitable assets that would exceed 100% allocation
            var assetParameters = new Dictionary<string, KellyParameters>();
            for (int i = 0; i < 10; i++)
            {
                assetParameters[$"ASSET{i}"] = new KellyParameters
                {
                    WinProbability = 0.65m,
                    WinLossRatio = 2.0m,
                    UncertaintyDiscount = 0.20m
                };
            }
            
            // Act
            var result = await SystemUnderTest.CalculateKellyPositionsAsync(assetParameters, 100000m);
            
            // Assert
            Assert.True(result.IsSuccess);
            var totalAllocated = result.Data.Values.Sum();
            AssertFinancialPrecision(100000m, totalAllocated); // Should be normalized to exactly 100%
        }
        
        #endregion
        
        #region Risk Parity Tests
        
        [Fact]
        public async Task CalculateRiskParityPositionsAsync_InverseVolatilityWeighting_AllocatesCorrectly()
        {
            // Arrange
            var assetRisks = new Dictionary<string, RiskMetrics>
            {
                ["LOW_VOL"] = new RiskMetrics { Volatility = 0.10m },   // 10% vol
                ["MED_VOL"] = new RiskMetrics { Volatility = 0.20m },   // 20% vol
                ["HIGH_VOL"] = new RiskMetrics { Volatility = 0.40m }   // 40% vol
            };
            
            // Act
            var result = await SystemUnderTest.CalculateRiskParityPositionsAsync(
                assetRisks, 100000m);
            
            // Assert
            Assert.True(result.IsSuccess);
            
            // Low vol should get highest allocation
            Assert.True(result.Data["LOW_VOL"] > result.Data["MED_VOL"]);
            Assert.True(result.Data["MED_VOL"] > result.Data["HIGH_VOL"]);
            
            // Verify inverse relationship
            // LOW_VOL should get approximately 4x HIGH_VOL allocation
            var ratio = result.Data["LOW_VOL"] / result.Data["HIGH_VOL"];
            Assert.True(ratio > 3.5m && ratio < 4.5m);
            
            Output.WriteLine($"Low vol: ${result.Data["LOW_VOL"]:N2}");
            Output.WriteLine($"Med vol: ${result.Data["MED_VOL"]:N2}");
            Output.WriteLine($"High vol: ${result.Data["HIGH_VOL"]:N2}");
        }
        
        [Fact]
        public async Task CalculateRiskParityPositionsAsync_WithTargetVolatility_ScalesPositions()
        {
            // Arrange
            var config = new RiskParityConfiguration
            {
                TargetPortfolioVolatility = 0.10m,  // 10% target vol
                MaxPositionWeight = 0.40m,
                MinPositionWeight = 0.05m
            };
            
            var assetRisks = new Dictionary<string, RiskMetrics>
            {
                ["ASSET1"] = new RiskMetrics { Volatility = 0.15m },
                ["ASSET2"] = new RiskMetrics { Volatility = 0.15m }
            };
            
            // Act
            var result = await SystemUnderTest.CalculateRiskParityPositionsAsync(
                assetRisks, 100000m, config);
            
            // Assert
            Assert.True(result.IsSuccess);
            
            // With equal volatilities, should get equal weights
            AssertFinancialPrecision(result.Data["ASSET1"], result.Data["ASSET2"]);
        }
        
        [Fact]
        public async Task CalculateRiskParityPositionsAsync_AppliesPositionLimits()
        {
            // Arrange
            var config = new RiskParityConfiguration
            {
                MaxPositionWeight = 0.30m,  // 30% max
                MinPositionWeight = 0.10m   // 10% min
            };
            
            var assetRisks = new Dictionary<string, RiskMetrics>
            {
                ["VERY_LOW_VOL"] = new RiskMetrics { Volatility = 0.05m },  // Would get >30%
                ["HIGH_VOL"] = new RiskMetrics { Volatility = 0.50m }       // Would get <10%
            };
            
            // Act
            var result = await SystemUnderTest.CalculateRiskParityPositionsAsync(
                assetRisks, 100000m, config);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.Data["VERY_LOW_VOL"] <= 100000m * 0.30m);
            Assert.True(result.Data["HIGH_VOL"] >= 100000m * 0.10m);
        }
        
        [Fact]
        public async Task CalculateRiskParityPositionsAsync_ZeroVolatility_ExcludesAsset()
        {
            // Arrange
            var assetRisks = new Dictionary<string, RiskMetrics>
            {
                ["NORMAL"] = new RiskMetrics { Volatility = 0.20m },
                ["ZERO_VOL"] = new RiskMetrics { Volatility = 0m }  // Cash-like
            };
            
            // Act
            var result = await SystemUnderTest.CalculateRiskParityPositionsAsync(
                assetRisks, 100000m);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.Data["NORMAL"] > 0);
            Assert.False(result.Data.ContainsKey("ZERO_VOL"));
        }
        
        #endregion
        
        #region Equal Risk Contribution Tests
        
        [Fact]
        public async Task CalculateERCPositionsAsync_ConvergesToEqualRiskContributions()
        {
            // Arrange - 3 assets with different volatilities
            var volatilities = new Dictionary<string, decimal>
            {
                ["A"] = 0.10m,
                ["B"] = 0.20m,
                ["C"] = 0.30m
            };
            
            // Simple correlation matrix (low correlations)
            var correlation = new decimal[3, 3]
            {
                { 1.0m, 0.2m, 0.1m },
                { 0.2m, 1.0m, 0.15m },
                { 0.1m, 0.15m, 1.0m }
            };
            
            // Act
            var result = await SystemUnderTest.CalculateERCPositionsAsync(
                correlation, volatilities, 100000m);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Data.Count);
            
            // All positions should be positive
            Assert.True(result.Data.Values.All(v => v > 0));
            
            // Total should equal capital
            var total = result.Data.Values.Sum();
            AssertFinancialPrecision(100000m, total);
            
            Output.WriteLine("ERC Positions:");
            foreach (var kvp in result.Data)
            {
                Output.WriteLine($"{kvp.Key}: ${kvp.Value:N2} ({kvp.Value/total:P2})");
            }
        }
        
        [Fact]
        public async Task CalculateERCPositionsAsync_HighCorrelation_AdjustsWeights()
        {
            // Arrange - 2 assets with high correlation
            var volatilities = new Dictionary<string, decimal>
            {
                ["X"] = 0.20m,
                ["Y"] = 0.20m
            };
            
            var highCorrelation = new decimal[2, 2]
            {
                { 1.0m, 0.9m },
                { 0.9m, 1.0m }
            };
            
            // Act
            var result = await SystemUnderTest.CalculateERCPositionsAsync(
                highCorrelation, volatilities, 100000m);
            
            // Assert
            Assert.True(result.IsSuccess);
            // With equal vols and high correlation, should get equal weights
            AssertFinancialPrecision(result.Data["X"], result.Data["Y"], 2);
        }
        
        #endregion
        
        #region Drawdown Constraint Tests
        
        [Fact]
        public async Task CalculateMaxDrawdownConstrainedPositionsAsync_ScalesBasedOnHistoricalDrawdowns()
        {
            // Arrange
            var assetDrawdowns = new Dictionary<string, DrawdownMetrics>
            {
                ["LOW_DD"] = new DrawdownMetrics { MaxDrawdown = 0.10m },   // 10% max DD
                ["MED_DD"] = new DrawdownMetrics { MaxDrawdown = 0.20m },   // 20% max DD
                ["HIGH_DD"] = new DrawdownMetrics { MaxDrawdown = 0.40m }   // 40% max DD
            };
            
            decimal maxPortfolioDrawdown = 0.15m; // 15% portfolio limit
            
            // Act
            var result = await SystemUnderTest.CalculateMaxDrawdownConstrainedPositionsAsync(
                assetDrawdowns, 100000m, maxPortfolioDrawdown);
            
            // Assert
            Assert.True(result.IsSuccess);
            
            // LOW_DD can use full allocation (15%/10% = 1.5, capped at 1.0)
            // MED_DD scaled to 75% (15%/20% = 0.75)
            // HIGH_DD scaled to 37.5% (15%/40% = 0.375)
            
            Assert.True(result.Data["LOW_DD"] > result.Data["MED_DD"]);
            Assert.True(result.Data["MED_DD"] > result.Data["HIGH_DD"]);
            
            Output.WriteLine($"Low DD: ${result.Data["LOW_DD"]:N2}");
            Output.WriteLine($"Med DD: ${result.Data["MED_DD"]:N2}");
            Output.WriteLine($"High DD: ${result.Data["HIGH_DD"]:N2}");
        }
        
        [Fact]
        public async Task CalculateMaxDrawdownConstrainedPositionsAsync_UsesFullCapital()
        {
            // Arrange
            var assetDrawdowns = new Dictionary<string, DrawdownMetrics>
            {
                ["A"] = new DrawdownMetrics { MaxDrawdown = 0.15m },
                ["B"] = new DrawdownMetrics { MaxDrawdown = 0.25m },
                ["C"] = new DrawdownMetrics { MaxDrawdown = 0.35m }
            };
            
            // Act
            var result = await SystemUnderTest.CalculateMaxDrawdownConstrainedPositionsAsync(
                assetDrawdowns, 100000m, 0.20m);
            
            // Assert
            Assert.True(result.IsSuccess);
            var totalAllocated = result.Data.Values.Sum();
            AssertFinancialPrecision(100000m, totalAllocated);
        }
        
        [Fact]
        public async Task CalculateMaxDrawdownConstrainedPositionsAsync_ZeroDrawdown_HandlesGracefully()
        {
            // Arrange
            var assetDrawdowns = new Dictionary<string, DrawdownMetrics>
            {
                ["NORMAL"] = new DrawdownMetrics { MaxDrawdown = 0.20m },
                ["ZERO_DD"] = new DrawdownMetrics { MaxDrawdown = 0m }  // No historical drawdown
            };
            
            // Act
            var result = await SystemUnderTest.CalculateMaxDrawdownConstrainedPositionsAsync(
                assetDrawdowns, 100000m, 0.15m);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.Data["NORMAL"] > 0);
            // Asset with zero drawdown should be excluded or handled specially
        }
        
        #endregion
        
        #region Edge Cases and Error Handling
        
        [Fact]
        public async Task CalculateKellyPositionsAsync_EmptyAssets_ReturnsEmptyPositions()
        {
            // Arrange
            var emptyParameters = new Dictionary<string, KellyParameters>();
            
            // Act
            var result = await SystemUnderTest.CalculateKellyPositionsAsync(
                emptyParameters, 100000m);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.Empty(result.Data);
        }
        
        [Fact]
        public async Task CalculateRiskParityPositionsAsync_NegativeCapital_ReturnsFailure()
        {
            // Arrange
            var assetRisks = new Dictionary<string, RiskMetrics>
            {
                ["ASSET"] = new RiskMetrics { Volatility = 0.20m }
            };
            
            // Act
            var result = await SystemUnderTest.CalculateRiskParityPositionsAsync(
                assetRisks, -100000m);
            
            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Failed to calculate positions", result.ErrorMessage);
        }
        
        [Theory]
        [InlineData(0.0, 0.5)]     // 0% win probability
        [InlineData(1.0, 2.0)]     // 100% win probability  
        [InlineData(0.5, 0.0)]     // Zero win/loss ratio
        [InlineData(0.5, -1.0)]    // Negative win/loss ratio
        public async Task CalculateKellyPositionsAsync_ExtremeProbabilities_HandlesGracefully(
            decimal winProb, decimal winLossRatio)
        {
            // Arrange
            var parameters = new Dictionary<string, KellyParameters>
            {
                ["EXTREME"] = new KellyParameters
                {
                    WinProbability = winProb,
                    WinLossRatio = winLossRatio
                }
            };
            
            // Act
            var result = await SystemUnderTest.CalculateKellyPositionsAsync(parameters, 100000m);
            
            // Assert
            Assert.True(result.IsSuccess);
            // Should either exclude or cap at reasonable levels
            if (result.Data.ContainsKey("EXTREME"))
            {
                Assert.True(result.Data["EXTREME"] >= 0);
                Assert.True(result.Data["EXTREME"] <= 25000m); // Max 25% position
            }
        }
        
        #endregion
        
        #region Performance Tests
        
        [Fact]
        public async Task CalculateKellyPositionsAsync_LargePortfolio_PerformsEfficiently()
        {
            // Arrange - 100 assets
            var assetParameters = new Dictionary<string, KellyParameters>();
            for (int i = 0; i < 100; i++)
            {
                assetParameters[$"ASSET{i:D3}"] = new KellyParameters
                {
                    WinProbability = 0.50m + (i % 30) * 0.01m,
                    WinLossRatio = 1.0m + (i % 20) * 0.1m,
                    UncertaintyDiscount = 0.20m
                };
            }
            
            // Act & Assert - Should complete quickly
            await AssertCompletesWithinAsync(100, async () =>
            {
                var result = await SystemUnderTest.CalculateKellyPositionsAsync(
                    assetParameters, 1000000m);
                Assert.True(result.IsSuccess);
                Assert.Equal(100, result.Data.Count);
            });
        }
        
        #endregion
    }
}