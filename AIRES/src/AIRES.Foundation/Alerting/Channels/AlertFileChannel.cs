using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using Microsoft.Extensions.Configuration;

namespace AIRES.Foundation.Alerting.Channels;

/// <summary>
/// Alert file channel for agent monitoring.
/// TODO: Implement full functionality.
/// </summary>
public class AlertFileChannel : AIRESServiceBase, IAlertChannel
{
    public string ChannelName => "AlertFile";
    public AlertChannelType ChannelType => AlertChannelType.AlertFile;
    public bool IsEnabled { get; }
    public AlertSeverity MinimumSeverity { get; }
    
    public AlertFileChannel(IAIRESLogger logger, IConfiguration configuration) 
        : base(logger, nameof(AlertFileChannel))
    {
        var channelConfig = configuration.GetSection("Alerting:Channels:AlertFile");
        IsEnabled = channelConfig.GetValue("Enabled", true);
        MinimumSeverity = Enum.Parse<AlertSeverity>(
            channelConfig.GetValue("MinimumSeverity", "Warning")!);
    }
    
    public Task SendAlertAsync(AlertMessage alert, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        // TODO: Implement alert file writing
        LogMethodExit();
        return Task.CompletedTask;
    }
    
    public Task<bool> IsHealthyAsync() => Task.FromResult(true);
    
    public Task<Dictionary<string, object>> GetMetricsAsync() => 
        Task.FromResult(new Dictionary<string, object> { ["ChannelName"] = ChannelName });
}