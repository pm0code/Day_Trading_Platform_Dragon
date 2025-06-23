using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;
using TradingPlatform.Core.Logging;
using TradingPlatform.Logging.Interfaces;
using TradingPlatform.Logging.Services;

namespace TradingPlatform.Logging.Configuration;

/// <summary>
/// Centralized logging configuration for all trading platform services
/// CRITICAL: All logs must be written to centralized /logs directory with proper naming and timestamping
/// </summary>
public static class LoggingConfiguration
{
    private static readonly string LogsDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
    
    static LoggingConfiguration()
    {
        // Ensure logs directory exists
        Directory.CreateDirectory(LogsDirectory);
    }

    /// <summary>
    /// Configure comprehensive Serilog logging for trading platform
    /// Creates service-specific log files with timestamps and structured logging
    /// </summary>
    public static IHostBuilder ConfigureTradingLogging(this IHostBuilder hostBuilder, string serviceName)
    {
        return hostBuilder.UseSerilog((context, services, configuration) =>
        {
            var environment = context.HostingEnvironment.EnvironmentName;
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", serviceName)
                .Enrich.WithProperty("Environment", environment)
                .Enrich.WithProperty("MachineName", Environment.MachineName)
                .Enrich.WithProperty("ProcessId", Environment.ProcessId)
                .Enrich.WithThreadId()
                .Enrich.WithEnvironmentName()
                .Enrich.WithCorrelationId()
                
                // Console output for development and debugging
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{Service}] {SourceContext}: {Message:lj} {Properties}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Debug)
                
                // Main application log - all events
                .WriteTo.File(
                    path: Path.Combine(LogsDirectory, $"{serviceName}_{timestamp}_application.log"),
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{Service}] [{ThreadId}] {SourceContext}: {Message:lj} {Properties}{NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    restrictedToMinimumLevel: LogEventLevel.Information)
                
                // Trading operations log - trading-specific events only
                .WriteTo.Logger(lg => lg
                    .Filter.ByIncludingOnly(e => e.Properties.ContainsKey("OperationType"))
                    .WriteTo.File(
                        path: Path.Combine(LogsDirectory, $"{serviceName}_{timestamp}_trading.log"),
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{OperationType}] {Message:lj} {Properties}{NewLine}",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 60))
                
                // Performance metrics log - structured JSON for analysis
                .WriteTo.Logger(lg => lg
                    .Filter.ByIncludingOnly(e => e.MessageTemplate.Text.StartsWith("PERFORMANCE_METRIC"))
                    .WriteTo.File(
                        new CompactJsonFormatter(),
                        path: Path.Combine(LogsDirectory, $"{serviceName}_{timestamp}_performance.json"),
                        rollingInterval: RollingInterval.Hour,
                        retainedFileCountLimit: 168)) // 7 days of hourly files
                
                // Error log - errors and warnings only
                .WriteTo.Logger(lg => lg
                    .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Warning)
                    .WriteTo.File(
                        path: Path.Combine(LogsDirectory, $"{serviceName}_{timestamp}_errors.log"),
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{Service}] {SourceContext}: {Message:lj} {Properties}{NewLine}{Exception}",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 90))
                
                // Debug log - verbose debugging (only in development)
                .WriteTo.Logger(lg => lg
                    .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Debug && environment == "Development")
                    .WriteTo.File(
                        path: Path.Combine(LogsDirectory, $"{serviceName}_{timestamp}_debug.log"),
                        outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{ThreadId}] {SourceContext}.{Method}: {Message:lj} {Properties}{NewLine}",
                        rollingInterval: RollingInterval.Hour,
                        retainedFileCountLimit: 24))
                
                // Audit log - critical trading operations
                .WriteTo.Logger(lg => lg
                    .Filter.ByIncludingOnly(e => 
                        e.MessageTemplate.Text.StartsWith("ORDER_") ||
                        e.MessageTemplate.Text.StartsWith("STRATEGY_") ||
                        e.MessageTemplate.Text.StartsWith("RISK_") ||
                        e.MessageTemplate.Text.StartsWith("COMPLIANCE_"))
                    .WriteTo.File(
                        path: Path.Combine(LogsDirectory, $"{serviceName}_{timestamp}_audit.log"),
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] {Message:lj} {Properties}{NewLine}",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 365)) // Keep audit logs for 1 year
                
                // Latency monitoring log - ultra-low latency performance tracking
                .WriteTo.Logger(lg => lg
                    .Filter.ByIncludingOnly(e => 
                        e.MessageTemplate.Text.Contains("LATENCY") ||
                        e.Properties.ContainsKey("LatencyMs") ||
                        e.Properties.ContainsKey("Duration"))
                    .WriteTo.File(
                        new CompactJsonFormatter(),
                        path: Path.Combine(LogsDirectory, $"{serviceName}_{timestamp}_latency.json"),
                        rollingInterval: RollingInterval.Hour,
                        retainedFileCountLimit: 72)) // 3 days of hourly latency data
                
                // Health monitoring log - system health and metrics
                .WriteTo.Logger(lg => lg
                    .Filter.ByIncludingOnly(e => 
                        e.MessageTemplate.Text.StartsWith("SYSTEM_") ||
                        e.MessageTemplate.Text.StartsWith("HEALTH_") ||
                        e.MessageTemplate.Text.StartsWith("RESOURCE_"))
                    .WriteTo.File(
                        path: Path.Combine(LogsDirectory, $"{serviceName}_{timestamp}_health.log"),
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {Message:lj}{NewLine}",
                        rollingInterval: RollingInterval.Hour,
                        retainedFileCountLimit: 48));

            // Add Elasticsearch sink for production environments (optional)
            if (environment == "Production")
            {
                var elasticsearchUrl = context.Configuration.GetConnectionString("Elasticsearch");
                if (!string.IsNullOrEmpty(elasticsearchUrl))
                {
                    configuration.WriteTo.Elasticsearch(elasticsearchUrl, 
                        indexFormat: $"trading-{serviceName}-{DateTime.UtcNow:yyyy-MM}");
                }
            }

            // Add Seq sink for development environments (optional)
            if (environment == "Development")
            {
                var seqUrl = context.Configuration.GetConnectionString("Seq");
                if (!string.IsNullOrEmpty(seqUrl))
                {
                    configuration.WriteTo.Seq(seqUrl);
                }
            }
        });
    }

    /// <summary>
    /// Add trading logging services to dependency injection
    /// </summary>
    public static IServiceCollection AddTradingLogging(this IServiceCollection services, string serviceName)
    {
        // Register core logging services using CANONICAL TradingLogOrchestrator
        services.AddSingleton<Core.Interfaces.ITradingLogger>(provider => 
            TradingLogOrchestrator.Instance);
        services.AddSingleton<ITradingOperationsLogger>(provider => 
            new TradingLogger(serviceName));
        
        services.AddSingleton<IPerformanceLogger, PerformanceLogger>();
        
        // Add logging context enrichers
        services.AddSingleton<LoggingLevelSwitch>();
        
        return services;
    }

    /// <summary>
    /// Create log file naming convention
    /// Format: {ServiceName}_{Timestamp}_{LogType}.{Extension}
    /// Example: Gateway_2025-06-16_14-30-15_trading.log
    /// </summary>
    public static string GetLogFileName(string serviceName, string logType, string extension = "log")
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        return $"{serviceName}_{timestamp}_{logType}.{extension}";
    }

    /// <summary>
    /// Get the centralized logs directory path
    /// </summary>
    public static string GetLogsDirectory()
    {
        return LogsDirectory;
    }

    /// <summary>
    /// Clean up old log files based on retention policy
    /// </summary>
    public static void CleanupOldLogs(int retentionDays = 30)
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-retentionDays);
            var logFiles = Directory.GetFiles(LogsDirectory, "*.log", SearchOption.AllDirectories);
            
            foreach (var logFile in logFiles)
            {
                var fileInfo = new FileInfo(logFile);
                if (fileInfo.CreationTime < cutoffDate)
                {
                    File.Delete(logFile);
                    Serilog.Log.Information("Deleted old log file: {LogFile}", logFile);
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to cleanup old log files");
        }
    }

    /// <summary>
    /// Get log file statistics for monitoring
    /// </summary>
    public static Dictionary<string, object> GetLogStatistics()
    {
        try
        {
            var logFiles = Directory.GetFiles(LogsDirectory, "*.*", SearchOption.AllDirectories);
            var totalSize = logFiles.Sum(f => new FileInfo(f).Length);
            var logsByType = logFiles
                .GroupBy(f => Path.GetExtension(f))
                .ToDictionary(g => g.Key, g => g.Count());

            return new Dictionary<string, object>
            {
                ["total_files"] = logFiles.Length,
                ["total_size_mb"] = totalSize / (1024.0 * 1024.0),
                ["files_by_type"] = logsByType,
                ["logs_directory"] = LogsDirectory,
                ["last_updated"] = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to get log statistics");
            return new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["logs_directory"] = LogsDirectory
            };
        }
    }
}

/// <summary>
/// Log file naming conventions for different log types
/// </summary>
public static class LogFileNames
{
    public const string Application = "application";
    public const string Trading = "trading";
    public const string Performance = "performance";
    public const string Errors = "errors";
    public const string Debug = "debug";
    public const string Audit = "audit";
    public const string Latency = "latency";
    public const string Health = "health";
    public const string Security = "security";
    public const string Network = "network";
    public const string Database = "database";
}

/// <summary>
/// Service-specific log file prefixes
/// </summary>
public static class ServiceNames
{
    public const string Gateway = "Gateway";
    public const string MarketData = "MarketData";
    public const string StrategyEngine = "StrategyEngine";
    public const string RiskManagement = "RiskManagement";
    public const string PaperTrading = "PaperTrading";
    public const string WindowsOptimization = "WindowsOptimization";
    public const string Database = "Database";
    public const string Messaging = "Messaging";
    public const string FixEngine = "FixEngine";
    public const string DataIngestion = "DataIngestion";
    public const string Screening = "Screening";
}