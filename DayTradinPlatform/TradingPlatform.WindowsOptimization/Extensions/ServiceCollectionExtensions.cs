using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.WindowsOptimization.Models;
using TradingPlatform.WindowsOptimization.Services;

namespace TradingPlatform.WindowsOptimization.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsOptimization(
        this IServiceCollection services,
        Action<ProcessPriorityConfiguration>? configureProcessPriority = null,
        Action<SystemOptimizationSettings>? configureSystemOptimization = null)
    {
        // Configure process priority for DRAGON i9-14900K system
        var processPriorityConfig = new ProcessPriorityConfiguration
        {
            ProcessName = "TradingPlatform",
            Priority = System.Diagnostics.ProcessPriorityClass.RealTime,
            CpuCoreAffinity = new[] { 4, 5 }, // Preferred P-Cores on i9-14900K
            EnableLowLatencyGc = true,
            PreJitMethods = true,
            WorkingSetLimit = 0 // No limit on 32GB DDR5 system
        };
        configureProcessPriority?.Invoke(processPriorityConfig);
        services.Configure<ProcessPriorityConfiguration>(options =>
        {
            options.ProcessName = processPriorityConfig.ProcessName;
            options.Priority = processPriorityConfig.Priority;
            options.CpuCoreAffinity = processPriorityConfig.CpuCoreAffinity;
            options.EnableLowLatencyGc = processPriorityConfig.EnableLowLatencyGc;
            options.PreJitMethods = processPriorityConfig.PreJitMethods;
            options.WorkingSetLimit = processPriorityConfig.WorkingSetLimit;
        });

        // Configure system optimization for DRAGON platform
        var systemOptimizationConfig = new SystemOptimizationSettings
        {
            DisableWindowsDefender = false, // Keep security enabled
            DisableWindowsUpdate = false,   // Keep updates enabled
            OptimizeNetworkStack = true,    // Critical for trading data feeds
            SetHighPerformancePowerPlan = true,
            DisableHibernation = true,      // Free up disk space and reduce latency
            TimerResolution = 1,            // 1ms for ultra-low latency
            EnableHighPrecisionTimer = true
        };
        configureSystemOptimization?.Invoke(systemOptimizationConfig);
        services.Configure<SystemOptimizationSettings>(options =>
        {
            options.DisableWindowsDefender = systemOptimizationConfig.DisableWindowsDefender;
            options.DisableWindowsUpdate = systemOptimizationConfig.DisableWindowsUpdate;
            options.OptimizeNetworkStack = systemOptimizationConfig.OptimizeNetworkStack;
            options.SetHighPerformancePowerPlan = systemOptimizationConfig.SetHighPerformancePowerPlan;
            options.DisableHibernation = systemOptimizationConfig.DisableHibernation;
            options.TimerResolution = systemOptimizationConfig.TimerResolution;
            options.EnableHighPrecisionTimer = systemOptimizationConfig.EnableHighPrecisionTimer;
        });

        // Register services
        services.AddSingleton<IWindowsOptimizationService, WindowsOptimizationService>();
        services.AddSingleton<IProcessManager, ProcessManager>();
        services.AddSingleton<ISystemMonitor, SystemMonitor>();

        return services;
    }

    public static IServiceCollection AddDragonSystemOptimization(this IServiceCollection services)
    {
        return services.AddWindowsOptimization(
            processPriority =>
            {
                // Optimized for DRAGON i9-14900K with 32 threads
                processPriority.Priority = System.Diagnostics.ProcessPriorityClass.RealTime;
                processPriority.CpuCoreAffinity = new[] { 4, 5, 6, 7 }; // Use preferred P-Cores
                processPriority.EnableLowLatencyGc = true;
                processPriority.PreJitMethods = true;
                processPriority.WorkingSetLimit = 8L * 1024 * 1024 * 1024; // 8GB limit on 32GB DDR5
            },
            systemOptimization =>
            {
                // DRAGON-specific optimizations
                systemOptimization.OptimizeNetworkStack = true;
                systemOptimization.SetHighPerformancePowerPlan = true;
                systemOptimization.DisableHibernation = true;
                systemOptimization.TimerResolution = 1; // 1ms for sub-millisecond trading
                systemOptimization.EnableHighPrecisionTimer = true;
            });
    }

    public static IServiceCollection AddTradingProcessOptimization(
        this IServiceCollection services,
        string[] tradingProcessNames)
    {
        return services.AddWindowsOptimization(
            processPriority =>
            {
                // Configure for specific trading processes
                processPriority.Priority = System.Diagnostics.ProcessPriorityClass.High; // High instead of RealTime for stability
                processPriority.CpuCoreAffinity = new[] { 0, 1, 2, 3 }; // Use first 4 P-Cores
                processPriority.EnableLowLatencyGc = true;
                processPriority.PreJitMethods = true;
                processPriority.WorkingSetLimit = 4L * 1024 * 1024 * 1024; // 4GB per process
            },
            systemOptimization =>
            {
                systemOptimization.OptimizeNetworkStack = true;
                systemOptimization.SetHighPerformancePowerPlan = true;
                systemOptimization.TimerResolution = 1;
                systemOptimization.EnableHighPrecisionTimer = true;
            });
    }

    public static IServiceCollection AddGpuAccelerationSupport(this IServiceCollection services)
    {
        // Placeholder for future CUDA integration with dual GPU setup
        // Will be implemented when GPU acceleration features are added

        services.Configure<ProcessPriorityConfiguration>(options =>
        {
            // Reserve some CPU cores for GPU communication
            options.CpuCoreAffinity = new[] { 0, 1, 4, 5 }; // Mix of P-Cores for GPU workloads
        });

        return services;
    }
}