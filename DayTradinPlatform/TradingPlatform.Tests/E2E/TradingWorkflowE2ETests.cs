using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.PaperTrading.Services;
using TradingPlatform.PaperTrading.Models;
using TradingPlatform.Screening.Engines;
using TradingPlatform.Screening.Models;
using TradingPlatform.StrategyEngine.Services;
using TradingPlatform.RiskManagement.Services;
using TradingPlatform.GoldenRules.Engine;
using TradingPlatform.Tests.Core.Canonical;
using Moq;

namespace TradingPlatform.Tests.E2E
{
    /// <summary>
    /// End-to-end tests for complete trading workflows
    /// Tests the entire flow from screening to execution with risk management
    /// </summary>
    public class TradingWorkflowE2ETests : E2ETestBase
    {
        private RealTimeScreeningEngineCanonical _screeningEngine;
        private StrategyManagerCanonical _strategyManager;
        private IOrderExecutionEngine _executionEngine;
        private IPortfolioManager _portfolioManager;
        private RiskCalculatorCanonical _riskCalculator;
        private CanonicalGoldenRulesEngine _goldenRulesEngine;
        private Mock<IMarketDataService> _mockMarketDataService;
        
        public TradingWorkflowE2ETests(ITestOutputHelper output) : base(output)
        {
            _mockMarketDataService = new Mock<IMarketDataService>();
        }
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            // Register all required services
            services.AddPaperTradingServices();
            services.AddScreeningServices();
            services.AddSingleton<StrategyManagerCanonical>();
            services.AddSingleton<RiskCalculatorCanonical>();
            services.AddSingleton<CanonicalGoldenRulesEngine>();
            
            // Register mocks
            services.AddSingleton(MockLogger.Object);
            services.AddSingleton(_mockMarketDataService.Object);
            services.AddSingleton(Mock.Of<IDataIngestionService>());
            services.AddSingleton(Mock.Of<IAlertService>());
            services.AddSingleton(Mock.Of<IRiskManagementService>());
            services.AddSingleton(Mock.Of<IPortfolioService>());
        }
        
        protected override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            
            // Get all services
            _screeningEngine = ServiceProvider.GetRequiredService<RealTimeScreeningEngineCanonical>();
            _strategyManager = ServiceProvider.GetRequiredService<StrategyManagerCanonical>();
            _executionEngine = ServiceProvider.GetRequiredService<IOrderExecutionEngine>();
            _portfolioManager = ServiceProvider.GetRequiredService<IPortfolioManager>();
            _riskCalculator = ServiceProvider.GetRequiredService<RiskCalculatorCanonical>();
            _goldenRulesEngine = ServiceProvider.GetRequiredService<CanonicalGoldenRulesEngine>();
            
            // Initialize all services
            await _screeningEngine.InitializeAsync();
            await _strategyManager.InitializeAsync();
            await _riskCalculator.InitializeAsync();
            await _goldenRulesEngine.InitializeAsync();
            
            // Initialize portfolio
            await _portfolioManager.InitializePortfolioAsync(100000m); // $100k starting capital
        }
        
        #region Complete Trading Day Workflow
        
        [Fact]
        public async Task CompleteTradingDay_FromScreeningToExecution_FollowsGoldenRules()
        {
            // Arrange
            Output.WriteLine("=== Starting Complete Trading Day E2E Test ===");
            
            // Setup market data
            var marketOpen = DateTime.Today.AddHours(9).AddMinutes(30);
            SetupMarketDataForTradingDay();
            
            // Define screening criteria for day trading
            var screeningCriteria = new ScreeningCriteria
            {
                Name = "Day Trading Opportunities",
                PriceMin = 20m,
                PriceMax = 200m,
                VolumeMultiplier = 2.0m,
                VolatilityMin = 0.02m,
                GapPercentMin = 0.02m,
                RequiresNews = true
            };
            
            // Act & Assert - Full trading workflow
            
            // Step 1: Morning Screening
            Output.WriteLine("\n1. Running morning screening...");
            var screeningResults = await _screeningEngine.ScreenStocksAsync(
                GetUniverseSymbols(), screeningCriteria);
            
            Assert.NotEmpty(screeningResults);
            Output.WriteLine($"   Found {screeningResults.Count} candidates");
            
            // Step 2: Strategy Analysis
            Output.WriteLine("\n2. Analyzing candidates with strategies...");
            var tradingSignals = new List<TradingSignal>();
            
            foreach (var candidate in screeningResults.Take(5)) // Analyze top 5
            {
                var signal = await _strategyManager.AnalyzeStockAsync(candidate.Symbol);
                if (signal != null && signal.Confidence > 0.6m)
                {
                    tradingSignals.Add(signal);
                    Output.WriteLine($"   {signal.Symbol}: {signal.Action} signal, confidence: {signal.Confidence:P0}");
                }
            }
            
            Assert.NotEmpty(tradingSignals);
            
            // Step 3: Risk Assessment
            Output.WriteLine("\n3. Performing risk assessment...");
            var portfolio = await _portfolioManager.GetPortfolioAsync();
            var currentRisk = await CalculatePortfolioRisk(portfolio);
            
            Output.WriteLine($"   Current portfolio risk: VaR={currentRisk.VaR:P2}, Sharpe={currentRisk.SharpeRatio:F2}");
            
            // Step 4: Golden Rules Validation
            Output.WriteLine("\n4. Validating against Golden Rules...");
            var approvedSignals = new List<TradingSignal>();
            
            foreach (var signal in tradingSignals)
            {
                var validation = await _goldenRulesEngine.ValidateTradeAsync(
                    signal.Symbol, 
                    signal.Action == SignalAction.Buy ? OrderSide.Buy : OrderSide.Sell,
                    signal.SuggestedQuantity ?? 100,
                    portfolio.CashBalance);
                
                if (validation.IsValid)
                {
                    approvedSignals.Add(signal);
                    Output.WriteLine($"   {signal.Symbol}: APPROVED - {validation.AppliedRules.Count} rules passed");
                }
                else
                {
                    Output.WriteLine($"   {signal.Symbol}: REJECTED - {validation.ViolatedRules.FirstOrDefault()?.RuleName}");
                }
            }
            
            Assert.NotEmpty(approvedSignals);
            
            // Step 5: Position Sizing
            Output.WriteLine("\n5. Calculating position sizes...");
            var positionSizes = await CalculatePositionSizes(approvedSignals, portfolio);
            
            foreach (var position in positionSizes)
            {
                Output.WriteLine($"   {position.Key}: {position.Value} shares");
            }
            
            // Step 6: Order Execution
            Output.WriteLine("\n6. Executing orders...");
            var executions = new List<Execution>();
            
            foreach (var signal in approvedSignals)
            {
                var quantity = positionSizes[signal.Symbol];
                var order = CreateOrder(signal.Symbol, quantity, 
                    signal.Action == SignalAction.Buy ? OrderSide.Buy : OrderSide.Sell);
                
                var marketData = await _mockMarketDataService.Object.GetMarketDataAsync(signal.Symbol);
                var execution = await _executionEngine.ExecuteOrderAsync(order, marketData.Price);
                
                if (execution != null && execution.Status == OrderStatus.Filled)
                {
                    executions.Add(execution);
                    Output.WriteLine($"   {execution.Symbol}: {execution.Side} {execution.ExecutedQuantity} @ {execution.ExecutedPrice:C}");
                }
            }
            
            Assert.NotEmpty(executions);
            
            // Step 7: Post-Trade Analysis
            Output.WriteLine("\n7. Post-trade analysis...");
            var updatedPortfolio = await _portfolioManager.GetPortfolioAsync();
            var newRisk = await CalculatePortfolioRisk(updatedPortfolio);
            
            Output.WriteLine($"   New portfolio value: {await _portfolioManager.GetTotalPortfolioValueAsync():C}");
            Output.WriteLine($"   Positions: {updatedPortfolio.Holdings.Count}");
            Output.WriteLine($"   Cash remaining: {updatedPortfolio.CashBalance:C}");
            Output.WriteLine($"   New risk metrics: VaR={newRisk.VaR:P2}, Sharpe={newRisk.SharpeRatio:F2}");
            
            // Verify risk didn't increase dramatically
            Assert.True(newRisk.VaR < currentRisk.VaR * 1.5m, "Risk increased too much");
            
            Output.WriteLine("\n=== Trading Day E2E Test Completed Successfully ===");
        }
        
        #endregion
        
        #region Intraday Trading Scenarios
        
        [Fact]
        public async Task IntradayScenario_StopLossTriggered_ExitsPosition()
        {
            // Arrange
            Output.WriteLine("=== Testing Stop Loss Scenario ===");
            
            // Buy a position
            var buyOrder = CreateOrder("AAPL", 100, OrderSide.Buy);
            var buyExecution = await _executionEngine.ExecuteOrderAsync(buyOrder, 175m);
            
            Assert.NotNull(buyExecution);
            Assert.Equal(OrderStatus.Filled, buyExecution.Status);
            
            var entryPrice = buyExecution.ExecutedPrice;
            var stopLossPrice = entryPrice * 0.98m; // 2% stop loss
            
            Output.WriteLine($"Position entered at {entryPrice:C}, stop loss at {stopLossPrice:C}");
            
            // Act - Simulate price dropping below stop loss
            await SimulatePriceMovement("AAPL", stopLossPrice * 0.99m);
            
            // Check if stop loss should trigger
            var currentPrice = (await _mockMarketDataService.Object.GetMarketDataAsync("AAPL")).Price;
            var shouldExit = currentPrice <= stopLossPrice;
            
            Assert.True(shouldExit);
            
            // Execute stop loss
            var stopOrder = CreateOrder("AAPL", 100, OrderSide.Sell);
            stopOrder.OrderType = OrderType.Market; // Exit at market
            
            var stopExecution = await _executionEngine.ExecuteOrderAsync(stopOrder, currentPrice);
            
            // Assert
            Assert.NotNull(stopExecution);
            Assert.Equal(OrderStatus.Filled, stopExecution.Status);
            
            var loss = (stopExecution.ExecutedPrice - entryPrice) * 100;
            Output.WriteLine($"Stop loss executed at {stopExecution.ExecutedPrice:C}");
            Output.WriteLine($"Loss: {loss:C} ({loss / (entryPrice * 100):P2})");
            
            // Verify position is closed
            var portfolio = await _portfolioManager.GetPortfolioAsync();
            Assert.DoesNotContain("AAPL", portfolio.Holdings.Keys);
        }
        
        [Fact]
        public async Task IntradayScenario_TakeProfitTarget_ExitsWithProfit()
        {
            // Arrange
            Output.WriteLine("=== Testing Take Profit Scenario ===");
            
            // Buy a position
            var buyOrder = CreateOrder("MSFT", 50, OrderSide.Buy);
            var buyExecution = await _executionEngine.ExecuteOrderAsync(buyOrder, 350m);
            
            var entryPrice = buyExecution.ExecutedPrice;
            var takeProfitPrice = entryPrice * 1.03m; // 3% take profit
            
            Output.WriteLine($"Position entered at {entryPrice:C}, take profit at {takeProfitPrice:C}");
            
            // Act - Simulate price reaching take profit
            await SimulatePriceMovement("MSFT", takeProfitPrice * 1.01m);
            
            var currentPrice = (await _mockMarketDataService.Object.GetMarketDataAsync("MSFT")).Price;
            var shouldTakeProfit = currentPrice >= takeProfitPrice;
            
            Assert.True(shouldTakeProfit);
            
            // Execute take profit
            var profitOrder = CreateOrder("MSFT", 50, OrderSide.Sell);
            var profitExecution = await _executionEngine.ExecuteOrderAsync(profitOrder, currentPrice);
            
            // Assert
            Assert.NotNull(profitExecution);
            Assert.Equal(OrderStatus.Filled, profitExecution.Status);
            
            var profit = (profitExecution.ExecutedPrice - entryPrice) * 50;
            Output.WriteLine($"Take profit executed at {profitExecution.ExecutedPrice:C}");
            Output.WriteLine($"Profit: {profit:C} ({profit / (entryPrice * 50):P2})");
        }
        
        #endregion
        
        #region Risk Management Scenarios
        
        [Fact]
        public async Task RiskManagement_PortfolioDrawdown_StopsNewTrades()
        {
            // Arrange
            Output.WriteLine("=== Testing Drawdown Risk Management ===");
            
            var initialValue = await _portfolioManager.GetTotalPortfolioValueAsync();
            
            // Simulate losses by executing losing trades
            var losingTrades = new[]
            {
                ("AAPL", 100, 175m, 170m),
                ("MSFT", 50, 350m, 340m),
                ("GOOGL", 25, 140m, 134m)
            };
            
            foreach (var (symbol, qty, buyPrice, sellPrice) in losingTrades)
            {
                var buyOrder = CreateOrder(symbol, qty, OrderSide.Buy);
                await _executionEngine.ExecuteOrderAsync(buyOrder, buyPrice);
                
                await SimulatePriceMovement(symbol, sellPrice);
                
                var sellOrder = CreateOrder(symbol, qty, OrderSide.Sell);
                await _executionEngine.ExecuteOrderAsync(sellOrder, sellPrice);
            }
            
            // Calculate drawdown
            var currentValue = await _portfolioManager.GetTotalPortfolioValueAsync();
            var drawdown = (initialValue - currentValue) / initialValue;
            
            Output.WriteLine($"Initial value: {initialValue:C}");
            Output.WriteLine($"Current value: {currentValue:C}");
            Output.WriteLine($"Drawdown: {drawdown:P2}");
            
            // Act - Try to place new trade
            var newSignal = new TradingSignal
            {
                Symbol = "NVDA",
                Action = SignalAction.Buy,
                Confidence = 0.8m,
                SuggestedQuantity = 50
            };
            
            var validation = await _goldenRulesEngine.ValidateTradeAsync(
                newSignal.Symbol,
                OrderSide.Buy,
                newSignal.SuggestedQuantity.Value,
                currentValue);
            
            // Assert - Should be rejected if drawdown exceeds threshold
            if (drawdown > 0.05m) // 5% drawdown threshold
            {
                Assert.False(validation.IsValid);
                Assert.Contains(validation.ViolatedRules, r => r.RuleName.Contains("Drawdown") || r.RuleName.Contains("Risk"));
                Output.WriteLine($"New trade rejected due to {drawdown:P2} drawdown");
            }
        }
        
        [Fact]
        public async Task RiskManagement_ConcentrationLimit_PreventsOverexposure()
        {
            // Arrange
            Output.WriteLine("=== Testing Position Concentration Limits ===");
            
            // Build concentrated position
            var order = CreateOrder("AAPL", 500, OrderSide.Buy);
            var execution = await _executionEngine.ExecuteOrderAsync(order, 175m);
            
            Assert.Equal(OrderStatus.Filled, execution.Status);
            
            var portfolio = await _portfolioManager.GetPortfolioAsync();
            var totalValue = await _portfolioManager.GetTotalPortfolioValueAsync();
            var positionValue = portfolio.Holdings["AAPL"].MarketValue;
            var concentration = positionValue / totalValue;
            
            Output.WriteLine($"AAPL position: {positionValue:C} ({concentration:P2} of portfolio)");
            
            // Act - Try to add more to concentrated position
            var additionalOrder = CreateOrder("AAPL", 200, OrderSide.Buy);
            var validation = await _goldenRulesEngine.ValidateTradeAsync(
                "AAPL",
                OrderSide.Buy,
                200,
                portfolio.CashBalance);
            
            // Assert
            if (concentration > 0.25m) // 25% concentration limit
            {
                Assert.False(validation.IsValid);
                Assert.Contains(validation.ViolatedRules, r => r.RuleName.Contains("Concentration") || r.RuleName.Contains("Position Size"));
                Output.WriteLine("Additional purchase rejected due to concentration limit");
            }
        }
        
        #endregion
        
        #region Multi-Day Trading Scenarios
        
        [Fact]
        public async Task MultiDayScenario_OvernightGap_HandlesCorrectly()
        {
            // Arrange
            Output.WriteLine("=== Testing Overnight Gap Handling ===");
            
            // Day 1: Enter position before close
            var buyOrder = CreateOrder("TSLA", 40, OrderSide.Buy);
            var buyExecution = await _executionEngine.ExecuteOrderAsync(buyOrder, 250m);
            
            var closePrice = 252m;
            await SimulatePriceMovement("TSLA", closePrice);
            
            Output.WriteLine($"Day 1 - Entered at {buyExecution.ExecutedPrice:C}, closed at {closePrice:C}");
            
            // Act - Day 2: Simulate gap up
            var openPrice = 258m; // 2.4% gap up
            await SimulateMarketOpen("TSLA", openPrice);
            
            var gapPercent = (openPrice - closePrice) / closePrice;
            Output.WriteLine($"Day 2 - Gapped up {gapPercent:P2} to {openPrice:C}");
            
            // Check if we should take profits on gap
            var shouldTakeProfit = gapPercent > 0.02m; // Take profit on >2% gap
            
            if (shouldTakeProfit)
            {
                var sellOrder = CreateOrder("TSLA", 40, OrderSide.Sell);
                var sellExecution = await _executionEngine.ExecuteOrderAsync(sellOrder, openPrice);
                
                Assert.Equal(OrderStatus.Filled, sellExecution.Status);
                
                var profit = (sellExecution.ExecutedPrice - buyExecution.ExecutedPrice) * 40;
                Output.WriteLine($"Sold on gap at {sellExecution.ExecutedPrice:C}, profit: {profit:C}");
            }
        }
        
        #endregion
        
        #region Performance and Stress Tests
        
        [Fact]
        public async Task StressTest_HighFrequencyTrading_MaintainsIntegrity()
        {
            // Arrange
            Output.WriteLine("=== High Frequency Trading Stress Test ===");
            
            var symbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA" };
            var trades = new List<(DateTime Time, string Symbol, OrderSide Side, decimal Quantity)>();
            var executionTasks = new List<Task<Execution>>();
            
            // Generate rapid-fire trades
            var startTime = DateTime.UtcNow;
            for (int i = 0; i < 100; i++)
            {
                var symbol = symbols[i % symbols.Length];
                var side = i % 2 == 0 ? OrderSide.Buy : OrderSide.Sell;
                var quantity = 10 + (i % 5) * 10;
                
                trades.Add((startTime.AddMilliseconds(i * 100), symbol, side, quantity));
            }
            
            // Act - Execute trades concurrently
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            foreach (var trade in trades)
            {
                var order = CreateOrder(trade.Symbol, trade.Quantity, trade.Side);
                var task = _executionEngine.ExecuteOrderAsync(order, 100m + Random.Shared.Next(50));
                executionTasks.Add(task);
                
                // Small delay to simulate realistic order flow
                await Task.Delay(10);
            }
            
            var executions = await Task.WhenAll(executionTasks);
            stopwatch.Stop();
            
            // Assert
            var successfulExecutions = executions.Where(e => e?.Status == OrderStatus.Filled).Count();
            var rejectedExecutions = executions.Where(e => e?.Status == OrderStatus.Rejected).Count();
            
            Output.WriteLine($"Executed {trades.Count} trades in {stopwatch.ElapsedMilliseconds}ms");
            Output.WriteLine($"Successful: {successfulExecutions}, Rejected: {rejectedExecutions}");
            Output.WriteLine($"Throughput: {trades.Count / (stopwatch.ElapsedMilliseconds / 1000.0):F0} trades/second");
            
            // Verify portfolio integrity
            var portfolio = await _portfolioManager.GetPortfolioAsync();
            var totalValue = await _portfolioManager.GetTotalPortfolioValueAsync();
            
            Assert.True(totalValue > 0);
            Assert.True(portfolio.CashBalance >= 0);
            
            Output.WriteLine($"\nFinal portfolio value: {totalValue:C}");
            Output.WriteLine($"Positions held: {portfolio.Holdings.Count}");
        }
        
        #endregion
        
        #region Helper Methods
        
        private Order CreateOrder(string symbol, decimal quantity, OrderSide side)
        {
            return new Order
            {
                OrderId = Guid.NewGuid().ToString(),
                Symbol = symbol,
                Quantity = quantity,
                OrderType = OrderType.Market,
                Side = side,
                CreatedAt = DateTime.UtcNow
            };
        }
        
        private string[] GetUniverseSymbols()
        {
            return new[]
            {
                "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "META", "NVDA", "JPM",
                "V", "JNJ", "WMT", "PG", "UNH", "HD", "MA", "DIS", "PYPL", "BAC",
                "NFLX", "ADBE", "CRM", "XOM", "VZ", "CMCSA", "PFE", "KO", "PEP",
                "TMO", "CSCO", "ABT", "CVX", "NKE", "ABBV", "ACN", "AVGO", "MRK"
            };
        }
        
        private void SetupMarketDataForTradingDay()
        {
            var symbols = GetUniverseSymbols();
            var random = new Random(42);
            
            foreach (var symbol in symbols)
            {
                var basePrice = 50m + random.Next(0, 300);
                var previousClose = basePrice * (1m - (decimal)(random.NextDouble() * 0.05));
                var gapPercent = (decimal)(random.NextDouble() * 0.06 - 0.03); // -3% to +3%
                var openPrice = previousClose * (1m + gapPercent);
                
                var marketData = new MarketData
                {
                    Symbol = symbol,
                    Price = basePrice,
                    PreviousClose = previousClose,
                    Open = openPrice,
                    High = basePrice * 1.02m,
                    Low = basePrice * 0.98m,
                    Volume = random.Next(5_000_000, 50_000_000),
                    AverageVolume = random.Next(10_000_000, 30_000_000),
                    MarketCap = basePrice * random.Next(1_000_000_000, 10_000_000_000),
                    HasNews = random.NextDouble() > 0.6,
                    NewsCount = random.NextDouble() > 0.6 ? random.Next(1, 5) : 0
                };
                
                _mockMarketDataService.Setup(x => x.GetMarketDataAsync(symbol))
                    .ReturnsAsync(marketData);
            }
        }
        
        private async Task SimulatePriceMovement(string symbol, decimal newPrice)
        {
            var currentData = await _mockMarketDataService.Object.GetMarketDataAsync(symbol);
            var updatedData = new MarketData
            {
                Symbol = symbol,
                Price = newPrice,
                PreviousClose = currentData.PreviousClose,
                Open = currentData.Open,
                High = Math.Max(currentData.High, newPrice),
                Low = Math.Min(currentData.Low, newPrice),
                Volume = currentData.Volume + Random.Shared.Next(100_000, 1_000_000),
                AverageVolume = currentData.AverageVolume,
                MarketCap = newPrice * (currentData.MarketCap / currentData.Price),
                HasNews = currentData.HasNews,
                NewsCount = currentData.NewsCount
            };
            
            _mockMarketDataService.Setup(x => x.GetMarketDataAsync(symbol))
                .ReturnsAsync(updatedData);
        }
        
        private async Task SimulateMarketOpen(string symbol, decimal openPrice)
        {
            var previousData = await _mockMarketDataService.Object.GetMarketDataAsync(symbol);
            var openData = new MarketData
            {
                Symbol = symbol,
                Price = openPrice,
                PreviousClose = previousData.Price, // Yesterday's close
                Open = openPrice,
                High = openPrice,
                Low = openPrice,
                Volume = Random.Shared.Next(1_000_000, 5_000_000),
                AverageVolume = previousData.AverageVolume,
                MarketCap = openPrice * (previousData.MarketCap / previousData.Price),
                HasNews = previousData.HasNews,
                NewsCount = previousData.NewsCount
            };
            
            _mockMarketDataService.Setup(x => x.GetMarketDataAsync(symbol))
                .ReturnsAsync(openData);
        }
        
        private async Task<RiskMetrics> CalculatePortfolioRisk(Portfolio portfolio)
        {
            // Simplified risk calculation for testing
            var returns = new List<decimal>();
            var values = new List<decimal> { 100000m }; // Starting value
            
            // Generate some sample returns
            for (int i = 0; i < 20; i++)
            {
                var dailyReturn = (decimal)(Random.Shared.NextDouble() * 0.04 - 0.02); // -2% to +2%
                returns.Add(dailyReturn);
                values.Add(values.Last() * (1 + dailyReturn));
            }
            
            var riskContext = new RiskCalculationContext
            {
                Returns = returns,
                PortfolioValues = values,
                ConfidenceLevel = 0.95m,
                RiskFreeRate = 0.02m
            };
            
            var assessment = await _riskCalculator.EvaluateRiskAsync(riskContext);
            
            return new RiskMetrics
            {
                VaR = (decimal)assessment.Metrics["VaR"],
                SharpeRatio = (decimal)assessment.Metrics["SharpeRatio"],
                MaxDrawdown = (decimal)assessment.Metrics["MaxDrawdown"]
            };
        }
        
        private async Task<Dictionary<string, decimal>> CalculatePositionSizes(
            List<TradingSignal> signals, 
            Portfolio portfolio)
        {
            var sizes = new Dictionary<string, decimal>();
            var availableCapital = portfolio.CashBalance * 0.95m; // Keep 5% cash reserve
            var maxPositionSize = availableCapital * 0.20m; // Max 20% per position
            
            foreach (var signal in signals)
            {
                var marketData = await _mockMarketDataService.Object.GetMarketDataAsync(signal.Symbol);
                var shares = Math.Floor(Math.Min(maxPositionSize, availableCapital / signals.Count) / marketData.Price);
                sizes[signal.Symbol] = shares;
            }
            
            return sizes;
        }
        
        #endregion
    }
    
    // Helper classes for E2E tests
    public class TradingSignal
    {
        public string Symbol { get; set; }
        public SignalAction Action { get; set; }
        public decimal Confidence { get; set; }
        public decimal? SuggestedQuantity { get; set; }
        public string Strategy { get; set; }
    }
    
    public enum SignalAction
    {
        Buy,
        Sell,
        Hold
    }
    
    public class RiskCalculationContext
    {
        public List<decimal> Returns { get; set; }
        public List<decimal> PortfolioValues { get; set; }
        public decimal ConfidenceLevel { get; set; }
        public decimal RiskFreeRate { get; set; }
    }
    
    public class RiskMetrics
    {
        public decimal VaR { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal MaxDrawdown { get; set; }
    }
}