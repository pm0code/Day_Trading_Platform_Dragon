using TradingPlatform.TradingApp.Models;

namespace TradingPlatform.TradingApp.Services;

/// <summary>
/// Service interface for managing multi-monitor configurations and screen positions
/// </summary>
public interface IMonitorService
{
    /// <summary>
    /// Detects all available monitors and their configurations
    /// </summary>
    /// <returns>List of detected monitor configurations</returns>
    Task<List<MonitorConfiguration>> DetectMonitorsAsync();

    /// <summary>
    /// Gets the current multi-monitor configuration
    /// </summary>
    /// <returns>Current monitor configuration</returns>
    Task<MultiMonitorConfiguration> GetConfigurationAsync();

    /// <summary>
    /// Saves the multi-monitor configuration
    /// </summary>
    /// <param name="configuration">Configuration to save</param>
    Task SaveConfigurationAsync(MultiMonitorConfiguration configuration);

    /// <summary>
    /// Assigns a trading screen to a specific monitor
    /// </summary>
    /// <param name="screenType">Type of trading screen</param>
    /// <param name="monitorId">Monitor identifier</param>
    Task AssignScreenToMonitorAsync(TradingScreenType screenType, string monitorId);

    /// <summary>
    /// Gets the monitor assigned to a specific trading screen
    /// </summary>
    /// <param name="screenType">Trading screen type</param>
    /// <returns>Monitor configuration or null if not assigned</returns>
    Task<MonitorConfiguration?> GetAssignedMonitorAsync(TradingScreenType screenType);

    /// <summary>
    /// Saves window position for screen memory functionality
    /// </summary>
    /// <param name="positionInfo">Window position information</param>
    Task SaveWindowPositionAsync(WindowPositionInfo positionInfo);

    /// <summary>
    /// Retrieves saved window position for a trading screen
    /// </summary>
    /// <param name="screenType">Trading screen type</param>
    /// <returns>Saved window position or null if not found</returns>
    Task<WindowPositionInfo?> GetSavedWindowPositionAsync(TradingScreenType screenType);

    /// <summary>
    /// Automatically configures monitors for 4-screen day trading setup
    /// </summary>
    /// <returns>True if configuration was successful</returns>
    Task<bool> AutoConfigureForDayTradingAsync();

    /// <summary>
    /// Validates that all required screens have monitor assignments
    /// </summary>
    /// <returns>Validation result with missing assignments</returns>
    Task<(bool IsValid, List<TradingScreenType> MissingAssignments)> ValidateScreenAssignmentsAsync();

    /// <summary>
    /// Event raised when monitor configuration changes
    /// </summary>
    event EventHandler<MultiMonitorConfiguration>? ConfigurationChanged;

    /// <summary>
    /// Event raised when a window position is saved
    /// </summary>
    event EventHandler<WindowPositionInfo>? WindowPositionSaved;
}