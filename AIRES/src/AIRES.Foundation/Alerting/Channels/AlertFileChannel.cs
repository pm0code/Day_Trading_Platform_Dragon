using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace AIRES.Foundation.Alerting.Channels;

/// <summary>
/// Alert file channel that writes alerts to structured JSON files for agent monitoring.
/// Creates daily alert files with severity-based categorization.
/// </summary>
public class AlertFileChannel : AIRESServiceBase, IAlertChannel
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    
    private readonly string _alertDirectory;
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private readonly bool _prettifyJson;
    private readonly Dictionary<AlertSeverity, long> _alertsBySeverity = new();
    private long _totalAlertsWritten;
    private DateTime _lastWriteTime = DateTime.UtcNow;
    
    public string ChannelName => "AlertFile";
    public AlertChannelType ChannelType => AlertChannelType.AlertFile;
    public bool IsEnabled { get; }
    public AlertSeverity MinimumSeverity { get; }
    
    public AlertFileChannel(IAIRESLogger logger, IConfiguration configuration) 
        : base(logger, nameof(AlertFileChannel))
    {
        var channelConfig = configuration.GetSection("Alerting:Channels:AlertFile");
        var enabledValue = channelConfig["Enabled"];
        IsEnabled = !string.IsNullOrWhiteSpace(enabledValue) && 
                   (enabledValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    enabledValue.Equals("1", StringComparison.OrdinalIgnoreCase));
        MinimumSeverity = Enum.Parse<AlertSeverity>(
            channelConfig["MinimumSeverity"] ?? "Warning");
        
        // Configure alert directory
        _alertDirectory = channelConfig["Directory"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIRES", "AlertFiles");
        
        var prettifyValue = channelConfig["PrettifyJson"];
        _prettifyJson = string.IsNullOrWhiteSpace(prettifyValue) || 
                       prettifyValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                       prettifyValue.Equals("1", StringComparison.OrdinalIgnoreCase);
        
        // Initialize severity counters
        foreach (AlertSeverity severity in Enum.GetValues<AlertSeverity>())
        {
            _alertsBySeverity[severity] = 0;
        }
        
        // Ensure directory exists
        Directory.CreateDirectory(_alertDirectory);
        
        // Create severity subdirectories
        foreach (AlertSeverity severity in Enum.GetValues<AlertSeverity>())
        {
            Directory.CreateDirectory(Path.Combine(_alertDirectory, severity.ToString()));
        }
    }
    
    public async Task SendAlertAsync(AlertMessage alert, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        if (alert.Severity < MinimumSeverity)
        {
            LogMethodExit();
            return;
        }
        
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Determine file paths
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var severityDir = Path.Combine(_alertDirectory, alert.Severity.ToString());
            var fileName = $"alerts_{date}.json";
            var filePath = Path.Combine(severityDir, fileName);
            
            // Create alert record
            var alertRecord = new
            {
                alert.Id,
                alert.Timestamp,
                alert.Severity,
                alert.Component,
                alert.Message,
                alert.SuggestedAction,
                alert.Details,
                MachineName = Environment.MachineName,
                ProcessId = Environment.ProcessId,
                ThreadId = Environment.CurrentManagedThreadId
            };
            
            // Read existing alerts or create new list
            var alerts = new List<object>();
            if (File.Exists(filePath))
            {
                try
                {
                    var existingContent = await File.ReadAllTextAsync(filePath, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(existingContent))
                    {
                        var existingAlerts = JsonSerializer.Deserialize<List<object>>(existingContent);
                        if (existingAlerts != null)
                        {
                            alerts.AddRange(existingAlerts);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to read existing alerts file: {filePath}", ex);
                }
            }
            
            // Add new alert
            alerts.Add(alertRecord);
            
            // Write back to file
            var json = _prettifyJson 
                ? JsonSerializer.Serialize(alerts, JsonOptions)
                : JsonSerializer.Serialize(alerts);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            
            // Also write to a "latest" symlink for easy access
            var latestPath = Path.Combine(severityDir, "latest.json");
            try
            {
                if (File.Exists(latestPath))
                    File.Delete(latestPath);
                File.Copy(filePath, latestPath);
            }
            catch
            {
                // Ignore symlink creation failures
            }
            
            // Update metrics
            _totalAlertsWritten++;
            _alertsBySeverity[alert.Severity]++;
            _lastWriteTime = DateTime.UtcNow;
            
            Logger.LogDebug($"Alert written to file: {filePath}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to write alert to file", ex);
            throw;
        }
        finally
        {
            _writeSemaphore.Release();
            LogMethodExit();
        }
    }
    
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Check if we can write to the directory
            var testPath = Path.Combine(_alertDirectory, ".health");
            await File.WriteAllTextAsync(testPath, DateTime.UtcNow.ToString("O"));
            File.Delete(testPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public Task<Dictionary<string, object>> GetMetricsAsync()
    {
        var metrics = new Dictionary<string, object>
        {
            ["ChannelName"] = ChannelName,
            ["AlertDirectory"] = _alertDirectory,
            ["TotalAlertsWritten"] = _totalAlertsWritten,
            ["LastWriteTime"] = _lastWriteTime,
            ["PrettifyJson"] = _prettifyJson
        };
        
        // Add severity breakdown
        foreach (var kvp in _alertsBySeverity)
        {
            metrics[$"Alerts_{kvp.Key}"] = kvp.Value;
        }
        
        // Add file counts
        try
        {
            var fileCount = Directory.GetFiles(_alertDirectory, "*.json", SearchOption.AllDirectories).Length;
            metrics["TotalAlertFiles"] = fileCount;
        }
        catch
        {
            metrics["TotalAlertFiles"] = -1;
        }
        
        return Task.FromResult(metrics);
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _writeSemaphore?.Dispose();
        }
        base.Dispose(disposing);
    }
}