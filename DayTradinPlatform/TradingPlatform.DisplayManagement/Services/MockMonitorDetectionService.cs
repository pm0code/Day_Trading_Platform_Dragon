using TradingPlatform.Core.Interfaces;
using TradingPlatform.DisplayManagement.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.DisplayManagement.Services;

/// <summary>
/// Mock monitor detection service for RDP testing and UI development
/// Simulates various monitor configurations for testing purposes
/// </summary>
public class MockMonitorDetectionService : IMonitorDetectionService
{
    private readonly ITradingLogger _logger;
    private readonly IGpuDetectionService _gpuDetectionService;

    public MockMonitorDetectionService(
        ITradingLogger logger,
        IGpuDetectionService gpuDetectionService)
    {
        _logger = logger;
        _gpuDetectionService = gpuDetectionService;
    }

    public async Task<List<MonitorConfiguration>> GetConnectedMonitorsAsync()
    {
        TradingLogOrchestrator.Instance.LogInfo("Mock monitor detection: RDP session detected - showing 1 active monitor");

        await Task.Delay(300);

        // For RDP: Only show the active RDP display
        return new List<MonitorConfiguration>
        {
            new MonitorConfiguration
            {
                MonitorId = "RDP_DISPLAY",
                DisplayName = "RDP Remote Display (Active)",
                X = 0,
                Y = 0,
                Width = 1920,
                Height = 1080,
                IsPrimary = true,
                DpiScale = 1.0,
                IsActive = true,
                AssignedScreen = TradingScreenType.PrimaryCharting
            }
        };
    }

    public async Task<MonitorSelectionRecommendation> GetMonitorRecommendationAsync()
    {
        await Task.Delay(200);

        return new MonitorSelectionRecommendation
        {
            RecommendedMonitorCount = 6,
            MaximumSupportedMonitors = 8,
            OptimalResolution = Resolution.UHD4K,
            SuggestedLayout = new List<TradingScreenLayout>
            {
                new TradingScreenLayout
                {
                    ScreenType = TradingScreenType.PrimaryCharting,
                    MonitorIndex = 0,
                    Priority = 1,
                    Description = "Primary charts and technical analysis",
                    RequiresHighRefreshRate = true,
                    MinimumResolution = Resolution.FullHD
                },
                new TradingScreenLayout
                {
                    ScreenType = TradingScreenType.OrderExecution,
                    MonitorIndex = 1,
                    Priority = 2,
                    Description = "Order entry and Level II market depth",
                    RequiresHighRefreshRate = true,
                    MinimumResolution = Resolution.FullHD
                },
                new TradingScreenLayout
                {
                    ScreenType = TradingScreenType.PortfolioRisk,
                    MonitorIndex = 2,
                    Priority = 3,
                    Description = "Portfolio P&L and risk management",
                    RequiresHighRefreshRate = false,
                    MinimumResolution = Resolution.FullHD
                },
                new TradingScreenLayout
                {
                    ScreenType = TradingScreenType.MarketScanner,
                    MonitorIndex = 3,
                    Priority = 4,
                    Description = "Market scanner and news feeds",
                    RequiresHighRefreshRate = false,
                    MinimumResolution = Resolution.HD
                }
            },
            PerformanceExpectation = "Excellent performance with 6 monitors. Sub-millisecond order execution, smooth 60+ FPS charts, ideal for high-frequency trading.",
            HardwareRequirements = new List<string>
            {
                "RTX 4070 Ti + RTX 3060 Ti detected - excellent for 6+ monitor trading setup",
                "High-speed DisplayPort or HDMI 2.1 cables for 4K monitors",
                "Adequate desk space and monitor arms for ergonomic 6-screen arrangement"
            },
            AlternativeConfigurations = new List<string>
            {
                "4-monitor setup: Focus on essential trading screens only",
                "8-monitor setup: Maximum capability - requires excellent cooling",
                "Ultra-wide alternative: Two 49\" curved monitors instead of 4 standard monitors"
            }
        };
    }

    public async Task<MultiMonitorConfiguration?> LoadMonitorConfigurationAsync()
    {
        await Task.Delay(100);

        // Simulate saved configuration
        var monitors = await GetConnectedMonitorsAsync();

        return new MultiMonitorConfiguration
        {
            Monitors = monitors.Take(1).ToList(), // Currently only RDP monitor
            ScreenAssignments = new Dictionary<TradingScreenType, string>
            {
                { TradingScreenType.PrimaryCharting, "RDP_DISPLAY" }
            },
            Version = 1,
            LastUpdated = DateTime.UtcNow.AddDays(-2),
            AutoAssignScreens = true,
            RememberWindowPositions = true
        };
    }

    public async Task SaveMonitorConfigurationAsync(MultiMonitorConfiguration configuration)
    {
        TradingLogOrchestrator.Instance.LogInfo($"Mock save: Monitor configuration with {configuration.Monitors.Count} monitors");

        await Task.Delay(100);

        // Simulate successful save
    }

    public async Task<MonitorConfigurationValidation> ValidateAndOptimizeConfigurationAsync(MultiMonitorConfiguration configuration)
    {
        var validation = await _gpuDetectionService.ValidateMonitorConfigurationAsync(configuration.Monitors);

        // Add trading-specific mock validation
        if (configuration.Monitors.Count == 1)
        {
            validation.OptimizationSuggestions.Add("Consider adding a second monitor for Order Execution to improve trading workflow");
        }

        if (configuration.Monitors.Count >= 4)
        {
            validation.OptimizationSuggestions.Add("Arrange monitors in curved formation: Charts (center), Orders (right), Portfolio (left), Scanner (far right)");
        }

        return validation;
    }
}
