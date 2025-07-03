using System.Threading.Tasks;
using TradingPlatform.SecureConfiguration.Core;

namespace TradingPlatform.SecureConfiguration.Core
{
    /// <summary>
    /// Extended interface for secure configuration with advanced export/import
    /// </summary>
    public interface ISecureConfigurationExtended : ISecureConfiguration
    {
        /// <summary>
        /// Export configuration with password protection and Windows user binding
        /// </summary>
        /// <param name="exportPath">Path to export to</param>
        /// <param name="password">Optional password for additional protection</param>
        /// <param name="options">Export options</param>
        Task<SecureConfigResult> ExportWithPasswordAsync(
            string exportPath, 
            string password,
            ExportOptions? options = null);

        /// <summary>
        /// Import configuration with password and Windows user verification
        /// </summary>
        /// <param name="importPath">Path to import from</param>
        /// <param name="password">Password if the export was password-protected</param>
        Task<SecureConfigResult> ImportWithPasswordAsync(
            string importPath,
            string password);

        /// <summary>
        /// Verify if an export file can be imported by the current user
        /// </summary>
        /// <param name="exportPath">Path to export file to verify</param>
        /// <returns>Information about the export file</returns>
        Task<ExportVerificationResult> VerifyExportFileAsync(string exportPath);
    }

    /// <summary>
    /// Result of export file verification
    /// </summary>
    public class ExportVerificationResult
    {
        public bool CanImport { get; set; }
        public string? Reason { get; set; }
        public string? ExportedBy { get; set; }
        public DateTime? ExportedAt { get; set; }
        public string? ExportedMachine { get; set; }
        public bool RequiresPassword { get; set; }
        public bool IsExpired { get; set; }
        public DateTime? ExpiresAt { get; set; }
        
        public static ExportVerificationResult Success(
            string exportedBy,
            DateTime exportedAt,
            string machine,
            bool requiresPassword = false)
        {
            return new ExportVerificationResult
            {
                CanImport = true,
                ExportedBy = exportedBy,
                ExportedAt = exportedAt,
                ExportedMachine = machine,
                RequiresPassword = requiresPassword
            };
        }
        
        public static ExportVerificationResult Failure(string reason)
        {
            return new ExportVerificationResult
            {
                CanImport = false,
                Reason = reason
            };
        }
    }
}