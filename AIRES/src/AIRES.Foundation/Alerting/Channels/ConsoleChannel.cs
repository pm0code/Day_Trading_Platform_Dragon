using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;

namespace AIRES.Foundation.Alerting.Channels;

/// <summary>
/// Console output channel for alerts.
/// Thread-safe implementation with color coding.
/// </summary>
public class ConsoleChannel : AIRESServiceBase, IAlertChannel
{
    private readonly bool _isEnabled;
    private readonly AlertSeverity _minimumSeverity;
    // Static members first per SA1204
    private static readonly Dictionary<AlertSeverity, ConsoleColor> SeverityColors = new()
    {
        [AlertSeverity.Information] = ConsoleColor.Cyan,
        [AlertSeverity.Warning] = ConsoleColor.Yellow,
        [AlertSeverity.Error] = ConsoleColor.Red,
        [AlertSeverity.Critical] = ConsoleColor.DarkRed
    };
    
    private readonly ConcurrentDictionary<AlertSeverity, long> _metricCounts = new();
    private readonly object _consoleLock = new();
    
    public string ChannelName => "Console";
    public AlertChannelType ChannelType => AlertChannelType.Console;
    public bool IsEnabled => _isEnabled;
    public AlertSeverity MinimumSeverity => _minimumSeverity;
    
    public ConsoleChannel(IAIRESLogger logger, IConfiguration configuration) 
        : base(logger, nameof(ConsoleChannel))
    {
        var channelConfig = configuration.GetSection("Alerting:Channels:Console");
        _isEnabled = channelConfig.GetValue("Enabled", true);
        _minimumSeverity = Enum.Parse<AlertSeverity>(
            channelConfig.GetValue("MinimumSeverity", "Information")!);
            
        LogInfo($"Console channel initialized. Enabled: {_isEnabled}, MinSeverity: {_minimumSeverity}");
    }
    
    public async Task SendAlertAsync(AlertMessage alert, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            if (!IsEnabled || alert.Severity < MinimumSeverity)
            {
                LogDebug($"Alert filtered. Enabled: {IsEnabled}, Severity: {alert.Severity}");
                return;
            }
            
            // Thread-safe console writing
            await Task.Run(() =>
            {
                lock (_consoleLock)
                {
                    var originalColor = Console.ForegroundColor;
                    try
                    {
                        Console.ForegroundColor = SeverityColors[alert.Severity];
                        Console.WriteLine(FormatAlert(alert));
                    }
                    finally
                    {
                        Console.ForegroundColor = originalColor;
                    }
                }
            }, cancellationToken);
            
            // Update metrics
            _metricCounts.AddOrUpdate(alert.Severity, 1, (_, count) => count + 1);
            
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Failed to send console alert", ex);
            LogMethodExit();
            throw;
        }
    }
    
    public Task<bool> IsHealthyAsync()
    {
        LogMethodEntry();
        
        try
        {
            // Console is always healthy if we can write to it
            var isHealthy = Console.Out != null;
            LogMethodExit();
            return Task.FromResult(isHealthy);
        }
        catch (Exception ex)
        {
            LogError("Failed to check console health", ex);
            LogMethodExit();
            return Task.FromResult(false);
        }
    }
    
    public Task<Dictionary<string, object>> GetMetricsAsync()
    {
        LogMethodEntry();
        
        try
        {
            var metrics = new Dictionary<string, object>
            {
                ["ChannelName"] = ChannelName,
                ["IsEnabled"] = IsEnabled,
                ["TotalAlerts"] = _metricCounts.Sum(kvp => kvp.Value)
            };
            
            foreach (var kvp in _metricCounts)
            {
                metrics[$"Alerts_{kvp.Key}"] = kvp.Value;
            }
            
            LogMethodExit();
            return Task.FromResult(metrics);
        }
        catch (Exception ex)
        {
            LogError("Failed to get metrics", ex);
            LogMethodExit();
            throw;
        }
    }
    
    private string FormatAlert(AlertMessage alert)
    {
        var detailsJson = alert.Details != null && alert.Details.Any() 
            ? $" | Details: {System.Text.Json.JsonSerializer.Serialize(alert.Details)}" 
            : string.Empty;
            
        var suggestedAction = !string.IsNullOrEmpty(alert.SuggestedAction)
            ? $" | Action: {alert.SuggestedAction}"
            : string.Empty;
            
        return $"{alert}{detailsJson}{suggestedAction}";
    }
}