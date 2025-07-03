using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TradingPlatform.SecureConfiguration.Core
{
    /// <summary>
    /// Base implementation of secure configuration with AES-256 + DPAPI
    /// Suitable for banks, trading platforms, and financial institutions
    /// </summary>
    public abstract class SecureConfigurationBase : ISecureConfiguration, IDisposable
    {
        protected readonly ILogger _logger;
        protected readonly string _configPath;
        protected readonly string _keyPath;
        protected readonly object _lock = new();
        protected Dictionary<string, string> _decryptedValues = new();
        protected bool _isInitialized;
        private bool _disposed;

        protected SecureConfigurationBase(ILogger logger, string applicationName)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            if (string.IsNullOrWhiteSpace(applicationName))
                throw new ArgumentException("Application name is required", nameof(applicationName));

            // Use LocalApplicationData for user-specific storage
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                applicationName,
                "SecureConfig"
            );
            
            Directory.CreateDirectory(appDataPath);
            
            _configPath = Path.Combine(appDataPath, "config.encrypted");
            _keyPath = Path.Combine(appDataPath, "config.key");
            
            _logger.LogDebug("Secure configuration initialized for {ApplicationName}", applicationName);
        }

        #region ISecureConfiguration Implementation

        public virtual async Task<SecureConfigResult> InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing secure configuration");
                
                lock (_lock)
                {
                    if (_isInitialized)
                    {
                        _logger.LogWarning("Configuration already initialized");
                        return SecureConfigResult.Success();
                    }
                }

                // Check if this is first run
                if (!File.Exists(_configPath) || !File.Exists(_keyPath))
                {
                    _logger.LogInformation("First run detected - starting configuration setup");
                    var setupResult = await RunFirstTimeSetupAsync();
                    
                    if (!setupResult.IsSuccess)
                    {
                        return setupResult;
                    }
                }
                else
                {
                    // Load existing configuration
                    var loadResult = await LoadConfigurationAsync();
                    
                    if (!loadResult.IsSuccess)
                    {
                        return loadResult;
                    }
                }

                // Validate configuration
                var validationResult = await ValidateConfigurationAsync();
                
                if (!validationResult.IsSuccess)
                {
                    return validationResult;
                }

                lock (_lock)
                {
                    _isInitialized = true;
                }

                _logger.LogInformation("Secure configuration initialized successfully");
                return SecureConfigResult.Success(new Dictionary<string, object>
                {
                    ["KeyCount"] = _decryptedValues.Count,
                    ["ConfigPath"] = _configPath
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize secure configuration");
                return SecureConfigResult.Failure(
                    $"Initialization failed: {ex.Message}", 
                    "INIT_ERROR");
            }
        }

        public string GetValue(string key)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Configuration not initialized. Call InitializeAsync first.");

            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            lock (_lock)
            {
                if (_decryptedValues.TryGetValue(key, out var value))
                {
                    _logger.LogDebug("Retrieved value for key: {Key}", key);
                    return value;
                }
            }

            throw new KeyNotFoundException($"Configuration key '{key}' not found");
        }

        public bool TryGetValue(string key, out string? value)
        {
            value = null;
            
            if (!_isInitialized || string.IsNullOrWhiteSpace(key))
                return false;

            lock (_lock)
            {
                if (_decryptedValues.TryGetValue(key, out var foundValue))
                {
                    value = foundValue;
                    return true;
                }
            }

            return false;
        }

        public bool HasValue(string key)
        {
            if (!_isInitialized || string.IsNullOrWhiteSpace(key))
                return false;

            lock (_lock)
            {
                return _decryptedValues.ContainsKey(key) && 
                       !string.IsNullOrWhiteSpace(_decryptedValues[key]);
            }
        }

        public IReadOnlyList<string> GetConfiguredKeys()
        {
            if (!_isInitialized)
                return Array.Empty<string>();

            lock (_lock)
            {
                return _decryptedValues.Keys.ToList().AsReadOnly();
            }
        }

        public async Task<SecureConfigResult> SetValueAsync(string key, string value)
        {
            if (!_isInitialized)
                return SecureConfigResult.Failure("Configuration not initialized", "NOT_INITIALIZED");

            if (string.IsNullOrWhiteSpace(key))
                return SecureConfigResult.Failure("Key cannot be null or empty", "INVALID_KEY");

            try
            {
                Dictionary<string, string> newValues;
                
                lock (_lock)
                {
                    newValues = new Dictionary<string, string>(_decryptedValues)
                    {
                        [key] = value ?? string.Empty
                    };
                }

                var saveResult = await SaveConfigurationAsync(newValues);
                
                if (saveResult.IsSuccess)
                {
                    lock (_lock)
                    {
                        _decryptedValues = newValues;
                    }
                }

                return saveResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set value for key: {Key}", key);
                return SecureConfigResult.Failure($"Failed to set value: {ex.Message}", "SET_ERROR");
            }
        }

        public async Task<SecureConfigResult> SetValuesAsync(IDictionary<string, string> values)
        {
            if (!_isInitialized)
                return SecureConfigResult.Failure("Configuration not initialized", "NOT_INITIALIZED");

            if (values == null || values.Count == 0)
                return SecureConfigResult.Failure("No values provided", "NO_VALUES");

            try
            {
                Dictionary<string, string> newValues;
                
                lock (_lock)
                {
                    newValues = new Dictionary<string, string>(_decryptedValues);
                    
                    foreach (var kvp in values)
                    {
                        if (!string.IsNullOrWhiteSpace(kvp.Key))
                        {
                            newValues[kvp.Key] = kvp.Value ?? string.Empty;
                        }
                    }
                }

                var saveResult = await SaveConfigurationAsync(newValues);
                
                if (saveResult.IsSuccess)
                {
                    lock (_lock)
                    {
                        _decryptedValues = newValues;
                    }
                }

                return saveResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set multiple values");
                return SecureConfigResult.Failure($"Failed to set values: {ex.Message}", "SET_ERROR");
            }
        }

        public async Task<SecureConfigResult> RemoveKeyAsync(string key)
        {
            if (!_isInitialized)
                return SecureConfigResult.Failure("Configuration not initialized", "NOT_INITIALIZED");

            if (string.IsNullOrWhiteSpace(key))
                return SecureConfigResult.Failure("Key cannot be null or empty", "INVALID_KEY");

            try
            {
                Dictionary<string, string> newValues;
                
                lock (_lock)
                {
                    if (!_decryptedValues.ContainsKey(key))
                    {
                        return SecureConfigResult.Success();
                    }

                    newValues = new Dictionary<string, string>(_decryptedValues);
                    newValues.Remove(key);
                }

                var saveResult = await SaveConfigurationAsync(newValues);
                
                if (saveResult.IsSuccess)
                {
                    lock (_lock)
                    {
                        _decryptedValues = newValues;
                    }
                }

                return saveResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove key: {Key}", key);
                return SecureConfigResult.Failure($"Failed to remove key: {ex.Message}", "REMOVE_ERROR");
            }
        }

        public async Task<SecureConfigResult> ExportAsync(string exportPath)
        {
            if (!_isInitialized)
                return SecureConfigResult.Failure("Configuration not initialized", "NOT_INITIALIZED");

            try
            {
                var exportDir = Path.GetDirectoryName(exportPath);
                if (!string.IsNullOrEmpty(exportDir))
                {
                    Directory.CreateDirectory(exportDir);
                }

                // Use new secure export with Windows user binding
                var exportService = new SecureExportImport(_logger);
                var result = await exportService.ExportWithUserBindingAsync(
                    _configPath,
                    _keyPath,
                    exportPath,
                    password: null, // Can be enhanced to accept password
                    new ExportOptions
                    {
                        UseAdditionalDPAPI = true,
                        SignExport = true,
                        ExportReason = "Manual export via API"
                    });

                if (!result.IsSuccess)
                {
                    return SecureConfigResult.Failure(result.ErrorMessage ?? "Export failed", "EXPORT_ERROR");
                }

                _logger.LogInformation("Configuration exported to {Path} with user binding", exportPath);
                return SecureConfigResult.Success(new Dictionary<string, object>
                {
                    ["ExportPath"] = exportPath,
                    ["Size"] = result.ExportInfo?.FileSizeBytes ?? 0,
                    ["UserBound"] = true,
                    ["RequiresPassword"] = result.ExportInfo?.RequiresPassword ?? false
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export configuration");
                return SecureConfigResult.Failure($"Export failed: {ex.Message}", "EXPORT_ERROR");
            }
        }

        public async Task<SecureConfigResult> ImportAsync(string importPath)
        {
            try
            {
                // Use new secure import with Windows user verification
                var importService = new SecureExportImport(_logger);
                var result = await importService.ImportWithUserVerificationAsync(
                    importPath,
                    _configPath,
                    _keyPath,
                    password: null); // Can be enhanced to accept password

                if (!result.IsSuccess)
                {
                    return SecureConfigResult.Failure(result.ErrorMessage ?? "Import failed", "IMPORT_ERROR");
                }

                // Reload configuration
                var loadResult = await LoadConfigurationAsync();
                
                if (!loadResult.IsSuccess)
                {
                    return loadResult;
                }

                lock (_lock)
                {
                    _isInitialized = true;
                }

                _logger.LogInformation("Configuration imported from {Path} (originally exported by {User} on {Date})", 
                    importPath, 
                    result.ImportInfo?.OriginalUser,
                    result.ImportInfo?.OriginalExportDate);
                    
                return SecureConfigResult.Success(new Dictionary<string, object>
                {
                    ["ImportedFrom"] = importPath,
                    ["OriginalUser"] = result.ImportInfo?.OriginalUser ?? "Unknown",
                    ["OriginalMachine"] = result.ImportInfo?.OriginalMachine ?? "Unknown",
                    ["ExportDate"] = result.ImportInfo?.OriginalExportDate ?? DateTime.MinValue
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import configuration");
                return SecureConfigResult.Failure($"Import failed: {ex.Message}", "IMPORT_ERROR");
            }
        }

        public async Task<SecureConfigResult> WipeAllDataAsync()
        {
            try
            {
                _logger.LogWarning("Wiping all secure configuration data");

                lock (_lock)
                {
                    // Clear memory
                    foreach (var key in _decryptedValues.Keys.ToList())
                    {
                        _decryptedValues[key] = new string('0', _decryptedValues[key].Length);
                    }
                    _decryptedValues.Clear();
                    _isInitialized = false;
                }

                // Securely delete files
                if (File.Exists(_configPath))
                {
                    await SecureDeleteFileAsync(_configPath);
                }

                if (File.Exists(_keyPath))
                {
                    await SecureDeleteFileAsync(_keyPath);
                }

                _logger.LogInformation("All secure configuration data wiped");
                return SecureConfigResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to wipe configuration data");
                return SecureConfigResult.Failure($"Wipe failed: {ex.Message}", "WIPE_ERROR");
            }
        }

        #endregion

        #region Abstract Methods for Derived Classes

        /// <summary>
        /// Run first-time setup to collect configuration values
        /// </summary>
        protected abstract Task<SecureConfigResult> RunFirstTimeSetupAsync();

        /// <summary>
        /// Validate that all required configuration values are present
        /// </summary>
        protected abstract Task<SecureConfigResult> ValidateConfigurationAsync();

        #endregion

        #region Encryption/Decryption

        protected async Task<SecureConfigResult> SaveConfigurationAsync(Dictionary<string, string> values)
        {
            try
            {
                // Generate new AES key for each save
                using var aes = Aes.Create();
                aes.GenerateKey();
                aes.GenerateIV();

                // Protect AES key with DPAPI
                var protectedKey = ProtectedData.Protect(
                    aes.Key,
                    aes.IV, // Use IV as additional entropy
                    DataProtectionScope.CurrentUser
                );

                // Save protected key and IV
                var keyData = new
                {
                    ProtectedKey = Convert.ToBase64String(protectedKey),
                    IV = Convert.ToBase64String(aes.IV),
                    Version = "1.0",
                    Created = DateTime.UtcNow
                };

                await File.WriteAllTextAsync(_keyPath, JsonSerializer.Serialize(keyData, 
                    new JsonSerializerOptions { WriteIndented = true }));

                // Encrypt configuration
                var jsonData = JsonSerializer.Serialize(values);
                var encrypted = EncryptStringToBytes(jsonData, aes.Key, aes.IV);
                await File.WriteAllBytesAsync(_configPath, encrypted);

                _logger.LogInformation("Configuration saved successfully ({Count} keys)", values.Count);
                return SecureConfigResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save configuration");
                return SecureConfigResult.Failure($"Save failed: {ex.Message}", "SAVE_ERROR");
            }
        }

        protected async Task<SecureConfigResult> LoadConfigurationAsync()
        {
            try
            {
                // Load protected key
                var keyJson = await File.ReadAllTextAsync(_keyPath);
                var keyData = JsonSerializer.Deserialize<KeyData>(keyJson);

                if (keyData == null)
                {
                    return SecureConfigResult.Failure("Invalid key file format", "INVALID_KEY_FILE");
                }

                // Unprotect AES key
                var protectedKey = Convert.FromBase64String(keyData.ProtectedKey);
                var iv = Convert.FromBase64String(keyData.IV);

                byte[] aesKey;
                try
                {
                    aesKey = ProtectedData.Unprotect(
                        protectedKey,
                        iv,
                        DataProtectionScope.CurrentUser
                    );
                }
                catch (CryptographicException)
                {
                    return SecureConfigResult.Failure(
                        "Failed to decrypt configuration. The file may be corrupted or was encrypted by a different user.",
                        "DECRYPT_ERROR");
                }

                // Load and decrypt configuration
                var encrypted = await File.ReadAllBytesAsync(_configPath);
                var decrypted = DecryptStringFromBytes(encrypted, aesKey, iv);

                var values = JsonSerializer.Deserialize<Dictionary<string, string>>(decrypted);

                if (values == null)
                {
                    return SecureConfigResult.Failure("Invalid configuration format", "INVALID_CONFIG");
                }

                lock (_lock)
                {
                    _decryptedValues = values;
                }

                // Clear sensitive data
                Array.Clear(aesKey, 0, aesKey.Length);

                _logger.LogInformation("Configuration loaded successfully ({Count} keys)", values.Count);
                return SecureConfigResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration");
                return SecureConfigResult.Failure($"Load failed: {ex.Message}", "LOAD_ERROR");
            }
        }

        private byte[] EncryptStringToBytes(string plainText, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            using var msEncrypt = new MemoryStream();
            using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            using var swEncrypt = new StreamWriter(csEncrypt, Encoding.UTF8);

            swEncrypt.Write(plainText);
            swEncrypt.Flush();
            csEncrypt.FlushFinalBlock();

            return msEncrypt.ToArray();
        }

        private string DecryptStringFromBytes(byte[] cipherText, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            using var msDecrypt = new MemoryStream(cipherText);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt, Encoding.UTF8);

            return srDecrypt.ReadToEnd();
        }

        #endregion

        #region Helper Methods

        private async Task SecureDeleteFileAsync(string path)
        {
            if (!File.Exists(path))
                return;

            // Overwrite with random data
            var fileInfo = new FileInfo(path);
            var randomData = new byte[fileInfo.Length];
            
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomData);
            }

            await File.WriteAllBytesAsync(path, randomData);
            
            // Delete
            File.Delete(path);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    lock (_lock)
                    {
                        // Clear sensitive data from memory
                        foreach (var key in _decryptedValues.Keys.ToList())
                        {
                            _decryptedValues[key] = new string('0', _decryptedValues[key].Length);
                        }
                        _decryptedValues.Clear();
                    }
                }

                _disposed = true;
            }
        }

        #endregion

        #region Helper Classes

        protected class KeyData
        {
            public string ProtectedKey { get; set; } = string.Empty;
            public string IV { get; set; } = string.Empty;
            public string Version { get; set; } = string.Empty;
            public DateTime Created { get; set; }
        }

        #endregion
    }
}