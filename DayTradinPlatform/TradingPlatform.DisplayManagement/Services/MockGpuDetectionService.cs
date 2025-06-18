using TradingPlatform.Core.Interfaces;
using TradingPlatform.DisplayManagement.Models;

namespace TradingPlatform.DisplayManagement.Services;

/// <summary>
/// Mock GPU detection service for RDP testing and UI development
/// Simulates RTX 4070 Ti + RTX 3060 Ti dual-GPU configuration
/// </summary>
public class MockGpuDetectionService : IGpuDetectionService
{
    private readonly ILogger _logger;

    public MockGpuDetectionService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<List<GpuInfo>> GetGpuInformationAsync()
    {
        _logger.LogInformation("Mock GPU detection: Simulating RTX 4070 Ti + RTX 3060 Ti setup");
        
        await Task.Delay(500); // Simulate detection time
        
        return new List<GpuInfo>
        {
            new GpuInfo
            {
                Name = "NVIDIA GeForce RTX 4070 Ti",
                DriverVersion = "546.29",
                VideoMemoryGB = 12.0,
                MaxDisplayOutputs = 4,
                IsActive = true,
                VendorId = "PCI\\VEN_10DE&DEV_2782",
                DeviceId = "GPU_0",
                PerformanceTier = GpuPerformanceTier.DedicatedHighEnd,
                SupportsHardwareAcceleration = true,
                LastDetected = DateTime.UtcNow
            },
            new GpuInfo
            {
                Name = "NVIDIA GeForce RTX 3060 Ti",
                DriverVersion = "546.29", 
                VideoMemoryGB = 8.0,
                MaxDisplayOutputs = 4,
                IsActive = true,
                VendorId = "PCI\\VEN_10DE&DEV_2489",
                DeviceId = "GPU_1",
                PerformanceTier = GpuPerformanceTier.DedicatedMidRange,
                SupportsHardwareAcceleration = true,
                LastDetected = DateTime.UtcNow
            }
        };
    }

    public async Task<int> GetRecommendedMaxMonitorsAsync()
    {
        await Task.Delay(100);
        return 8; // Dual GPU setup supports 8 monitors total
    }

    public async Task<GpuPerformanceAssessment> GetPerformanceAssessmentAsync()
    {
        await Task.Delay(200);
        
        return new GpuPerformanceAssessment
        {
            OverallRating = PerformanceRating.Excellent,
            RecommendedMonitors = 6,
            TotalVideoMemoryGB = 20.0,
            PrimaryGpuName = "NVIDIA GeForce RTX 4070 Ti",
            TradingWorkloadSupport = "Excellent for high-frequency trading with 6+ monitors at 4K resolution",
            Limitations = new List<string>(),
            OptimalResolutionSuggestion = "4K (3840x2160) per monitor for up to 4 monitors, 1440p for 5+ setup",
            SupportsUltraLowLatency = true,
            AssessmentTime = DateTime.UtcNow
        };
    }

    public async Task<MonitorConfigurationValidation> ValidateMonitorConfigurationAsync(List<MonitorConfiguration> configuration)
    {
        await Task.Delay(100);
        
        var validation = new MonitorConfigurationValidation
        {
            IsSupported = configuration.Count <= 8,
            MonitorCount = configuration.Count,
            RecommendedMaximum = 8,
            Warnings = new List<string>(),
            Errors = new List<string>(),
            OptimizationSuggestions = new List<string>(),
            PerformanceImpact = configuration.Count switch
            {
                <= 4 => PerformanceImpact.None,
                <= 6 => PerformanceImpact.Minimal,
                <= 8 => PerformanceImpact.Moderate,
                _ => PerformanceImpact.Significant
            },
            RecommendedForTrading = configuration.Count <= 8,
            ValidationTime = DateTime.UtcNow
        };

        // Add some realistic warnings and suggestions
        if (configuration.Count >= 6)
        {
            validation.Warnings.Add("6+ monitor configuration - ensure adequate GPU cooling for extended trading sessions");
        }

        if (configuration.Count >= 4)
        {
            validation.OptimizationSuggestions.Add("Consider 4K resolution for primary trading monitors, 1440p for secondary");
        }

        if (configuration.Count > 8)
        {
            validation.IsSupported = false;
            validation.Errors.Add($"Configuration requests {configuration.Count} monitors but GPU supports maximum 8");
        }

        return validation;
    }
}