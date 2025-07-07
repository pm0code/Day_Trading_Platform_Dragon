using System.Diagnostics;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Services;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.Gateway.Services;

/// <summary>
/// Comprehensive health monitoring for on-premise trading workstation with canonical patterns
/// Provides real-time system health, performance monitoring, and trading-specific alerts
/// All operations use canonical error handling and comprehensive logging
/// </summary>
public class HealthMonitor : CanonicalServiceBase, IHealthMonitor
{
    private readonly IMessageBus _messageBus;
    private readonly IProcessManager _processManager;
    private readonly Dictionary<string, Func<Task<HealthCheckResult>>> _healthChecks;
    private readonly List<SystemAlert> _activeAlerts;
    private readonly object _alertLock = new();
    private readonly Timer _monitoringTimer;
    
    // Performance metrics
    private long _totalHealthChecks = 0;
    private long _failedHealthChecks = 0;
    private long _alertsGenerated = 0;
    private long _alertsResolved = 0;
    private long _criticalAlertsGenerated = 0;

    /// <summary>
    /// Initializes a new instance of HealthMonitor with canonical service patterns
    /// </summary>
    /// <param name="messageBus">Message bus for system communication</param>
    /// <param name="processManager">Process manager for service health monitoring</param>
    /// <param name="logger">Trading logger for comprehensive health monitoring tracking</param>
    public HealthMonitor(IMessageBus messageBus, IProcessManager processManager, ITradingLogger logger) : base(logger, "HealthMonitor")
    {
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _healthChecks = new Dictionary<string, Func<Task<HealthCheckResult>>>();
        _activeAlerts = new List<SystemAlert>();

        RegisterDefaultHealthChecks();

        // Start monitoring timer (every 10 seconds)
        _monitoringTimer = new Timer(async _ => await PerformHealthChecksAsync(), null,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    public async Task<TradingResult<HealthStatus>> GetHealthStatusAsync()
    {
        LogMethodEntry();
        try
        {
            Interlocked.Increment(ref _totalHealthChecks);
            
            var stopwatch = Stopwatch.StartNew();
            var serviceHealth = new Dictionary<string, bool>();
            var overallHealthy = true;

            // Check Redis messaging health
            var redisHealthy = await _messageBus.IsHealthyAsync();
            serviceHealth["Redis"] = redisHealthy;
            overallHealthy &= redisHealthy;

            // Check process health
            var processResult = await _processManager.GetProcessStatusAsync();
            if (processResult.IsSuccess)
            {
                foreach (var process in processResult.Data)
                {
                    var isHealthy = process.Status == ProcessStatus.Running;
                    serviceHealth[process.ServiceName] = isHealthy;
                    overallHealthy &= isHealthy;
                }
            }
            else
            {
                LogError($"Failed to get process status: {processResult.Error}");
                serviceHealth["ProcessManager"] = false;
                overallHealthy = false;
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
                    LogError($"Health check failed for {name}", ex);
                    serviceHealth[name] = false;
                    overallHealthy = false;
                    Interlocked.Increment(ref _failedHealthChecks);
                }
            }

            stopwatch.Stop();

            var status = overallHealthy ? "Healthy" : "Unhealthy";
            var alertsResult = await GetActiveAlertsAsync();
            var alerts = alertsResult.IsSuccess ? alertsResult.Data : Array.Empty<SystemAlert>();

            var healthStatus = new HealthStatus(overallHealthy, status, stopwatch.Elapsed, serviceHealth, alerts, DateTimeOffset.UtcNow);
            
            LogInfo($"Health check completed - Status: {status}, Duration: {stopwatch.ElapsedMilliseconds}ms");
            LogMethodExit();
            return TradingResult<HealthStatus>.Success(healthStatus);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedHealthChecks);
            LogError("Error performing health check", ex);
            LogMethodExit();
            return TradingResult<HealthStatus>.Failure("HEALTH_CHECK_ERROR", $"Failed to perform health check: {ex.Message}");
        }
    }

    public async Task<TradingResult<ServiceHealthInfo[]>> GetDetailedHealthAsync()
    {
        LogMethodEntry();
        try
        {
            var healthInfos = new List<ServiceHealthInfo>();

            // Get process information
            var processResult = await _processManager.GetProcessStatusAsync();
            if (processResult.IsSuccess)
            {
                foreach (var process in processResult.Data)
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
            }
            else
            {
                LogError($"Failed to get process information: {processResult.Error}");
            }

            // Add Redis health info
            try
            {
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
                LogError("Failed to get Redis health information", ex);
            }

            LogInfo($"Retrieved detailed health information for {healthInfos.Count} services");
            LogMethodExit();
            return TradingResult<ServiceHealthInfo[]>.Success(healthInfos.ToArray());
        }
        catch (Exception ex)
        {
            LogError("Error getting detailed health information", ex);
            LogMethodExit();
            return TradingResult<ServiceHealthInfo[]>.Failure("DETAILED_HEALTH_ERROR", $"Failed to get detailed health information: {ex.Message}");
        }
    }

    public async Task<TradingResult<bool>> AreCriticalSystemsHealthyAsync()
    {
        LogMethodEntry();
        try
        {
            // Define critical systems for trading
            var criticalSystems = new[] { "Redis", "RiskManagement", "PaperTrading" };

            var healthResult = await GetHealthStatusAsync();
            if (!healthResult.IsSuccess)
            {
                LogError($"Failed to get health status: {healthResult.Error}");
                LogMethodExit();
                return TradingResult<bool>.Failure("HEALTH_STATUS_ERROR", $"Failed to get health status: {healthResult.Error}");
            }

            var healthStatus = healthResult.Data;
            foreach (var system in criticalSystems)
            {
                if (!healthStatus.ServiceHealth.GetValueOrDefault(system, false))
                {
                    LogWarning($"Critical system {system} is not healthy");
                    LogMethodExit();
                    return TradingResult<bool>.Success(false);
                }
            }

            LogInfo("All critical systems are healthy");
            LogMethodExit();
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Error checking critical systems health", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("CRITICAL_SYSTEMS_CHECK_ERROR", $"Error checking critical systems health: {ex.Message}");
        }
    }

    public async Task<TradingResult<TradingHealthMetrics>> GetTradingHealthAsync()
    {
        LogMethodEntry();
        try
        {
            // TODO: Implement actual trading health metrics collection
            // For MVP, return mock metrics with realistic values

            var marketOpen = IsMarketOpen();
            var sessionStart = GetMarketSessionStart();

            var metrics = new TradingHealthMetrics(
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
                
            LogInfo($"Trading health metrics collected - Market Open: {marketOpen}, Daily PnL: {metrics.DailyPnL}");
            LogMethodExit();
            return TradingResult<TradingHealthMetrics>.Success(metrics);
        }
        catch (Exception ex)
        {
            LogError("Error getting trading health metrics", ex);
            LogMethodExit();
            return TradingResult<TradingHealthMetrics>.Failure("TRADING_HEALTH_ERROR", $"Error getting trading health metrics: {ex.Message}");
        }
    }

    public void RegisterHealthCheck(string name, Func<Task<HealthCheckResult>> healthCheck)
    {
        LogMethodEntry(new { name });
        _healthChecks[name] = healthCheck ?? throw new ArgumentNullException(nameof(healthCheck));
        LogInfo($"Registered health check: {name}");
        LogMethodExit();
    }

    public async Task<TradingResult<bool>> StartMonitoringAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        LogInfo("Starting continuous health monitoring");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await PerformHealthChecksAsync();
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken); // Check every 30 seconds
            }
            
            LogInfo("Health monitoring stopped");
            LogMethodExit();
            return TradingResult<bool>.Success(true);
        }
        catch (OperationCanceledException)
        {
            LogInfo("Health monitoring stopped due to cancellation");
            LogMethodExit();
            return TradingResult<bool>.Success(false);
        }
        catch (Exception ex)
        {
            LogError("Error in health monitoring loop", ex);
            LogMethodExit();
            return TradingResult<bool>.Failure("MONITORING_ERROR", $"Error in health monitoring loop: {ex.Message}");
        }
    }

    public async Task<TradingResult<SystemAlert[]>> GetActiveAlertsAsync()
    {
        LogMethodEntry();
        try
        {
            SystemAlert[] alerts;
            lock (_alertLock)
            {
                // Return copy of active alerts
                alerts = _activeAlerts.ToArray();
            }
            
            LogInfo($"Retrieved {alerts.Length} active alerts");
            LogMethodExit();
            return TradingResult<SystemAlert[]>.Success(alerts);
        }
        catch (Exception ex)
        {
            LogError("Error getting active alerts", ex);
            LogMethodExit();
            return TradingResult<SystemAlert[]>.Failure("ALERTS_ERROR", $"Error getting active alerts: {ex.Message}");
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
            var healthResult = await GetHealthStatusAsync();
            if (!healthResult.IsSuccess)
            {
                LogError($"Failed to get health status during periodic check: {healthResult.Error}");
                return;
            }
            
            var healthStatus = healthResult.Data;

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
                        Interlocked.Increment(ref _alertsResolved);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogError("Error performing periodic health checks", ex);
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

        Interlocked.Increment(ref _alertsGenerated);
        if (alert.Severity == AlertSeverity.Critical)
        {
            Interlocked.Increment(ref _criticalAlertsGenerated);
        }
        LogWarning($"New system alert: {alert.Severity} - {alert.Title}: {alert.Description}");
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