using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TradingPlatform.SecureConfiguration.Core
{
    /// <summary>
    /// Secure export/import functionality tied to Windows user credentials
    /// Ensures exported configurations can only be imported by the same Windows user
    /// </summary>
    public class SecureExportImport
    {
        private readonly ILogger<SecureExportImport> _logger;
        private const string ExportVersion = "2.0";
        private const int Pbkdf2Iterations = 100000;
        private const int SaltSize = 32;
        
        public SecureExportImport(ILogger<SecureExportImport> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Export configuration with Windows user binding
        /// </summary>
        public async Task<ExportResult> ExportWithUserBindingAsync(
            string configPath,
            string keyPath,
            string exportPath,
            string? password = null,
            ExportOptions? options = null)
        {
            try
            {
                _logger.LogInformation("Starting secure export with user binding");
                
                options ??= new ExportOptions();
                
                // Get Windows user information
                var userInfo = GetWindowsUserInfo();
                _logger.LogDebug("Exporting for user: {UserName}@{Domain}", 
                    userInfo.UserName, userInfo.Domain);
                
                // Read source files
                if (!File.Exists(configPath) || !File.Exists(keyPath))
                {
                    return ExportResult.Failure("Configuration files not found");
                }
                
                var configData = await File.ReadAllBytesAsync(configPath);
                var keyData = await File.ReadAllTextAsync(keyPath);
                
                // Create export container
                var exportContainer = new SecureExportContainer
                {
                    Version = ExportVersion,
                    ExportedAt = DateTime.UtcNow,
                    MachineName = Environment.MachineName,
                    UserBinding = userInfo,
                    Metadata = new ExportMetadata
                    {
                        OriginalConfigPath = options.IncludePaths ? configPath : null,
                        ConfigSizeBytes = configData.Length,
                        ExportReason = options.ExportReason,
                        ExpiresAt = options.ExpiryTime
                    }
                };
                
                // Generate random salt for this export
                var salt = GenerateSalt();
                exportContainer.Salt = Convert.ToBase64String(salt);
                
                // Derive key from Windows credentials + optional password
                var exportKey = DeriveExportKey(userInfo, password, salt);
                
                // Create payload
                var payload = new ExportPayload
                {
                    ConfigData = Convert.ToBase64String(configData),
                    KeyData = keyData,
                    Checksum = ComputeChecksum(configData, keyData)
                };
                
                var payloadJson = JsonSerializer.Serialize(payload);
                var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
                
                // Encrypt payload with derived key
                using var aes = Aes.Create();
                aes.Key = exportKey;
                aes.GenerateIV();
                
                var encryptedPayload = EncryptData(payloadBytes, aes.Key, aes.IV);
                
                exportContainer.EncryptedPayload = Convert.ToBase64String(encryptedPayload);
                exportContainer.IV = Convert.ToBase64String(aes.IV);
                
                // Sign the container
                if (options.SignExport)
                {
                    exportContainer.Signature = SignContainer(exportContainer, userInfo);
                }
                
                // Additional protection with DPAPI
                var containerJson = JsonSerializer.Serialize(exportContainer, 
                    new JsonSerializerOptions { WriteIndented = true });
                var containerBytes = Encoding.UTF8.GetBytes(containerJson);
                
                byte[] finalData;
                if (options.UseAdditionalDPAPI)
                {
                    // Extra layer of DPAPI protection
                    finalData = ProtectedData.Protect(
                        containerBytes,
                        salt, // Use salt as entropy
                        DataProtectionScope.CurrentUser);
                    
                    // Add header to indicate DPAPI protection
                    var header = Encoding.UTF8.GetBytes("DPAPI:");
                    finalData = CombineArrays(header, finalData);
                }
                else
                {
                    finalData = containerBytes;
                }
                
                // Write to file
                await File.WriteAllBytesAsync(exportPath, finalData);
                
                // Clear sensitive data
                Array.Clear(exportKey, 0, exportKey.Length);
                Array.Clear(payloadBytes, 0, payloadBytes.Length);
                
                _logger.LogInformation("Export completed successfully to {Path}", exportPath);
                
                return ExportResult.Success(new ExportInfo
                {
                    ExportPath = exportPath,
                    FileSizeBytes = finalData.Length,
                    RequiresPassword = !string.IsNullOrEmpty(password),
                    ExpiresAt = options.ExpiryTime,
                    UserBound = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export configuration");
                return ExportResult.Failure($"Export failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Import configuration with Windows user verification
        /// </summary>
        public async Task<ImportResult> ImportWithUserVerificationAsync(
            string importPath,
            string targetConfigPath,
            string targetKeyPath,
            string? password = null)
        {
            try
            {
                _logger.LogInformation("Starting secure import with user verification");
                
                if (!File.Exists(importPath))
                {
                    return ImportResult.Failure("Import file not found");
                }
                
                // Read import file
                var importData = await File.ReadAllBytesAsync(importPath);
                
                // Check for DPAPI header
                byte[] containerBytes;
                if (importData.Length > 6 && Encoding.UTF8.GetString(importData, 0, 6) == "DPAPI:")
                {
                    _logger.LogDebug("Import file has DPAPI protection");
                    
                    try
                    {
                        // Remove header and unprotect
                        var protectedData = new byte[importData.Length - 6];
                        Array.Copy(importData, 6, protectedData, 0, protectedData.Length);
                        
                        containerBytes = ProtectedData.Unprotect(
                            protectedData,
                            null, // Salt will be in container
                            DataProtectionScope.CurrentUser);
                    }
                    catch (CryptographicException)
                    {
                        return ImportResult.Failure(
                            "Failed to decrypt DPAPI protection. This file was exported by a different Windows user.");
                    }
                }
                else
                {
                    containerBytes = importData;
                }
                
                // Parse container
                var containerJson = Encoding.UTF8.GetString(containerBytes);
                var container = JsonSerializer.Deserialize<SecureExportContainer>(containerJson);
                
                if (container == null)
                {
                    return ImportResult.Failure("Invalid export file format");
                }
                
                // Verify version compatibility
                if (container.Version != ExportVersion)
                {
                    return ImportResult.Failure(
                        $"Incompatible export version. Expected {ExportVersion}, found {container.Version}");
                }
                
                // Check expiry
                if (container.Metadata?.ExpiresAt != null && 
                    DateTime.UtcNow > container.Metadata.ExpiresAt)
                {
                    return ImportResult.Failure("Export has expired");
                }
                
                // Verify user binding
                var currentUser = GetWindowsUserInfo();
                if (!VerifyUserBinding(container.UserBinding, currentUser))
                {
                    _logger.LogWarning(
                        "User binding verification failed. Export user: {ExportUser}, Current user: {CurrentUser}",
                        $"{container.UserBinding.UserName}@{container.UserBinding.Domain}",
                        $"{currentUser.UserName}@{currentUser.Domain}");
                    
                    return ImportResult.Failure(
                        "This export was created by a different Windows user and cannot be imported");
                }
                
                // Verify signature if present
                if (!string.IsNullOrEmpty(container.Signature))
                {
                    if (!VerifySignature(container, currentUser))
                    {
                        return ImportResult.Failure("Export signature verification failed");
                    }
                }
                
                // Derive decryption key
                var salt = Convert.FromBase64String(container.Salt);
                var decryptionKey = DeriveExportKey(currentUser, password, salt);
                
                // Decrypt payload
                var encryptedPayload = Convert.FromBase64String(container.EncryptedPayload);
                var iv = Convert.FromBase64String(container.IV);
                
                byte[] payloadBytes;
                try
                {
                    payloadBytes = DecryptData(encryptedPayload, decryptionKey, iv);
                }
                catch (CryptographicException)
                {
                    return ImportResult.Failure(
                        "Failed to decrypt payload. Incorrect password or corrupted file");
                }
                finally
                {
                    Array.Clear(decryptionKey, 0, decryptionKey.Length);
                }
                
                // Parse payload
                var payloadJson = Encoding.UTF8.GetString(payloadBytes);
                var payload = JsonSerializer.Deserialize<ExportPayload>(payloadJson);
                
                if (payload == null)
                {
                    return ImportResult.Failure("Invalid payload format");
                }
                
                // Verify checksum
                var configData = Convert.FromBase64String(payload.ConfigData);
                var expectedChecksum = ComputeChecksum(configData, payload.KeyData);
                
                if (payload.Checksum != expectedChecksum)
                {
                    return ImportResult.Failure("Checksum verification failed. File may be corrupted");
                }
                
                // Backup existing files if they exist
                if (File.Exists(targetConfigPath))
                {
                    var backupPath = targetConfigPath + $".backup_{DateTime.Now:yyyyMMddHHmmss}";
                    await File.CopyAsync(targetConfigPath, backupPath);
                    _logger.LogInformation("Backed up existing config to {Path}", backupPath);
                }
                
                if (File.Exists(targetKeyPath))
                {
                    var backupPath = targetKeyPath + $".backup_{DateTime.Now:yyyyMMddHHmmss}";
                    await File.CopyAsync(targetKeyPath, backupPath);
                    _logger.LogInformation("Backed up existing key to {Path}", backupPath);
                }
                
                // Write imported files
                await File.WriteAllBytesAsync(targetConfigPath, configData);
                await File.WriteAllTextAsync(targetKeyPath, payload.KeyData);
                
                // Clear sensitive data
                Array.Clear(payloadBytes, 0, payloadBytes.Length);
                
                _logger.LogInformation("Import completed successfully");
                
                return ImportResult.Success(new ImportInfo
                {
                    ImportedFrom = importPath,
                    OriginalExportDate = container.ExportedAt,
                    OriginalMachine = container.MachineName,
                    OriginalUser = $"{container.UserBinding.UserName}@{container.UserBinding.Domain}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import configuration");
                return ImportResult.Failure($"Import failed: {ex.Message}");
            }
        }

        #region Helper Methods

        private WindowsUserInfo GetWindowsUserInfo()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            
            return new WindowsUserInfo
            {
                UserName = identity.Name.Split('\\').LastOrDefault() ?? identity.Name,
                Domain = identity.Name.Contains('\\') 
                    ? identity.Name.Split('\\').First() 
                    : Environment.UserDomainName,
                SID = identity.User?.Value ?? string.Empty,
                AuthenticationType = identity.AuthenticationType,
                IsAdministrator = principal.IsInRole(WindowsBuiltInRole.Administrator)
            };
        }

        private bool VerifyUserBinding(WindowsUserInfo exportUser, WindowsUserInfo currentUser)
        {
            // Primary check: SID must match
            if (!string.IsNullOrEmpty(exportUser.SID) && 
                !string.IsNullOrEmpty(currentUser.SID))
            {
                return exportUser.SID.Equals(currentUser.SID, StringComparison.OrdinalIgnoreCase);
            }
            
            // Fallback: Domain and username must match
            return exportUser.Domain.Equals(currentUser.Domain, StringComparison.OrdinalIgnoreCase) &&
                   exportUser.UserName.Equals(currentUser.UserName, StringComparison.OrdinalIgnoreCase);
        }

        private byte[] DeriveExportKey(WindowsUserInfo userInfo, string? password, byte[] salt)
        {
            // Combine user info and optional password
            var baseKey = $"{userInfo.SID}|{userInfo.Domain}|{userInfo.UserName}|{password ?? string.Empty}";
            
            using var pbkdf2 = new Rfc2898DeriveBytes(
                baseKey,
                salt,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256);
            
            return pbkdf2.GetBytes(32); // 256-bit key
        }

        private string ComputeChecksum(byte[] configData, string keyData)
        {
            using var sha256 = SHA256.Create();
            var combined = CombineArrays(configData, Encoding.UTF8.GetBytes(keyData));
            var hash = sha256.ComputeHash(combined);
            return Convert.ToBase64String(hash);
        }

        private string SignContainer(SecureExportContainer container, WindowsUserInfo userInfo)
        {
            // Create signature data
            var signatureData = $"{container.Version}|{container.ExportedAt:O}|" +
                               $"{container.MachineName}|{userInfo.SID}|" +
                               $"{container.EncryptedPayload}";
            
            // Use DPAPI to create a signature
            var signatureBytes = Encoding.UTF8.GetBytes(signatureData);
            var protectedSignature = ProtectedData.Protect(
                signatureBytes,
                Encoding.UTF8.GetBytes(container.Salt),
                DataProtectionScope.CurrentUser);
            
            return Convert.ToBase64String(protectedSignature);
        }

        private bool VerifySignature(SecureExportContainer container, WindowsUserInfo currentUser)
        {
            try
            {
                // Recreate signature data
                var signatureData = $"{container.Version}|{container.ExportedAt:O}|" +
                                   $"{container.MachineName}|{currentUser.SID}|" +
                                   $"{container.EncryptedPayload}";
                
                // Try to unprotect the signature
                var protectedSignature = Convert.FromBase64String(container.Signature);
                var unprotected = ProtectedData.Unprotect(
                    protectedSignature,
                    Encoding.UTF8.GetBytes(container.Salt),
                    DataProtectionScope.CurrentUser);
                
                var originalData = Encoding.UTF8.GetString(unprotected);
                return signatureData == originalData;
            }
            catch
            {
                return false;
            }
        }

        private byte[] GenerateSalt()
        {
            var salt = new byte[SaltSize];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return salt;
        }

        private byte[] EncryptData(byte[] data, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            
            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
            
            cs.Write(data, 0, data.Length);
            cs.FlushFinalBlock();
            
            return ms.ToArray();
        }

        private byte[] DecryptData(byte[] encryptedData, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            
            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(encryptedData);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var result = new MemoryStream();
            
            cs.CopyTo(result);
            return result.ToArray();
        }

        private byte[] CombineArrays(byte[] first, byte[] second)
        {
            var combined = new byte[first.Length + second.Length];
            Array.Copy(first, 0, combined, 0, first.Length);
            Array.Copy(second, 0, combined, first.Length, second.Length);
            return combined;
        }

        #endregion

        #region Nested Types

        private class SecureExportContainer
        {
            public string Version { get; set; } = string.Empty;
            public DateTime ExportedAt { get; set; }
            public string MachineName { get; set; } = string.Empty;
            public WindowsUserInfo UserBinding { get; set; } = new();
            public string Salt { get; set; } = string.Empty;
            public string IV { get; set; } = string.Empty;
            public string EncryptedPayload { get; set; } = string.Empty;
            public string? Signature { get; set; }
            public ExportMetadata? Metadata { get; set; }
        }

        private class WindowsUserInfo
        {
            public string UserName { get; set; } = string.Empty;
            public string Domain { get; set; } = string.Empty;
            public string SID { get; set; } = string.Empty;
            public string AuthenticationType { get; set; } = string.Empty;
            public bool IsAdministrator { get; set; }
        }

        private class ExportPayload
        {
            public string ConfigData { get; set; } = string.Empty;
            public string KeyData { get; set; } = string.Empty;
            public string Checksum { get; set; } = string.Empty;
        }

        private class ExportMetadata
        {
            public string? OriginalConfigPath { get; set; }
            public long ConfigSizeBytes { get; set; }
            public string? ExportReason { get; set; }
            public DateTime? ExpiresAt { get; set; }
        }

        #endregion
    }

    #region Public Types

    /// <summary>
    /// Options for exporting configuration
    /// </summary>
    public class ExportOptions
    {
        /// <summary>
        /// Add an additional layer of DPAPI protection
        /// </summary>
        public bool UseAdditionalDPAPI { get; set; } = true;
        
        /// <summary>
        /// Sign the export with user credentials
        /// </summary>
        public bool SignExport { get; set; } = true;
        
        /// <summary>
        /// Include original file paths in metadata
        /// </summary>
        public bool IncludePaths { get; set; } = false;
        
        /// <summary>
        /// Optional expiry time for the export
        /// </summary>
        public DateTime? ExpiryTime { get; set; }
        
        /// <summary>
        /// Reason for export (audit trail)
        /// </summary>
        public string? ExportReason { get; set; }
    }

    /// <summary>
    /// Result of export operation
    /// </summary>
    public class ExportResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public ExportInfo? ExportInfo { get; set; }
        
        public static ExportResult Success(ExportInfo info) => new()
        {
            IsSuccess = true,
            ExportInfo = info
        };
        
        public static ExportResult Failure(string error) => new()
        {
            IsSuccess = false,
            ErrorMessage = error
        };
    }

    /// <summary>
    /// Information about completed export
    /// </summary>
    public class ExportInfo
    {
        public string ExportPath { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public bool RequiresPassword { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool UserBound { get; set; }
    }

    /// <summary>
    /// Result of import operation
    /// </summary>
    public class ImportResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public ImportInfo? ImportInfo { get; set; }
        
        public static ImportResult Success(ImportInfo info) => new()
        {
            IsSuccess = true,
            ImportInfo = info
        };
        
        public static ImportResult Failure(string error) => new()
        {
            IsSuccess = false,
            ErrorMessage = error
        };
    }

    /// <summary>
    /// Information about completed import
    /// </summary>
    public class ImportInfo
    {
        public string ImportedFrom { get; set; } = string.Empty;
        public DateTime OriginalExportDate { get; set; }
        public string OriginalMachine { get; set; } = string.Empty;
        public string OriginalUser { get; set; } = string.Empty;
    }

    #endregion
}