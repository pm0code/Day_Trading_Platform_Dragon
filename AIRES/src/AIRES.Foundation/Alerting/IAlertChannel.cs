using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIRES.Foundation.Alerting;

/// <summary>
/// Represents a single alert delivery channel.
/// Based on Gemini architectural guidance for channel abstraction.
/// </summary>
public interface IAlertChannel : IDisposable
{
    /// <summary>
    /// Gets the unique name of this channel.
    /// </summary>
    string ChannelName { get; }
    
    /// <summary>
    /// Gets the type of this channel.
    /// </summary>
    AlertChannelType ChannelType { get; }
    
    /// <summary>
    /// Gets whether this channel is currently enabled.
    /// </summary>
    bool IsEnabled { get; }
    
    /// <summary>
    /// Gets the minimum severity level for this channel.
    /// </summary>
    AlertSeverity MinimumSeverity { get; }
    
    /// <summary>
    /// Sends an alert through this channel.
    /// </summary>
    Task SendAlertAsync(AlertMessage alert, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if this channel is healthy and able to send alerts.
    /// </summary>
    Task<bool> IsHealthyAsync();
    
    /// <summary>
    /// Gets performance metrics for this channel.
    /// </summary>
    Task<Dictionary<string, object>> GetMetricsAsync();
}

/// <summary>
/// Types of alert channels supported by AIRES.
/// </summary>
public enum AlertChannelType
{
    /// <summary>
    /// Console output channel
    /// </summary>
    Console,
    
    /// <summary>
    /// Log file channel
    /// </summary>
    LogFile,
    
    /// <summary>
    /// Alert file channel for agent monitoring
    /// </summary>
    AlertFile,
    
    /// <summary>
    /// Windows Event Log channel
    /// </summary>
    WindowsEventLog,
    
    /// <summary>
    /// Health endpoint channel for external monitoring
    /// </summary>
    HealthEndpoint
}