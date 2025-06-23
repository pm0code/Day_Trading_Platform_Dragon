using System.Management;
using System.Runtime.InteropServices;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.DisplayManagement.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.DisplayManagement.Services;

/// <summary>
/// Service for detecting GPU capabilities and recommending maximum monitor configurations
/// Optimized for Windows 11 x64 DRAGON system multi-monitor trading setups
/// </summary>
public interface IGpuDetectionService
{
    /// <summary>
    /// Gets detailed information about all GPUs in the system
    /// </summary>
    Task<List<GpuInfo>> GetGpuInformationAsync();

    /// <summary>
    /// Gets the recommended maximum number of monitors based on GPU capabilities
    /// </summary>
    Task<int> GetRecommendedMaxMonitorsAsync();

    /// <summary>
    /// Gets GPU performance assessment for trading workloads
    /// </summary>
    Task<GpuPerformanceAssessment> GetPerformanceAssessmentAsync();

    /// <summary>
    /// Validates if a specific monitor configuration is supported by current GPUs
    /// </summary>
    Task<MonitorConfigurationValidation> ValidateMonitorConfigurationAsync(List<MonitorConfiguration> configuration);
}

/// <summary>
/// GPU detection and monitoring service for DRAGON trading platform
/// </summary>
public class GpuDetectionService : IGpuDetectionService
{
    private readonly ITradingLogger _logger;
    private readonly List<GpuInfo> _cachedGpuInfo = new();
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

    public GpuDetectionService(ITradingLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets comprehensive GPU information using Windows Management Instrumentation
    /// </summary>
    public async Task<List<GpuInfo>> GetGpuInformationAsync()
    {
        TradingLogOrchestrator.Instance.LogInfo("Detecting GPU information for DRAGON system monitor configuration");

        // Return cached data if still valid
        if (_cachedGpuInfo.Any() && DateTime.UtcNow - _lastCacheUpdate < _cacheExpiry)
        {
            TradingLogOrchestrator.Instance.LogInfo("Returning cached GPU information");
            return _cachedGpuInfo;
        }

        try
        {
            _cachedGpuInfo.Clear();

            // Query video controllers using WMI
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            var videoControllers = await Task.Run(() => searcher.Get());

            foreach (ManagementObject gpu in videoControllers)
            {
                try
                {
                    var gpuInfo = ExtractGpuInfo(gpu);
                    if (gpuInfo != null)
                    {
                        _cachedGpuInfo.Add(gpuInfo);
                        TradingLogOrchestrator.Instance.LogInfo($"Detected GPU: {gpuInfo.Name} - {gpuInfo.MaxDisplayOutputs} max monitors, {gpuInfo.VideoMemoryGB}GB VRAM");
                    }
                }
                catch (Exception ex)
                {
                    TradingLogOrchestrator.Instance.LogWarning("Failed to extract information for GPU device", additionalData: new { Error = ex.Message });
                }
            }

            _lastCacheUpdate = DateTime.UtcNow;

            TradingLogOrchestrator.Instance.LogInfo($"GPU detection complete. Found {_cachedGpuInfo.Count} GPU(s)");
            return _cachedGpuInfo;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to detect GPU information", ex);
            return new List<GpuInfo>();
        }
    }

    /// <summary>
    /// Calculates recommended maximum monitors based on all GPU capabilities
    /// </summary>
    public async Task<int> GetRecommendedMaxMonitorsAsync()
    {
        var gpus = await GetGpuInformationAsync();

        if (!gpus.Any())
        {
            TradingLogOrchestrator.Instance.LogWarning("No GPUs detected, defaulting to single monitor recommendation");
            return 1;
        }

        // Sum maximum outputs from all active GPUs
        var totalMaxOutputs = gpus
            .Where(gpu => gpu.IsActive && gpu.MaxDisplayOutputs > 0)
            .Sum(gpu => gpu.MaxDisplayOutputs);

        // Apply conservative factor for trading workload stability
        var recommendedMax = Math.Max(1, (int)(totalMaxOutputs * 0.8)); // 80% of theoretical max

        // Cap at reasonable limits for trading platforms
        var finalRecommendation = Math.Min(recommendedMax, 12); // Maximum 12 monitors for practical trading

        TradingLogOrchestrator.Instance.LogInfo($"Recommended maximum monitors: {finalRecommendation} (based on {totalMaxOutputs} total GPU outputs)");

        return finalRecommendation;
    }

    /// <summary>
    /// Assesses GPU performance for trading workloads
    /// </summary>
    public async Task<GpuPerformanceAssessment> GetPerformanceAssessmentAsync()
    {
        var gpus = await GetGpuInformationAsync();

        if (!gpus.Any())
        {
            return new GpuPerformanceAssessment
            {
                OverallRating = PerformanceRating.Poor,
                RecommendedMonitors = 1,
                TradingWorkloadSupport = "No GPU detected - limited multi-monitor capability",
                Limitations = new List<string> { "No dedicated GPU found", "Integrated graphics may limit performance" }
            };
        }

        var primaryGpu = gpus.OrderByDescending(g => g.VideoMemoryGB).First();
        var totalVram = gpus.Sum(g => g.VideoMemoryGB);
        var totalOutputs = gpus.Sum(g => g.MaxDisplayOutputs);

        var rating = CalculatePerformanceRating(primaryGpu, totalVram, totalOutputs);
        var recommendedMonitors = await GetRecommendedMaxMonitorsAsync();

        return new GpuPerformanceAssessment
        {
            OverallRating = rating,
            RecommendedMonitors = recommendedMonitors,
            TotalVideoMemoryGB = totalVram,
            PrimaryGpuName = primaryGpu.Name,
            TradingWorkloadSupport = GetTradingWorkloadDescription(rating),
            Limitations = GetLimitations(gpus),
            OptimalResolutionSuggestion = GetOptimalResolution(rating, recommendedMonitors)
        };
    }

    /// <summary>
    /// Validates if a monitor configuration is supported by current hardware
    /// </summary>
    public async Task<MonitorConfigurationValidation> ValidateMonitorConfigurationAsync(List<MonitorConfiguration> configuration)
    {
        var gpus = await GetGpuInformationAsync();
        var maxRecommended = await GetRecommendedMaxMonitorsAsync();

        var validation = new MonitorConfigurationValidation
        {
            IsSupported = true,
            Warnings = new List<string>(),
            Errors = new List<string>(),
            MonitorCount = configuration.Count,
            RecommendedMaximum = maxRecommended
        };

        // Check monitor count against GPU capabilities
        if (configuration.Count > maxRecommended)
        {
            validation.IsSupported = false;
            validation.Errors.Add($"Configuration requests {configuration.Count} monitors but GPU supports maximum {maxRecommended}");
        }

        // Check resolution requirements
        var totalPixels = configuration.Sum(m => (long)m.Width * m.Height);
        var totalVram = gpus.Sum(g => g.VideoMemoryGB);
        var estimatedVramNeeded = totalPixels * 4 / (1024 * 1024 * 1024); // Rough estimate: 4 bytes per pixel

        if (estimatedVramNeeded > totalVram * 0.6) // Use 60% of VRAM for display buffer
        {
            validation.Warnings.Add($"High resolution configuration may strain video memory ({estimatedVramNeeded:F1}GB estimated need vs {totalVram:F1}GB available)");
        }

        // Check for optimal trading setup
        if (configuration.Count >= 4)
        {
            validation.Warnings.Add("4+ monitor configuration detected - ensure adequate GPU cooling for extended trading sessions");
        }

        TradingLogOrchestrator.Instance.LogInfo($"Monitor configuration validation: {configuration.Count} monitors, Supported: {validation.IsSupported}");

        return validation;
    }

    #region Private Helper Methods

    private GpuInfo? ExtractGpuInfo(ManagementObject gpu)
    {
        try
        {
            var name = gpu["Name"]?.ToString() ?? "Unknown GPU";
            var driverVersion = gpu["DriverVersion"]?.ToString() ?? "Unknown";

            // Extract video memory (in bytes, convert to GB)
            var vramBytes = Convert.ToUInt64(gpu["AdapterRAM"] ?? 0);
            var vramGB = vramBytes / (1024.0 * 1024.0 * 1024.0);

            // Determine maximum display outputs (vendor-specific logic)
            var maxOutputs = EstimateMaxDisplayOutputs(name, vramGB);

            // Check if GPU is active/enabled
            var availability = Convert.ToUInt16(gpu["Availability"] ?? 0);
            var isActive = availability == 3; // Available and active

            return new GpuInfo
            {
                Name = name,
                DriverVersion = driverVersion,
                VideoMemoryGB = vramGB,
                MaxDisplayOutputs = maxOutputs,
                IsActive = isActive,
                VendorId = gpu["PNPDeviceID"]?.ToString() ?? "",
                DeviceId = gpu["DeviceID"]?.ToString() ?? "",
                LastDetected = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogWarning("Failed to extract GPU information from WMI object", additionalData: new { Error = ex.Message });
            return null;
        }
    }

    private int EstimateMaxDisplayOutputs(string gpuName, double vramGB)
    {
        var name = gpuName.ToLowerInvariant();

        // NVIDIA estimation
        if (name.Contains("nvidia") || name.Contains("geforce") || name.Contains("rtx") || name.Contains("gtx"))
        {
            if (name.Contains("rtx 40") || name.Contains("rtx 30")) return 4; // Modern RTX series
            if (name.Contains("rtx 20") || name.Contains("gtx 16")) return 4;
            if (name.Contains("gtx 10")) return 3;
            if (vramGB >= 8) return 4;
            if (vramGB >= 4) return 3;
            return 2;
        }

        // AMD estimation
        if (name.Contains("amd") || name.Contains("radeon") || name.Contains("rx"))
        {
            if (name.Contains("rx 7") || name.Contains("rx 6")) return 4; // RDNA2/3
            if (name.Contains("rx 5")) return 4;
            if (vramGB >= 8) return 4;
            if (vramGB >= 4) return 3;
            return 2;
        }

        // Intel estimation
        if (name.Contains("intel") || name.Contains("iris") || name.Contains("uhd"))
        {
            if (name.Contains("arc")) return 4; // Intel Arc series
            if (vramGB >= 2) return 3; // Integrated with adequate memory
            return 2; // Basic integrated graphics
        }

        // Conservative fallback based on VRAM
        if (vramGB >= 8) return 4;
        if (vramGB >= 4) return 3;
        if (vramGB >= 2) return 2;
        return 1;
    }

    private PerformanceRating CalculatePerformanceRating(GpuInfo primaryGpu, double totalVram, int totalOutputs)
    {
        var score = 0;

        // VRAM scoring
        if (totalVram >= 16) score += 40;
        else if (totalVram >= 8) score += 30;
        else if (totalVram >= 4) score += 20;
        else score += 10;

        // Output capacity scoring
        if (totalOutputs >= 8) score += 30;
        else if (totalOutputs >= 4) score += 25;
        else if (totalOutputs >= 2) score += 15;
        else score += 5;

        // GPU type scoring
        var name = primaryGpu.Name.ToLowerInvariant();
        if (name.Contains("rtx 40") || name.Contains("rx 7")) score += 30; // Latest generation
        else if (name.Contains("rtx 30") || name.Contains("rtx 20") || name.Contains("rx 6")) score += 25;
        else if (name.Contains("gtx 16") || name.Contains("rx 5")) score += 20;
        else if (name.Contains("gtx 10")) score += 15;
        else score += 10;

        return score switch
        {
            >= 90 => PerformanceRating.Excellent,
            >= 70 => PerformanceRating.Good,
            >= 50 => PerformanceRating.Fair,
            _ => PerformanceRating.Poor
        };
    }

    private string GetTradingWorkloadDescription(PerformanceRating rating)
    {
        return rating switch
        {
            PerformanceRating.Excellent => "Excellent for high-frequency trading with 6+ monitors at 4K resolution",
            PerformanceRating.Good => "Good for professional trading with 4-6 monitors at 1440p/4K resolution",
            PerformanceRating.Fair => "Adequate for standard trading with 2-4 monitors at 1080p/1440p resolution",
            PerformanceRating.Poor => "Limited to basic trading setup with 1-2 monitors at 1080p resolution",
            _ => "Performance assessment unavailable"
        };
    }

    private List<string> GetLimitations(List<GpuInfo> gpus)
    {
        var limitations = new List<string>();

        var totalVram = gpus.Sum(g => g.VideoMemoryGB);
        if (totalVram < 4)
        {
            limitations.Add("Low video memory may limit high-resolution multi-monitor performance");
        }

        if (gpus.Count == 1 && gpus[0].Name.ToLowerInvariant().Contains("intel"))
        {
            limitations.Add("Integrated graphics detected - consider dedicated GPU for optimal multi-monitor trading");
        }

        if (gpus.Any(g => !g.IsActive))
        {
            limitations.Add("Some GPUs are inactive - check device manager and drivers");
        }

        return limitations;
    }

    private string GetOptimalResolution(PerformanceRating rating, int monitorCount)
    {
        return rating switch
        {
            PerformanceRating.Excellent when monitorCount <= 4 => "4K (3840x2160) per monitor",
            PerformanceRating.Excellent => "1440p (2560x1440) per monitor for 5+ setup",
            PerformanceRating.Good when monitorCount <= 3 => "4K (3840x2160) per monitor",
            PerformanceRating.Good => "1440p (2560x1440) per monitor",
            PerformanceRating.Fair => "1080p (1920x1080) per monitor",
            PerformanceRating.Poor => "1080p (1920x1080) maximum",
            _ => "1080p (1920x1080) recommended"
        };
    }

    #endregion
}
