using Xunit;
using TradingPlatform.FixEngine.Trading;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.FixEngine.Models;

namespace TradingPlatform.Tests.FixEngine.Trading;

/// <summary>
/// Comprehensive unit tests for order routing in US equity markets
/// Validates smart routing logic, venue selection, and latency requirements
/// </summary>
public class OrderRouterTests : IDisposable
{
    private readonly OrderRouter _orderRouter;
    private readonly MockLogger _mockLogger;
    
    public OrderRouterTests()
    {
        _mockLogger = new MockLogger();
        _orderRouter = new OrderRouter(_mockLogger);
    }
    
    [Fact]
    public void USMarketVenues_Configuration_ContainsRequiredVenues()
    {
        // Act & Assert - Verify US market venues are properly configured
        Assert.True(OrderRouter.USMarketVenues.ContainsKey("NYSE"));
        Assert.True(OrderRouter.USMarketVenues.ContainsKey("NASDAQ"));
        Assert.True(OrderRouter.USMarketVenues.ContainsKey("BATS"));
        Assert.True(OrderRouter.USMarketVenues.ContainsKey("IEX"));
        Assert.True(OrderRouter.USMarketVenues.ContainsKey("ARCA"));
        
        // Verify venue configurations
        Assert.Equal("XNYS", OrderRouter.USMarketVenues["NYSE"].Mic);
        Assert.Equal("XNAS", OrderRouter.USMarketVenues["NASDAQ"].Mic);
        Assert.Equal("BATS", OrderRouter.USMarketVenues["BATS"].Mic);
        Assert.Equal("IEXG", OrderRouter.USMarketVenues["IEX"].Mic);
        Assert.Equal("ARCX", OrderRouter.USMarketVenues["ARCA"].Mic);
    }
    
    [Theory]
    [InlineData("AAPL", 100, 175.50, "NASDAQ")] // Tech stock → NASDAQ
    [InlineData("MSFT", 200, 380.25, "NASDAQ")] // Tech stock → NASDAQ  
    [InlineData("JPM", 150, 145.75, "NYSE")]    // Traditional stock → NYSE
    [InlineData("XOM", 300, 95.30, "NYSE")]     // Traditional stock → NYSE
    public void SelectOptimalVenue_TechVsTraditionalStocks_RoutesCorrectly(
        string symbol, decimal quantity, decimal price, string expectedVenue)
    {
        // This test uses reflection to access the private SelectOptimalVenue method
        // In production, this logic would be exposed through a public interface
        
        // Arrange
        var request = new OrderRequest
        {
            Symbol = symbol,
            Side = "BUY",
            Quantity = quantity,
            Price = price,
            OrderType = "LIMIT"
        };
        
        // Act - Use reflection to test private method
        var method = typeof(OrderRouter).GetMethod("SelectOptimalVenue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (string)method!.Invoke(_orderRouter, new object[] { request })!;
        
        // Assert
        Assert.Equal(expectedVenue, result);
    }
    
    [Fact]
    public void CreateFixOrder_LimitOrder_GeneratesCorrectFixMessage()
    {
        // Arrange
        var request = new OrderRequest
        {
            Symbol = "AAPL",
            Side = "BUY", 
            Quantity = 100m,
            Price = 175.50m,
            OrderType = "LIMIT",
            TimeInForce = "DAY"
        };
        
        // Act - Use reflection to test private method
        var method = typeof(OrderRouter).GetMethod("CreateFixOrder",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (FixMessage)method!.Invoke(_orderRouter, new object[] { request, "NASDAQ" })!;
        
        // Assert
        Assert.Equal(FixMessageTypes.NewOrderSingle, result.MsgType);
        Assert.Equal("AAPL", result.GetField(55)); // Symbol
        Assert.Equal("1", result.GetField(54)); // Side (Buy)
        Assert.Equal(100m, result.GetDecimalField(38)); // OrderQty
        Assert.Equal("2", result.GetField(40)); // OrdType (Limit)
        Assert.Equal(175.50m, result.GetDecimalField(44)); // Price
        Assert.Equal("0", result.GetField(59)); // TimeInForce (Day)
        Assert.Equal("XNAS", result.GetField(100)); // ExDestination (NASDAQ MIC)
    }
    
    [Fact]
    public void CreateFixOrder_MarketOrder_ExcludesPriceField()
    {
        // Arrange
        var request = new OrderRequest
        {
            Symbol = "MSFT",
            Side = "SELL",
            Quantity = 50m,
            Price = 380.25m, // Should be ignored for market orders
            OrderType = "MARKET"
        };
        
        // Act
        var method = typeof(OrderRouter).GetMethod("CreateFixOrder",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (FixMessage)method!.Invoke(_orderRouter, new object[] { request, "NYSE" })!;
        
        // Assert
        Assert.Equal("1", result.GetField(40)); // OrdType (Market)
        Assert.Null(result.GetField(44)); // Price should not be set for market orders
        Assert.Equal("2", result.GetField(54)); // Side (Sell)
    }
    
    [Fact]
    public void CreateFixOrder_StopLimitOrder_IncludesBothPrices()
    {
        // Arrange
        var request = new OrderRequest
        {
            Symbol = "TSLA",
            Side = "BUY",
            Quantity = 25m,
            Price = 250.00m,
            StopPrice = 245.00m,
            OrderType = "STOP_LIMIT"
        };
        
        // Act
        var method = typeof(OrderRouter).GetMethod("CreateFixOrder",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (FixMessage)method!.Invoke(_orderRouter, new object[] { request, "NASDAQ" })!;
        
        // Assert
        Assert.Equal("4", result.GetField(40)); // OrdType (Stop Limit)
        Assert.Equal(250.00m, result.GetDecimalField(44)); // Price
        Assert.Equal(245.00m, result.GetDecimalField(99)); // StopPx
    }
    
    [Theory]
    [InlineData("IOC", "3")]
    [InlineData("FOK", "4")]
    [InlineData("GTD", "6")]
    [InlineData("DAY", "0")]
    [InlineData("UNKNOWN", "0")]
    public void CreateFixOrder_TimeInForce_MapsCorrectly(string tif, string expectedFixValue)
    {
        // Arrange
        var request = new OrderRequest
        {
            Symbol = "AAPL",
            Side = "BUY",
            Quantity = 100m,
            Price = 175.50m,
            OrderType = "LIMIT",
            TimeInForce = tif
        };
        
        // Act
        var method = typeof(OrderRouter).GetMethod("CreateFixOrder",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (FixMessage)method!.Invoke(_orderRouter, new object[] { request, "NYSE" })!;
        
        // Assert
        Assert.Equal(expectedFixValue, result.GetField(59)); // TimeInForce
    }
    
    [Fact]
    public void CreateFixOrder_HiddenOrderOnSupportedVenue_SetsExecutionInstruction()
    {
        // Arrange
        var request = new OrderRequest
        {
            Symbol = "GOOG",
            Side = "BUY",
            Quantity = 50m,
            Price = 2800.00m,
            OrderType = "LIMIT",
            IsHiddenOrder = true
        };
        
        // Act - Use NYSE which supports hidden orders
        var method = typeof(OrderRouter).GetMethod("CreateFixOrder",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (FixMessage)method!.Invoke(_orderRouter, new object[] { request, "NYSE" })!;
        
        // Assert
        Assert.Equal("H", result.GetField(18)); // ExecInst = Hidden
    }
    
    [Fact]
    public void CreateFixOrder_HiddenOrderOnUnsupportedVenue_ExcludesExecutionInstruction()
    {
        // Arrange
        var request = new OrderRequest
        {
            Symbol = "GOOG",
            Side = "BUY",
            Quantity = 50m,
            Price = 2800.00m,
            OrderType = "LIMIT",
            IsHiddenOrder = true
        };
        
        // Act - Use IEX which doesn't support hidden orders
        var method = typeof(OrderRouter).GetMethod("CreateFixOrder",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (FixMessage)method!.Invoke(_orderRouter, new object[] { request, "IEX" })!;
        
        // Assert
        Assert.Null(result.GetField(18)); // ExecInst should not be set
    }
    
    [Fact]
    public void GetFallbackVenue_PrimaryVenueDown_SelectsNextBestVenue()
    {
        // Arrange
        var request = new OrderRequest
        {
            Symbol = "AAPL",
            Side = "BUY",
            Quantity = 100m,
            Price = 175.50m,
            OrderType = "LIMIT"
        };
        
        // Act - Test fallback when NASDAQ is down
        var method = typeof(OrderRouter).GetMethod("GetFallbackVenue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (string)method!.Invoke(_orderRouter, new object[] { request, "NASDAQ" })!;
        
        // Assert - Should fallback to next best venue (NYSE has latency rank 1)
        Assert.Equal("NYSE", result);
    }
    
    [Fact]
    public void GetFallbackVenue_LargeOrderExceedsVenueLimit_SkipsUnsuitableVenues()
    {
        // Arrange - Large order that exceeds IEX limit (100k)
        var request = new OrderRequest
        {
            Symbol = "AAPL",
            Side = "BUY",
            Quantity = 1000m,
            Price = 175.50m, // Total value: $175,500
            OrderType = "LIMIT"
        };
        
        // Act - Test fallback when primary venue is down
        var method = typeof(OrderRouter).GetMethod("GetFallbackVenue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (string)method!.Invoke(_orderRouter, new object[] { request, "NASDAQ" })!;
        
        // Assert - Should skip IEX (too small) and select NYSE or ARCA
        Assert.True(result == "NYSE" || result == "ARCA");
        Assert.NotEqual("IEX", result);
    }
    
    [Fact]
    public void IsTechStock_KnownTechSymbols_ReturnsTrue()
    {
        // Act & Assert - Test known tech stocks
        var method = typeof(OrderRouter).GetMethod("IsTechStock",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        Assert.True((bool)method!.Invoke(null, new object[] { "AAPL" })!);
        Assert.True((bool)method!.Invoke(null, new object[] { "MSFT" })!);
        Assert.True((bool)method!.Invoke(null, new object[] { "GOOGL" })!);
        Assert.True((bool)method!.Invoke(null, new object[] { "NVDA" })!);
        Assert.True((bool)method!.Invoke(null, new object[] { "QCOM" })!); // Q prefix
    }
    
    [Fact]
    public void IsTechStock_TraditionalStocks_ReturnsFalse()
    {
        // Act & Assert - Test traditional stocks
        var method = typeof(OrderRouter).GetMethod("IsTechStock",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        Assert.False((bool)method!.Invoke(null, new object[] { "JPM" })!);
        Assert.False((bool)method!.Invoke(null, new object[] { "XOM" })!);
        Assert.False((bool)method!.Invoke(null, new object[] { "BAC" })!);
        Assert.False((bool)method!.Invoke(null, new object[] { "WMT" })!);
    }
    
    [Fact]
    public void GenerateClOrdId_Multiple_GeneratesUniqueIds()
    {
        // Act - Generate multiple ClOrdIDs
        var method = typeof(OrderRouter).GetMethod("GenerateClOrdId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        var id1 = (string)method!.Invoke(null, null)!;
        var id2 = (string)method!.Invoke(null, null)!;
        var id3 = (string)method!.Invoke(null, null)!;
        
        // Assert
        Assert.StartsWith("DT", id1);
        Assert.StartsWith("DT", id2);
        Assert.StartsWith("DT", id3);
        
        // Verify uniqueness
        Assert.NotEqual(id1, id2);
        Assert.NotEqual(id2, id3);
        Assert.NotEqual(id1, id3);
        
        // Verify format (DT + timestamp + random)
        Assert.True(id1.Length >= 15); // DT + 13-digit timestamp + 4-digit random
    }
    
    [Theory]
    [InlineData("MARKET", "1")]
    [InlineData("LIMIT", "2")]
    [InlineData("STOP", "3")]
    [InlineData("STOP_LIMIT", "4")]
    [InlineData("UNKNOWN", "2")] // Default to limit
    public void GetFixOrderType_VariousOrderTypes_MapsCorrectly(string orderType, string expectedFixType)
    {
        // Act
        var method = typeof(OrderRouter).GetMethod("GetFixOrderType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (string)method!.Invoke(null, new object[] { orderType })!;
        
        // Assert
        Assert.Equal(expectedFixType, result);
    }
    
    public void Dispose()
    {
        _orderRouter.Dispose();
    }
}

/// <summary>
/// Mock logger for testing purposes
/// </summary>
public class MockLogger : ILogger
{
    public List<string> LogMessages { get; } = new();
    
    public void LogInfo(string message)
    {
        LogMessages.Add($"INFO: {message}");
    }
    
    public void LogWarning(string message)
    {
        LogMessages.Add($"WARN: {message}");
    }
    
    public void LogError(string message, Exception? exception = null)
    {
        LogMessages.Add($"ERROR: {message} {exception?.Message}");
    }
    
    public void LogDebug(string message)
    {
        LogMessages.Add($"DEBUG: {message}");
    }
    
    public void LogTrace(string message)
    {
        LogMessages.Add($"TRACE: {message}");
    }
    
    public void LogTrade(string symbol, decimal price, int quantity, string action)
    {
        LogMessages.Add($"TRADE: {action} {quantity} {symbol} @ {price}");
    }
    
    public void LogPerformance(string operation, TimeSpan duration)
    {
        LogMessages.Add($"PERF: {operation} took {duration.TotalMilliseconds}ms");
    }
}