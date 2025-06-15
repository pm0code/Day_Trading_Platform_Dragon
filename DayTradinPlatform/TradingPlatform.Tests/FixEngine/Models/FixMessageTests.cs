using Xunit;
using TradingPlatform.FixEngine.Models;

namespace TradingPlatform.Tests.FixEngine.Models;

/// <summary>
/// Comprehensive unit tests for FIX message parsing and generation
/// Validates sub-millisecond performance requirements and US market compliance
/// </summary>
public class FixMessageTests
{
    [Fact]
    public void ToFixString_NewOrderSingle_GeneratesValidFixMessage()
    {
        // Arrange
        var message = new FixMessage
        {
            MsgType = FixMessageTypes.NewOrderSingle,
            SenderCompID = "DAYTRADER",
            TargetCompID = "NYSE",
            MsgSeqNum = 1
        };
        
        message.SetField(11, "DT1234567890"); // ClOrdID
        message.SetField(55, "AAPL"); // Symbol
        message.SetField(54, "1"); // Side (Buy)
        message.SetField(38, 100m); // OrderQty
        message.SetField(40, "2"); // OrdType (Limit)
        message.SetField(44, 150.25m); // Price
        message.SetField(59, "0"); // TimeInForce (Day)
        
        // Act
        var fixString = message.ToFixString();
        
        // Assert
        Assert.Contains("8=FIX.4.2", fixString);
        Assert.Contains("35=D", fixString); // NewOrderSingle
        Assert.Contains("49=DAYTRADER", fixString);
        Assert.Contains("56=NYSE", fixString);
        Assert.Contains("34=1", fixString);
        Assert.Contains("11=DT1234567890", fixString);
        Assert.Contains("55=AAPL", fixString);
        Assert.Contains("54=1", fixString);
        Assert.Contains("38=100.00000000", fixString); // Decimal precision maintained
        Assert.Contains("44=150.25000000", fixString);
        Assert.EndsWith("\x01", fixString); // Ends with SOH
        Assert.Contains("10=", fixString); // Contains checksum
    }
    
    [Fact]
    public void Parse_ValidFixMessage_ParsesCorrectly()
    {
        // Arrange
        var fixString = "8=FIX.4.2\x019=154\x0135=D\x0149=DAYTRADER\x0156=NYSE\x01" +
                       "34=1\x0152=20231215-14:30:00.123\x0111=DT1234567890\x01" +
                       "55=AAPL\x0154=1\x0138=100\x0140=2\x0144=150.25\x0159=0\x0110=123\x01";
        
        // Act
        var message = FixMessage.Parse(fixString);
        
        // Assert
        Assert.Equal("FIX.4.2", message.BeginString);
        Assert.Equal(FixMessageTypes.NewOrderSingle, message.MsgType);
        Assert.Equal("DAYTRADER", message.SenderCompID);
        Assert.Equal("NYSE", message.TargetCompID);
        Assert.Equal(1, message.MsgSeqNum);
        Assert.Equal("DT1234567890", message.GetField(11));
        Assert.Equal("AAPL", message.GetField(55));
        Assert.Equal("1", message.GetField(54));
        Assert.Equal(100m, message.GetDecimalField(38));
        Assert.Equal(150.25m, message.GetDecimalField(44));
    }
    
    [Fact]
    public void SetField_DecimalValues_MaintainsFinancialPrecision()
    {
        // Arrange
        var message = new FixMessage();
        var price = 123.456789012345m; // High precision decimal
        
        // Act
        message.SetField(44, price);
        
        // Assert
        var retrievedPrice = message.GetDecimalField(44);
        Assert.Equal(price, retrievedPrice);
        
        // Verify string representation maintains precision
        var priceString = message.GetField(44);
        Assert.Equal("123.45678901", priceString);
    }
    
    [Fact]
    public void ToFixString_Performance_CompletesUnderMicrosecondTarget()
    {
        // Arrange
        var message = CreateComplexOrderMessage();
        var iterations = 1000;
        
        // Warm up JIT
        for (int i = 0; i < 100; i++)
        {
            _ = message.ToFixString();
        }
        
        // Act & Assert
        var startTime = DateTime.UtcNow;
        
        for (int i = 0; i < iterations; i++)
        {
            _ = message.ToFixString();
        }
        
        var totalTime = DateTime.UtcNow - startTime;
        var avgTimePerMessage = totalTime.TotalMicroseconds / iterations;
        
        // Should complete in under 10 microseconds per message for ultra-low latency
        Assert.True(avgTimePerMessage < 10.0, 
            $"Message generation took {avgTimePerMessage:F2}μs, exceeds 10μs target");
    }
    
    [Fact]
    public void Parse_Performance_CompletesUnderMicrosecondTarget()
    {
        // Arrange
        var fixString = CreateComplexFixString();
        var iterations = 1000;
        
        // Warm up JIT
        for (int i = 0; i < 100; i++)
        {
            _ = FixMessage.Parse(fixString);
        }
        
        // Act & Assert
        var startTime = DateTime.UtcNow;
        
        for (int i = 0; i < iterations; i++)
        {
            _ = FixMessage.Parse(fixString);
        }
        
        var totalTime = DateTime.UtcNow - startTime;
        var avgTimePerMessage = totalTime.TotalMicroseconds / iterations;
        
        // Should complete in under 5 microseconds per message for ultra-low latency
        Assert.True(avgTimePerMessage < 5.0, 
            $"Message parsing took {avgTimePerMessage:F2}μs, exceeds 5μs target");
    }
    
    [Fact]
    public void HardwareTimestamp_SetAndRetrieve_MaintainsNanosecondPrecision()
    {
        // Arrange
        var message = new FixMessage();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;
        
        // Act
        message.HardwareTimestamp = timestamp;
        
        // Assert
        Assert.Equal(timestamp, message.HardwareTimestamp);
        
        // Verify nanosecond precision is maintained
        var retrievedTimestamp = message.HardwareTimestamp;
        Assert.Equal(timestamp, retrievedTimestamp);
    }
    
    [Theory]
    [InlineData("0", "Heartbeat")]
    [InlineData("1", "TestRequest")]
    [InlineData("A", "Logon")]
    [InlineData("D", "NewOrderSingle")]
    [InlineData("8", "ExecutionReport")]
    [InlineData("F", "OrderCancelRequest")]
    [InlineData("G", "OrderCancelReplaceRequest")]
    public void MsgType_ValidTypes_SetsCorrectly(string msgType, string description)
    {
        // Arrange & Act
        var message = new FixMessage { MsgType = msgType };
        
        // Assert
        Assert.Equal(msgType, message.MsgType);
    }
    
    [Fact]
    public void GetField_NonExistentField_ReturnsNull()
    {
        // Arrange
        var message = new FixMessage();
        
        // Act
        var result = message.GetField(9999); // Non-existent tag
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void GetDecimalField_NonExistentField_ReturnsZero()
    {
        // Arrange
        var message = new FixMessage();
        
        // Act
        var result = message.GetDecimalField(9999); // Non-existent tag
        
        // Assert
        Assert.Equal(0m, result);
    }
    
    [Fact]
    public void GetIntField_NonExistentField_ReturnsZero()
    {
        // Arrange
        var message = new FixMessage();
        
        // Act
        var result = message.GetIntField(9999); // Non-existent tag
        
        // Assert
        Assert.Equal(0, result);
    }
    
    [Fact]
    public void ToFixString_USMarketOrder_IncludesRequiredFields()
    {
        // Arrange - Create typical US market order
        var message = new FixMessage
        {
            MsgType = FixMessageTypes.NewOrderSingle,
            SenderCompID = "DAYTRADER",
            TargetCompID = "NASDAQ"
        };
        
        message.SetField(11, "DT1639574400001"); // ClOrdID
        message.SetField(1, "ACCOUNT123"); // Account
        message.SetField(55, "MSFT"); // Symbol
        message.SetField(54, "1"); // Side (Buy)
        message.SetField(38, 500m); // OrderQty
        message.SetField(40, "1"); // OrdType (Market)
        message.SetField(59, "0"); // TimeInForce (Day)
        message.SetField(21, "1"); // HandlInst (Automated)
        message.SetField(100, "XNAS"); // ExDestination (NASDAQ MIC)
        
        // Act
        var fixString = message.ToFixString();
        
        // Assert - Verify US market compliance fields
        Assert.Contains("1=ACCOUNT123", fixString); // Account required
        Assert.Contains("21=1", fixString); // Automated execution
        Assert.Contains("100=XNAS", fixString); // Venue routing
        Assert.Contains("59=0", fixString); // Day order (US standard)
    }
    
    private static FixMessage CreateComplexOrderMessage()
    {
        var message = new FixMessage
        {
            MsgType = FixMessageTypes.NewOrderSingle,
            SenderCompID = "DAYTRADER",
            TargetCompID = "NYSE",
            MsgSeqNum = 12345
        };
        
        // Add multiple fields for complexity
        message.SetField(11, "DT1639574400001");
        message.SetField(1, "TESTACCOUNT");
        message.SetField(55, "AAPL");
        message.SetField(54, "1");
        message.SetField(38, 1000m);
        message.SetField(40, "2");
        message.SetField(44, 175.50m);
        message.SetField(59, "0");
        message.SetField(21, "1");
        message.SetField(100, "XNYS");
        message.SetField(18, "H"); // Hidden order
        
        return message;
    }
    
    private static string CreateComplexFixString()
    {
        return "8=FIX.4.2\x019=200\x0135=D\x0149=DAYTRADER\x0156=NYSE\x01" +
               "34=12345\x0152=20231215-14:30:00.123\x0111=DT1639574400001\x01" +
               "1=TESTACCOUNT\x0155=AAPL\x0154=1\x0138=1000\x0140=2\x01" +
               "44=175.50\x0159=0\x0121=1\x01100=XNYS\x0118=H\x0110=123\x01";
    }
}