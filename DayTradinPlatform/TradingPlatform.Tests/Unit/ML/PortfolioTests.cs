using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.ML.Common;
using TradingPlatform.Tests.Core.Canonical;

namespace TradingPlatform.Tests.Unit.ML
{
    /// <summary>
    /// Comprehensive unit tests for Portfolio and PortfolioHolding calculations
    /// Tests financial calculations for market value, unrealized P&L, and portfolio weights
    /// </summary>
    public class PortfolioTests : TestBase
    {
        private const decimal PRECISION_TOLERANCE = 0.0001m;
        
        public PortfolioTests(ITestOutputHelper output) : base(output)
        {
        }
        
        #region PortfolioHolding Tests
        
        [Fact]
        public void MarketValue_CalculatesCorrectly()
        {
            // Arrange
            var holding = new PortfolioHolding
            {
                Symbol = "AAPL",
                Quantity = 100,
                CurrentPrice = 175.50m
            };
            
            // Act
            var marketValue = holding.MarketValue;
            
            // Assert
            AssertFinancialPrecision(17550m, marketValue);
        }
        
        [Fact]
        public void MarketValue_HandlesZeroQuantity()
        {
            // Arrange
            var holding = new PortfolioHolding
            {
                Symbol = "AAPL",
                Quantity = 0,
                CurrentPrice = 175.50m
            };
            
            // Act
            var marketValue = holding.MarketValue;
            
            // Assert
            Assert.Equal(0m, marketValue);
        }
        
        [Fact]
        public void MarketValue_HandlesNegativeQuantity_ShortPosition()
        {
            // Arrange
            var holding = new PortfolioHolding
            {
                Symbol = "AAPL",
                Quantity = -100,  // Short position
                CurrentPrice = 175.50m
            };
            
            // Act
            var marketValue = holding.MarketValue;
            
            // Assert
            AssertFinancialPrecision(-17550m, marketValue);
        }
        
        [Theory]
        [InlineData(100, 150, 175, 2500)]      // Long position with profit
        [InlineData(100, 200, 175, -2500)]     // Long position with loss
        [InlineData(-100, 150, 175, -2500)]    // Short position with loss
        [InlineData(-100, 200, 175, 2500)]     // Short position with profit
        [InlineData(0, 150, 175, 0)]           // Zero quantity
        public void UnrealizedPnL_CalculatesCorrectly(
            decimal quantity, decimal avgPrice, decimal currentPrice, decimal expectedPnL)
        {
            // Arrange
            var holding = new PortfolioHolding
            {
                Symbol = "TEST",
                Quantity = quantity,
                AveragePrice = avgPrice,
                CurrentPrice = currentPrice
            };
            
            // Act
            var unrealizedPnL = holding.UnrealizedPnL;
            
            // Assert
            AssertFinancialPrecision(expectedPnL, unrealizedPnL);
        }
        
        [Fact]
        public void UnrealizedPnL_HandlesHighPrecisionCalculations()
        {
            // Arrange - Using prices with many decimal places
            var holding = new PortfolioHolding
            {
                Symbol = "CRYPTO",
                Quantity = 1000.123456m,
                AveragePrice = 0.12345678m,
                CurrentPrice = 0.12456789m
            };
            
            // Act
            var unrealizedPnL = holding.UnrealizedPnL;
            
            // Expected: (0.12456789 - 0.12345678) * 1000.123456 = 0.111124681
            var expected = (holding.CurrentPrice - holding.AveragePrice) * holding.Quantity;
            
            // Assert
            AssertFinancialPrecision(expected, unrealizedPnL, 8);
        }
        
        [Fact]
        public void PortfolioHolding_InitializesWithDefaults()
        {
            // Arrange & Act
            var holding = new PortfolioHolding();
            
            // Assert
            Assert.Equal(string.Empty, holding.Symbol);
            Assert.Equal(0m, holding.Quantity);
            Assert.Equal(0m, holding.AveragePrice);
            Assert.Equal(0m, holding.CurrentPrice);
            Assert.Equal(0m, holding.Weight);
            Assert.Equal("Equity", holding.AssetClass);
            Assert.Equal("Unknown", holding.Sector);
            Assert.NotNull(holding.Attributes);
            Assert.Empty(holding.Attributes);
        }
        
        #endregion
        
        #region Portfolio Tests
        
        [Fact]
        public void Portfolio_InitializesWithDefaults()
        {
            // Arrange & Act
            var portfolio = new Portfolio();
            
            // Assert
            Assert.NotEmpty(portfolio.Id);
            Assert.Equal(string.Empty, portfolio.Name);
            Assert.NotNull(portfolio.Holdings);
            Assert.Empty(portfolio.Holdings);
            Assert.Equal(0m, portfolio.TotalValue);
            Assert.Equal(0m, portfolio.CashBalance);
            Assert.True(portfolio.LastUpdated <= DateTime.UtcNow);
            Assert.True(portfolio.LastUpdated > DateTime.UtcNow.AddSeconds(-5));
            Assert.NotNull(portfolio.Metadata);
            Assert.Empty(portfolio.Metadata);
        }
        
        [Fact]
        public void Portfolio_CalculatesTotalValue()
        {
            // Arrange
            var portfolio = new Portfolio
            {
                Name = "Test Portfolio",
                CashBalance = 10000m,
                Holdings = new Dictionary<string, PortfolioHolding>
                {
                    ["AAPL"] = new PortfolioHolding
                    {
                        Symbol = "AAPL",
                        Quantity = 100,
                        AveragePrice = 150m,
                        CurrentPrice = 175m
                    },
                    ["MSFT"] = new PortfolioHolding
                    {
                        Symbol = "MSFT",
                        Quantity = 50,
                        AveragePrice = 300m,
                        CurrentPrice = 325m
                    },
                    ["GOOGL"] = new PortfolioHolding
                    {
                        Symbol = "GOOGL",
                        Quantity = -25,  // Short position
                        AveragePrice = 2500m,
                        CurrentPrice = 2600m
                    }
                }
            };
            
            // Calculate expected total value
            var aaplValue = 100m * 175m;        // 17,500
            var msftValue = 50m * 325m;         // 16,250
            var googlValue = -25m * 2600m;      // -65,000 (short)
            var totalHoldingsValue = aaplValue + msftValue + googlValue; // -31,250
            var expectedTotalValue = portfolio.CashBalance + totalHoldingsValue; // 10,000 - 31,250 = -21,250
            
            // Act
            portfolio.TotalValue = portfolio.CashBalance + 
                portfolio.Holdings.Values.Sum(h => h.MarketValue);
            
            // Assert
            AssertFinancialPrecision(expectedTotalValue, portfolio.TotalValue);
        }
        
        [Fact]
        public void Portfolio_CalculatesWeights()
        {
            // Arrange
            var portfolio = new Portfolio
            {
                CashBalance = 20000m,
                Holdings = new Dictionary<string, PortfolioHolding>
                {
                    ["AAPL"] = new PortfolioHolding
                    {
                        Symbol = "AAPL",
                        Quantity = 100,
                        CurrentPrice = 200m  // Market value: 20,000
                    },
                    ["MSFT"] = new PortfolioHolding
                    {
                        Symbol = "MSFT",
                        Quantity = 200,
                        CurrentPrice = 300m  // Market value: 60,000
                    }
                }
            };
            
            // Total portfolio value = 20,000 (cash) + 20,000 (AAPL) + 60,000 (MSFT) = 100,000
            portfolio.TotalValue = 100000m;
            
            // Act - Calculate weights
            foreach (var holding in portfolio.Holdings.Values)
            {
                holding.Weight = holding.MarketValue / portfolio.TotalValue;
            }
            
            // Assert
            AssertFinancialPrecision(0.20m, portfolio.Holdings["AAPL"].Weight); // 20%
            AssertFinancialPrecision(0.60m, portfolio.Holdings["MSFT"].Weight); // 60%
            // Cash weight would be 20%
        }
        
        [Fact]
        public void Portfolio_HandlesEmptyHoldings()
        {
            // Arrange
            var portfolio = new Portfolio
            {
                CashBalance = 50000m,
                Holdings = new Dictionary<string, PortfolioHolding>()
            };
            
            // Act
            var totalHoldingsValue = portfolio.Holdings.Values.Sum(h => h.MarketValue);
            portfolio.TotalValue = portfolio.CashBalance + totalHoldingsValue;
            
            // Assert
            Assert.Equal(50000m, portfolio.TotalValue);
        }
        
        [Fact]
        public void Portfolio_CalculatesUnrealizedPnL()
        {
            // Arrange
            var portfolio = new Portfolio
            {
                Holdings = new Dictionary<string, PortfolioHolding>
                {
                    ["AAPL"] = new PortfolioHolding
                    {
                        Symbol = "AAPL",
                        Quantity = 100,
                        AveragePrice = 150m,
                        CurrentPrice = 175m  // Gain: 2,500
                    },
                    ["MSFT"] = new PortfolioHolding
                    {
                        Symbol = "MSFT",
                        Quantity = 50,
                        AveragePrice = 350m,
                        CurrentPrice = 325m  // Loss: -1,250
                    },
                    ["GOOGL"] = new PortfolioHolding
                    {
                        Symbol = "GOOGL",
                        Quantity = -25,      // Short position
                        AveragePrice = 2500m,
                        CurrentPrice = 2400m // Gain: 2,500 (profit on short)
                    }
                }
            };
            
            // Act
            var totalUnrealizedPnL = portfolio.Holdings.Values.Sum(h => h.UnrealizedPnL);
            
            // Assert
            // Total: 2,500 - 1,250 + 2,500 = 3,750
            AssertFinancialPrecision(3750m, totalUnrealizedPnL);
        }
        
        [Fact]
        public void Portfolio_TracksMetadata()
        {
            // Arrange
            var portfolio = new Portfolio
            {
                Name = "Growth Portfolio",
                Metadata = new Dictionary<string, object>
                {
                    ["Strategy"] = "Momentum",
                    ["RiskLevel"] = "High",
                    ["RebalanceFrequency"] = "Monthly",
                    ["Benchmark"] = "SPY",
                    ["CreatedDate"] = new DateTime(2024, 1, 1),
                    ["TargetReturn"] = 0.15m
                }
            };
            
            // Assert
            Assert.Equal("Momentum", portfolio.Metadata["Strategy"]);
            Assert.Equal("High", portfolio.Metadata["RiskLevel"]);
            Assert.Equal(0.15m, portfolio.Metadata["TargetReturn"]);
        }
        
        [Fact]
        public void PortfolioHolding_TracksSectorAndAssetClass()
        {
            // Arrange
            var holdings = new List<PortfolioHolding>
            {
                new PortfolioHolding 
                { 
                    Symbol = "AAPL", 
                    AssetClass = "Equity", 
                    Sector = "Technology" 
                },
                new PortfolioHolding 
                { 
                    Symbol = "JPM", 
                    AssetClass = "Equity", 
                    Sector = "Financials" 
                },
                new PortfolioHolding 
                { 
                    Symbol = "GLD", 
                    AssetClass = "ETF", 
                    Sector = "Commodities" 
                },
                new PortfolioHolding 
                { 
                    Symbol = "BTC-USD", 
                    AssetClass = "Crypto", 
                    Sector = "Digital Assets" 
                }
            };
            
            // Assert
            var techHoldings = holdings.Where(h => h.Sector == "Technology").ToList();
            Assert.Single(techHoldings);
            Assert.Equal("AAPL", techHoldings[0].Symbol);
            
            var equityHoldings = holdings.Where(h => h.AssetClass == "Equity").ToList();
            Assert.Equal(2, equityHoldings.Count);
        }
        
        #endregion
        
        #region Edge Cases
        
        [Fact]
        public void MarketValue_HandlesExtremePrices()
        {
            // Arrange - Penny stock with high quantity
            var pennyStock = new PortfolioHolding
            {
                Symbol = "PENNY",
                Quantity = 1000000m,
                CurrentPrice = 0.0001m
            };
            
            // Act
            var marketValue = pennyStock.MarketValue;
            
            // Assert
            AssertFinancialPrecision(100m, marketValue); // 1,000,000 * 0.0001 = 100
        }
        
        [Fact]
        public void UnrealizedPnL_HandlesLargePositions()
        {
            // Arrange - Large institutional position
            var holding = new PortfolioHolding
            {
                Symbol = "SPY",
                Quantity = 1000000m,
                AveragePrice = 400m,
                CurrentPrice = 401.25m
            };
            
            // Act
            var unrealizedPnL = holding.UnrealizedPnL;
            
            // Assert
            // (401.25 - 400) * 1,000,000 = 1,250,000
            AssertFinancialPrecision(1250000m, unrealizedPnL);
        }
        
        #endregion
        
        #region Helper Methods
        
        protected void AssertFinancialPrecision(decimal expected, decimal actual, int decimalPlaces = 4)
        {
            var tolerance = (decimal)Math.Pow(10, -decimalPlaces);
            Assert.True(Math.Abs(expected - actual) <= tolerance,
                $"Expected {expected} but got {actual}. Difference: {Math.Abs(expected - actual)}");
        }
        
        #endregion
    }
}