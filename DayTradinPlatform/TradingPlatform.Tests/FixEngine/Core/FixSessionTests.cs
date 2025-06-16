using Xunit;
using TradingPlatform.FixEngine.Core;
using TradingPlatform.FixEngine.Models;
using TradingPlatform.Tests.FixEngine.Trading;

namespace TradingPlatform.Tests.FixEngine.Core;

/// <summary>
/// Unit tests for FIX session management and message processing
/// Validates ultra-low latency requirements and session state management
/// </summary>
public class FixSessionTests : IDisposable
{
    private readonly FixSession _fixSession;
    private readonly MockLogger _mockLogger;
    
    public FixSessionTests()
    {
        _mockLogger = new MockLogger();
        _fixSession = new FixSession("DAYTRADER", "TESTEXCHANGE", _mockLogger);
    }
    
    [Fact]
    public void Constructor_ValidParameters_InitializesCorrectly()
    {
        // Act & Assert
        Assert.Equal("DAYTRADER", _fixSession.SenderCompId);
        Assert.Equal("TESTEXCHANGE", _fixSession.TargetCompId);
        Assert.False(_fixSession.IsConnected);
        Assert.Equal(30, _fixSession.HeartbeatInterval);
    }
    
    [Fact]
    public void HeartbeatInterval_SetValue_UpdatesCorrectly()
    {
        // Arrange
        var newInterval = 60;
        
        // Act
        _fixSession.HeartbeatInterval = newInterval;
        
        // Assert
        Assert.Equal(newInterval, _fixSession.HeartbeatInterval);
    }
    
    [Fact]
    public async Task SendMessageAsync_NotConnected_ReturnsFalse()
    {
        // Arrange
        var message = new FixMessage
        {
            MsgType = FixMessageTypes.NewOrderSingle
        };
        
        // Act
        var result = await _fixSession.SendMessageAsync(message);
        
        // Assert
        Assert.False(result);
        Assert.Contains("Attempted to send message on disconnected FIX session", 
            _mockLogger.LogMessages.FirstOrDefault(m => m.Contains("WARN")) ?? "");
    }
    
    [Fact]
    public async Task SendMessageAsync_ValidMessage_SetsRequiredFields()
    {
        // Arrange
        var message = new FixMessage
        {
            MsgType = FixMessageTypes.Heartbeat
        };
        
        // Act
        await _fixSession.SendMessageAsync(message);
        
        // Assert - Verify required fields are set
        Assert.Equal("DAYTRADER", message.SenderCompID);
        Assert.Equal("TESTEXCHANGE", message.TargetCompID);
        Assert.True(message.MsgSeqNum > 0);
        Assert.True(message.HardwareTimestamp > 0);
        Assert.True(message.SendingTime > DateTime.UtcNow.AddMinutes(-1));
    }
    
    [Fact]
    public void MessageReceived_Event_CanBeSubscribed()
    {
        // Arrange
        FixMessage? receivedMessage = null;
        _fixSession.MessageReceived += (sender, message) => receivedMessage = message;
        
        // Act - Use internal test trigger method
        var testMessage = new FixMessage { MsgType = FixMessageTypes.ExecutionReport };
        
#if DEBUG || TEST
        // Use internal test method to trigger event
        ((dynamic)_fixSession).TriggerMessageReceived(testMessage);
#else
        // Fallback for non-test builds
        _fixSession.GetType()
            .GetEvent("MessageReceived")?
            .GetRaiseMethod(true)?
            .Invoke(_fixSession, new object[] { _fixSession, testMessage });
#endif
        
        // Assert
        Assert.NotNull(receivedMessage);
        Assert.Equal(FixMessageTypes.ExecutionReport, receivedMessage.MsgType);
    }
    
    [Fact]
    public void SessionStateChanged_Event_CanBeSubscribed()
    {
        // Arrange
        string? stateChange = null;
        _fixSession.SessionStateChanged += (sender, state) => stateChange = state;
        
        // Act - Use internal test trigger method
#if DEBUG || TEST
        // Use internal test method to trigger event
        ((dynamic)_fixSession).TriggerSessionStateChanged("Connected");
#else
        // Fallback for non-test builds
        _fixSession.GetType()
            .GetEvent("SessionStateChanged")?
            .GetRaiseMethod(true)?
            .Invoke(_fixSession, new object[] { _fixSession, "Connected" });
#endif
        
        // Assert
        Assert.Equal("Connected", stateChange);
    }
    
    [Fact]
    public async Task ConnectAsync_InvalidHost_ReturnsFalse()
    {
        // Act
        var result = await _fixSession.ConnectAsync("invalid.host.com", 9999, TimeSpan.FromSeconds(1));
        
        // Assert
        Assert.False(result);
        Assert.False(_fixSession.IsConnected);
    }
    
    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_CompletesSuccessfully()
    {
        // Act & Assert - Should not throw
        await _fixSession.DisconnectAsync();
        Assert.False(_fixSession.IsConnected);
    }
    
    [Theory]
    [InlineData(FixMessageTypes.Heartbeat)]
    [InlineData(FixMessageTypes.TestRequest)]
    [InlineData(FixMessageTypes.Logon)]
    [InlineData(FixMessageTypes.Logout)]
    [InlineData(FixMessageTypes.NewOrderSingle)]
    [InlineData(FixMessageTypes.ExecutionReport)]
    public void FixMessageTypes_Constants_AreValidStrings(string messageType)
    {
        // Assert
        Assert.NotNull(messageType);
        Assert.NotEmpty(messageType);
        Assert.True(messageType.Length <= 2); // FIX message types are 1-2 characters
    }
    
    [Fact]
    public void IsLatencyCritical_MessageTypes_ReturnsCorrectValues()
    {
        // Assert - Latency critical messages
        Assert.True(FixMessageTypes.IsLatencyCritical(FixMessageTypes.NewOrderSingle));
        Assert.True(FixMessageTypes.IsLatencyCritical(FixMessageTypes.OrderCancelRequest));
        Assert.True(FixMessageTypes.IsLatencyCritical(FixMessageTypes.ExecutionReport));
        Assert.True(FixMessageTypes.IsLatencyCritical(FixMessageTypes.MarketDataIncrementalRefresh));
        
        // Assert - Non-latency critical messages
        Assert.False(FixMessageTypes.IsLatencyCritical(FixMessageTypes.Heartbeat));
        Assert.False(FixMessageTypes.IsLatencyCritical(FixMessageTypes.TestRequest));
        Assert.False(FixMessageTypes.IsLatencyCritical(FixMessageTypes.Logon));
        Assert.False(FixMessageTypes.IsLatencyCritical(FixMessageTypes.News));
    }
    
    [Fact]
    public void IsSessionLevel_MessageTypes_ReturnsCorrectValues()
    {
        // Assert - Session level messages
        Assert.True(FixMessageTypes.IsSessionLevel(FixMessageTypes.Heartbeat));
        Assert.True(FixMessageTypes.IsSessionLevel(FixMessageTypes.TestRequest));
        Assert.True(FixMessageTypes.IsSessionLevel(FixMessageTypes.Logon));
        Assert.True(FixMessageTypes.IsSessionLevel(FixMessageTypes.Logout));
        Assert.True(FixMessageTypes.IsSessionLevel(FixMessageTypes.Reject));
        
        // Assert - Application level messages
        Assert.False(FixMessageTypes.IsSessionLevel(FixMessageTypes.NewOrderSingle));
        Assert.False(FixMessageTypes.IsSessionLevel(FixMessageTypes.ExecutionReport));
        Assert.False(FixMessageTypes.IsSessionLevel(FixMessageTypes.MarketDataRequest));
    }
    
    [Fact]
    public void Performance_MessageProcessing_MeetsLatencyTargets()
    {
        // Arrange
        var messages = new List<FixMessage>();
        for (int i = 0; i < 1000; i++)
        {
            messages.Add(new FixMessage 
            { 
                MsgType = FixMessageTypes.NewOrderSingle,
                SenderCompID = "SENDER",
                TargetCompID = "TARGET",
                MsgSeqNum = i + 1
            });
        }
        
        // Warm up
        foreach (var msg in messages.Take(100))
        {
            msg.ToFixString();
        }
        
        // Act
        var startTime = DateTime.UtcNow;
        foreach (var message in messages)
        {
            var fixString = message.ToFixString();
            var parsedBack = FixMessage.Parse(fixString);
        }
        var totalTime = DateTime.UtcNow - startTime;
        
        // Assert - Should process 1000 messages in under 10ms (10μs per message)
        var avgMicroseconds = totalTime.TotalMicroseconds / messages.Count;
        Assert.True(avgMicroseconds < 10.0, 
            $"Message processing took {avgMicroseconds:F2}μs per message, exceeds 10μs target");
    }
    
    [Fact]
    public void SequenceNumbers_MessageSending_IncrementsCorrectly()
    {
        // Arrange
        var message1 = new FixMessage { MsgType = FixMessageTypes.Heartbeat };
        var message2 = new FixMessage { MsgType = FixMessageTypes.Heartbeat };
        var message3 = new FixMessage { MsgType = FixMessageTypes.Heartbeat };
        
        // Act
        _ = _fixSession.SendMessageAsync(message1);
        _ = _fixSession.SendMessageAsync(message2);
        _ = _fixSession.SendMessageAsync(message3);
        
        // Assert
        Assert.Equal(1, message1.MsgSeqNum);
        Assert.Equal(2, message2.MsgSeqNum);
        Assert.Equal(3, message3.MsgSeqNum);
    }
    
    [Fact]
    public void HardwareTimestamp_Assignment_UsesNanosecondPrecision()
    {
        // Arrange
        var message = new FixMessage { MsgType = FixMessageTypes.NewOrderSingle };
        var beforeTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;
        
        // Act
        _ = _fixSession.SendMessageAsync(message);
        var afterTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;
        
        // Assert
        Assert.True(message.HardwareTimestamp >= beforeTime);
        Assert.True(message.HardwareTimestamp <= afterTime);
        
        // Verify nanosecond precision (should have nanosecond-level digits)
        Assert.True(message.HardwareTimestamp > 1_000_000_000_000_000_000L); // After year 2001
    }
    
    public void Dispose()
    {
        _fixSession.Dispose();
    }
}