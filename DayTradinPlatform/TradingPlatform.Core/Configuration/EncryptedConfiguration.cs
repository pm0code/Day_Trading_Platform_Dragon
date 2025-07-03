using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Canonical;

namespace TradingPlatform.Core.Configuration
{
    /// <summary>
    /// Secure configuration management with automatic encryption
    /// Uses AES-256 with DPAPI-protected key on Windows
    /// </summary>
    public class EncryptedConfiguration : CanonicalBase
    {
        private readonly string _configPath;
        private readonly string _keyPath;
        private Dictionary<string, string> _decryptedKeys = new();
        private readonly object _lock = new();
        
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

        public EncryptedConfiguration(ILogger<EncryptedConfiguration> logger) : base(logger)
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
        public override async Task<TradingResult> InitializeAsync()
        {
            using var operation = BeginOperation(OperationContext("Initializing encrypted configuration"));
            
            try
            {
                // Check if this is first run
                if (!File.Exists(_configPath) || !File.Exists(_keyPath))
                {
                    operation.Log("First run detected - starting configuration wizard");
                    var setupResult = await RunFirstTimeSetup();
                    
                    if (!setupResult.IsSuccess)
                    {
                        return operation.Failed(setupResult.ErrorMessage!);
                    }
                }
                
                // Load and decrypt existing configuration
                var loadResult = await LoadConfiguration();
                
                if (!loadResult.IsSuccess)
                {
                    return operation.Failed(loadResult.ErrorMessage!);
                }
                
                // Validate all required keys are present
                var validationResult = ValidateConfiguration();
                
                if (!validationResult.IsSuccess)
                {
                    return operation.Failed(validationResult.ErrorMessage!);
                }
                
                SetHealthStatus(HealthStatus.Healthy, "Configuration loaded and validated");
                return operation.Succeeded("Encrypted configuration initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize encrypted configuration");
                return operation.Failed($"Configuration initialization failed: {ex.Message}");
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
                    _logger.LogDebug("Retrieved API key for {KeyName}", keyName);
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
            using var operation = BeginOperation(OperationContext("Running first-time configuration setup"));
            
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
                        return operation.Failed($"Required API key '{keyName}' was not provided");
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
                    return operation.Failed(saveResult.ErrorMessage!);
                }
                
                Console.WriteLine("\n✅ Configuration saved successfully!");
                Console.WriteLine("Your API keys have been encrypted and stored.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey(true);
                Console.Clear();
                
                return operation.Succeeded("First-time setup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "First-time setup failed");
                return operation.Failed($"Setup failed: {ex.Message}");
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
            using var operation = BeginOperation(OperationContext("Saving encrypted configuration"));
            
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
                operation.Log("Encryption key saved (DPAPI protected)");
                
                // Encrypt the configuration
                var jsonData = JsonSerializer.Serialize(apiKeys);
                var encrypted = EncryptStringToBytes(jsonData, aes.Key, aes.IV);
                await File.WriteAllBytesAsync(_configPath, encrypted);
                
                operation.Log($"Configuration encrypted and saved ({encrypted.Length} bytes)");
                
                // Load into memory
                lock (_lock)
                {
                    _decryptedKeys = new Dictionary<string, string>(apiKeys);
                }
                
                return operation.Succeeded("Configuration saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save configuration");
                return operation.Failed($"Save failed: {ex.Message}");
            }
        }

        private async Task<TradingResult> LoadConfiguration()
        {
            using var operation = BeginOperation(OperationContext("Loading encrypted configuration"));
            
            try
            {
                // Load the protected key
                var keyJson = await File.ReadAllTextAsync(_keyPath);
                var keyData = JsonSerializer.Deserialize<KeyData>(keyJson);
                
                if (keyData == null)
                {
                    return operation.Failed("Invalid key file format");
                }
                
                // Unprotect the AES key using DPAPI
                var protectedKey = Convert.FromBase64String(keyData.ProtectedKey);
                var iv = Convert.FromBase64String(keyData.IV);
                
                var aesKey = ProtectedData.Unprotect(
                    protectedKey,
                    iv,
                    DataProtectionScope.CurrentUser
                );
                
                operation.Log("Encryption key loaded and unprotected");
                
                // Load and decrypt configuration
                var encrypted = await File.ReadAllBytesAsync(_configPath);
                var decrypted = DecryptStringFromBytes(encrypted, aesKey, iv);
                
                var apiKeys = JsonSerializer.Deserialize<Dictionary<string, string>>(decrypted);
                
                if (apiKeys == null)
                {
                    return operation.Failed("Invalid configuration format");
                }
                
                lock (_lock)
                {
                    _decryptedKeys = apiKeys;
                }
                
                operation.Log($"Configuration loaded successfully ({apiKeys.Count} keys)");
                return operation.Succeeded();
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Failed to decrypt configuration - possible corruption or different user account");
                return operation.Failed("Failed to decrypt configuration. The file may be corrupted or was encrypted by a different user.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration");
                return operation.Failed($"Load failed: {ex.Message}");
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
                    $"Missing required API keys: {string.Join(", ", missingKeys)}");
            }
            
            _logger.LogInformation(
                "Configuration validated: {RequiredCount} required keys, {OptionalCount} optional keys configured",
                RequiredKeys.Length,
                _decryptedKeys.Count - RequiredKeys.Length);
            
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

        protected override void Dispose(bool disposing)
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
            
            base.Dispose(disposing);
        }
    }
}