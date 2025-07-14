using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AIRES.Foundation.Alerting.Channels;

/// <summary>
/// Windows Event Log channel for system integration.
/// Writes alerts to Windows Event Log for monitoring by system administrators.
/// </summary>
public class WindowsEventLogChannel : AIRESServiceBase, IAlertChannel
{
    private readonly string _sourceName;
    private readonly string _logName;
    private readonly bool _isWindows;
    private readonly Dictionary<AlertSeverity, long> _alertsBySeverity = new();
    private EventLog? _eventLog;
    private long _totalAlertsWritten;
    private DateTime _lastWriteTime = DateTime.UtcNow;
    
    public string ChannelName => "WindowsEventLog";
    public AlertChannelType ChannelType => AlertChannelType.WindowsEventLog;
    public bool IsEnabled { get; }
    public AlertSeverity MinimumSeverity { get; }
    
    public WindowsEventLogChannel(IAIRESLogger logger, IConfiguration configuration) 
        : base(logger, nameof(WindowsEventLogChannel))
    {
        var channelConfig = configuration.GetSection("Alerting:Channels:WindowsEventLog");
        var enabledValue = channelConfig["Enabled"];
        var configEnabled = !string.IsNullOrWhiteSpace(enabledValue) && 
                           (enabledValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                            enabledValue.Equals("1", StringComparison.OrdinalIgnoreCase));
        IsEnabled = configEnabled; // Disabled by default
        MinimumSeverity = Enum.Parse<AlertSeverity>(
            channelConfig["MinimumSeverity"] ?? "Error");
        
        _sourceName = channelConfig["SourceName"] ?? "AIRES";
        _logName = channelConfig["LogName"] ?? "Application";
        _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        
        // Initialize severity counters
        foreach (AlertSeverity severity in Enum.GetValues<AlertSeverity>())
        {
            _alertsBySeverity[severity] = 0;
        }
        
        // Initialize Windows Event Log if on Windows
        if (_isWindows && IsEnabled)
        {
            try
            {
                // Create the event source if it doesn't exist
                CreateEventLogSource();
                
                _eventLog = CreateEventLog();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize Windows Event Log", ex);
                // Don't throw - just disable the channel
                IsEnabled = false;
            }
        }
        else if (!_isWindows && IsEnabled)
        {
            Logger.LogWarning("Windows Event Log channel is enabled but OS is not Windows. Channel will be disabled.");
            IsEnabled = false;
        }
    }
    
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void WriteToEventLog(AlertMessage alert)
    {
        if (_eventLog == null) return;
        
        var entryType = MapSeverityToEventLogEntryType(alert.Severity);
        var message = FormatAlertForEventLog(alert);
        _eventLog.WriteEntry(message, entryType, (int)alert.Severity, 0);
    }
    
    public Task SendAlertAsync(AlertMessage alert, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        if (!IsEnabled || !_isWindows || _eventLog == null)
        {
            LogMethodExit();
            return Task.CompletedTask;
        }
        
        if (alert.Severity < MinimumSeverity)
        {
            LogMethodExit();
            return Task.CompletedTask;
        }
        
        try
        {
            // Write to event log
            if (_isWindows)
            {
                WriteToEventLog(alert);
            }
            
            // Update metrics
            _totalAlertsWritten++;
            _alertsBySeverity[alert.Severity]++;
            _lastWriteTime = DateTime.UtcNow;
            
            Logger.LogDebug($"Alert written to Windows Event Log: {alert.Id}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to write alert to Windows Event Log", ex);
            throw;
        }
        finally
        {
            LogMethodExit();
        }
        
        return Task.CompletedTask;
    }
    
    public Task<bool> IsHealthyAsync()
    {
        if (!_isWindows || !IsEnabled)
            return Task.FromResult(false);
        
        if (_isWindows)
        {
            return Task.FromResult(CheckWindowsEventLogHealth());
        }
        
        return Task.FromResult(false);
    }
    
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private bool CheckWindowsEventLogHealth()
    {
        try
        {
            // Check if we can access the event log
            if (_eventLog != null)
            {
                _ = GetEventLogCount();
                return true;
            }
        }
        catch
        {
            // Ignore exceptions
        }
        
        return false;
    }
    
    public Task<Dictionary<string, object>> GetMetricsAsync()
    {
        var metrics = new Dictionary<string, object>
        {
            ["ChannelName"] = ChannelName,
            ["SourceName"] = _sourceName,
            ["LogName"] = _logName,
            ["IsWindows"] = _isWindows,
            ["TotalAlertsWritten"] = _totalAlertsWritten,
            ["LastWriteTime"] = _lastWriteTime
        };
        
        // Add severity breakdown
        foreach (var kvp in _alertsBySeverity)
        {
            metrics[$"Alerts_{kvp.Key}"] = kvp.Value;
        }
        
        // Add event log entry count if available
        if (_isWindows && _eventLog != null)
        {
            metrics["EventLogEntries"] = GetWindowsEventLogMetric();
        }
        
        return Task.FromResult(metrics);
    }
    
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void CreateEventLogSource()
    {
        if (!EventLog.SourceExists(_sourceName))
        {
            EventLog.CreateEventSource(_sourceName, _logName);
            Logger.LogInfo($"Created Windows Event Log source: {_sourceName} in log: {_logName}");
        }
    }
    
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private EventLog CreateEventLog()
    {
        return new EventLog(_logName)
        {
            Source = _sourceName
        };
    }
    
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static EventLogEntryType MapSeverityToEventLogEntryType(AlertSeverity severity)
    {
        return severity switch
        {
            AlertSeverity.Critical => EventLogEntryType.Error,
            AlertSeverity.Error => EventLogEntryType.Error,
            AlertSeverity.Warning => EventLogEntryType.Warning,
            AlertSeverity.Information => EventLogEntryType.Information,
            _ => EventLogEntryType.Information
        };
    }
    
    private static string FormatAlertForEventLog(AlertMessage alert)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"AIRES Alert: {alert.Severity}");
        sb.AppendLine($"Component: {alert.Component}");
        sb.AppendLine($"Alert ID: {alert.Id}");
        sb.AppendLine($"Timestamp: {alert.Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC");
        sb.AppendLine();
        sb.AppendLine("Message:");
        sb.AppendLine(alert.Message);
        
        if (!string.IsNullOrEmpty(alert.SuggestedAction))
        {
            sb.AppendLine();
            sb.AppendLine("Suggested Action:");
            sb.AppendLine(alert.SuggestedAction);
        }
        
        if (alert.Details != null && alert.Details.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Additional Details:");
            foreach (var kvp in alert.Details)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
        }
        
        return sb.ToString();
    }
    
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private int GetEventLogCount()
    {
        return _eventLog?.Entries.Count ?? 0;
    }
    
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private int GetWindowsEventLogMetric()
    {
        try
        {
            return GetEventLogCount();
        }
        catch
        {
            return -1;
        }
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _eventLog?.Dispose();
        }
        base.Dispose(disposing);
    }
}