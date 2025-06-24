using System.Runtime.InteropServices;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.DisplayManagement.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.DisplayManagement.Services;

/// <summary>
/// Service for detecting and managing monitor configurations for DRAGON trading platform
/// </summary>
public interface IMonitorDetectionService
{
    /// <summary>
    /// Gets all currently connected monitors
    /// </summary>
    Task<List<MonitorConfiguration>> GetConnectedMonitorsAsync();

    /// <summary>
    /// Gets monitor selection recommendations based on GPU capabilities
    /// </summary>
    Task<MonitorSelectionRecommendation> GetMonitorRecommendationAsync();

    /// <summary>
    /// Saves monitor configuration preferences
    /// </summary>
    Task SaveMonitorConfigurationAsync(MultiMonitorConfiguration configuration);

    /// <summary>
    /// Loads saved monitor configuration
    /// </summary>
    Task<MultiMonitorConfiguration?> LoadMonitorConfigurationAsync();

    /// <summary>
    /// Validates and optimizes a monitor configuration for trading
    /// </summary>
    Task<MonitorConfigurationValidation> ValidateAndOptimizeConfigurationAsync(MultiMonitorConfiguration configuration);
}

/// <summary>
/// Monitor detection and configuration service for DRAGON multi-monitor trading setup
/// </summary>
public class MonitorDetectionService : IMonitorDetectionService
{
    private readonly ITradingLogger _logger;
    private readonly IGpuDetectionService _gpuDetectionService;
    private readonly string _configurationFilePath;

    // Windows API declarations for monitor enumeration
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    private const uint MONITORINFOF_PRIMARY = 0x00000001;

    public MonitorDetectionService(
        ITradingLogger logger,
        IGpuDetectionService gpuDetectionService)
    {
        _logger = logger;
        _gpuDetectionService = gpuDetectionService;
        _configurationFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TradingPlatform", "monitor_configuration.json");
    }

    /// <summary>
    /// Detects all connected monitors using Windows API
    /// </summary>
    public Task<List<MonitorConfiguration>> GetConnectedMonitorsAsync()
    {
        TradingLogOrchestrator.Instance.LogInfo("Detecting connected monitors for DRAGON trading platform");

        var monitors = new List<MonitorConfiguration>();
        var monitorIndex = 0;

        try
        {
            // Use Windows API to enumerate monitors
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData) =>
            {
                var monitorInfo = new MonitorInfoEx();
                monitorInfo.Size = Marshal.SizeOf(monitorInfo);

                if (GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    var monitor = new MonitorConfiguration
                    {
                        MonitorId = $"MONITOR_{monitorIndex}_{monitorInfo.DeviceName}",
                        DisplayName = $"Monitor {monitorIndex + 1} ({monitorInfo.DeviceName})",
                        X = monitorInfo.Monitor.Left,
                        Y = monitorInfo.Monitor.Top,
                        Width = monitorInfo.Monitor.Right - monitorInfo.Monitor.Left,
                        Height = monitorInfo.Monitor.Bottom - monitorInfo.Monitor.Top,
                        IsPrimary = (monitorInfo.Flags & MONITORINFOF_PRIMARY) == MONITORINFOF_PRIMARY,
                        DpiScale = GetMonitorDpiScale(hMonitor),
                        IsActive = true
                    };

                    monitors.Add(monitor);
                    monitorIndex++;

                    TradingLogOrchestrator.Instance.LogInfo($"Detected monitor: {monitor.DisplayName} at {monitor.X},{monitor.Y} ({monitor.Width}x{monitor.Height}), Primary: {monitor.IsPrimary}");
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);

            TradingLogOrchestrator.Instance.LogInfo($"Monitor detection complete. Found {monitors.Count} monitor(s)");
            return Task.FromResult(monitors);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to detect monitors", ex);

            // Fallback: create a single default monitor configuration
            return Task.FromResult(new List<MonitorConfiguration>
            {
                new MonitorConfiguration
                {
                    MonitorId = "DEFAULT_MONITOR",
                    DisplayName = "Primary Monitor",
                    X = 0,
                    Y = 0,
                    Width = 1920,
                    Height = 1080,
                    IsPrimary = true,
                    DpiScale = 1.0,
                    IsActive = true
                }
            });
        }
    }

    /// <summary>
    /// Generates monitor recommendations based on GPU capabilities and trading requirements
    /// </summary>
    public async Task<MonitorSelectionRecommendation> GetMonitorRecommendationAsync()
    {
        TradingLogOrchestrator.Instance.LogInfo("Generating monitor recommendations for DRAGON trading setup");

        var connectedMonitors = await GetConnectedMonitorsAsync();
        var gpuAssessment = await _gpuDetectionService.GetPerformanceAssessmentAsync();
        var maxGpuSupported = await _gpuDetectionService.GetRecommendedMaxMonitorsAsync();

        // Determine optimal monitor count for trading
        var recommendedCount = DetermineOptimalMonitorCount(connectedMonitors.Count, maxGpuSupported, gpuAssessment.OverallRating);
        var optimalResolution = DetermineOptimalResolution(gpuAssessment.OverallRating, recommendedCount);

        var recommendation = new MonitorSelectionRecommendation
        {
            RecommendedMonitorCount = recommendedCount,
            MaximumSupportedMonitors = maxGpuSupported,
            OptimalResolution = optimalResolution,
            SuggestedLayout = GenerateTradingScreenLayout(recommendedCount),
            PerformanceExpectation = GeneratePerformanceExpectation(gpuAssessment.OverallRating, recommendedCount),
            HardwareRequirements = GenerateHardwareRequirements(recommendedCount, optimalResolution),
            AlternativeConfigurations = GenerateAlternativeConfigurations(connectedMonitors.Count, maxGpuSupported)
        };

        TradingLogOrchestrator.Instance.LogInfo($"Monitor recommendation: {recommendedCount} monitors at {optimalResolution.DisplayName} resolution");

        return recommendation;
    }

    /// <summary>
    /// Saves monitor configuration to application data
    /// </summary>
    public async Task SaveMonitorConfigurationAsync(MultiMonitorConfiguration configuration)
    {
        try
        {
            var configDirectory = Path.GetDirectoryName(_configurationFilePath);
            if (!Directory.Exists(configDirectory))
            {
                Directory.CreateDirectory(configDirectory!);
            }

            var json = System.Text.Json.JsonSerializer.Serialize(configuration, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_configurationFilePath, json);

            TradingLogOrchestrator.Instance.LogInfo($"Monitor configuration saved to {_configurationFilePath}");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to save monitor configuration", ex);
            throw;
        }
    }

    /// <summary>
    /// Loads saved monitor configuration
    /// </summary>
    public async Task<MultiMonitorConfiguration?> LoadMonitorConfigurationAsync()
    {
        try
        {
            if (!File.Exists(_configurationFilePath))
            {
                TradingLogOrchestrator.Instance.LogInfo("No saved monitor configuration found");
                return null;
            }

            var json = await File.ReadAllTextAsync(_configurationFilePath);
            var configuration = System.Text.Json.JsonSerializer.Deserialize<MultiMonitorConfiguration>(json);

            TradingLogOrchestrator.Instance.LogInfo($"Monitor configuration loaded from {_configurationFilePath}");
            return configuration;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to load monitor configuration", ex);
            return null;
        }
    }

    /// <summary>
    /// Validates and provides optimization suggestions for a monitor configuration
    /// </summary>
    public async Task<MonitorConfigurationValidation> ValidateAndOptimizeConfigurationAsync(MultiMonitorConfiguration configuration)
    {
        var validation = await _gpuDetectionService.ValidateMonitorConfigurationAsync(configuration.Monitors);
        var recommendation = await GetMonitorRecommendationAsync();

        // Add trading-specific validation and optimization
        AddTradingSpecificValidation(validation, configuration, recommendation);

        return validation;
    }

    #region Private Helper Methods

    private double GetMonitorDpiScale(IntPtr hMonitor)
    {
        // For simplicity, return 1.0. In a full implementation, you would use
        // GetDpiForMonitor API to get actual DPI scaling
        return 1.0;
    }

    private int DetermineOptimalMonitorCount(int connectedCount, int maxSupported, PerformanceRating gpuRating)
    {
        // Trading-optimized monitor count based on professional trading practices
        var tradingOptimal = gpuRating switch
        {
            PerformanceRating.Excellent => 6, // Professional trading setup
            PerformanceRating.Good => 4,      // Standard professional setup
            PerformanceRating.Fair => 2,      // Basic trading setup
            PerformanceRating.Poor => 1,      // Single monitor
            _ => 1
        };

        // Return the minimum of: connected monitors, GPU max supported, trading optimal
        return Math.Min(Math.Min(connectedCount, maxSupported), tradingOptimal);
    }

    private Resolution DetermineOptimalResolution(PerformanceRating gpuRating, int monitorCount)
    {
        return gpuRating switch
        {
            PerformanceRating.Excellent when monitorCount <= 4 => Resolution.UHD4K,
            PerformanceRating.Excellent => Resolution.QHD,
            PerformanceRating.Good when monitorCount <= 3 => Resolution.UHD4K,
            PerformanceRating.Good => Resolution.QHD,
            PerformanceRating.Fair => Resolution.FullHD,
            _ => Resolution.FullHD
        };
    }

    private List<TradingScreenLayout> GenerateTradingScreenLayout(int monitorCount)
    {
        var layouts = new List<TradingScreenLayout>();

        // Essential trading screens in priority order
        var essentialScreens = new[]
        {
            new { Type = TradingScreenType.PrimaryCharting, Description = "Charts and technical analysis", Priority = 1, HighRefresh = true },
            new { Type = TradingScreenType.OrderExecution, Description = "Order entry and Level II", Priority = 2, HighRefresh = true },
            new { Type = TradingScreenType.PortfolioRisk, Description = "Portfolio P&L and risk management", Priority = 3, HighRefresh = false },
            new { Type = TradingScreenType.MarketScanner, Description = "Market scanner and news", Priority = 4, HighRefresh = false }
        };

        for (int i = 0; i < Math.Min(monitorCount, essentialScreens.Length); i++)
        {
            var screen = essentialScreens[i];
            layouts.Add(new TradingScreenLayout
            {
                ScreenType = screen.Type,
                MonitorIndex = i,
                Priority = screen.Priority,
                Description = screen.Description,
                RequiresHighRefreshRate = screen.HighRefresh,
                MinimumResolution = screen.HighRefresh ? Resolution.FullHD : Resolution.HD
            });
        }

        return layouts;
    }

    private string GeneratePerformanceExpectation(PerformanceRating rating, int monitorCount)
    {
        return rating switch
        {
            PerformanceRating.Excellent => $"Excellent performance with {monitorCount} monitors. Sub-millisecond order execution, smooth 60+ FPS charts, ideal for high-frequency trading.",
            PerformanceRating.Good => $"Good performance with {monitorCount} monitors. Fast order execution, smooth charting, suitable for professional day trading.",
            PerformanceRating.Fair => $"Adequate performance with {monitorCount} monitors. Acceptable for standard trading with occasional minor delays during high market activity.",
            PerformanceRating.Poor => $"Basic performance with {monitorCount} monitor(s). May experience delays during high market volatility. Consider GPU upgrade for multi-monitor setup.",
            _ => "Performance assessment unavailable."
        };
    }

    private List<string> GenerateHardwareRequirements(int monitorCount, Resolution resolution)
    {
        var requirements = new List<string>();

        if (monitorCount >= 4)
        {
            requirements.Add("Dedicated GPU with 8GB+ VRAM recommended for 4+ monitor setup");
            requirements.Add("High-speed DisplayPort or HDMI 2.1 cables for optimal bandwidth");
        }

        if (resolution.TotalPixels >= Resolution.UHD4K.TotalPixels)
        {
            requirements.Add("GPU with 4K multi-monitor support required");
            requirements.Add("High-bandwidth video connections (DisplayPort 1.4+ or HDMI 2.1)");
        }

        if (monitorCount >= 2)
        {
            requirements.Add("Adequate desk space and monitor arms for ergonomic setup");
            requirements.Add("Sufficient power supply wattage for multiple monitors");
        }

        return requirements;
    }

    private List<string> GenerateAlternativeConfigurations(int connectedCount, int maxSupported)
    {
        var alternatives = new List<string>();

        if (connectedCount < 2)
        {
            alternatives.Add("Consider adding a second monitor for improved trading workflow");
        }

        if (maxSupported > connectedCount)
        {
            alternatives.Add($"Your GPU supports up to {maxSupported} monitors - consider expanding your setup");
        }

        if (connectedCount > 4)
        {
            alternatives.Add("For 5+ monitors, consider grouping related functions to reduce screen switching");
        }

        alternatives.Add("Alternative: Use virtual desktops if physical monitor space is limited");
        alternatives.Add("Consider ultra-wide monitors as an alternative to multiple standard monitors");

        return alternatives;
    }

    private void AddTradingSpecificValidation(MonitorConfigurationValidation validation, MultiMonitorConfiguration configuration, MonitorSelectionRecommendation recommendation)
    {
        // Check for essential trading screen assignments
        var assignedScreens = configuration.ScreenAssignments.Keys.ToList();

        if (!assignedScreens.Contains(TradingScreenType.PrimaryCharting))
        {
            validation.Warnings.Add("No primary charting screen assigned - critical for technical analysis");
        }

        if (!assignedScreens.Contains(TradingScreenType.OrderExecution))
        {
            validation.Warnings.Add("No order execution screen assigned - may impact trading speed");
        }

        // Check for optimal trading layout
        if (configuration.Monitors.Count >= 2 && assignedScreens.Count < 2)
        {
            validation.OptimizationSuggestions.Add("Assign different trading functions to separate monitors for improved workflow");
        }

        // Validate monitor positioning for trading ergonomics
        if (configuration.Monitors.Count >= 3)
        {
            validation.OptimizationSuggestions.Add("Arrange monitors in a curved formation for optimal trading ergonomics");
        }
    }

    #endregion
}
