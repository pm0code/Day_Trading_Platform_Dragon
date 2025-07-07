using System.Threading.Tasks;

namespace TradingPlatform.Core.Configuration
{
    /// <summary>
    /// Interface for secure configuration management.
    /// </summary>
    public interface ISecureConfiguration
    {
        /// <summary>
        /// Gets a configuration value by key.
        /// </summary>
        /// <param name="key">The configuration key</param>
        /// <returns>The configuration value</returns>
        Task<string?> GetValueAsync(string key);

        /// <summary>
        /// Sets a configuration value.
        /// </summary>
        /// <param name="key">The configuration key</param>
        /// <param name="value">The configuration value</param>
        Task SetValueAsync(string key, string value);

        /// <summary>
        /// Gets the SSL certificate path.
        /// </summary>
        /// <returns>The path to the SSL certificate</returns>
        string GetSslCertificatePath();

        /// <summary>
        /// Gets the SSL certificate password.
        /// </summary>
        /// <returns>The SSL certificate password</returns>
        string GetSslCertificatePassword();
    }
}