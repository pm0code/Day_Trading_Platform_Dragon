using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TradingPlatform.WindowsOptimization.Models;

namespace TradingPlatform.WindowsOptimization.Services;

public class ProcessManager : IProcessManager
{
    private readonly ILogger<ProcessManager> _logger;
    private readonly IWindowsOptimizationService _optimizationService;

    public ProcessManager(
        ILogger<ProcessManager> logger,
        IWindowsOptimizationService optimizationService)
    {
        _logger = logger;
        _optimizationService = optimizationService;
    }

    public async Task<Process[]> GetTradingProcessesAsync()
    {
        try
        {
            var tradingProcessNames = new[]
            {
                "TradingPlatform.Gateway",
                "TradingPlatform.MarketData",
                "TradingPlatform.StrategyEngine",
                "TradingPlatform.RiskManagement",
                "TradingPlatform.PaperTrading"
            };

            var tradingProcesses = new List<Process>();
            
            foreach (var processName in tradingProcessNames)
            {
                var processes = Process.GetProcessesByName(processName);
                tradingProcesses.AddRange(processes);
            }

            _logger.LogInformation("Found {Count} trading processes", tradingProcesses.Count);
            return tradingProcesses.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get trading processes");
            return Array.Empty<Process>();
        }
    }

    public async Task<bool> StartProcessWithOptimizationAsync(string processPath, ProcessPriorityConfiguration config)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = processPath,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start process: {ProcessPath}", processPath);
                return false;
            }

            // Wait a moment for the process to initialize
            await Task.Delay(1000);

            // Apply optimizations
            var success = await _optimizationService.SetProcessPriorityAsync(process.Id, config.Priority);
            
            if (config.CpuCoreAffinity != null && config.CpuCoreAffinity.Length > 0)
            {
                success &= await _optimizationService.SetCpuAffinityAsync(process.Id, config.CpuCoreAffinity);
            }

            _logger.LogInformation("Started and optimized process: {ProcessPath} with PID: {ProcessId}", 
                processPath, process.Id);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start process with optimization: {ProcessPath}", processPath);
            return false;
        }
    }

    public async Task<bool> KillProcessGracefullyAsync(string processName, TimeSpan timeout)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                _logger.LogWarning("No processes found with name: {ProcessName}", processName);
                return true; // Already stopped
            }

            var tasks = processes.Select(async process =>
            {
                try
                {
                    // Try graceful shutdown first
                    if (!process.CloseMainWindow())
                    {
                        _logger.LogWarning("Failed to send close message to process {ProcessId}", process.Id);
                    }

                    // Wait for graceful shutdown
                    if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                    {
                        _logger.LogWarning("Process {ProcessId} did not exit gracefully, forcing termination", process.Id);
                        process.Kill();
                        await process.WaitForExitAsync();
                    }

                    _logger.LogInformation("Successfully terminated process: {ProcessName} (PID: {ProcessId})", 
                        processName, process.Id);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to terminate process {ProcessId}", process.Id);
                    return false;
                }
            });

            var results = await Task.WhenAll(tasks);
            return results.All(r => r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to kill processes gracefully: {ProcessName}", processName);
            return false;
        }
    }

    public async Task<bool> RestartProcessWithOptimizationAsync(string processName, ProcessPriorityConfiguration config)
    {
        try
        {
            // Get the process executable path before killing
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                _logger.LogWarning("No processes found with name: {ProcessName}", processName);
                return false;
            }

            var processPath = processes[0].MainModule?.FileName;
            if (string.IsNullOrEmpty(processPath))
            {
                _logger.LogError("Could not determine executable path for process: {ProcessName}", processName);
                return false;
            }

            // Kill existing processes
            var killSuccess = await KillProcessGracefullyAsync(processName, TimeSpan.FromSeconds(10));
            if (!killSuccess)
            {
                _logger.LogError("Failed to kill existing processes: {ProcessName}", processName);
                return false;
            }

            // Wait a moment before restarting
            await Task.Delay(2000);

            // Start with optimization
            var startSuccess = await StartProcessWithOptimizationAsync(processPath, config);
            if (!startSuccess)
            {
                _logger.LogError("Failed to restart process with optimization: {ProcessName}", processName);
                return false;
            }

            _logger.LogInformation("Successfully restarted process with optimization: {ProcessName}", processName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart process with optimization: {ProcessName}", processName);
            return false;
        }
    }

    public async Task<Dictionary<string, PerformanceMetrics>> MonitorProcessesAsync(string[] processNames)
    {
        var metrics = new Dictionary<string, PerformanceMetrics>();

        try
        {
            var tasks = processNames.Select(async processName =>
            {
                try
                {
                    var processMetrics = await _optimizationService.GetProcessMetricsAsync(processName);
                    return new { ProcessName = processName, Metrics = processMetrics };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get metrics for process: {ProcessName}", processName);
                    return new { ProcessName = processName, Metrics = (PerformanceMetrics?)null };
                }
            });

            var results = await Task.WhenAll(tasks);
            
            foreach (var result in results)
            {
                if (result.Metrics != null)
                {
                    metrics[result.ProcessName] = result.Metrics;
                }
            }

            _logger.LogDebug("Collected metrics for {Count} processes", metrics.Count);
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to monitor processes");
            return metrics;
        }
    }
}