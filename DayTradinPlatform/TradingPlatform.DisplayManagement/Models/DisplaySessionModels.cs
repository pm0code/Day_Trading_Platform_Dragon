using System.Runtime.InteropServices;

namespace TradingPlatform.DisplayManagement.Models;

/// <summary>
/// Represents the current display session type for DRAGON trading platform
/// </summary>
public enum DisplaySessionType
{
    /// <summary>
    /// Unknown or undetected session type
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Direct console session on physical hardware
    /// </summary>
    DirectConsole = 1,

    /// <summary>
    /// Remote Desktop Protocol session
    /// </summary>
    RemoteDesktop = 2,

    /// <summary>
    /// Terminal Services session
    /// </summary>
    TerminalServices = 3,

    /// <summary>
    /// Virtual machine session
    /// </summary>
    VirtualMachine = 4,

    /// <summary>
    /// Citrix or similar remote application session
    /// </summary>
    RemoteApplication = 5
}

/// <summary>
/// Comprehensive display session information for DRAGON system
/// </summary>
public record DisplaySessionInfo
{
    /// <summary>
    /// Current session type
    /// </summary>
    public DisplaySessionType SessionType { get; init; } = DisplaySessionType.Unknown;

    /// <summary>
    /// Whether this is a remote session (RDP, Terminal Services, etc.)
    /// </summary>
    public bool IsRemoteSession { get; init; }

    /// <summary>
    /// Whether direct hardware access is available
    /// </summary>
    public bool HasDirectHardwareAccess { get; init; }

    /// <summary>
    /// Session identifier (Console, RDP-Tcp#0, etc.)
    /// </summary>
    public string SessionName { get; init; } = string.Empty;

    /// <summary>
    /// Session ID number
    /// </summary>
    public int SessionId { get; init; }

    /// <summary>
    /// Remote client information (if applicable)
    /// </summary>
    public RemoteClientInfo? RemoteClient { get; init; }

    /// <summary>
    /// Display capabilities in current session
    /// </summary>
    public DisplayCapabilities Capabilities { get; init; } = new();

    /// <summary>
    /// When this session information was detected
    /// </summary>
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// User-friendly description of the session
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Whether GPU hardware acceleration is available
    /// </summary>
    public bool SupportsHardwareAcceleration { get; init; }

    /// <summary>
    /// Maximum recommended monitors for this session type
    /// </summary>
    public int RecommendedMaxMonitors { get; init; } = 1;

    /// <summary>
    /// Performance limitations in this session
    /// </summary>
    public List<string> PerformanceLimitations { get; init; } = new();
}

/// <summary>
/// Remote client connection information
/// </summary>
public record RemoteClientInfo
{
    /// <summary>
    /// Client computer name or IP address
    /// </summary>
    public string ClientName { get; init; } = string.Empty;

    /// <summary>
    /// Client IP address
    /// </summary>
    public string ClientAddress { get; init; } = string.Empty;

    /// <summary>
    /// Connection protocol (RDP, ICA, etc.)
    /// </summary>
    public string Protocol { get; init; } = string.Empty;

    /// <summary>
    /// Client display resolution
    /// </summary>
    public Resolution ClientResolution { get; init; } = new(1920, 1080);

    /// <summary>
    /// Color depth supported by client
    /// </summary>
    public int ColorDepth { get; init; } = 32;

    /// <summary>
    /// Whether client supports multiple monitors
    /// </summary>
    public bool SupportsMultiMonitor { get; init; }
}

/// <summary>
/// Display capabilities for current session
/// </summary>
public record DisplayCapabilities
{
    /// <summary>
    /// Maximum number of displays supported in current session
    /// </summary>
    public int MaxDisplays { get; init; } = 1;

    /// <summary>
    /// Maximum resolution supported
    /// </summary>
    public Resolution MaxResolution { get; init; } = new(1920, 1080);

    /// <summary>
    /// Whether dynamic display configuration is supported
    /// </summary>
    public bool SupportsDynamicConfiguration { get; init; }

    /// <summary>
    /// Whether DPI scaling is available
    /// </summary>
    public bool SupportsDpiScaling { get; init; }

    /// <summary>
    /// Whether GPU acceleration is available for UI rendering
    /// </summary>
    public bool SupportsGpuAcceleration { get; init; }

    /// <summary>
    /// Whether monitor hot-plugging is supported
    /// </summary>
    public bool SupportsHotPlugging { get; init; }

    /// <summary>
    /// Performance rating for trading applications
    /// </summary>
    public SessionPerformanceRating PerformanceRating { get; init; } = SessionPerformanceRating.Limited;
}



/// <summary>
/// Performance rating for different session types
/// </summary>
public enum SessionPerformanceRating
{
    /// <summary>
    /// Severely limited performance (basic remote sessions)
    /// </summary>
    Limited = 1,

    /// <summary>
    /// Adequate performance for basic trading (optimized RDP)
    /// </summary>
    Adequate = 2,

    /// <summary>
    /// Good performance (local VM or high-end remote)
    /// </summary>
    Good = 3,

    /// <summary>
    /// Excellent performance (direct hardware access)
    /// </summary>
    Excellent = 4
}

/// <summary>
/// Event arguments for display session changes
/// </summary>
public class DisplaySessionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Previous session information
    /// </summary>
    public DisplaySessionInfo? PreviousSession { get; init; }

    /// <summary>
    /// Current session information
    /// </summary>
    public DisplaySessionInfo CurrentSession { get; init; } = new();

    /// <summary>
    /// When the session change occurred
    /// </summary>
    public DateTime ChangeTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this change affects trading capabilities
    /// </summary>
    public bool AffectsTradingCapabilities { get; init; }

    /// <summary>
    /// Recommended actions for the session change
    /// </summary>
    public List<string> RecommendedActions { get; init; } = new();
}

/// <summary>
/// Configuration options for display session management
/// </summary>
public record DisplaySessionOptions
{
    /// <summary>
    /// How frequently to check for session changes (in seconds)
    /// </summary>
    public int MonitoringIntervalSeconds { get; init; } = 30;

    /// <summary>
    /// Whether to automatically adapt UI for session type
    /// </summary>
    public bool AutoAdaptUI { get; init; } = true;

    /// <summary>
    /// Whether to cache session information
    /// </summary>
    public bool CacheSessionInfo { get; init; } = true;

    /// <summary>
    /// Cache duration for session information
    /// </summary>
    public TimeSpan CacheDuration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to log session changes
    /// </summary>
    public bool LogSessionChanges { get; init; } = true;

    /// <summary>
    /// Whether to broadcast session changes to other services
    /// </summary>
    public bool BroadcastSessionChanges { get; init; } = true;
}
