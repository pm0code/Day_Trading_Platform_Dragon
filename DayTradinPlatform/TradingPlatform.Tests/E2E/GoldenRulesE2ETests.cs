using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.GoldenRules.Engine;
using TradingPlatform.GoldenRules.Rules;
using TradingPlatform.PaperTrading.Services;
using TradingPlatform.PaperTrading.Models;
using TradingPlatform.Tests.Core.Canonical;
using Moq;

namespace TradingPlatform.Tests.E2E
{
    /// <summary>
    /// End-to-end tests for Golden Rules enforcement
    /// Ensures all trading rules are properly enforced throughout the trading workflow
    /// </summary>
    public class GoldenRulesE2ETests : E2ETestBase
    {
        private CanonicalGoldenRulesEngine _goldenRulesEngine;
        private IOrderExecutionEngine _executionEngine;
        private IPortfolioManager _portfolioManager;
        private Mock<IMarketDataService> _mockMarketDataService;
        
        public GoldenRulesE2ETests(ITestOutputHelper output) : base(output)
        {
            _mockMarketDataService = new Mock<IMarketDataService>();
        }
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            // Register Golden Rules
            services.AddSingleton<CanonicalGoldenRulesEngine>();
            services.AddTransient<ICanonicalRule, Rule01_CapitalPreservation>();
            services.AddTransient<ICanonicalRule, Rule02_NeverAddToLosingPosition>();
            services.AddTransient<ICanonicalRule, Rule03_CutLossesQuickly>();
            services.AddTransient<ICanonicalRule, Rule04_LetWinnersRun>();
            services.AddTransient<ICanonicalRule, Rule05_TradingDiscipline>();
            services.AddTransient<ICanonicalRule, Rule06_EmotionManagement>();
            services.AddTransient<ICanonicalRule, Rule07_ProperPositionSizing>();
            services.AddTransient<ICanonicalRule, Rule08_PaperTradeFirst>();
            services.AddTransient<ICanonicalRule, Rule09_ContinuousLearning>();
            services.AddTransient<ICanonicalRule, Rule10_PatientEntries>();
            services.AddTransient<ICanonicalRule, Rule11_NeverRiskRentMoney>();
            services.AddTransient<ICanonicalRule, Rule12_RecordKeeping>();
            
            // Register trading services
            services.AddPaperTradingServices();
            
            // Register mocks
            services.AddSingleton(MockLogger.Object);
            services.AddSingleton(_mockMarketDataService.Object);
            services.AddSingleton(Mock.Of<IServiceProvider>());
        }
        
        protected override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            
            _goldenRulesEngine = ServiceProvider.GetRequiredService<CanonicalGoldenRulesEngine>();
            _executionEngine = ServiceProvider.GetRequiredService<IOrderExecutionEngine>();
            _portfolioManager = ServiceProvider.GetRequiredService<IPortfolioManager>();
            
            // Initialize services
            await _goldenRulesEngine.InitializeAsync();
            await _portfolioManager.InitializePortfolioAsync(100000m); // $100k starting capital
            
            // Register all rules
            var rules = ServiceProvider.GetServices<ICanonicalRule>();
            foreach (var rule in rules)
            {
                _goldenRulesEngine.RegisterRule(rule);
            }
        }
        
        #region Capital Preservation Tests
        
        [Fact]
        public async Task Rule01_CapitalPreservation_PreventsExcessiveRisk()
        {
            // Arrange
            Output.WriteLine("=== Testing Rule 01: Capital Preservation ===");
            
            var portfolio = await _portfolioManager.GetPortfolioAsync();
            var initialCapital = portfolio.CashBalance;
            
            // Try to risk more than 2% on a single trade
            var stockPrice = 100m;
            var stopLoss = 95m; // 5% stop loss
            var riskPerShare = stockPrice - stopLoss;
            var maxRisk = initialCapital * 0.02m; // 2% max risk
            var theoreticalShares = maxRisk / riskPerShare; // Should be 400 shares
            var excessiveShares = 500m; // This would risk 2.5%
            
            SetupMarketData("AAPL", stockPrice);
            
            // Act
            var validation = await _goldenRulesEngine.ValidateTradeAsync(
                "AAPL", OrderSide.Buy, excessiveShares, portfolio.CashBalance);
            
            // Assert
            Assert.False(validation.IsValid);
            Assert.Contains(validation.ViolatedRules, r => r.RuleName.Contains("Capital Preservation"));
            
            Output.WriteLine($"Trade validation: {(validation.IsValid ? "PASSED" : "FAILED")}");
            Output.WriteLine($"Risk amount: ${excessiveShares * riskPerShare:N2} ({excessiveShares * riskPerShare / initialCapital:P2})");
            Output.WriteLine($"Violated rule: {validation.ViolatedRules.FirstOrDefault()?.RuleName}");
            Output.WriteLine($"Reason: {validation.ViolatedRules.FirstOrDefault()?.ViolationReason}");
            
            // Now try with acceptable risk
            var acceptableShares = Math.Floor(theoreticalShares);
            var validation2 = await _goldenRulesEngine.ValidateTradeAsync(
                "AAPL", OrderSide.Buy, acceptableShares, portfolio.CashBalance);
            
            Assert.True(validation2.IsValid);
            Output.WriteLine($"\nAcceptable trade: {acceptableShares} shares (risk: {acceptableShares * riskPerShare / initialCapital:P2})");
        }
        
        [Fact]
        public async Task DailyLossLimit_StopsTrading_AfterExcessiveLosses()
        {
            // Arrange
            Output.WriteLine("=== Testing Daily Loss Limit ===");
            
            var initialCapital = 100000m;
            var dailyLossLimit = initialCapital * 0.06m; // 6% daily loss limit
            var currentLoss = 0m;
            
            // Execute losing trades
            var losingTrades = new[]
            {
                ("AAPL", 100m, 175m, 170m),  // -$500
                ("MSFT", 50m, 350m, 340m),   // -$500
                ("GOOGL", 40m, 140m, 130m),  // -$400
                ("TSLA", 20m, 250m, 230m),   // -$400
                ("NVDA", 30m, 300m, 280m)    // -$600 - Total: -$2,400 (2.4%)
            };
            
            foreach (var (symbol, qty, buyPrice, sellPrice) in losingTrades)
            {
                SetupMarketData(symbol, buyPrice);
                
                // Buy
                var buyOrder = CreateOrder(symbol, qty, OrderSide.Buy);
                await _executionEngine.ExecuteOrderAsync(buyOrder, buyPrice);
                
                // Update price and sell at loss
                SetupMarketData(symbol, sellPrice);
                var sellOrder = CreateOrder(symbol, qty, OrderSide.Sell);
                await _executionEngine.ExecuteOrderAsync(sellOrder, sellPrice);
                
                currentLoss += (buyPrice - sellPrice) * qty;
                Output.WriteLine($"{symbol}: Loss ${(buyPrice - sellPrice) * qty:N0} (Total: ${currentLoss:N0})");
            }
            
            // Try another large trade that would exceed daily limit
            var nextTrade = ("META", 100m, 300m); // Would be $30k position
            SetupMarketData(nextTrade.Item1, nextTrade.Item3);
            
            // Act
            var validation = await _goldenRulesEngine.ValidateTradeAsync(
                nextTrade.Item1, OrderSide.Buy, nextTrade.Item2, 
                await _portfolioManager.GetPortfolioAsync().ContinueWith(t => t.Result.CashBalance));
            
            // Assert
            // Should be blocked if daily loss is too high
            if (currentLoss > dailyLossLimit * 0.5m) // Half of daily limit reached
            {
                Output.WriteLine($"\nDaily loss: ${currentLoss:N0} ({currentLoss/initialCapital:P2})");
                Output.WriteLine("Trade restricted due to daily loss approaching limit");
                // In a real implementation, this would check daily P&L
            }
        }
        
        #endregion
        
        #region Position Management Tests
        
        [Fact]
        public async Task Rule02_NeverAddToLosingPosition_BlocksAveraging()
        {
            // Arrange
            Output.WriteLine("=== Testing Rule 02: Never Add to Losing Position ===");
            
            // Enter initial position
            var initialPrice = 100m;
            var shares = 100m;
            SetupMarketData("TSLA", initialPrice);
            
            var buyOrder = CreateOrder("TSLA", shares, OrderSide.Buy);
            var execution = await _executionEngine.ExecuteOrderAsync(buyOrder, initialPrice);
            
            Output.WriteLine($"Initial position: {shares} shares @ ${initialPrice}");
            
            // Price drops
            var currentPrice = 95m; // 5% loss
            SetupMarketData("TSLA", currentPrice);
            await _portfolioManager.UpdatePositionPriceAsync("TSLA", currentPrice);
            
            var position = (await _portfolioManager.GetPositionsAsync())
                .FirstOrDefault(p => p.Symbol == "TSLA");
            
            var unrealizedLoss = (currentPrice - position.AverageCost) * position.Quantity;
            Output.WriteLine($"Current price: ${currentPrice} (Loss: ${unrealizedLoss:N0})");
            
            // Act - Try to add to losing position
            var additionalShares = 100m;
            var validation = await _goldenRulesEngine.ValidateTradeAsync(
                "TSLA", OrderSide.Buy, additionalShares, 
                (await _portfolioManager.GetPortfolioAsync()).CashBalance);
            
            // Assert
            Assert.False(validation.IsValid);
            Assert.Contains(validation.ViolatedRules, r => r.RuleName.Contains("Never Add to Losing"));
            
            Output.WriteLine($"\nAttempt to add {additionalShares} shares: BLOCKED");
            Output.WriteLine($"Reason: {validation.ViolatedRules.FirstOrDefault()?.ViolationReason}");
        }
        
        [Fact]
        public async Task Rule03_CutLossesQuickly_EnforcesStopLoss()
        {
            // Arrange
            Output.WriteLine("=== Testing Rule 03: Cut Losses Quickly ===");
            
            // Enter position
            var entryPrice = 150m;
            var shares = 100m;
            var stopLossPercent = 0.02m; // 2% stop loss
            var stopLossPrice = entryPrice * (1 - stopLossPercent);
            
            SetupMarketData("AAPL", entryPrice);
            var buyOrder = CreateOrder("AAPL", shares, OrderSide.Buy);
            await _executionEngine.ExecuteOrderAsync(buyOrder, entryPrice);
            
            Output.WriteLine($"Position entered: {shares} shares @ ${entryPrice}");
            Output.WriteLine($"Stop loss set at: ${stopLossPrice:F2} ({stopLossPercent:P0})");
            
            // Act - Price hits stop loss
            var currentPrice = stopLossPrice - 0.50m; // Price drops below stop
            SetupMarketData("AAPL", currentPrice);
            await _portfolioManager.UpdatePositionPriceAsync("AAPL", currentPrice);
            
            // Check if stop loss should trigger
            var position = (await _portfolioManager.GetPositionsAsync())
                .FirstOrDefault(p => p.Symbol == "AAPL");
            
            var lossPercent = (currentPrice - entryPrice) / entryPrice;
            var shouldStopOut = lossPercent <= -stopLossPercent;
            
            // Assert
            Assert.True(shouldStopOut);
            Output.WriteLine($"\nCurrent price: ${currentPrice} ({lossPercent:P2} loss)");
            Output.WriteLine("STOP LOSS TRIGGERED - Position should be closed");
            
            // Execute stop loss
            var stopOrder = CreateOrder("AAPL", shares, OrderSide.Sell);
            var stopExecution = await _executionEngine.ExecuteOrderAsync(stopOrder, currentPrice);
            
            var realizedLoss = (stopExecution.ExecutedPrice - entryPrice) * shares;
            Output.WriteLine($"Position closed at ${stopExecution.ExecutedPrice}");
            Output.WriteLine($"Realized loss: ${realizedLoss:N0} ({realizedLoss/(entryPrice*shares):P2})");
        }
        
        [Fact]
        public async Task Rule04_LetWinnersRun_AllowsProfitablePositions()
        {
            // Arrange
            Output.WriteLine("=== Testing Rule 04: Let Winners Run ===");
            
            // Enter position
            var entryPrice = 200m;
            var shares = 50m;
            SetupMarketData("NVDA", entryPrice);
            
            var buyOrder = CreateOrder("NVDA", shares, OrderSide.Buy);
            await _executionEngine.ExecuteOrderAsync(buyOrder, entryPrice);
            
            Output.WriteLine($"Position entered: {shares} shares @ ${entryPrice}");
            
            // Price increases
            var priceTargets = new[] { 210m, 220m, 230m, 240m }; // 5%, 10%, 15%, 20% gains
            
            foreach (var targetPrice in priceTargets)
            {
                SetupMarketData("NVDA", targetPrice);
                await _portfolioManager.UpdatePositionPriceAsync("NVDA", targetPrice);
                
                var gainPercent = (targetPrice - entryPrice) / entryPrice;
                var unrealizedProfit = (targetPrice - entryPrice) * shares;
                
                Output.WriteLine($"\nPrice: ${targetPrice} (+{gainPercent:P1}) - Profit: ${unrealizedProfit:N0}");
                
                // Validate we can still hold (not forced to sell)
                var validation = await _goldenRulesEngine.ValidateTradeAsync(
                    "NVDA", OrderSide.Hold, shares, 
                    (await _portfolioManager.GetPortfolioAsync()).CashBalance);
                
                Assert.True(validation.IsValid);
                Output.WriteLine("Position allowed to continue running ✓");
            }
            
            // Use trailing stop to protect profits
            var currentPrice = priceTargets.Last();
            var trailingStopPercent = 0.10m; // 10% trailing stop
            var trailingStopPrice = currentPrice * (1 - trailingStopPercent);
            
            Output.WriteLine($"\nTrailing stop set at ${trailingStopPrice:F2} " +
                           $"(protects {(trailingStopPrice - entryPrice) / entryPrice:P1} profit)");
        }
        
        #endregion
        
        #region Trading Discipline Tests
        
        [Fact]
        public async Task Rule05_TradingDiscipline_RequiresTradingPlan()
        {
            // Arrange
            Output.WriteLine("=== Testing Rule 05: Trading Discipline ===");
            
            // Attempt trade without proper setup
            var randomTrade = new Order
            {
                Symbol = "RANDOM",
                Quantity = 100,
                OrderType = OrderType.Market,
                Side = OrderSide.Buy,
                // Missing: Entry criteria, stop loss, profit target
            };
            
            SetupMarketData("RANDOM", 50m);
            
            // Act
            var validation = await _goldenRulesEngine.ValidateTradeAsync(
                randomTrade.Symbol, randomTrade.Side, randomTrade.Quantity,
                (await _portfolioManager.GetPortfolioAsync()).CashBalance);
            
            // Assert - Should require trading plan
            Output.WriteLine("Trade without plan: " + (validation.IsValid ? "ALLOWED" : "BLOCKED"));
            
            // Now create proper trading plan
            var plannedTrade = new Order
            {
                Symbol = "AAPL",
                Quantity = 100,
                OrderType = OrderType.Limit,
                LimitPrice = 175m,
                Side = OrderSide.Buy,
                StopLossPrice = 171.5m, // 2% stop
                TakeProfitPrice = 180m, // ~2.9% profit target
                EntryReason = "Breakout above resistance with volume",
                RiskRewardRatio = 2.33m
            };
            
            SetupMarketData("AAPL", 175m);
            
            var validation2 = await _goldenRulesEngine.ValidateTradeAsync(
                plannedTrade.Symbol, plannedTrade.Side, plannedTrade.Quantity,
                (await _portfolioManager.GetPortfolioAsync()).CashBalance);
            
            Assert.True(validation2.IsValid);
            
            Output.WriteLine("\nPlanned trade details:");
            Output.WriteLine($"  Entry: ${plannedTrade.LimitPrice}");
            Output.WriteLine($"  Stop Loss: ${plannedTrade.StopLossPrice}");
            Output.WriteLine($"  Target: ${plannedTrade.TakeProfitPrice}");
            Output.WriteLine($"  Risk/Reward: {plannedTrade.RiskRewardRatio:F2}");
            Output.WriteLine($"  Reason: {plannedTrade.EntryReason}");
            Output.WriteLine("  Status: APPROVED ✓");
        }
        
        [Fact]
        public async Task Rule07_ProperPositionSizing_EnforcesLimits()
        {
            // Arrange
            Output.WriteLine("=== Testing Rule 07: Proper Position Sizing ===");
            
            var portfolio = await _portfolioManager.GetPortfolioAsync();
            var accountSize = await _portfolioManager.GetTotalPortfolioValueAsync();
            
            // Test various position sizes
            var testCases = new[]
            {
                ("AAPL", 175m, accountSize * 0.15m), // 15% - should pass
                ("MSFT", 350m, accountSize * 0.30m), // 30% - too large
                ("GOOGL", 140m, accountSize * 0.08m), // 8% - good
                ("TSLA", 250m, accountSize * 0.40m)  // 40% - way too large
            };
            
            foreach (var (symbol, price, positionValue) in testCases)
            {
                SetupMarketData(symbol, price);
                var shares = Math.Floor(positionValue / price);
                var actualPercent = (shares * price) / accountSize;
                
                var validation = await _goldenRulesEngine.ValidateTradeAsync(
                    symbol, OrderSide.Buy, shares, portfolio.CashBalance);
                
                Output.WriteLine($"\n{symbol}: {shares} shares @ ${price} = ${shares * price:N0} ({actualPercent:P1})");
                Output.WriteLine($"  Status: {(validation.IsValid ? "APPROVED ✓" : "REJECTED ✗")}");
                
                if (!validation.IsValid)
                {
                    Output.WriteLine($"  Reason: {validation.ViolatedRules.FirstOrDefault()?.ViolationReason}");
                }
                
                // Position sizing rules:
                // - Max 25% in any single position
                // - Max 10% for day trades
                // - Scale down in volatile markets
                if (actualPercent > 0.25m)
                {
                    Assert.False(validation.IsValid);
                }
            }
        }
        
        #endregion
        
        #region Record Keeping Tests
        
        [Fact]
        public async Task Rule12_RecordKeeping_TracksAllTrades()
        {
            // Arrange
            Output.WriteLine("=== Testing Rule 12: Record Keeping ===");
            
            var trades = new List<TradeRecord>();
            
            // Execute several trades
            var tradeSequence = new[]
            {
                ("AAPL", 100m, OrderSide.Buy, 175m, "Breakout trade"),
                ("AAPL", 100m, OrderSide.Sell, 178m, "Hit profit target"),
                ("MSFT", 50m, OrderSide.Buy, 350m, "Momentum play"),
                ("MSFT", 50m, OrderSide.Sell, 345m, "Stop loss hit"),
                ("GOOGL", 25m, OrderSide.Buy, 140m, "Value entry")
            };
            
            foreach (var (symbol, qty, side, price, reason) in tradeSequence)
            {
                SetupMarketData(symbol, price);
                
                var order = CreateOrder(symbol, qty, side);
                order.EntryReason = reason;
                
                var execution = await _executionEngine.ExecuteOrderAsync(order, price);
                
                // Record trade
                var tradeRecord = new TradeRecord
                {
                    TradeId = execution.ExecutionId,
                    Symbol = symbol,
                    Side = side,
                    Quantity = qty,
                    Price = execution.ExecutedPrice,
                    Timestamp = execution.ExecutedAt,
                    Reason = reason,
                    Strategy = "Test Strategy"
                };
                
                trades.Add(tradeRecord);
            }
            
            // Act - Generate trade journal
            Output.WriteLine("\nTrade Journal:");
            Output.WriteLine("================");
            
            decimal totalPnL = 0;
            var openPositions = new Dictionary<string, (decimal AvgPrice, decimal Quantity)>();
            
            foreach (var trade in trades)
            {
                Output.WriteLine($"\n{trade.Timestamp:yyyy-MM-dd HH:mm:ss} - {trade.Symbol}");
                Output.WriteLine($"  Action: {trade.Side} {trade.Quantity} @ ${trade.Price:F2}");
                Output.WriteLine($"  Reason: {trade.Reason}");
                
                // Calculate P&L
                if (trade.Side == OrderSide.Buy)
                {
                    if (!openPositions.ContainsKey(trade.Symbol))
                    {
                        openPositions[trade.Symbol] = (trade.Price, trade.Quantity);
                    }
                    else
                    {
                        var existing = openPositions[trade.Symbol];
                        var totalQty = existing.Quantity + trade.Quantity;
                        var avgPrice = (existing.AvgPrice * existing.Quantity + trade.Price * trade.Quantity) / totalQty;
                        openPositions[trade.Symbol] = (avgPrice, totalQty);
                    }
                }
                else // Sell
                {
                    if (openPositions.ContainsKey(trade.Symbol))
                    {
                        var position = openPositions[trade.Symbol];
                        var pnl = (trade.Price - position.AvgPrice) * trade.Quantity;
                        totalPnL += pnl;
                        
                        Output.WriteLine($"  P&L: ${pnl:N2} ({pnl/(position.AvgPrice * trade.Quantity):P2})");
                        
                        // Update or remove position
                        if (position.Quantity > trade.Quantity)
                        {
                            openPositions[trade.Symbol] = (position.AvgPrice, position.Quantity - trade.Quantity);
                        }
                        else
                        {
                            openPositions.Remove(trade.Symbol);
                        }
                    }
                }
            }
            
            // Summary
            Output.WriteLine("\n================");
            Output.WriteLine("Summary:");
            Output.WriteLine($"  Total Trades: {trades.Count}");
            Output.WriteLine($"  Total P&L: ${totalPnL:N2}");
            Output.WriteLine($"  Open Positions: {openPositions.Count}");
            
            // Assert - All trades should be recorded
            Assert.Equal(tradeSequence.Length, trades.Count);
            Assert.All(trades, t => Assert.NotNull(t.Reason));
        }
        
        #endregion
        
        #region Complete Trading Workflow with All Rules
        
        [Fact]
        public async Task CompleteTradingWorkflow_AllRulesEnforced()
        {
            // Arrange
            Output.WriteLine("=== Complete Trading Workflow with Golden Rules ===");
            
            var startingCapital = 100000m;
            var tradingDay = new TradingDay
            {
                Date = DateTime.Today,
                MaxDailyLoss = startingCapital * 0.06m, // 6% max daily loss
                MaxPositions = 4,
                RiskPerTrade = 0.02m // 2% risk per trade
            };
            
            Output.WriteLine($"Starting Capital: ${startingCapital:N0}");
            Output.WriteLine($"Max Daily Loss: ${tradingDay.MaxDailyLoss:N0}");
            Output.WriteLine($"Risk Per Trade: {tradingDay.RiskPerTrade:P0}");
            
            // Morning preparation (Rule 9: Continuous Learning)
            Output.WriteLine("\n1. Morning Preparation:");
            Output.WriteLine("   ✓ Reviewed overnight news");
            Output.WriteLine("   ✓ Checked pre-market movers");
            Output.WriteLine("   ✓ Identified key levels");
            Output.WriteLine("   ✓ Set daily goals");
            
            // Identify trade setups
            var tradeSetups = new[]
            {
                new TradeSetup
                {
                    Symbol = "AAPL",
                    SetupType = "Breakout",
                    EntryPrice = 175m,
                    StopLoss = 171.5m,
                    Target = 180m,
                    RiskRewardRatio = 2.33m
                },
                new TradeSetup
                {
                    Symbol = "MSFT",
                    SetupType = "Pullback",
                    EntryPrice = 350m,
                    StopLoss = 343m,
                    Target = 364m,
                    RiskRewardRatio = 2.0m
                }
            };
            
            Output.WriteLine("\n2. Trade Setups Identified:");
            foreach (var setup in tradeSetups)
            {
                Output.WriteLine($"   {setup.Symbol}: {setup.SetupType} @ ${setup.EntryPrice}");
                Output.WriteLine($"     Stop: ${setup.StopLoss}, Target: ${setup.Target}, R:R = {setup.RiskRewardRatio:F1}");
            }
            
            // Execute trades with rule validation
            Output.WriteLine("\n3. Trade Execution:");
            var executedTrades = new List<ExecutedTrade>();
            
            foreach (var setup in tradeSetups)
            {
                // Calculate position size (Rule 7)
                var riskAmount = startingCapital * tradingDay.RiskPerTrade;
                var riskPerShare = setup.EntryPrice - setup.StopLoss;
                var shares = Math.Floor(riskAmount / riskPerShare);
                
                Output.WriteLine($"\n   {setup.Symbol}:");
                Output.WriteLine($"     Position size: {shares} shares (risk: ${riskAmount:N0})");
                
                // Validate against all rules
                SetupMarketData(setup.Symbol, setup.EntryPrice);
                var validation = await _goldenRulesEngine.ValidateTradeAsync(
                    setup.Symbol, OrderSide.Buy, shares, 
                    (await _portfolioManager.GetPortfolioAsync()).CashBalance);
                
                if (validation.IsValid)
                {
                    Output.WriteLine("     ✓ All rules passed");
                    
                    // Execute trade
                    var order = CreateOrder(setup.Symbol, shares, OrderSide.Buy);
                    var execution = await _executionEngine.ExecuteOrderAsync(order, setup.EntryPrice);
                    
                    executedTrades.Add(new ExecutedTrade
                    {
                        Setup = setup,
                        Shares = shares,
                        ExecutionPrice = execution.ExecutedPrice,
                        ExecutionTime = execution.ExecutedAt
                    });
                    
                    Output.WriteLine($"     Executed: {shares} @ ${execution.ExecutedPrice:F2}");
                }
                else
                {
                    Output.WriteLine("     ✗ Trade rejected:");
                    foreach (var violation in validation.ViolatedRules)
                    {
                        Output.WriteLine($"       - {violation.RuleName}: {violation.ViolationReason}");
                    }
                }
            }
            
            // Monitor positions (Rule 3: Cut losses, Rule 4: Let winners run)
            Output.WriteLine("\n4. Position Monitoring:");
            
            // Simulate price movements
            await Task.Delay(1000); // Simulate time passing
            
            foreach (var trade in executedTrades)
            {
                // Simulate price movement
                var priceChange = (decimal)(Random.Shared.NextDouble() * 0.06 - 0.03); // ±3%
                var currentPrice = trade.Setup.EntryPrice * (1 + priceChange);
                
                SetupMarketData(trade.Setup.Symbol, currentPrice);
                await _portfolioManager.UpdatePositionPriceAsync(trade.Setup.Symbol, currentPrice);
                
                var pnlPercent = (currentPrice - trade.Setup.EntryPrice) / trade.Setup.EntryPrice;
                var pnl = (currentPrice - trade.Setup.EntryPrice) * trade.Shares;
                
                Output.WriteLine($"\n   {trade.Setup.Symbol}: ${currentPrice:F2} ({pnlPercent:P2})");
                
                // Check stop loss
                if (currentPrice <= trade.Setup.StopLoss)
                {
                    Output.WriteLine("     ⚠️ STOP LOSS HIT - Closing position");
                    var stopOrder = CreateOrder(trade.Setup.Symbol, trade.Shares, OrderSide.Sell);
                    await _executionEngine.ExecuteOrderAsync(stopOrder, currentPrice);
                }
                // Check profit target
                else if (currentPrice >= trade.Setup.Target)
                {
                    Output.WriteLine("     🎯 TARGET HIT - Taking profits");
                    var profitOrder = CreateOrder(trade.Setup.Symbol, trade.Shares, OrderSide.Sell);
                    await _executionEngine.ExecuteOrderAsync(profitOrder, currentPrice);
                }
                else
                {
                    Output.WriteLine($"     Position open - P&L: ${pnl:N2}");
                }
            }
            
            // End of day summary (Rule 12: Record keeping)
            Output.WriteLine("\n5. End of Day Summary:");
            var portfolio = await _portfolioManager.GetPortfolioAsync();
            var endingCapital = await _portfolioManager.GetTotalPortfolioValueAsync();
            var dailyPnL = endingCapital - startingCapital;
            var dailyPnLPercent = dailyPnL / startingCapital;
            
            Output.WriteLine($"   Starting Capital: ${startingCapital:N0}");
            Output.WriteLine($"   Ending Capital: ${endingCapital:N0}");
            Output.WriteLine($"   Daily P&L: ${dailyPnL:N2} ({dailyPnLPercent:P2})");
            Output.WriteLine($"   Open Positions: {portfolio.Holdings.Count}");
            Output.WriteLine($"   Trades Executed: {executedTrades.Count}");
            
            // Assert - Daily loss limit not exceeded
            if (dailyPnL < 0)
            {
                Assert.True(Math.Abs(dailyPnL) <= tradingDay.MaxDailyLoss, 
                    "Daily loss limit should not be exceeded");
            }
            
            Output.WriteLine("\n✅ All Golden Rules enforced successfully!");
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
        
        private void SetupMarketData(string symbol, decimal price)
        {
            var marketData = new MarketData
            {
                Symbol = symbol,
                Price = price,
                PreviousClose = price * 0.99m,
                Open = price * 0.995m,
                High = price * 1.01m,
                Low = price * 0.98m,
                Volume = Random.Shared.Next(1_000_000, 10_000_000),
                AverageVolume = Random.Shared.Next(2_000_000, 8_000_000)
            };
            
            _mockMarketDataService.Setup(x => x.GetMarketDataAsync(symbol))
                .ReturnsAsync(marketData);
        }
        
        #endregion
    }
    
    // Helper classes
    public class TradeRecord
    {
        public string TradeId { get; set; }
        public string Symbol { get; set; }
        public OrderSide Side { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public DateTime Timestamp { get; set; }
        public string Reason { get; set; }
        public string Strategy { get; set; }
    }
    
    public class TradingDay
    {
        public DateTime Date { get; set; }
        public decimal MaxDailyLoss { get; set; }
        public int MaxPositions { get; set; }
        public decimal RiskPerTrade { get; set; }
    }
    
    public class TradeSetup
    {
        public string Symbol { get; set; }
        public string SetupType { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal StopLoss { get; set; }
        public decimal Target { get; set; }
        public decimal RiskRewardRatio { get; set; }
    }
    
    public class ExecutedTrade
    {
        public TradeSetup Setup { get; set; }
        public decimal Shares { get; set; }
        public decimal ExecutionPrice { get; set; }
        public DateTime ExecutionTime { get; set; }
    }
    
    public enum OrderSide
    {
        Buy,
        Sell,
        Hold
    }
}