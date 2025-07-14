namespace AIRES.Core.Configuration;

/// <summary>
/// Configuration interface for AIRES system settings.
/// Provides strongly-typed access to all configuration values.
/// </summary>
public interface IAIRESConfiguration
{
    // Directories
    DirectoryConfiguration Directories { get; }
    
    // AI Services
    AIServicesConfiguration AIServices { get; }
    
    // Pipeline
    PipelineConfiguration Pipeline { get; }
    
    // Watchdog
    WatchdogConfiguration Watchdog { get; }
    
    // Processing
    ProcessingConfiguration Processing { get; }
    
    // Alerting
    AlertingConfiguration Alerting { get; }
    
    // Logging
    LoggingConfiguration Logging { get; }
    
    // Monitoring
    MonitoringConfiguration Monitoring { get; }
    
    // Security
    SecurityConfiguration Security { get; }
    
    // Performance
    PerformanceConfiguration Performance { get; }
    
    // Development
    DevelopmentConfiguration Development { get; }
    
    /// <summary>
    /// Reloads configuration from source.
    /// </summary>
    Task ReloadAsync();
    
    /// <summary>
    /// Gets a raw configuration value by key.
    /// </summary>
    string? GetValue(string key);
    
    /// <summary>
    /// Sets a configuration value.
    /// </summary>
    Task SetValueAsync(string key, string value);
}

// Configuration section classes
public class DirectoryConfiguration
{
    public string InputDirectory { get; set; } = "./input";
    public string OutputDirectory { get; set; } = "./docs/error-booklets";
    public string TempDirectory { get; set; } = "./temp";
    public string AlertDirectory { get; set; } = "./alerts";
    public string LogDirectory { get; set; } = "./logs";
}

public class AIServicesConfiguration
{
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public int OllamaTimeout { get; set; } = 120;
    public string MistralModel { get; set; } = "mistral-optimized:latest";
    public string DeepSeekModel { get; set; } = "deepseek-coder-optimized:latest";
    public string CodeGemmaModel { get; set; } = "codegemma-optimized:latest";
    public string Gemma2Model { get; set; } = "gemma2:9b";
    public double ModelTemperature { get; set; } = 0.3;
    public int ModelMaxTokens { get; set; } = 2000;
    public double ModelTopP { get; set; } = 0.9;
}

public class PipelineConfiguration
{
    public int MaxRetries { get; set; } = 3;
    public int RetryDelay { get; set; } = 2;
    public bool EnableParallelProcessing { get; set; } = true;
    public int BatchSize { get; set; } = 5;
    public int MaxConcurrentFiles { get; set; } = 3;
}

public class WatchdogConfiguration
{
    public bool Enabled { get; set; } = false;
    public int PollingIntervalSeconds { get; set; } = 30;
    public int FileAgeThresholdMinutes { get; set; } = 60;
    public int MaxQueueSize { get; set; } = 100;
    public int ProcessingThreads { get; set; } = 2;
}

public class ProcessingConfiguration
{
    public int MaxFileSizeMB { get; set; } = 10;
    public string AllowedExtensions { get; set; } = ".txt,.log";
    public int MaxErrorsPerFile { get; set; } = 1000;
    public int ContextLinesBeforeError { get; set; } = 5;
    public int ContextLinesAfterError { get; set; } = 5;
}

public class AlertingConfiguration
{
    public bool Enabled { get; set; } = true;
    public bool ConsoleAlerts { get; set; } = true;
    public bool FileAlerts { get; set; } = true;
    public bool WindowsEventLog { get; set; } = false;
    public int CriticalDiskSpaceMB { get; set; } = 100;
    public int WarningDiskSpaceMB { get; set; } = 500;
    public int CriticalMemoryPercent { get; set; } = 90;
    public int WarningMemoryPercent { get; set; } = 70;
    public int ErrorRateThresholdPercent { get; set; } = 20;
}

public class LoggingConfiguration
{
    public string LogLevel { get; set; } = "Debug";
    public bool StructuredLogging { get; set; } = true;
    public string LogFilePath { get; set; } = "./logs/aires.log";
    public string RollingInterval { get; set; } = "Day";
    public int RetainedFileCount { get; set; } = 30;
    public int FileSizeLimitMB { get; set; } = 100;
    public bool ConsoleLoggingEnabled { get; set; } = true;
    public bool ConsoleUseColors { get; set; } = true;
}

public class MonitoringConfiguration
{
    public bool EnableTelemetry { get; set; } = false;
    public int MetricsInterval { get; set; } = 60;
    public bool EnableHealthChecks { get; set; } = true;
    public int HealthCheckPort { get; set; } = 5000;
    public string HealthCheckPath { get; set; } = "/health";
}

public class SecurityConfiguration
{
    public bool SanitizeErrorContent { get; set; } = true;
    public bool ValidateFilePaths { get; set; } = true;
    public bool PreventPathTraversal { get; set; } = true;
    public bool SanitizeLogs { get; set; } = true;
    public bool MaskApiKeys { get; set; } = true;
    public bool ExcludeSensitiveData { get; set; } = true;
}

public class PerformanceConfiguration
{
    public bool EnableConnectionPooling { get; set; } = true;
    public int MaxConnections { get; set; } = 10;
    public bool EnableResponseCaching { get; set; } = false;
    public int CacheDurationMinutes { get; set; } = 60;
    public int MaxMemoryMB { get; set; } = 500;
    public int MaxCpuPercent { get; set; } = 50;
    public int FileReadTimeoutSeconds { get; set; } = 30;
    public int BookletGenerationTimeoutSeconds { get; set; } = 300;
}

public class DevelopmentConfiguration
{
    public bool TreatWarningsAsErrors { get; set; } = true;
    public bool DebugMode { get; set; } = false;
    public bool VerboseLogging { get; set; } = false;
    public bool SaveIntermediateResults { get; set; } = false;
    public bool EnableTestMode { get; set; } = false;
    public bool MockAIResponses { get; set; } = false;
}