using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using System.Collections.Concurrent;

namespace AIRES.Foundation.Alerting;

/// <summary>
/// Basic console-based implementation of IAIRESAlertingService.
/// This is a minimal implementation to satisfy V5 standards.
/// TODO: Implement full multi-channel alerting as per requirements.
/// </summary>
public class ConsoleAlertingService : AIRESServiceBase, IAIRESAlertingService
{
    private readonly ConcurrentDictionary<AlertSeverity, int> _alertCounts = new();
    private readonly string _alertFilePath;
    
    public ConsoleAlertingService(IAIRESLogger logger) 
        : base(logger, nameof(ConsoleAlertingService))
    {
        _alertFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "alerts", 
            $"aires_alerts_{DateTime.UtcNow:yyyyMMdd}.log");
        
        // Ensure alerts directory exists
        var alertDir = Path.GetDirectoryName(_alertFilePath);
        if (!string.IsNullOrEmpty(alertDir) && !Directory.Exists(alertDir))
        {
            Directory.CreateDirectory(alertDir);
        }
    }
    
    public async Task RaiseAlertAsync(
        AlertSeverity severity, 
        string component, 
        string message, 
        Dictionary<string, object>? details = null)
    {
        LogMethodEntry();
        
        try
        {
            // Track alert count
            _alertCounts.AddOrUpdate(severity, 1, (key, value) => value + 1);
            
            var timestamp = DateTime.UtcNow;
            var alertId = Guid.NewGuid();
            
            // Format alert message
            var formattedAlert = FormatAlert(alertId, timestamp, severity, component, message, details);
            
            // Channel 1: Console output with color coding
            WriteToConsole(severity, formattedAlert);
            
            // Channel 2: Log file
            await WriteToFileAsync(formattedAlert);
            
            // Channel 3: Structured logging
            LogAlert(severity, component, message, details);
            
            // TODO: Channel 4: Windows Event Log
            // TODO: Channel 5: Health endpoint
            
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Failed to raise alert", ex);
            LogMethodExit();
            throw;
        }
    }
    
    public async Task<HealthCheckResult> GetHealthStatusAsync()
    {
        LogMethodEntry();
        
        try
        {
            var totalAlerts = _alertCounts.Sum(kvp => kvp.Value);
            var criticalAlerts = _alertCounts.GetValueOrDefault(AlertSeverity.Critical, 0);
            var errorAlerts = _alertCounts.GetValueOrDefault(AlertSeverity.Error, 0);
            
            var isHealthy = criticalAlerts == 0 && errorAlerts < 10;
            var status = isHealthy ? "Healthy" : "Degraded";
            
            var details = new Dictionary<string, object>
            {
                ["TotalAlerts"] = totalAlerts,
                ["CriticalAlerts"] = criticalAlerts,
                ["ErrorAlerts"] = errorAlerts,
                ["WarningAlerts"] = _alertCounts.GetValueOrDefault(AlertSeverity.Warning, 0),
                ["InformationAlerts"] = _alertCounts.GetValueOrDefault(AlertSeverity.Information, 0),
                ["AlertFilePath"] = _alertFilePath
            };
            
            LogMethodExit();
            return await Task.FromResult(new HealthCheckResult(isHealthy, status, details));
        }
        catch (Exception ex)
        {
            LogError("Failed to get health status", ex);
            LogMethodExit();
            return new HealthCheckResult(false, "Error", new Dictionary<string, object> 
            { 
                ["Error"] = ex.Message 
            });
        }
    }
    
    private string FormatAlert(
        Guid alertId,
        DateTime timestamp,
        AlertSeverity severity,
        string component,
        string message,
        Dictionary<string, object>? details)
    {
        var detailsJson = details != null 
            ? System.Text.Json.JsonSerializer.Serialize(details) 
            : "{}";
            
        return $"[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{severity}] [{component}] {message} | ID: {alertId} | Details: {detailsJson}";
    }
    
    private void WriteToConsole(AlertSeverity severity, string formattedAlert)
    {
        var originalColor = Console.ForegroundColor;
        
        try
        {
            Console.ForegroundColor = severity switch
            {
                AlertSeverity.Critical => ConsoleColor.Red,
                AlertSeverity.Error => ConsoleColor.DarkRed,
                AlertSeverity.Warning => ConsoleColor.Yellow,
                AlertSeverity.Information => ConsoleColor.Cyan,
                _ => ConsoleColor.White
            };
            
            Console.WriteLine(formattedAlert);
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }
    
    private async Task WriteToFileAsync(string formattedAlert)
    {
        try
        {
            await File.AppendAllTextAsync(_alertFilePath, formattedAlert + Environment.NewLine);
        }
        catch (Exception ex)
        {
            LogError($"Failed to write alert to file: {_alertFilePath}", ex);
        }
    }
    
    private void LogAlert(AlertSeverity severity, string component, string message, Dictionary<string, object>? details)
    {
        var logMessage = $"ALERT [{severity}] from {component}: {message}";
        
        switch (severity)
        {
            case AlertSeverity.Critical:
            case AlertSeverity.Error:
                LogError(logMessage);
                break;
            case AlertSeverity.Warning:
                LogWarning(logMessage);
                break;
            default:
                LogInfo(logMessage);
                break;
        }
        
        if (details != null && details.Any())
        {
            LogDebug($"Alert details: {System.Text.Json.JsonSerializer.Serialize(details)}");
        }
    }
}