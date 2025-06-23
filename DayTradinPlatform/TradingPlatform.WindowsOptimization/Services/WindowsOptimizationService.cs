using System.Diagnostics;
using System.Management;
using System.Runtime;
using System.Runtime.InteropServices;
using TradingPlatform.Core.Interfaces;
using Microsoft.Extensions.Options;
using TradingPlatform.WindowsOptimization.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.WindowsOptimization.Services;

public class WindowsOptimizationService : IWindowsOptimizationService
{
    private readonly ITradingLogger _logger;
    private readonly ProcessPriorityConfiguration _config;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessAffinityMask(IntPtr hProcess, UIntPtr dwProcessAffinityMask);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern int timeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern int timeEndPeriod(uint uPeriod);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemInfo(out SystemInfo lpSystemInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemInfo
    {
        public ushort ProcessorArchitecture;
        public ushort Reserved;
        public uint PageSize;
        public IntPtr MinimumApplicationAddress;
        public IntPtr MaximumApplicationAddress;
        public IntPtr ActiveProcessorMask;
        public uint NumberOfProcessors;
        public uint ProcessorType;
        public uint AllocationGranularity;
        public ushort ProcessorLevel;
        public ushort ProcessorRevision;
    }

    private const uint REALTIME_PRIORITY_CLASS = 0x00000100;
    private const uint HIGH_PRIORITY_CLASS = 0x00000080;
    private const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000;

    public WindowsOptimizationService(
        ITradingLogger logger,
        IOptions<ProcessPriorityConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public async Task<bool> SetProcessPriorityAsync(string processName, ProcessPriorityClass priority)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                TradingLogOrchestrator.Instance.LogWarning($"No processes found with name: {processName}");
                return false;
            }

            var success = true;
            foreach (var process in processes)
            {
                if (!await SetProcessPriorityAsync(process.Id, priority))
                {
                    success = false;
                }
            }

            return success;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to set process priority for {processName}", ex);
            return false;
        }
    }

    public async Task<bool> SetProcessPriorityAsync(int processId, ProcessPriorityClass priority)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            process.PriorityClass = priority;

            // Use native Windows API for REALTIME priority
            if (priority == ProcessPriorityClass.RealTime)
            {
                var result = SetPriorityClass(process.Handle, REALTIME_PRIORITY_CLASS);
                if (!result)
                {
                    TradingLogOrchestrator.Instance.LogWarning($"Failed to set REALTIME priority for process {processId}");
                    return false;
                }
            }

            TradingLogOrchestrator.Instance.LogInfo($"Set process {processId} priority to {priority}");
            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to set process priority for process {processId}", ex);
            return false;
        }
    }

    public async Task<bool> SetCpuAffinityAsync(string processName, int[] cpuCores)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                TradingLogOrchestrator.Instance.LogWarning($"No processes found with name: {processName}");
                return false;
            }

            var success = true;
            foreach (var process in processes)
            {
                if (!await SetCpuAffinityAsync(process.Id, cpuCores))
                {
                    success = false;
                }
            }

            return success;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to set CPU affinity for {processName}", ex);
            return false;
        }
    }

    public async Task<bool> SetCpuAffinityAsync(int processId, int[] cpuCores)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            
            // Calculate affinity mask from CPU core array
            UIntPtr affinityMask = CalculateAffinityMask(cpuCores);
            
            var result = SetProcessAffinityMask(process.Handle, affinityMask);
            if (!result)
            {
                TradingLogOrchestrator.Instance.LogWarning($"Failed to set CPU affinity for process {processId}");
                return false;
            }

            TradingLogOrchestrator.Instance.LogInfo($"Set CPU affinity for process {processId} to cores: {string.Join(", ", cpuCores)}");
            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to set CPU affinity for process {processId}", ex);
            return false;
        }
    }

    public async Task<bool> OptimizeCurrentProcessAsync(ProcessPriorityConfiguration config)
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var success = true;

            // Set process priority
            if (!await SetProcessPriorityAsync(currentProcess.Id, config.Priority))
            {
                success = false;
            }

            // Set CPU affinity if specified
            if (config.CpuCoreAffinity != null && config.CpuCoreAffinity.Length > 0)
            {
                if (!await SetCpuAffinityAsync(currentProcess.Id, config.CpuCoreAffinity))
                {
                    success = false;
                }
            }

            // Enable low-latency GC
            if (config.EnableLowLatencyGc)
            {
                if (!await EnableLowLatencyGcAsync())
                {
                    success = false;
                }
            }

            // Pre-JIT critical methods
            if (config.PreJitMethods)
            {
                if (!await PreJitCriticalMethodsAsync())
                {
                    success = false;
                }
            }

            // Set working set limit
            if (config.WorkingSetLimit > 0)
            {
                var workingSetSize = new IntPtr(config.WorkingSetLimit);
                SetProcessWorkingSetSize(currentProcess.Handle, workingSetSize, workingSetSize);
            }

            TradingLogOrchestrator.Instance.LogInfo($"Current process optimization completed with config: {config}");
            return success;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to optimize current process", ex);
            return false;
        }
    }

    public async Task<PerformanceMetrics> GetProcessMetricsAsync(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                throw new ArgumentException($"No processes found with name: {processName}");
            }

            return await GetProcessMetricsAsync(processes[0].Id);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to get process metrics for {processName}", ex);
            throw;
        }
    }

    public async Task<PerformanceMetrics> GetProcessMetricsAsync(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            process.Refresh();

            return new PerformanceMetrics
            {
                Timestamp = DateTime.UtcNow,
                CpuUsagePercent = await GetProcessCpuUsageAsync(process),
                MemoryUsageBytes = process.WorkingSet64,
                WorkingSetBytes = process.WorkingSet64,
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                ProcessorTime = process.TotalProcessorTime,
                PageFaults = process.PagedMemorySize64,
                CurrentPriority = process.PriorityClass,
                ProcessorAffinity = (long)process.ProcessorAffinity,
                LatencyMicroseconds = await MeasureProcessLatencyAsync(process)
            };
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to get process metrics for process {processId}", ex);
            throw;
        }
    }

    public async Task<bool> ApplySystemOptimizationsAsync(SystemOptimizationSettings settings)
    {
        try
        {
            var success = true;

            // Set high-performance power plan
            if (settings.SetHighPerformancePowerPlan)
            {
                success &= await SetHighPerformancePowerPlanAsync();
            }

            // Set timer resolution
            if (settings.EnableHighPrecisionTimer)
            {
                success &= await SetTimerResolutionAsync(settings.TimerResolution);
            }

            // Disable hibernation
            if (settings.DisableHibernation)
            {
                success &= await DisableHibernationAsync();
            }

            // Optimize network stack
            if (settings.OptimizeNetworkStack)
            {
                success &= await OptimizeNetworkStackAsync();
            }

            TradingLogOrchestrator.Instance.LogInfo($"System optimizations applied: {settings}");
            return success;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to apply system optimizations", ex);
            return false;
        }
    }

    public async Task<Dictionary<string, object>> GetSystemPerformanceAsync()
    {
        try
        {
            var metrics = new Dictionary<string, object>();
            
            // Get system info
            GetSystemInfo(out var sysInfo);
            metrics["ProcessorCount"] = sysInfo.NumberOfProcessors;
            metrics["PageSize"] = sysInfo.PageSize;

            // Get memory info
            var totalMemory = GC.GetTotalMemory(false);
            metrics["TotalManagedMemory"] = totalMemory;
            metrics["Gen0Collections"] = GC.CollectionCount(0);
            metrics["Gen1Collections"] = GC.CollectionCount(1);
            metrics["Gen2Collections"] = GC.CollectionCount(2);

            // Get performance counters
            using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            using var memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            
            cpuCounter.NextValue(); // First call returns 0
            await Task.Delay(100);
            
            metrics["CpuUsagePercent"] = cpuCounter.NextValue();
            metrics["AvailableMemoryMB"] = memoryCounter.NextValue();

            return metrics;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to get system performance metrics", ex);
            throw;
        }
    }

    public async Task<bool> SetTimerResolutionAsync(int milliseconds)
    {
        try
        {
            var result = timeBeginPeriod((uint)milliseconds);
            if (result != 0)
            {
                TradingLogOrchestrator.Instance.LogWarning($"Failed to set timer resolution to {milliseconds}ms");
                return false;
            }

            TradingLogOrchestrator.Instance.LogInfo($"Set system timer resolution to {milliseconds}ms");
            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to set timer resolution", ex);
            return false;
        }
    }

    public async Task<bool> EnableLowLatencyGcAsync()
    {
        try
        {
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            TradingLogOrchestrator.Instance.LogInfo("Enabled low-latency garbage collection");
            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to enable low-latency GC", ex);
            return false;
        }
    }

    public async Task<bool> PreJitCriticalMethodsAsync()
    {
        try
        {
            // Force JIT compilation of critical methods
            ProfileOptimization.SetProfileRoot(Path.GetTempPath());
            ProfileOptimization.StartProfile("TradingPlatform.profile");
            
            TradingLogOrchestrator.Instance.LogInfo("Pre-JIT compilation enabled for critical methods");
            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to enable pre-JIT compilation", ex);
            return false;
        }
    }

    public async Task<bool> OptimizeMemoryUsageAsync()
    {
        try
        {
            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Compact large object heap
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();

            TradingLogOrchestrator.Instance.LogInfo("Memory optimization completed");
            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to optimize memory usage", ex);
            return false;
        }
    }

    private static UIntPtr CalculateAffinityMask(int[] cpuCores)
    {
        ulong mask = 0;
        foreach (var core in cpuCores)
        {
            if (core >= 0 && core < 64) // Maximum 64 CPU cores
            {
                mask |= (1UL << core);
            }
        }
        return new UIntPtr(mask);
    }

    private async Task<double> GetProcessCpuUsageAsync(Process process)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = process.TotalProcessorTime;
            
            await Task.Delay(100);
            
            var endTime = DateTime.UtcNow;
            var endCpuUsage = process.TotalProcessorTime;
            
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            
            return cpuUsageTotal * 100;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<double> MeasureProcessLatencyAsync(Process process)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Simulate a quick operation to measure response time
            process.Refresh();
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMicroseconds;
        }
        catch
        {
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMicroseconds;
        }
    }

    private async Task<bool> SetHighPerformancePowerPlanAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", // High Performance GUID
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            await process.WaitForExitAsync();
            
            TradingLogOrchestrator.Instance.LogInfo("Set high-performance power plan");
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to set high-performance power plan", ex);
            return false;
        }
    }

    private async Task<bool> DisableHibernationAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/hibernate off",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            await process.WaitForExitAsync();
            
            TradingLogOrchestrator.Instance.LogInfo("Disabled hibernation");
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to disable hibernation", ex);
            return false;
        }
    }

    private async Task<bool> OptimizeNetworkStackAsync()
    {
        try
        {
            // These optimizations require administrator privileges
            var networkOptimizations = new[]
            {
                "netsh int tcp set global autotuninglevel=normal",
                "netsh int tcp set global chimney=enabled",
                "netsh int tcp set global rss=enabled",
                "netsh int tcp set global netdma=enabled",
                "netsh int tcp set global dca=enabled",
                "netsh int tcp set global ecncapability=enabled"
            };

            foreach (var command in networkOptimizations)
            {
                var parts = command.Split(' ', 2);
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = parts[0],
                        Arguments = parts.Length > 1 ? parts[1] : "",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                await process.WaitForExitAsync();
            }

            TradingLogOrchestrator.Instance.LogInfo("Applied network stack optimizations");
            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to optimize network stack", ex);
            return false;
        }
    }
}
