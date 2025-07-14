using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using AIRES.Foundation.Alerting;
using AIRES.Foundation.Alerting.Channels;
using AIRES.Foundation.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Collections.Immutable;

namespace AIRES.Foundation.Tests.Alerting.Channels;

/// <summary>
/// Test suite for ConsoleChannel.
/// Tests console output, severity filtering, and metrics tracking.
/// </summary>
public class ConsoleChannelTests : IDisposable
{
    private readonly Mock<IAIRESLogger> _mockLogger;
    private readonly IConfiguration _configuration;
    private readonly ConsoleChannel _channel;
    private readonly StringWriter _consoleOutput;

    public ConsoleChannelTests()
    {
        _mockLogger = new Mock<IAIRESLogger>();
        
        // Create test configuration
        var inMemorySettings = new Dictionary<string, string>
        {
            ["Alerting:Channels:Console:Enabled"] = "true",
            ["Alerting:Channels:Console:MinimumSeverity"] = "Information",
        };
        
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
        
        // Redirect console output for testing
        _consoleOutput = new StringWriter();
        Console.SetOut(_consoleOutput);
        
        _channel = new ConsoleChannel(_mockLogger.Object, _configuration);
    }

    [Fact]
    public void Constructor_LoadsConfiguration()
    {
        // Assert
        Assert.Equal("Console", _channel.ChannelName);
        Assert.Equal(AlertChannelType.Console, _channel.ChannelType);
        Assert.True(_channel.IsEnabled);
        Assert.Equal(AlertSeverity.Information, _channel.MinimumSeverity);
    }

    [Fact]
    public async Task SendAlertAsync_WritesToConsole()
    {
        // Arrange
        var alert = new AlertMessage
        {
            Id = Guid.NewGuid(),
            Severity = AlertSeverity.Warning,
            Component = "TestComponent",
            Message = "Test warning message",
            Timestamp = DateTime.UtcNow
        };
        
        // Act
        await _channel.SendAlertAsync(alert);
        
        // Assert
        var output = _consoleOutput.ToString();
        Assert.Contains("TestComponent", output);
        Assert.Contains("Test warning message", output);
        Assert.Contains("Warning", output);
    }

    [Fact]
    public async Task SendAlertAsync_WithDetails_IncludesDetailsInOutput()
    {
        // Arrange
        var alert = new AlertMessage
        {
            Id = Guid.NewGuid(),
            Severity = AlertSeverity.Error,
            Component = "TestComponent",
            Message = "Error with details",
            Details = new Dictionary<string, object>
            {
                ["ErrorCode"] = "TEST_ERROR",
                ["Count"] = 42
            }.ToImmutableDictionary(),
            Timestamp = DateTime.UtcNow
        };
        
        // Act
        await _channel.SendAlertAsync(alert);
        
        // Assert
        var output = _consoleOutput.ToString();
        Assert.Contains("TEST_ERROR", output);
        Assert.Contains("42", output);
    }

    [Fact]
    public async Task SendAlertAsync_WithSuggestedAction_IncludesActionInOutput()
    {
        // Arrange
        var alert = new AlertMessage
        {
            Id = Guid.NewGuid(),
            Severity = AlertSeverity.Critical,
            Component = "TestComponent",
            Message = "Critical error",
            SuggestedAction = "Restart the service immediately",
            Timestamp = DateTime.UtcNow
        };
        
        // Act
        await _channel.SendAlertAsync(alert);
        
        // Assert
        var output = _consoleOutput.ToString();
        Assert.Contains("Restart the service immediately", output);
    }

    [Fact]
    public async Task SendAlertAsync_BelowMinimumSeverity_DoesNotWrite()
    {
        // Arrange
        var testConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Alerting:Channels:Console:Enabled"] = "true",
                ["Alerting:Channels:Console:MinimumSeverity"] = "Warning"
            }!)
            .Build();
        
        var channel = new ConsoleChannel(_mockLogger.Object, testConfig);
        
        var alert = new AlertMessage
        {
            Severity = AlertSeverity.Information,
            Component = "Test",
            Message = "Should not appear"
        };
        
        // Act
        await channel.SendAlertAsync(alert);
        
        // Assert
        var output = _consoleOutput.ToString();
        Assert.DoesNotContain("Should not appear", output);
    }

    [Fact]
    public async Task SendAlertAsync_WhenDisabled_DoesNotWrite()
    {
        // Arrange
        var testConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Alerting:Channels:Console:Enabled"] = "false",
                ["Alerting:Channels:Console:MinimumSeverity"] = "Information"
            }!)
            .Build();
        
        var channel = new ConsoleChannel(_mockLogger.Object, testConfig);
        
        var alert = new AlertMessage
        {
            Severity = AlertSeverity.Critical,
            Component = "Test",
            Message = "Should not appear when disabled"
        };
        
        // Act
        await channel.SendAlertAsync(alert);
        
        // Assert
        var output = _consoleOutput.ToString();
        Assert.DoesNotContain("Should not appear when disabled", output);
    }

    [Fact]
    public async Task IsHealthyAsync_ReturnsTrue()
    {
        // Act
        var result = await _channel.IsHealthyAsync();
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetMetricsAsync_ReturnsMetrics()
    {
        // Arrange
        // Send some alerts to generate metrics
        await _channel.SendAlertAsync(new AlertMessage 
        { 
            Severity = AlertSeverity.Information, 
            Component = "Test", 
            Message = "Info" 
        });
        
        await _channel.SendAlertAsync(new AlertMessage 
        { 
            Severity = AlertSeverity.Warning, 
            Component = "Test", 
            Message = "Warning" 
        });
        
        await _channel.SendAlertAsync(new AlertMessage 
        { 
            Severity = AlertSeverity.Warning, 
            Component = "Test", 
            Message = "Warning 2" 
        });
        
        // Act
        var metrics = await _channel.GetMetricsAsync();
        
        // Assert
        Assert.Equal("Console", metrics["ChannelName"]);
        Assert.Equal(true, metrics["IsEnabled"]);
        Assert.Equal(3L, metrics["TotalAlerts"]);
        Assert.Equal(1L, metrics["Alerts_Information"]);
        Assert.Equal(2L, metrics["Alerts_Warning"]);
    }

    [Fact]
    public async Task SendAlertAsync_HandlesNullDetails()
    {
        // Arrange
        var alert = new AlertMessage
        {
            Severity = AlertSeverity.Error,
            Component = "Test",
            Message = "Error without details",
            Details = null
        };
        
        // Act & Assert - Should not throw
        await _channel.SendAlertAsync(alert);
        
        var output = _consoleOutput.ToString();
        Assert.Contains("Error without details", output);
    }

    [Fact]
    public async Task SendAlertAsync_WithCancellation_RespectsCancellationToken()
    {
        // Arrange
        var alert = new AlertMessage
        {
            Severity = AlertSeverity.Warning,
            Component = "Test",
            Message = "Cancellable alert"
        };
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // Act & Assert - Should handle cancellation gracefully
        await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await _channel.SendAlertAsync(alert, cts.Token));
    }

    [Theory]
    [InlineData(AlertSeverity.Information)]
    [InlineData(AlertSeverity.Warning)]
    [InlineData(AlertSeverity.Error)]
    [InlineData(AlertSeverity.Critical)]
    public async Task SendAlertAsync_UpdatesMetricsForEachSeverity(AlertSeverity severity)
    {
        // Arrange
        var alert = new AlertMessage
        {
            Severity = severity,
            Component = "Test",
            Message = $"{severity} message"
        };
        
        // Act
        await _channel.SendAlertAsync(alert);
        
        // Assert
        var metrics = await _channel.GetMetricsAsync();
        Assert.Equal(1L, metrics[$"Alerts_{severity}"]);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Act & Assert
        _channel.Dispose();
    }
    
    public void Dispose()
    {
        _channel?.Dispose();
        _consoleOutput?.Dispose();
        GC.SuppressFinalize(this);
    }
}