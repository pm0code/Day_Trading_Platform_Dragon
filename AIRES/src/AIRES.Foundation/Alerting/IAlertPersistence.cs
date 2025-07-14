namespace AIRES.Foundation.Alerting;

/// <summary>
/// Manages persistence of alerts for audit and analysis.
/// </summary>
public interface IAlertPersistence
{
    /// <summary>
    /// Saves an alert to persistent storage.
    /// </summary>
    Task<AlertRecord> SaveAlertAsync(AlertMessage alert);
    
    /// <summary>
    /// Retrieves alerts based on query criteria.
    /// </summary>
    Task<IEnumerable<AlertRecord>> GetAlertsAsync(AlertQuery query);
    
    /// <summary>
    /// Acknowledges an alert.
    /// </summary>
    Task<bool> AcknowledgeAlertAsync(Guid alertId, string acknowledgedBy);
    
    /// <summary>
    /// Gets alert statistics for a time period.
    /// </summary>
    Task<AlertStatistics> GetStatisticsAsync(DateTime from, DateTime to);
}

/// <summary>
/// Persisted alert record.
/// </summary>
public record AlertRecord
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public AlertSeverity Severity { get; init; }
    public string Component { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Dictionary<string, object>? Details { get; init; }
    public bool Acknowledged { get; init; }
    public DateTime? AcknowledgedAt { get; init; }
    public string? AcknowledgedBy { get; init; }
}

/// <summary>
/// Query criteria for retrieving alerts.
/// </summary>
public record AlertQuery
{
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public AlertSeverity? MinimumSeverity { get; init; }
    public string? Component { get; init; }
    public bool? IncludeAcknowledged { get; init; }
    public int? Limit { get; init; }
}

/// <summary>
/// Alert statistics as required by V5 standards.
/// </summary>
public record AlertStatistics
{
    public long TotalAlerts { get; init; }
    public long CriticalAlerts { get; init; }
    public long ErrorAlerts { get; init; }
    public long WarningAlerts { get; init; }
    public long InformationAlerts { get; init; }
    public Dictionary<string, long> AlertsByComponent { get; init; } = new();
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
}