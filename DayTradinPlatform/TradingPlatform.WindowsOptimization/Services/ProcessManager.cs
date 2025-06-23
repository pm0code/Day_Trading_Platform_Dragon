using System.Diagnostics;
using System.Runtime.InteropServices;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.WindowsOptimization.Models;

namespace TradingPlatform.WindowsOptimization.Services;

/// <summary>
/// High-performance process management service for Windows 11 optimization
/// Manages CPU affinity, priority levels, and resource allocation for trading services
/// </summary>
public class ProcessManager : IProcessManager
{
    private readonly ITradingLogger _logger;
    private readonly Dictionary<string, Process> _managedProcesses;
    private readonly Dictionary<string, ProcessConfiguration> _processConfigurations;
    private readonly object _processLock = new();

    public ProcessManager(ITradingLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _managedProcesses = new Dictionary<string, Process>();
        _processConfigurations = new Dictionary<string, ProcessConfiguration>();
        
        TradingLogOrchestrator.Instance.LogInfo("ProcessManager initialized for Windows optimization");
    }

    /// <summary>
    /// Start a process with optimized settings for trading performance
    /// </summary>
    public async Task<bool> StartProcessAsync(string processName, ProcessConfiguration configuration)
    {
        try
        {
            lock (_processLock)
            {
                if (_managedProcesses.ContainsKey(processName) && !_managedProcesses[processName].HasExited)
                {
                    TradingLogOrchestrator.Instance.LogWarning($"Process {processName} is already running", 
                        impact: "Start request ignored",
                        recommendedAction: "Check process status before starting",
                        additionalData: new { ProcessName = processName });
                    return false;
                }
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = configuration.ExecutablePath,
                Arguments = configuration.Arguments,
                UseShellExecute = false,
                CreateNoWindow = configuration.RunInBackground,
                WorkingDirectory = configuration.WorkingDirectory
            };

            var process = Process.Start(startInfo);
            if (process == null)
            {
                TradingLogOrchestrator.Instance.LogError($"Failed to start process {processName}", 
                    operationContext: "Process start",
                    additionalData: new { ProcessName = processName, Configuration = configuration });
                return false;
            }

            // Apply Windows optimizations
            await ApplyProcessOptimizationsAsync(process, configuration);

            lock (_processLock)
            {
                _managedProcesses[processName] = process;
                _processConfigurations[processName] = configuration;
            }

            TradingLogOrchestrator.Instance.LogInfo($"Started process {processName} with PID {process.Id}", 
                new { ProcessName = processName, ProcessId = process.Id, Configuration = configuration });

            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error starting process {processName}", ex,
                operationContext: "Process start",
                userImpact: "Service may not be available",
                troubleshootingHints: "Check executable path and permissions",
                additionalData: new { ProcessName = processName, Configuration = configuration });
            return false;
        }
    }

    /// <summary>
    /// Stop a managed process gracefully
    /// </summary>
    public async Task<bool> StopProcessAsync(string processName, int timeoutMs = 5000)
    {
        try
        {
            Process process;
            lock (_processLock)
            {
                if (!_managedProcesses.TryGetValue(processName, out process!) || process.HasExited)
                {
                    TradingLogOrchestrator.Instance.LogWarning($"Process {processName} is not running",
                        impact: "Stop request ignored",
                        additionalData: new { ProcessName = processName });
                    return true;
                }
            }

            // Try graceful shutdown first
            process.CloseMainWindow();
            var stopped = await Task.Run(() => process.WaitForExit(timeoutMs));

            if (!stopped)
            {
                // Force termination if graceful shutdown failed
                TradingLogOrchestrator.Instance.LogWarning($"Process {processName} did not stop gracefully, forcing termination",
                    impact: "Process will be forcefully terminated",
                    additionalData: new { ProcessName = processName, ProcessId = process.Id });
                process.Kill();
                await Task.Run(() => process.WaitForExit(1000));
            }

            lock (_processLock)
            {
                _managedProcesses.Remove(processName);
                _processConfigurations.Remove(processName);
            }

            TradingLogOrchestrator.Instance.LogInfo($"Stopped process {processName}",
                new { ProcessName = processName, GracefulShutdown = stopped });

            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error stopping process {processName}", ex,
                operationContext: "Process stop",
                troubleshootingHints: "Process may need manual termination",
                additionalData: new { ProcessName = processName });
            return false;
        }
    }

    /// <summary>
    /// Apply Windows-specific optimizations to a process
    /// </summary>
    private async Task ApplyProcessOptimizationsAsync(Process process, ProcessConfiguration configuration)
    {
        await Task.Run(() =>
        {
            try
            {
                // Set process priority
                if (configuration.Priority != ProcessPriorityClass.Normal)
                {
                    process.PriorityClass = configuration.Priority;
                    TradingLogOrchestrator.Instance.LogInfo($"Set process priority to {configuration.Priority} for PID {process.Id}",
                        new { Priority = configuration.Priority, ProcessId = process.Id });
                }

                // Set CPU affinity if specified
                if (configuration.CpuAffinity != null && configuration.CpuAffinity.Length > 0)
                {
                    var affinityMask = 0;
                    foreach (var cpu in configuration.CpuAffinity)
                    {
                        affinityMask |= 1 << cpu;
                    }
                    process.ProcessorAffinity = new IntPtr(affinityMask);
                    TradingLogOrchestrator.Instance.LogInfo($"Set CPU affinity mask to {affinityMask} for PID {process.Id}",
                        new { AffinityMask = affinityMask, ProcessId = process.Id, CPUs = configuration.CpuAffinity });
                }

                // Boost working set size for low latency
                if (configuration.BoostWorkingSet)
                {
                    SetProcessWorkingSetSize(process.Handle, new IntPtr(configuration.MinWorkingSetMB * 1024 * 1024),
                        new IntPtr(configuration.MaxWorkingSetMB * 1024 * 1024));
                    TradingLogOrchestrator.Instance.LogInfo($"Boosted working set to {configuration.MinWorkingSetMB}MB-{configuration.MaxWorkingSetMB}MB for PID {process.Id}",
                        new { Min = configuration.MinWorkingSetMB, Max = configuration.MaxWorkingSetMB, ProcessId = process.Id });
                }
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError("Error applying process optimizations", ex,
                    operationContext: "Process optimization",
                    additionalData: new { ProcessId = process.Id, Configuration = configuration });
            }
        });
    }

    /// <summary>
    /// Get status of all managed processes
    /// </summary>
    public async Task<Dictionary<string, ProcessStatus>> GetProcessStatusesAsync()
    {
        var statuses = new Dictionary<string, ProcessStatus>();

        await Task.Run(() =>
        {
            lock (_processLock)
            {
                foreach (var (name, process) in _managedProcesses.ToList())
                {
                    try
                    {
                        if (process.HasExited)
                        {
                            statuses[name] = new ProcessStatus
                            {
                                ProcessName = name,
                                IsRunning = false,
                                ProcessId = process.Id,
                                ExitCode = process.ExitCode,
                                ExitTime = process.ExitTime
                            };
                            _managedProcesses.Remove(name);
                        }
                        else
                        {
                            process.Refresh();
                            statuses[name] = new ProcessStatus
                            {
                                ProcessName = name,
                                IsRunning = true,
                                ProcessId = process.Id,
                                CpuUsagePercent = GetProcessCpuUsage(process),
                                MemoryUsageMB = process.WorkingSet64 / (1024 * 1024),
                                ThreadCount = process.Threads.Count,
                                HandleCount = process.HandleCount,
                                StartTime = process.StartTime,
                                TotalProcessorTime = process.TotalProcessorTime
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        TradingLogOrchestrator.Instance.LogError($"Error getting status for process {name}", ex,
                            operationContext: "Process status check",
                            additionalData: new { ProcessName = name });
                    }
                }
            }
        });

        return statuses;
    }

    /// <summary>
    /// Monitor process performance metrics
    /// </summary>
    public async Task<ProcessPerformanceMetrics> GetProcessMetricsAsync(string processName)
    {
        Process process;
        lock (_processLock)
        {
            if (!_managedProcesses.TryGetValue(processName, out process!) || process.HasExited)
            {
                return new ProcessPerformanceMetrics { ProcessName = processName, IsRunning = false };
            }
        }

        return await Task.Run(() =>
        {
            try
            {
                process.Refresh();
                return new ProcessPerformanceMetrics
                {
                    ProcessName = processName,
                    IsRunning = true,
                    ProcessId = process.Id,
                    CpuUsagePercent = GetProcessCpuUsage(process),
                    MemoryUsageMB = process.WorkingSet64 / (1024 * 1024),
                    PrivateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024),
                    VirtualMemoryMB = process.VirtualMemorySize64 / (1024 * 1024),
                    ThreadCount = process.Threads.Count,
                    HandleCount = process.HandleCount,
                    GdiObjects = GetGdiObjectCount(process),
                    UserObjects = GetUserObjectCount(process),
                    PageFaultsPerSec = GetPageFaultsPerSecond(process)
                };
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError($"Error getting metrics for process {processName}", ex,
                    operationContext: "Process metrics",
                    additionalData: new { ProcessName = processName });
                return new ProcessPerformanceMetrics { ProcessName = processName, IsRunning = false };
            }
        });
    }

    // Windows API imports
    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);

    [DllImport("user32.dll")]
    private static extern int GetGuiResources(IntPtr hProcess, int uiFlags);

    private const int GR_GDIOBJECTS = 0;
    private const int GR_USEROBJECTS = 1;

    private double GetProcessCpuUsage(Process process)
    {
        // Simple CPU usage calculation - in production, use performance counters
        return 0.0;
    }

    private int GetGdiObjectCount(Process process)
    {
        return GetGuiResources(process.Handle, GR_GDIOBJECTS);
    }

    private int GetUserObjectCount(Process process)
    {
        return GetGuiResources(process.Handle, GR_USEROBJECTS);
    }

    private double GetPageFaultsPerSecond(Process process)
    {
        // In production, track delta over time
        return 0.0;
    }

    /// <summary>
    /// Get all trading-related processes
    /// </summary>
    public async Task<Process[]> GetTradingProcessesAsync()
    {
        return await Task.Run(() =>
        {
            var tradingProcessNames = new[] 
            { 
                "TradingPlatform.Gateway",
                "TradingPlatform.FixEngine", 
                "TradingPlatform.MarketData",
                "TradingPlatform.StrategyEngine",
                "TradingPlatform.RiskManagement",
                "TradingPlatform.PaperTrading"
            };

            var processes = new List<Process>();
            foreach (var name in tradingProcessNames)
            {
                try
                {
                    var procs = Process.GetProcessesByName(name);
                    processes.AddRange(procs);
                }
                catch (Exception ex)
                {
                    TradingLogOrchestrator.Instance.LogWarning($"Could not get processes for {name}", 
                        impact: "Process monitoring incomplete",
                        recommendedAction: "Check process permissions",
                        additionalData: new { ProcessName = name, Error = ex.Message });
                }
            }

            lock (_processLock)
            {
                processes.AddRange(_managedProcesses.Values.Where(p => !p.HasExited));
            }

            return processes.Distinct().ToArray();
        });
    }

    /// <summary>
    /// Start a process with optimization configuration
    /// </summary>
    public async Task<bool> StartProcessWithOptimizationAsync(string processPath, ProcessPriorityConfiguration config)
    {
        try
        {
            var processConfig = new ProcessConfiguration
            {
                ExecutablePath = processPath,
                Priority = config.Priority,
                CpuAffinity = config.CpuCoreAffinity,
                BoostWorkingSet = config.WorkingSetLimit > 0,
                MinWorkingSetMB = config.WorkingSetLimit > 0 ? (int)(config.WorkingSetLimit / (1024 * 1024)) / 2 : 50,
                MaxWorkingSetMB = config.WorkingSetLimit > 0 ? (int)(config.WorkingSetLimit / (1024 * 1024)) : 500
            };

            var processName = Path.GetFileNameWithoutExtension(processPath);
            await StartProcessAsync(processName, processConfig);
            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to start process with optimization: {processPath}", ex);
            return false;
        }
    }

    /// <summary>
    /// Kill a process gracefully with timeout
    /// </summary>
    public async Task<bool> KillProcessGracefullyAsync(string processName, TimeSpan timeout)
    {
        try
        {
            Process process;
            lock (_processLock)
            {
                if (!_managedProcesses.TryGetValue(processName, out process!))
                {
                    // Try to find by name
                    var processes = Process.GetProcessesByName(processName);
                    if (processes.Length == 0)
                    {
                        return true; // Already not running
                    }
                    process = processes[0];
                }
            }

            if (process.HasExited)
            {
                return true;
            }

            // Try graceful shutdown first
            process.CloseMainWindow();
            var gracefulExit = await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds));

            if (!gracefulExit)
            {
                // Force kill if graceful shutdown failed
                process.Kill();
                await Task.Run(() => process.WaitForExit(5000));
            }

            lock (_processLock)
            {
                _managedProcesses.Remove(processName);
            }

            return true;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error killing process {processName}", ex);
            return false;
        }
    }

    /// <summary>
    /// Restart a process with optimization
    /// </summary>
    public async Task<bool> RestartProcessWithOptimizationAsync(string processName, ProcessPriorityConfiguration config)
    {
        try
        {
            // Kill existing process
            await KillProcessGracefullyAsync(processName, TimeSpan.FromSeconds(30));

            // Wait a bit to ensure clean shutdown
            await Task.Delay(1000);

            // Find the executable path
            string processPath = processName;
            lock (_processLock)
            {
                if (_processConfigurations.TryGetValue(processName, out var existingConfig))
                {
                    processPath = existingConfig.ExecutablePath;
                }
            }

            // Start with new configuration
            return await StartProcessWithOptimizationAsync(processPath, config);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Failed to restart process {processName}", ex);
            return false;
        }
    }

    /// <summary>
    /// Monitor multiple processes and return their performance metrics
    /// </summary>
    public async Task<Dictionary<string, PerformanceMetrics>> MonitorProcessesAsync(string[] processNames)
    {
        var metrics = new Dictionary<string, PerformanceMetrics>();

        foreach (var name in processNames)
        {
            try
            {
                var processMetrics = await GetProcessMetricsAsync(name);
                metrics[name] = new PerformanceMetrics
                {
                    CpuUsagePercent = processMetrics.CpuUsagePercent,
                    MemoryUsageBytes = processMetrics.MemoryUsageMB * 1024 * 1024,
                    ThreadCount = processMetrics.ThreadCount,
                    HandleCount = processMetrics.HandleCount,
                    WorkingSetBytes = processMetrics.MemoryUsageMB * 1024 * 1024,
                    LatencyMicroseconds = 0 // Would need actual measurement
                };
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogWarning($"Could not get metrics for {name}",
                    impact: "Incomplete performance monitoring",
                    additionalData: new { ProcessName = name, Error = ex.Message });
                    
                metrics[name] = new PerformanceMetrics
                {
                    CpuUsagePercent = 100,
                    MemoryUsageBytes = 0
                };
            }
        }

        return metrics;
    }
}

/// <summary>
/// Process configuration for optimization settings
/// </summary>
public class ProcessConfiguration
{
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public bool RunInBackground { get; set; } = true;
    public ProcessPriorityClass Priority { get; set; } = ProcessPriorityClass.Normal;
    public int[]? CpuAffinity { get; set; }
    public bool BoostWorkingSet { get; set; }
    public int MinWorkingSetMB { get; set; } = 50;
    public int MaxWorkingSetMB { get; set; } = 500;
}

/// <summary>
/// Process status information
/// </summary>
public class ProcessStatus
{
    public string ProcessName { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public int ProcessId { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageMB { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public DateTime? StartTime { get; set; }
    public TimeSpan? TotalProcessorTime { get; set; }
    public int? ExitCode { get; set; }
    public DateTime? ExitTime { get; set; }
}

/// <summary>
/// Detailed process performance metrics
/// </summary>
public class ProcessPerformanceMetrics
{
    public string ProcessName { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public int ProcessId { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageMB { get; set; }
    public long PrivateMemoryMB { get; set; }
    public long VirtualMemoryMB { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public int GdiObjects { get; set; }
    public int UserObjects { get; set; }
    public double PageFaultsPerSec { get; set; }
}

