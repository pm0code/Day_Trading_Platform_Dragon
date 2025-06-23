// TradingPlatform.Core.Logging.LoggingConfiguration - COMPREHENSIVE CONFIGURABLE LOGGING
// User-configurable switches: Critical/Project-specific/All methods
// Performance thresholds, verbosity levels, tiered storage configuration

using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingPlatform.Core.Logging;

/// <summary>
/// Comprehensive logging configuration with user-configurable switches and thresholds
/// Supports Critical/Project-specific/All logging modes with performance monitoring
/// </summary>
public class LoggingConfiguration
{
    #region Logging Scope Configuration
    
    /// <summary>
    /// Configurable logging scope: Critical, ProjectSpecific, All
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LoggingScope Scope { get; set; } = LoggingScope.Critical;
    
    /// <summary>
    /// Specific projects to log when Scope = ProjectSpecific
    /// </summary>
    public HashSet<string> EnabledProjects { get; set; } = new();
    
    /// <summary>
    /// Enable automatic method entry/exit logging
    /// </summary>
    public bool EnableMethodLifecycleLogging { get; set; } = false;
    
    /// <summary>
    /// Enable parameter logging in method entry/exit
    /// </summary>
    public bool EnableParameterLogging { get; set; } = false;
    
    #endregion
    
    #region Performance Threshold Configuration
    
    /// <summary>
    /// User-configurable performance thresholds
    /// </summary>
    public PerformanceThresholds Thresholds { get; set; } = new();
    
    /// <summary>
    /// Enable performance deviation alerts
    /// </summary>
    public bool EnablePerformanceAlerting { get; set; } = true;
    
    #endregion
    
    #region Environment and Verbosity
    
    /// <summary>
    /// Current environment (Development, Production)
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LoggingEnvironment Environment { get; set; } = LoggingEnvironment.Development;
    
    /// <summary>
    /// Minimum log level to process
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;
    
    /// <summary>
    /// Enable verbose diagnostic information
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = true;
    
    #endregion
    
    #region Storage Configuration
    
    /// <summary>
    /// Tiered storage configuration
    /// </summary>
    public StorageConfiguration Storage { get; set; } = new();
    
    /// <summary>
    /// Enable log compression
    /// </summary>
    public bool EnableCompression { get; set; } = true;
    
    /// <summary>
    /// Log rotation settings
    /// </summary>
    public RotationConfiguration Rotation { get; set; } = new();
    
    #endregion
    
    #region AI/ML Configuration
    
    /// <summary>
    /// Enable AI/ML anomaly detection
    /// </summary>
    public bool EnableAnomalyDetection { get; set; } = true;
    
    /// <summary>
    /// AI/ML processing configuration
    /// </summary>
    public AiConfiguration AI { get; set; } = new();
    
    #endregion
    
    #region Real-time Configuration
    
    /// <summary>
    /// Enable real-time log streaming
    /// </summary>
    public bool EnableRealTimeStreaming { get; set; } = true;
    
    /// <summary>
    /// Real-time streaming configuration
    /// </summary>
    public StreamingConfiguration Streaming { get; set; } = new();
    
    #endregion
    
    #region Default Configurations
    
    /// <summary>
    /// Create default configuration for Development environment
    /// </summary>
    public static LoggingConfiguration CreateDevelopmentDefault()
    {
        return new LoggingConfiguration
        {
            Scope = LoggingScope.All,
            Environment = LoggingEnvironment.Development,
            MinimumLevel = LogLevel.Debug,
            EnableMethodLifecycleLogging = true,
            EnableParameterLogging = true,
            EnableVerboseLogging = true,
            EnabledProjects = new HashSet<string>
            {
                "TradingPlatform.Core",
                "TradingPlatform.DataIngestion", 
                "TradingPlatform.PaperTrading",
                "TradingPlatform.RiskManagement"
            },
            Thresholds = PerformanceThresholds.CreateDevelopmentDefaults(),
            Storage = StorageConfiguration.CreateDevelopmentDefaults(),
            AI = AiConfiguration.CreateDevelopmentDefaults()
        };
    }
    
    /// <summary>
    /// Create default configuration for Production environment
    /// </summary>
    public static LoggingConfiguration CreateProductionDefault()
    {
        return new LoggingConfiguration
        {
            Scope = LoggingScope.Critical,
            Environment = LoggingEnvironment.Production,
            MinimumLevel = LogLevel.Info,
            EnableMethodLifecycleLogging = false,
            EnableParameterLogging = false,
            EnableVerboseLogging = false,
            Thresholds = PerformanceThresholds.CreateProductionDefaults(),
            Storage = StorageConfiguration.CreateProductionDefaults(),
            AI = AiConfiguration.CreateProductionDefaults()
        };
    }
    
    #endregion
}

/// <summary>
/// Logging scope enumeration
/// </summary>
public enum LoggingScope
{
    /// <summary>
    /// Log only critical trading operations and errors
    /// </summary>
    Critical,
    
    /// <summary>
    /// Log specific projects defined in EnabledProjects
    /// </summary>
    ProjectSpecific,
    
    /// <summary>
    /// Log everything across the entire platform
    /// </summary>
    All
}

/// <summary>
/// Logging environment enumeration
/// </summary>
public enum LoggingEnvironment
{
    Development,
    Production
}

/// <summary>
/// Log level enumeration
/// </summary>
public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4, Trade = 5, Position = 6, Performance = 7, Health = 8, Risk = 9, DataPipeline = 10, MarketData = 11
}

/// <summary>
/// User-configurable performance thresholds
/// </summary>
public class PerformanceThresholds
{
    /// <summary>
    /// Trading operation latency threshold (microseconds)
    /// </summary>
    public long TradingOperationMicroseconds { get; set; } = 100;
    
    /// <summary>
    /// Data processing latency threshold (milliseconds)
    /// </summary>
    public long DataProcessingMilliseconds { get; set; } = 1;
    
    /// <summary>
    /// Market data latency threshold (microseconds)
    /// </summary>
    public long MarketDataMicroseconds { get; set; } = 50;
    
    /// <summary>
    /// Order execution latency threshold (microseconds)
    /// </summary>
    public long OrderExecutionMicroseconds { get; set; } = 75;
    
    /// <summary>
    /// Risk calculation latency threshold (microseconds)
    /// </summary>
    public long RiskCalculationMicroseconds { get; set; } = 200;
    
    /// <summary>
    /// Database operation latency threshold (milliseconds)
    /// </summary>
    public long DatabaseOperationMilliseconds { get; set; } = 10;
    
    /// <summary>
    /// Memory usage threshold (percentage)
    /// </summary>
    public double MemoryUsagePercentage { get; set; } = 85.0;
    
    /// <summary>
    /// CPU usage threshold (percentage)
    /// </summary>
    public double CpuUsagePercentage { get; set; } = 80.0;
    
    public static PerformanceThresholds CreateDevelopmentDefaults()
    {
        return new PerformanceThresholds
        {
            TradingOperationMicroseconds = 500, // More relaxed for development
            DataProcessingMilliseconds = 5,
            MarketDataMicroseconds = 200,
            OrderExecutionMicroseconds = 300,
            RiskCalculationMicroseconds = 1000,
            DatabaseOperationMilliseconds = 50,
            MemoryUsagePercentage = 90.0,
            CpuUsagePercentage = 90.0
        };
    }
    
    public static PerformanceThresholds CreateProductionDefaults()
    {
        return new PerformanceThresholds(); // Use strict defaults
    }
}

/// <summary>
/// Storage configuration for tiered logging
/// </summary>
public class StorageConfiguration
{
    /// <summary>
    /// Hot storage path (NVMe - recent data)
    /// </summary>
    public string HotStoragePath { get; set; } = "/logs/hot";
    
    /// <summary>
    /// Warm storage path (HDD - older data)
    /// </summary>
    public string WarmStoragePath { get; set; } = "/logs/warm";
    
    /// <summary>
    /// Cold storage path (Archive - historical data)
    /// </summary>
    public string ColdStoragePath { get; set; } = "/logs/cold";
    
    /// <summary>
    /// Hot storage retention hours
    /// </summary>
    public int HotRetentionHours { get; set; } = 24;
    
    /// <summary>
    /// Warm storage retention days
    /// </summary>
    public int WarmRetentionDays { get; set; } = 30;
    
    /// <summary>
    /// Cold storage retention years
    /// </summary>
    public int ColdRetentionYears { get; set; } = 7;
    
    /// <summary>
    /// Enable ClickHouse database integration
    /// </summary>
    public bool EnableClickHouse { get; set; } = true;
    
    /// <summary>
    /// ClickHouse connection string
    /// </summary>
    public string ClickHouseConnectionString { get; set; } = "Host=localhost;Port=9000;Database=trading_logs";
    
    public static StorageConfiguration CreateDevelopmentDefaults()
    {
        return new StorageConfiguration
        {
            HotRetentionHours = 8,
            WarmRetentionDays = 7,
            ColdRetentionYears = 1,
            EnableClickHouse = false // Simplified for development
        };
    }
    
    public static StorageConfiguration CreateProductionDefaults()
    {
        return new StorageConfiguration(); // Use full defaults
    }
}

/// <summary>
/// Log rotation configuration
/// </summary>
public class RotationConfiguration
{
    /// <summary>
    /// Maximum file size in MB before rotation
    /// </summary>
    public int MaxFileSizeMB { get; set; } = 100;
    
    /// <summary>
    /// Rotation interval in hours
    /// </summary>
    public int RotationIntervalHours { get; set; } = 1;
    
    /// <summary>
    /// Maximum number of archived files to keep
    /// </summary>
    public int MaxArchivedFiles { get; set; } = 100;
}

/// <summary>
/// AI/ML configuration
/// </summary>
public class AiConfiguration
{
    /// <summary>
    /// Enable anomaly detection
    /// </summary>
    public bool EnableAnomalyDetection { get; set; } = true;
    
    /// <summary>
    /// Enable predictive analysis
    /// </summary>
    public bool EnablePredictiveAnalysis { get; set; } = true;
    
    /// <summary>
    /// Enable RTX GPU acceleration
    /// </summary>
    public bool EnableGpuAcceleration { get; set; } = true;
    
    /// <summary>
    /// Anomaly detection sensitivity (0.0 - 1.0)
    /// </summary>
    public double AnomalySensitivity { get; set; } = 0.8;
    
    /// <summary>
    /// ML model update interval in hours
    /// </summary>
    public int ModelUpdateIntervalHours { get; set; } = 24;
    
    public static AiConfiguration CreateDevelopmentDefaults()
    {
        return new AiConfiguration
        {
            EnableAnomalyDetection = true,
            EnablePredictiveAnalysis = false, // Simplified for development
            EnableGpuAcceleration = false,
            AnomalySensitivity = 0.5,
            ModelUpdateIntervalHours = 72
        };
    }
    
    public static AiConfiguration CreateProductionDefaults()
    {
        return new AiConfiguration(); // Use full defaults
    }
}

/// <summary>
/// Real-time streaming configuration
/// </summary>
public class StreamingConfiguration
{
    /// <summary>
    /// Streaming port for real-time log access
    /// </summary>
    public int StreamingPort { get; set; } = 8080;
    
    /// <summary>
    /// Maximum streaming clients
    /// </summary>
    public int MaxStreamingClients { get; set; } = 10;
    
    /// <summary>
    /// Streaming buffer size
    /// </summary>
    public int StreamingBufferSize { get; set; } = 10000;
    
    /// <summary>
    /// Enable WebSocket streaming
    /// </summary>
    public bool EnableWebSocketStreaming { get; set; } = true;
}
