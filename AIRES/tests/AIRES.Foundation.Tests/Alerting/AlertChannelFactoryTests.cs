using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AIRES.Foundation.Alerting;
using AIRES.Foundation.Alerting.Channels;
using AIRES.Foundation.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AIRES.Foundation.Tests.Alerting;

/// <summary>
/// Test suite for AlertChannelFactory.
/// Tests channel creation, configuration handling, and error scenarios.
/// </summary>
public class AlertChannelFactoryTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IAIRESLogger> _mockLogger;
    private readonly AlertChannelFactory _factory;
    private readonly IConfiguration _configuration;

    public AlertChannelFactoryTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockLogger = new Mock<IAIRESLogger>();
        
        // Create test configuration
        var inMemorySettings = new Dictionary<string, string>
        {
            ["Alerting:Channels:Console:Enabled"] = "true",
            ["Alerting:Channels:Console:MinimumSeverity"] = "Information",
            ["Alerting:Channels:LogFile:Enabled"] = "true",
            ["Alerting:Channels:LogFile:MinimumSeverity"] = "Warning",
            ["Alerting:Channels:AlertFile:Enabled"] = "false",
            ["Alerting:Channels:WindowsEventLog:Enabled"] = "false",
            ["Alerting:Channels:HealthEndpoint:Enabled"] = "false"
        };
        
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
        
        _factory = new AlertChannelFactory(_mockServiceProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public void CreateChannel_Console_ReturnsConsoleChannel()
    {
        // Act
        var channel = _factory.CreateChannel(AlertChannelType.Console, _configuration);
        
        // Assert
        Assert.NotNull(channel);
        Assert.IsType<ConsoleChannel>(channel);
        Assert.Equal("Console", channel.ChannelName);
        Assert.Equal(AlertChannelType.Console, channel.ChannelType);
    }

    [Fact]
    public void CreateChannel_LogFile_ReturnsLogFileChannel()
    {
        // Act
        var channel = _factory.CreateChannel(AlertChannelType.LogFile, _configuration);
        
        // Assert
        Assert.NotNull(channel);
        Assert.IsType<LogFileChannel>(channel);
        Assert.Equal("LogFile", channel.ChannelName);
        Assert.Equal(AlertChannelType.LogFile, channel.ChannelType);
    }

    [Fact]
    public void CreateChannel_AlertFile_ReturnsAlertFileChannel()
    {
        // Act
        var channel = _factory.CreateChannel(AlertChannelType.AlertFile, _configuration);
        
        // Assert
        Assert.NotNull(channel);
        Assert.IsType<AlertFileChannel>(channel);
        Assert.Equal("AlertFile", channel.ChannelName);
        Assert.Equal(AlertChannelType.AlertFile, channel.ChannelType);
    }

    [Fact]
    public void CreateChannel_WindowsEventLog_ReturnsWindowsEventLogChannel()
    {
        // Act
        var channel = _factory.CreateChannel(AlertChannelType.WindowsEventLog, _configuration);
        
        // Assert
        Assert.NotNull(channel);
        Assert.IsType<WindowsEventLogChannel>(channel);
        Assert.Equal("WindowsEventLog", channel.ChannelName);
        Assert.Equal(AlertChannelType.WindowsEventLog, channel.ChannelType);
    }

    [Fact]
    public void CreateChannel_HealthEndpoint_ReturnsHealthEndpointChannel()
    {
        // Act
        var channel = _factory.CreateChannel(AlertChannelType.HealthEndpoint, _configuration);
        
        // Assert
        Assert.NotNull(channel);
        Assert.IsType<HealthEndpointChannel>(channel);
        Assert.Equal("HealthEndpoint", channel.ChannelName);
        Assert.Equal(AlertChannelType.HealthEndpoint, channel.ChannelType);
    }

    [Fact]
    public void CreateChannel_InvalidChannelType_ThrowsNotSupportedException()
    {
        // Arrange
        var invalidChannelType = (AlertChannelType)999;
        
        // Act & Assert
        var exception = Assert.Throws<NotSupportedException>(
            () => _factory.CreateChannel(invalidChannelType, _configuration));
        
        Assert.Contains("999", exception.Message);
        Assert.Contains("not supported", exception.Message);
    }

    [Fact]
    public void CreateAllChannels_ReturnsOnlyEnabledChannels()
    {
        // Act
        var channels = _factory.CreateAllChannels(_configuration).ToList();
        
        // Assert
        Assert.Equal(2, channels.Count); // Only Console and LogFile are enabled
        
        var channelTypes = channels.Select(c => c.ChannelType).ToList();
        Assert.Contains(AlertChannelType.Console, channelTypes);
        Assert.Contains(AlertChannelType.LogFile, channelTypes);
        Assert.DoesNotContain(AlertChannelType.AlertFile, channelTypes);
        Assert.DoesNotContain(AlertChannelType.WindowsEventLog, channelTypes);
        Assert.DoesNotContain(AlertChannelType.HealthEndpoint, channelTypes);
    }

    [Fact]
    public void CreateAllChannels_WithAllEnabled_ReturnsAllChannels()
    {
        // Arrange
        var allEnabledConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Alerting:Channels:Console:Enabled"] = "true",
                ["Alerting:Channels:LogFile:Enabled"] = "true",
                ["Alerting:Channels:AlertFile:Enabled"] = "true",
                ["Alerting:Channels:WindowsEventLog:Enabled"] = "true",
                ["Alerting:Channels:HealthEndpoint:Enabled"] = "true"
            }!)
            .Build();
        
        // Act
        var channels = _factory.CreateAllChannels(allEnabledConfig).ToList();
        
        // Assert
        Assert.Equal(5, channels.Count);
        
        var channelTypes = channels.Select(c => c.ChannelType).ToList();
        foreach (AlertChannelType channelType in Enum.GetValues<AlertChannelType>())
        {
            Assert.Contains(channelType, channelTypes);
        }
    }

    [Fact]
    public void CreateAllChannels_WithNoneEnabled_ReturnsEmptyList()
    {
        // Arrange
        var noneEnabledConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Alerting:Channels:Console:Enabled"] = "false",
                ["Alerting:Channels:LogFile:Enabled"] = "false",
                ["Alerting:Channels:AlertFile:Enabled"] = "false",
                ["Alerting:Channels:WindowsEventLog:Enabled"] = "false",
                ["Alerting:Channels:HealthEndpoint:Enabled"] = "false"
            }!)
            .Build();
        
        // Act
        var channels = _factory.CreateAllChannels(noneEnabledConfig).ToList();
        
        // Assert
        Assert.Empty(channels);
    }

    [Fact]
    public void CreateAllChannels_WithMissingConfiguration_DefaultsToDisabled()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder().Build();
        
        // Act
        var channels = _factory.CreateAllChannels(emptyConfig).ToList();
        
        // Assert
        Assert.Empty(channels); // All channels default to disabled
    }

    [Fact]
    public void CreateAllChannels_WhenChannelCreationFails_ContinuesWithOthers()
    {
        // Arrange
        var partialConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Alerting:Channels:Console:Enabled"] = "true",
                ["Alerting:Channels:LogFile:Enabled"] = "true",
                ["Alerting:Channels:AlertFile:Enabled"] = "true"
            }!)
            .Build();
        
        // Create a factory that will throw for LogFile channel
        var mockFactory = new Mock<AlertChannelFactory>(_mockServiceProvider.Object, _mockLogger.Object);
        mockFactory.CallBase = true;
        mockFactory.Setup(f => f.CreateChannel(AlertChannelType.LogFile, It.IsAny<IConfiguration>()))
            .Throws(new InvalidOperationException("LogFile channel creation failed"));
        
        // Act
        var channels = mockFactory.Object.CreateAllChannels(partialConfig).ToList();
        
        // Assert
        Assert.Equal(2, channels.Count); // Console and AlertFile should still be created
        var channelTypes = channels.Select(c => c.ChannelType).ToList();
        Assert.Contains(AlertChannelType.Console, channelTypes);
        Assert.Contains(AlertChannelType.AlertFile, channelTypes);
        Assert.DoesNotContain(AlertChannelType.LogFile, channelTypes);
        
        // Verify error was logged
        _mockLogger.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Failed to create LogFile channel")), 
            It.IsAny<Exception>()), 
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new AlertChannelFactory(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new AlertChannelFactory(_mockServiceProvider.Object, null!));
    }

    [Fact]
    public void CreateChannel_LogsChannelCreation()
    {
        // Act
        _factory.CreateChannel(AlertChannelType.Console, _configuration);
        
        // Assert
        _mockLogger.Verify(l => l.LogInfo(
            It.Is<string>(msg => msg.Contains("Creating alert channel: Console"))), 
            Times.Once);
    }

    [Fact]
    public void CreateAllChannels_LogsCreationSummary()
    {
        // Act
        var channels = _factory.CreateAllChannels(_configuration).ToList();
        
        // Assert
        _mockLogger.Verify(l => l.LogInfo(
            It.Is<string>(msg => msg.Contains("Creating all configured alert channels"))), 
            Times.Once);
        
        _mockLogger.Verify(l => l.LogInfo(
            It.Is<string>(msg => msg.Contains($"Created {channels.Count} alert channels"))), 
            Times.Once);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("True", true)]
    [InlineData("FALSE", false)]
    [InlineData("invalid", false)] // Invalid values default to false
    [InlineData("", false)] // Empty values default to false
    public void CreateAllChannels_ParsesEnabledConfiguration(string configValue, bool expectedEnabled)
    {
        // Arrange
        var testConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Alerting:Channels:Console:Enabled"] = configValue
            }!)
            .Build();
        
        // Act
        var channels = _factory.CreateAllChannels(testConfig).ToList();
        
        // Assert
        if (expectedEnabled)
        {
            Assert.Single(channels);
            Assert.Equal(AlertChannelType.Console, channels[0].ChannelType);
        }
        else
        {
            Assert.Empty(channels);
        }
    }
}