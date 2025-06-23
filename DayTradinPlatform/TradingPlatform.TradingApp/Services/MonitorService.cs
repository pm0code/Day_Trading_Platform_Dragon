using Microsoft.UI.Windowing;
using System.Text.Json;
using TradingPlatform.Logging.Interfaces;
using TradingPlatform.TradingApp.Models;
using Windows.Graphics;
using WinRT.Interop;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.TradingApp.Services;

/// <summary>
/// Service for managing multi-monitor configurations and screen position memory
/// </summary>
public class MonitorService : IMonitorService
{
    private readonly Core.Interfaces.ITradingLogger _logger;
    private readonly string _configFilePath;
    private readonly string _positionsFilePath;
    private MultiMonitorConfiguration? _currentConfiguration;
    private readonly Dictionary<TradingScreenType, WindowPositionInfo> _windowPositions = new();
    private readonly SemaphoreSlim _configLock = new(1, 1);

    public event EventHandler<MultiMonitorConfiguration>? ConfigurationChanged;
    public event EventHandler<WindowPositionInfo>? WindowPositionSaved;

    public MonitorService(Core.Interfaces.ITradingLogger logger)
    {
        _logger = logger;
        
        // Store configuration in user's AppData folder
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appDataPath, "TradingPlatform", "Configuration");
        Directory.CreateDirectory(configDir);
        
        _configFilePath = Path.Combine(configDir, "monitor-configuration.json");
        _positionsFilePath = Path.Combine(configDir, "window-positions.json");
        
        TradingLogOrchestrator.Instance.LogInfo("MonitorService initialized", null, new Dictionary<string, object>
        {
            ["ConfigPath"] = _configFilePath,
            ["PositionsPath"] = _positionsFilePath
        });
    }

    public async Task<List<MonitorConfiguration>> DetectMonitorsAsync()
    {
        TradingLogOrchestrator.Instance.LogInfo("Detecting available monitors", null);
        
        var monitors = new List<MonitorConfiguration>();
        
        try
        {
            // Get all display areas using WinUI 3 DisplayArea API
            var displayAreas = DisplayArea.FindAll();
            
            for (int i = 0; i < displayAreas.Count; i++)
            {
                var displayArea = displayAreas[i];
                var workArea = displayArea.WorkArea;
                
                var monitor = new MonitorConfiguration
                {
                    MonitorId = $"Monitor_{i}_{workArea.X}_{workArea.Y}",
                    DisplayName = $"Display {i + 1}",
                    X = workArea.X,
                    Y = workArea.Y,
                    Width = workArea.Width,
                    Height = workArea.Height,
                    IsPrimary = i == 0, // First monitor is typically primary
                    DpiScale = 1.0, // WinUI handles DPI automatically
                    IsActive = true
                };
                
                monitors.Add(monitor);
                
                TradingLogOrchestrator.Instance.LogInfo("Detected monitor", null, new Dictionary<string, object>
                {
                    ["MonitorId"] = monitor.MonitorId,
                    ["DisplayName"] = monitor.DisplayName,
                    ["Resolution"] = $"{monitor.Width}x{monitor.Height}",
                    ["Position"] = $"({monitor.X}, {monitor.Y})",
                    ["IsPrimary"] = monitor.IsPrimary
                });
            }
            
            TradingLogOrchestrator.Instance.LogInfo("Monitor detection completed", null, new Dictionary<string, object>
            {
                ["MonitorCount"] = monitors.Count
            });
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to detect monitors", null, ex);
            throw;
        }
        
        return monitors;
    }

    public async Task<MultiMonitorConfiguration> GetConfigurationAsync()
    {
        if (_currentConfiguration != null)
            return _currentConfiguration;

        await _configLock.WaitAsync();
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                _currentConfiguration = JsonSerializer.Deserialize<MultiMonitorConfiguration>(json);
                
                if (_currentConfiguration != null)
                {
                    TradingLogOrchestrator.Instance.LogInfo("Loaded monitor configuration from file", null, new Dictionary<string, object>
                    {
                        ["Version"] = _currentConfiguration.Version,
                        ["MonitorCount"] = _currentConfiguration.Monitors.Count,
                        ["ScreenAssignments"] = _currentConfiguration.ScreenAssignments.Count
                    });
                    
                    return _currentConfiguration;
                }
            }

            // Create default configuration if none exists
            var monitors = await DetectMonitorsAsync();
            _currentConfiguration = new MultiMonitorConfiguration
            {
                Monitors = monitors,
                ScreenAssignments = new Dictionary<TradingScreenType, string>(),
                Version = 1,
                LastUpdated = DateTime.UtcNow,
                AutoAssignScreens = true,
                RememberWindowPositions = true
            };

            await SaveConfigurationAsync(_currentConfiguration);
            return _currentConfiguration;
        }
        finally
        {
            _configLock.Release();
        }
    }

    public async Task SaveConfigurationAsync(MultiMonitorConfiguration configuration)
    {
        await _configLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(_configFilePath, json);
            _currentConfiguration = configuration;
            
            TradingLogOrchestrator.Instance.LogInfo("Saved monitor configuration", null, new Dictionary<string, object>
            {
                ["Version"] = configuration.Version,
                ["MonitorCount"] = configuration.Monitors.Count,
                ["ScreenAssignments"] = configuration.ScreenAssignments.Count
            });
            
            ConfigurationChanged?.Invoke(this, configuration);
        }
        finally
        {
            _configLock.Release();
        }
    }

    public async Task AssignScreenToMonitorAsync(TradingScreenType screenType, string monitorId)
    {
        var config = await GetConfigurationAsync();
        
        // Remove any existing assignment for this screen type
        var existingAssignment = config.ScreenAssignments.FirstOrDefault(x => x.Key == screenType);
        if (existingAssignment.Key != default)
        {
            config.ScreenAssignments.Remove(existingAssignment.Key);
        }
        
        // Add new assignment
        config.ScreenAssignments[screenType] = monitorId;
        
        // Update monitor assignment
        foreach (var monitor in config.Monitors)
        {
            if (monitor.MonitorId == monitorId)
            {
                monitor.AssignedScreen = screenType;
            }
            else if (monitor.AssignedScreen == screenType)
            {
                monitor.AssignedScreen = null;
            }
        }
        
        var updatedConfig = config with { LastUpdated = DateTime.UtcNow };
        await SaveConfigurationAsync(updatedConfig);
        
        TradingLogOrchestrator.Instance.LogInfo("Assigned trading screen to monitor", null, new Dictionary<string, object>
        {
            ["ScreenType"] = screenType.ToString(),
            ["MonitorId"] = monitorId
        });
    }

    public async Task<MonitorConfiguration?> GetAssignedMonitorAsync(TradingScreenType screenType)
    {
        var config = await GetConfigurationAsync();
        
        if (config.ScreenAssignments.TryGetValue(screenType, out var monitorId))
        {
            return config.Monitors.FirstOrDefault(m => m.MonitorId == monitorId);
        }
        
        return null;
    }

    public async Task SaveWindowPositionAsync(WindowPositionInfo positionInfo)
    {
        _windowPositions[positionInfo.ScreenType] = positionInfo;
        
        var json = JsonSerializer.Serialize(_windowPositions.Values.ToList(), new JsonSerializerOptions
        {
            WriteIndented = true
        });
        
        await File.WriteAllTextAsync(_positionsFilePath, json);
        
        TradingLogOrchestrator.Instance.LogInfo("Saved window position", null, new Dictionary<string, object>
        {
            ["ScreenType"] = positionInfo.ScreenType.ToString(),
            ["MonitorId"] = positionInfo.MonitorId,
            ["Position"] = $"({positionInfo.X}, {positionInfo.Y})",
            ["Size"] = $"{positionInfo.Width}x{positionInfo.Height}",
            ["WindowState"] = positionInfo.WindowState.ToString()
        });
        
        WindowPositionSaved?.Invoke(this, positionInfo);
    }

    public async Task<WindowPositionInfo?> GetSavedWindowPositionAsync(TradingScreenType screenType)
    {
        if (_windowPositions.TryGetValue(screenType, out var position))
            return position;

        // Load from file if not in memory
        if (File.Exists(_positionsFilePath))
        {
            var json = await File.ReadAllTextAsync(_positionsFilePath);
            var positions = JsonSerializer.Deserialize<List<WindowPositionInfo>>(json) ?? [];
            
            foreach (var pos in positions)
            {
                _windowPositions[pos.ScreenType] = pos;
            }
            
            return _windowPositions.GetValueOrDefault(screenType);
        }
        
        return null;
    }

    public async Task<bool> AutoConfigureForDayTradingAsync()
    {
        var monitors = await DetectMonitorsAsync();
        
        if (monitors.Count < 4)
        {
            TradingLogOrchestrator.Instance.LogWarning("Insufficient monitors for 4-screen day trading setup", null, new Dictionary<string, object>
            {
                ["AvailableMonitors"] = monitors.Count,
                ["RequiredMonitors"] = 4
            });
            
            // Configure with available monitors
            var screens = new[] { TradingScreenType.PrimaryCharting, TradingScreenType.OrderExecution, 
                                TradingScreenType.PortfolioRisk, TradingScreenType.MarketScanner };
            
            for (int i = 0; i < Math.Min(monitors.Count, screens.Length); i++)
            {
                await AssignScreenToMonitorAsync(screens[i], monitors[i].MonitorId);
            }
            
            return monitors.Count >= 2; // Minimum viable setup
        }

        // Optimal 4-screen configuration
        // Primary monitor (typically center-left): Primary Charting
        var primaryMonitor = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
        await AssignScreenToMonitorAsync(TradingScreenType.PrimaryCharting, primaryMonitor.MonitorId);

        // Assign remaining screens based on position
        var remainingMonitors = monitors.Where(m => m.MonitorId != primaryMonitor.MonitorId)
                                       .OrderBy(m => m.X) // Left to right ordering
                                       .ToList();

        if (remainingMonitors.Count >= 1)
            await AssignScreenToMonitorAsync(TradingScreenType.OrderExecution, remainingMonitors[0].MonitorId);
        
        if (remainingMonitors.Count >= 2)
            await AssignScreenToMonitorAsync(TradingScreenType.PortfolioRisk, remainingMonitors[1].MonitorId);
        
        if (remainingMonitors.Count >= 3)
            await AssignScreenToMonitorAsync(TradingScreenType.MarketScanner, remainingMonitors[2].MonitorId);

        TradingLogOrchestrator.Instance.LogInfo("Auto-configured monitors for day trading", null, new Dictionary<string, object>
        {
            ["TotalMonitors"] = monitors.Count,
            ["ConfiguredScreens"] = Math.Min(4, monitors.Count)
        });

        return true;
    }

    public async Task<(bool IsValid, List<TradingScreenType> MissingAssignments)> ValidateScreenAssignmentsAsync()
    {
        var config = await GetConfigurationAsync();
        var requiredScreens = new[] { TradingScreenType.PrimaryCharting, TradingScreenType.OrderExecution, 
                                    TradingScreenType.PortfolioRisk, TradingScreenType.MarketScanner };
        
        var missingAssignments = new List<TradingScreenType>();
        
        foreach (var screenType in requiredScreens)
        {
            if (!config.ScreenAssignments.ContainsKey(screenType))
            {
                missingAssignments.Add(screenType);
            }
        }
        
        var isValid = missingAssignments.Count == 0;
        
        if (!isValid)
        {
            TradingLogOrchestrator.Instance.LogWarning("Screen assignments validation failed", null, new Dictionary<string, object>
            {
                ["MissingAssignments"] = missingAssignments.Select(s => s.ToString()).ToArray(),
                ["TotalMissing"] = missingAssignments.Count
            });
        }
        
        return (isValid, missingAssignments);
    }
}