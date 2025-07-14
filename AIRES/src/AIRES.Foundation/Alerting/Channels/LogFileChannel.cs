using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using Microsoft.Extensions.Configuration;

namespace AIRES.Foundation.Alerting.Channels;

/// <summary>
/// Log file channel for alerts.
/// TODO: Implement full functionality.
/// </summary>
public class LogFileChannel : AIRESServiceBase, IAlertChannel
{
    public string ChannelName => "LogFile";
    public AlertChannelType ChannelType => AlertChannelType.LogFile;
    public bool IsEnabled { get; }
    public AlertSeverity MinimumSeverity { get; }
    
    public LogFileChannel(IAIRESLogger logger, IConfiguration configuration) 
        : base(logger, nameof(LogFileChannel))
    {
        var channelConfig = configuration.GetSection("Alerting:Channels:LogFile");
        IsEnabled = channelConfig.GetValue("Enabled", true);
        MinimumSeverity = Enum.Parse<AlertSeverity>(
            channelConfig.GetValue("MinimumSeverity", "Information")!);
    }
    
    public Task SendAlertAsync(AlertMessage alert, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        // TODO: Implement log file writing
        LogMethodExit();
        return Task.CompletedTask;
    }
    
    public Task<bool> IsHealthyAsync() => Task.FromResult(true);
    
    public Task<Dictionary<string, object>> GetMetricsAsync() => 
        Task.FromResult(new Dictionary<string, object> { ["ChannelName"] = ChannelName });
}