using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.ML.Algorithms.SARI;
using TradingPlatform.ML.Common;
using TradingPlatform.Tests.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Common;
using Moq;

namespace TradingPlatform.Tests.Unit.ML
{
    /// <summary>
    /// Comprehensive unit tests for SARICalculator
    /// Tests Systemic Asset Risk Indicator calculations and stress scenario processing
    /// </summary>
    public class SARICalculatorTests : CanonicalTestBase<SARICalculator>
    {
        private const decimal PRECISION_TOLERANCE = 0.0001m;
        private readonly Mock<StressScenarioLibrary> _mockScenarioLibrary;
        private readonly Mock<StressPropagationEngine> _mockPropagationEngine;
        private readonly Mock<IMarketDataService> _mockMarketDataService;
        
        public SARICalculatorTests(ITestOutputHelper output) : base(output)
        {
            _mockScenarioLibrary = new Mock<StressScenarioLibrary>();
            _mockPropagationEngine = new Mock<StressPropagationEngine>();
            _mockMarketDataService = new Mock<IMarketDataService>();
        }
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(MockLogger.Object);
            services.AddSingleton(_mockScenarioLibrary.Object);
            services.AddSingleton(_mockPropagationEngine.Object);
            services.AddSingleton(_mockMarketDataService.Object);
        }
        
        protected override SARICalculator CreateSystemUnderTest()
        {
            return new SARICalculator(
                _mockScenarioLibrary.Object,
                _mockPropagationEngine.Object,
                _mockMarketDataService.Object,
                MockLogger.Object);
        }
        
        #region SARI Calculation Tests
        
        [Fact]
        public async Task CalculateSARIAsync_StandardPortfolio_CalculatesRiskIndex()
        {
            // Arrange
            var portfolio = CreateTestPortfolio();
            var marketContext = new MarketContext
            {
                MarketRegime = MarketRegime.Normal,
                VolatilityLevel = 0.15m,
                LiquidityScore = 0.8m,
                MacroEnvironment = MacroEnvironment.Stable
            };
            
            var scenarios = CreateTestScenarios();
            _mockScenarioLibrary.Setup(x => x.GetScenariosForRegime(It.IsAny<MarketRegime>()))
                .Returns(TradingResult<List<StressScenario>>.Success(scenarios));
            
            SetupPropagationEngine();
            
            // Act
            var result = await SystemUnderTest.CalculateSARIAsync(portfolio, marketContext);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.True(result.Data.CompositeRiskScore > 0);
            Assert.True(result.Data.CompositeRiskScore <= 1.0f); // Normalized 0-1
            
            Output.WriteLine($"SARI Score: {result.Data.CompositeRiskScore:F4}");
            Output.WriteLine($"Confidence: {result.Data.Confidence:F4}");
            Output.WriteLine($"Scenarios processed: {result.Data.ScenarioResults.Count}");
        }
        
        [Fact]
        public async Task CalculateSARIAsync_StressedMarket_ReturnsHigherRisk()
        {
            // Arrange
            var portfolio = CreateTestPortfolio();
            
            var normalContext = new MarketContext
            {
                MarketRegime = MarketRegime.Normal,
                VolatilityLevel = 0.15m,
                LiquidityScore = 0.9m
            };
            
            var stressedContext = new MarketContext
            {
                MarketRegime = MarketRegime.Crisis,
                VolatilityLevel = 0.45m,
                LiquidityScore = 0.3m
            };
            
            SetupScenariosForRegimes();
            SetupPropagationEngine();
            
            // Act
            var normalResult = await SystemUnderTest.CalculateSARIAsync(portfolio, normalContext);
            var stressedResult = await SystemUnderTest.CalculateSARIAsync(portfolio, stressedContext);
            
            // Assert
            Assert.True(normalResult.IsSuccess);
            Assert.True(stressedResult.IsSuccess);
            Assert.True(stressedResult.Data.CompositeRiskScore > normalResult.Data.CompositeRiskScore);
            
            Output.WriteLine($"Normal SARI: {normalResult.Data.CompositeRiskScore:F4}");
            Output.WriteLine($"Stressed SARI: {stressedResult.Data.CompositeRiskScore:F4}");
        }
        
        [Fact]
        public async Task CalculateSARIAsync_MultipleTimeHorizons_CalculatesCorrectly()
        {
            // Arrange
            var portfolio = CreateTestPortfolio();
            var marketContext = CreateMarketContext();
            var parameters = new SARIParameters
            {
                TimeHorizons = new[] { TimeHorizon.Day, TimeHorizon.Week, TimeHorizon.Month }
            };
            
            SetupCompleteScenarios();
            
            // Act
            var result = await SystemUnderTest.CalculateSARIAsync(portfolio, marketContext, parameters);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Data.TimeHorizonResults.Count);
            Assert.Contains(TimeHorizon.Day, result.Data.TimeHorizonResults.Keys);
            Assert.Contains(TimeHorizon.Week, result.Data.TimeHorizonResults.Keys);
            Assert.Contains(TimeHorizon.Month, result.Data.TimeHorizonResults.Keys);
            
            // Longer horizons typically have higher risk
            Assert.True(result.Data.TimeHorizonResults[TimeHorizon.Month] >= 
                       result.Data.TimeHorizonResults[TimeHorizon.Day]);
        }
        
        [Fact]
        public async Task CalculateSARIAsync_ConcentratedPortfolio_HigherRisk()
        {
            // Arrange
            var diversifiedPortfolio = CreateDiversifiedPortfolio(10);
            var concentratedPortfolio = CreateConcentratedPortfolio(2);
            var marketContext = CreateMarketContext();
            
            SetupScenariosForRegimes();
            SetupPropagationEngine();
            
            // Act
            var diversifiedResult = await SystemUnderTest.CalculateSARIAsync(
                diversifiedPortfolio, marketContext);
            var concentratedResult = await SystemUnderTest.CalculateSARIAsync(
                concentratedPortfolio, marketContext);
            
            // Assert
            Assert.True(diversifiedResult.IsSuccess);
            Assert.True(concentratedResult.IsSuccess);
            
            // Concentrated portfolio should show higher risk
            Assert.True(concentratedResult.Data.ConcentrationRisk > 
                       diversifiedResult.Data.ConcentrationRisk);
            
            Output.WriteLine($"Diversified concentration risk: {diversifiedResult.Data.ConcentrationRisk:F4}");
            Output.WriteLine($"Concentrated concentration risk: {concentratedResult.Data.ConcentrationRisk:F4}");
        }
        
        #endregion
        
        #region Scenario Weight Tests
        
        [Fact]
        public async Task UpdateScenarioWeightsAsync_MarketConditions_AdjustsWeights()
        {
            // Arrange
            var portfolio = CreateTestPortfolio();
            var volatileMarket = new MarketContext
            {
                MarketRegime = MarketRegime.HighVolatility,
                VolatilityLevel = 0.35m,
                CorrelationBreakdown = true
            };
            
            var scenarios = new List<StressScenario>
            {
                CreateScenario("MarketCrash", ScenarioType.MarketCrash),
                CreateScenario("LiquidityCrisis", ScenarioType.LiquidityCrisis),
                CreateScenario("SectorRotation", ScenarioType.SectorRotation)
            };
            
            _mockScenarioLibrary.Setup(x => x.GetScenariosForRegime(It.IsAny<MarketRegime>()))
                .Returns(TradingResult<List<StressScenario>>.Success(scenarios));
            
            SetupPropagationEngine();
            
            // Act
            var result = await SystemUnderTest.CalculateSARIAsync(portfolio, volatileMarket);
            
            // Assert
            Assert.True(result.IsSuccess);
            
            // In volatile markets, market crash scenarios should have higher weight
            var marketCrashResult = result.Data.ScenarioResults
                .FirstOrDefault(r => r.ScenarioName == "MarketCrash");
            Assert.NotNull(marketCrashResult);
            Assert.True(marketCrashResult.Weight > 0.3f); // Higher weight in volatile conditions
        }
        
        #endregion
        
        #region Component Risk Tests
        
        [Fact]
        public async Task CalculateSARIAsync_ComponentRisks_CalculatedCorrectly()
        {
            // Arrange
            var portfolio = CreateTestPortfolio();
            var marketContext = CreateMarketContext();
            
            SetupCompleteScenarios();
            
            // Act
            var result = await SystemUnderTest.CalculateSARIAsync(portfolio, marketContext);
            
            // Assert
            Assert.True(result.IsSuccess);
            
            // Verify all component risks are calculated
            Assert.True(result.Data.MarketRisk >= 0 && result.Data.MarketRisk <= 1);
            Assert.True(result.Data.LiquidityRisk >= 0 && result.Data.LiquidityRisk <= 1);
            Assert.True(result.Data.ConcentrationRisk >= 0 && result.Data.ConcentrationRisk <= 1);
            Assert.True(result.Data.CorrelationRisk >= 0 && result.Data.CorrelationRisk <= 1);
            Assert.True(result.Data.TailRisk >= 0 && result.Data.TailRisk <= 1);
            
            Output.WriteLine("Component Risks:");
            Output.WriteLine($"  Market Risk: {result.Data.MarketRisk:F4}");
            Output.WriteLine($"  Liquidity Risk: {result.Data.LiquidityRisk:F4}");
            Output.WriteLine($"  Concentration Risk: {result.Data.ConcentrationRisk:F4}");
            Output.WriteLine($"  Correlation Risk: {result.Data.CorrelationRisk:F4}");
            Output.WriteLine($"  Tail Risk: {result.Data.TailRisk:F4}");
        }
        
        [Fact]
        public async Task CalculateSARIAsync_EmptyPortfolio_ReturnsMinimalRisk()
        {
            // Arrange
            var emptyPortfolio = new Portfolio
            {
                Name = "Empty",
                CashBalance = 100000m,
                Holdings = new Dictionary<string, PortfolioHolding>()
            };
            
            var marketContext = CreateMarketContext();
            SetupScenariosForRegimes();
            
            // Act
            var result = await SystemUnderTest.CalculateSARIAsync(emptyPortfolio, marketContext);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.Data.CompositeRiskScore < 0.1f); // Very low risk for cash
            Assert.Equal(0f, result.Data.ConcentrationRisk); // No concentration in empty portfolio
        }
        
        #endregion
        
        #region Performance and Edge Cases
        
        [Fact]
        public async Task CalculateSARIAsync_LargePortfolio_HandlesEfficiently()
        {
            // Arrange - Portfolio with 100 positions
            var largePortfolio = CreateDiversifiedPortfolio(100);
            var marketContext = CreateMarketContext();
            
            SetupScenariosForRegimes();
            SetupPropagationEngine();
            
            // Act & Assert - Should complete within reasonable time
            await AssertCompletesWithinAsync(500, async () =>
            {
                var result = await SystemUnderTest.CalculateSARIAsync(largePortfolio, marketContext);
                Assert.True(result.IsSuccess);
                Assert.True(result.Data.ProcessingTimeMs < 500);
            });
        }
        
        [Fact]
        public async Task CalculateSARIAsync_NoScenariosAvailable_ReturnsFailure()
        {
            // Arrange
            var portfolio = CreateTestPortfolio();
            var marketContext = CreateMarketContext();
            
            _mockScenarioLibrary.Setup(x => x.GetScenariosForRegime(It.IsAny<MarketRegime>()))
                .Returns(TradingResult<List<StressScenario>>.Failure("No scenarios available"));
            
            // Act
            var result = await SystemUnderTest.CalculateSARIAsync(portfolio, marketContext);
            
            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("No scenarios available", result.ErrorMessage);
        }
        
        #endregion
        
        #region Helper Methods
        
        private Portfolio CreateTestPortfolio()
        {
            return new Portfolio
            {
                Name = "Test Portfolio",
                CashBalance = 20000m,
                Holdings = new Dictionary<string, PortfolioHolding>
                {
                    ["AAPL"] = new PortfolioHolding
                    {
                        Symbol = "AAPL",
                        Quantity = 100,
                        AveragePrice = 150m,
                        CurrentPrice = 175m,
                        Sector = "Technology"
                    },
                    ["JPM"] = new PortfolioHolding
                    {
                        Symbol = "JPM",
                        Quantity = 200,
                        AveragePrice = 140m,
                        CurrentPrice = 145m,
                        Sector = "Financials"
                    },
                    ["XOM"] = new PortfolioHolding
                    {
                        Symbol = "XOM",
                        Quantity = 150,
                        AveragePrice = 80m,
                        CurrentPrice = 85m,
                        Sector = "Energy"
                    }
                }
            };
        }
        
        private Portfolio CreateDiversifiedPortfolio(int positionCount)
        {
            var portfolio = new Portfolio
            {
                Name = "Diversified Portfolio",
                CashBalance = 50000m,
                Holdings = new Dictionary<string, PortfolioHolding>()
            };
            
            var sectors = new[] { "Technology", "Financials", "Healthcare", "Energy", "Consumer" };
            
            for (int i = 0; i < positionCount; i++)
            {
                var symbol = $"STOCK{i:D3}";
                portfolio.Holdings[symbol] = new PortfolioHolding
                {
                    Symbol = symbol,
                    Quantity = 100,
                    AveragePrice = 50m + i,
                    CurrentPrice = 50m + i + (decimal)(new Random(i).NextDouble() * 10),
                    Sector = sectors[i % sectors.Length]
                };
            }
            
            return portfolio;
        }
        
        private Portfolio CreateConcentratedPortfolio(int positionCount)
        {
            var portfolio = new Portfolio
            {
                Name = "Concentrated Portfolio",
                CashBalance = 10000m,
                Holdings = new Dictionary<string, PortfolioHolding>()
            };
            
            for (int i = 0; i < positionCount; i++)
            {
                var symbol = $"CONC{i}";
                portfolio.Holdings[symbol] = new PortfolioHolding
                {
                    Symbol = symbol,
                    Quantity = 1000 / positionCount, // Concentrate capital
                    AveragePrice = 100m,
                    CurrentPrice = 105m,
                    Sector = "Technology" // All in same sector
                };
            }
            
            return portfolio;
        }
        
        private MarketContext CreateMarketContext()
        {
            return new MarketContext
            {
                MarketRegime = MarketRegime.Normal,
                VolatilityLevel = 0.20m,
                LiquidityScore = 0.75m,
                MacroEnvironment = MacroEnvironment.Stable,
                InterestRateEnvironment = InterestRateEnvironment.Rising,
                CorrelationBreakdown = false
            };
        }
        
        private List<StressScenario> CreateTestScenarios()
        {
            return new List<StressScenario>
            {
                CreateScenario("Market Crash", ScenarioType.MarketCrash, -0.20f),
                CreateScenario("Liquidity Crisis", ScenarioType.LiquidityCrisis, -0.15f),
                CreateScenario("Interest Rate Shock", ScenarioType.InterestRateShock, -0.10f)
            };
        }
        
        private StressScenario CreateScenario(string name, ScenarioType type, float impact = -0.10f)
        {
            return new StressScenario
            {
                Name = name,
                Type = type,
                Severity = ScenarioSeverity.Moderate,
                MarketImpact = impact,
                Probability = 0.05f,
                Description = $"Test scenario: {name}"
            };
        }
        
        private void SetupScenariosForRegimes()
        {
            _mockScenarioLibrary.Setup(x => x.GetScenariosForRegime(MarketRegime.Normal))
                .Returns(TradingResult<List<StressScenario>>.Success(new List<StressScenario>
                {
                    CreateScenario("Mild Correction", ScenarioType.MarketCorrection, -0.05f)
                }));
                
            _mockScenarioLibrary.Setup(x => x.GetScenariosForRegime(MarketRegime.Crisis))
                .Returns(TradingResult<List<StressScenario>>.Success(new List<StressScenario>
                {
                    CreateScenario("Severe Crash", ScenarioType.MarketCrash, -0.30f),
                    CreateScenario("Systemic Crisis", ScenarioType.SystemicCrisis, -0.40f)
                }));
                
            _mockScenarioLibrary.Setup(x => x.GetScenariosForRegime(MarketRegime.HighVolatility))
                .Returns(TradingResult<List<StressScenario>>.Success(CreateTestScenarios()));
        }
        
        private void SetupCompleteScenarios()
        {
            var scenarios = new List<StressScenario>
            {
                CreateScenario("Market Crash", ScenarioType.MarketCrash, -0.25f),
                CreateScenario("Liquidity Crisis", ScenarioType.LiquidityCrisis, -0.20f),
                CreateScenario("Sector Rotation", ScenarioType.SectorRotation, -0.10f),
                CreateScenario("Interest Rate Shock", ScenarioType.InterestRateShock, -0.15f),
                CreateScenario("Currency Crisis", ScenarioType.CurrencyCrisis, -0.12f)
            };
            
            _mockScenarioLibrary.Setup(x => x.GetScenariosForRegime(It.IsAny<MarketRegime>()))
                .Returns(TradingResult<List<StressScenario>>.Success(scenarios));
        }
        
        private void SetupPropagationEngine()
        {
            _mockPropagationEngine.Setup(x => x.PropagateStressAsync(
                It.IsAny<StressScenario>(),
                It.IsAny<Portfolio>(),
                It.IsAny<MarketContext>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync((StressScenario scenario, Portfolio portfolio, MarketContext context, CancellationToken ct) =>
                {
                    // Simulate stress propagation results
                    return new StressPropagationResult
                    {
                        ScenarioName = scenario.Name,
                        PortfolioImpact = scenario.MarketImpact * 0.8f, // 80% of market impact
                        IndividualImpacts = portfolio.Holdings.ToDictionary(
                            h => h.Key,
                            h => scenario.MarketImpact * (1f + (float)(new Random().NextDouble() * 0.2 - 0.1)))
                    };
                });
        }
        
        #endregion
    }
    
    // Mock classes for testing
    public class StressScenarioLibrary
    {
        public virtual TradingResult<List<StressScenario>> GetScenariosForRegime(MarketRegime regime)
        {
            return TradingResult<List<StressScenario>>.Success(new List<StressScenario>());
        }
    }
    
    public class StressPropagationEngine
    {
        public virtual Task<StressPropagationResult> PropagateStressAsync(
            StressScenario scenario,
            Portfolio portfolio,
            MarketContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new StressPropagationResult());
        }
    }
    
    public class MarketContext
    {
        public MarketRegime MarketRegime { get; set; }
        public decimal VolatilityLevel { get; set; }
        public decimal LiquidityScore { get; set; }
        public MacroEnvironment MacroEnvironment { get; set; }
        public InterestRateEnvironment InterestRateEnvironment { get; set; }
        public bool CorrelationBreakdown { get; set; }
    }
    
    public class SARIParameters
    {
        public TimeHorizon[] TimeHorizons { get; set; } = new[] { TimeHorizon.Day };
        public float ConfidenceLevel { get; set; } = 0.95f;
        public bool IncludeCorrelationBreakdown { get; set; } = true;
    }
    
    public class SARIResult
    {
        public DateTime Timestamp { get; set; }
        public float CompositeRiskScore { get; set; }
        public float Confidence { get; set; }
        public float MarketRisk { get; set; }
        public float LiquidityRisk { get; set; }
        public float ConcentrationRisk { get; set; }
        public float CorrelationRisk { get; set; }
        public float TailRisk { get; set; }
        public MarketRegime MarketRegime { get; set; }
        public List<ScenarioResult> ScenarioResults { get; set; }
        public Dictionary<TimeHorizon, float> TimeHorizonResults { get; set; }
        public long ProcessingTimeMs { get; set; }
    }
    
    public class ScenarioResult
    {
        public string ScenarioName { get; set; }
        public float Impact { get; set; }
        public float Weight { get; set; }
    }
    
    public class StressScenario
    {
        public string Name { get; set; }
        public ScenarioType Type { get; set; }
        public ScenarioSeverity Severity { get; set; }
        public float MarketImpact { get; set; }
        public float Probability { get; set; }
        public string Description { get; set; }
    }
    
    public class StressPropagationResult
    {
        public string ScenarioName { get; set; }
        public float PortfolioImpact { get; set; }
        public Dictionary<string, float> IndividualImpacts { get; set; }
    }
    
    public enum MarketRegime
    {
        Normal,
        HighVolatility,
        Crisis,
        Recovery
    }
    
    public enum MacroEnvironment
    {
        Stable,
        Growing,
        Contracting,
        Uncertain
    }
    
    public enum InterestRateEnvironment
    {
        Stable,
        Rising,
        Falling,
        Volatile
    }
    
    public enum TimeHorizon
    {
        Day,
        Week,
        Month,
        Quarter,
        Year
    }
    
    public enum ScenarioType
    {
        MarketCrash,
        MarketCorrection,
        LiquidityCrisis,
        SystemicCrisis,
        SectorRotation,
        InterestRateShock,
        CurrencyCrisis
    }
    
    public enum ScenarioSeverity
    {
        Mild,
        Moderate,
        Severe,
        Extreme
    }
}