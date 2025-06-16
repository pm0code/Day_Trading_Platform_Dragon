using System.Text.Json.Serialization;

namespace TradingPlatform.TradingApp.Models;

/// <summary>
/// Configuration for individual monitor/screen setup
/// </summary>
public record MonitorConfiguration
{
    /// <summary>
    /// Unique identifier for this monitor
    /// </summary>
    public string MonitorId { get; init; } = string.Empty;

    /// <summary>
    /// Display name for this monitor
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// X coordinate of the monitor
    /// </summary>
    public int X { get; init; }

    /// <summary>
    /// Y coordinate of the monitor
    /// </summary>
    public int Y { get; init; }

    /// <summary>
    /// Width of the monitor in pixels
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Height of the monitor in pixels
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Whether this is the primary monitor
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// DPI scale factor for this monitor
    /// </summary>
    public double DpiScale { get; init; } = 1.0;

    /// <summary>
    /// Assigned trading screen for this monitor
    /// </summary>
    public TradingScreenType? AssignedScreen { get; set; }

    /// <summary>
    /// Whether this monitor is currently active/enabled
    /// </summary>
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// Types of trading screens for the 4-screen day trading setup
/// </summary>
public enum TradingScreenType
{
    /// <summary>
    /// Primary charting and technical analysis screen
    /// </summary>
    PrimaryCharting = 1,

    /// <summary>
    /// Order execution and Level II market depth screen
    /// </summary>
    OrderExecution = 2,

    /// <summary>
    /// Portfolio P&L and risk management screen
    /// </summary>
    PortfolioRisk = 3,

    /// <summary>
    /// Market scanner and news feed screen
    /// </summary>
    MarketScanner = 4
}

/// <summary>
/// Complete multi-monitor trading configuration
/// </summary>
public record MultiMonitorConfiguration
{
    /// <summary>
    /// List of all detected monitors
    /// </summary>
    public List<MonitorConfiguration> Monitors { get; init; } = [];

    /// <summary>
    /// Default screen assignments for 4-screen setup
    /// </summary>
    public Dictionary<TradingScreenType, string> ScreenAssignments { get; init; } = new();

    /// <summary>
    /// Configuration version for migration support
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// When this configuration was last updated
    /// </summary>
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether to automatically assign screens to new monitors
    /// </summary>
    public bool AutoAssignScreens { get; init; } = true;

    /// <summary>
    /// Whether screens should remember their positions when windows are closed/reopened
    /// </summary>
    public bool RememberWindowPositions { get; init; } = true;
}

/// <summary>
/// Window position and state information for screen memory
/// </summary>
public record WindowPositionInfo
{
    /// <summary>
    /// Trading screen type this window represents
    /// </summary>
    public TradingScreenType ScreenType { get; init; }

    /// <summary>
    /// Monitor ID where this window is positioned
    /// </summary>
    public string MonitorId { get; init; } = string.Empty;

    /// <summary>
    /// X coordinate within the monitor
    /// </summary>
    public int X { get; init; }

    /// <summary>
    /// Y coordinate within the monitor
    /// </summary>
    public int Y { get; init; }

    /// <summary>
    /// Window width
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Window height
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Whether window is maximized
    /// </summary>
    public bool IsMaximized { get; init; }

    /// <summary>
    /// Whether window is minimized
    /// </summary>
    public bool IsMinimized { get; init; }

    /// <summary>
    /// Window state (Normal, Maximized, Minimized)
    /// </summary>
    public WindowState WindowState { get; init; } = WindowState.Normal;

    /// <summary>
    /// When this position was last saved
    /// </summary>
    public DateTime LastSaved { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Window state enumeration
/// </summary>
public enum WindowState
{
    Normal = 0,
    Minimized = 1,
    Maximized = 2
}