using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using AIRES.Core.Configuration;
using AIRES.Core.Health;
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
    private readonly object _configLock = new();
    private IConfiguration _configuration;
    
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
    
    protected override async Task<AIRESResult<bool>> OnInitializeAsync(CancellationToken cancellationToken)
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
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                LogInfo($"Created configuration directory: {directory}");
            }

            // Lock for thread safety during file operations
            lock (_configLock)
            {
                LogDebug($"Setting configuration value: {key} = {value}");
                
                // Read existing configuration preserving comments and structure
                var lines = File.Exists(_configFilePath) 
                    ? File.ReadAllLines(_configFilePath).ToList()
                    : new List<string>();

                // Parse the key to get section and property
                var keyParts = key.Split(':');
                if (keyParts.Length != 2)
                {
                    throw new ArgumentException($"Invalid configuration key format: {key}. Expected format: Section:Property", nameof(key));
                }

                var section = keyParts[0];
                var property = keyParts[1];
                
                // Find or create section
                var sectionIndex = FindSectionIndex(lines, section);
                if (sectionIndex == -1)
                {
                    // Section doesn't exist, add it at the end
                    if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                    {
                        lines.Add(string.Empty); // Add blank line before new section
                    }
                    lines.Add($"[{section}]");
                    lines.Add($"{property} = {value}");
                    LogInfo($"Created new configuration section: [{section}]");
                }
                else
                {
                    // Section exists, find or add property
                    var propertyUpdated = false;
                    var insertIndex = sectionIndex + 1;
                    
                    // Look for the property in this section
                    for (int i = sectionIndex + 1; i < lines.Count; i++)
                    {
                        var line = lines[i].Trim();
                        
                        // Stop if we hit another section
                        if (line.StartsWith('[') && line.EndsWith(']'))
                        {
                            break;
                        }
                        
                        // Skip empty lines and comments
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || line.StartsWith(';'))
                        {
                            insertIndex = i + 1;
                            continue;
                        }
                        
                        // Check if this is our property
                        var equalIndex = line.IndexOf('=');
                        if (equalIndex > 0)
                        {
                            var currentProperty = line.Substring(0, equalIndex).Trim();
                            if (currentProperty.Equals(property, StringComparison.OrdinalIgnoreCase))
                            {
                                // Update existing property
                                lines[i] = $"{property} = {value}";
                                propertyUpdated = true;
                                LogDebug($"Updated existing property: {property}");
                                break;
                            }
                            insertIndex = i + 1;
                        }
                    }
                    
                    if (!propertyUpdated)
                    {
                        // Property doesn't exist in section, add it
                        lines.Insert(insertIndex, $"{property} = {value}");
                        LogInfo($"Added new property to section [{section}]: {property}");
                    }
                }

                // Write the updated configuration back to file
                File.WriteAllLines(_configFilePath, lines);
                LogInfo($"Configuration saved to: {_configFilePath}");
                
                // Reload configuration to reflect changes
                _configuration = BuildConfiguration();
                LoadConfiguration();
            }
            
            await Task.CompletedTask;
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Failed to set configuration value: {key}", ex);
            LogMethodExit();
            throw;
        }
    }
    
    private int FindSectionIndex(List<string> lines, string sectionName)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.Equals($"[{sectionName}]", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
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
                ModelTopP = GetConfigValue("AI_Services:ModelTopP", AIServices.ModelTopP),
                EnableGpuLoadBalancing = GetConfigValue("AI_Services:EnableGpuLoadBalancing", AIServices.EnableGpuLoadBalancing)
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
    
    /// <summary>
    /// Performs comprehensive health check of the configuration service.
    /// </summary>
    public Task<HealthCheckResult> CheckHealthAsync()
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var diagnostics = new Dictionary<string, object>();
            var failureReasons = new List<string>();
            
            // 1. Check configuration file existence and accessibility
            diagnostics["ConfigFilePath"] = _configFilePath;
            diagnostics["ConfigFileExists"] = File.Exists(_configFilePath);
            
            if (!File.Exists(_configFilePath))
            {
                failureReasons.Add($"Configuration file not found: {_configFilePath}");
                LogError($"Configuration file not found at {_configFilePath}");
                
                stopwatch.Stop();
                LogMethodExit();
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    nameof(AIRESConfigurationService),
                    "Configuration Service",
                    stopwatch.ElapsedMilliseconds,
                    "Configuration file not found",
                    null,
                    failureReasons.ToImmutableList(),
                    diagnostics.ToImmutableDictionary()
                ));
            }
            
            // Test file accessibility
            try
            {
                using (var stream = File.OpenRead(_configFilePath))
                {
                    diagnostics["ConfigFileReadable"] = true;
                    diagnostics["ConfigFileSize"] = stream.Length;
                }
            }
            catch (Exception ex)
            {
                diagnostics["ConfigFileReadable"] = false;
                failureReasons.Add($"Cannot read configuration file: {ex.Message}");
                
                stopwatch.Stop();
                LogMethodExit();
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    nameof(AIRESConfigurationService),
                    "Configuration Service",
                    stopwatch.ElapsedMilliseconds,
                    "Cannot access configuration file",
                    ex,
                    failureReasons.ToImmutableList(),
                    diagnostics.ToImmutableDictionary()
                ));
            }
            
            // 2. Validate critical configuration values
            var validationIssues = new List<string>();
            
            // Check directories
            if (string.IsNullOrWhiteSpace(Directories.InputDirectory))
                validationIssues.Add("InputDirectory is not configured");
            if (string.IsNullOrWhiteSpace(Directories.OutputDirectory))
                validationIssues.Add("OutputDirectory is not configured");
                
            diagnostics["InputDirectory"] = Directories.InputDirectory ?? "(not set)";
            diagnostics["OutputDirectory"] = Directories.OutputDirectory ?? "(not set)";
            
            // Check AI services configuration
            if (string.IsNullOrWhiteSpace(AIServices.OllamaBaseUrl))
                validationIssues.Add("OllamaBaseUrl is not configured");
            if (AIServices.OllamaTimeout <= 0)
                validationIssues.Add($"Invalid OllamaTimeout: {AIServices.OllamaTimeout}");
                
            diagnostics["OllamaBaseUrl"] = AIServices.OllamaBaseUrl ?? "(not set)";
            diagnostics["OllamaTimeout"] = AIServices.OllamaTimeout;
            
            // Check AI models
            var missingModels = new List<string>();
            if (string.IsNullOrWhiteSpace(AIServices.MistralModel))
                missingModels.Add("MistralModel");
            if (string.IsNullOrWhiteSpace(AIServices.DeepSeekModel))
                missingModels.Add("DeepSeekModel");
            if (string.IsNullOrWhiteSpace(AIServices.CodeGemmaModel))
                missingModels.Add("CodeGemmaModel");
            if (string.IsNullOrWhiteSpace(AIServices.Gemma2Model))
                missingModels.Add("Gemma2Model");
                
            if (missingModels.Any())
                validationIssues.Add($"Missing AI models: {string.Join(", ", missingModels)}");
                
            diagnostics["ConfiguredAIModels"] = 4 - missingModels.Count;
            
            // Check pipeline configuration
            if (Pipeline.MaxRetries < 0)
                validationIssues.Add($"Invalid MaxRetries: {Pipeline.MaxRetries}");
            if (Pipeline.BatchSize <= 0)
                validationIssues.Add($"Invalid BatchSize: {Pipeline.BatchSize}");
            if (Pipeline.MaxConcurrentFiles <= 0)
                validationIssues.Add($"Invalid MaxConcurrentFiles: {Pipeline.MaxConcurrentFiles}");
                
            diagnostics["PipelineMaxRetries"] = Pipeline.MaxRetries;
            diagnostics["PipelineBatchSize"] = Pipeline.BatchSize;
            diagnostics["PipelineMaxConcurrentFiles"] = Pipeline.MaxConcurrentFiles;
            
            // Check watchdog configuration
            if (Watchdog.Enabled)
            {
                if (Watchdog.PollingIntervalSeconds <= 0)
                    validationIssues.Add($"Invalid PollingIntervalSeconds: {Watchdog.PollingIntervalSeconds}");
                if (Watchdog.ProcessingThreads <= 0)
                    validationIssues.Add($"Invalid ProcessingThreads: {Watchdog.ProcessingThreads}");
                    
                diagnostics["WatchdogEnabled"] = true;
                diagnostics["WatchdogPollingInterval"] = Watchdog.PollingIntervalSeconds;
                diagnostics["WatchdogProcessingThreads"] = Watchdog.ProcessingThreads;
            }
            else
            {
                diagnostics["WatchdogEnabled"] = false;
            }
            
            // Check processing configuration
            if (Processing.MaxFileSizeMB <= 0)
                validationIssues.Add($"Invalid MaxFileSizeMB: {Processing.MaxFileSizeMB}");
            if (string.IsNullOrWhiteSpace(Processing.AllowedExtensions))
                validationIssues.Add("AllowedExtensions is not configured");
                
            diagnostics["ProcessingMaxFileSizeMB"] = Processing.MaxFileSizeMB;
            diagnostics["ProcessingAllowedExtensions"] = Processing.AllowedExtensions ?? "(not set)";
            
            // Add configuration load time metrics
            diagnostics["ConfigLoadTime"] = GetMetricValue("ConfigurationLoadTime");
            diagnostics["ConfigReloadCount"] = GetMetricValue("ConfigurationReloads");
            
            stopwatch.Stop();
            
            // Determine health status based on validation issues
            if (validationIssues.Any())
            {
                // Check if these are critical issues
                var criticalIssues = validationIssues.Where(issue => 
                    issue.Contains("InputDirectory") || 
                    issue.Contains("OutputDirectory") || 
                    issue.Contains("OllamaBaseUrl") ||
                    issue.Contains("AllowedExtensions")).ToList();
                
                if (criticalIssues.Any())
                {
                    failureReasons.AddRange(criticalIssues);
                    LogError($"Critical configuration issues: {string.Join("; ", criticalIssues)}");
                    LogMethodExit();
                    
                    return Task.FromResult(HealthCheckResult.Unhealthy(
                        nameof(AIRESConfigurationService),
                        "Configuration Service",
                        stopwatch.ElapsedMilliseconds,
                        "Critical configuration values missing or invalid",
                        null,
                        failureReasons.ToImmutableList(),
                        diagnostics.ToImmutableDictionary()
                    ));
                }
                else
                {
                    // Non-critical issues - degraded
                    failureReasons.AddRange(validationIssues);
                    LogWarning($"Configuration validation issues: {string.Join("; ", validationIssues)}");
                    LogMethodExit();
                    
                    return Task.FromResult(HealthCheckResult.Degraded(
                        nameof(AIRESConfigurationService),
                        "Configuration Service",
                        stopwatch.ElapsedMilliseconds,
                        failureReasons.ToImmutableList(),
                        diagnostics.ToImmutableDictionary()
                    ));
                }
            }
            
            // All checks passed
            LogInfo($"Configuration health check completed successfully in {stopwatch.ElapsedMilliseconds}ms");
            LogMethodExit();
            
            return Task.FromResult(HealthCheckResult.Healthy(
                nameof(AIRESConfigurationService),
                "Configuration Service",
                stopwatch.ElapsedMilliseconds,
                diagnostics.ToImmutableDictionary()
            ));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogError("Error during configuration health check", ex);
            LogMethodExit();
            
            return Task.FromResult(HealthCheckResult.Unhealthy(
                nameof(AIRESConfigurationService),
                "Configuration Service",
                stopwatch.ElapsedMilliseconds,
                $"Health check failed: {ex.Message}",
                ex,
                ImmutableList.Create($"Exception during health check: {ex.GetType().Name}")
            ));
        }
    }
    
    private double GetMetricValue(string metricName)
    {
        var metrics = GetMetrics();
        var value = metrics.GetValueOrDefault(metricName);
        return value != null ? Convert.ToDouble(value) : 0;
    }
}