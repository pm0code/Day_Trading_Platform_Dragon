using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using Microsoft.Extensions.Configuration;

namespace AIRES.Foundation.Alerting.Channels;

/// <summary>
/// Windows Event Log channel for system integration.
/// TODO: Implement full functionality.
/// </summary>
public class WindowsEventLogChannel : AIRESServiceBase, IAlertChannel
{
    public string ChannelName => "WindowsEventLog";
    public AlertChannelType ChannelType => AlertChannelType.WindowsEventLog;
    public bool IsEnabled { get; }
    public AlertSeverity MinimumSeverity { get; }
    
    public WindowsEventLogChannel(IAIRESLogger logger, IConfiguration configuration) 
        : base(logger, nameof(WindowsEventLogChannel))
    {
        var channelConfig = configuration.GetSection("Alerting:Channels:WindowsEventLog");
        IsEnabled = bool.Parse(channelConfig["Enabled"] ?? "false"); // Disabled by default
        MinimumSeverity = Enum.Parse<AlertSeverity>(
            channelConfig["MinimumSeverity"] ?? "Error");
    }
    
    public Task SendAlertAsync(AlertMessage alert, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        // TODO: Implement Windows Event Log writing
        LogMethodExit();
        return Task.CompletedTask;
    }
    
    public Task<bool> IsHealthyAsync() => Task.FromResult(true);
    
    public Task<Dictionary<string, object>> GetMetricsAsync() => 
        Task.FromResult(new Dictionary<string, object> { ["ChannelName"] = ChannelName });
}