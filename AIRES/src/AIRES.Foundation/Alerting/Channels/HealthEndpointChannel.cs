using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using Microsoft.Extensions.Configuration;

namespace AIRES.Foundation.Alerting.Channels;

/// <summary>
/// Health endpoint channel for external monitoring.
/// TODO: Implement full functionality.
/// </summary>
public class HealthEndpointChannel : AIRESServiceBase, IAlertChannel
{
    public string ChannelName => "HealthEndpoint";
    public AlertChannelType ChannelType => AlertChannelType.HealthEndpoint;
    public bool IsEnabled { get; }
    public AlertSeverity MinimumSeverity { get; }
    
    public HealthEndpointChannel(IAIRESLogger logger, IConfiguration configuration) 
        : base(logger, nameof(HealthEndpointChannel))
    {
        var channelConfig = configuration.GetSection("Alerting:Channels:HealthEndpoint");
        IsEnabled = bool.Parse(channelConfig["Enabled"] ?? "false"); // Disabled by default
        MinimumSeverity = Enum.Parse<AlertSeverity>(
            channelConfig["MinimumSeverity"] ?? "Information");
    }
    
    public Task SendAlertAsync(AlertMessage alert, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        // TODO: Implement health endpoint updates
        LogMethodExit();
        return Task.CompletedTask;
    }
    
    public Task<bool> IsHealthyAsync() => Task.FromResult(true);
    
    public Task<Dictionary<string, object>> GetMetricsAsync() => 
        Task.FromResult(new Dictionary<string, object> { ["ChannelName"] = ChannelName });
}