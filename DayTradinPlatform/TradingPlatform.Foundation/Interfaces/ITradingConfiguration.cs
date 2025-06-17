using Microsoft.Extensions.Primitives;

namespace TradingPlatform.Foundation.Interfaces;

/// <summary>
/// Strongly-typed configuration interface for trading platform components.
/// Provides reactive configuration updates and validation support.
/// </summary>
/// <typeparam name="T">Configuration model type</typeparam>
public interface ITradingConfiguration<T> where T : class
{
    /// <summary>
    /// Current configuration value with strong typing.
    /// Guaranteed to be valid according to configured validation rules.
    /// </summary>
    T Value { get; }

    /// <summary>
    /// Reloads configuration from all configured sources.
    /// Triggers validation and change notifications if values differ.
    /// </summary>
    void Reload();

    /// <summary>
    /// Gets a change token that triggers when configuration changes.
    /// Used for reactive configuration updates without polling.
    /// </summary>
    /// <returns>Change token for monitoring configuration changes</returns>
    IChangeToken GetReloadToken();
}

/// <summary>
/// Configuration provider specifically designed for trading applications.
/// Handles environment-specific settings, secrets management, and hot reload.
/// </summary>
public interface ITradingConfigurationProvider
{
    /// <summary>
    /// Gets strongly-typed configuration for the specified type.
    /// Includes validation and environment-specific overrides.
    /// </summary>
    /// <typeparam name="T">Configuration model type</typeparam>
    /// <param name="sectionPath">Configuration section path (e.g., "MarketData:AlphaVantage")</param>
    /// <returns>Strongly-typed configuration instance</returns>
    ITradingConfiguration<T> GetConfiguration<T>(string sectionPath) where T : class;

    /// <summary>
    /// Gets configuration value with fallback support.
    /// </summary>
    /// <param name="key">Configuration key using colon notation (e.g., "Redis:ConnectionString")</param>
    /// <param name="defaultValue">Default value if key is not found</param>
    /// <returns>Configuration value or default</returns>
    string GetValue(string key, string? defaultValue = null);

    /// <summary>
    /// Gets configuration value with type conversion.
    /// </summary>
    /// <typeparam name="T">Target type for conversion</typeparam>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if key is not found</param>
    /// <returns>Converted configuration value or default</returns>
    T GetValue<T>(string key, T defaultValue = default!);

    /// <summary>
    /// Validates all loaded configuration against defined rules.
    /// Should be called during application startup to catch configuration errors early.
    /// </summary>
    /// <returns>Validation results with any errors or warnings</returns>
    Task<ConfigurationValidationResult> ValidateAllAsync();

    /// <summary>
    /// Event raised when any configuration value changes.
    /// Provides details about what changed for targeted updates.
    /// </summary>
    event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;
}

/// <summary>
/// Base interface for all trading configuration models.
/// Provides common validation and metadata support.
/// </summary>
public interface ITradingConfigurationModel
{
    /// <summary>
    /// Validates the configuration model against business rules.
    /// Called automatically when configuration is loaded or changed.
    /// </summary>
    /// <returns>Validation result with any errors or warnings</returns>
    ConfigurationValidationResult Validate();

    /// <summary>
    /// Environment where this configuration is intended to be used.
    /// Helps prevent accidental use of production configs in development.
    /// </summary>
    string Environment { get; }

    /// <summary>
    /// Version of the configuration schema.
    /// Used for migration and compatibility checking.
    /// </summary>
    string SchemaVersion { get; }
}

/// <summary>
/// Configuration validation result containing any errors or warnings.
/// </summary>
public record ConfigurationValidationResult(
    bool IsValid,
    IReadOnlyList<ConfigurationValidationError> Errors,
    IReadOnlyList<ConfigurationValidationWarning> Warnings)
{
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ConfigurationValidationResult Success()
        => new(true, Array.Empty<ConfigurationValidationError>(), Array.Empty<ConfigurationValidationWarning>());

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    public static ConfigurationValidationResult Failed(params ConfigurationValidationError[] errors)
        => new(false, errors, Array.Empty<ConfigurationValidationWarning>());

    /// <summary>
    /// Creates a validation result with warnings but no errors.
    /// </summary>
    public static ConfigurationValidationResult WithWarnings(params ConfigurationValidationWarning[] warnings)
        => new(true, Array.Empty<ConfigurationValidationError>(), warnings);
}

/// <summary>
/// Configuration validation error indicating invalid or missing required values.
/// </summary>
public record ConfigurationValidationError(
    string PropertyPath,
    string ErrorMessage,
    object? AttemptedValue = null,
    string? ErrorCode = null);

/// <summary>
/// Configuration validation warning indicating suboptimal but valid configuration.
/// </summary>
public record ConfigurationValidationWarning(
    string PropertyPath,
    string WarningMessage,
    object? CurrentValue = null,
    object? RecommendedValue = null);

/// <summary>
/// Event arguments for configuration change notifications.
/// </summary>
public class ConfigurationChangedEventArgs : EventArgs
{
    /// <summary>
    /// The configuration section that changed.
    /// </summary>
    public string SectionPath { get; }

    /// <summary>
    /// Specific properties that changed within the section.
    /// </summary>
    public IReadOnlyDictionary<string, ConfigurationChange> Changes { get; }

    /// <summary>
    /// Timestamp when the change was detected.
    /// </summary>
    public DateTime ChangeTime { get; }

    public ConfigurationChangedEventArgs(string sectionPath, IReadOnlyDictionary<string, ConfigurationChange> changes)
    {
        SectionPath = sectionPath ?? throw new ArgumentNullException(nameof(sectionPath));
        Changes = changes ?? throw new ArgumentNullException(nameof(changes));
        ChangeTime = DateTime.UtcNow;
    }
}

/// <summary>
/// Details about a specific configuration value change.
/// </summary>
public record ConfigurationChange(
    string PropertyName,
    object? OldValue,
    object? NewValue,
    bool RequiresRestart = false);

/// <summary>
/// Marker interface for configuration models that contain sensitive information.
/// Implementations should not log or expose these values in diagnostics.
/// </summary>
public interface ISensitiveConfiguration
{
    /// <summary>
    /// Returns a sanitized version of the configuration suitable for logging.
    /// All sensitive values should be masked or replaced with placeholders.
    /// </summary>
    /// <returns>Configuration instance with sensitive values masked</returns>
    object GetSanitizedCopy();
}

/// <summary>
/// Configuration model for trading environment settings.
/// Provides standardized environment identification and validation.
/// </summary>
public interface IEnvironmentConfiguration : ITradingConfigurationModel
{
    /// <summary>
    /// Current environment name (Development, Staging, Production).
    /// </summary>
    string EnvironmentName { get; }

    /// <summary>
    /// Whether this is a production environment requiring extra safety checks.
    /// </summary>
    bool IsProduction { get; }

    /// <summary>
    /// Whether debug features should be enabled.
    /// </summary>
    bool EnableDebugFeatures { get; }

    /// <summary>
    /// Application version for this deployment.
    /// </summary>
    string ApplicationVersion { get; }

    /// <summary>
    /// Deployment timestamp for tracking configuration age.
    /// </summary>
    DateTime DeploymentTime { get; }
}