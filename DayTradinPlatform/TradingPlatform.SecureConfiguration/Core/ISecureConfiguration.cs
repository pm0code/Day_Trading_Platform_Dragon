using System.Threading.Tasks;

namespace TradingPlatform.SecureConfiguration.Core
{
    /// <summary>
    /// Core interface for secure configuration management
    /// Suitable for any financial institution requiring encrypted secrets
    /// </summary>
    public interface ISecureConfiguration
    {
        /// <summary>
        /// Initialize the secure configuration system
        /// Will run first-time setup if no configuration exists
        /// </summary>
        Task<SecureConfigResult> InitializeAsync();

        /// <summary>
        /// Get a decrypted value by key
        /// </summary>
        /// <param name="key">The configuration key</param>
        /// <returns>Decrypted value</returns>
        /// <exception cref="KeyNotFoundException">If key doesn't exist</exception>
        /// <exception cref="InvalidOperationException">If not initialized</exception>
        string GetValue(string key);

        /// <summary>
        /// Try to get a decrypted value by key
        /// </summary>
        /// <param name="key">The configuration key</param>
        /// <param name="value">The decrypted value if found</param>
        /// <returns>True if key exists and was retrieved</returns>
        bool TryGetValue(string key, out string? value);

        /// <summary>
        /// Check if a key exists and has a non-empty value
        /// </summary>
        bool HasValue(string key);

        /// <summary>
        /// Get all configured keys (names only, not values)
        /// </summary>
        IReadOnlyList<string> GetConfiguredKeys();

        /// <summary>
        /// Update or add a single value (will re-encrypt entire configuration)
        /// </summary>
        Task<SecureConfigResult> SetValueAsync(string key, string value);

        /// <summary>
        /// Update multiple values at once (more efficient than individual updates)
        /// </summary>
        Task<SecureConfigResult> SetValuesAsync(IDictionary<string, string> values);

        /// <summary>
        /// Remove a key from configuration
        /// </summary>
        Task<SecureConfigResult> RemoveKeyAsync(string key);

        /// <summary>
        /// Export configuration to another location (remains encrypted)
        /// </summary>
        Task<SecureConfigResult> ExportAsync(string exportPath);

        /// <summary>
        /// Import configuration from another location
        /// </summary>
        Task<SecureConfigResult> ImportAsync(string importPath);

        /// <summary>
        /// Securely wipe all configuration data
        /// </summary>
        Task<SecureConfigResult> WipeAllDataAsync();
    }

    /// <summary>
    /// Result of secure configuration operations
    /// </summary>
    public class SecureConfigResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorCode { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }

        public static SecureConfigResult Success(Dictionary<string, object>? metadata = null)
        {
            return new SecureConfigResult 
            { 
                IsSuccess = true,
                Metadata = metadata
            };
        }

        public static SecureConfigResult Failure(string errorMessage, string errorCode = "UNKNOWN")
        {
            return new SecureConfigResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }
}