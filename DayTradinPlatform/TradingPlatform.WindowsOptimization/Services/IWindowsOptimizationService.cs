using System.Diagnostics;
using TradingPlatform.WindowsOptimization.Models;

namespace TradingPlatform.WindowsOptimization.Services;

public interface IWindowsOptimizationService
{
    Task<bool> SetProcessPriorityAsync(string processName, ProcessPriorityClass priority);
    Task<bool> SetProcessPriorityAsync(int processId, ProcessPriorityClass priority);
    Task<bool> SetCpuAffinityAsync(string processName, int[] cpuCores);
    Task<bool> SetCpuAffinityAsync(int processId, int[] cpuCores);
    Task<bool> OptimizeCurrentProcessAsync(ProcessPriorityConfiguration config);
    Task<PerformanceMetrics> GetProcessMetricsAsync(string processName);
    Task<PerformanceMetrics> GetProcessMetricsAsync(int processId);
    Task<bool> ApplySystemOptimizationsAsync(SystemOptimizationSettings settings);
    Task<Dictionary<string, object>> GetSystemPerformanceAsync();
    Task<bool> SetTimerResolutionAsync(int milliseconds);
    Task<bool> EnableLowLatencyGcAsync();
    Task<bool> PreJitCriticalMethodsAsync();
    Task<bool> OptimizeMemoryUsageAsync();
}

public interface IProcessManager
{
    Task<Process[]> GetTradingProcessesAsync();
    Task<bool> StartProcessWithOptimizationAsync(string processPath, ProcessPriorityConfiguration config);
    Task<bool> KillProcessGracefullyAsync(string processName, TimeSpan timeout);
    Task<bool> RestartProcessWithOptimizationAsync(string processName, ProcessPriorityConfiguration config);
    Task<Dictionary<string, PerformanceMetrics>> MonitorProcessesAsync(string[] processNames);
}

public interface ISystemMonitor
{
    event EventHandler<PerformanceMetrics> PerformanceThresholdExceeded;
    Task StartMonitoringAsync(TimeSpan interval);
    Task StopMonitoringAsync();
    Task<bool> IsSystemOptimalForTradingAsync();
    Task<Dictionary<string, double>> GetRealTimeMetricsAsync();
    Task<bool> CheckCriticalResourcesAsync();
}