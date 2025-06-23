using Microsoft.UI.Xaml;
using TradingPlatform.Logging.Interfaces;
using TradingPlatform.TradingApp.Models;
using TradingPlatform.TradingApp.Views.TradingScreens;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.TradingApp.Services;

/// <summary>
/// Manages trading windows across multiple monitors with position memory
/// </summary>
public class TradingWindowManager : ITradingWindowManager
{
    private readonly Core.Interfaces.ITradingLogger _logger;
    private readonly IMonitorService _monitorService;
    private readonly Dictionary<TradingScreenType, Window> _openWindows = new();
    private readonly SemaphoreSlim _windowLock = new(1, 1);

    public event EventHandler<TradingWindowEventArgs>? WindowOpened;
    public event EventHandler<TradingWindowEventArgs>? WindowClosed;
    public event EventHandler<TradingWindowEventArgs>? WindowMoved;

    public TradingWindowManager(Core.Interfaces.ITradingLogger logger, IMonitorService monitorService)
    {
        _logger = logger;
        _monitorService = monitorService;
        
        TradingLogOrchestrator.Instance.LogInfo("Trading window manager initialized", null);
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Ensure monitor configuration is loaded
            var config = await _monitorService.GetConfigurationAsync();
            
            // Auto-configure if no assignments exist
            var (isValid, missingAssignments) = await _monitorService.ValidateScreenAssignmentsAsync();
            if (!isValid)
            {
                TradingLogOrchestrator.Instance.LogInfo("Auto-configuring monitor assignments for day trading", null, new Dictionary<string, object>
                {
                    ["MissingAssignments"] = missingAssignments.Count
                });
                
                await _monitorService.AutoConfigureForDayTradingAsync();
            }
            
            TradingLogOrchestrator.Instance.LogInfo("Trading window manager initialization completed", null);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to initialize trading window manager", null, ex);
            throw;
        }
    }

    public async Task<bool> OpenTradingScreenAsync(TradingScreenType screenType)
    {
        await _windowLock.WaitAsync();
        try
        {
            // Check if window is already open
            if (_openWindows.ContainsKey(screenType))
            {
                TradingLogOrchestrator.Instance.LogWarning("Trading screen already open", screenType.ToString());
                return false;
            }

            // Get assigned monitor
            var assignedMonitor = await _monitorService.GetAssignedMonitorAsync(screenType);
            if (assignedMonitor == null)
            {
                TradingLogOrchestrator.Instance.LogError("No monitor assigned for trading screen", screenType.ToString());
                WindowOpened?.Invoke(this, new TradingWindowEventArgs(screenType, null, null, false, "No monitor assigned"));
                return false;
            }

            // Create the appropriate window
            Window? window = screenType switch
            {
                TradingScreenType.PrimaryCharting => new PrimaryChartingScreen(_logger, _monitorService),
                TradingScreenType.OrderExecution => new OrderExecutionScreen(_logger, _monitorService),
                TradingScreenType.PortfolioRisk => new PortfolioRiskScreen(_logger, _monitorService),
                TradingScreenType.MarketScanner => new MarketScannerScreen(_logger, _monitorService),
                _ => null
            };

            if (window == null)
            {
                TradingLogOrchestrator.Instance.LogError("Failed to create window for trading screen", screenType.ToString());
                WindowOpened?.Invoke(this, new TradingWindowEventArgs(screenType, null, assignedMonitor.MonitorId, false, "Failed to create window"));
                return false;
            }

            // Position window on assigned monitor
            await PositionWindowOnMonitorAsync(window, assignedMonitor, screenType);

            // Track the window
            _openWindows[screenType] = window;

            // Subscribe to window closed event
            window.Closed += (sender, args) => OnWindowClosed(screenType);

            // Show the window
            window.Activate();

            TradingLogOrchestrator.Instance.LogInfo("Trading screen opened successfully", screenType.ToString(), new Dictionary<string, object>
            {
                ["MonitorId"] = assignedMonitor.MonitorId,
                ["MonitorPosition"] = $"({assignedMonitor.X}, {assignedMonitor.Y})"
            });

            WindowOpened?.Invoke(this, new TradingWindowEventArgs(screenType, window, assignedMonitor.MonitorId, true));
            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to open trading screen", screenType.ToString(), ex);
            WindowOpened?.Invoke(this, new TradingWindowEventArgs(screenType, null, null, false, ex.Message));
            return false;
        }
        finally
        {
            _windowLock.Release();
        }
    }

    public async Task<bool> CloseTradingScreenAsync(TradingScreenType screenType)
    {
        await _windowLock.WaitAsync();
        try
        {
            if (!_openWindows.TryGetValue(screenType, out var window))
            {
                TradingLogOrchestrator.Instance.LogWarning("Trading screen not open", screenType.ToString());
                return false;
            }

            // Save window position before closing
            await SaveWindowPositionAsync(window, screenType);

            // Close the window
            window.Close();
            _openWindows.Remove(screenType);

            TradingLogOrchestrator.Instance.LogInfo("Trading screen closed successfully", screenType.ToString());
            WindowClosed?.Invoke(this, new TradingWindowEventArgs(screenType, window, null, true));
            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to close trading screen", screenType.ToString(), ex);
            WindowClosed?.Invoke(this, new TradingWindowEventArgs(screenType, null, null, false, ex.Message));
            return false;
        }
        finally
        {
            _windowLock.Release();
        }
    }

    public async Task<int> OpenAllTradingScreensAsync()
    {
        TradingLogOrchestrator.Instance.LogInfo("Opening all trading screens", null);
        
        var successCount = 0;
        var allScreens = new[] 
        { 
            TradingScreenType.PrimaryCharting, 
            TradingScreenType.OrderExecution, 
            TradingScreenType.PortfolioRisk, 
            TradingScreenType.MarketScanner 
        };

        foreach (var screenType in allScreens)
        {
            if (await OpenTradingScreenAsync(screenType))
            {
                successCount++;
                // Small delay between window opens to prevent visual artifacts
                await Task.Delay(200);
            }
        }

        TradingLogOrchestrator.Instance.LogInfo("Opened trading screens", null, new Dictionary<string, object>
        {
            ["SuccessCount"] = successCount,
            ["TotalScreens"] = allScreens.Length
        });

        return successCount;
    }

    public async Task<int> CloseAllTradingScreensAsync()
    {
        TradingLogOrchestrator.Instance.LogInfo("Closing all trading screens", null);
        
        var successCount = 0;
        var openScreens = _openWindows.Keys.ToList();

        foreach (var screenType in openScreens)
        {
            if (await CloseTradingScreenAsync(screenType))
            {
                successCount++;
            }
        }

        TradingLogOrchestrator.Instance.LogInfo("Closed trading screens", null, new Dictionary<string, object>
        {
            ["SuccessCount"] = successCount,
            ["TotalScreens"] = openScreens.Count
        });

        return successCount;
    }

    public async Task<bool> RestoreWindowPositionsAsync(TradingScreenType? screenType = null)
    {
        try
        {
            if (screenType.HasValue)
            {
                // Restore specific screen
                if (_openWindows.TryGetValue(screenType.Value, out var window))
                {
                    var savedPosition = await _monitorService.GetSavedWindowPositionAsync(screenType.Value);
                    if (savedPosition != null)
                    {
                        await RestoreWindowPositionAsync(window, savedPosition);
                        return true;
                    }
                }
                return false;
            }
            else
            {
                // Restore all open screens
                var restoredCount = 0;
                foreach (var kvp in _openWindows)
                {
                    var savedPosition = await _monitorService.GetSavedWindowPositionAsync(kvp.Key);
                    if (savedPosition != null)
                    {
                        await RestoreWindowPositionAsync(kvp.Value, savedPosition);
                        restoredCount++;
                    }
                }
                
                TradingLogOrchestrator.Instance.LogInfo("Restored window positions", null, new Dictionary<string, object>
                {
                    ["RestoredCount"] = restoredCount,
                    ["TotalWindows"] = _openWindows.Count
                });
                
                return restoredCount > 0;
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to restore window positions", screenType?.ToString() ?? "All", ex);
            return false;
        }
    }

    public async Task<int> SaveAllWindowPositionsAsync()
    {
        var savedCount = 0;
        
        foreach (var kvp in _openWindows)
        {
            try
            {
                await SaveWindowPositionAsync(kvp.Value, kvp.Key);
                savedCount++;
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError("Failed to save window position", kvp.Key.ToString(), ex);
            }
        }
        
        TradingLogOrchestrator.Instance.LogInfo("Saved window positions", null, new Dictionary<string, object>
        {
            ["SavedCount"] = savedCount,
            ["TotalWindows"] = _openWindows.Count
        });
        
        return savedCount;
    }

    public Window? GetTradingWindow(TradingScreenType screenType)
    {
        return _openWindows.GetValueOrDefault(screenType);
    }

    public bool IsScreenOpen(TradingScreenType screenType)
    {
        return _openWindows.ContainsKey(screenType);
    }

    public List<TradingScreenType> GetOpenScreens()
    {
        return _openWindows.Keys.ToList();
    }

    public async Task<bool> MoveTradingScreenToMonitorAsync(TradingScreenType screenType, string targetMonitorId)
    {
        try
        {
            if (!_openWindows.TryGetValue(screenType, out var window))
            {
                TradingLogOrchestrator.Instance.LogWarning("Cannot move non-open trading screen", screenType.ToString());
                return false;
            }

            // Update monitor assignment
            await _monitorService.AssignScreenToMonitorAsync(screenType, targetMonitorId);

            // Get target monitor configuration
            var config = await _monitorService.GetConfigurationAsync();
            var targetMonitor = config.Monitors.FirstOrDefault(m => m.MonitorId == targetMonitorId);
            
            if (targetMonitor == null)
            {
                TradingLogOrchestrator.Instance.LogError("Target monitor not found", screenType.ToString(), new Dictionary<string, object>
                {
                    ["TargetMonitorId"] = targetMonitorId
                });
                return false;
            }

            // Move window to target monitor
            await PositionWindowOnMonitorAsync(window, targetMonitor, screenType);

            TradingLogOrchestrator.Instance.LogInfo("Moved trading screen to monitor", screenType.ToString(), new Dictionary<string, object>
            {
                ["TargetMonitorId"] = targetMonitorId,
                ["MonitorPosition"] = $"({targetMonitor.X}, {targetMonitor.Y})"
            });

            WindowMoved?.Invoke(this, new TradingWindowEventArgs(screenType, window, targetMonitorId, true));
            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to move trading screen to monitor", screenType.ToString(), ex);
            WindowMoved?.Invoke(this, new TradingWindowEventArgs(screenType, null, targetMonitorId, false, ex.Message));
            return false;
        }
    }

    public async Task<bool> ArrangeForDayTradingAsync()
    {
        try
        {
            TradingLogOrchestrator.Instance.LogInfo("Arranging windows for optimal day trading layout", null);

            // Close all windows first
            await CloseAllTradingScreensAsync();
            
            // Wait a moment for windows to close
            await Task.Delay(500);

            // Open all windows in optimal order for day trading
            var arrangedCount = await OpenAllTradingScreensAsync();

            var success = arrangedCount == 4;
            
            TradingLogOrchestrator.Instance.LogInfo("Day trading arrangement completed", null, new Dictionary<string, object>
            {
                ["Success"] = success,
                ["ArrangedWindows"] = arrangedCount
            });

            return success;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to arrange windows for day trading", null, ex);
            return false;
        }
    }

    private async Task PositionWindowOnMonitorAsync(Window window, MonitorConfiguration monitor, TradingScreenType screenType)
    {
        try
        {
            var appWindow = window.AppWindow;
            if (appWindow != null)
            {
                // Try to restore saved position first
                var savedPosition = await _monitorService.GetSavedWindowPositionAsync(screenType);
                
                if (savedPosition != null && savedPosition.MonitorId == monitor.MonitorId)
                {
                    // Restore exact saved position
                    var position = new Windows.Graphics.PointInt32(
                        monitor.X + savedPosition.X,
                        monitor.Y + savedPosition.Y
                    );
                    
                    var size = new Windows.Graphics.SizeInt32(
                        savedPosition.Width,
                        savedPosition.Height
                    );
                    
                    appWindow.Move(position);
                    appWindow.Resize(size);
                }
                else
                {
                    // Use default positioning based on screen type
                    var (defaultPos, defaultSize) = GetDefaultWindowLayout(screenType, monitor);
                    appWindow.Move(defaultPos);
                    appWindow.Resize(defaultSize);
                }
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to position window on monitor", screenType.ToString(), ex);
        }
    }

    private (Windows.Graphics.PointInt32 Position, Windows.Graphics.SizeInt32 Size) GetDefaultWindowLayout(
        TradingScreenType screenType, MonitorConfiguration monitor)
    {
        // Default positions and sizes for each screen type
        return screenType switch
        {
            TradingScreenType.PrimaryCharting => (
                new Windows.Graphics.PointInt32(monitor.X, monitor.Y),
                new Windows.Graphics.SizeInt32(Math.Min(1920, monitor.Width), Math.Min(1080, monitor.Height))
            ),
            TradingScreenType.OrderExecution => (
                new Windows.Graphics.PointInt32(monitor.X, monitor.Y),
                new Windows.Graphics.SizeInt32(Math.Min(800, monitor.Width), Math.Min(1080, monitor.Height))
            ),
            TradingScreenType.PortfolioRisk => (
                new Windows.Graphics.PointInt32(monitor.X, monitor.Y),
                new Windows.Graphics.SizeInt32(Math.Min(800, monitor.Width), Math.Min(1080, monitor.Height))
            ),
            TradingScreenType.MarketScanner => (
                new Windows.Graphics.PointInt32(monitor.X, monitor.Y),
                new Windows.Graphics.SizeInt32(Math.Min(1200, monitor.Width), Math.Min(1080, monitor.Height))
            ),
            _ => (
                new Windows.Graphics.PointInt32(monitor.X, monitor.Y),
                new Windows.Graphics.SizeInt32(800, 600)
            )
        };
    }

    private async Task SaveWindowPositionAsync(Window window, TradingScreenType screenType)
    {
        try
        {
            var appWindow = window.AppWindow;
            if (appWindow != null)
            {
                var assignedMonitor = await _monitorService.GetAssignedMonitorAsync(screenType);
                if (assignedMonitor != null)
                {
                    var position = appWindow.Position;
                    var size = appWindow.Size;
                    
                    var relativeX = position.X - assignedMonitor.X;
                    var relativeY = position.Y - assignedMonitor.Y;
                    
                    var positionInfo = new WindowPositionInfo
                    {
                        ScreenType = screenType,
                        MonitorId = assignedMonitor.MonitorId,
                        X = relativeX,
                        Y = relativeY,
                        Width = size.Width,
                        Height = size.Height,
                        WindowState = WindowState.Normal, // TODO: Detect actual window state
                        LastSaved = DateTime.UtcNow
                    };
                    
                    await _monitorService.SaveWindowPositionAsync(positionInfo);
                }
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to save window position", screenType.ToString(), ex);
        }
    }

    private async Task RestoreWindowPositionAsync(Window window, WindowPositionInfo savedPosition)
    {
        try
        {
            var config = await _monitorService.GetConfigurationAsync();
            var monitor = config.Monitors.FirstOrDefault(m => m.MonitorId == savedPosition.MonitorId);
            
            if (monitor != null)
            {
                var appWindow = window.AppWindow;
                if (appWindow != null)
                {
                    var position = new Windows.Graphics.PointInt32(
                        monitor.X + savedPosition.X,
                        monitor.Y + savedPosition.Y
                    );
                    
                    var size = new Windows.Graphics.SizeInt32(
                        savedPosition.Width,
                        savedPosition.Height
                    );
                    
                    appWindow.Move(position);
                    appWindow.Resize(size);
                }
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to restore window position", savedPosition.ScreenType.ToString(), ex);
        }
    }

    private void OnWindowClosed(TradingScreenType screenType)
    {
        _openWindows.Remove(screenType);
        TradingLogOrchestrator.Instance.LogInfo("Trading window closed and removed from tracking", screenType.ToString());
        WindowClosed?.Invoke(this, new TradingWindowEventArgs(screenType, null, null, true));
    }
}