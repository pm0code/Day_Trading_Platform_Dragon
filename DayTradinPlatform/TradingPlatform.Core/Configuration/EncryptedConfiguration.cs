using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Interfaces;
using TradingPlatform.Foundation.Enums;
using System.Threading.Tasks;
using System.Linq;

namespace TradingPlatform.Core.Configuration
{
    /// <summary>
    /// Secure configuration management with automatic encryption
    /// Uses AES-256 with DPAPI-protected key on Windows
    /// </summary>
    public class EncryptedConfiguration : CanonicalServiceBase, IDisposable
    {
        private readonly string _configPath;
        private readonly string _keyPath;
        private Dictionary<string, string> _decryptedKeys = new();
        private readonly object _lock = new();
        private ServiceHealth _healthStatus = ServiceHealth.Unknown;
        private string _healthMessage = "Not initialized";
        
        // Required API keys
        private static readonly string[] RequiredKeys = new[]
        {
            "AlphaVantage",
            "Finnhub"
        };
        
        // Optional API keys
        private static readonly string[] OptionalKeys = new[]
        {
            "IexCloud",
            "Polygon",
            "TwelveData"
        };

        public EncryptedConfiguration(ITradingLogger logger) : base(logger, "EncryptedConfiguration")
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TradingPlatform"
            );
            
            Directory.CreateDirectory(appDataPath);
            
            _configPath = Path.Combine(appDataPath, "config.encrypted");
            _keyPath = Path.Combine(appDataPath, "config.key");
            
            LogMethodEntry();
        }

        /// <summary>
        /// Initialize configuration, run first-time setup if needed
        /// </summary>
        public async Task<TradingResult> InitializeAsync()
        {
            try
            {
                LogInfo("Initializing encrypted configuration");
                
                // Check if this is first run
                if (!File.Exists(_configPath) || !File.Exists(_keyPath))
                {
                    LogInfo("First run detected - starting configuration wizard");
                    var setupResult = await RunFirstTimeSetup();
                    
                    if (!setupResult.IsSuccess)
                    {
                        return TradingResult.Failure("CONFIG_SETUP_FAILED", setupResult.Error?.Message ?? "Setup failed");
                    }
                }
                
                // Load and decrypt existing configuration
                var loadResult = await LoadConfiguration();
                
                if (!loadResult.IsSuccess)
                {
                    return TradingResult.Failure("CONFIG_LOAD_FAILED", loadResult.Error?.Message ?? "Load failed");
                }
                
                // Validate all required keys are present
                var validationResult = ValidateConfiguration();
                
                if (!validationResult.IsSuccess)
                {
                    return TradingResult.Failure("CONFIG_VALIDATION_FAILED", validationResult.Error?.Message ?? "Validation failed");
                }
                
                SetHealthStatus(ServiceHealth.Healthy, "Configuration loaded and validated");
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                LogError("Failed to initialize encrypted configuration", ex, "Configuration initialization", "Configuration unavailable", "Check configuration files and permissions");
                return TradingResult.Failure("CONFIG_INIT_FAILED", $"Configuration initialization failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get decrypted API key
        /// </summary>
        public string GetApiKey(string keyName)
        {
            LogMethodEntry(new { keyName });
            
            lock (_lock)
            {
                if (_decryptedKeys.TryGetValue(keyName, out var value))
                {
                    LogDebug($"Retrieved API key for {keyName}");
                    return value;
                }
                
                throw new InvalidOperationException($"API key '{keyName}' not found in configuration");
            }
        }

        /// <summary>
        /// Check if an optional API key is configured
        /// </summary>
        public bool HasApiKey(string keyName)
        {
            lock (_lock)
            {
                return _decryptedKeys.ContainsKey(keyName) && 
                       !string.IsNullOrWhiteSpace(_decryptedKeys[keyName]);
            }
        }

        #region First Time Setup

        private async Task<TradingResult> RunFirstTimeSetup()
        {
            LogMethodEntry();
            
            try
            {
                Console.Clear();
                Console.WriteLine("=====================================");
                Console.WriteLine("  Trading Platform Configuration");
                Console.WriteLine("=====================================");
                Console.WriteLine();
                Console.WriteLine("This appears to be your first time running the platform.");
                Console.WriteLine("Let's set up your API keys securely.");
                Console.WriteLine();
                Console.WriteLine("Your keys will be encrypted and stored locally.");
                Console.WriteLine("You'll never need to enter them again.");
                Console.WriteLine();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
                
                var apiKeys = new Dictionary<string, string>();
                
                // Collect required keys
                Console.WriteLine("\n--- Required API Keys ---");
                foreach (var keyName in RequiredKeys)
                {
                    var key = CollectApiKey(keyName, required: true);
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        return TradingResult.Failure("MISSING_REQUIRED_KEY", $"Required API key '{keyName}' was not provided");
                    }
                    apiKeys[keyName] = key;
                }
                
                // Collect optional keys
                Console.WriteLine("\n--- Optional API Keys ---");
                Console.WriteLine("Press Enter to skip any optional keys.");
                
                foreach (var keyName in OptionalKeys)
                {
                    var key = CollectApiKey(keyName, required: false);
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        apiKeys[keyName] = key;
                    }
                }
                
                // Encrypt and save configuration
                Console.WriteLine("\nEncrypting configuration...");
                var saveResult = await SaveConfiguration(apiKeys);
                
                if (!saveResult.IsSuccess)
                {
                    return TradingResult.Failure("SAVE_FAILED", saveResult.Error?.Message ?? "Save failed");
                }
                
                Console.WriteLine("\n✅ Configuration saved successfully!");
                Console.WriteLine("Your API keys have been encrypted and stored.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey(true);
                Console.Clear();
                
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                LogError("First-time setup failed", ex);
                return TradingResult.Failure("SETUP_FAILED", $"Setup failed: {ex.Message}", ex);
            }
        }

        private string CollectApiKey(string keyName, bool required)
        {
            while (true)
            {
                Console.WriteLine($"\n{keyName} API Key{(required ? " (required)" : " (optional)")}:");
                Console.Write("> ");
                
                var key = ReadPassword();
                
                if (string.IsNullOrWhiteSpace(key))
                {
                    if (!required)
                    {
                        return string.Empty;
                    }
                    
                    Console.WriteLine("This key is required. Please enter a valid API key.");
                    continue;
                }
                
                // Basic validation
                if (key.Length < 10)
                {
                    Console.WriteLine("API key seems too short. Please check and re-enter.");
                    continue;
                }
                
                Console.WriteLine($"✓ {keyName} key accepted");
                return key.Trim();
            }
        }

        private string ReadPassword()
        {
            var password = new StringBuilder();
            ConsoleKeyInfo key;
            
            do
            {
                key = Console.ReadKey(intercept: true);
                
                if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password.Remove(password.Length - 1, 1);
                    Console.Write("\b \b");
                }
                else if (key.Key != ConsoleKey.Enter && key.Key != ConsoleKey.Backspace)
                {
                    password.Append(key.KeyChar);
                    Console.Write("*");
                }
            }
            while (key.Key != ConsoleKey.Enter);
            
            Console.WriteLine();
            return password.ToString();
        }

        #endregion

        #region Encryption/Decryption

        private async Task<TradingResult> SaveConfiguration(Dictionary<string, string> apiKeys)
        {
            LogMethodEntry();
            
            try
            {
                // Generate a random AES key
                using var aes = Aes.Create();
                aes.GenerateKey();
                aes.GenerateIV();
                
                // Protect the AES key using DPAPI (Windows)
                var protectedKey = ProtectedData.Protect(
                    aes.Key,
                    aes.IV, // Use IV as additional entropy
                    DataProtectionScope.CurrentUser
                );
                
                // Save protected key and IV
                var keyData = new
                {
                    ProtectedKey = Convert.ToBase64String(protectedKey),
                    IV = Convert.ToBase64String(aes.IV)
                };
                
                await File.WriteAllTextAsync(_keyPath, JsonSerializer.Serialize(keyData));
                LogDebug("Encryption key saved (DPAPI protected)");
                
                // Encrypt the configuration
                var jsonData = JsonSerializer.Serialize(apiKeys);
                var encrypted = EncryptStringToBytes(jsonData, aes.Key, aes.IV);
                await File.WriteAllBytesAsync(_configPath, encrypted);
                
                LogDebug($"Configuration encrypted and saved ({encrypted.Length} bytes)");
                
                // Load into memory
                lock (_lock)
                {
                    _decryptedKeys = new Dictionary<string, string>(apiKeys);
                }
                
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                LogError("Failed to save configuration", ex);
                return TradingResult.Failure("SAVE_FAILED", $"Save failed: {ex.Message}", ex);
            }
        }

        private async Task<TradingResult> LoadConfiguration()
        {
            LogMethodEntry();
            
            try
            {
                // Load the protected key
                var keyJson = await File.ReadAllTextAsync(_keyPath);
                var keyData = JsonSerializer.Deserialize<KeyData>(keyJson);
                
                if (keyData == null)
                {
                    LogError("Invalid key file format");
                    LogMethodExit();
                    return TradingResult.Failure("INVALID_KEY_FORMAT", "Invalid key file format");
                }
                
                // Unprotect the AES key using DPAPI
                var protectedKey = Convert.FromBase64String(keyData.ProtectedKey);
                var iv = Convert.FromBase64String(keyData.IV);
                
                var aesKey = ProtectedData.Unprotect(
                    protectedKey,
                    iv,
                    DataProtectionScope.CurrentUser
                );
                
                LogInfo("Encryption key loaded and unprotected");
                
                // Load and decrypt configuration
                var encrypted = await File.ReadAllBytesAsync(_configPath);
                var decrypted = DecryptStringFromBytes(encrypted, aesKey, iv);
                
                var apiKeys = JsonSerializer.Deserialize<Dictionary<string, string>>(decrypted);
                
                if (apiKeys == null)
                {
                    LogError("Invalid configuration format");
                    LogMethodExit();
                    return TradingResult.Failure("INVALID_CONFIG_FORMAT", "Invalid configuration format");
                }
                
                lock (_lock)
                {
                    _decryptedKeys = apiKeys;
                }
                
                LogInfo($"Configuration loaded successfully ({apiKeys.Count} keys)");
                LogMethodExit();
                return TradingResult.Success();
            }
            catch (CryptographicException ex)
            {
                LogError("Failed to decrypt configuration - possible corruption or different user account", ex);
                LogMethodExit();
                return TradingResult.Failure("DECRYPTION_FAILED", "Failed to decrypt configuration. The file may be corrupted or was encrypted by a different user.");
            }
            catch (Exception ex)
            {
                LogError("Failed to load configuration", ex);
                LogMethodExit();
                return TradingResult.Failure("LOAD_FAILED", $"Load failed: {ex.Message}");
            }
        }

        private byte[] EncryptStringToBytes(string plainText, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            
            using var encryptor = aes.CreateEncryptor();
            using var msEncrypt = new MemoryStream();
            using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            using var swEncrypt = new StreamWriter(csEncrypt);
            
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
            
            using var decryptor = aes.CreateDecryptor();
            using var msDecrypt = new MemoryStream(cipherText);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);
            
            return srDecrypt.ReadToEnd();
        }

        #endregion

        #region Validation

        private TradingResult ValidateConfiguration()
        {
            var missingKeys = new List<string>();
            
            foreach (var requiredKey in RequiredKeys)
            {
                if (!_decryptedKeys.ContainsKey(requiredKey) || 
                    string.IsNullOrWhiteSpace(_decryptedKeys[requiredKey]))
                {
                    missingKeys.Add(requiredKey);
                }
            }
            
            if (missingKeys.Count > 0)
            {
                return TradingResult.Failure(
                    "MISSING_API_KEYS",
                    $"Missing required API keys: {string.Join(", ", missingKeys)}");
            }
            
            LogInfo($"Configuration validated: {RequiredKeys.Length} required keys, {_decryptedKeys.Count - RequiredKeys.Length} optional keys configured");
            
            return TradingResult.Success();
        }

        #endregion

        #region Helper Classes

        private class KeyData
        {
            public string ProtectedKey { get; set; } = string.Empty;
            public string IV { get; set; } = string.Empty;
        }

        #endregion

        /// <summary>
        /// Helper method to set health status
        /// </summary>
        private void SetHealthStatus(ServiceHealth status, string message)
        {
            _healthStatus = status;
            _healthMessage = message;
        }

        /// <summary>
        /// Gets the current health status
        /// </summary>
        public ServiceHealth GetHealthStatus() => _healthStatus;

        /// <summary>
        /// Gets the current health message
        /// </summary>
        public string GetHealthMessage() => _healthMessage;

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_lock)
                {
                    // Clear sensitive data from memory
                    foreach (var key in _decryptedKeys.Keys.ToList())
                    {
                        _decryptedKeys[key] = new string('0', _decryptedKeys[key].Length);
                    }
                    _decryptedKeys.Clear();
                }
            }
            
            // CanonicalServiceBase doesn't have Dispose, so we don't call base.Dispose
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        #region CanonicalServiceBase Implementation
        
        protected override async Task<TradingResult<bool>> OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            
            try
            {
                var initResult = await InitializeAsync();
                
                if (initResult.IsSuccess)
                {
                    LogInfo("EncryptedConfiguration initialized successfully");
                    return TradingResult<bool>.Success(true);
                }
                else
                {
                    LogError("Failed to initialize EncryptedConfiguration", null, initResult.Error?.Message);
                    return TradingResult<bool>.Failure(initResult.Error!);
                }
            }
            catch (Exception ex)
            {
                LogError("Exception during EncryptedConfiguration initialization", ex);
                return TradingResult<bool>.Failure("INIT_EXCEPTION", $"Initialization failed: {ex.Message}", ex);
            }
        }
        
        protected override async Task<TradingResult<bool>> OnStartAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            
            // Configuration doesn't need active startup, it's initialized and ready
            LogInfo("EncryptedConfiguration service started");
            return await Task.FromResult(TradingResult<bool>.Success(true));
        }
        
        protected override async Task<TradingResult<bool>> OnStopAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            
            // Clear sensitive data from memory on stop
            lock (_lock)
            {
                foreach (var key in _decryptedKeys.Keys.ToList())
                {
                    _decryptedKeys[key] = new string('0', _decryptedKeys[key].Length);
                }
                _decryptedKeys.Clear();
            }
            
            LogInfo("EncryptedConfiguration service stopped and sensitive data cleared");
            return await Task.FromResult(TradingResult<bool>.Success(true));
        }
        
        #endregion
    }
}