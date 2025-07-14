using Xunit;
using AIRES.Foundation.Alerting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace AIRES.Foundation.Tests.Alerting;

/// <summary>
/// Test suite for InMemoryAlertPersistence.
/// Tests alert storage, retrieval, acknowledgment, and statistics.
/// </summary>
public class InMemoryAlertPersistenceTests : IDisposable
{
    private readonly InMemoryAlertPersistence _persistence;

    public InMemoryAlertPersistenceTests()
    {
        _persistence = new InMemoryAlertPersistence();
    }

    [Fact]
    public async Task SaveAlertAsync_StoresAlert()
    {
        // Arrange
        var alert = new AlertMessage
        {
            Id = Guid.NewGuid(),
            Severity = AlertSeverity.Warning,
            Component = "TestComponent",
            Message = "Test warning",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var record = await _persistence.SaveAlertAsync(alert);

        // Assert
        Assert.NotNull(record);
        Assert.Equal(alert.Id, record.Id);
        Assert.Equal(alert.Severity, record.Severity);
        Assert.Equal(alert.Component, record.Component);
        Assert.Equal(alert.Message, record.Message);
        Assert.False(record.Acknowledged);
    }

    [Fact]
    public async Task GetAlertsAsync_ReturnsAllAlerts()
    {
        // Arrange
        var alerts = new List<AlertMessage>();
        for (int i = 0; i < 5; i++)
        {
            var alert = new AlertMessage
            {
                Id = Guid.NewGuid(),
                Severity = AlertSeverity.Information,
                Component = $"Component{i}",
                Message = $"Message {i}",
                Timestamp = DateTime.UtcNow
            };
            alerts.Add(alert);
            await _persistence.SaveAlertAsync(alert);
        }

        // Act
        var query = new AlertQuery();
        var results = await _persistence.GetAlertsAsync(query);

        // Assert
        Assert.Equal(5, results.Count());
    }

    [Fact]
    public async Task GetAlertsAsync_WithDateFilter_ReturnsFilteredAlerts()
    {
        // Arrange
        var now = DateTime.UtcNow;
        
        // Save alerts at different times
        await _persistence.SaveAlertAsync(new AlertMessage
        {
            Id = Guid.NewGuid(),
            Severity = AlertSeverity.Warning,
            Component = "Old",
            Message = "Old alert",
            Timestamp = now.AddHours(-2)
        });

        await _persistence.SaveAlertAsync(new AlertMessage
        {
            Id = Guid.NewGuid(),
            Severity = AlertSeverity.Warning,
            Component = "Recent",
            Message = "Recent alert",
            Timestamp = now.AddMinutes(-30)
        });

        // Act
        var query = new AlertQuery
        {
            FromDate = now.AddHours(-1)
        };
        var results = await _persistence.GetAlertsAsync(query);

        // Assert
        Assert.Single(results);
        Assert.Equal("Recent", results.First().Component);
    }

    [Fact]
    public async Task GetAlertsAsync_WithSeverityFilter_ReturnsFilteredAlerts()
    {
        // Arrange
        await _persistence.SaveAlertAsync(new AlertMessage
        {
            Severity = AlertSeverity.Information,
            Component = "Test",
            Message = "Info"
        });

        await _persistence.SaveAlertAsync(new AlertMessage
        {
            Severity = AlertSeverity.Warning,
            Component = "Test",
            Message = "Warning"
        });

        await _persistence.SaveAlertAsync(new AlertMessage
        {
            Severity = AlertSeverity.Error,
            Component = "Test",
            Message = "Error"
        });

        // Act
        var query = new AlertQuery
        {
            MinimumSeverity = AlertSeverity.Warning
        };
        var results = await _persistence.GetAlertsAsync(query);

        // Assert
        Assert.Equal(2, results.Count());
        Assert.All(results, r => Assert.True(r.Severity >= AlertSeverity.Warning));
    }

    [Fact]
    public async Task GetAlertsAsync_WithComponentFilter_ReturnsFilteredAlerts()
    {
        // Arrange
        await _persistence.SaveAlertAsync(new AlertMessage
        {
            Component = "ServiceA",
            Message = "Alert from A"
        });

        await _persistence.SaveAlertAsync(new AlertMessage
        {
            Component = "ServiceB",
            Message = "Alert from B"
        });

        await _persistence.SaveAlertAsync(new AlertMessage
        {
            Component = "ServiceAB",
            Message = "Alert from AB"
        });

        // Act
        var query = new AlertQuery
        {
            Component = "ServiceA"
        };
        var results = await _persistence.GetAlertsAsync(query);

        // Assert
        Assert.Equal(2, results.Count()); // ServiceA and ServiceAB
        Assert.All(results, r => Assert.Contains("ServiceA", r.Component));
    }

    [Fact]
    public async Task GetAlertsAsync_WithLimit_ReturnsLimitedResults()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _persistence.SaveAlertAsync(new AlertMessage
            {
                Component = "Test",
                Message = $"Alert {i}",
                Timestamp = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        // Act
        var query = new AlertQuery { Limit = 3 };
        var results = await _persistence.GetAlertsAsync(query);

        // Assert
        Assert.Equal(3, results.Count());
    }

    [Fact]
    public async Task GetAlertsAsync_OrdersByTimestampDescending()
    {
        // Arrange
        var timestamps = new List<DateTime>();
        for (int i = 0; i < 5; i++)
        {
            var timestamp = DateTime.UtcNow.AddMinutes(-i * 10);
            timestamps.Add(timestamp);
            
            await _persistence.SaveAlertAsync(new AlertMessage
            {
                Component = "Test",
                Message = $"Alert {i}",
                Timestamp = timestamp
            });
        }

        // Act
        var query = new AlertQuery();
        var results = (await _persistence.GetAlertsAsync(query)).ToList();

        // Assert
        for (int i = 0; i < results.Count - 1; i++)
        {
            Assert.True(results[i].Timestamp >= results[i + 1].Timestamp);
        }
    }

    [Fact]
    public async Task AcknowledgeAlertAsync_UpdatesAlert()
    {
        // Arrange
        var alert = new AlertMessage
        {
            Id = Guid.NewGuid(),
            Component = "Test",
            Message = "Test alert"
        };
        await _persistence.SaveAlertAsync(alert);

        // Act
        var result = await _persistence.AcknowledgeAlertAsync(alert.Id, "TestUser");

        // Assert
        Assert.True(result);
        
        var query = new AlertQuery();
        var alerts = await _persistence.GetAlertsAsync(query);
        var acknowledgedAlert = alerts.First(a => a.Id == alert.Id);
        
        Assert.True(acknowledgedAlert.Acknowledged);
        Assert.NotNull(acknowledgedAlert.AcknowledgedAt);
        Assert.Equal("TestUser", acknowledgedAlert.AcknowledgedBy);
    }

    [Fact]
    public async Task AcknowledgeAlertAsync_WithNonExistentId_ReturnsFalse()
    {
        // Act
        var result = await _persistence.AcknowledgeAlertAsync(Guid.NewGuid(), "TestUser");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetAlertsAsync_ExcludeAcknowledged_FiltersOut()
    {
        // Arrange
        var alert1 = await _persistence.SaveAlertAsync(new AlertMessage
        {
            Component = "Test",
            Message = "Unacknowledged"
        });

        var alert2 = await _persistence.SaveAlertAsync(new AlertMessage
        {
            Component = "Test",
            Message = "To be acknowledged"
        });

        // Acknowledge one alert
        await _persistence.AcknowledgeAlertAsync(alert2.Id, "TestUser");

        // Act
        var query = new AlertQuery { IncludeAcknowledged = false };
        var results = await _persistence.GetAlertsAsync(query);

        // Assert
        Assert.Single(results);
        Assert.Equal("Unacknowledged", results.First().Message);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsCorrectCounts()
    {
        // Arrange
        var now = DateTime.UtcNow;
        
        // Add various alerts
        await _persistence.SaveAlertAsync(new AlertMessage
        {
            Severity = AlertSeverity.Critical,
            Component = "ServiceA",
            Timestamp = now.AddMinutes(-30)
        });

        await _persistence.SaveAlertAsync(new AlertMessage
        {
            Severity = AlertSeverity.Error,
            Component = "ServiceA",
            Timestamp = now.AddMinutes(-20)
        });

        await _persistence.SaveAlertAsync(new AlertMessage
        {
            Severity = AlertSeverity.Error,
            Component = "ServiceB",
            Timestamp = now.AddMinutes(-15)
        });

        await _persistence.SaveAlertAsync(new AlertMessage
        {
            Severity = AlertSeverity.Warning,
            Component = "ServiceA",
            Timestamp = now.AddMinutes(-10)
        });

        await _persistence.SaveAlertAsync(new AlertMessage
        {
            Severity = AlertSeverity.Information,
            Component = "ServiceB",
            Timestamp = now.AddMinutes(-5)
        });

        // Add one outside the time range
        await _persistence.SaveAlertAsync(new AlertMessage
        {
            Severity = AlertSeverity.Error,
            Component = "ServiceC",
            Timestamp = now.AddHours(-2)
        });

        // Act
        var stats = await _persistence.GetStatisticsAsync(now.AddHours(-1), now);

        // Assert
        Assert.Equal(5, stats.TotalAlerts);
        Assert.Equal(1, stats.CriticalAlerts);
        Assert.Equal(2, stats.ErrorAlerts);
        Assert.Equal(1, stats.WarningAlerts);
        Assert.Equal(1, stats.InformationAlerts);
        
        Assert.Equal(3, stats.AlertsByComponent["ServiceA"]);
        Assert.Equal(2, stats.AlertsByComponent["ServiceB"]);
        Assert.False(stats.AlertsByComponent.ContainsKey("ServiceC"));
    }

    [Fact]
    public async Task GetStatisticsAsync_WithNoAlerts_ReturnsZeroCounts()
    {
        // Act
        var now = DateTime.UtcNow;
        var stats = await _persistence.GetStatisticsAsync(now.AddHours(-1), now);

        // Assert
        Assert.Equal(0, stats.TotalAlerts);
        Assert.Equal(0, stats.CriticalAlerts);
        Assert.Equal(0, stats.ErrorAlerts);
        Assert.Equal(0, stats.WarningAlerts);
        Assert.Equal(0, stats.InformationAlerts);
        Assert.Empty(stats.AlertsByComponent);
    }

    [Fact]
    public async Task SaveAlertAsync_WithDetailsAndNulls_HandlesCorrectly()
    {
        // Arrange
        var details = new Dictionary<string, object>
        {
            ["StringValue"] = "test",
            ["IntValue"] = 42,
            ["NullValue"] = null!
        };

        var alert = new AlertMessage
        {
            Id = Guid.NewGuid(),
            Severity = AlertSeverity.Warning,
            Component = "Test",
            Message = "Test with details",
            Details = details.ToImmutableDictionary()
        };

        // Act
        var record = await _persistence.SaveAlertAsync(alert);

        // Assert
        Assert.NotNull(record.Details);
        Assert.Equal("test", record.Details["StringValue"]);
        Assert.Equal(42, record.Details["IntValue"]);
        Assert.Null(record.Details["NullValue"]);
    }

    [Fact]
    public async Task ConcurrentOperations_HandledSafely()
    {
        // Arrange
        var tasks = new List<Task>();
        var alertCount = 100;

        // Act - Perform many operations concurrently
        for (int i = 0; i < alertCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var alert = new AlertMessage
                {
                    Id = Guid.NewGuid(),
                    Severity = (AlertSeverity)(index % 4),
                    Component = $"Component{index % 5}",
                    Message = $"Alert {index}",
                    Timestamp = DateTime.UtcNow
                };

                await _persistence.SaveAlertAsync(alert);

                // Some queries
                if (index % 10 == 0)
                {
                    await _persistence.GetAlertsAsync(new AlertQuery { Limit = 5 });
                }

                // Some acknowledgments
                if (index % 20 == 0)
                {
                    await _persistence.AcknowledgeAlertAsync(alert.Id, "ConcurrentUser");
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Verify all alerts were saved
        var allAlerts = await _persistence.GetAlertsAsync(new AlertQuery());
        Assert.Equal(alertCount, allAlerts.Count());
    }

    public void Dispose()
    {
        _persistence?.Dispose();
        GC.SuppressFinalize(this);
    }
}