using System.Diagnostics;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Gateway.Services;

/// <summary>
/// Comprehensive health monitoring for on-premise trading workstation
/// Provides real-time system health, performance monitoring, and trading-specific alerts
/// </summary>
public class HealthMonitor : IHealthMonitor
{
    private readonly IMessageBus _messageBus;
    private readonly IProcessManager _processManager;
    private readonly ITradingLogger _logger;
    private readonly Dictionary<string, Func<Task<HealthCheckResult>>> _healthChecks;
    private readonly List<SystemAlert> _activeAlerts;
    private readonly object _alertLock = new();
    private readonly Timer _monitoringTimer;

    public HealthMonitor(IMessageBus messageBus, IProcessManager processManager, ITradingLogger logger)
    {
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _healthChecks = new Dictionary<string, Func<Task<HealthCheckResult>>>();
        _activeAlerts = new List<SystemAlert>();

        RegisterDefaultHealthChecks();

        // Start monitoring timer (every 10 seconds)
        _monitoringTimer = new Timer(async _ => await PerformHealthChecksAsync(), null,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    public async Task<HealthStatus> GetHealthStatusAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var serviceHealth = new Dictionary<string, bool>();
        var overallHealthy = true;

        try
        {
            // Check Redis messaging health
            var redisHealthy = await _messageBus.IsHealthyAsync();
            serviceHealth["Redis"] = redisHealthy;
            overallHealthy &= redisHealthy;

            // Check process health
            var processes = await _processManager.GetProcessStatusAsync();
            foreach (var process in processes)
            {
                var isHealthy = process.Status == ProcessStatus.Running;
                serviceHealth[process.ServiceName] = isHealthy;
                overallHealthy &= isHealthy;
            }

            // Run custom health checks
            foreach (var (name, healthCheck) in _healthChecks)
            {
                try
                {
                    var result = await healthCheck();
                    serviceHealth[name] = result.IsHealthy;
                    overallHealthy &= result.IsHealthy;
                }
                catch (Exception ex)
                {
                    TradingLogOrchestrator.Instance.LogError($"Health check failed for {name}", ex);
                    serviceHealth[name] = false;
                    overallHealthy = false;
                }
            }

            stopwatch.Stop();

            var status = overallHealthy ? "Healthy" : "Unhealthy";
            var alerts = await GetActiveAlertsAsync();

            return new HealthStatus(overallHealthy, status, stopwatch.Elapsed, serviceHealth, alerts, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error performing health check", ex);
            stopwatch.Stop();

            return new HealthStatus(false, "Error", stopwatch.Elapsed, serviceHealth,
                new[] { CreateAlert(AlertSeverity.Critical, "Health Check Failed", ex.Message) },
                DateTimeOffset.UtcNow);
        }
    }

    public async Task<ServiceHealthInfo[]> GetDetailedHealthAsync()
    {
        var healthInfos = new List<ServiceHealthInfo>();

        try
        {
            // Get process information
            var processes = await _processManager.GetProcessStatusAsync();
            foreach (var process in processes)
            {
                var issues = new List<string>();

                // Check for potential issues
                if (process.CpuUsagePercent > 80)
                    issues.Add($"High CPU usage: {process.CpuUsagePercent:F1}%");

                if (process.MemoryUsageMB > 2048) // 2GB threshold
                    issues.Add($"High memory usage: {process.MemoryUsageMB}MB");

                if (process.Status != ProcessStatus.Running)
                    issues.Add($"Process not running: {process.Status}");

                healthInfos.Add(new ServiceHealthInfo(
                    process.ServiceName,
                    process.Status == ProcessStatus.Running && issues.Count == 0,
                    process.Status.ToString(),
                    TimeSpan.FromMilliseconds(Random.Shared.Next(1, 10)), // Mock response time
                    process.CpuUsagePercent,
                    process.MemoryUsageMB,
                    Random.Shared.Next(5, 25), // Mock active connections
                    DateTimeOffset.UtcNow,
                    issues.ToArray()));
            }

            // Add Redis health info
            var redisLatency = await _messageBus.GetLatencyAsync();
            var redisHealthy = redisLatency.TotalMilliseconds < 100; // Consider healthy if <100ms

            healthInfos.Add(new ServiceHealthInfo(
                "Redis",
                redisHealthy,
                redisHealthy ? "Connected" : "Slow Response",
                redisLatency,
                Random.Shared.Next(5, 15), // Mock CPU
                Random.Shared.Next(100, 300), // Mock memory
                Random.Shared.Next(10, 50), // Mock connections
                DateTimeOffset.UtcNow,
                redisHealthy ? Array.Empty<string>() : new[] { $"High latency: {redisLatency.TotalMilliseconds:F1}ms" }));
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error getting detailed health information", ex);
        }

        return healthInfos.ToArray();
    }

    public async Task<bool> AreCriticalSystemsHealthyAsync()
    {
        try
        {
            // Define critical systems for trading
            var criticalSystems = new[] { "Redis", "RiskManagement", "PaperTrading" };

            var healthStatus = await GetHealthStatusAsync();

            foreach (var system in criticalSystems)
            {
                if (!healthStatus.ServiceHealth.GetValueOrDefault(system, false))
                {
                    TradingLogOrchestrator.Instance.LogWarning($"Critical system {system} is not healthy");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error checking critical systems health", ex);
            return false;
        }
    }

    public async Task<TradingHealthMetrics> GetTradingHealthAsync()
    {
        try
        {
            // TODO: Implement actual trading health metrics collection
            // For MVP, return mock metrics with realistic values

            var marketOpen = IsMarketOpen();
            var sessionStart = GetMarketSessionStart();

            return new TradingHealthMetrics(
                true, // Market data connected
                TimeSpan.FromMicroseconds(85), // Average order latency
                TimeSpan.FromMilliseconds(5), // Average market data latency
                true, // Risk system operational
                2, // Active strategies
                125.75m, // Daily PnL
                true, // PDT compliant
                0, // Failed orders
                sessionStart,
                marketOpen);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error getting trading health metrics", ex);
            throw;
        }
    }

    public void RegisterHealthCheck(string name, Func<Task<HealthCheckResult>> healthCheck)
    {
        _healthChecks[name] = healthCheck ?? throw new ArgumentNullException(nameof(healthCheck));
        TradingLogOrchestrator.Instance.LogInfo($"Registered health check: {name}");
    }

    public async Task StartMonitoringAsync(CancellationToken cancellationToken)
    {
        TradingLogOrchestrator.Instance.LogInfo("Starting continuous health monitoring");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await PerformHealthChecksAsync();
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken); // Check every 30 seconds
            }
        }
        catch (OperationCanceledException)
        {
            TradingLogOrchestrator.Instance.LogInfo("Health monitoring stopped");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error in health monitoring loop", ex);
        }
    }

    public async Task<SystemAlert[]> GetActiveAlertsAsync()
    {
        lock (_alertLock)
        {
            // Return copy of active alerts
            return _activeAlerts.ToArray();
        }
    }

    // Private helper methods
    private void RegisterDefaultHealthChecks()
    {
        // System resource health check
        RegisterHealthCheck("SystemResources", async () =>
        {
            var process = Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / (1024 * 1024);
            var isHealthy = memoryMB < 1024; // Less than 1GB is healthy

            return new HealthCheckResult(
                isHealthy,
                isHealthy ? "Normal" : "High Memory Usage",
                $"Gateway memory usage: {memoryMB}MB",
                TimeSpan.FromMilliseconds(1),
                new Dictionary<string, object> { ["MemoryMB"] = memoryMB });
        });

        // Disk space health check
        RegisterHealthCheck("DiskSpace", async () =>
        {
            var drive = new DriveInfo("C:");
            var freeSpaceGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
            var isHealthy = freeSpaceGB > 10; // More than 10GB free is healthy

            return new HealthCheckResult(
                isHealthy,
                isHealthy ? "Adequate" : "Low Disk Space",
                $"Available disk space: {freeSpaceGB}GB",
                TimeSpan.FromMilliseconds(5),
                new Dictionary<string, object> { ["FreeSpaceGB"] = freeSpaceGB });
        });

        // Trading session health check
        RegisterHealthCheck("TradingSession", async () =>
        {
            var isMarketOpen = IsMarketOpen();
            var sessionStatus = isMarketOpen ? "Market Open" : "Market Closed";

            return new HealthCheckResult(
                true, // Always healthy, just informational
                sessionStatus,
                $"Trading session status: {sessionStatus}",
                TimeSpan.FromMilliseconds(1),
                new Dictionary<string, object>
                {
                    ["IsMarketOpen"] = isMarketOpen,
                    ["SessionStart"] = GetMarketSessionStart()
                });
        });
    }

    private async Task PerformHealthChecksAsync()
    {
        try
        {
            var healthStatus = await GetHealthStatusAsync();

            // Check for new issues and create alerts
            foreach (var (serviceName, isHealthy) in healthStatus.ServiceHealth)
            {
                if (!isHealthy)
                {
                    var existingAlert = _activeAlerts.FirstOrDefault(a =>
                        a.Title.Contains(serviceName) && !a.IsAcknowledged);

                    if (existingAlert == null)
                    {
                        var alert = CreateAlert(AlertSeverity.Error,
                            $"Service Unhealthy: {serviceName}",
                            $"Service {serviceName} is reporting unhealthy status");

                        AddAlert(alert);
                    }
                }
            }

            // Clean up resolved alerts (auto-acknowledge)
            lock (_alertLock)
            {
                var resolvedAlerts = _activeAlerts.Where(a =>
                    !a.IsAcknowledged &&
                    a.CreatedAt < DateTimeOffset.UtcNow.AddMinutes(-5) && // Older than 5 minutes
                    healthStatus.ServiceHealth.Values.All(h => h)) // All services healthy
                    .ToArray();

                for (int i = 0; i < _activeAlerts.Count; i++)
                {
                    var alert = _activeAlerts[i];
                    if (resolvedAlerts.Contains(alert))
                    {
                        _activeAlerts[i] = alert with { IsAcknowledged = true, AcknowledgedBy = "System", AcknowledgedAt = DateTimeOffset.UtcNow };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error performing periodic health checks", ex);
        }
    }

    private SystemAlert CreateAlert(AlertSeverity severity, string title, string description)
    {
        return new SystemAlert(
            Guid.NewGuid().ToString(),
            severity,
            title,
            description,
            DateTimeOffset.UtcNow,
            false,
            null,
            null);
    }

    private void AddAlert(SystemAlert alert)
    {
        lock (_alertLock)
        {
            _activeAlerts.Add(alert);

            // Keep only last 100 alerts
            if (_activeAlerts.Count > 100)
            {
                _activeAlerts.RemoveAt(0);
            }
        }

        TradingLogOrchestrator.Instance.LogWarning($"New system alert: {alert.Severity} - {alert.Title}: {alert.Description}");
    }

    private bool IsMarketOpen()
    {
        var now = DateTime.Now;
        var est = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var marketTime = TimeZoneInfo.ConvertTime(now, est);

        // Simple market hours check (9:30 AM - 4:00 PM EST, Monday-Friday)
        return marketTime.DayOfWeek >= DayOfWeek.Monday &&
               marketTime.DayOfWeek <= DayOfWeek.Friday &&
               marketTime.TimeOfDay >= TimeSpan.FromHours(9.5) &&
               marketTime.TimeOfDay <= TimeSpan.FromHours(16);
    }

    private DateTimeOffset GetMarketSessionStart()
    {
        var today = DateTime.Today;
        var est = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var marketStart = new DateTime(today.Year, today.Month, today.Day, 9, 30, 0);
        return TimeZoneInfo.ConvertTime(marketStart, est, TimeZoneInfo.Local);
    }
}