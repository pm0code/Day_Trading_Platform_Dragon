using System.Collections.Concurrent;

namespace AIRES.Foundation.Alerting;

/// <summary>
/// In-memory alert persistence for development and testing.
/// TODO: Replace with LiteDB implementation for production.
/// </summary>
public class InMemoryAlertPersistence : IAlertPersistence
{
    private readonly ConcurrentDictionary<Guid, AlertRecord> _alerts = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    public Task<AlertRecord> SaveAlertAsync(AlertMessage alert)
    {
        var record = new AlertRecord
        {
            Id = alert.Id,
            Timestamp = alert.Timestamp,
            Severity = alert.Severity,
            Component = alert.Component,
            Message = alert.Message,
            Details = alert.Details?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Acknowledged = false,
            AcknowledgedAt = null,
            AcknowledgedBy = null
        };
        
        _alerts.TryAdd(record.Id, record);
        return Task.FromResult(record);
    }
    
    public async Task<IEnumerable<AlertRecord>> GetAlertsAsync(AlertQuery query)
    {
        await _semaphore.WaitAsync();
        try
        {
            var alerts = _alerts.Values.AsEnumerable();
            
            if (query.FromDate.HasValue)
                alerts = alerts.Where(a => a.Timestamp >= query.FromDate.Value);
                
            if (query.ToDate.HasValue)
                alerts = alerts.Where(a => a.Timestamp <= query.ToDate.Value);
                
            if (query.MinimumSeverity.HasValue)
                alerts = alerts.Where(a => a.Severity >= query.MinimumSeverity.Value);
                
            if (!string.IsNullOrEmpty(query.Component))
                alerts = alerts.Where(a => a.Component.Contains(query.Component, StringComparison.OrdinalIgnoreCase));
                
            if (query.IncludeAcknowledged.HasValue && !query.IncludeAcknowledged.Value)
                alerts = alerts.Where(a => !a.Acknowledged);
                
            if (query.Limit.HasValue)
                alerts = alerts.Take(query.Limit.Value);
                
            return alerts.OrderByDescending(a => a.Timestamp).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    public Task<bool> AcknowledgeAlertAsync(Guid alertId, string acknowledgedBy)
    {
        if (_alerts.TryGetValue(alertId, out var record))
        {
            var updatedRecord = record with 
            { 
                Acknowledged = true,
                AcknowledgedAt = DateTime.UtcNow,
                AcknowledgedBy = acknowledgedBy
            };
            
            _alerts.TryUpdate(alertId, updatedRecord, record);
            return Task.FromResult(true);
        }
        
        return Task.FromResult(false);
    }
    
    public async Task<AlertStatistics> GetStatisticsAsync(DateTime from, DateTime to)
    {
        await _semaphore.WaitAsync();
        try
        {
            var relevantAlerts = _alerts.Values
                .Where(a => a.Timestamp >= from && a.Timestamp <= to)
                .ToList();
                
            var alertsByComponent = relevantAlerts
                .GroupBy(a => a.Component)
                .ToDictionary(g => g.Key, g => (long)g.Count());
                
            return new AlertStatistics
            {
                TotalAlerts = relevantAlerts.Count,
                CriticalAlerts = relevantAlerts.Count(a => a.Severity == AlertSeverity.Critical),
                ErrorAlerts = relevantAlerts.Count(a => a.Severity == AlertSeverity.Error),
                WarningAlerts = relevantAlerts.Count(a => a.Severity == AlertSeverity.Warning),
                InformationAlerts = relevantAlerts.Count(a => a.Severity == AlertSeverity.Information),
                AlertsByComponent = alertsByComponent,
                PeriodStart = from,
                PeriodEnd = to
            };
        }
        finally
        {
            _semaphore.Release();
        }
    }
}