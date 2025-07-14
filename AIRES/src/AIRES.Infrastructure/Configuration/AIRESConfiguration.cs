using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;

namespace AIRES.Infrastructure.Configuration;

/// <summary>
/// AIRES configuration model that maps to aires.ini file.
/// </summary>
public class AIRESConfiguration
{
    /// <summary>
    /// Gets or sets the directory monitoring configuration.
    /// </summary>
    public DirectoriesSection Directories { get; set; } = new();

    /// <summary>
    /// Gets or sets the AI services configuration.
    /// </summary>
    public AIServicesSection AIServices { get; set; } = new();

    /// <summary>
    /// Gets or sets the pipeline configuration.
    /// </summary>
    public PipelineSection Pipeline { get; set; } = new();

    /// <summary>
    /// Gets or sets the logging configuration.
    /// </summary>
    public LoggingSection Logging { get; set; } = new();

    /// <summary>
    /// Gets or sets the monitoring configuration.
    /// </summary>
    public MonitoringSection Monitoring { get; set; } = new();

    /// <summary>
    /// Gets or sets the security configuration.
    /// </summary>
    public SecuritySection Security { get; set; } = new();

    /// <summary>
    /// Gets or sets the development configuration.
    /// </summary>
    public DevelopmentSection Development { get; set; } = new();
}

/// <summary>
/// Directory configuration section.
/// </summary>
public class DirectoriesSection
{
    [Required]
    public string InputDirectory { get; set; } = "./input";

    [Required]
    public string OutputDirectory { get; set; } = "./docs/error-booklets";

    public string TempDirectory { get; set; } = "./temp";
}

/// <summary>
/// AI services configuration section.
/// </summary>
public class AIServicesSection
{
    [Required]
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    public int OllamaTimeout { get; set; } = 120;

    public string MistralModel { get; set; } = "mistral:latest";
    public string DeepSeekModel { get; set; } = "deepseek-coder:latest";
    public string CodeGemmaModel { get; set; } = "codegemma:latest";
    public string Gemma2Model { get; set; } = "gemma2:latest";
}

/// <summary>
/// Pipeline configuration section.
/// </summary>
public class PipelineSection
{
    public int MaxRetries { get; set; } = 3;
    public int RetryDelay { get; set; } = 2;
    public bool EnableParallelProcessing { get; set; } = true;
}

/// <summary>
/// Logging configuration section.
/// </summary>
public class LoggingSection
{
    public string LogLevel { get; set; } = "Info";
    public bool StructuredLogging { get; set; } = true;
    public string? LogFilePath { get; set; }
}

/// <summary>
/// Monitoring configuration section.
/// </summary>
public class MonitoringSection
{
    public bool EnableTelemetry { get; set; } = true;
    public int MetricsInterval { get; set; } = 60;
    public bool EnableHealthChecks { get; set; } = true;
}

/// <summary>
/// Security configuration section.
/// </summary>
public class SecuritySection
{
    public bool SanitizeLogs { get; set; } = true;
    public bool MaskApiKeys { get; set; } = true;
}

/// <summary>
/// Development configuration section.
/// </summary>
public class DevelopmentSection
{
    public bool TreatWarningsAsErrors { get; set; } = true;
    public bool DebugMode { get; set; } = false;
}

/// <summary>
/// Service for loading and validating AIRES configuration from INI file.
/// </summary>
public class AIRESConfigurationService : AIRESServiceBase
{
    private readonly string _configPath;
    private AIRESConfiguration? _configuration;

    public AIRESConfigurationService(IAIRESLogger logger, string configPath = "config/aires.ini") 
        : base(logger, nameof(AIRESConfigurationService))
    {
        _configPath = configPath;
    }

    /// <summary>
    /// Loads the AIRES configuration from the INI file.
    /// </summary>
    public async Task<AIRESResult<AIRESConfiguration>> LoadConfigurationAsync()
    {
        LogMethodEntry();

        try
        {
            // Use async file check to make this method truly async
            await Task.Run(() => File.Exists(_configPath)).ConfigureAwait(false);
            
            if (!File.Exists(_configPath))
            {
                LogWarning($"Configuration file not found at: {_configPath}. Using defaults.");
                _configuration = new AIRESConfiguration();
                LogMethodExit();
                return AIRESResult<AIRESConfiguration>.Success(_configuration);
            }

            var builder = new ConfigurationBuilder()
                .AddIniFile(_configPath, optional: false, reloadOnChange: true);

            var configRoot = builder.Build();
            _configuration = new AIRESConfiguration();

            // Bind configuration sections
            configRoot.GetSection("Directories").Bind(_configuration.Directories);
            configRoot.GetSection("AI_Services").Bind(_configuration.AIServices);
            configRoot.GetSection("Pipeline").Bind(_configuration.Pipeline);
            configRoot.GetSection("Logging").Bind(_configuration.Logging);
            configRoot.GetSection("Monitoring").Bind(_configuration.Monitoring);
            configRoot.GetSection("Security").Bind(_configuration.Security);
            configRoot.GetSection("Development").Bind(_configuration.Development);

            // Validate configuration
            var validationResult = ValidateConfiguration(_configuration);
            if (!validationResult.IsSuccess)
            {
                LogMethodExit();
                return validationResult;
            }

            LogInfo($"Configuration loaded successfully from: {_configPath}");
            LogMethodExit();
            return AIRESResult<AIRESConfiguration>.Success(_configuration);
        }
        catch (Exception ex)
        {
            LogError("Failed to load configuration", ex);
            LogMethodExit();
            return AIRESResult<AIRESConfiguration>.Failure(
                "CONFIG_LOAD_ERROR",
                $"Failed to load configuration: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Gets the current configuration, loading it if necessary.
    /// </summary>
    public async Task<AIRESResult<AIRESConfiguration>> GetConfigurationAsync()
    {
        LogMethodEntry();

        if (_configuration == null)
        {
            var result = await LoadConfigurationAsync();
            if (!result.IsSuccess)
            {
                LogMethodExit();
                return result;
            }
        }

        LogMethodExit();
        return AIRESResult<AIRESConfiguration>.Success(_configuration!);
    }

    private AIRESResult<AIRESConfiguration> ValidateConfiguration(AIRESConfiguration config)
    {
        LogMethodEntry();

        var validationContext = new ValidationContext(config);
        var validationResults = new List<ValidationResult>();

        // Validate root object
        if (!Validator.TryValidateObject(config, validationContext, validationResults, true))
        {
            var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
            LogError($"Configuration validation failed: {errors}");
            LogMethodExit();
            return AIRESResult<AIRESConfiguration>.Failure(
                "CONFIG_VALIDATION_ERROR",
                $"Configuration validation failed: {errors}");
        }

        // Validate nested objects
        var sectionsToValidate = new object[]
        {
            config.Directories,
            config.AIServices,
            config.Pipeline,
            config.Logging,
            config.Monitoring,
            config.Security,
            config.Development
        };

        foreach (var section in sectionsToValidate)
        {
            validationContext = new ValidationContext(section);
            validationResults.Clear();

            if (!Validator.TryValidateObject(section, validationContext, validationResults, true))
            {
                var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
                LogError($"Configuration section validation failed: {errors}");
                LogMethodExit();
                return AIRESResult<AIRESConfiguration>.Failure(
                    "CONFIG_SECTION_VALIDATION_ERROR",
                    $"Configuration section validation failed: {errors}");
            }
        }

        // Custom validations
        if (!Directory.Exists(config.Directories.InputDirectory))
        {
            LogWarning($"Input directory does not exist: {config.Directories.InputDirectory}");
        }

        LogInfo("Configuration validation passed");
        LogMethodExit();
        return AIRESResult<AIRESConfiguration>.Success(config);
    }
}