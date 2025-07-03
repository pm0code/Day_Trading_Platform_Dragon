using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using TradingPlatform.SecureConfiguration.Core;
using TradingPlatform.SecureConfiguration.Implementations;

namespace TradingPlatform.SecureConfiguration.Builders
{
    /// <summary>
    /// Fluent builder for creating secure configurations for any use case
    /// </summary>
    public class SecureConfigurationBuilder
    {
        private string _applicationName = "DefaultApp";
        private string _displayName = "Application";
        private readonly List<ConfigurationItem> _items = new();
        private ConfigurationMode _mode = ConfigurationMode.Interactive;
        private string? _secretsFilePath;
        private readonly Dictionary<string, string> _environmentMappings = new();
        private ILogger? _logger;
        private ValidationRules _validationRules = new();
        private EncryptionOptions _encryptionOptions = new();

        /// <summary>
        /// Start building a secure configuration
        /// </summary>
        public static SecureConfigurationBuilder Create() => new();

        /// <summary>
        /// Set the application name (used for storage location)
        /// </summary>
        public SecureConfigurationBuilder ForApplication(string applicationName, string? displayName = null)
        {
            _applicationName = applicationName ?? throw new ArgumentNullException(nameof(applicationName));
            _displayName = displayName ?? applicationName;
            return this;
        }

        /// <summary>
        /// Add API key configuration
        /// </summary>
        public SecureConfigurationBuilder WithApiKey(
            string keyName, 
            bool required = true,
            string? displayName = null,
            string? environmentVariable = null,
            Func<string, bool>? validator = null)
        {
            _items.Add(new ConfigurationItem
            {
                Key = keyName,
                DisplayName = displayName ?? FormatKeyName(keyName),
                Required = required,
                Type = ConfigurationType.ApiKey,
                Validator = validator ?? ValidateApiKey,
                EnvironmentVariable = environmentVariable
            });

            if (!string.IsNullOrEmpty(environmentVariable))
            {
                _environmentMappings[keyName] = environmentVariable;
            }

            return this;
        }

        /// <summary>
        /// Add database connection string
        /// </summary>
        public SecureConfigurationBuilder WithDatabaseConnection(
            string connectionName,
            bool required = true,
            string? displayName = null,
            string? environmentVariable = null)
        {
            _items.Add(new ConfigurationItem
            {
                Key = connectionName,
                DisplayName = displayName ?? $"{connectionName} Database Connection",
                Required = required,
                Type = ConfigurationType.ConnectionString,
                Validator = ValidateConnectionString,
                EnvironmentVariable = environmentVariable
            });

            if (!string.IsNullOrEmpty(environmentVariable))
            {
                _environmentMappings[connectionName] = environmentVariable;
            }

            return this;
        }

        /// <summary>
        /// Add certificate configuration
        /// </summary>
        public SecureConfigurationBuilder WithCertificate(
            string certName,
            bool required = true,
            CertificateFormat format = CertificateFormat.Thumbprint)
        {
            _items.Add(new ConfigurationItem
            {
                Key = certName,
                DisplayName = $"{certName} Certificate",
                Required = required,
                Type = ConfigurationType.Certificate,
                Validator = format == CertificateFormat.Thumbprint ? ValidateThumbprint : ValidateCertificate,
                Metadata = new Dictionary<string, object> { ["Format"] = format }
            });

            return this;
        }

        /// <summary>
        /// Add OAuth/JWT secret
        /// </summary>
        public SecureConfigurationBuilder WithOAuthSecret(
            string secretName,
            bool required = true,
            string? displayName = null)
        {
            _items.Add(new ConfigurationItem
            {
                Key = secretName,
                DisplayName = displayName ?? $"{secretName} Secret",
                Required = required,
                Type = ConfigurationType.OAuthSecret,
                Validator = ValidateSecret
            });

            return this;
        }

        /// <summary>
        /// Add encryption key (for services that provide encryption)
        /// </summary>
        public SecureConfigurationBuilder WithEncryptionKey(
            string keyName,
            EncryptionKeyType keyType = EncryptionKeyType.AES256,
            bool required = true)
        {
            _items.Add(new ConfigurationItem
            {
                Key = keyName,
                DisplayName = $"{keyName} Encryption Key",
                Required = required,
                Type = ConfigurationType.EncryptionKey,
                Validator = (value) => ValidateEncryptionKey(value, keyType),
                Metadata = new Dictionary<string, object> { ["KeyType"] = keyType }
            });

            return this;
        }

        /// <summary>
        /// Add webhook secret
        /// </summary>
        public SecureConfigurationBuilder WithWebhookSecret(
            string webhookName,
            bool required = true)
        {
            _items.Add(new ConfigurationItem
            {
                Key = $"{webhookName}WebhookSecret",
                DisplayName = $"{webhookName} Webhook Secret",
                Required = required,
                Type = ConfigurationType.WebhookSecret,
                Validator = ValidateSecret
            });

            return this;
        }

        /// <summary>
        /// Add custom configuration value
        /// </summary>
        public SecureConfigurationBuilder WithCustomValue(
            string key,
            string displayName,
            bool required = true,
            Func<string, bool>? validator = null,
            string? environmentVariable = null)
        {
            _items.Add(new ConfigurationItem
            {
                Key = key,
                DisplayName = displayName,
                Required = required,
                Type = ConfigurationType.Custom,
                Validator = validator,
                EnvironmentVariable = environmentVariable
            });

            if (!string.IsNullOrEmpty(environmentVariable))
            {
                _environmentMappings[key] = environmentVariable;
            }

            return this;
        }

        /// <summary>
        /// Configure for financial data provider (like Finnhub, AlphaVantage themselves)
        /// </summary>
        public SecureConfigurationBuilder ForFinancialDataProvider(string providerName)
        {
            return ForApplication($"{providerName}Service", $"{providerName} API Service")
                .WithDatabaseConnection("Main", environmentVariable: "DATABASE_URL")
                .WithDatabaseConnection("Analytics", required: false, environmentVariable: "ANALYTICS_DB_URL")
                .WithOAuthSecret("JwtSigningKey")
                .WithEncryptionKey("DataEncryption")
                .WithWebhookSecret("CustomerCallback")
                .WithApiKey("InternalServiceKey", displayName: "Internal Service Communication Key")
                .WithCertificate("SSL", format: CertificateFormat.PEM)
                .WithCustomValue("RateLimitConfig", "Rate Limiting Configuration", required: false);
        }

        /// <summary>
        /// Configure for a banking system
        /// </summary>
        public SecureConfigurationBuilder ForBankingSystem(string bankName)
        {
            return ForApplication($"{bankName}Banking", $"{bankName} Banking System")
                .WithDatabaseConnection("Core", environmentVariable: "CORE_DB")
                .WithDatabaseConnection("Audit", environmentVariable: "AUDIT_DB")
                .WithCertificate("HSM", format: CertificateFormat.PKCS12)
                .WithEncryptionKey("CustomerData", EncryptionKeyType.AES256)
                .WithEncryptionKey("TransactionSigning", EncryptionKeyType.RSA4096)
                .WithApiKey("FederalReserveAPI", displayName: "Federal Reserve API Key")
                .WithApiKey("SwiftAPI", displayName: "SWIFT Network API Key")
                .WithOAuthSecret("CustomerPortalJWT")
                .WithCustomValue("HSMPin", "Hardware Security Module PIN", validator: ValidatePin);
        }

        /// <summary>
        /// Configure for a cryptocurrency exchange
        /// </summary>
        public SecureConfigurationBuilder ForCryptoExchange(string exchangeName)
        {
            return ForApplication($"{exchangeName}Exchange", $"{exchangeName} Crypto Exchange")
                .WithEncryptionKey("ColdWalletKey", EncryptionKeyType.Secp256k1)
                .WithEncryptionKey("HotWalletKey", EncryptionKeyType.Secp256k1)
                .WithDatabaseConnection("OrderBook")
                .WithDatabaseConnection("UserAccounts")
                .WithApiKey("BlockchainProvider")
                .WithWebhookSecret("DepositNotification")
                .WithWebhookSecret("WithdrawalConfirmation")
                .WithOAuthSecret("TradingAPISecret")
                .WithCustomValue("MasterSeed", "HD Wallet Master Seed", validator: ValidateSeed);
        }

        /// <summary>
        /// Set configuration mode
        /// </summary>
        public SecureConfigurationBuilder WithMode(ConfigurationMode mode)
        {
            _mode = mode;
            return this;
        }

        /// <summary>
        /// Set headless mode with secrets file
        /// </summary>
        public SecureConfigurationBuilder WithSecretsFile(string path)
        {
            _secretsFilePath = path;
            _mode = ConfigurationMode.Headless;
            return this;
        }

        /// <summary>
        /// Set custom validation rules
        /// </summary>
        public SecureConfigurationBuilder WithValidation(Action<ValidationRules> configure)
        {
            configure?.Invoke(_validationRules);
            return this;
        }

        /// <summary>
        /// Set encryption options
        /// </summary>
        public SecureConfigurationBuilder WithEncryption(Action<EncryptionOptions> configure)
        {
            configure?.Invoke(_encryptionOptions);
            return this;
        }

        /// <summary>
        /// Set logger
        /// </summary>
        public SecureConfigurationBuilder WithLogger(ILogger logger)
        {
            _logger = logger;
            return this;
        }

        /// <summary>
        /// Build the secure configuration
        /// </summary>
        public ISecureConfiguration Build()
        {
            if (_logger == null)
            {
                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                _logger = loggerFactory.CreateLogger("SecureConfiguration");
            }

            var requiredKeys = _items.Where(i => i.Required).Select(i => i.Key).ToArray();
            var optionalKeys = _items.Where(i => !i.Required).Select(i => i.Key).ToArray();

            switch (_mode)
            {
                case ConfigurationMode.Interactive:
                    return new InteractiveSecureConfiguration(
                        (ILogger<InteractiveSecureConfiguration>)_logger,
                        _applicationName,
                        _displayName,
                        requiredKeys,
                        optionalKeys);

                case ConfigurationMode.Headless:
                    var requiredMappings = _items.Where(i => i.Required && !string.IsNullOrEmpty(i.EnvironmentVariable))
                        .ToDictionary(i => i.Key, i => i.EnvironmentVariable!);
                    var optionalMappings = _items.Where(i => !i.Required && !string.IsNullOrEmpty(i.EnvironmentVariable))
                        .ToDictionary(i => i.Key, i => i.EnvironmentVariable!);

                    return new HeadlessSecureConfiguration(
                        (ILogger<HeadlessSecureConfiguration>)_logger,
                        _applicationName,
                        requiredMappings,
                        optionalMappings,
                        _secretsFilePath);

                default:
                    throw new InvalidOperationException($"Unknown configuration mode: {_mode}");
            }
        }

        #region Validation Methods

        private static bool ValidateApiKey(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length >= 10;
        }

        private static bool ValidateConnectionString(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && 
                   (value.Contains("Server=") || value.Contains("Data Source=") || value.Contains("mongodb://"));
        }

        private static bool ValidateThumbprint(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && 
                   value.Length == 40 && 
                   value.All(c => "0123456789ABCDEFabcdef".Contains(c));
        }

        private static bool ValidateCertificate(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && 
                   (value.Contains("BEGIN CERTIFICATE") || value.Contains("BEGIN PRIVATE KEY"));
        }

        private static bool ValidateSecret(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length >= 32;
        }

        private static bool ValidateEncryptionKey(string value, EncryptionKeyType keyType)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            return keyType switch
            {
                EncryptionKeyType.AES128 => value.Length == 32, // 16 bytes hex
                EncryptionKeyType.AES256 => value.Length == 64, // 32 bytes hex
                EncryptionKeyType.RSA2048 => value.Contains("BEGIN RSA") || value.Length >= 256,
                EncryptionKeyType.RSA4096 => value.Contains("BEGIN RSA") || value.Length >= 512,
                EncryptionKeyType.Secp256k1 => value.Length == 64, // 32 bytes hex
                _ => value.Length >= 32
            };
        }

        private static bool ValidatePin(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && 
                   value.Length >= 4 && 
                   value.All(char.IsDigit);
        }

        private static bool ValidateSeed(string value)
        {
            var words = value.Split(' ');
            return words.Length == 12 || words.Length == 24; // BIP39 seed phrase
        }

        private static string FormatKeyName(string key)
        {
            // Convert various formats to readable display
            if (key.Contains('_'))
            {
                return string.Join(" ", key.Split('_').Select(word =>
                    char.ToUpper(word[0]) + word.Substring(1).ToLower()));
            }
            
            return key;
        }

        #endregion

        #region Nested Types

        private class ConfigurationItem
        {
            public string Key { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public bool Required { get; set; }
            public ConfigurationType Type { get; set; }
            public Func<string, bool>? Validator { get; set; }
            public string? EnvironmentVariable { get; set; }
            public Dictionary<string, object>? Metadata { get; set; }
        }

        #endregion
    }

    /// <summary>
    /// Configuration modes
    /// </summary>
    public enum ConfigurationMode
    {
        /// <summary>Interactive console wizard</summary>
        Interactive,
        /// <summary>Headless for automation</summary>
        Headless,
        /// <summary>GUI wizard (future)</summary>
        Gui,
        /// <summary>Web-based configuration (future)</summary>
        Web
    }

    /// <summary>
    /// Types of configuration values
    /// </summary>
    public enum ConfigurationType
    {
        ApiKey,
        ConnectionString,
        Certificate,
        OAuthSecret,
        EncryptionKey,
        WebhookSecret,
        Custom
    }

    /// <summary>
    /// Certificate formats
    /// </summary>
    public enum CertificateFormat
    {
        Thumbprint,
        PEM,
        DER,
        PKCS12
    }

    /// <summary>
    /// Encryption key types
    /// </summary>
    public enum EncryptionKeyType
    {
        AES128,
        AES256,
        RSA2048,
        RSA4096,
        Secp256k1, // Bitcoin/Ethereum
        Ed25519    // Modern elliptic curve
    }

    /// <summary>
    /// Validation rules configuration
    /// </summary>
    public class ValidationRules
    {
        public int MinimumKeyLength { get; set; } = 10;
        public bool RequireComplexPasswords { get; set; } = true;
        public bool ValidateCertificateExpiry { get; set; } = true;
        public TimeSpan CertificateExpiryWarning { get; set; } = TimeSpan.FromDays(30);
    }

    /// <summary>
    /// Encryption options
    /// </summary>
    public class EncryptionOptions
    {
        public bool UseHardwareSecurityModule { get; set; }
        public string? HsmProvider { get; set; }
        public bool EnableKeyRotation { get; set; }
        public TimeSpan KeyRotationInterval { get; set; } = TimeSpan.FromDays(90);
    }
}