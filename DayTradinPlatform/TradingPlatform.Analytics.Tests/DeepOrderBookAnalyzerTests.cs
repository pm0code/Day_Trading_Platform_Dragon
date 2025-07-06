using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using TradingPlatform.Analytics.OrderBook;
using TradingPlatform.GPU.Infrastructure;
using TradingPlatform.ML.Interfaces;
using TradingPlatform.ML.Services;
using TradingPlatform.ML.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.Analytics.Tests
{
    /// <summary>
    /// Comprehensive tests for Deep Order Book Analytics Engine
    /// </summary>
    public class DeepOrderBookAnalyzerTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly DeepOrderBookAnalyzer _analyzer;
        private readonly OrderBookAnalyticsConfiguration _config;
        private readonly GpuContext _gpuContext;
        private readonly IMLInferenceService _mlService;

        public DeepOrderBookAnalyzerTests(ITestOutputHelper output)
        {
            _output = output;
            
            // Create test configuration
            _config = new OrderBookAnalyticsConfiguration
            {
                MaxHistorySnapshots = 500,
                MinHistoryForPatternDetection = 10,
                ImpactAnalysisSizes = new[] { 1000m, 5000m, 10000m, 25000m },
                MinimumOpportunityScore = 30m,
                MaxOpportunitiesReturned = 10,
                EnableMLPatternDetection = true,
                EnableGpuAcceleration = true
            };
            
            // Initialize GPU context
            _gpuContext = new GpuContext(NullLogger<GpuContext>.Instance);
            
            // Initialize ML service
            var mlConfig = new MLInferenceConfiguration
            {
                Provider = ExecutionProvider.CPU,
                ModelsPath = "TestModels"
            };
            
            _mlService = new MLInferenceService(
                mlConfig,
                logger: NullLogger<MLInferenceService>.Instance);
            
            // Create analyzer
            _analyzer = new DeepOrderBookAnalyzer(
                _config,
                _mlService,
                _gpuContext,
                NullLogger<DeepOrderBookAnalyzer>.Instance);
        }

        #region Basic Analysis Tests

        [Fact]
        public async Task AnalyzeOrderBookAsync_ValidSnapshot_ReturnsComprehensiveAnalysis()
        {
            // Arrange
            var snapshot = CreateTestOrderBookSnapshot("AAPL", 150.00m, 150.05m);
            
            // Act
            var analysis = await _analyzer.AnalyzeOrderBookAsync("AAPL", snapshot);
            
            // Assert
            Assert.NotNull(analysis);
            Assert.Equal("AAPL", analysis.Symbol);
            Assert.NotNull(analysis.LiquidityAnalysis);
            Assert.NotNull(analysis.PriceImpactAnalysis);
            Assert.NotNull(analysis.MicrostructurePatterns);
            Assert.NotNull(analysis.TradingOpportunities);
            Assert.NotNull(analysis.OrderFlowAnalysis);
            Assert.NotNull(analysis.Anomalies);
            Assert.NotNull(analysis.AggregateMetrics);
            Assert.NotNull(analysis.Features);
            
            // Verify quality metrics
            Assert.True(analysis.AnalysisQuality >= 0);
            Assert.True(analysis.AnalysisQuality <= 100);
            
            _output.WriteLine($"Order Book Analysis Results:");
            _output.WriteLine($"  Symbol: {analysis.Symbol}");
            _output.WriteLine($"  Liquidity Score: {analysis.LiquidityAnalysis.LiquidityScore:F2}");
            _output.WriteLine($"  Patterns Detected: {analysis.MicrostructurePatterns.Count}");
            _output.WriteLine($"  Opportunities Found: {analysis.TradingOpportunities.Count}");
            _output.WriteLine($"  Anomalies Detected: {analysis.Anomalies.Count}");
            _output.WriteLine($"  Analysis Quality: {analysis.AnalysisQuality:F1}%");
        }

        [Fact]
        public async Task AnalyzeLiquidityAsync_TightSpread_HighLiquidityScore()
        {
            // Arrange
            var snapshot = CreateTightSpreadSnapshot("MSFT", 300.00m, 300.01m);
            var features = CreateTestFeatures(snapshot);
            
            // Act
            var liquidityAnalysis = await _analyzer.AnalyzeLiquidityAsync(snapshot, features);
            
            // Assert
            Assert.NotNull(liquidityAnalysis);
            Assert.True(liquidityAnalysis.LiquidityScore > 70m, "Tight spread should result in high liquidity score");
            Assert.NotNull(liquidityAnalysis.SpreadMetrics);
            Assert.NotNull(liquidityAnalysis.DepthAnalysis);
            Assert.True(liquidityAnalysis.SpreadMetrics.RelativeSpread < 0.001m, "Relative spread should be very small");
            
            _output.WriteLine($"Liquidity Analysis (Tight Spread):");
            _output.WriteLine($"  Liquidity Score: {liquidityAnalysis.LiquidityScore:F2}");
            _output.WriteLine($"  Relative Spread: {liquidityAnalysis.SpreadMetrics.RelativeSpread:P4}");
            _output.WriteLine($"  Depth Score: {liquidityAnalysis.DepthAnalysis.BidDepthMean:F0}");
        }

        [Fact]
        public async Task AnalyzeLiquidityAsync_WideSpread_LowLiquidityScore()
        {
            // Arrange
            var snapshot = CreateWideSpreadSnapshot("ILLIQ", 50.00m, 50.50m);
            var features = CreateTestFeatures(snapshot);
            
            // Act
            var liquidityAnalysis = await _analyzer.AnalyzeLiquidityAsync(snapshot, features);
            
            // Assert
            Assert.NotNull(liquidityAnalysis);
            Assert.True(liquidityAnalysis.LiquidityScore < 50m, "Wide spread should result in low liquidity score");
            Assert.True(liquidityAnalysis.SpreadMetrics.RelativeSpread > 0.005m, "Relative spread should be significant");
            
            _output.WriteLine($"Liquidity Analysis (Wide Spread):");
            _output.WriteLine($"  Liquidity Score: {liquidityAnalysis.LiquidityScore:F2}");
            _output.WriteLine($"  Relative Spread: {liquidityAnalysis.SpreadMetrics.RelativeSpread:P4}");
        }

        #endregion

        #region Price Impact Analysis Tests

        [Fact]
        public async Task AnalyzePriceImpactAsync_MultipleOrderSizes_ProducesImpactProfiles()
        {
            // Arrange
            var snapshot = CreateDeepOrderBookSnapshot("TSLA", 800.00m, 800.10m);
            var features = CreateTestFeatures(snapshot);
            
            // Act
            var impactAnalysis = await _analyzer.AnalyzePriceImpactAsync(snapshot, features);
            
            // Assert
            Assert.NotNull(impactAnalysis);
            Assert.Equal(_config.ImpactAnalysisSizes.Length, impactAnalysis.ImpactProfiles.Count);
            
            foreach (var profile in impactAnalysis.ImpactProfiles)
            {
                Assert.True(profile.BuyImpact.ImpactBps >= 0, "Buy impact should be non-negative");
                Assert.True(profile.SellImpact.ImpactBps >= 0, "Sell impact should be non-negative");
                Assert.True(profile.AsymmetryRatio > 0, "Asymmetry ratio should be positive");
            }
            
            // Verify impact increases with order size
            for (int i = 1; i < impactAnalysis.ImpactProfiles.Count; i++)
            {
                var current = impactAnalysis.ImpactProfiles[i];
                var previous = impactAnalysis.ImpactProfiles[i - 1];
                
                Assert.True(current.BuyImpact.ImpactBps >= previous.BuyImpact.ImpactBps,
                    "Impact should increase or stay same with larger orders");
            }
            
            _output.WriteLine($"Price Impact Analysis:");
            _output.WriteLine($"  Linearity Index: {impactAnalysis.LinearityIndex:F3}");
            _output.WriteLine($"  Optimal Order Size: {impactAnalysis.OptimalOrderSize:N0}");
            
            foreach (var profile in impactAnalysis.ImpactProfiles)
            {
                _output.WriteLine($"  Size {profile.OrderSize:N0}: Buy={profile.BuyImpact.ImpactBps:F1}bps, " +
                                $"Sell={profile.SellImpact.ImpactBps:F1}bps, Asymmetry={profile.AsymmetryRatio:F2}");
            }
        }

        [Fact]
        public async Task AnalyzePriceImpactAsync_InsufficientLiquidity_HandlesGracefully()
        {
            // Arrange
            var snapshot = CreateShallowOrderBookSnapshot("PENNY", 1.00m, 1.05m);
            var features = CreateTestFeatures(snapshot);
            
            // Act
            var impactAnalysis = await _analyzer.AnalyzePriceImpactAsync(snapshot, features);
            
            // Assert
            Assert.NotNull(impactAnalysis);
            
            // Check for large order sizes that exceed available liquidity
            var largeOrderProfile = impactAnalysis.ImpactProfiles
                .FirstOrDefault(p => p.OrderSize == _config.ImpactAnalysisSizes.Max());
            
            if (largeOrderProfile != null)
            {
                // Should handle insufficient liquidity cases
                Assert.True(largeOrderProfile.BuyImpact.ImpactBps >= 0);
                Assert.True(largeOrderProfile.SellImpact.ImpactBps >= 0);
            }
            
            _output.WriteLine($"Shallow Book Impact Analysis:");
            _output.WriteLine($"  Available bid liquidity: {snapshot.Bids.Sum(b => b.Quantity):N0}");
            _output.WriteLine($"  Available ask liquidity: {snapshot.Asks.Sum(a => a.Quantity):N0}");
        }

        #endregion

        #region Pattern Detection Tests

        [Fact]
        public async Task DetectMicrostructurePatternsAsync_ValidHistory_DetectsPatterns()
        {
            // Arrange
            var snapshot = CreateTestOrderBookSnapshot("GOOG", 2500.00m, 2500.10m);
            
            // Add some snapshots to history first
            for (int i = 0; i < 20; i++)
            {
                var histSnapshot = CreateTestOrderBookSnapshot("GOOG", 
                    2500.00m + (i * 0.01m), 2500.10m + (i * 0.01m));
                await _analyzer.AnalyzeOrderBookAsync("GOOG", histSnapshot);
            }
            
            // Act
            var patterns = await _analyzer.DetectMicrostructurePatternsAsync("GOOG", snapshot);
            
            // Assert
            Assert.NotNull(patterns);
            
            foreach (var pattern in patterns)
            {
                Assert.True(pattern.Confidence >= 0 && pattern.Confidence <= 1,
                    "Pattern confidence should be between 0 and 1");
                Assert.NotEmpty(pattern.Description);
                Assert.True(pattern.PatternStrength >= 0);
                
                _output.WriteLine($"Pattern Detected:");
                _output.WriteLine($"  Type: {pattern.Type}");
                _output.WriteLine($"  Price: {pattern.Price:C}");
                _output.WriteLine($"  Confidence: {pattern.Confidence:P}");
                _output.WriteLine($"  Description: {pattern.Description}");
            }
        }

        #endregion

        #region Trading Opportunities Tests

        [Fact]
        public async Task IdentifyTradingOpportunitiesAsync_ImbalancedBook_FindsOpportunities()
        {
            // Arrange
            var snapshot = CreateImbalancedOrderBookSnapshot("AMZN", 3000.00m, 3000.20m);
            var features = CreateTestFeatures(snapshot);
            
            // Act
            var opportunities = await _analyzer.IdentifyTradingOpportunitiesAsync(snapshot, features);
            
            // Assert
            Assert.NotNull(opportunities);
            
            foreach (var opportunity in opportunities)
            {
                Assert.True(opportunity.Score >= _config.MinimumOpportunityScore,
                    "Returned opportunities should meet minimum score threshold");
                Assert.True(opportunity.Confidence >= 0 && opportunity.Confidence <= 1);
                Assert.True(opportunity.ExpectedProfit != 0 || opportunity.Type == OpportunityType.Statistical);
                Assert.NotEmpty(opportunity.Description);
                
                _output.WriteLine($"Trading Opportunity:");
                _output.WriteLine($"  Type: {opportunity.Type}");
                _output.WriteLine($"  Expected Profit: {opportunity.ExpectedProfit:C4}");
                _output.WriteLine($"  Confidence: {opportunity.Confidence:P}");
                _output.WriteLine($"  Score: {opportunity.Score:F1}");
                _output.WriteLine($"  Risk-Adjusted Score: {opportunity.RiskAdjustedScore:F1}");
                _output.WriteLine($"  Description: {opportunity.Description}");
            }
            
            Assert.True(opportunities.Count <= _config.MaxOpportunitiesReturned,
                "Should not exceed maximum opportunities limit");
        }

        [Fact]
        public async Task IdentifyTradingOpportunitiesAsync_CrossedMarket_FindsArbitrage()
        {
            // Arrange - Create a crossed market (bid > ask)
            var snapshot = CreateCrossedMarketSnapshot("ARBT", 100.05m, 100.00m);
            var features = CreateTestFeatures(snapshot);
            
            // Act
            var opportunities = await _analyzer.IdentifyTradingOpportunitiesAsync(snapshot, features);
            
            // Assert
            Assert.NotNull(opportunities);
            
            var arbitrageOpportunity = opportunities.FirstOrDefault(o => o.Type == OpportunityType.Arbitrage);
            Assert.NotNull(arbitrageOpportunity);
            Assert.True(arbitrageOpportunity.ExpectedProfit > 0, "Arbitrage should have positive expected profit");
            Assert.True(arbitrageOpportunity.Confidence > 0.9m, "Arbitrage confidence should be very high");
            
            _output.WriteLine($"Arbitrage Opportunity Found:");
            _output.WriteLine($"  Buy Price: {arbitrageOpportunity.BuyPrice:C}");
            _output.WriteLine($"  Sell Price: {arbitrageOpportunity.SellPrice:C}");
            _output.WriteLine($"  Expected Profit: {arbitrageOpportunity.ExpectedProfit:C}");
            _output.WriteLine($"  Confidence: {arbitrageOpportunity.Confidence:P}");
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public async Task AnalyzeOrderBookAsync_EmptyOrderBook_HandlesGracefully()
        {
            // Arrange
            var snapshot = CreateEmptyOrderBookSnapshot("EMPTY");
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _analyzer.AnalyzeOrderBookAsync("EMPTY", snapshot);
            });
        }

        [Fact]
        public async Task AnalyzeOrderBookAsync_NullSnapshot_ThrowsException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await _analyzer.AnalyzeOrderBookAsync("TEST", null!);
            });
        }

        [Fact]
        public async Task AnalyzeOrderBookAsync_EmptySymbol_ThrowsException()
        {
            // Arrange
            var snapshot = CreateTestOrderBookSnapshot("TEST", 100.00m, 100.05m);
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _analyzer.AnalyzeOrderBookAsync("", snapshot);
            });
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task AnalyzeOrderBookAsync_LargeOrderBook_CompletesWithinLatencyTarget()
        {
            // Arrange
            var snapshot = CreateLargeOrderBookSnapshot("LARGE", 1000.00m, 1000.10m, 100);
            
            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var analysis = await _analyzer.AnalyzeOrderBookAsync("LARGE", snapshot);
            stopwatch.Stop();
            
            // Assert
            Assert.NotNull(analysis);
            Assert.True(stopwatch.ElapsedMilliseconds <= _config.MaxAnalysisLatency.TotalMilliseconds,
                $"Analysis took {stopwatch.ElapsedMilliseconds}ms, exceeds target of {_config.MaxAnalysisLatency.TotalMilliseconds}ms");
            
            _output.WriteLine($"Large Order Book Analysis Performance:");
            _output.WriteLine($"  Order Book Levels: {snapshot.Bids.Count + snapshot.Asks.Count}");
            _output.WriteLine($"  Analysis Time: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Target Latency: {_config.MaxAnalysisLatency.TotalMilliseconds}ms");
        }

        #endregion

        #region Helper Methods

        private OrderBookSnapshot CreateTestOrderBookSnapshot(string symbol, decimal bestBid, decimal bestAsk)
        {
            var snapshot = new OrderBookSnapshot
            {
                Symbol = symbol,
                Timestamp = DateTime.UtcNow,
                BestBid = bestBid,
                BestAsk = bestAsk,
                Bids = new List<OrderBookLevel>(),
                Asks = new List<OrderBookLevel>()
            };
            
            // Add 5 bid levels
            for (int i = 0; i < 5; i++)
            {
                snapshot.Bids.Add(new OrderBookLevel
                {
                    Price = bestBid - (i * 0.01m),
                    Quantity = 1000m + (i * 200m),
                    OrderCount = 1 + i,
                    LastUpdateTime = DateTime.UtcNow
                });
            }
            
            // Add 5 ask levels
            for (int i = 0; i < 5; i++)
            {
                snapshot.Asks.Add(new OrderBookLevel
                {
                    Price = bestAsk + (i * 0.01m),
                    Quantity = 1000m + (i * 200m),
                    OrderCount = 1 + i,
                    LastUpdateTime = DateTime.UtcNow
                });
            }
            
            return snapshot;
        }

        private OrderBookSnapshot CreateTightSpreadSnapshot(string symbol, decimal bestBid, decimal bestAsk)
        {
            var snapshot = CreateTestOrderBookSnapshot(symbol, bestBid, bestAsk);
            
            // Increase depth for tight spread scenarios
            foreach (var bid in snapshot.Bids)
            {
                bid.Quantity *= 3m;
            }
            
            foreach (var ask in snapshot.Asks)
            {
                ask.Quantity *= 3m;
            }
            
            return snapshot;
        }

        private OrderBookSnapshot CreateWideSpreadSnapshot(string symbol, decimal bestBid, decimal bestAsk)
        {
            var snapshot = CreateTestOrderBookSnapshot(symbol, bestBid, bestAsk);
            
            // Reduce depth for wide spread scenarios
            foreach (var bid in snapshot.Bids)
            {
                bid.Quantity /= 2m;
            }
            
            foreach (var ask in snapshot.Asks)
            {
                ask.Quantity /= 2m;
            }
            
            return snapshot;
        }

        private OrderBookSnapshot CreateDeepOrderBookSnapshot(string symbol, decimal bestBid, decimal bestAsk)
        {
            var snapshot = new OrderBookSnapshot
            {
                Symbol = symbol,
                Timestamp = DateTime.UtcNow,
                BestBid = bestBid,
                BestAsk = bestAsk,
                Bids = new List<OrderBookLevel>(),
                Asks = new List<OrderBookLevel>()
            };
            
            // Add 10 deep bid levels
            for (int i = 0; i < 10; i++)
            {
                snapshot.Bids.Add(new OrderBookLevel
                {
                    Price = bestBid - (i * 0.10m),
                    Quantity = 5000m + (i * 1000m),
                    OrderCount = 3 + i,
                    LastUpdateTime = DateTime.UtcNow
                });
            }
            
            // Add 10 deep ask levels
            for (int i = 0; i < 10; i++)
            {
                snapshot.Asks.Add(new OrderBookLevel
                {
                    Price = bestAsk + (i * 0.10m),
                    Quantity = 5000m + (i * 1000m),
                    OrderCount = 3 + i,
                    LastUpdateTime = DateTime.UtcNow
                });
            }
            
            return snapshot;
        }

        private OrderBookSnapshot CreateShallowOrderBookSnapshot(string symbol, decimal bestBid, decimal bestAsk)
        {
            return new OrderBookSnapshot
            {
                Symbol = symbol,
                Timestamp = DateTime.UtcNow,
                BestBid = bestBid,
                BestAsk = bestAsk,
                Bids = new List<OrderBookLevel>
                {
                    new() { Price = bestBid, Quantity = 100m, OrderCount = 1, LastUpdateTime = DateTime.UtcNow },
                    new() { Price = bestBid - 0.01m, Quantity = 50m, OrderCount = 1, LastUpdateTime = DateTime.UtcNow }
                },
                Asks = new List<OrderBookLevel>
                {
                    new() { Price = bestAsk, Quantity = 100m, OrderCount = 1, LastUpdateTime = DateTime.UtcNow },
                    new() { Price = bestAsk + 0.01m, Quantity = 50m, OrderCount = 1, LastUpdateTime = DateTime.UtcNow }
                }
            };
        }

        private OrderBookSnapshot CreateImbalancedOrderBookSnapshot(string symbol, decimal bestBid, decimal bestAsk)
        {
            var snapshot = CreateTestOrderBookSnapshot(symbol, bestBid, bestAsk);
            
            // Create imbalance by heavily weighting one side
            foreach (var bid in snapshot.Bids)
            {
                bid.Quantity *= 5m; // Much more liquidity on bid side
            }
            
            return snapshot;
        }

        private OrderBookSnapshot CreateCrossedMarketSnapshot(string symbol, decimal bestBid, decimal bestAsk)
        {
            return new OrderBookSnapshot
            {
                Symbol = symbol,
                Timestamp = DateTime.UtcNow,
                BestBid = bestBid,
                BestAsk = bestAsk,
                Bids = new List<OrderBookLevel>
                {
                    new() { Price = bestBid, Quantity = 1000m, OrderCount = 1, LastUpdateTime = DateTime.UtcNow }
                },
                Asks = new List<OrderBookLevel>
                {
                    new() { Price = bestAsk, Quantity = 1000m, OrderCount = 1, LastUpdateTime = DateTime.UtcNow }
                }
            };
        }

        private OrderBookSnapshot CreateEmptyOrderBookSnapshot(string symbol)
        {
            return new OrderBookSnapshot
            {
                Symbol = symbol,
                Timestamp = DateTime.UtcNow,
                BestBid = 0m,
                BestAsk = 0m,
                Bids = new List<OrderBookLevel>(),
                Asks = new List<OrderBookLevel>()
            };
        }

        private OrderBookSnapshot CreateLargeOrderBookSnapshot(string symbol, decimal bestBid, decimal bestAsk, int levelsPerSide)
        {
            var snapshot = new OrderBookSnapshot
            {
                Symbol = symbol,
                Timestamp = DateTime.UtcNow,
                BestBid = bestBid,
                BestAsk = bestAsk,
                Bids = new List<OrderBookLevel>(),
                Asks = new List<OrderBookLevel>()
            };
            
            var random = new Random(42);
            
            // Add many bid levels
            for (int i = 0; i < levelsPerSide; i++)
            {
                snapshot.Bids.Add(new OrderBookLevel
                {
                    Price = bestBid - (i * 0.01m),
                    Quantity = 500m + (decimal)(random.NextDouble() * 2000m),
                    OrderCount = 1 + random.Next(10),
                    LastUpdateTime = DateTime.UtcNow
                });
            }
            
            // Add many ask levels
            for (int i = 0; i < levelsPerSide; i++)
            {
                snapshot.Asks.Add(new OrderBookLevel
                {
                    Price = bestAsk + (i * 0.01m),
                    Quantity = 500m + (decimal)(random.NextDouble() * 2000m),
                    OrderCount = 1 + random.Next(10),
                    LastUpdateTime = DateTime.UtcNow
                });
            }
            
            return snapshot;
        }

        private OrderBookFeatures CreateTestFeatures(OrderBookSnapshot snapshot)
        {
            return new OrderBookFeatures
            {
                BidAskSpread = snapshot.BestAsk - snapshot.BestBid,
                RelativeSpread = (snapshot.BestAsk - snapshot.BestBid) / ((snapshot.BestAsk + snapshot.BestBid) / 2m),
                MidPrice = (snapshot.BestAsk + snapshot.BestBid) / 2m,
                BidDepth = snapshot.Bids.Sum(b => b.Quantity),
                AskDepth = snapshot.Asks.Sum(a => a.Quantity),
                TotalDepth = snapshot.Bids.Sum(b => b.Quantity) + snapshot.Asks.Sum(a => a.Quantity),
                BidLevels = snapshot.Bids.Count,
                AskLevels = snapshot.Asks.Count,
                TotalLevels = snapshot.Bids.Count + snapshot.Asks.Count
            };
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _analyzer?.Dispose();
            _mlService?.Dispose();
            _gpuContext?.Dispose();
        }

        #endregion
    }
}