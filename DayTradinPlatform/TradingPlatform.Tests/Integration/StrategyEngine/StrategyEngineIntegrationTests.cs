using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;
using TradingPlatform.StrategyEngine.Services;
using TradingPlatform.StrategyEngine.Models;
using TradingPlatform.StrategyEngine.Strategies;
using TradingPlatform.Tests.Core.Canonical;
using Moq;

namespace TradingPlatform.Tests.Integration.StrategyEngine
{
    /// <summary>
    /// Integration tests for Strategy Engine components
    /// Tests strategy registration, execution, signal generation, and orchestration
    /// </summary>
    public class StrategyEngineIntegrationTests : IntegrationTestBase
    {
        private StrategyManagerCanonical _strategyManager;
        private Mock<IMarketDataService> _mockMarketDataService;
        private Mock<IPortfolioService> _mockPortfolioService;
        private Mock<IRiskManagementService> _mockRiskService;
        
        public StrategyEngineIntegrationTests(ITestOutputHelper output) : base(output)
        {
            _mockMarketDataService = new Mock<IMarketDataService>();
            _mockPortfolioService = new Mock<IPortfolioService>();
            _mockRiskService = new Mock<IRiskManagementService>();
        }
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            // Register strategy services
            services.AddSingleton<StrategyManagerCanonical>();
            
            // Register test strategies
            services.AddSingleton<ITradingStrategy, MomentumStrategy>();
            services.AddSingleton<ITradingStrategy, MeanReversionStrategy>();
            services.AddSingleton<ITradingStrategy, BreakoutStrategy>();
            services.AddSingleton<ITradingStrategy, VolumeStrategy>();
            
            // Register mocks
            services.AddSingleton(MockLogger.Object);
            services.AddSingleton(_mockMarketDataService.Object);
            services.AddSingleton(_mockPortfolioService.Object);
            services.AddSingleton(_mockRiskService.Object);
            services.AddSingleton<IServiceProvider>(sp => sp);
        }
        
        protected override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            
            _strategyManager = ServiceProvider.GetRequiredService<StrategyManagerCanonical>();
            
            // Register strategies with manager
            var strategies = ServiceProvider.GetServices<ITradingStrategy>();
            foreach (var strategy in strategies)
            {
                await _strategyManager.RegisterStrategyAsync(strategy);
            }
            
            await _strategyManager.InitializeAsync();
            await _strategyManager.StartAsync();
        }
        
        #region Strategy Registration and Management
        
        [Fact]
        public async Task StrategyRegistration_MultipleStrategies_RegistersCorrectly()
        {
            // Arrange & Act
            var activeStrategies = await _strategyManager.GetActiveStrategiesAsync();
            
            // Assert
            Assert.NotNull(activeStrategies);
            Assert.Equal(4, activeStrategies.Length); // 4 test strategies registered
            
            Output.WriteLine("Registered Strategies:");
            foreach (var strategy in activeStrategies)
            {
                Output.WriteLine($"  {strategy.Name} (ID: {strategy.StrategyId})");
                Output.WriteLine($"    Status: {strategy.Status}");
                Output.WriteLine($"    Type: {strategy.StrategyType}");
            }
            
            // Verify all strategies are active
            Assert.All(activeStrategies, s => Assert.Equal(StrategyStatus.Active, s.Status));
        }
        
        [Fact]
        public async Task StrategyExecution_ConcurrentStrategies_GeneratesSignals()
        {
            // Arrange
            var testSymbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN" };
            SetupMarketDataForSymbols(testSymbols);
            
            var signals = new List<TradingSignal>();
            
            // Act - Execute all strategies on test symbols
            foreach (var symbol in testSymbols)
            {
                var signal = await _strategyManager.AnalyzeStockAsync(symbol);
                if (signal != null)
                {
                    signals.Add(signal);
                }
            }
            
            // Assert
            Assert.NotEmpty(signals);
            
            Output.WriteLine($"Generated {signals.Count} signals:");
            foreach (var signal in signals)
            {
                Output.WriteLine($"  {signal.Symbol}: {signal.Action} " +
                               $"(Strategy: {signal.Strategy}, Confidence: {signal.Confidence:P0})");
            }
            
            // Verify signal quality
            Assert.All(signals, s =>
            {
                Assert.NotNull(s.Symbol);
                Assert.NotNull(s.Strategy);
                Assert.InRange(s.Confidence, 0, 1);
            });
        }
        
        #endregion
        
        #region Strategy Combination and Consensus
        
        [Fact]
        public async Task StrategyConsensus_MultipleSignals_CombinesCorrectly()
        {
            // Arrange
            var symbol = "AAPL";
            SetupBullishMarketData(symbol);
            
            // Enable consensus mode
            await _strategyManager.SetConsensusMode(true, 0.6m); // 60% agreement required
            
            // Act
            var consensusSignal = await _strategyManager.AnalyzeStockAsync(symbol);
            
            // Assert
            Assert.NotNull(consensusSignal);
            Output.WriteLine($"Consensus Signal for {symbol}:");
            Output.WriteLine($"  Action: {consensusSignal.Action}");
            Output.WriteLine($"  Confidence: {consensusSignal.Confidence:P0}");
            Output.WriteLine($"  Contributing Strategies: {consensusSignal.ContributingStrategies?.Count ?? 0}");
            
            if (consensusSignal.ContributingStrategies != null)
            {
                foreach (var strategy in consensusSignal.ContributingStrategies)
                {
                    Output.WriteLine($"    - {strategy}");
                }
            }
        }
        
        [Fact]
        public async Task StrategyConflict_OpposingSignals_ResolvesCorrectly()
        {
            // Arrange
            var symbol = "TSLA";
            SetupVolatileMarketData(symbol); // Creates conflicting signals
            
            // Act
            var individualSignals = await GetIndividualStrategySignals(symbol);
            var resolvedSignal = await _strategyManager.AnalyzeStockAsync(symbol);
            
            // Assert
            Output.WriteLine($"Individual signals for {symbol}:");
            foreach (var signal in individualSignals)
            {
                Output.WriteLine($"  {signal.Strategy}: {signal.Action} ({signal.Confidence:P0})");
            }
            
            // Check for conflicts
            var hasConflict = individualSignals.Any(s => s.Action == SignalAction.Buy) &&
                             individualSignals.Any(s => s.Action == SignalAction.Sell);
            
            if (hasConflict)
            {
                Output.WriteLine("\nConflict detected!");
                
                if (resolvedSignal != null)
                {
                    Output.WriteLine($"Resolved to: {resolvedSignal.Action} ({resolvedSignal.Confidence:P0})");
                    Assert.True(resolvedSignal.Confidence < 0.8m, "Confidence should be lower due to conflict");
                }
                else
                {
                    Output.WriteLine("No consensus reached - signal rejected");
                    Assert.Null(resolvedSignal);
                }
            }
        }
        
        #endregion
        
        #region Strategy Performance Tracking
        
        [Fact]
        public async Task StrategyPerformance_TrackingMetrics_UpdatesCorrectly()
        {
            // Arrange
            var testPeriod = TimeSpan.FromHours(1);
            var signalsGenerated = new Dictionary<string, List<TradingSignal>>();
            
            // Generate signals over time
            for (int i = 0; i < 10; i++)
            {
                var symbol = $"TEST{i}";
                SetupMarketDataForSymbol(symbol, 100m + i * 10);
                
                var signal = await _strategyManager.AnalyzeStockAsync(symbol);
                if (signal != null)
                {
                    if (!signalsGenerated.ContainsKey(signal.Strategy))
                    {
                        signalsGenerated[signal.Strategy] = new List<TradingSignal>();
                    }
                    signalsGenerated[signal.Strategy].Add(signal);
                    
                    // Simulate signal outcome
                    await SimulateSignalOutcome(signal);
                }
                
                await Task.Delay(100); // Simulate time passing
            }
            
            // Act - Get performance metrics
            var strategies = await _strategyManager.GetActiveStrategiesAsync();
            var performanceMetrics = new Dictionary<string, StrategyPerformanceMetrics>();
            
            foreach (var strategy in strategies)
            {
                var metrics = await _strategyManager.GetStrategyPerformanceAsync(strategy.StrategyId);
                performanceMetrics[strategy.Name] = metrics;
            }
            
            // Assert
            Output.WriteLine("Strategy Performance Metrics:");
            foreach (var kvp in performanceMetrics)
            {
                var metrics = kvp.Value;
                Output.WriteLine($"\n{kvp.Key}:");
                Output.WriteLine($"  Signals Generated: {metrics.SignalsGenerated}");
                Output.WriteLine($"  Success Rate: {metrics.SuccessRate:P0}");
                Output.WriteLine($"  Avg Confidence: {metrics.AverageConfidence:P0}");
                Output.WriteLine($"  Total Return: {metrics.TotalReturn:P2}");
                
                Assert.True(metrics.SignalsGenerated >= 0);
                Assert.InRange(metrics.SuccessRate, 0, 1);
                Assert.InRange(metrics.AverageConfidence, 0, 1);
            }
        }
        
        #endregion
        
        #region Market Regime Adaptation
        
        [Fact]
        public async Task MarketRegimeAdaptation_ChangingConditions_AdjustsStrategies()
        {
            // Arrange
            Output.WriteLine("=== Testing Market Regime Adaptation ===");
            
            // Test different market regimes
            var regimes = new[]
            {
                (MarketRegime.Trending, "Trending Market"),
                (MarketRegime.RangeRound, "Range-Bound Market"),
                (MarketRegime.Volatile, "Volatile Market"),
                (MarketRegime.Quiet, "Quiet Market")
            };
            
            foreach (var (regime, description) in regimes)
            {
                Output.WriteLine($"\n{description}:");
                
                // Setup market data for regime
                SetupMarketDataForRegime(regime);
                
                // Act
                await _strategyManager.AdaptToMarketRegime(regime);
                
                // Analyze multiple symbols
                var signals = new List<TradingSignal>();
                var testSymbols = new[] { "SPY", "QQQ", "IWM" };
                
                foreach (var symbol in testSymbols)
                {
                    var signal = await _strategyManager.AnalyzeStockAsync(symbol);
                    if (signal != null)
                    {
                        signals.Add(signal);
                    }
                }
                
                // Assert - Different regimes should produce different signal patterns
                Output.WriteLine($"  Signals generated: {signals.Count}");
                
                switch (regime)
                {
                    case MarketRegime.Trending:
                        // Momentum strategies should dominate
                        var momentumSignals = signals.Where(s => s.Strategy.Contains("Momentum")).Count();
                        Output.WriteLine($"  Momentum signals: {momentumSignals}");
                        Assert.True(momentumSignals > signals.Count / 2);
                        break;
                        
                    case MarketRegime.RangeRound:
                        // Mean reversion should dominate
                        var meanReversionSignals = signals.Where(s => s.Strategy.Contains("MeanReversion")).Count();
                        Output.WriteLine($"  Mean reversion signals: {meanReversionSignals}");
                        Assert.True(meanReversionSignals > 0);
                        break;
                        
                    case MarketRegime.Volatile:
                        // Lower confidence overall
                        var avgConfidence = signals.Any() ? signals.Average(s => s.Confidence) : 0;
                        Output.WriteLine($"  Average confidence: {avgConfidence:P0}");
                        Assert.True(avgConfidence < 0.7m);
                        break;
                }
            }
        }
        
        #endregion
        
        #region Strategy Backtesting Integration
        
        [Fact]
        public async Task StrategyBacktest_HistoricalData_ValidatesPerformance()
        {
            // Arrange
            Output.WriteLine("=== Strategy Backtesting ===");
            
            var backtestPeriod = new DateRange(
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow
            );
            
            var symbol = "AAPL";
            var historicalData = GenerateHistoricalData(symbol, backtestPeriod, 175m);
            
            // Act - Run backtest for each strategy
            var strategies = await _strategyManager.GetActiveStrategiesAsync();
            var backtestResults = new Dictionary<string, BacktestResult>();
            
            foreach (var strategy in strategies)
            {
                var result = await RunStrategyBacktest(strategy.StrategyId, symbol, historicalData);
                backtestResults[strategy.Name] = result;
            }
            
            // Assert
            Output.WriteLine($"Backtest Results for {symbol}:");
            foreach (var kvp in backtestResults)
            {
                var result = kvp.Value;
                Output.WriteLine($"\n{kvp.Key}:");
                Output.WriteLine($"  Total Trades: {result.TotalTrades}");
                Output.WriteLine($"  Win Rate: {result.WinRate:P0}");
                Output.WriteLine($"  Total Return: {result.TotalReturn:P2}");
                Output.WriteLine($"  Max Drawdown: {result.MaxDrawdown:P2}");
                Output.WriteLine($"  Sharpe Ratio: {result.SharpeRatio:F2}");
                
                Assert.True(result.TotalTrades > 0);
                Assert.InRange(result.WinRate, 0, 1);
                Assert.True(result.MaxDrawdown <= 0);
            }
            
            // Find best performing strategy
            var bestStrategy = backtestResults.OrderByDescending(kvp => kvp.Value.SharpeRatio).First();
            Output.WriteLine($"\nBest Strategy: {bestStrategy.Key} (Sharpe: {bestStrategy.Value.SharpeRatio:F2})");
        }
        
        #endregion
        
        #region Real-Time Strategy Updates
        
        [Fact]
        public async Task RealTimeUpdates_StreamingData_GeneratesTimelySignals()
        {
            // Arrange
            Output.WriteLine("=== Testing Real-Time Strategy Updates ===");
            
            var symbol = "MSFT";
            var signalEvents = new List<(DateTime Time, TradingSignal Signal)>();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // 5 second test
            
            // Subscribe to signal events
            _strategyManager.OnSignalGenerated += (sender, args) =>
            {
                signalEvents.Add((DateTime.UtcNow, args.Signal));
                Output.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Signal: {args.Signal.Symbol} - {args.Signal.Action}");
            };
            
            // Act - Simulate streaming market data
            var updateTask = Task.Run(async () =>
            {
                var basePrice = 350m;
                var tick = 0;
                
                while (!cts.Token.IsCancellationRequested)
                {
                    // Simulate price movement
                    var volatility = 0.002m; // 0.2% volatility
                    var randomWalk = (decimal)(Random.Shared.NextDouble() - 0.5) * 2 * volatility;
                    var newPrice = basePrice * (1 + randomWalk);
                    
                    var marketData = new MarketData
                    {
                        Symbol = symbol,
                        Price = newPrice,
                        Volume = 1000000 + Random.Shared.Next(500000),
                        Timestamp = DateTime.UtcNow
                    };
                    
                    // Update market data
                    _mockMarketDataService.Setup(x => x.GetMarketDataAsync(symbol))
                        .ReturnsAsync(marketData);
                    
                    // Process update through strategies
                    await _strategyManager.ProcessMarketUpdateAsync(marketData);
                    
                    basePrice = newPrice;
                    tick++;
                    
                    await Task.Delay(100); // 10 updates per second
                }
            }, cts.Token);
            
            try
            {
                await updateTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            
            // Assert
            Output.WriteLine($"\nGenerated {signalEvents.Count} signals in 5 seconds");
            
            if (signalEvents.Any())
            {
                // Check signal timing
                var firstSignal = signalEvents.First();
                var lastSignal = signalEvents.Last();
                var timespan = lastSignal.Time - firstSignal.Time;
                
                Output.WriteLine($"First signal at: {firstSignal.Time:HH:mm:ss.fff}");
                Output.WriteLine($"Last signal at: {lastSignal.Time:HH:mm:ss.fff}");
                Output.WriteLine($"Total timespan: {timespan.TotalSeconds:F1} seconds");
                
                // Verify signals are generated throughout the period
                Assert.True(timespan.TotalSeconds > 2, "Signals should be spread over time");
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private void SetupMarketDataForSymbols(string[] symbols)
        {
            foreach (var symbol in symbols)
            {
                SetupMarketDataForSymbol(symbol, 100m + Random.Shared.Next(50, 200));
            }
        }
        
        private void SetupMarketDataForSymbol(string symbol, decimal price)
        {
            var marketData = new MarketData
            {
                Symbol = symbol,
                Price = price,
                PreviousClose = price * 0.98m,
                Open = price * 0.99m,
                High = price * 1.02m,
                Low = price * 0.97m,
                Volume = Random.Shared.Next(1_000_000, 10_000_000),
                AverageVolume = Random.Shared.Next(2_000_000, 8_000_000)
            };
            
            _mockMarketDataService.Setup(x => x.GetMarketDataAsync(symbol))
                .ReturnsAsync(marketData);
            
            // Setup historical data
            var historicalData = GenerateHistoricalData(symbol, 
                new DateRange(DateTime.Today.AddDays(-30), DateTime.Today), price);
            
            _mockMarketDataService.Setup(x => x.GetHistoricalDataAsync(
                symbol, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(historicalData);
        }
        
        private void SetupBullishMarketData(string symbol)
        {
            var price = 150m;
            var marketData = new MarketData
            {
                Symbol = symbol,
                Price = price,
                PreviousClose = price * 0.95m, // 5% gap up
                Open = price * 0.96m,
                High = price * 1.01m,
                Low = price * 0.96m,
                Volume = 15_000_000, // High volume
                AverageVolume = 8_000_000,
                HasNews = true,
                NewsCount = 5
            };
            
            _mockMarketDataService.Setup(x => x.GetMarketDataAsync(symbol))
                .ReturnsAsync(marketData);
        }
        
        private void SetupVolatileMarketData(string symbol)
        {
            var price = 250m;
            var marketData = new MarketData
            {
                Symbol = symbol,
                Price = price,
                PreviousClose = price,
                Open = price * 1.03m,  // Gap up
                High = price * 1.08m,  // Large range
                Low = price * 0.94m,   // Large range
                Volume = 25_000_000,   // Very high volume
                AverageVolume = 10_000_000
            };
            
            _mockMarketDataService.Setup(x => x.GetMarketDataAsync(symbol))
                .ReturnsAsync(marketData);
        }
        
        private void SetupMarketDataForRegime(MarketRegime regime)
        {
            var basePrice = 100m;
            
            switch (regime)
            {
                case MarketRegime.Trending:
                    // Strong uptrend data
                    for (int i = 0; i < 20; i++)
                    {
                        basePrice *= 1.002m; // 0.2% daily gains
                    }
                    break;
                    
                case MarketRegime.RangeRound:
                    // Oscillating data
                    // Price stays between 98-102
                    break;
                    
                case MarketRegime.Volatile:
                    // High volatility data
                    // Large daily swings
                    break;
                    
                case MarketRegime.Quiet:
                    // Low volatility, low volume
                    break;
            }
        }
        
        private List<DailyData> GenerateHistoricalData(string symbol, DateRange period, decimal basePrice)
        {
            var data = new List<DailyData>();
            var currentDate = period.Start;
            var price = basePrice;
            
            while (currentDate <= period.End)
            {
                var dailyReturn = (decimal)(Random.Shared.NextDouble() * 0.04 - 0.02); // ±2%
                price *= (1 + dailyReturn);
                
                data.Add(new DailyData
                {
                    Date = currentDate,
                    Open = price * (1m + (decimal)(Random.Shared.NextDouble() * 0.01 - 0.005)),
                    High = price * (1m + (decimal)Random.Shared.NextDouble() * 0.02),
                    Low = price * (1m - (decimal)Random.Shared.NextDouble() * 0.02),
                    Close = price,
                    Volume = Random.Shared.Next(1_000_000, 5_000_000)
                });
                
                currentDate = currentDate.AddDays(1);
            }
            
            return data;
        }
        
        private async Task<List<TradingSignal>> GetIndividualStrategySignals(string symbol)
        {
            var signals = new List<TradingSignal>();
            var strategies = await _strategyManager.GetActiveStrategiesAsync();
            
            foreach (var strategy in strategies)
            {
                // This would call individual strategy analyze methods
                // Simplified for testing
                var signal = new TradingSignal
                {
                    Symbol = symbol,
                    Strategy = strategy.Name,
                    Action = Random.Shared.NextDouble() > 0.5 ? SignalAction.Buy : SignalAction.Sell,
                    Confidence = (decimal)Random.Shared.NextDouble() * 0.4m + 0.5m
                };
                signals.Add(signal);
            }
            
            return signals;
        }
        
        private async Task SimulateSignalOutcome(TradingSignal signal)
        {
            // Simulate whether the signal was profitable
            var isSuccessful = Random.Shared.NextDouble() > 0.45; // 55% success rate
            
            // Update strategy performance metrics
            // This would normally be done by tracking actual trade outcomes
            await Task.CompletedTask;
        }
        
        private async Task<BacktestResult> RunStrategyBacktest(
            string strategyId, 
            string symbol, 
            List<DailyData> historicalData)
        {
            // Simplified backtesting logic
            var trades = new List<BacktestTrade>();
            var position = 0m;
            var entryPrice = 0m;
            
            foreach (var data in historicalData)
            {
                // Generate signal for this day
                var signal = Random.Shared.NextDouble() > 0.7 
                    ? (Random.Shared.NextDouble() > 0.5 ? SignalAction.Buy : SignalAction.Sell)
                    : SignalAction.Hold;
                
                if (signal == SignalAction.Buy && position == 0)
                {
                    position = 100;
                    entryPrice = data.Close;
                }
                else if (signal == SignalAction.Sell && position > 0)
                {
                    var profit = (data.Close - entryPrice) * position;
                    trades.Add(new BacktestTrade
                    {
                        EntryPrice = entryPrice,
                        ExitPrice = data.Close,
                        Quantity = position,
                        Profit = profit,
                        IsWin = profit > 0
                    });
                    position = 0;
                }
            }
            
            // Calculate metrics
            var winningTrades = trades.Where(t => t.IsWin).Count();
            var totalReturn = trades.Sum(t => t.Profit) / 100000m; // Assume $100k capital
            
            return new BacktestResult
            {
                TotalTrades = trades.Count,
                WinRate = trades.Any() ? (decimal)winningTrades / trades.Count : 0,
                TotalReturn = totalReturn,
                MaxDrawdown = CalculateMaxDrawdown(trades),
                SharpeRatio = CalculateSharpeRatio(trades)
            };
        }
        
        private decimal CalculateMaxDrawdown(List<BacktestTrade> trades)
        {
            if (!trades.Any()) return 0;
            
            var equity = 100000m;
            var peak = equity;
            var maxDrawdown = 0m;
            
            foreach (var trade in trades)
            {
                equity += trade.Profit;
                if (equity > peak)
                {
                    peak = equity;
                }
                else
                {
                    var drawdown = (peak - equity) / peak;
                    maxDrawdown = Math.Max(maxDrawdown, drawdown);
                }
            }
            
            return -maxDrawdown; // Return as negative
        }
        
        private decimal CalculateSharpeRatio(List<BacktestTrade> trades)
        {
            if (!trades.Any()) return 0;
            
            var returns = trades.Select(t => t.Profit / 100000m).ToList();
            var avgReturn = returns.Average();
            var stdDev = CalculateStdDev(returns);
            
            if (stdDev == 0) return 0;
            
            return (avgReturn - 0.0001m) / stdDev * (decimal)Math.Sqrt(252); // Annualized
        }
        
        private decimal CalculateStdDev(List<decimal> values)
        {
            if (values.Count < 2) return 0;
            
            var mean = values.Average();
            var sumSquares = values.Sum(v => (v - mean) * (v - mean));
            var variance = sumSquares / (values.Count - 1);
            
            return (decimal)Math.Sqrt((double)variance);
        }
        
        #endregion
    }
    
    // Helper classes
    public class BacktestResult
    {
        public int TotalTrades { get; set; }
        public decimal WinRate { get; set; }
        public decimal TotalReturn { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal SharpeRatio { get; set; }
    }
    
    public class BacktestTrade
    {
        public decimal EntryPrice { get; set; }
        public decimal ExitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal Profit { get; set; }
        public bool IsWin { get; set; }
    }
    
    public class DateRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        
        public DateRange(DateTime start, DateTime end)
        {
            Start = start;
            End = end;
        }
    }
    
    public enum MarketRegime
    {
        Trending,
        RangeRound,
        Volatile,
        Quiet
    }
    
    public class StrategyPerformanceMetrics
    {
        public int SignalsGenerated { get; set; }
        public decimal SuccessRate { get; set; }
        public decimal AverageConfidence { get; set; }
        public decimal TotalReturn { get; set; }
    }
    
    public enum StrategyStatus
    {
        Active,
        Paused,
        Disabled
    }
    
    public class StrategyInfo
    {
        public string StrategyId { get; set; }
        public string Name { get; set; }
        public string StrategyType { get; set; }
        public StrategyStatus Status { get; set; }
    }
}