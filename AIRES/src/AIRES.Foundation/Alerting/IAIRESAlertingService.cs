namespace AIRES.Foundation.Alerting;

/// <summary>
/// AIRES-specific alerting service interface for multi-channel alerts.
/// MANDATORY for all AIRES services per V5 standards.
/// </summary>
public interface IAIRESAlertingService
{
    /// <summary>
    /// Raises an alert through all configured channels.
    /// </summary>
    /// <param name="severity">Alert severity level</param>
    /// <param name="component">Component raising the alert</param>
    /// <param name="message">Alert message</param>
    /// <param name="details">Optional additional details</param>
    Task RaiseAlertAsync(
        AlertSeverity severity, 
        string component, 
        string message, 
        Dictionary<string, object>? details = null);
    
    /// <summary>
    /// Gets the health status of the alerting service.
    /// </summary>
    Task<HealthCheckResult> GetHealthStatusAsync();
}

/// <summary>
/// Alert severity levels for AIRES system.
/// </summary>
public enum AlertSeverity
{
    /// <summary>
    /// Informational alert - no action required
    /// </summary>
    Information = 0,
    
    /// <summary>
    /// Warning alert - potential issue
    /// </summary>
    Warning = 1,
    
    /// <summary>
    /// Error alert - action required
    /// </summary>
    Error = 2,
    
    /// <summary>
    /// Critical alert - immediate action required
    /// </summary>
    Critical = 3
}

/// <summary>
/// Health check result for alerting service.
/// </summary>
public record HealthCheckResult(
    bool IsHealthy,
    string Status,
    Dictionary<string, object>? Details = null);