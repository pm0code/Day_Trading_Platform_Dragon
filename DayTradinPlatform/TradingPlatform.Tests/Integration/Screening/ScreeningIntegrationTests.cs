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
using TradingPlatform.Screening.Engines;
using TradingPlatform.Screening.Criteria;
using TradingPlatform.Screening.Models;
using TradingPlatform.Tests.Core.Canonical;
using Moq;

namespace TradingPlatform.Tests.Integration.Screening
{
    /// <summary>
    /// Integration tests for Screening module
    /// Verifies that screening criteria and engines work together correctly
    /// </summary>
    public class ScreeningIntegrationTests : IntegrationTestBase
    {
        private RealTimeScreeningEngineCanonical _screeningEngine;
        private ScreeningOrchestratorCanonical _screeningOrchestrator;
        private Mock<IMarketDataService> _mockMarketDataService;
        private Mock<IAlertService> _mockAlertService;
        
        public ScreeningIntegrationTests(ITestOutputHelper output) : base(output)
        {
            _mockMarketDataService = new Mock<IMarketDataService>();
            _mockAlertService = new Mock<IAlertService>();
        }
        
        protected override void ConfigureServices(IServiceCollection services)
        {
            // Register screening services
            services.AddScreeningServices();
            
            // Register mocks
            services.AddSingleton(MockLogger.Object);
            services.AddSingleton(_mockMarketDataService.Object);
            services.AddSingleton(_mockAlertService.Object);
            services.AddSingleton(Mock.Of<IDataIngestionService>());
        }
        
        protected override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            
            _screeningEngine = ServiceProvider.GetRequiredService<RealTimeScreeningEngineCanonical>();
            _screeningOrchestrator = ServiceProvider.GetRequiredService<ScreeningOrchestratorCanonical>();
            
            await _screeningEngine.InitializeAsync();
            await _screeningOrchestrator.InitializeAsync();
        }
        
        #region Multi-Criteria Screening Tests
        
        [Fact]
        public async Task ScreenStocks_MultipleCriteria_FiltersCorrectly()
        {
            // Arrange
            var screeningCriteria = new ScreeningCriteria
            {
                Name = "High Volume Breakout",
                PriceMin = 10m,
                PriceMax = 100m,
                VolumeMultiplier = 2.0m,
                VolatilityMin = 0.02m,
                GapPercentMin = 0.02m,
                RequiresNews = true
            };
            
            var stocks = CreateTestStocks();
            SetupMarketDataForStocks(stocks);
            
            // Act
            var results = await _screeningEngine.ScreenStocksAsync(
                stocks.Select(s => s.Symbol).ToArray(), 
                screeningCriteria);
            
            // Assert
            Assert.NotNull(results);
            Assert.True(results.Count > 0);
            Assert.True(results.Count < stocks.Count); // Some should be filtered out
            
            // Verify all results meet criteria
            foreach (var result in results)
            {
                Assert.InRange(result.Price, screeningCriteria.PriceMin, screeningCriteria.PriceMax);
                Assert.True(result.VolumeRatio >= screeningCriteria.VolumeMultiplier);
                Assert.True(result.Volatility >= screeningCriteria.VolatilityMin);
                Assert.True(Math.Abs(result.GapPercent) >= screeningCriteria.GapPercentMin);
                Assert.True(result.HasNews);
                
                Output.WriteLine($"{result.Symbol}: Price={result.Price:C}, " +
                               $"Volume={result.VolumeRatio:F1}x, " +
                               $"Gap={result.GapPercent:P2}, " +
                               $"Score={result.Score:F2}");
            }
        }
        
        [Fact]
        public async Task ScreeningOrchestrator_MultipleStrategies_CombinesResults()
        {
            // Arrange
            var momentumCriteria = new ScreeningCriteria
            {
                Name = "Momentum",
                PriceMin = 20m,
                VolumeMultiplier = 1.5m,
                GapPercentMin = 0.01m
            };
            
            var breakoutCriteria = new ScreeningCriteria
            {
                Name = "Breakout",
                VolatilityMin = 0.03m,
                VolumeMultiplier = 3.0m,
                RequiresNews = true
            };
            
            var stocks = CreateTestStocks();
            SetupMarketDataForStocks(stocks);
            
            // Act
            var momentumResults = await _screeningOrchestrator.RunScreeningAsync(
                "Momentum", momentumCriteria);
            var breakoutResults = await _screeningOrchestrator.RunScreeningAsync(
                "Breakout", breakoutCriteria);
            
            // Assert
            Assert.NotNull(momentumResults);
            Assert.NotNull(breakoutResults);
            
            // Check for overlapping results (stocks that meet both criteria)
            var overlapping = momentumResults.Results
                .Select(r => r.Symbol)
                .Intersect(breakoutResults.Results.Select(r => r.Symbol))
                .ToList();
            
            Output.WriteLine($"Momentum strategy: {momentumResults.Results.Count} stocks");
            Output.WriteLine($"Breakout strategy: {breakoutResults.Results.Count} stocks");
            Output.WriteLine($"Overlapping: {overlapping.Count} stocks");
            
            if (overlapping.Any())
            {
                Output.WriteLine($"Stocks meeting both criteria: {string.Join(", ", overlapping)}");
            }
        }
        
        #endregion
        
        #region Real-Time Screening Tests
        
        [Fact]
        public async Task RealTimeScreening_MarketDataUpdates_TriggersAlerts()
        {
            // Arrange
            var alertsTriggered = new List<(string Symbol, string Reason)>();
            _mockAlertService.Setup(x => x.SendAlertAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((symbol, reason) => alertsTriggered.Add((symbol, reason)))
                .Returns(Task.CompletedTask);
            
            var criteria = new ScreeningCriteria
            {
                Name = "Volume Spike Alert",
                VolumeMultiplier = 5.0m,
                PriceMin = 50m
            };
            
            // Start screening
            await _screeningEngine.StartAsync();
            
            // Simulate market data updates
            var updates = new[]
            {
                CreateMarketData("AAPL", 175m, 150_000_000), // Huge volume
                CreateMarketData("MSFT", 350m, 50_000_000),  // Normal volume
                CreateMarketData("GOOGL", 140m, 100_000_000) // High volume
            };
            
            // Act
            foreach (var update in updates)
            {
                await _screeningEngine.ProcessMarketDataAsync(update);
            }
            
            // Give time for processing
            await Task.Delay(100);
            
            // Assert
            Assert.NotEmpty(alertsTriggered);
            Assert.Contains(alertsTriggered, a => a.Symbol == "AAPL");
            Assert.Contains(alertsTriggered, a => a.Symbol == "GOOGL");
            Assert.DoesNotContain(alertsTriggered, a => a.Symbol == "MSFT");
            
            Output.WriteLine($"Alerts triggered: {alertsTriggered.Count}");
            foreach (var alert in alertsTriggered)
            {
                Output.WriteLine($"  {alert.Symbol}: {alert.Reason}");
            }
        }
        
        #endregion
        
        #region Criteria Combination Tests
        
        [Fact]
        public async Task CompositeCriteria_AllMustPass_FiltersStrictly()
        {
            // Arrange
            var strictCriteria = new ScreeningCriteria
            {
                Name = "Strict Filter",
                PriceMin = 50m,
                PriceMax = 200m,
                VolumeMultiplier = 2.0m,
                VolatilityMin = 0.02m,
                VolatilityMax = 0.05m,
                GapPercentMin = 0.03m,
                RequiresNews = true,
                RequiresPositiveTrend = true
            };
            
            var stocks = CreateDiverseTestStocks();
            SetupMarketDataForStocks(stocks);
            
            // Act
            var results = await _screeningEngine.ScreenStocksAsync(
                stocks.Select(s => s.Symbol).ToArray(), 
                strictCriteria);
            
            // Assert
            // With strict criteria, we expect very few results
            Assert.True(results.Count < stocks.Count / 4);
            
            // Verify each result meets ALL criteria
            foreach (var result in results)
            {
                var marketData = await _mockMarketDataService.Object.GetMarketDataAsync(result.Symbol);
                Assert.NotNull(marketData);
                
                // Price range
                Assert.InRange(result.Price, strictCriteria.PriceMin, strictCriteria.PriceMax);
                
                // Volume
                Assert.True(result.VolumeRatio >= strictCriteria.VolumeMultiplier);
                
                // Volatility
                Assert.InRange(result.Volatility, strictCriteria.VolatilityMin, strictCriteria.VolatilityMax);
                
                // Gap
                Assert.True(Math.Abs(result.GapPercent) >= strictCriteria.GapPercentMin);
                
                // News and trend
                Assert.True(result.HasNews);
                Assert.True(result.TrendDirection == TrendDirection.Up);
            }
            
            Output.WriteLine($"Strict filter results: {results.Count}/{stocks.Count} stocks passed");
        }
        
        [Fact]
        public async Task WeightedScoring_PrioritizesCriteria_RanksCorrectly()
        {
            // Arrange
            var weightedCriteria = new ScreeningCriteria
            {
                Name = "Weighted Scoring",
                PriceMin = 10m,
                VolumeMultiplier = 1.5m,
                CriteriaWeights = new Dictionary<string, decimal>
                {
                    ["Volume"] = 0.4m,
                    ["Gap"] = 0.3m,
                    ["Volatility"] = 0.2m,
                    ["News"] = 0.1m
                }
            };
            
            var stocks = CreateTestStocks();
            SetupMarketDataForStocks(stocks);
            
            // Act
            var results = await _screeningEngine.ScreenStocksAsync(
                stocks.Select(s => s.Symbol).ToArray(), 
                weightedCriteria);
            
            // Assert
            Assert.NotEmpty(results);
            
            // Results should be sorted by score
            var scores = results.Select(r => r.Score).ToList();
            Assert.Equal(scores.OrderByDescending(s => s).ToList(), scores);
            
            // Top results should have high volume (highest weight)
            var topResult = results.First();
            Assert.True(topResult.VolumeRatio >= 3.0m); // Should prioritize high volume
            
            Output.WriteLine("Weighted scoring results:");
            foreach (var result in results.Take(5))
            {
                Output.WriteLine($"  {result.Symbol}: Score={result.Score:F2}, " +
                               $"Volume={result.VolumeRatio:F1}x, " +
                               $"Gap={result.GapPercent:P2}");
            }
        }
        
        #endregion
        
        #region Performance Tests
        
        [Fact]
        public async Task LargeScaleScreening_PerformsEfficiently()
        {
            // Arrange - 1000 stocks
            var stocks = CreateLargeStockUniverse(1000);
            SetupMarketDataForStocks(stocks);
            
            var criteria = new ScreeningCriteria
            {
                Name = "Performance Test",
                PriceMin = 5m,
                VolumeMultiplier = 1.5m
            };
            
            // Act & Assert - Should complete within reasonable time
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var results = await _screeningEngine.ScreenStocksAsync(
                stocks.Select(s => s.Symbol).ToArray(), 
                criteria);
            
            stopwatch.Stop();
            
            Assert.NotNull(results);
            Assert.True(stopwatch.ElapsedMilliseconds < 1000); // Should complete in under 1 second
            
            Output.WriteLine($"Screened {stocks.Count} stocks in {stopwatch.ElapsedMilliseconds}ms");
            Output.WriteLine($"Results: {results.Count} stocks passed criteria");
            Output.WriteLine($"Performance: {stocks.Count / (stopwatch.ElapsedMilliseconds / 1000.0):F0} stocks/second");
        }
        
        #endregion
        
        #region Helper Methods
        
        private List<Stock> CreateTestStocks()
        {
            return new List<Stock>
            {
                new() { Symbol = "AAPL", Name = "Apple Inc" },
                new() { Symbol = "MSFT", Name = "Microsoft Corp" },
                new() { Symbol = "GOOGL", Name = "Alphabet Inc" },
                new() { Symbol = "AMZN", Name = "Amazon.com Inc" },
                new() { Symbol = "TSLA", Name = "Tesla Inc" },
                new() { Symbol = "META", Name = "Meta Platforms" },
                new() { Symbol = "NVDA", Name = "NVIDIA Corp" },
                new() { Symbol = "JPM", Name = "JPMorgan Chase" }
            };
        }
        
        private List<Stock> CreateDiverseTestStocks()
        {
            var stocks = new List<Stock>();
            var sectors = new[] { "Tech", "Finance", "Healthcare", "Energy", "Consumer" };
            
            for (int i = 0; i < 50; i++)
            {
                stocks.Add(new Stock
                {
                    Symbol = $"TEST{i:D3}",
                    Name = $"Test Company {i}",
                    Sector = sectors[i % sectors.Length]
                });
            }
            
            return stocks;
        }
        
        private List<Stock> CreateLargeStockUniverse(int count)
        {
            var stocks = new List<Stock>();
            for (int i = 0; i < count; i++)
            {
                stocks.Add(new Stock
                {
                    Symbol = $"STK{i:D4}",
                    Name = $"Stock {i}"
                });
            }
            return stocks;
        }
        
        private void SetupMarketDataForStocks(List<Stock> stocks)
        {
            var random = new Random(42);
            
            foreach (var stock in stocks)
            {
                var basePrice = 20m + random.Next(0, 180);
                var previousClose = basePrice * (1m + (decimal)(random.NextDouble() * 0.1 - 0.05));
                var volume = random.Next(500_000, 10_000_000);
                var avgVolume = volume / (1m + (decimal)(random.NextDouble() * 3));
                
                var marketData = new MarketData
                {
                    Symbol = stock.Symbol,
                    Price = basePrice,
                    PreviousClose = previousClose,
                    Volume = volume,
                    AverageVolume = avgVolume,
                    High = basePrice * 1.02m,
                    Low = basePrice * 0.98m,
                    Open = previousClose,
                    MarketCap = basePrice * random.Next(100_000_000, 1_000_000_000),
                    HasNews = random.NextDouble() > 0.7,
                    NewsCount = random.NextDouble() > 0.7 ? random.Next(1, 5) : 0
                };
                
                _mockMarketDataService.Setup(x => x.GetMarketDataAsync(stock.Symbol))
                    .ReturnsAsync(marketData);
                
                // Setup historical data for volatility
                var historicalData = GenerateHistoricalData(stock.Symbol, basePrice, 20);
                _mockMarketDataService.Setup(x => x.GetHistoricalDataAsync(
                    stock.Symbol, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                    .ReturnsAsync(historicalData);
            }
        }
        
        private MarketData CreateMarketData(string symbol, decimal price, long volume)
        {
            return new MarketData
            {
                Symbol = symbol,
                Price = price,
                Volume = volume,
                AverageVolume = volume / 5, // Assume 5x normal volume
                PreviousClose = price * 0.98m,
                Open = price * 0.99m,
                High = price * 1.01m,
                Low = price * 0.97m,
                HasNews = true,
                NewsCount = 3
            };
        }
        
        private List<DailyData> GenerateHistoricalData(string symbol, decimal basePrice, int days)
        {
            var data = new List<DailyData>();
            var random = new Random(symbol.GetHashCode());
            
            for (int i = days; i > 0; i--)
            {
                var volatility = (decimal)(random.NextDouble() * 0.04);
                var dayPrice = basePrice * (1m + (decimal)(random.NextDouble() - 0.5) * volatility);
                
                data.Add(new DailyData
                {
                    Date = DateTime.Today.AddDays(-i),
                    Open = dayPrice * 0.99m,
                    High = dayPrice * 1.01m,
                    Low = dayPrice * 0.98m,
                    Close = dayPrice,
                    Volume = random.Next(1_000_000, 5_000_000)
                });
            }
            
            return data;
        }
        
        #endregion
    }
    
    public enum TrendDirection
    {
        Down,
        Neutral,
        Up
    }
}