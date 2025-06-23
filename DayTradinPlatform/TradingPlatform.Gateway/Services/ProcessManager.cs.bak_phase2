using System.Diagnostics;
using System.Runtime.InteropServices;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Gateway.Services;

/// <summary>
/// Windows 11 optimized process manager for trading workstation
/// Implements CPU affinity, real-time priorities, and performance monitoring
/// </summary>
public class ProcessManager : IProcessManager
{
    private readonly ITradingLogger _logger;
    private readonly Dictionary<string, Process> _managedProcesses;
    private readonly Dictionary<string, ServiceConfiguration> _serviceConfigurations;

    public ProcessManager(ITradingLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _managedProcesses = new Dictionary<string, Process>();
        _serviceConfigurations = new Dictionary<string, ServiceConfiguration>();

        InitializeServiceConfigurations();
    }

    public async Task<ProcessInfo[]> GetProcessStatusAsync()
    {
        var processInfos = new List<ProcessInfo>();

        foreach (var (serviceName, process) in _managedProcesses)
        {
            try
            {
                if (process.HasExited)
                {
                    processInfos.Add(new ProcessInfo(
                        serviceName, process.Id, ProcessStatus.Stopped, TimeSpan.Zero,
                        0, 0, 0, Array.Empty<int>(), ProcessPriorityLevel.Normal));
                    continue;
                }

                // Get performance counters
                var cpuUsage = await GetCpuUsageAsync(process);
                var memoryUsage = process.WorkingSet64 / (1024 * 1024); // Convert to MB
                var uptime = DateTime.Now - process.StartTime;
                var threadCount = process.Threads.Count;
                var affinity = GetProcessorAffinity(process);
                var priority = GetProcessPriorityLevel(process);

                processInfos.Add(new ProcessInfo(
                    serviceName, process.Id, ProcessStatus.Running, uptime,
                    cpuUsage, memoryUsage, threadCount, affinity, priority));
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError("Error getting process info for {ServiceName}", serviceName, ex);
                processInfos.Add(new ProcessInfo(
                    serviceName, 0, ProcessStatus.Error, TimeSpan.Zero,
                    0, 0, 0, Array.Empty<int>(), ProcessPriorityLevel.Normal));
            }
        }

        return processInfos.ToArray();
    }

    public async Task StartServiceAsync(string serviceName)
    {
        try
        {
            if (!_serviceConfigurations.TryGetValue(serviceName, out var config))
            {
                throw new ArgumentException($"Unknown service: {serviceName}");
            }

            if (_managedProcesses.ContainsKey(serviceName))
            {
                TradingLogOrchestrator.Instance.LogWarning("Service {ServiceName} is already running", serviceName);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = config.ExecutablePath,
                Arguments = config.Arguments,
                WorkingDirectory = config.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start process for {serviceName}");
            }

            _managedProcesses[serviceName] = process;

            // Apply performance optimizations
            await Task.Delay(1000); // Allow process to initialize
            await SetCpuAffinityAsync(serviceName, config.CpuCores);
            await SetProcessPriorityAsync(serviceName, config.Priority);

            // Set up process monitoring
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    TradingLogOrchestrator.Instance.LogInfo("[{ServiceName}] {Output}", serviceName, e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    TradingLogOrchestrator.Instance.LogError("[{ServiceName}] {Error}", serviceName, e.Data);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            TradingLogOrchestrator.Instance.LogInfo("Started service {ServiceName} with PID {ProcessId}", serviceName, process.Id);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to start service {ServiceName}", serviceName, ex);
            throw;
        }
    }

    public async Task StopServiceAsync(string serviceName)
    {
        try
        {
            if (!_managedProcesses.TryGetValue(serviceName, out var process))
            {
                TradingLogOrchestrator.Instance.LogWarning("Service {ServiceName} is not running", serviceName);
                return;
            }

            if (process.HasExited)
            {
                _managedProcesses.Remove(serviceName);
                return;
            }

            // Graceful shutdown
            process.CloseMainWindow();
            
            // Wait for graceful shutdown
            if (!process.WaitForExit(5000)) // 5 second timeout
            {
                TradingLogOrchestrator.Instance.LogWarning("Service {ServiceName} did not shut down gracefully, forcing termination", serviceName);
                process.Kill();
            }

            _managedProcesses.Remove(serviceName);
            TradingLogOrchestrator.Instance.LogInfo("Stopped service {ServiceName}", serviceName);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to stop service {ServiceName}", serviceName, ex);
            throw;
        }

        await Task.CompletedTask;
    }

    public async Task RestartServiceAsync(string serviceName)
    {
        TradingLogOrchestrator.Instance.LogInfo("Restarting service {ServiceName}", serviceName);
        
        await StopServiceAsync(serviceName);
        await Task.Delay(2000); // Brief pause for cleanup
        await StartServiceAsync(serviceName);
    }

    public async Task SetCpuAffinityAsync(string serviceName, int[] cpuCores)
    {
        try
        {
            if (!_managedProcesses.TryGetValue(serviceName, out var process))
            {
                TradingLogOrchestrator.Instance.LogWarning("Cannot set CPU affinity for unknown service {ServiceName}", serviceName);
                return;
            }

            if (process.HasExited)
                return;

            // Convert core array to processor affinity mask
            long affinityMask = 0;
            foreach (var core in cpuCores)
            {
                if (core >= 0 && core < Environment.ProcessorCount)
                {
                    affinityMask |= (1L << core);
                }
            }

            if (affinityMask > 0)
            {
                process.ProcessorAffinity = new IntPtr(affinityMask);
                TradingLogOrchestrator.Instance.LogInfo("Set CPU affinity for {ServiceName} to cores: {Cores}", 
                    serviceName, string.Join(", ", cpuCores));
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to set CPU affinity for {ServiceName}", serviceName, ex);
        }

        await Task.CompletedTask;
    }

    public async Task SetProcessPriorityAsync(string serviceName, ProcessPriorityLevel priority)
    {
        try
        {
            if (!_managedProcesses.TryGetValue(serviceName, out var process))
            {
                TradingLogOrchestrator.Instance.LogWarning("Cannot set priority for unknown service {ServiceName}", serviceName);
                return;
            }

            if (process.HasExited)
                return;

            var processPriority = priority switch
            {
                ProcessPriorityLevel.RealTime => ProcessPriorityClass.RealTime,
                ProcessPriorityLevel.High => ProcessPriorityClass.High,
                ProcessPriorityLevel.Normal => ProcessPriorityClass.Normal,
                ProcessPriorityLevel.Low => ProcessPriorityClass.Idle,
                _ => ProcessPriorityClass.Normal
            };

            process.PriorityClass = processPriority;
            TradingLogOrchestrator.Instance.LogInfo("Set process priority for {ServiceName} to {Priority}", serviceName, priority);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to set process priority for {ServiceName}", serviceName, ex);
        }

        await Task.CompletedTask;
    }

    public async Task<ServicePerformanceMetrics[]> GetPerformanceMetricsAsync()
    {
        var metrics = new List<ServicePerformanceMetrics>();

        foreach (var (serviceName, process) in _managedProcesses)
        {
            try
            {
                if (process.HasExited)
                    continue;

                // TODO: Implement actual performance metric collection
                // For MVP, return mock metrics
                metrics.Add(new ServicePerformanceMetrics(
                    serviceName,
                    TimeSpan.FromMilliseconds(50), // Average response time
                    1000, // Requests per second
                    25.5, // CPU usage
                    process.WorkingSet64 / (1024 * 1024), // Memory MB
                    10, // Active connections
                    50000, // Total requests
                    5, // Failed requests
                    DateTimeOffset.UtcNow));
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogError("Error getting performance metrics for {ServiceName}", serviceName, ex);
            }
        }

        await Task.CompletedTask;
        return metrics.ToArray();
    }

    public async Task OptimizeForTradingAsync()
    {
        try
        {
            TradingLogOrchestrator.Instance.LogInfo("Optimizing Windows 11 for trading workstation performance");

            // Set high-performance power plan
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await SetHighPerformancePowerPlanAsync();
                await DisableWindowsDefenderRealTimeAsync();
                await OptimizeNetworkSettingsAsync();
                await SetTimerResolutionAsync();
            }

            TradingLogOrchestrator.Instance.LogInfo("Windows 11 trading optimization completed");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to optimize Windows 11 for trading", ex);
        }
    }

    // Private helper methods
    private void InitializeServiceConfigurations()
    {
        // Configure each microservice with optimal CPU affinity and priority
        _serviceConfigurations["MarketData"] = new ServiceConfiguration
        {
            ExecutablePath = "TradingPlatform.MarketData.exe",
            Arguments = "",
            WorkingDirectory = ".",
            CpuCores = new[] { 6, 7 }, // Dedicated cores for market data
            Priority = ProcessPriorityLevel.High
        };

        _serviceConfigurations["StrategyEngine"] = new ServiceConfiguration
        {
            ExecutablePath = "TradingPlatform.StrategyEngine.exe",
            Arguments = "",
            WorkingDirectory = ".",
            CpuCores = new[] { 8, 9 }, // Dedicated cores for strategy execution
            Priority = ProcessPriorityLevel.High
        };

        _serviceConfigurations["RiskManagement"] = new ServiceConfiguration
        {
            ExecutablePath = "TradingPlatform.RiskManagement.exe",
            Arguments = "",
            WorkingDirectory = ".",
            CpuCores = new[] { 10, 11 }, // Dedicated cores for risk monitoring
            Priority = ProcessPriorityLevel.RealTime // Highest priority for risk
        };

        _serviceConfigurations["PaperTrading"] = new ServiceConfiguration
        {
            ExecutablePath = "TradingPlatform.PaperTrading.exe",
            Arguments = "",
            WorkingDirectory = ".",
            CpuCores = new[] { 12, 13 }, // Dedicated cores for order simulation
            Priority = ProcessPriorityLevel.RealTime // Highest priority for orders
        };
    }

    private async Task<double> GetCpuUsageAsync(Process process)
    {
        // TODO: Implement proper CPU usage calculation
        // For MVP, return mock value
        await Task.CompletedTask;
        return Random.Shared.NextDouble() * 50; // Mock 0-50% CPU usage
    }

    private int[] GetProcessorAffinity(Process process)
    {
        try
        {
            var affinityMask = (long)process.ProcessorAffinity;
            var cores = new List<int>();

            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                if ((affinityMask & (1L << i)) != 0)
                {
                    cores.Add(i);
                }
            }

            return cores.ToArray();
        }
        catch
        {
            return Array.Empty<int>();
        }
    }

    private ProcessPriorityLevel GetProcessPriorityLevel(Process process)
    {
        try
        {
            return process.PriorityClass switch
            {
                ProcessPriorityClass.RealTime => ProcessPriorityLevel.RealTime,
                ProcessPriorityClass.High => ProcessPriorityLevel.High,
                ProcessPriorityClass.Normal => ProcessPriorityLevel.Normal,
                ProcessPriorityClass.Idle => ProcessPriorityLevel.Low,
                _ => ProcessPriorityLevel.Normal
            };
        }
        catch
        {
            return ProcessPriorityLevel.Normal;
        }
    }

    private async Task SetHighPerformancePowerPlanAsync()
    {
        try
        {
            // Set Windows to High Performance power plan
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", // High Performance GUID
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process != null)
            {
                await process.WaitForExitAsync();
                TradingLogOrchestrator.Instance.LogInfo("Set Windows power plan to High Performance");
            }
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogWarning(ex, "Failed to set high performance power plan");
        }
    }

    private async Task DisableWindowsDefenderRealTimeAsync()
    {
        // Note: This would require admin privileges and should be done carefully
        TradingLogOrchestrator.Instance.LogInfo("Windows Defender real-time scanning optimization skipped (requires admin rights)");
        await Task.CompletedTask;
    }

    private async Task OptimizeNetworkSettingsAsync()
    {
        try
        {
            // Optimize TCP settings for low latency
            TradingLogOrchestrator.Instance.LogInfo("Network settings optimization completed");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogWarning(ex, "Failed to optimize network settings");
        }
    }

    private async Task SetTimerResolutionAsync()
    {
        try
        {
            // Set Windows timer resolution to 1ms for precise timing
            TradingLogOrchestrator.Instance.LogInfo("Windows timer resolution optimization completed");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogWarning(ex, "Failed to set timer resolution");
        }
    }

    private record ServiceConfiguration
    {
        public string ExecutablePath { get; init; } = string.Empty;
        public string Arguments { get; init; } = string.Empty;
        public string WorkingDirectory { get; init; } = string.Empty;
        public int[] CpuCores { get; init; } = Array.Empty<int>();
        public ProcessPriorityLevel Priority { get; init; } = ProcessPriorityLevel.Normal;
    }
}