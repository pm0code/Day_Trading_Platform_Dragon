using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradingPlatform.SecureConfiguration.Core;

namespace TradingPlatform.SecureConfiguration.Implementations
{
    /// <summary>
    /// Interactive console-based secure configuration for first-time setup
    /// </summary>
    public class InteractiveSecureConfiguration : SecureConfigurationBase
    {
        private readonly string[] _requiredKeys;
        private readonly string[] _optionalKeys;
        private readonly string _applicationDisplayName;

        public InteractiveSecureConfiguration(
            ILogger<InteractiveSecureConfiguration> logger,
            string applicationName,
            string applicationDisplayName,
            string[] requiredKeys,
            string[]? optionalKeys = null) 
            : base(logger, applicationName)
        {
            _applicationDisplayName = applicationDisplayName ?? applicationName;
            _requiredKeys = requiredKeys ?? Array.Empty<string>();
            _optionalKeys = optionalKeys ?? Array.Empty<string>();
        }

        protected override async Task<SecureConfigResult> RunFirstTimeSetupAsync()
        {
            try
            {
                Console.Clear();
                DisplayWelcomeScreen();

                var collectedValues = new Dictionary<string, string>();

                // Collect required keys
                if (_requiredKeys.Length > 0)
                {
                    Console.WriteLine("\n--- Required Configuration ---");
                    Console.WriteLine("These values must be provided to continue.\n");

                    foreach (var key in _requiredKeys)
                    {
                        var value = CollectSecureValue(key, required: true);
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            return SecureConfigResult.Failure(
                                $"Required value '{key}' was not provided", 
                                "MISSING_REQUIRED");
                        }
                        collectedValues[key] = value;
                    }
                }

                // Collect optional keys
                if (_optionalKeys.Length > 0)
                {
                    Console.WriteLine("\n--- Optional Configuration ---");
                    Console.WriteLine("Press Enter to skip any optional values.\n");

                    foreach (var key in _optionalKeys)
                    {
                        var value = CollectSecureValue(key, required: false);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            collectedValues[key] = value;
                        }
                    }
                }

                // Save configuration
                Console.WriteLine("\nEncrypting and saving configuration...");
                var saveResult = await SaveConfigurationAsync(collectedValues);

                if (!saveResult.IsSuccess)
                {
                    Console.WriteLine($"\n❌ Failed to save configuration: {saveResult.ErrorMessage}");
                    Console.WriteLine("\nPress any key to exit...");
                    Console.ReadKey(true);
                    return saveResult;
                }

                // Success
                DisplaySuccessScreen(collectedValues.Count);
                
                lock (_lock)
                {
                    _decryptedValues = collectedValues;
                }

                return SecureConfigResult.Success(new Dictionary<string, object>
                {
                    ["ConfiguredKeys"] = collectedValues.Count,
                    ["RequiredKeys"] = _requiredKeys.Length,
                    ["OptionalKeys"] = _optionalKeys.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "First-time setup failed");
                return SecureConfigResult.Failure($"Setup failed: {ex.Message}", "SETUP_ERROR");
            }
        }

        protected override Task<SecureConfigResult> ValidateConfigurationAsync()
        {
            var missingKeys = new List<string>();

            foreach (var requiredKey in _requiredKeys)
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

            _logger.LogInformation(
                "Configuration validated: {RequiredCount} required, {OptionalCount} optional values configured",
                _requiredKeys.Length,
                _decryptedValues.Count - _requiredKeys.Length);

            return Task.FromResult(SecureConfigResult.Success());
        }

        #region Console UI Methods

        private void DisplayWelcomeScreen()
        {
            var width = 60;
            Console.WriteLine("╔" + new string('═', width - 2) + "╗");
            Console.WriteLine("║" + CenterText("SECURE CONFIGURATION SETUP", width - 2) + "║");
            Console.WriteLine("║" + CenterText(_applicationDisplayName, width - 2) + "║");
            Console.WriteLine("╠" + new string('═', width - 2) + "╣");
            Console.WriteLine("║" + CenterText("First-Time Configuration", width - 2) + "║");
            Console.WriteLine("╚" + new string('═', width - 2) + "╝");
            
            Console.WriteLine("\nThis appears to be your first time running the application.");
            Console.WriteLine("Let's set up your secure configuration.");
            Console.WriteLine();
            Console.WriteLine("🔒 Your values will be encrypted using AES-256");
            Console.WriteLine("🔑 The encryption key is protected by Windows DPAPI");
            Console.WriteLine("💾 You'll never need to enter these values again");
            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        private void DisplaySuccessScreen(int configuredCount)
        {
            Console.Clear();
            var width = 60;
            Console.WriteLine("╔" + new string('═', width - 2) + "╗");
            Console.WriteLine("║" + CenterText("✅ CONFIGURATION COMPLETE", width - 2) + "║");
            Console.WriteLine("╚" + new string('═', width - 2) + "╝");
            
            Console.WriteLine($"\n✓ {configuredCount} values encrypted and saved");
            Console.WriteLine("✓ Configuration stored in secure location");
            Console.WriteLine("✓ Only accessible by your Windows account");
            Console.WriteLine();
            Console.WriteLine("The application will now continue with normal startup.");
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
            Console.Clear();
        }

        private string CollectSecureValue(string key, bool required)
        {
            var displayName = FormatKeyName(key);
            var attempts = 0;
            const int maxAttempts = 3;

            while (attempts < maxAttempts)
            {
                Console.WriteLine($"\n{displayName}{(required ? " (required)" : " (optional)")}:");
                
                if (!required)
                {
                    Console.WriteLine("Press Enter to skip");
                }
                
                Console.Write("> ");

                var value = ReadSecureInput();

                if (string.IsNullOrWhiteSpace(value))
                {
                    if (!required)
                    {
                        return string.Empty;
                    }

                    attempts++;
                    Console.WriteLine($"⚠️  This value is required. ({maxAttempts - attempts} attempts remaining)");
                    continue;
                }

                // Basic validation
                if (value.Length < 8 && key.ToLower().Contains("key"))
                {
                    Console.WriteLine("⚠️  Value seems too short. Please check and re-enter.");
                    Console.Write("Continue anyway? (y/N): ");
                    var confirm = Console.ReadLine();
                    if (confirm?.ToLower() != "y")
                    {
                        attempts++;
                        continue;
                    }
                }

                Console.WriteLine($"✓ {displayName} configured");
                return value.Trim();
            }

            throw new InvalidOperationException($"Failed to collect {displayName} after {maxAttempts} attempts");
        }

        private string ReadSecureInput()
        {
            var input = new StringBuilder();
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Backspace && input.Length > 0)
                {
                    input.Remove(input.Length - 1, 1);
                    Console.Write("\b \b");
                }
                else if (key.Key != ConsoleKey.Enter && 
                         key.Key != ConsoleKey.Backspace &&
                         !char.IsControl(key.KeyChar))
                {
                    input.Append(key.KeyChar);
                    Console.Write("*");
                }
            }
            while (key.Key != ConsoleKey.Enter);

            Console.WriteLine();
            return input.ToString();
        }

        private string FormatKeyName(string key)
        {
            // Convert from various formats to readable display
            // Examples: "ApiKey" -> "API Key", "alpha_vantage_key" -> "Alpha Vantage Key"
            
            var formatted = key;
            
            // Handle snake_case
            if (key.Contains('_'))
            {
                formatted = string.Join(" ", key.Split('_').Select(word => 
                    char.ToUpper(word[0]) + word.Substring(1).ToLower()));
            }
            // Handle PascalCase or camelCase
            else if (key.Any(char.IsLower) && key.Any(char.IsUpper))
            {
                var sb = new StringBuilder();
                for (int i = 0; i < key.Length; i++)
                {
                    if (i > 0 && char.IsUpper(key[i]) && 
                        (i == key.Length - 1 || !char.IsUpper(key[i + 1])))
                    {
                        sb.Append(' ');
                    }
                    sb.Append(key[i]);
                }
                formatted = sb.ToString();
            }

            // Handle common abbreviations
            formatted = formatted.Replace("Api", "API")
                                .Replace("Url", "URL")
                                .Replace("Id", "ID")
                                .Replace("Db", "Database");

            return formatted;
        }

        private string CenterText(string text, int width)
        {
            if (text.Length >= width) return text.Substring(0, width);
            var padding = (width - text.Length) / 2;
            return new string(' ', padding) + text + new string(' ', width - text.Length - padding);
        }

        #endregion
    }
}