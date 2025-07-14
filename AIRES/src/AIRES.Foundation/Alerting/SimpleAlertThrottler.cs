using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;

namespace AIRES.Foundation.Alerting;

/// <summary>
/// Simple in-memory alert throttler implementation.
/// Prevents alert flooding as per AIRES requirements.
/// </summary>
public class SimpleAlertThrottler : IAlertThrottler, IDisposable
{
    private readonly ConcurrentDictionary<string, AlertThrottleInfo> _alertHistory = new();
    private readonly ConcurrentDictionary<AlertSeverity, long> _severityCounts = new();
    private readonly int _sameAlertIntervalSeconds;
    private readonly int _maxAlertsPerMinute;
    private readonly SemaphoreSlim _cleanupSemaphore = new(1, 1);
    private DateTime _lastCleanup = DateTime.UtcNow;
    
    private long _totalAlertsSent;
    private long _totalAlertsThrottled;
    
    public SimpleAlertThrottler(IConfiguration configuration)
    {
        var throttleConfig = configuration.GetSection("Alerting:Throttling");
        _sameAlertIntervalSeconds = int.Parse(throttleConfig["SameAlertIntervalSeconds"] ?? "60");
        _maxAlertsPerMinute = int.Parse(throttleConfig["MaxAlertsPerMinute"] ?? "10");
    }
    
    public bool ShouldThrottle(string alertKey, AlertSeverity severity)
    {
        // Critical alerts are never throttled
        if (severity == AlertSeverity.Critical)
        {
            return false;
        }
        
        var now = DateTime.UtcNow;
        
        // Check same alert throttling
        if (_alertHistory.TryGetValue(alertKey, out var info))
        {
            var timeSinceLastAlert = now - info.LastOccurrence;
            if (timeSinceLastAlert.TotalSeconds < _sameAlertIntervalSeconds)
            {
                Interlocked.Increment(ref _totalAlertsThrottled);
                return true;
            }
        }
        
        // Check rate limiting
        var recentAlerts = GetAlertsInLastMinute();
        if (recentAlerts >= _maxAlertsPerMinute)
        {
            Interlocked.Increment(ref _totalAlertsThrottled);
            return true;
        }
        
        return false;
    }
    
    public void RecordAlert(string alertKey, AlertSeverity severity)
    {
        var now = DateTime.UtcNow;
        
        _alertHistory.AddOrUpdate(alertKey, 
            new AlertThrottleInfo { LastOccurrence = now, Count = 1 },
            (key, existing) => 
            {
                existing.LastOccurrence = now;
                existing.Count++;
                return existing;
            });
            
        _severityCounts.AddOrUpdate(severity, 1, (_, count) => count + 1);
        Interlocked.Increment(ref _totalAlertsSent);
        
        // Periodic cleanup
        if (now - _lastCleanup > TimeSpan.FromMinutes(5))
        {
            _ = Task.Run(() => CleanupOldEntries());
        }
    }
    
    public Task<ThrottleStatistics> GetStatisticsAsync()
    {
        var stats = new ThrottleStatistics
        {
            TotalAlertsSent = _totalAlertsSent,
            TotalAlertsThrottled = _totalAlertsThrottled,
            AlertsLastMinute = GetAlertsInLastMinute(),
            AlertsBySeverity = _severityCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
        
        return Task.FromResult(stats);
    }
    
    private int GetAlertsInLastMinute()
    {
        var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
        return _alertHistory.Values.Count(info => info.LastOccurrence >= oneMinuteAgo);
    }
    
    private async Task CleanupOldEntries()
    {
        await _cleanupSemaphore.WaitAsync();
        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-10);
            var keysToRemove = _alertHistory
                .Where(kvp => kvp.Value.LastOccurrence < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();
                
            foreach (var key in keysToRemove)
            {
                _alertHistory.TryRemove(key, out _);
            }
            
            _lastCleanup = DateTime.UtcNow;
        }
        finally
        {
            _cleanupSemaphore.Release();
        }
    }
    
    public void Dispose()
    {
        _cleanupSemaphore?.Dispose();
        GC.SuppressFinalize(this);
    }
    
    private class AlertThrottleInfo
    {
        public DateTime LastOccurrence { get; set; }
        public int Count { get; set; }
    }
}