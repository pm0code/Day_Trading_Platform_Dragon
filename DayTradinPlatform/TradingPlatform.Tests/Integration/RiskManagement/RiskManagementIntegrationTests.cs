using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Common;
using TradingPlatform.Foundation.Models;
using TradingPlatform.RiskManagement.Services;
using TradingPlatform.RiskManagement.Models;
using TradingPlatform.PaperTrading.Services;
using TradingPlatform.ML.Algorithms.RAPM;
using TradingPlatform.ML.Common;
using TradingPlatform.Tests.Core.Canonical;
using Moq;

namespace TradingPlatform.Tests.Integration.RiskManagement
{
    /// <summary>
    /// Integration tests for Risk Management components
    /// Tests risk calculator, position monitor, and position sizing working together
    /// </summary>
    public class RiskManagementIntegrationTests : IntegrationTestBase
    {
        private RiskCalculatorCanonical _riskCalculator;
        private PositionMonitorCanonical _positionMonitor;
        private PositionSizingService _positionSizingService;
        private IPortfolioManager _portfolioManager;
        private Mock<IMarketDataService> _mockMarketDataService;
        
        public RiskManagementIntegrationTests(ITestOutputHelper output) : base(output)
        {
            _mockMarketDataService = new Mock<IMarketDataService>();
        }
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            // Register risk management services
            services.AddSingleton<RiskCalculatorCanonical>();
            services.AddSingleton<PositionMonitorCanonical>();
            services.AddSingleton<PositionSizingService>();
            services.AddSingleton<RiskMeasures>();
            
            // Register paper trading services for portfolio
            services.AddPaperTradingServices();
            
            // Register mocks
            services.AddSingleton(MockLogger.Object);
            services.AddSingleton(_mockMarketDataService.Object);
            services.AddSingleton(Mock.Of<IServiceProvider>());
            services.AddSingleton(Mock.Of<IRiskLimitService>());
        }
        
        protected override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            
            _riskCalculator = ServiceProvider.GetRequiredService<RiskCalculatorCanonical>();
            _positionMonitor = ServiceProvider.GetRequiredService<PositionMonitorCanonical>();
            _positionSizingService = ServiceProvider.GetRequiredService<PositionSizingService>();
            _portfolioManager = ServiceProvider.GetRequiredService<IPortfolioManager>();
            
            // Initialize services
            await _riskCalculator.InitializeAsync();
            await _positionMonitor.InitializeAsync();
            await _positionSizingService.InitializeAsync();
            await _portfolioManager.InitializePortfolioAsync(250000m); // $250k portfolio
        }
        
        #region Portfolio Risk Assessment Integration
        
        [Fact]
        public async Task PortfolioRisk_WithMultiplePositions_CalculatesCorrectly()
        {
            // Arrange
            Output.WriteLine("=== Testing Portfolio Risk Calculation ===");
            
            // Create test portfolio with positions
            await SetupTestPortfolio();
            
            var portfolio = await _portfolioManager.GetPortfolioAsync();
            var returns = GenerateHistoricalReturns(30); // 30 days of returns
            var portfolioValues = GeneratePortfolioValues(250000m, returns);
            
            // Act
            var riskContext = new RiskCalculationContext
            {
                Returns = returns,
                PortfolioValues = portfolioValues,
                ConfidenceLevel = 0.95m,
                RiskFreeRate = 0.02m
            };
            
            var riskAssessment = await _riskCalculator.EvaluateRiskAsync(riskContext);
            
            // Assert
            Assert.True(riskAssessment.IsSuccess);
            Assert.NotNull(riskAssessment.Value);
            
            var metrics = riskAssessment.Value.Metrics;
            Assert.Contains("VaR", metrics.Keys);
            Assert.Contains("ExpectedShortfall", metrics.Keys);
            Assert.Contains("MaxDrawdown", metrics.Keys);
            Assert.Contains("SharpeRatio", metrics.Keys);
            
            Output.WriteLine($"Portfolio Risk Metrics:");
            Output.WriteLine($"  VaR (95%): {metrics["VaR"]:P2}");
            Output.WriteLine($"  Expected Shortfall: {metrics["ExpectedShortfall"]:P2}");
            Output.WriteLine($"  Max Drawdown: {metrics["MaxDrawdown"]:P2}");
            Output.WriteLine($"  Sharpe Ratio: {metrics["SharpeRatio"]:F2}");
            Output.WriteLine($"  Risk Score: {riskAssessment.Value.RiskScore:F2}");
            Output.WriteLine($"  Assessment: {(riskAssessment.Value.IsAcceptable ? "ACCEPTABLE" : "HIGH RISK")}");
        }
        
        [Fact]
        public async Task PositionMonitoring_RealTimeRiskTracking_UpdatesCorrectly()
        {
            // Arrange
            await SetupTestPortfolio();
            await _positionMonitor.StartAsync();
            
            var positions = await _portfolioManager.GetPositionsAsync();
            var monitoringTasks = new List<Task>();
            
            // Act - Monitor positions and simulate price changes
            foreach (var position in positions)
            {
                var task = _positionMonitor.MonitorPositionAsync(position);
                monitoringTasks.Add(task);
                
                // Simulate price movement
                var currentPrice = position.CurrentPrice;
                var newPrice = currentPrice * (1m + (decimal)(Random.Shared.NextDouble() * 0.06 - 0.03)); // ±3%
                
                await _portfolioManager.UpdatePositionPriceAsync(position.Symbol, newPrice);
                
                Output.WriteLine($"{position.Symbol}: {currentPrice:C} -> {newPrice:C} ({(newPrice/currentPrice - 1):P2})");
            }
            
            // Get position risks
            var positionRisks = await _positionMonitor.GetPositionRisksAsync();
            
            // Assert
            Assert.NotEmpty(positionRisks);
            
            Output.WriteLine("\nPosition Risk Analysis:");
            foreach (var risk in positionRisks)
            {
                Output.WriteLine($"  {risk.Symbol}:");
                Output.WriteLine($"    Volatility: {risk.Volatility:P2}");
                Output.WriteLine($"    Beta: {risk.Beta:F2}");
                Output.WriteLine($"    Risk Score: {risk.RiskScore:F2}");
                Output.WriteLine($"    Status: {(risk.RequiresAction ? "ACTION REQUIRED" : "OK")}");
            }
        }
        
        #endregion
        
        #region Position Sizing Integration
        
        [Fact]
        public async Task PositionSizing_KellyWithRiskLimits_SizesCorrectly()
        {
            // Arrange
            Output.WriteLine("=== Testing Kelly Position Sizing with Risk Limits ===");
            
            var portfolio = await _portfolioManager.GetPortfolioAsync();
            var availableCapital = portfolio.CashBalance;
            
            // Setup Kelly parameters for multiple assets
            var assetParameters = new Dictionary<string, KellyParameters>
            {
                ["AAPL"] = new KellyParameters 
                { 
                    WinProbability = 0.65m, 
                    WinLossRatio = 1.8m,
                    UncertaintyDiscount = 0.20m
                },
                ["MSFT"] = new KellyParameters 
                { 
                    WinProbability = 0.60m, 
                    WinLossRatio = 2.0m,
                    UncertaintyDiscount = 0.25m
                },
                ["GOOGL"] = new KellyParameters 
                { 
                    WinProbability = 0.58m, 
                    WinLossRatio = 1.5m,
                    UncertaintyDiscount = 0.30m
                }
            };
            
            // Act - Calculate Kelly positions
            var kellyResult = await _positionSizingService.CalculateKellyPositionsAsync(
                assetParameters, availableCapital);
            
            // Verify with risk limits
            var proposedPositions = kellyResult.Data;
            var validatedPositions = new Dictionary<string, decimal>();
            
            foreach (var kvp in proposedPositions)
            {
                // Check if position passes risk limits
                var positionRisk = await CalculatePositionRisk(kvp.Key, kvp.Value);
                
                if (positionRisk.VaR < 0.05m) // Max 5% VaR per position
                {
                    validatedPositions[kvp.Key] = kvp.Value;
                }
                else
                {
                    // Reduce position size
                    validatedPositions[kvp.Key] = kvp.Value * 0.5m;
                    Output.WriteLine($"  {kvp.Key}: Position reduced due to high risk");
                }
            }
            
            // Assert
            Assert.True(kellyResult.IsSuccess);
            Assert.NotEmpty(validatedPositions);
            
            var totalAllocation = validatedPositions.Values.Sum();
            Assert.True(totalAllocation <= availableCapital);
            
            Output.WriteLine($"Available Capital: {availableCapital:C}");
            Output.WriteLine($"Kelly Position Sizes (risk-adjusted):");
            foreach (var kvp in validatedPositions)
            {
                Output.WriteLine($"  {kvp.Key}: ${kvp.Value:N2} ({kvp.Value/availableCapital:P2})");
            }
            Output.WriteLine($"Total Allocation: ${totalAllocation:N2} ({totalAllocation/availableCapital:P2})");
        }
        
        [Fact]
        public async Task PositionSizing_RiskParity_BalancesRiskContributions()
        {
            // Arrange
            Output.WriteLine("=== Testing Risk Parity Position Sizing ===");
            
            var availableCapital = 200000m;
            
            // Setup risk metrics for assets
            var assetRisks = new Dictionary<string, RiskMetrics>
            {
                ["SPY"] = new RiskMetrics { Volatility = 0.12m },   // Low vol ETF
                ["QQQ"] = new RiskMetrics { Volatility = 0.18m },   // Tech ETF
                ["IWM"] = new RiskMetrics { Volatility = 0.22m },   // Small cap
                ["GLD"] = new RiskMetrics { Volatility = 0.15m },   // Gold
                ["TLT"] = new RiskMetrics { Volatility = 0.14m }    // Bonds
            };
            
            // Act - Calculate risk parity positions
            var riskParityResult = await _positionSizingService.CalculateRiskParityPositionsAsync(
                assetRisks, availableCapital);
            
            // Calculate risk contributions
            var riskContributions = new Dictionary<string, decimal>();
            foreach (var kvp in riskParityResult.Data)
            {
                var weight = kvp.Value / availableCapital;
                var contribution = weight * assetRisks[kvp.Key].Volatility;
                riskContributions[kvp.Key] = contribution;
            }
            
            // Assert
            Assert.True(riskParityResult.IsSuccess);
            
            // Risk contributions should be roughly equal
            var avgContribution = riskContributions.Values.Average();
            foreach (var contribution in riskContributions.Values)
            {
                var deviation = Math.Abs(contribution - avgContribution) / avgContribution;
                Assert.True(deviation < 0.20m, "Risk contributions should be within 20% of average");
            }
            
            Output.WriteLine("Risk Parity Allocation:");
            foreach (var kvp in riskParityResult.Data)
            {
                var weight = kvp.Value / availableCapital;
                Output.WriteLine($"  {kvp.Key}: ${kvp.Value:N2} ({weight:P2}) - " +
                               $"Vol: {assetRisks[kvp.Key].Volatility:P1}, " +
                               $"Risk Contribution: {riskContributions[kvp.Key]:P2}");
            }
        }
        
        #endregion
        
        #region Dynamic Risk Management
        
        [Fact]
        public async Task DynamicRiskManagement_MarketStress_AdjustsPositions()
        {
            // Arrange
            Output.WriteLine("=== Testing Dynamic Risk Management Under Stress ===");
            
            await SetupTestPortfolio();
            var initialPositions = await _portfolioManager.GetPositionsAsync();
            var initialValue = await _portfolioManager.GetTotalPortfolioValueAsync();
            
            // Simulate normal market conditions
            var normalReturns = GenerateHistoricalReturns(20, 0.0003m, 0.015m);
            var normalRisk = await CalculatePortfolioRisk(normalReturns);
            
            Output.WriteLine($"Normal Market Conditions:");
            Output.WriteLine($"  VaR: {normalRisk.VaR:P2}");
            Output.WriteLine($"  Volatility: {normalRisk.Volatility:P2}");
            
            // Act - Simulate market stress
            var stressReturns = GenerateHistoricalReturns(10, -0.002m, 0.035m); // Higher vol, negative drift
            var allReturns = normalReturns.Concat(stressReturns).ToList();
            var stressRisk = await CalculatePortfolioRisk(allReturns);
            
            Output.WriteLine($"\nStress Market Conditions:");
            Output.WriteLine($"  VaR: {stressRisk.VaR:P2}");
            Output.WriteLine($"  Volatility: {stressRisk.Volatility:P2}");
            
            // Risk management should reduce positions
            var shouldReducePositions = stressRisk.VaR > normalRisk.VaR * 1.5m;
            
            if (shouldReducePositions)
            {
                Output.WriteLine("\nReducing positions due to increased risk...");
                
                // Calculate reduction factors
                var reductionFactor = normalRisk.VaR / stressRisk.VaR;
                
                foreach (var position in initialPositions)
                {
                    var newQuantity = Math.Floor(position.Quantity * reductionFactor);
                    var reduction = position.Quantity - newQuantity;
                    
                    if (reduction > 0)
                    {
                        Output.WriteLine($"  {position.Symbol}: Reduce by {reduction} shares ({reduction/position.Quantity:P0})");
                    }
                }
            }
            
            // Assert
            Assert.True(stressRisk.VaR > normalRisk.VaR);
            Assert.True(shouldReducePositions);
        }
        
        [Fact]
        public async Task CorrelationBreakdown_Detection_TriggersRebalancing()
        {
            // Arrange
            Output.WriteLine("=== Testing Correlation Breakdown Detection ===");
            
            // Create diversified portfolio
            var positions = new[]
            {
                ("SPY", 100m), ("QQQ", 75m), ("IWM", 150m), 
                ("GLD", 200m), ("TLT", 100m), ("XLE", 125m)
            };
            
            foreach (var (symbol, quantity) in positions)
            {
                // Simulate buying positions
                await SimulatePositionEntry(symbol, quantity, 100m);
            }
            
            // Normal correlations
            var normalCorrelations = new decimal[,]
            {
                { 1.0m, 0.8m, 0.7m, -0.2m, -0.3m, 0.6m },  // SPY
                { 0.8m, 1.0m, 0.6m, -0.1m, -0.2m, 0.5m },  // QQQ
                { 0.7m, 0.6m, 1.0m, -0.1m, -0.2m, 0.7m },  // IWM
                { -0.2m, -0.1m, -0.1m, 1.0m, 0.3m, -0.1m }, // GLD
                { -0.3m, -0.2m, -0.2m, 0.3m, 1.0m, -0.2m }, // TLT
                { 0.6m, 0.5m, 0.7m, -0.1m, -0.2m, 1.0m }   // XLE
            };
            
            // Stressed correlations (everything becomes highly correlated)
            var stressedCorrelations = new decimal[,]
            {
                { 1.0m, 0.95m, 0.93m, 0.85m, 0.80m, 0.92m },
                { 0.95m, 1.0m, 0.94m, 0.83m, 0.78m, 0.91m },
                { 0.93m, 0.94m, 1.0m, 0.82m, 0.77m, 0.93m },
                { 0.85m, 0.83m, 0.82m, 1.0m, 0.88m, 0.84m },
                { 0.80m, 0.78m, 0.77m, 0.88m, 1.0m, 0.79m },
                { 0.92m, 0.91m, 0.93m, 0.84m, 0.79m, 1.0m }
            };
            
            // Act - Detect correlation breakdown
            var correlationBreakdown = DetectCorrelationBreakdown(normalCorrelations, stressedCorrelations);
            
            // Calculate new position sizes with updated correlations
            if (correlationBreakdown)
            {
                Output.WriteLine("Correlation breakdown detected! Rebalancing required.");
                
                var volatilities = new Dictionary<string, decimal>
                {
                    ["SPY"] = 0.15m, ["QQQ"] = 0.20m, ["IWM"] = 0.25m,
                    ["GLD"] = 0.18m, ["TLT"] = 0.12m, ["XLE"] = 0.30m
                };
                
                var ercResult = await _positionSizingService.CalculateERCPositionsAsync(
                    stressedCorrelations, volatilities, 200000m);
                
                Output.WriteLine("\nRecommended ERC positions:");
                foreach (var kvp in ercResult.Data)
                {
                    Output.WriteLine($"  {kvp.Key}: ${kvp.Value:N2}");
                }
            }
            
            // Assert
            Assert.True(correlationBreakdown);
        }
        
        #endregion
        
        #region Helper Methods
        
        private async Task SetupTestPortfolio()
        {
            var positions = new[]
            {
                ("AAPL", 100m, 175m),
                ("MSFT", 50m, 350m),
                ("GOOGL", 25m, 140m),
                ("TSLA", 40m, 250m)
            };
            
            foreach (var (symbol, quantity, price) in positions)
            {
                await SimulatePositionEntry(symbol, quantity, price);
            }
        }
        
        private async Task SimulatePositionEntry(string symbol, decimal quantity, decimal price)
        {
            // This is simplified - in real integration test would use execution engine
            var position = new Position
            {
                Symbol = symbol,
                Quantity = quantity,
                AverageCost = price,
                CurrentPrice = price
            };
            
            // Update portfolio (simplified)
            await _portfolioManager.UpdatePositionPriceAsync(symbol, price);
        }
        
        private List<decimal> GenerateHistoricalReturns(int days, decimal meanReturn = 0.0002m, decimal volatility = 0.02m)
        {
            var returns = new List<decimal>();
            var random = new Random(42);
            
            for (int i = 0; i < days; i++)
            {
                var normalRandom = (decimal)(Math.Sqrt(-2.0 * Math.Log(random.NextDouble())) * 
                                           Math.Cos(2.0 * Math.PI * random.NextDouble()));
                var dailyReturn = meanReturn + volatility * normalRandom;
                returns.Add(dailyReturn);
            }
            
            return returns;
        }
        
        private List<decimal> GeneratePortfolioValues(decimal startValue, List<decimal> returns)
        {
            var values = new List<decimal> { startValue };
            
            foreach (var ret in returns)
            {
                values.Add(values.Last() * (1 + ret));
            }
            
            return values;
        }
        
        private async Task<PortfolioRiskMetrics> CalculatePortfolioRisk(List<decimal> returns)
        {
            var values = GeneratePortfolioValues(250000m, returns);
            
            var context = new RiskCalculationContext
            {
                Returns = returns,
                PortfolioValues = values,
                ConfidenceLevel = 0.95m,
                RiskFreeRate = 0.02m
            };
            
            var assessment = await _riskCalculator.EvaluateRiskAsync(context);
            
            return new PortfolioRiskMetrics
            {
                VaR = (decimal)assessment.Value.Metrics["VaR"],
                ExpectedShortfall = (decimal)assessment.Value.Metrics["ExpectedShortfall"],
                Volatility = CalculateVolatility(returns),
                SharpeRatio = (decimal)assessment.Value.Metrics["SharpeRatio"]
            };
        }
        
        private async Task<PositionRiskMetrics> CalculatePositionRisk(string symbol, decimal positionSize)
        {
            // Simplified position risk calculation
            var volatility = 0.20m + (decimal)(Random.Shared.NextDouble() * 0.15); // 20-35% vol
            var var95 = volatility * 1.645m * (decimal)Math.Sqrt(1.0 / 252.0); // Daily VaR
            
            return new PositionRiskMetrics
            {
                Symbol = symbol,
                VaR = var95,
                Volatility = volatility
            };
        }
        
        private decimal CalculateVolatility(List<decimal> returns)
        {
            if (returns.Count < 2) return 0;
            
            var mean = returns.Average();
            var sumSquaredDeviations = returns.Sum(r => (r - mean) * (r - mean));
            var variance = sumSquaredDeviations / (returns.Count - 1);
            
            return (decimal)Math.Sqrt((double)variance) * (decimal)Math.Sqrt(252); // Annualized
        }
        
        private bool DetectCorrelationBreakdown(decimal[,] normal, decimal[,] current)
        {
            var n = normal.GetLength(0);
            var significantChanges = 0;
            
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var change = Math.Abs(current[i, j] - normal[i, j]);
                    if (change > 0.30m) // 30% change threshold
                    {
                        significantChanges++;
                    }
                }
            }
            
            var totalPairs = n * (n - 1) / 2;
            return significantChanges > totalPairs * 0.3; // 30% of pairs changed significantly
        }
        
        #endregion
    }
    
    // Helper classes
    public class PortfolioRiskMetrics
    {
        public decimal VaR { get; set; }
        public decimal ExpectedShortfall { get; set; }
        public decimal Volatility { get; set; }
        public decimal SharpeRatio { get; set; }
    }
    
    public class PositionRiskMetrics
    {
        public string Symbol { get; set; }
        public decimal VaR { get; set; }
        public decimal Volatility { get; set; }
    }
    
    public class Position
    {
        public string Symbol { get; set; }
        public decimal Quantity { get; set; }
        public decimal AverageCost { get; set; }
        public decimal CurrentPrice { get; set; }
    }
}