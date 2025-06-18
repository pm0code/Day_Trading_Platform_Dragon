using System.Management;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using TradingPlatform.Core.Interfaces;
using Microsoft.Extensions.Options;
using TradingPlatform.DisplayManagement.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.DisplayManagement.Services;

/// <summary>
/// Interface for centralized display session detection and management
/// </summary>
public interface IDisplaySessionService
{
    /// <summary>
    /// Current display session information
    /// </summary>
    DisplaySessionInfo CurrentSession { get; }

    /// <summary>
    /// Observable stream of session changes
    /// </summary>
    IObservable<DisplaySessionChangedEventArgs> SessionChanged { get; }

    /// <summary>
    /// Gets current session information
    /// </summary>
    Task<DisplaySessionInfo> GetCurrentSessionAsync();

    /// <summary>
    /// Forces refresh of session detection
    /// </summary>
    Task RefreshSessionInfoAsync();

    /// <summary>
    /// Checks if current session supports specific capability
    /// </summary>
    bool SupportsCapability(string capability);

    /// <summary>
    /// Gets performance recommendations for current session
    /// </summary>
    Task<List<string>> GetPerformanceRecommendationsAsync();
}

/// <summary>
/// Centralized display session detection and management service for DRAGON trading platform
/// Provides system-wide awareness of RDP, direct console, and other session types
/// </summary>
public class DisplaySessionService : BackgroundService, IDisplaySessionService
{
    private readonly ILogger _logger;
    private readonly DisplaySessionOptions _options;
    private readonly Subject<DisplaySessionChangedEventArgs> _sessionChangedSubject = new();

    private DisplaySessionInfo _currentSession = new();
    private DateTime _lastSessionCheck = DateTime.MinValue;

    // Windows API declarations for session detection
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("wtsapi32.dll")]
    private static extern bool WTSQuerySessionInformation(
        IntPtr hServer, int sessionId, WtsInfoClass wtsInfoClass,
        out IntPtr ppBuffer, out int pBytesReturned);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr pMemory);

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    private enum WtsInfoClass
    {
        WTSInitialProgram,
        WTSApplicationName,
        WTSWorkingDirectory,
        WTSOEMId,
        WTSSessionId,
        WTSUserName,
        WTSWinStationName,
        WTSDomainName,
        WTSConnectState,
        WTSClientBuildNumber,
        WTSClientName,
        WTSClientDirectory,
        WTSClientProductId,
        WTSClientHardwareId,
        WTSClientAddress,
        WTSClientDisplay,
        WTSClientProtocolType
    }

    public DisplaySessionInfo CurrentSession => _currentSession;
    public IObservable<DisplaySessionChangedEventArgs> SessionChanged => _sessionChangedSubject.AsObservable();

    public DisplaySessionService(
        ILogger logger,
        IOptions<DisplaySessionOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Background service execution for continuous session monitoring
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TradingLogOrchestrator.Instance.LogInfo("Starting DRAGON display session monitoring service");

        // Initial session detection
        await RefreshSessionInfoAsync();

        // Continuous monitoring
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorSessionChanges();
                await Task.Delay(TimeSpan.FromSeconds(_options.MonitoringIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError(ex, "Error during session monitoring");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Retry after 30 seconds
            }
        }

        TradingLogOrchestrator.Instance.LogInfo("DRAGON display session monitoring service stopped");
    }

    /// <summary>
    /// Gets comprehensive current session information
    /// </summary>
    public async Task<DisplaySessionInfo> GetCurrentSessionAsync()
    {
        // Return cached info if recent and caching enabled
        if (_options.CacheSessionInfo && 
            DateTime.UtcNow - _lastSessionCheck < _options.CacheDuration)
        {
            return _currentSession;
        }

        await RefreshSessionInfoAsync();
        return _currentSession;
    }

    /// <summary>
    /// Forces refresh of session detection
    /// </summary>
    public async Task RefreshSessionInfoAsync()
    {
        try
        {
            TradingLogOrchestrator.Instance.LogInfo("Refreshing DRAGON display session information");
            
            var previousSession = _currentSession;
            _currentSession = await DetectCurrentSessionAsync();
            _lastSessionCheck = DateTime.UtcNow;

            // Check for session changes
            if (HasSessionChanged(previousSession, _currentSession))
            {
                await NotifySessionChanged(previousSession, _currentSession);
            }

            TradingLogOrchestrator.Instance.LogInfo("Session detected: {SessionType} ({Description})", 
                _currentSession.SessionType, _currentSession.Description);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError(ex, "Failed to refresh session information");
        }
    }

    /// <summary>
    /// Checks if current session supports specific capability
    /// </summary>
    public bool SupportsCapability(string capability)
    {
        return capability.ToLowerInvariant() switch
        {
            "multi_monitor" => _currentSession.Capabilities.MaxDisplays > 1,
            "gpu_acceleration" => _currentSession.Capabilities.SupportsGpuAcceleration,
            "hot_plugging" => _currentSession.Capabilities.SupportsHotPlugging,
            "dynamic_configuration" => _currentSession.Capabilities.SupportsDynamicConfiguration,
            "dpi_scaling" => _currentSession.Capabilities.SupportsDpiScaling,
            "direct_hardware" => _currentSession.HasDirectHardwareAccess,
            _ => false
        };
    }

    /// <summary>
    /// Gets performance recommendations for current session
    /// </summary>
    public async Task<List<string>> GetPerformanceRecommendationsAsync()
    {
        await Task.CompletedTask;
        
        var recommendations = new List<string>();
        var session = _currentSession;

        switch (session.SessionType)
        {
            case DisplaySessionType.RemoteDesktop:
                recommendations.Add("RDP session detected - UI optimized for remote access");
                recommendations.Add("For multi-monitor trading, connect directly to DRAGON hardware");
                recommendations.Add("GPU acceleration limited - consider reducing chart complexity");
                break;

            case DisplaySessionType.DirectConsole:
                recommendations.Add("Direct hardware access available - full GPU acceleration enabled");
                recommendations.Add($"Up to {session.RecommendedMaxMonitors} monitors supported for optimal trading");
                recommendations.Add("Hardware acceleration available for high-frequency chart updates");
                break;

            case DisplaySessionType.VirtualMachine:
                recommendations.Add("Virtual machine detected - performance may be limited");
                recommendations.Add("Enable GPU passthrough for optimal trading performance");
                break;

            case DisplaySessionType.TerminalServices:
                recommendations.Add("Terminal Services session - shared resource environment");
                recommendations.Add("Monitor system resource usage during trading hours");
                break;
        }

        // Add performance limitations
        recommendations.AddRange(session.PerformanceLimitations);

        return recommendations;
    }

    #region Private Implementation

    /// <summary>
    /// Detects current session type and capabilities
    /// </summary>
    private async Task<DisplaySessionInfo> DetectCurrentSessionAsync()
    {
        await Task.CompletedTask;

        var sessionType = DetectSessionType();
        var sessionName = GetSessionName();
        var sessionId = GetSessionId();
        var remoteClient = await GetRemoteClientInfoAsync();
        var capabilities = DetermineCapabilities(sessionType, remoteClient);

        return new DisplaySessionInfo
        {
            SessionType = sessionType,
            IsRemoteSession = IsRemoteSessionType(sessionType),
            HasDirectHardwareAccess = sessionType == DisplaySessionType.DirectConsole,
            SessionName = sessionName,
            SessionId = sessionId,
            RemoteClient = remoteClient,
            Capabilities = capabilities,
            DetectedAt = DateTime.UtcNow,
            Description = GetSessionDescription(sessionType, sessionName),
            SupportsHardwareAcceleration = capabilities.SupportsGpuAcceleration,
            RecommendedMaxMonitors = GetRecommendedMaxMonitors(sessionType, capabilities),
            PerformanceLimitations = GetPerformanceLimitations(sessionType)
        };
    }

    /// <summary>
    /// Detects the current session type
    /// </summary>
    private DisplaySessionType DetectSessionType()
    {
        try
        {
            // Check Windows session name environment variable
            var sessionName = Environment.GetEnvironmentVariable("SESSIONNAME");
            
            if (string.IsNullOrEmpty(sessionName))
            {
                return DisplaySessionType.Unknown;
            }

            // Direct console session
            if (sessionName.Equals("Console", StringComparison.OrdinalIgnoreCase))
            {
                return DisplaySessionType.DirectConsole;
            }

            // RDP session
            if (sessionName.StartsWith("RDP-", StringComparison.OrdinalIgnoreCase))
            {
                return DisplaySessionType.RemoteDesktop;
            }

            // Terminal Services
            if (sessionName.StartsWith("ICA-", StringComparison.OrdinalIgnoreCase) ||
                sessionName.StartsWith("TS-", StringComparison.OrdinalIgnoreCase))
            {
                return DisplaySessionType.TerminalServices;
            }

            // Check for virtualization
            if (IsVirtualMachine())
            {
                return DisplaySessionType.VirtualMachine;
            }

            // Default to remote desktop for non-console sessions
            return DisplaySessionType.RemoteDesktop;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogWarning(ex, "Failed to detect session type, defaulting to Unknown");
            return DisplaySessionType.Unknown;
        }
    }

    /// <summary>
    /// Gets the current session name
    /// </summary>
    private string GetSessionName()
    {
        return Environment.GetEnvironmentVariable("SESSIONNAME") ?? "Unknown";
    }

    /// <summary>
    /// Gets the current session ID
    /// </summary>
    private int GetSessionId()
    {
        try
        {
            return (int)WTSGetActiveConsoleSessionId();
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Gets remote client information if applicable
    /// </summary>
    private async Task<RemoteClientInfo?> GetRemoteClientInfoAsync()
    {
        await Task.CompletedTask;

        var sessionType = DetectSessionType();
        if (!IsRemoteSessionType(sessionType))
        {
            return null;
        }

        try
        {
            var clientName = GetWtsSessionInfo(WtsInfoClass.WTSClientName);
            var clientAddress = GetWtsSessionInfo(WtsInfoClass.WTSClientAddress);
            
            return new RemoteClientInfo
            {
                ClientName = clientName ?? "Unknown",
                ClientAddress = clientAddress ?? "Unknown",
                Protocol = sessionType == DisplaySessionType.RemoteDesktop ? "RDP" : "Unknown",
                ClientResolution = new Resolution(1920, 1080), // Default, could be detected
                ColorDepth = 32,
                SupportsMultiMonitor = false // Limited in most remote sessions
            };
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogWarning(ex, "Failed to get remote client information");
            return null;
        }
    }

    /// <summary>
    /// Determines display capabilities for session type
    /// </summary>
    private DisplayCapabilities DetermineCapabilities(DisplaySessionType sessionType, RemoteClientInfo? remoteClient)
    {
        return sessionType switch
        {
            DisplaySessionType.DirectConsole => new DisplayCapabilities
            {
                MaxDisplays = 8, // Based on GPU capabilities
                MaxResolution = new Resolution(7680, 4320), // 8K support
                SupportsDynamicConfiguration = true,
                SupportsDpiScaling = true,
                SupportsGpuAcceleration = true,
                SupportsHotPlugging = true,
                PerformanceRating = SessionPerformanceRating.Excellent
            },
            
            DisplaySessionType.RemoteDesktop => new DisplayCapabilities
            {
                MaxDisplays = 1, // Limited in RDP
                MaxResolution = new Resolution(1920, 1080), // Conservative for RDP
                SupportsDynamicConfiguration = false,
                SupportsDpiScaling = true,
                SupportsGpuAcceleration = false,
                SupportsHotPlugging = false,
                PerformanceRating = SessionPerformanceRating.Adequate
            },
            
            DisplaySessionType.VirtualMachine => new DisplayCapabilities
            {
                MaxDisplays = 4, // VM dependent
                MaxResolution = new Resolution(2560, 1440),
                SupportsDynamicConfiguration = true,
                SupportsDpiScaling = true,
                SupportsGpuAcceleration = false, // Unless GPU passthrough
                SupportsHotPlugging = false,
                PerformanceRating = SessionPerformanceRating.Good
            },
            
            _ => new DisplayCapabilities
            {
                MaxDisplays = 1,
                MaxResolution = new Resolution(1920, 1080),
                SupportsDynamicConfiguration = false,
                SupportsDpiScaling = false,
                SupportsGpuAcceleration = false,
                SupportsHotPlugging = false,
                PerformanceRating = SessionPerformanceRating.Limited
            }
        };
    }

    /// <summary>
    /// Gets session description for UI display
    /// </summary>
    private string GetSessionDescription(DisplaySessionType sessionType, string sessionName)
    {
        return sessionType switch
        {
            DisplaySessionType.DirectConsole => "Direct hardware console access",
            DisplaySessionType.RemoteDesktop => $"Remote Desktop session ({sessionName})",
            DisplaySessionType.TerminalServices => $"Terminal Services session ({sessionName})",
            DisplaySessionType.VirtualMachine => "Virtual machine session",
            DisplaySessionType.RemoteApplication => "Remote application session",
            _ => $"Unknown session type ({sessionName})"
        };
    }

    /// <summary>
    /// Gets recommended maximum monitors for session type
    /// </summary>
    private int GetRecommendedMaxMonitors(DisplaySessionType sessionType, DisplayCapabilities capabilities)
    {
        return sessionType switch
        {
            DisplaySessionType.DirectConsole => 8, // Full GPU capability
            DisplaySessionType.VirtualMachine => 4, // VM limitation
            DisplaySessionType.RemoteDesktop => 1, // RDP limitation
            DisplaySessionType.TerminalServices => 2, // Conservative
            _ => 1
        };
    }

    /// <summary>
    /// Gets performance limitations for session type
    /// </summary>
    private List<string> GetPerformanceLimitations(DisplaySessionType sessionType)
    {
        return sessionType switch
        {
            DisplaySessionType.RemoteDesktop => new List<string>
            {
                "GPU acceleration not available",
                "Limited to single monitor",
                "Network latency may affect responsiveness",
                "High-frequency chart updates may be throttled"
            },
            
            DisplaySessionType.VirtualMachine => new List<string>
            {
                "GPU acceleration limited without passthrough",
                "Shared host resources may impact performance",
                "Monitor configuration changes require VM restart"
            },
            
            DisplaySessionType.TerminalServices => new List<string>
            {
                "Shared system resources",
                "Limited GPU acceleration",
                "Performance varies with system load"
            },
            
            _ => new List<string>()
        };
    }

    /// <summary>
    /// Checks if session type is remote
    /// </summary>
    private bool IsRemoteSessionType(DisplaySessionType sessionType)
    {
        return sessionType switch
        {
            DisplaySessionType.RemoteDesktop => true,
            DisplaySessionType.TerminalServices => true,
            DisplaySessionType.RemoteApplication => true,
            _ => false
        };
    }

    /// <summary>
    /// Detects if running in a virtual machine
    /// </summary>
    private bool IsVirtualMachine()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            foreach (ManagementObject system in searcher.Get())
            {
                var model = system["Model"]?.ToString()?.ToLowerInvariant();
                if (model != null && (model.Contains("virtual") || model.Contains("vmware") || model.Contains("virtualbox")))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore detection errors
        }
        return false;
    }

    /// <summary>
    /// Gets WTS session information
    /// </summary>
    private string? GetWtsSessionInfo(WtsInfoClass infoClass)
    {
        try
        {
            if (WTSQuerySessionInformation(IntPtr.Zero, -1, infoClass, out var buffer, out var bytesReturned))
            {
                try
                {
                    return Marshal.PtrToStringAnsi(buffer);
                }
                finally
                {
                    WTSFreeMemory(buffer);
                }
            }
        }
        catch
        {
            // Ignore WTS errors
        }
        return null;
    }

    /// <summary>
    /// Monitors for session changes
    /// </summary>
    private async Task MonitorSessionChanges()
    {
        var currentSessionInfo = await DetectCurrentSessionAsync();
        
        if (HasSessionChanged(_currentSession, currentSessionInfo))
        {
            var previousSession = _currentSession;
            _currentSession = currentSessionInfo;
            await NotifySessionChanged(previousSession, currentSessionInfo);
        }
    }

    /// <summary>
    /// Checks if session has changed
    /// </summary>
    private bool HasSessionChanged(DisplaySessionInfo previous, DisplaySessionInfo current)
    {
        return previous.SessionType != current.SessionType ||
               previous.SessionName != current.SessionName ||
               previous.SessionId != current.SessionId;
    }

    /// <summary>
    /// Notifies subscribers of session changes
    /// </summary>
    private async Task NotifySessionChanged(DisplaySessionInfo previous, DisplaySessionInfo current)
    {
        if (!_options.BroadcastSessionChanges) return;

        try
        {
            var args = new DisplaySessionChangedEventArgs
            {
                PreviousSession = previous,
                CurrentSession = current,
                ChangeTime = DateTime.UtcNow,
                AffectsTradingCapabilities = previous.Capabilities.PerformanceRating != current.Capabilities.PerformanceRating,
                RecommendedActions = await GetSessionChangeRecommendations(previous, current)
            };

            _sessionChangedSubject.OnNext(args);

            if (_options.LogSessionChanges)
            {
                TradingLogOrchestrator.Instance.LogInfo("Session changed from {PreviousType} to {CurrentType}", 
                    previous.SessionType, current.SessionType);
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError(ex, "Failed to notify session change");
        }
    }

    /// <summary>
    /// Gets recommendations for session changes
    /// </summary>
    private async Task<List<string>> GetSessionChangeRecommendations(DisplaySessionInfo previous, DisplaySessionInfo current)
    {
        await Task.CompletedTask;
        
        var recommendations = new List<string>();

        if (previous.SessionType == DisplaySessionType.DirectConsole && 
            current.SessionType == DisplaySessionType.RemoteDesktop)
        {
            recommendations.Add("Switched to RDP session - multi-monitor trading disabled");
            recommendations.Add("GPU acceleration unavailable - consider reducing chart complexity");
        }
        else if (previous.SessionType == DisplaySessionType.RemoteDesktop && 
                 current.SessionType == DisplaySessionType.DirectConsole)
        {
            recommendations.Add("Switched to direct console - full multi-monitor support enabled");
            recommendations.Add("GPU acceleration available - full chart performance restored");
        }

        return recommendations;
    }

    #endregion

    /// <summary>
    /// Dispose of resources
    /// </summary>
    public override void Dispose()
    {
        _sessionChangedSubject?.Dispose();
        base.Dispose();
    }
}