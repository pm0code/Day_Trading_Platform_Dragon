using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace AIRES.Foundation.Alerting;

/// <summary>
/// Main alerting service implementation following AI-validated architecture.
/// Implements multi-channel alerting with throttling and persistence.
/// </summary>
public class AIRESAlertingService : AIRESServiceBase, IAIRESAlertingService
{
    private readonly IAlertChannelFactory _channelFactory;
    private readonly IAlertThrottler _throttler;
    private readonly IAlertPersistence _persistence;
    private readonly ImmutableList<IAlertChannel> _channels;
    private readonly SemaphoreSlim _alertSemaphore;
    private readonly IConfiguration _configuration;
    
    public AIRESAlertingService(
        IAIRESLogger logger,
        IAlertChannelFactory channelFactory,
        IAlertThrottler throttler,
        IAlertPersistence persistence,
        IConfiguration configuration) 
        : base(logger, nameof(AIRESAlertingService))
    {
        _channelFactory = channelFactory ?? throw new ArgumentNullException(nameof(channelFactory));
        _throttler = throttler ?? throw new ArgumentNullException(nameof(throttler));
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        
        // Create all configured channels
        _channels = _channelFactory.CreateAllChannels(configuration).ToImmutableList();
        
        // Allow 10 concurrent alerts as per Gemini guidance
        _alertSemaphore = new SemaphoreSlim(10, 10);
        
        LogInfo($"Initialized with {_channels.Count} alert channels");
    }
    
    public async Task RaiseAlertAsync(
        AlertSeverity severity, 
        string component, 
        string message, 
        Dictionary<string, object>? details = null)
    {
        LogMethodEntry();
        
        await _alertSemaphore.WaitAsync();
        try
        {
            // Generate alert key for throttling
            var alertKey = GenerateAlertKey(component, message);
            
            // Check throttling
            if (_throttler.ShouldThrottle(alertKey, severity))
            {
                LogDebug($"Alert throttled: {alertKey}");
                LogMethodExit();
                return;
            }
            
            // Create immutable alert message
            var alert = new AlertMessage
            {
                Id = Guid.NewGuid(),
                Severity = severity,
                Component = component,
                Message = message,
                Details = details?.ToImmutableDictionary(),
                Timestamp = DateTime.UtcNow,
                SuggestedAction = GenerateSuggestedAction(severity, component, message)
            };
            
            // Persist first for audit trail
            var record = await _persistence.SaveAlertAsync(alert);
            LogDebug($"Alert persisted with ID: {record.Id}");
            
            // Send to all enabled channels in parallel
            var enabledChannels = _channels
                .Where(c => c.IsEnabled && c.MinimumSeverity <= severity)
                .ToList();
                
            if (enabledChannels.Any())
            {
                var sendTasks = enabledChannels
                    .Select(c => SendToChannelWithResilience(c, alert));
                    
                await Task.WhenAll(sendTasks);
                LogInfo($"Alert sent to {enabledChannels.Count} channels");
            }
            
            // Record alert for throttling
            _throttler.RecordAlert(alertKey, severity);
            
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Failed to raise alert", ex);
            LogMethodExit();
            throw;
        }
        finally
        {
            _alertSemaphore.Release();
        }
    }
    
    public async Task<HealthCheckResult> GetHealthStatusAsync()
    {
        LogMethodEntry();
        
        try
        {
            // Get statistics from persistence
            var now = DateTime.UtcNow;
            var stats = await _persistence.GetStatisticsAsync(now.AddHours(-1), now);
            
            // Check channel health
            var channelHealthTasks = _channels.Select(async c => new
            {
                Channel = c.ChannelName,
                IsHealthy = await c.IsHealthyAsync()
            });
            
            var channelHealthResults = await Task.WhenAll(channelHealthTasks);
            var unhealthyChannels = channelHealthResults.Where(r => !r.IsHealthy).ToList();
            
            // Determine overall health
            var isHealthy = stats.CriticalAlerts == 0 && 
                           stats.ErrorAlerts < 10 && 
                           unhealthyChannels.Count == 0;
                           
            var status = isHealthy ? "Healthy" : 
                        stats.CriticalAlerts > 0 ? "Critical" : 
                        "Degraded";
            
            var details = new Dictionary<string, object>
            {
                ["TotalAlerts"] = stats.TotalAlerts,
                ["CriticalAlerts"] = stats.CriticalAlerts,
                ["ErrorAlerts"] = stats.ErrorAlerts,
                ["WarningAlerts"] = stats.WarningAlerts,
                ["InformationAlerts"] = stats.InformationAlerts,
                ["EnabledChannels"] = _channels.Count(c => c.IsEnabled),
                ["HealthyChannels"] = channelHealthResults.Count(r => r.IsHealthy),
                ["UnhealthyChannels"] = unhealthyChannels.Select(c => c.Channel).ToList()
            };
            
            LogMethodExit();
            return new HealthCheckResult(isHealthy, status, details);
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
    
    public async Task<AlertStatistics> GetAlertStatisticsAsync(TimeSpan period)
    {
        LogMethodEntry();
        
        try
        {
            var now = DateTime.UtcNow;
            var from = now.Subtract(period);
            
            var stats = await _persistence.GetStatisticsAsync(from, now);
            
            LogMethodExit();
            return stats;
        }
        catch (Exception ex)
        {
            LogError("Failed to get alert statistics", ex);
            LogMethodExit();
            throw;
        }
    }
    
    private async Task SendToChannelWithResilience(IAlertChannel channel, AlertMessage alert)
    {
        try
        {
            // Use timeout for channel operations
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await channel.SendAlertAsync(alert, cts.Token);
            
            LogDebug($"Alert sent successfully to channel: {channel.ChannelName}");
        }
        catch (OperationCanceledException)
        {
            LogWarning($"Alert send timeout for channel: {channel.ChannelName}");
            // Don't throw - other channels should still receive the alert
        }
        catch (Exception ex)
        {
            LogError($"Failed to send alert to channel {channel.ChannelName}", ex);
            // Don't throw - other channels should still receive the alert
        }
    }
    
    private string GenerateAlertKey(string component, string message)
    {
        // Create a key that groups similar alerts for throttling
        var normalizedMessage = message.Length > 100 ? message.Substring(0, 100) : message;
        return $"{component}:{normalizedMessage.GetHashCode()}";
    }
    
    private string? GenerateSuggestedAction(AlertSeverity severity, string component, string message)
    {
        // Generate suggested actions based on common patterns
        return severity switch
        {
            AlertSeverity.Critical when message.Contains("unavailable", StringComparison.OrdinalIgnoreCase) => 
                "Check if the service is running and accessible",
                
            AlertSeverity.Error when message.Contains("failed", StringComparison.OrdinalIgnoreCase) => 
                "Review error logs and check system resources",
                
            AlertSeverity.Warning when message.Contains("slow", StringComparison.OrdinalIgnoreCase) => 
                "Monitor performance metrics and consider optimization",
                
            _ => null
        };
    }
}