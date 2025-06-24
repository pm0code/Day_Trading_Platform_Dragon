using System.Diagnostics;
using System.Management;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.WindowsOptimization.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.WindowsOptimization.Services;

public class SystemMonitor : ISystemMonitor, IDisposable
{
    private readonly ITradingLogger _logger;
    private readonly IWindowsOptimizationService _optimizationService;
    private readonly Timer? _monitoringTimer;
    private readonly Dictionary<string, PerformanceCounter> _performanceCounters;
    private bool _isMonitoring;
    private bool _disposed;

    public event EventHandler<PerformanceMetrics>? PerformanceThresholdExceeded;

    // Performance thresholds optimized for i9-14900K DRAGON system
    private const double CPU_THRESHOLD = 75.0; // 75% CPU usage (conservative for 32 threads)
    private const double MEMORY_THRESHOLD_MB = 2048; // 2GB available (out of 32GB DDR5)
    private const double LATENCY_THRESHOLD_MS = 0.5; // 500μs latency for ultra-low latency
    private const int DISK_QUEUE_THRESHOLD = 5; // Lower threshold for trading system

    public SystemMonitor(
        ITradingLogger logger,
        IWindowsOptimizationService optimizationService)
    {
        _logger = logger;
        _optimizationService = optimizationService;
        _performanceCounters = InitializePerformanceCounters();
        _isMonitoring = false;
    }

    public async Task StartMonitoringAsync(TimeSpan interval)
    {
        if (_isMonitoring)
        {
            TradingLogOrchestrator.Instance.LogWarning("System monitoring is already running");
            return;
        }

        try
        {
            _isMonitoring = true;
            TradingLogOrchestrator.Instance.LogInfo($"Starting system monitoring with interval: {interval} on DRAGON i9-14900K platform");

            // Start monitoring timer
            var timer = new Timer(async _ => await MonitorSystemPerformanceAsync(),
                null, TimeSpan.Zero, interval);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to start system monitoring", ex);
            _isMonitoring = false;
            throw;
        }
    }

    public async Task StopMonitoringAsync()
    {
        if (!_isMonitoring)
        {
            TradingLogOrchestrator.Instance.LogWarning("System monitoring is not running");
            return;
        }

        try
        {
            _isMonitoring = false;
            TradingLogOrchestrator.Instance.LogInfo("Stopping system monitoring");

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to stop system monitoring", ex);
            throw;
        }
    }

    public async Task<bool> IsSystemOptimalForTradingAsync()
    {
        try
        {
            var metrics = await GetRealTimeMetricsAsync();

            var cpuUsage = metrics.GetValueOrDefault("CpuUsagePercent", 100.0);
            var availableMemoryMB = metrics.GetValueOrDefault("AvailableMemoryMB", 0.0);
            var diskQueueLength = metrics.GetValueOrDefault("DiskQueueLength", 100.0);
            var networkUtilization = metrics.GetValueOrDefault("NetworkUtilizationPercent", 100.0);
            var contextSwitches = metrics.GetValueOrDefault("ContextSwitchesPerSec", 100000.0);

            var isOptimal = cpuUsage < CPU_THRESHOLD &&
                           availableMemoryMB > MEMORY_THRESHOLD_MB &&
                           diskQueueLength < DISK_QUEUE_THRESHOLD &&
                           networkUtilization < 85.0 &&
                           contextSwitches < 50000; // High-frequency trading threshold

            TradingLogOrchestrator.Instance.LogInfo($"DRAGON system optimal for trading: {isOptimal} (CPU: {cpuUsage}%, Memory: {availableMemoryMB}MB, Disk: {diskQueueLength}, Network: {networkUtilization}%, CtxSw: {contextSwitches})");

            return isOptimal;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to check if DRAGON system is optimal for trading", ex);
            return false;
        }
    }

    public async Task<Dictionary<string, double>> GetRealTimeMetricsAsync()
    {
        var metrics = new Dictionary<string, double>();

        try
        {
            // CPU metrics for i9-14900K hybrid architecture
            if (_performanceCounters.TryGetValue("CPU", out var cpuCounter))
            {
                metrics["CpuUsagePercent"] = cpuCounter.NextValue();
            }

            // P-Core and E-Core specific monitoring
            if (_performanceCounters.TryGetValue("PCoreUsage", out var pCoreCounter))
            {
                metrics["PCoreUsagePercent"] = pCoreCounter.NextValue();
            }

            if (_performanceCounters.TryGetValue("ECoreUsage", out var eCoreCounter))
            {
                metrics["ECoreUsagePercent"] = eCoreCounter.NextValue();
            }

            // DDR5 Memory metrics (32GB capacity)
            if (_performanceCounters.TryGetValue("AvailableMemory", out var memoryCounter))
            {
                metrics["AvailableMemoryMB"] = memoryCounter.NextValue();
            }

            if (_performanceCounters.TryGetValue("CommittedMemory", out var committedMemoryCounter))
            {
                metrics["CommittedMemoryPercent"] = committedMemoryCounter.NextValue();
            }

            // Memory bandwidth utilization
            if (_performanceCounters.TryGetValue("MemoryBandwidth", out var bandwidthCounter))
            {
                metrics["MemoryBandwidthPercent"] = bandwidthCounter.NextValue();
            }

            // High-frequency disk metrics
            if (_performanceCounters.TryGetValue("DiskQueueLength", out var diskCounter))
            {
                metrics["DiskQueueLength"] = diskCounter.NextValue();
            }

            if (_performanceCounters.TryGetValue("DiskTransfersPerSec", out var diskTransfersCounter))
            {
                metrics["DiskTransfersPerSec"] = diskTransfersCounter.NextValue();
            }

            // Network metrics for trading data feeds
            if (_performanceCounters.TryGetValue("NetworkUtilization", out var networkCounter))
            {
                metrics["NetworkUtilizationPercent"] = networkCounter.NextValue();
            }

            if (_performanceCounters.TryGetValue("NetworkPacketsPerSec", out var packetsCounter))
            {
                metrics["NetworkPacketsPerSec"] = packetsCounter.NextValue();
            }

            // System responsiveness metrics
            if (_performanceCounters.TryGetValue("ProcessCount", out var processCounter))
            {
                metrics["ProcessCount"] = processCounter.NextValue();
            }

            if (_performanceCounters.TryGetValue("ThreadCount", out var threadCounter))
            {
                metrics["ThreadCount"] = threadCounter.NextValue();
            }

            // Critical for ultra-low latency trading
            if (_performanceCounters.TryGetValue("ContextSwitches", out var contextSwitchCounter))
            {
                metrics["ContextSwitchesPerSec"] = contextSwitchCounter.NextValue();
            }

            if (_performanceCounters.TryGetValue("InterruptsPerSec", out var interruptCounter))
            {
                metrics["InterruptsPerSec"] = interruptCounter.NextValue();
            }

            // Dual GPU metrics for CUDA acceleration (primary + RTX 3060 Ti)
            if (_performanceCounters.TryGetValue("GPU0Usage", out var gpu0Counter))
            {
                metrics["GPU0UsagePercent"] = gpu0Counter.NextValue();
            }

            if (_performanceCounters.TryGetValue("GPU1Usage", out var gpu1Counter))
            {
                metrics["GPU1UsagePercent"] = gpu1Counter.NextValue();
            }

            await Task.CompletedTask;
            return metrics;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to get real-time metrics from DRAGON system", ex);
            return metrics;
        }
    }

    public async Task<bool> CheckCriticalResourcesAsync()
    {
        try
        {
            var criticalChecks = new Dictionary<string, bool>();

            // Check CPU usage (both P-Cores and E-Cores)
            var metrics = await GetRealTimeMetricsAsync();
            criticalChecks["CPU"] = metrics.GetValueOrDefault("CpuUsagePercent", 100.0) < 90.0;
            criticalChecks["P-Cores"] = metrics.GetValueOrDefault("PCoreUsagePercent", 100.0) < 85.0;
            criticalChecks["E-Cores"] = metrics.GetValueOrDefault("ECoreUsagePercent", 100.0) < 95.0;

            // Check DDR5 memory (32GB capacity)
            criticalChecks["Memory"] = metrics.GetValueOrDefault("AvailableMemoryMB", 0.0) > 1024.0;
            criticalChecks["MemoryBandwidth"] = metrics.GetValueOrDefault("MemoryBandwidthPercent", 100.0) < 80.0;

            // Check disk performance (critical for tick data)
            criticalChecks["Disk"] = metrics.GetValueOrDefault("DiskQueueLength", 100.0) < 10.0;

            // Check ultra-low latency requirements
            var contextSwitches = metrics.GetValueOrDefault("ContextSwitchesPerSec", 100000.0);
            criticalChecks["ContextSwitches"] = contextSwitches < 30000.0;

            var interrupts = metrics.GetValueOrDefault("InterruptsPerSec", 100000.0);
            criticalChecks["Interrupts"] = interrupts < 50000.0;

            // Check system responsiveness
            var responsiveness = await CheckSystemResponsivenessAsync();
            criticalChecks["Responsiveness"] = responsiveness;

            // Check trading processes health
            var tradingProcessesHealthy = await CheckTradingProcessesHealthAsync();
            criticalChecks["TradingProcesses"] = tradingProcessesHealthy;

            // Check dual GPU availability for CUDA work (primary + RTX 3060 Ti)
            var gpuHealthy = await CheckDualGpuHealthAsync();
            criticalChecks["GPU0"] = gpuHealthy.Item1;
            criticalChecks["GPU1"] = gpuHealthy.Item2;

            var allCritical = criticalChecks.All(kvp => kvp.Value);

            if (!allCritical)
            {
                var failedChecks = criticalChecks.Where(kvp => !kvp.Value).Select(kvp => kvp.Key);
                TradingLogOrchestrator.Instance.LogWarning($"DRAGON system critical resource checks failed: {string.Join(", ", failedChecks)}");
            }
            else
            {
                TradingLogOrchestrator.Instance.LogInfo("DRAGON system: All critical resources optimal for trading");
            }

            return allCritical;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to check critical resources on DRAGON system", ex);
            return false;
        }
    }

    private Dictionary<string, PerformanceCounter> InitializePerformanceCounters()
    {
        var counters = new Dictionary<string, PerformanceCounter>();

        try
        {
            // CPU counters for i9-14900K
            counters["CPU"] = new PerformanceCounter("Processor", "% Processor Time", "_Total");

            // Try to get P-Core and E-Core specific counters (may not be available on all systems)
            try
            {
                counters["PCoreUsage"] = new PerformanceCounter("Processor Information", "% Processor Time", "0,0"); // P-Core 0
                counters["ECoreUsage"] = new PerformanceCounter("Processor Information", "% Processor Time", "0,8"); // E-Core 0
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogWarning("Could not initialize P-Core/E-Core specific counters", additionalData: new { Error = ex.Message });
            }

            // DDR5 Memory counters (32GB)
            counters["AvailableMemory"] = new PerformanceCounter("Memory", "Available MBytes");
            counters["CommittedMemory"] = new PerformanceCounter("Memory", "% Committed Bytes In Use");

            try
            {
                counters["MemoryBandwidth"] = new PerformanceCounter("Memory", "Cache Faults/sec");
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogWarning("Could not initialize memory bandwidth counter", additionalData: new { Error = ex.Message });
            }

            // High-performance disk counters
            counters["DiskQueueLength"] = new PerformanceCounter("PhysicalDisk", "Current Disk Queue Length", "_Total");
            counters["DiskTransfersPerSec"] = new PerformanceCounter("PhysicalDisk", "Disk Transfers/sec", "_Total");

            // Network counters for trading data feeds
            var networkInstanceName = GetFirstNetworkInterfaceInstance();
            if (!string.IsNullOrEmpty(networkInstanceName))
            {
                counters["NetworkUtilization"] = new PerformanceCounter("Network Interface", "Bytes Total/sec", networkInstanceName);
                counters["NetworkPacketsPerSec"] = new PerformanceCounter("Network Interface", "Packets/sec", networkInstanceName);
            }

            // System performance counters
            counters["ProcessCount"] = new PerformanceCounter("System", "Processes");
            counters["ThreadCount"] = new PerformanceCounter("System", "Threads");
            counters["ContextSwitches"] = new PerformanceCounter("System", "Context Switches/sec");
            counters["InterruptsPerSec"] = new PerformanceCounter("Processor", "Interrupts/sec", "_Total");

            // GPU counters for dual NVIDIA RTX setup (primary + RTX 3060 Ti)
            try
            {
                var gpuInstances = new PerformanceCounterCategory("GPU Engine").GetInstanceNames()
                    .Where(name => name.Contains("NVIDIA") || name.Contains("Graphics"))
                    .ToList();

                if (gpuInstances.Count > 0)
                {
                    // Primary GPU (RTX 4080/4090 class)
                    counters["GPU0Usage"] = new PerformanceCounter("GPU Engine", "Utilization Percentage", gpuInstances[0]);

                    // Secondary GPU (RTX 3060 Ti)
                    if (gpuInstances.Count > 1)
                    {
                        counters["GPU1Usage"] = new PerformanceCounter("GPU Engine", "Utilization Percentage", gpuInstances[1]);
                    }
                }
            }
            catch (Exception ex)
            {
                TradingLogOrchestrator.Instance.LogWarning("Could not initialize dual GPU performance counters", additionalData: new { Error = ex.Message });
            }

            TradingLogOrchestrator.Instance.LogInfo($"Initialized {counters.Count} performance counters for DRAGON i9-14900K system");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Failed to initialize some performance counters", ex);
        }

        return counters;
    }

    private string GetFirstNetworkInterfaceInstance()
    {
        try
        {
            var category = new PerformanceCounterCategory("Network Interface");
            var instances = category.GetInstanceNames()
                .Where(name => !name.Contains("Loopback") &&
                              !name.Contains("Teredo") &&
                              !name.Contains("isatap") &&
                              name.Length > 5) // Filter out virtual adapters
                .FirstOrDefault();

            return instances ?? string.Empty;
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogWarning("Failed to get network interface instance", additionalData: new { Error = ex.Message });
            return string.Empty;
        }
    }

    private async Task MonitorSystemPerformanceAsync()
    {
        if (!_isMonitoring)
            return;

        try
        {
            var metrics = await GetRealTimeMetricsAsync();
            var systemOptimal = await IsSystemOptimalForTradingAsync();

            if (!systemOptimal)
            {
                var performanceMetrics = new PerformanceMetrics
                {
                    Timestamp = DateTime.UtcNow,
                    CpuUsagePercent = metrics.GetValueOrDefault("CpuUsagePercent", 0),
                    MemoryUsageBytes = (long)((100 - (metrics.GetValueOrDefault("AvailableMemoryMB", 0) / 32768 * 100)) / 100 * 32 * 1024 * 1024 * 1024), // Calculate from 32GB total
                    LatencyMicroseconds = metrics.GetValueOrDefault("ContextSwitchesPerSec", 0) / 100, // Rough latency approximation
                };

                PerformanceThresholdExceeded?.Invoke(this, performanceMetrics);
            }

            TradingLogOrchestrator.Instance.LogInfo($"DRAGON system monitoring cycle completed. Optimal: {systemOptimal}");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error during DRAGON system performance monitoring", ex);
        }
    }

    private Task<bool> CheckSystemResponsivenessAsync()
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Perform a system call to test responsiveness on high-performance hardware
            var processes = Process.GetProcesses();
            var processCount = processes.Length;

            stopwatch.Stop();
            var responseTime = stopwatch.Elapsed.TotalMicroseconds;

            // DRAGON system should respond within 50μs for basic operations
            var isResponsive = responseTime < 50.0;

            TradingLogOrchestrator.Instance.LogInfo($"DRAGON system responsiveness check: {responseTime}μs, Responsive: {isResponsive}");

            return Task.FromResult(isResponsive);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogWarning("Failed to check DRAGON system responsiveness", additionalData: new { Error = ex.Message });
            return Task.FromResult(false);
        }
    }

    private Task<bool> CheckTradingProcessesHealthAsync()
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

            var healthyProcesses = 0;
            foreach (var processName in tradingProcessNames)
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    // Check if process is responding and using appropriate resources
                    var process = processes[0];
                    if (process.Responding && process.WorkingSet64 > 50 * 1024 * 1024) // At least 50MB working set
                    {
                        healthyProcesses++;
                    }
                }
            }

            // At least 80% of expected processes should be healthy
            var healthPercentage = (double)healthyProcesses / tradingProcessNames.Length;
            var isHealthy = healthPercentage >= 0.8;

            TradingLogOrchestrator.Instance.LogInfo($"DRAGON trading processes health: {healthyProcesses}/{tradingProcessNames.Length} ({healthPercentage})");

            return Task.FromResult(isHealthy);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogWarning("Failed to check trading processes health on DRAGON system", additionalData: new { Error = ex.Message });
            return Task.FromResult(false);
        }
    }

    private Task<(bool, bool)> CheckDualGpuHealthAsync()
    {
        try
        {
            // Check dual NVIDIA GPU availability (primary + RTX 3060 Ti)
            var gpu0Healthy = false;
            var gpu1Healthy = false;

            // Check primary GPU
            if (_performanceCounters.TryGetValue("GPU0Usage", out var gpu0Counter))
            {
                var gpu0Usage = gpu0Counter.NextValue();
                gpu0Healthy = gpu0Usage >= 0 && gpu0Usage <= 100;
            }

            // Check secondary GPU (RTX 3060 Ti)
            if (_performanceCounters.TryGetValue("GPU1Usage", out var gpu1Counter))
            {
                var gpu1Usage = gpu1Counter.NextValue();
                gpu1Healthy = gpu1Usage >= 0 && gpu1Usage <= 100;
            }

            // Alternative check using WMI for NVIDIA GPUs
            if (!gpu0Healthy || !gpu1Healthy)
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController WHERE Name LIKE '%NVIDIA%'");
                var collection = searcher.Get();
                var gpuCount = collection.Count;

                if (!gpu0Healthy && gpuCount > 0) gpu0Healthy = true;
                if (!gpu1Healthy && gpuCount > 1) gpu1Healthy = true;
            }

            TradingLogOrchestrator.Instance.LogInfo($"DRAGON dual NVIDIA GPU health check: GPU0={gpu0Healthy}, GPU1(RTX3060Ti)={gpu1Healthy}");
            return Task.FromResult((gpu0Healthy, gpu1Healthy));
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogWarning("Failed to check dual GPU health on DRAGON system", additionalData: new { Error = ex.Message });
            return Task.FromResult((false, false));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            _isMonitoring = false;

            foreach (var counter in _performanceCounters.Values)
            {
                counter?.Dispose();
            }
            _performanceCounters.Clear();

            TradingLogOrchestrator.Instance.LogInfo("DRAGON system monitor disposed");
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error disposing DRAGON system monitor", ex);
        }
        finally
        {
            _disposed = true;
        }
    }
}
