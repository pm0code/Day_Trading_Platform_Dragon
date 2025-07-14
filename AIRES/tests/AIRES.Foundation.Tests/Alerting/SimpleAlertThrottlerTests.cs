using Xunit;
using Microsoft.Extensions.Configuration;
using AIRES.Foundation.Alerting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace AIRES.Foundation.Tests.Alerting;

/// <summary>
/// Test suite for SimpleAlertThrottler.
/// Tests rate limiting, same alert throttling, and critical alert bypass.
/// </summary>
public class SimpleAlertThrottlerTests : IDisposable
{
    private readonly SimpleAlertThrottler _throttler;
    private readonly IConfiguration _configuration;

    public SimpleAlertThrottlerTests()
    {
        // Create test configuration
        var inMemorySettings = new Dictionary<string, string>
        {
            ["Alerting:Throttling:SameAlertIntervalSeconds"] = "60",
            ["Alerting:Throttling:MaxAlertsPerMinute"] = "10",
        };
        
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
        
        _throttler = new SimpleAlertThrottler(_configuration);
    }

    [Fact]
    public void ShouldThrottle_FirstAlert_ReturnsFalse()
    {
        // Arrange
        var alertKey = "test-alert";
        var severity = AlertSeverity.Warning;
        
        // Act
        var result = _throttler.ShouldThrottle(alertKey, severity);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldThrottle_CriticalAlert_AlwaysReturnsFalse()
    {
        // Arrange
        var alertKey = "critical-alert";
        var severity = AlertSeverity.Critical;
        
        // Record multiple alerts
        for (int i = 0; i < 20; i++)
        {
            _throttler.RecordAlert(alertKey, severity);
        }
        
        // Act - Critical alerts should never be throttled
        var result = _throttler.ShouldThrottle(alertKey, severity);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldThrottle_SameAlertWithinInterval_ReturnsTrue()
    {
        // Arrange
        var alertKey = "duplicate-alert";
        var severity = AlertSeverity.Warning;
        
        // Record first alert
        _throttler.RecordAlert(alertKey, severity);
        
        // Act - Try same alert immediately
        var result = _throttler.ShouldThrottle(alertKey, severity);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ShouldThrottle_SameAlertAfterInterval_ReturnsFalse()
    {
        // Arrange
        var alertKey = "delayed-alert";
        var severity = AlertSeverity.Information;
        
        // Create throttler with short interval for testing
        var testConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Alerting:Throttling:SameAlertIntervalSeconds"] = "1",
                ["Alerting:Throttling:MaxAlertsPerMinute"] = "10"
            }!)
            .Build();
        
        using var testThrottler = new SimpleAlertThrottler(testConfig);
        
        // Record first alert
        testThrottler.RecordAlert(alertKey, severity);
        
        // Wait for interval to pass
        await Task.Delay(1100);
        
        // Act
        var result = testThrottler.ShouldThrottle(alertKey, severity);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldThrottle_ExceedsMaxAlertsPerMinute_ReturnsTrue()
    {
        // Arrange
        var severity = AlertSeverity.Warning;
        
        // Record 10 different alerts (max per minute)
        for (int i = 0; i < 10; i++)
        {
            var alertKey = $"alert-{i}";
            _throttler.RecordAlert(alertKey, severity);
        }
        
        // Act - 11th alert should be throttled
        var result = _throttler.ShouldThrottle("alert-11", severity);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void RecordAlert_UpdatesStatistics()
    {
        // Arrange
        var alertKey = "stats-alert";
        var severity = AlertSeverity.Error;
        
        // Act
        _throttler.RecordAlert(alertKey, severity);
        
        // Assert
        var stats = _throttler.GetStatisticsAsync().Result;
        Assert.Equal(1, stats.TotalAlertsSent);
        Assert.Equal(0, stats.TotalAlertsThrottled);
        Assert.True(stats.AlertsBySeverity.ContainsKey(severity));
        Assert.Equal(1, stats.AlertsBySeverity[severity]);
    }

    [Fact]
    public void RecordAlert_TracksThrottledAlerts()
    {
        // Arrange
        var alertKey = "throttled-alert";
        var severity = AlertSeverity.Warning;
        
        // Record first alert
        _throttler.RecordAlert(alertKey, severity);
        
        // Try to send same alert (will be throttled)
        var wasThrottled = _throttler.ShouldThrottle(alertKey, severity);
        Assert.True(wasThrottled);
        
        // Act & Assert
        var stats = _throttler.GetStatisticsAsync().Result;
        Assert.Equal(1, stats.TotalAlertsSent);
        Assert.Equal(1, stats.TotalAlertsThrottled);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsCorrectCounts()
    {
        // Arrange & Act
        // Send various alerts
        _throttler.RecordAlert("info-1", AlertSeverity.Information);
        _throttler.RecordAlert("info-2", AlertSeverity.Information);
        _throttler.RecordAlert("warn-1", AlertSeverity.Warning);
        _throttler.RecordAlert("error-1", AlertSeverity.Error);
        _throttler.RecordAlert("critical-1", AlertSeverity.Critical);
        
        // Try to send duplicate (will be throttled)
        _throttler.ShouldThrottle("info-1", AlertSeverity.Information);
        
        var stats = await _throttler.GetStatisticsAsync();
        
        // Assert
        Assert.Equal(5, stats.TotalAlertsSent);
        Assert.Equal(1, stats.TotalAlertsThrottled);
        Assert.Equal(2, stats.AlertsBySeverity[AlertSeverity.Information]);
        Assert.Equal(1, stats.AlertsBySeverity[AlertSeverity.Warning]);
        Assert.Equal(1, stats.AlertsBySeverity[AlertSeverity.Error]);
        Assert.Equal(1, stats.AlertsBySeverity[AlertSeverity.Critical]);
    }

    [Fact]
    public void DifferentAlertKeys_NotThrottled()
    {
        // Arrange
        var severity = AlertSeverity.Warning;
        
        // Record alert with one key
        _throttler.RecordAlert("alert-key-1", severity);
        
        // Act - Different key should not be throttled
        var result = _throttler.ShouldThrottle("alert-key-2", severity);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AlertsLastMinute_TracksCorrectly()
    {
        // Arrange
        var severity = AlertSeverity.Information;
        
        // Send several alerts
        for (int i = 0; i < 5; i++)
        {
            _throttler.RecordAlert($"recent-alert-{i}", severity);
        }
        
        // Act
        var stats = _throttler.GetStatisticsAsync().Result;
        
        // Assert
        Assert.Equal(5, stats.AlertsLastMinute);
    }

    [Theory]
    [InlineData(AlertSeverity.Information)]
    [InlineData(AlertSeverity.Warning)]
    [InlineData(AlertSeverity.Error)]
    public void NonCriticalAlerts_RespectRateLimits(AlertSeverity severity)
    {
        // Arrange
        // Send 10 alerts (the limit)
        for (int i = 0; i < 10; i++)
        {
            _throttler.RecordAlert($"rate-test-{i}", severity);
        }
        
        // Act - Next alert should be throttled
        var result = _throttler.ShouldThrottle("rate-test-11", severity);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ConcurrentAlerts_HandledSafely()
    {
        // Arrange
        var tasks = new List<Task>();
        var alertCount = 100;
        var throttledCount = 0;
        
        // Act - Send many alerts concurrently
        for (int i = 0; i < alertCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var alertKey = $"concurrent-{index % 20}"; // Some duplicates
                var severity = (AlertSeverity)(index % 4);
                
                if (_throttler.ShouldThrottle(alertKey, severity))
                {
                    Interlocked.Increment(ref throttledCount);
                }
                else
                {
                    _throttler.RecordAlert(alertKey, severity);
                }
            }));
        }
        
        Task.WaitAll(tasks.ToArray());
        
        // Assert - Should handle concurrent access without errors
        var stats = _throttler.GetStatisticsAsync().Result;
        Assert.True(stats.TotalAlertsSent > 0);
        Assert.True(stats.TotalAlertsSent + throttledCount <= alertCount);
    }

    public void Dispose()
    {
        _throttler?.Dispose();
        GC.SuppressFinalize(this);
    }
}