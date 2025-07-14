using System.Globalization;
using AIRES.Core.Configuration;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;
using Microsoft.Extensions.Configuration;

namespace AIRES.Infrastructure.Configuration;

/// <summary>
/// Implementation of AIRES configuration service that reads from aires.ini file.
/// Extends AIRESServiceBase for canonical logging and lifecycle management.
/// </summary>
public class AIRESConfigurationService : AIRESServiceBase, IAIRESConfiguration
{
    private readonly string _configFilePath;
    private IConfiguration _configuration;
    private readonly object _configLock = new();
    
    // Configuration properties
    public DirectoryConfiguration Directories { get; private set; }
    public AIServicesConfiguration AIServices { get; private set; }
    public PipelineConfiguration Pipeline { get; private set; }
    public WatchdogConfiguration Watchdog { get; private set; }
    public ProcessingConfiguration Processing { get; private set; }
    public AlertingConfiguration Alerting { get; private set; }
    public LoggingConfiguration Logging { get; private set; }
    public MonitoringConfiguration Monitoring { get; private set; }
    public SecurityConfiguration Security { get; private set; }
    public PerformanceConfiguration Performance { get; private set; }
    public DevelopmentConfiguration Development { get; private set; }
    
    public AIRESConfigurationService(IAIRESLogger logger, string? configFilePath = null) 
        : base(logger, nameof(AIRESConfigurationService))
    {
        LogMethodEntry();
        
        _configFilePath = configFilePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "aires.ini");
        LogInfo($"Configuration file path: {_configFilePath}");
        
        // Initialize with defaults
        Directories = new DirectoryConfiguration();
        AIServices = new AIServicesConfiguration();
        Pipeline = new PipelineConfiguration();
        Watchdog = new WatchdogConfiguration();
        Processing = new ProcessingConfiguration();
        Alerting = new AlertingConfiguration();
        Logging = new LoggingConfiguration();
        Monitoring = new MonitoringConfiguration();
        Security = new SecurityConfiguration();
        Performance = new PerformanceConfiguration();
        Development = new DevelopmentConfiguration();
        
        // Build initial configuration
        _configuration = BuildConfiguration();
        LoadConfiguration();
        
        LogMethodExit();
    }
    
    protected override async Task<AIRESResult<bool>> OnInitializeAsync()
    {
        LogMethodEntry();
        
        try
        {
            // Verify configuration file exists
            if (!File.Exists(_configFilePath))
            {
                LogWarning($"Configuration file not found at {_configFilePath}. Using defaults.");
                LogMethodExit();
                return AIRESResult<bool>.Success(true);
            }
            
            // Reload configuration
            await ReloadAsync();
            
            LogInfo("Configuration service initialized successfully");
            LogMethodExit();
            return AIRESResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize configuration service", ex);
            LogMethodExit();
            return AIRESResult<bool>.Failure("CONFIG_INIT_ERROR", "Failed to initialize configuration", ex);
        }
    }
    
    public async Task ReloadAsync()
    {
        LogMethodEntry();
        
        try
        {
            await Task.Run(() =>
            {
                lock (_configLock)
                {
                    _configuration = BuildConfiguration();
                    LoadConfiguration();
                }
            });
            
            LogInfo("Configuration reloaded successfully");
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Failed to reload configuration", ex);
            LogMethodExit();
            throw;
        }
    }
    
    public string? GetValue(string key)
    {
        LogMethodEntry();
        
        try
        {
            var value = _configuration[key];
            LogDebug($"Retrieved configuration value: {key} = {value}");
            LogMethodExit();
            return value;
        }
        catch (Exception ex)
        {
            LogError($"Failed to get configuration value for key: {key}", ex);
            LogMethodExit();
            throw;
        }
    }
    
    public async Task SetValueAsync(string key, string value)
    {
        LogMethodEntry();
        
        try
        {
            // This would require INI file writing logic
            // For now, we'll throw NotImplementedException
            LogWarning("SetValueAsync not yet implemented");
            await Task.CompletedTask;
            throw new NotImplementedException("Configuration writing not yet implemented");
        }
        catch (Exception ex)
        {
            LogError($"Failed to set configuration value: {key}", ex);
            LogMethodExit();
            throw;
        }
    }
    
    private IConfiguration BuildConfiguration()
    {
        LogMethodEntry();
        
        try
        {
            var builder = new ConfigurationBuilder();
            
            if (File.Exists(_configFilePath))
            {
                builder.AddIniFile(_configFilePath, optional: false, reloadOnChange: true);
                LogDebug($"Added INI file: {_configFilePath}");
            }
            
            // Add environment variables as override
            builder.AddEnvironmentVariables("AIRES_");
            
            LogMethodExit();
            return builder.Build();
        }
        catch (Exception ex)
        {
            LogError("Failed to build configuration", ex);
            LogMethodExit();
            throw;
        }
    }
    
    private void LoadConfiguration()
    {
        LogMethodEntry();
        
        try
        {
            // Load Directories
            Directories = new DirectoryConfiguration
            {
                InputDirectory = GetConfigValue("Directories:InputDirectory", Directories.InputDirectory),
                OutputDirectory = GetConfigValue("Directories:OutputDirectory", Directories.OutputDirectory),
                TempDirectory = GetConfigValue("Directories:TempDirectory", Directories.TempDirectory),
                AlertDirectory = GetConfigValue("Directories:AlertDirectory", Directories.AlertDirectory),
                LogDirectory = GetConfigValue("Directories:LogDirectory", Directories.LogDirectory)
            };
            
            // Load AI Services
            AIServices = new AIServicesConfiguration
            {
                OllamaBaseUrl = GetConfigValue("AI_Services:OllamaBaseUrl", AIServices.OllamaBaseUrl),
                OllamaTimeout = GetConfigValue("AI_Services:OllamaTimeout", AIServices.OllamaTimeout),
                MistralModel = GetConfigValue("AI_Services:MistralModel", AIServices.MistralModel),
                DeepSeekModel = GetConfigValue("AI_Services:DeepSeekModel", AIServices.DeepSeekModel),
                CodeGemmaModel = GetConfigValue("AI_Services:CodeGemmaModel", AIServices.CodeGemmaModel),
                Gemma2Model = GetConfigValue("AI_Services:Gemma2Model", AIServices.Gemma2Model),
                ModelTemperature = GetConfigValue("AI_Services:ModelTemperature", AIServices.ModelTemperature),
                ModelMaxTokens = GetConfigValue("AI_Services:ModelMaxTokens", AIServices.ModelMaxTokens),
                ModelTopP = GetConfigValue("AI_Services:ModelTopP", AIServices.ModelTopP)
            };
            
            // Load Pipeline
            Pipeline = new PipelineConfiguration
            {
                MaxRetries = GetConfigValue("Pipeline:MaxRetries", Pipeline.MaxRetries),
                RetryDelay = GetConfigValue("Pipeline:RetryDelay", Pipeline.RetryDelay),
                EnableParallelProcessing = GetConfigValue("Pipeline:EnableParallelProcessing", Pipeline.EnableParallelProcessing),
                BatchSize = GetConfigValue("Pipeline:BatchSize", Pipeline.BatchSize),
                MaxConcurrentFiles = GetConfigValue("Pipeline:MaxConcurrentFiles", Pipeline.MaxConcurrentFiles)
            };
            
            // Load Watchdog
            Watchdog = new WatchdogConfiguration
            {
                Enabled = GetConfigValue("Watchdog:Enabled", Watchdog.Enabled),
                PollingIntervalSeconds = GetConfigValue("Watchdog:PollingIntervalSeconds", Watchdog.PollingIntervalSeconds),
                FileAgeThresholdMinutes = GetConfigValue("Watchdog:FileAgeThresholdMinutes", Watchdog.FileAgeThresholdMinutes),
                MaxQueueSize = GetConfigValue("Watchdog:MaxQueueSize", Watchdog.MaxQueueSize),
                ProcessingThreads = GetConfigValue("Watchdog:ProcessingThreads", Watchdog.ProcessingThreads)
            };
            
            // Load Processing
            Processing = new ProcessingConfiguration
            {
                MaxFileSizeMB = GetConfigValue("Processing:MaxFileSizeMB", Processing.MaxFileSizeMB),
                AllowedExtensions = GetConfigValue("Processing:AllowedExtensions", Processing.AllowedExtensions),
                MaxErrorsPerFile = GetConfigValue("Processing:MaxErrorsPerFile", Processing.MaxErrorsPerFile),
                ContextLinesBeforeError = GetConfigValue("Processing:ContextLinesBeforeError", Processing.ContextLinesBeforeError),
                ContextLinesAfterError = GetConfigValue("Processing:ContextLinesAfterError", Processing.ContextLinesAfterError)
            };
            
            // Load Alerting
            Alerting = new AlertingConfiguration
            {
                Enabled = GetConfigValue("Alerting:Enabled", Alerting.Enabled),
                ConsoleAlerts = GetConfigValue("Alerting:ConsoleAlerts", Alerting.ConsoleAlerts),
                FileAlerts = GetConfigValue("Alerting:FileAlerts", Alerting.FileAlerts),
                WindowsEventLog = GetConfigValue("Alerting:WindowsEventLog", Alerting.WindowsEventLog),
                CriticalDiskSpaceMB = GetConfigValue("Alerting:CriticalDiskSpaceMB", Alerting.CriticalDiskSpaceMB),
                WarningDiskSpaceMB = GetConfigValue("Alerting:WarningDiskSpaceMB", Alerting.WarningDiskSpaceMB),
                CriticalMemoryPercent = GetConfigValue("Alerting:CriticalMemoryPercent", Alerting.CriticalMemoryPercent),
                WarningMemoryPercent = GetConfigValue("Alerting:WarningMemoryPercent", Alerting.WarningMemoryPercent),
                ErrorRateThresholdPercent = GetConfigValue("Alerting:ErrorRateThresholdPercent", Alerting.ErrorRateThresholdPercent)
            };
            
            // Load Logging
            Logging = new LoggingConfiguration
            {
                LogLevel = GetConfigValue("Logging:LogLevel", Logging.LogLevel),
                StructuredLogging = GetConfigValue("Logging:StructuredLogging", Logging.StructuredLogging),
                LogFilePath = GetConfigValue("Logging:LogFilePath", Logging.LogFilePath),
                RollingInterval = GetConfigValue("Logging:RollingInterval", Logging.RollingInterval),
                RetainedFileCount = GetConfigValue("Logging:RetainedFileCount", Logging.RetainedFileCount),
                FileSizeLimitMB = GetConfigValue("Logging:FileSizeLimitMB", Logging.FileSizeLimitMB),
                ConsoleLoggingEnabled = GetConfigValue("Logging:ConsoleLoggingEnabled", Logging.ConsoleLoggingEnabled),
                ConsoleUseColors = GetConfigValue("Logging:ConsoleUseColors", Logging.ConsoleUseColors)
            };
            
            // Load Monitoring
            Monitoring = new MonitoringConfiguration
            {
                EnableTelemetry = GetConfigValue("Monitoring:EnableTelemetry", Monitoring.EnableTelemetry),
                MetricsInterval = GetConfigValue("Monitoring:MetricsInterval", Monitoring.MetricsInterval),
                EnableHealthChecks = GetConfigValue("Monitoring:EnableHealthChecks", Monitoring.EnableHealthChecks),
                HealthCheckPort = GetConfigValue("Monitoring:HealthCheckPort", Monitoring.HealthCheckPort),
                HealthCheckPath = GetConfigValue("Monitoring:HealthCheckPath", Monitoring.HealthCheckPath)
            };
            
            // Load Security
            Security = new SecurityConfiguration
            {
                SanitizeErrorContent = GetConfigValue("Security:SanitizeErrorContent", Security.SanitizeErrorContent),
                ValidateFilePaths = GetConfigValue("Security:ValidateFilePaths", Security.ValidateFilePaths),
                PreventPathTraversal = GetConfigValue("Security:PreventPathTraversal", Security.PreventPathTraversal),
                SanitizeLogs = GetConfigValue("Security:SanitizeLogs", Security.SanitizeLogs),
                MaskApiKeys = GetConfigValue("Security:MaskApiKeys", Security.MaskApiKeys),
                ExcludeSensitiveData = GetConfigValue("Security:ExcludeSensitiveData", Security.ExcludeSensitiveData)
            };
            
            // Load Performance
            Performance = new PerformanceConfiguration
            {
                EnableConnectionPooling = GetConfigValue("Performance:EnableConnectionPooling", Performance.EnableConnectionPooling),
                MaxConnections = GetConfigValue("Performance:MaxConnections", Performance.MaxConnections),
                EnableResponseCaching = GetConfigValue("Performance:EnableResponseCaching", Performance.EnableResponseCaching),
                CacheDurationMinutes = GetConfigValue("Performance:CacheDurationMinutes", Performance.CacheDurationMinutes),
                MaxMemoryMB = GetConfigValue("Performance:MaxMemoryMB", Performance.MaxMemoryMB),
                MaxCpuPercent = GetConfigValue("Performance:MaxCpuPercent", Performance.MaxCpuPercent),
                FileReadTimeoutSeconds = GetConfigValue("Performance:FileReadTimeoutSeconds", Performance.FileReadTimeoutSeconds),
                BookletGenerationTimeoutSeconds = GetConfigValue("Performance:BookletGenerationTimeoutSeconds", Performance.BookletGenerationTimeoutSeconds)
            };
            
            // Load Development
            Development = new DevelopmentConfiguration
            {
                TreatWarningsAsErrors = GetConfigValue("Development:TreatWarningsAsErrors", Development.TreatWarningsAsErrors),
                DebugMode = GetConfigValue("Development:DebugMode", Development.DebugMode),
                VerboseLogging = GetConfigValue("Development:VerboseLogging", Development.VerboseLogging),
                SaveIntermediateResults = GetConfigValue("Development:SaveIntermediateResults", Development.SaveIntermediateResults),
                EnableTestMode = GetConfigValue("Development:EnableTestMode", Development.EnableTestMode),
                MockAIResponses = GetConfigValue("Development:MockAIResponses", Development.MockAIResponses)
            };
            
            LogInfo("Configuration loaded successfully");
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError("Failed to load configuration", ex);
            LogMethodExit();
            throw;
        }
    }
    
    private string GetConfigValue(string key, string defaultValue)
    {
        var value = _configuration[key] ?? defaultValue;
        LogTrace($"Config[{key}] = {value}");
        return value;
    }
    
    private int GetConfigValue(string key, int defaultValue)
    {
        var stringValue = _configuration[key];
        if (string.IsNullOrEmpty(stringValue))
        {
            LogTrace($"Config[{key}] = {defaultValue} (default)");
            return defaultValue;
        }
        
        if (int.TryParse(stringValue, out var value))
        {
            LogTrace($"Config[{key}] = {value}");
            return value;
        }
        
        LogWarning($"Invalid integer value for {key}: {stringValue}. Using default: {defaultValue}");
        return defaultValue;
    }
    
    private double GetConfigValue(string key, double defaultValue)
    {
        var stringValue = _configuration[key];
        if (string.IsNullOrEmpty(stringValue))
        {
            LogTrace($"Config[{key}] = {defaultValue} (default)");
            return defaultValue;
        }
        
        if (double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            LogTrace($"Config[{key}] = {value}");
            return value;
        }
        
        LogWarning($"Invalid double value for {key}: {stringValue}. Using default: {defaultValue}");
        return defaultValue;
    }
    
    private bool GetConfigValue(string key, bool defaultValue)
    {
        var stringValue = _configuration[key];
        if (string.IsNullOrEmpty(stringValue))
        {
            LogTrace($"Config[{key}] = {defaultValue} (default)");
            return defaultValue;
        }
        
        if (bool.TryParse(stringValue, out var value))
        {
            LogTrace($"Config[{key}] = {value}");
            return value;
        }
        
        LogWarning($"Invalid boolean value for {key}: {stringValue}. Using default: {defaultValue}");
        return defaultValue;
    }
}