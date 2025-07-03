using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradingPlatform.SecureConfiguration.Core;

namespace TradingPlatform.SecureConfiguration.Implementations
{
    /// <summary>
    /// Headless secure configuration for automated environments (CI/CD, Docker, etc.)
    /// Reads initial values from environment variables or secure files
    /// </summary>
    public class HeadlessSecureConfiguration : SecureConfigurationBase
    {
        private readonly Dictionary<string, string> _requiredMappings;
        private readonly Dictionary<string, string> _optionalMappings;
        private readonly string? _secretsFilePath;

        /// <summary>
        /// Create headless configuration that reads from environment variables
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="applicationName">Application name for storage</param>
        /// <param name="requiredMappings">Map of config key to environment variable name</param>
        /// <param name="optionalMappings">Optional mappings</param>
        /// <param name="secretsFilePath">Optional path to secrets file (JSON format)</param>
        public HeadlessSecureConfiguration(
            ILogger<HeadlessSecureConfiguration> logger,
            string applicationName,
            Dictionary<string, string> requiredMappings,
            Dictionary<string, string>? optionalMappings = null,
            string? secretsFilePath = null)
            : base(logger, applicationName)
        {
            _requiredMappings = requiredMappings ?? new Dictionary<string, string>();
            _optionalMappings = optionalMappings ?? new Dictionary<string, string>();
            _secretsFilePath = secretsFilePath;
        }

        protected override async Task<SecureConfigResult> RunFirstTimeSetupAsync()
        {
            try
            {
                _logger.LogInformation("Running headless configuration setup");

                var collectedValues = new Dictionary<string, string>();
                var missingRequired = new List<string>();

                // Try to load from secrets file first
                Dictionary<string, string>? fileSecrets = null;
                if (!string.IsNullOrEmpty(_secretsFilePath) && File.Exists(_secretsFilePath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(_secretsFilePath);
                        fileSecrets = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        _logger.LogInformation("Loaded secrets from file: {Path}", _secretsFilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load secrets file: {Path}", _secretsFilePath);
                    }
                }

                // Collect required values
                foreach (var mapping in _requiredMappings)
                {
                    var value = GetSecretValue(mapping.Key, mapping.Value, fileSecrets);
                    
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        missingRequired.Add($"{mapping.Key} (env: {mapping.Value})");
                    }
                    else
                    {
                        collectedValues[mapping.Key] = value;
                        _logger.LogDebug("Collected required value for {Key}", mapping.Key);
                    }
                }

                // Check if all required values are present
                if (missingRequired.Count > 0)
                {
                    return SecureConfigResult.Failure(
                        $"Missing required configuration: {string.Join(", ", missingRequired)}",
                        "MISSING_REQUIRED");
                }

                // Collect optional values
                foreach (var mapping in _optionalMappings)
                {
                    var value = GetSecretValue(mapping.Key, mapping.Value, fileSecrets);
                    
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        collectedValues[mapping.Key] = value;
                        _logger.LogDebug("Collected optional value for {Key}", mapping.Key);
                    }
                }

                // Save configuration
                var saveResult = await SaveConfigurationAsync(collectedValues);
                
                if (!saveResult.IsSuccess)
                {
                    return saveResult;
                }

                lock (_lock)
                {
                    _decryptedValues = collectedValues;
                }

                _logger.LogInformation(
                    "Headless configuration completed: {Total} values ({Required} required, {Optional} optional)",
                    collectedValues.Count,
                    _requiredMappings.Count,
                    collectedValues.Count - _requiredMappings.Count);

                // Clean up secrets file if requested
                if (!string.IsNullOrEmpty(_secretsFilePath) && File.Exists(_secretsFilePath))
                {
                    var deleteEnvVar = Environment.GetEnvironmentVariable("DELETE_SECRETS_AFTER_IMPORT");
                    if (deleteEnvVar?.ToLower() == "true")
                    {
                        try
                        {
                            File.Delete(_secretsFilePath);
                            _logger.LogInformation("Deleted secrets file after import");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete secrets file");
                        }
                    }
                }

                return SecureConfigResult.Success(new Dictionary<string, object>
                {
                    ["ConfiguredKeys"] = collectedValues.Count,
                    ["Source"] = fileSecrets != null ? "File" : "Environment"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Headless setup failed");
                return SecureConfigResult.Failure($"Setup failed: {ex.Message}", "SETUP_ERROR");
            }
        }

        protected override Task<SecureConfigResult> ValidateConfigurationAsync()
        {
            var missingKeys = new List<string>();

            foreach (var requiredKey in _requiredMappings.Keys)
            {
                if (!_decryptedValues.ContainsKey(requiredKey) ||
                    string.IsNullOrWhiteSpace(_decryptedValues[requiredKey]))
                {
                    missingKeys.Add(requiredKey);
                }
            }

            if (missingKeys.Count > 0)
            {
                return Task.FromResult(SecureConfigResult.Failure(
                    $"Missing required configuration: {string.Join(", ", missingKeys)}",
                    "MISSING_REQUIRED"));
            }

            return Task.FromResult(SecureConfigResult.Success());
        }

        private string? GetSecretValue(
            string configKey, 
            string envVarName, 
            Dictionary<string, string>? fileSecrets)
        {
            // Priority: Environment variable > Secrets file
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                return envValue;
            }

            // Check secrets file
            if (fileSecrets != null)
            {
                // Try exact match
                if (fileSecrets.TryGetValue(configKey, out var fileValue))
                {
                    return fileValue;
                }

                // Try environment variable name as key
                if (fileSecrets.TryGetValue(envVarName, out fileValue))
                {
                    return fileValue;
                }
            }

            return null;
        }
    }
}