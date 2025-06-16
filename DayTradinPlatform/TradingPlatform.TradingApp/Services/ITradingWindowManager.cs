using Microsoft.UI.Xaml;
using TradingPlatform.TradingApp.Models;

namespace TradingPlatform.TradingApp.Services;

/// <summary>
/// Service interface for managing trading windows across multiple monitors
/// </summary>
public interface ITradingWindowManager
{
    /// <summary>
    /// Initializes the window manager with monitor service
    /// </summary>
    /// <returns>Initialization task</returns>
    Task InitializeAsync();

    /// <summary>
    /// Opens a trading screen window on its assigned monitor
    /// </summary>
    /// <param name="screenType">Type of trading screen to open</param>
    /// <returns>True if window was opened successfully</returns>
    Task<bool> OpenTradingScreenAsync(TradingScreenType screenType);

    /// <summary>
    /// Closes a trading screen window
    /// </summary>
    /// <param name="screenType">Type of trading screen to close</param>
    /// <returns>True if window was closed successfully</returns>
    Task<bool> CloseTradingScreenAsync(TradingScreenType screenType);

    /// <summary>
    /// Opens all configured trading screens
    /// </summary>
    /// <returns>Number of windows successfully opened</returns>
    Task<int> OpenAllTradingScreensAsync();

    /// <summary>
    /// Closes all trading screen windows
    /// </summary>
    /// <returns>Number of windows successfully closed</returns>
    Task<int> CloseAllTradingScreensAsync();

    /// <summary>
    /// Restores window positions from saved configuration
    /// </summary>
    /// <param name="screenType">Specific screen type or null for all screens</param>
    /// <returns>True if positions were restored successfully</returns>
    Task<bool> RestoreWindowPositionsAsync(TradingScreenType? screenType = null);

    /// <summary>
    /// Saves current window positions for all open screens
    /// </summary>
    /// <returns>Number of window positions saved</returns>
    Task<int> SaveAllWindowPositionsAsync();

    /// <summary>
    /// Gets the window instance for a specific trading screen
    /// </summary>
    /// <param name="screenType">Trading screen type</param>
    /// <returns>Window instance or null if not open</returns>
    Window? GetTradingWindow(TradingScreenType screenType);

    /// <summary>
    /// Checks if a trading screen window is currently open
    /// </summary>
    /// <param name="screenType">Trading screen type</param>
    /// <returns>True if window is open</returns>
    bool IsScreenOpen(TradingScreenType screenType);

    /// <summary>
    /// Gets the list of currently open trading screens
    /// </summary>
    /// <returns>List of open screen types</returns>
    List<TradingScreenType> GetOpenScreens();

    /// <summary>
    /// Moves a trading screen to a different monitor
    /// </summary>
    /// <param name="screenType">Trading screen type</param>
    /// <param name="targetMonitorId">Target monitor identifier</param>
    /// <returns>True if move was successful</returns>
    Task<bool> MoveTradingScreenToMonitorAsync(TradingScreenType screenType, string targetMonitorId);

    /// <summary>
    /// Arranges all trading windows in optimal layout for day trading
    /// </summary>
    /// <returns>True if arrangement was successful</returns>
    Task<bool> ArrangeForDayTradingAsync();

    /// <summary>
    /// Event raised when a trading window is opened
    /// </summary>
    event EventHandler<TradingWindowEventArgs>? WindowOpened;

    /// <summary>
    /// Event raised when a trading window is closed
    /// </summary>
    event EventHandler<TradingWindowEventArgs>? WindowClosed;

    /// <summary>
    /// Event raised when a window position is changed
    /// </summary>
    event EventHandler<TradingWindowEventArgs>? WindowMoved;
}

/// <summary>
/// Event arguments for trading window events
/// </summary>
public class TradingWindowEventArgs : EventArgs
{
    public TradingScreenType ScreenType { get; }
    public Window? Window { get; }
    public string? MonitorId { get; }
    public bool Success { get; }
    public string? ErrorMessage { get; }

    public TradingWindowEventArgs(TradingScreenType screenType, Window? window = null, 
        string? monitorId = null, bool success = true, string? errorMessage = null)
    {
        ScreenType = screenType;
        Window = window;
        MonitorId = monitorId;
        Success = success;
        ErrorMessage = errorMessage;
    }
}