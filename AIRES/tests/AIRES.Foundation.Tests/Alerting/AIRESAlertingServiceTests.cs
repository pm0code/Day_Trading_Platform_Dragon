using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using AIRES.Foundation.Alerting;
using AIRES.Foundation.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace AIRES.Foundation.Tests.Alerting;

/// <summary>
/// Comprehensive test suite for AIRESAlertingService.
/// Tests multi-channel delivery, throttling, persistence, and error handling.
/// </summary>
public class AIRESAlertingServiceTests : IDisposable
{
    private readonly Mock<IAIRESLogger> _mockLogger;
    private readonly Mock<IAlertChannelFactory> _mockChannelFactory;
    private readonly Mock<IAlertThrottler> _mockThrottler;
    private readonly Mock<IAlertPersistence> _mockPersistence;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly AIRESAlertingService _alertingService;
    private readonly List<Mock<IAlertChannel>> _mockChannels;

    public AIRESAlertingServiceTests()
    {
        _mockLogger = new Mock<IAIRESLogger>();
        _mockChannelFactory = new Mock<IAlertChannelFactory>();
        _mockThrottler = new Mock<IAlertThrottler>();
        _mockPersistence = new Mock<IAlertPersistence>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        // Setup mock channels
        _mockChannels = new List<Mock<IAlertChannel>>();
        for (int i = 0; i < 3; i++)
        {
            var mockChannel = new Mock<IAlertChannel>();
            mockChannel.Setup(c => c.ChannelName).Returns($"Channel{i}");
            mockChannel.Setup(c => c.IsEnabled).Returns(true);
            mockChannel.Setup(c => c.MinimumSeverity).Returns(AlertSeverity.Information);
            _mockChannels.Add(mockChannel);
        }
        
        _mockChannelFactory.Setup(f => f.CreateAllChannels(It.IsAny<IConfiguration>()))
            .Returns(_mockChannels.Select(m => m.Object));
        
        _alertingService = new AIRESAlertingService(
            _mockLogger.Object,
            _mockChannelFactory.Object,
            _mockThrottler.Object,
            _mockPersistence.Object,
            _mockConfiguration.Object);
    }

    [Fact]
    public async Task RaiseAlertAsync_WithValidAlert_PersistsAndDeliversToAllEnabledChannels()
    {
        // Arrange
        var severity = AlertSeverity.Warning;
        var component = "TestComponent";
        var message = "Test warning message";
        var details = new Dictionary<string, object> { ["key"] = "value" };
        
        _mockThrottler.Setup(t => t.ShouldThrottle(It.IsAny<string>(), severity))
            .Returns(false);
        
        _mockPersistence.Setup(p => p.SaveAlertAsync(It.IsAny<AlertMessage>()))
            .ReturnsAsync(new AlertRecord { Id = Guid.NewGuid() });
        
        foreach (var mockChannel in _mockChannels)
        {
            mockChannel.Setup(c => c.SendAlertAsync(It.IsAny<AlertMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }
        
        // Act
        await _alertingService.RaiseAlertAsync(severity, component, message, details);
        
        // Assert
        _mockPersistence.Verify(p => p.SaveAlertAsync(It.Is<AlertMessage>(a => 
            a.Severity == severity &&
            a.Component == component &&
            a.Message == message)), Times.Once);
            
        foreach (var mockChannel in _mockChannels)
        {
            mockChannel.Verify(c => c.SendAlertAsync(It.IsAny<AlertMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        
        _mockThrottler.Verify(t => t.RecordAlert(It.IsAny<string>(), severity), Times.Once);
    }

    [Fact]
    public async Task RaiseAlertAsync_WhenThrottled_DoesNotPersistOrDeliver()
    {
        // Arrange
        var severity = AlertSeverity.Information;
        var component = "TestComponent";
        var message = "Test info message";
        
        _mockThrottler.Setup(t => t.ShouldThrottle(It.IsAny<string>(), severity))
            .Returns(true);
        
        // Act
        await _alertingService.RaiseAlertAsync(severity, component, message);
        
        // Assert
        _mockPersistence.Verify(p => p.SaveAlertAsync(It.IsAny<AlertMessage>()), Times.Never);
        foreach (var mockChannel in _mockChannels)
        {
            mockChannel.Verify(c => c.SendAlertAsync(It.IsAny<AlertMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        _mockThrottler.Verify(t => t.RecordAlert(It.IsAny<string>(), It.IsAny<AlertSeverity>()), Times.Never);
    }

    [Fact]
    public async Task RaiseAlertAsync_WithCriticalAlert_NeverThrottled()
    {
        // Arrange
        var severity = AlertSeverity.Critical;
        var component = "TestComponent";
        var message = "Critical error!";
        
        _mockThrottler.Setup(t => t.ShouldThrottle(It.IsAny<string>(), severity))
            .Returns(false); // Critical alerts should never be throttled
        
        _mockPersistence.Setup(p => p.SaveAlertAsync(It.IsAny<AlertMessage>()))
            .ReturnsAsync(new AlertRecord { Id = Guid.NewGuid() });
        
        // Act
        await _alertingService.RaiseAlertAsync(severity, component, message);
        
        // Assert
        _mockPersistence.Verify(p => p.SaveAlertAsync(It.IsAny<AlertMessage>()), Times.Once);
    }

    [Fact]
    public async Task RaiseAlertAsync_OnlyDeliversToChannelsAboveMinimumSeverity()
    {
        // Arrange
        var severity = AlertSeverity.Warning;
        
        // Set different minimum severities for channels
        _mockChannels[0].Setup(c => c.MinimumSeverity).Returns(AlertSeverity.Information);
        _mockChannels[1].Setup(c => c.MinimumSeverity).Returns(AlertSeverity.Warning);
        _mockChannels[2].Setup(c => c.MinimumSeverity).Returns(AlertSeverity.Error);
        
        _mockThrottler.Setup(t => t.ShouldThrottle(It.IsAny<string>(), severity))
            .Returns(false);
        
        _mockPersistence.Setup(p => p.SaveAlertAsync(It.IsAny<AlertMessage>()))
            .ReturnsAsync(new AlertRecord { Id = Guid.NewGuid() });
        
        // Act
        await _alertingService.RaiseAlertAsync(severity, "Test", "Warning message");
        
        // Assert
        _mockChannels[0].Verify(c => c.SendAlertAsync(It.IsAny<AlertMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockChannels[1].Verify(c => c.SendAlertAsync(It.IsAny<AlertMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockChannels[2].Verify(c => c.SendAlertAsync(It.IsAny<AlertMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RaiseAlertAsync_WhenChannelFails_ContinuesWithOtherChannels()
    {
        // Arrange
        _mockThrottler.Setup(t => t.ShouldThrottle(It.IsAny<string>(), It.IsAny<AlertSeverity>()))
            .Returns(false);
        
        _mockPersistence.Setup(p => p.SaveAlertAsync(It.IsAny<AlertMessage>()))
            .ReturnsAsync(new AlertRecord { Id = Guid.NewGuid() });
        
        // First channel throws exception
        _mockChannels[0].Setup(c => c.SendAlertAsync(It.IsAny<AlertMessage>(), default))
            .ThrowsAsync(new InvalidOperationException("Channel failed"));
        
        // Other channels should still be called
        _mockChannels[1].Setup(c => c.SendAlertAsync(It.IsAny<AlertMessage>(), default))
            .Returns(Task.CompletedTask);
        _mockChannels[2].Setup(c => c.SendAlertAsync(It.IsAny<AlertMessage>(), default))
            .Returns(Task.CompletedTask);
        
        // Act
        await _alertingService.RaiseAlertAsync(AlertSeverity.Error, "Test", "Error message");
        
        // Assert - All channels should be attempted
        foreach (var mockChannel in _mockChannels)
        {
            mockChannel.Verify(c => c.SendAlertAsync(It.IsAny<AlertMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task GetHealthStatusAsync_WithNoAlerts_ReturnsHealthy()
    {
        // Arrange
        var stats = new AlertStatistics
        {
            TotalAlerts = 0,
            CriticalAlerts = 0,
            ErrorAlerts = 0,
            WarningAlerts = 0,
            InformationAlerts = 0
        };
        
        _mockPersistence.Setup(p => p.GetStatisticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(stats);
        
        foreach (var mockChannel in _mockChannels)
        {
            mockChannel.Setup(c => c.IsHealthyAsync()).ReturnsAsync(true);
        }
        
        // Act
        var result = await _alertingService.GetHealthStatusAsync();
        
        // Assert
        Assert.True(result.IsHealthy);
        Assert.Equal("Healthy", result.Status);
        Assert.Equal(0L, result.Details!["CriticalAlerts"]);
        Assert.Equal(0L, result.Details["ErrorAlerts"]);
    }

    [Fact]
    public async Task GetHealthStatusAsync_WithCriticalAlerts_ReturnsUnhealthy()
    {
        // Arrange
        var stats = new AlertStatistics
        {
            TotalAlerts = 10,
            CriticalAlerts = 2,
            ErrorAlerts = 3,
            WarningAlerts = 5,
            InformationAlerts = 0
        };
        
        _mockPersistence.Setup(p => p.GetStatisticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(stats);
        
        foreach (var mockChannel in _mockChannels)
        {
            mockChannel.Setup(c => c.IsHealthyAsync()).ReturnsAsync(true);
        }
        
        // Act
        var result = await _alertingService.GetHealthStatusAsync();
        
        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal("Critical", result.Status);
        Assert.Equal(2L, result.Details!["CriticalAlerts"]);
    }

    [Fact]
    public async Task GetHealthStatusAsync_WithUnhealthyChannel_ReturnsDegraded()
    {
        // Arrange
        var stats = new AlertStatistics
        {
            TotalAlerts = 5,
            CriticalAlerts = 0,
            ErrorAlerts = 2,
            WarningAlerts = 3,
            InformationAlerts = 0
        };
        
        _mockPersistence.Setup(p => p.GetStatisticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(stats);
        
        // One channel is unhealthy
        _mockChannels[0].Setup(c => c.IsHealthyAsync()).ReturnsAsync(false);
        _mockChannels[1].Setup(c => c.IsHealthyAsync()).ReturnsAsync(true);
        _mockChannels[2].Setup(c => c.IsHealthyAsync()).ReturnsAsync(true);
        
        // Act
        var result = await _alertingService.GetHealthStatusAsync();
        
        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal("Degraded", result.Status);
        var unhealthyChannels = result.Details!["UnhealthyChannels"] as List<string>;
        Assert.NotNull(unhealthyChannels);
        Assert.Contains("Channel0", unhealthyChannels!);
    }

    [Fact]
    public async Task GetAlertStatisticsAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        var period = TimeSpan.FromHours(1);
        var expectedStats = new AlertStatistics
        {
            TotalAlerts = 100,
            CriticalAlerts = 5,
            ErrorAlerts = 15,
            WarningAlerts = 30,
            InformationAlerts = 50,
            PeriodStart = DateTime.UtcNow.AddHours(-1),
            PeriodEnd = DateTime.UtcNow
        };
        
        _mockPersistence.Setup(p => p.GetStatisticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(expectedStats);
        
        // Act
        var result = await _alertingService.GetAlertStatisticsAsync(period);
        
        // Assert
        Assert.Equal(expectedStats.TotalAlerts, result.TotalAlerts);
        Assert.Equal(expectedStats.CriticalAlerts, result.CriticalAlerts);
        Assert.Equal(expectedStats.ErrorAlerts, result.ErrorAlerts);
        Assert.Equal(expectedStats.WarningAlerts, result.WarningAlerts);
        Assert.Equal(expectedStats.InformationAlerts, result.InformationAlerts);
    }

    [Fact]
    public async Task RaiseAlertAsync_GeneratesCorrectSuggestedAction()
    {
        // Arrange
        _mockThrottler.Setup(t => t.ShouldThrottle(It.IsAny<string>(), It.IsAny<AlertSeverity>()))
            .Returns(false);
        
        AlertMessage? capturedAlert = null;
        _mockPersistence.Setup(p => p.SaveAlertAsync(It.IsAny<AlertMessage>()))
            .Callback<AlertMessage>(alert => capturedAlert = alert)
            .ReturnsAsync(new AlertRecord { Id = Guid.NewGuid() });
        
        // Act
        await _alertingService.RaiseAlertAsync(
            AlertSeverity.Critical, 
            "TestService", 
            "Service unavailable");
        
        // Assert
        Assert.NotNull(capturedAlert);
        Assert.NotNull(capturedAlert);
        Assert.Equal("Check if the service is running and accessible", capturedAlert!.SuggestedAction);
    }

    [Fact]
    public void Constructor_WithNullDependencies_ThrowsArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => new AIRESAlertingService(
            null!, 
            _mockChannelFactory.Object, 
            _mockThrottler.Object, 
            _mockPersistence.Object, 
            _mockConfiguration.Object));
            
        Assert.Throws<ArgumentNullException>(() => new AIRESAlertingService(
            _mockLogger.Object, 
            null!, 
            _mockThrottler.Object, 
            _mockPersistence.Object, 
            _mockConfiguration.Object));
            
        Assert.Throws<ArgumentNullException>(() => new AIRESAlertingService(
            _mockLogger.Object, 
            _mockChannelFactory.Object, 
            null!, 
            _mockPersistence.Object, 
            _mockConfiguration.Object));
            
        Assert.Throws<ArgumentNullException>(() => new AIRESAlertingService(
            _mockLogger.Object, 
            _mockChannelFactory.Object, 
            _mockThrottler.Object, 
            null!, 
            _mockConfiguration.Object));
            
        Assert.Throws<ArgumentNullException>(() => new AIRESAlertingService(
            _mockLogger.Object, 
            _mockChannelFactory.Object, 
            _mockThrottler.Object, 
            _mockPersistence.Object, 
            null!));
    }
    
    public void Dispose()
    {
        _alertingService?.Dispose();
        GC.SuppressFinalize(this);
    }
}