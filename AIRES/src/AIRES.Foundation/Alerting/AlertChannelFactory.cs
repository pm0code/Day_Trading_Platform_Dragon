using AIRES.Foundation.Alerting.Channels;
using AIRES.Foundation.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIRES.Foundation.Alerting;

/// <summary>
/// Factory implementation for creating alert channels.
/// Follows Factory pattern as recommended by Gemini.
/// </summary>
public class AlertChannelFactory : IAlertChannelFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAIRESLogger _logger;
    
    public AlertChannelFactory(IServiceProvider serviceProvider, IAIRESLogger logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public IAlertChannel CreateChannel(AlertChannelType channelType, IConfiguration configuration)
    {
        _logger.LogInfo($"Creating alert channel: {channelType}");
        
        return channelType switch
        {
            AlertChannelType.Console => new ConsoleChannel(_logger, configuration),
            
            AlertChannelType.LogFile => new LogFileChannel(_logger, configuration),
            
            AlertChannelType.AlertFile => new AlertFileChannel(_logger, configuration),
            
            AlertChannelType.WindowsEventLog => new WindowsEventLogChannel(_logger, configuration),
            
            AlertChannelType.HealthEndpoint => new HealthEndpointChannel(_logger, configuration),
            
            _ => throw new NotSupportedException($"Channel type {channelType} is not supported")
        };
    }
    
    public IEnumerable<IAlertChannel> CreateAllChannels(IConfiguration configuration)
    {
        _logger.LogInfo("Creating all configured alert channels");
        
        var channels = new List<IAlertChannel>();
        var alertingConfig = configuration.GetSection("Alerting:Channels");
        
        // Create each channel type if configured
        foreach (AlertChannelType channelType in Enum.GetValues<AlertChannelType>())
        {
            var channelConfig = alertingConfig.GetSection(channelType.ToString());
            var isEnabled = bool.Parse(channelConfig["Enabled"] ?? "false");
            
            if (isEnabled)
            {
                try
                {
                    var channel = CreateChannel(channelType, configuration);
                    channels.Add(channel);
                    _logger.LogInfo($"Created {channelType} channel");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to create {channelType} channel", ex);
                    // Continue with other channels
                }
            }
            else
            {
                _logger.LogDebug($"Channel {channelType} is disabled");
            }
        }
        
        _logger.LogInfo($"Created {channels.Count} alert channels");
        return channels;
    }
}