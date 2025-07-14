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