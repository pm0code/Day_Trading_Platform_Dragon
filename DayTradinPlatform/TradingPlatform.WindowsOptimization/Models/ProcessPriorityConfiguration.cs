using System.Diagnostics;

namespace TradingPlatform.WindowsOptimization.Models;

public record ProcessPriorityConfiguration
{
    public string ProcessName { get; set; } = string.Empty;
    public ProcessPriorityClass Priority { get; set; } = ProcessPriorityClass.Normal;
    public int[]? CpuCoreAffinity { get; set; }
    public bool EnableLowLatencyGc { get; set; } = true;
    public bool PreJitMethods { get; set; } = true;
    public long WorkingSetLimit { get; set; } = 0; // 0 = no limit, changed to long to avoid overflow
}

public record SystemOptimizationSettings
{
    public bool DisableWindowsDefender { get; set; } = false;
    public bool DisableWindowsUpdate { get; set; } = false;
    public bool OptimizeNetworkStack { get; set; } = true;
    public bool SetHighPerformancePowerPlan { get; set; } = true;
    public bool DisableHibernation { get; set; } = true;
    public int TimerResolution { get; set; } = 1; // milliseconds
    public bool EnableHighPrecisionTimer { get; set; } = true;
}

public record PerformanceMetrics
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public double CpuUsagePercent { get; init; }
    public long MemoryUsageBytes { get; init; }
    public long WorkingSetBytes { get; init; }
    public int ThreadCount { get; init; }
    public int HandleCount { get; init; }
    public TimeSpan ProcessorTime { get; init; }
    public long PageFaults { get; init; }
    public ProcessPriorityClass CurrentPriority { get; init; }
    public long ProcessorAffinity { get; init; }
    public double LatencyMicroseconds { get; init; }
}