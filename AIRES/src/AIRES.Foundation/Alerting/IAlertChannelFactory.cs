using Microsoft.Extensions.Configuration;

namespace AIRES.Foundation.Alerting;

/// <summary>
/// Factory for creating alert channels based on configuration.
/// Implements Factory pattern as recommended by Gemini.
/// </summary>
public interface IAlertChannelFactory
{
    /// <summary>
    /// Creates a specific alert channel.
    /// </summary>
    IAlertChannel CreateChannel(AlertChannelType channelType, IConfiguration configuration);
    
    /// <summary>
    /// Creates all configured alert channels.
    /// </summary>
    IEnumerable<IAlertChannel> CreateAllChannels(IConfiguration configuration);
}