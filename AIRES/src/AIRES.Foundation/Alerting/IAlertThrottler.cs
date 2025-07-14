namespace AIRES.Foundation.Alerting;

/// <summary>
/// Manages alert throttling to prevent flooding.
/// </summary>
public interface IAlertThrottler
{
    /// <summary>
    /// Checks if an alert should be throttled.
    /// </summary>
    bool ShouldThrottle(string alertKey, AlertSeverity severity);
    
    /// <summary>
    /// Records that an alert was sent.
    /// </summary>
    void RecordAlert(string alertKey, AlertSeverity severity);
    
    /// <summary>
    /// Gets throttling statistics.
    /// </summary>
    Task<ThrottleStatistics> GetStatisticsAsync();
}

/// <summary>
/// Statistics about alert throttling.
/// </summary>
public record ThrottleStatistics
{
    /// <summary>
    /// Total alerts sent.
    /// </summary>
    public long TotalAlertsSent { get; init; }
    
    /// <summary>
    /// Total alerts throttled.
    /// </summary>
    public long TotalAlertsThrottled { get; init; }
    
    /// <summary>
    /// Alerts sent in the last minute.
    /// </summary>
    public int AlertsLastMinute { get; init; }
    
    /// <summary>
    /// Alert counts by severity.
    /// </summary>
    public Dictionary<AlertSeverity, long> AlertsBySeverity { get; init; } = new();
}