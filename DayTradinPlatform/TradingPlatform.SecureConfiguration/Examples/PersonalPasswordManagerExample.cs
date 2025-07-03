using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingPlatform.SecureConfiguration.Builders;
using TradingPlatform.SecureConfiguration.Core;
using TradingPlatform.SecureConfiguration.Extensions;

namespace TradingPlatform.SecureConfiguration.Examples
{
    /// <summary>
    /// Example: Personal Password Manager for Financial Accounts
    /// Shows how to use SecureConfiguration as a personal vault for all your sensitive credentials
    /// </summary>
    public static class PersonalPasswordManagerExample
    {
        /// <summary>
        /// Personal financial credentials vault
        /// </summary>
        public static async Task PersonalFinancialVaultExample()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            
            services.AddSecureConfiguration(builder => builder
                .ForApplication("PersonalFinancialVault", "My Financial Credentials Vault")
                
                // === BANKING CREDENTIALS ===
                .WithCustomValue("BankOfAmerica_Username", 
                    "Bank of America - Username", 
                    required: true)
                .WithCustomValue("BankOfAmerica_Password", 
                    "Bank of America - Password", 
                    required: true,
                    validator: ValidateStrongPassword)
                .WithCustomValue("BankOfAmerica_SecurityQuestions", 
                    "Bank of America - Security Q&A (JSON)", 
                    required: false)
                
                .WithCustomValue("Chase_Username",
                    "Chase Bank - Username",
                    required: true)
                .WithCustomValue("Chase_Password",
                    "Chase Bank - Password",
                    required: true,
                    validator: ValidateStrongPassword)
                .WithCustomValue("Chase_2FA_BackupCodes",
                    "Chase - 2FA Backup Codes",
                    required: false)
                
                .WithCustomValue("WellsFargo_Username",
                    "Wells Fargo - Username",
                    required: false)
                .WithCustomValue("WellsFargo_Password",
                    "Wells Fargo - Password",
                    required: false,
                    validator: ValidateStrongPassword)
                
                // === BROKERAGE ACCOUNTS ===
                .WithCustomValue("Fidelity_Username",
                    "Fidelity - Username",
                    required: true)
                .WithCustomValue("Fidelity_Password",
                    "Fidelity - Password",
                    required: true,
                    validator: ValidateStrongPassword)
                .WithCustomValue("Fidelity_TradingPIN",
                    "Fidelity - Trading PIN",
                    required: false,
                    validator: pin => pin?.Length >= 4 && pin.All(char.IsDigit))
                
                .WithCustomValue("Schwab_Username",
                    "Charles Schwab - Username",
                    required: false)
                .WithCustomValue("Schwab_Password",
                    "Charles Schwab - Password",
                    required: false,
                    validator: ValidateStrongPassword)
                
                .WithCustomValue("IBKR_Username",
                    "Interactive Brokers - Username",
                    required: false)
                .WithCustomValue("IBKR_Password",
                    "Interactive Brokers - Password",
                    required: false,
                    validator: ValidateStrongPassword)
                .WithCustomValue("IBKR_SecureDevice",
                    "Interactive Brokers - Secure Device Code",
                    required: false)
                
                // === TRADING PLATFORMS ===
                .WithCustomValue("TDAmeritrade_Username",
                    "TD Ameritrade - Username",
                    required: false)
                .WithCustomValue("TDAmeritrade_Password",
                    "TD Ameritrade - Password",
                    required: false,
                    validator: ValidateStrongPassword)
                .WithApiKey("TDAmeritrade_API",
                    displayName: "TD Ameritrade - API Key",
                    required: false)
                
                .WithCustomValue("Robinhood_Username",
                    "Robinhood - Username",
                    required: false)
                .WithCustomValue("Robinhood_Password",
                    "Robinhood - Password",
                    required: false,
                    validator: ValidateStrongPassword)
                
                // === CRYPTOCURRENCY ===
                .WithCustomValue("Coinbase_Email",
                    "Coinbase - Email",
                    required: false)
                .WithCustomValue("Coinbase_Password",
                    "Coinbase - Password",
                    required: false,
                    validator: ValidateStrongPassword)
                .WithCustomValue("Coinbase_2FA_Secret",
                    "Coinbase - 2FA Secret",
                    required: false)
                
                .WithCustomValue("Binance_Email",
                    "Binance - Email",
                    required: false)
                .WithCustomValue("Binance_Password",
                    "Binance - Password",
                    required: false,
                    validator: ValidateStrongPassword)
                .WithApiKey("Binance_API",
                    displayName: "Binance - API Key",
                    required: false)
                .WithCustomValue("Binance_API_Secret",
                    "Binance - API Secret",
                    required: false)
                
                // === CRYPTO WALLETS ===
                .WithCustomValue("Bitcoin_Wallet_Seed",
                    "Bitcoin Wallet - Seed Phrase (12/24 words)",
                    required: false,
                    validator: ValidateSeedPhrase)
                .WithCustomValue("Ethereum_Wallet_PrivateKey",
                    "Ethereum Wallet - Private Key",
                    required: false,
                    validator: key => key?.Length == 64) // 32 bytes hex
                .WithCustomValue("Hardware_Wallet_PIN",
                    "Hardware Wallet (Ledger/Trezor) - PIN",
                    required: false,
                    validator: pin => pin?.Length >= 4 && pin?.Length <= 8)
                
                // === FINANCIAL DATA SERVICES ===
                .WithApiKey("AlphaVantage",
                    displayName: "AlphaVantage - API Key",
                    required: false)
                .WithApiKey("Finnhub",
                    displayName: "Finnhub - API Key",
                    required: false)
                .WithApiKey("IEXCloud",
                    displayName: "IEX Cloud - API Key",
                    required: false)
                
                // === TAX & ACCOUNTING ===
                .WithCustomValue("TurboTax_Username",
                    "TurboTax - Username",
                    required: false)
                .WithCustomValue("TurboTax_Password",
                    "TurboTax - Password",
                    required: false,
                    validator: ValidateStrongPassword)
                
                .WithCustomValue("IRS_Username",
                    "IRS.gov - Username",
                    required: false)
                .WithCustomValue("IRS_Password",
                    "IRS.gov - Password",
                    required: false,
                    validator: ValidateStrongPassword)
                
                // === CREDIT CARDS ===
                .WithCustomValue("Amex_Username",
                    "American Express - Username",
                    required: false)
                .WithCustomValue("Amex_Password",
                    "American Express - Password",
                    required: false,
                    validator: ValidateStrongPassword)
                
                // === INSURANCE ===
                .WithCustomValue("HealthInsurance_Portal",
                    "Health Insurance - Portal Credentials",
                    required: false)
                .WithCustomValue("AutoInsurance_Login",
                    "Auto Insurance - Login Info",
                    required: false)
                
                // === RETIREMENT ACCOUNTS ===
                .WithCustomValue("401k_Provider_Login",
                    "401(k) Provider - Login",
                    required: false)
                .WithCustomValue("401k_Provider_Password",
                    "401(k) Provider - Password",
                    required: false,
                    validator: ValidateStrongPassword)
                
                // === SECURE NOTES ===
                .WithCustomValue("BankAccountNumbers",
                    "Bank Account Numbers (Encrypted Note)",
                    required: false)
                .WithCustomValue("SafeDepositBox_Info",
                    "Safe Deposit Box Information",
                    required: false)
                .WithCustomValue("EmergencyContacts",
                    "Financial Emergency Contacts",
                    required: false)
                
                // Set strong security
                .WithValidation(rules =>
                {
                    rules.MinimumKeyLength = 8;
                    rules.RequireComplexPasswords = true;
                })
                .WithEncryption(options =>
                {
                    options.EnableKeyRotation = false; // Manual control
                }));

            var provider = services.BuildServiceProvider();
            var vault = provider.GetRequiredService<ISecureConfiguration>();
            
            // Initialize - will run setup wizard on first use
            var result = await vault.InitializeAsync();
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Failed to initialize vault: {result.ErrorMessage}");
                return;
            }
            
            // Example: Retrieve credentials when needed
            Console.WriteLine("\n=== Retrieving Credentials ===");
            
            // Get bank credentials
            if (vault.TryGetValue("BankOfAmerica_Username", out var boaUsername))
            {
                Console.WriteLine($"Bank of America Username: {MaskValue(boaUsername)}");
            }
            
            // Get API keys
            if (vault.TryGetValue("AlphaVantage", out var alphaVantageKey))
            {
                Console.WriteLine($"AlphaVantage API Key: {MaskValue(alphaVantageKey)}");
            }
            
            // Get crypto wallet (BE VERY CAREFUL!)
            if (vault.TryGetValue("Bitcoin_Wallet_Seed", out var btcSeed))
            {
                Console.WriteLine($"Bitcoin Seed: {MaskValue(btcSeed, showChars: 3)}");
            }
            
            // Export backup (encrypted)
            Console.WriteLine("\n=== Creating Encrypted Backup ===");
            var backupPath = @"C:\SecureBackup\financial_vault_backup.encrypted";
            var exportResult = await vault.ExportAsync(backupPath);
            if (exportResult.IsSuccess)
            {
                Console.WriteLine($"✓ Backup created at: {backupPath}");
                Console.WriteLine("  Store this backup in a secure location!");
            }
        }

        /// <summary>
        /// Example: Quick personal setup for just trading credentials
        /// </summary>
        public static async Task QuickTradingCredentialsExample()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            
            services.AddSecureConfiguration(builder => builder
                .ForApplication("MyTradingCredentials", "Personal Trading Credentials")
                
                // Just the essentials for trading
                .WithCustomValue("PrimaryBroker_Username", "Primary Broker - Username", required: true)
                .WithCustomValue("PrimaryBroker_Password", "Primary Broker - Password", required: true)
                .WithApiKey("TradingAPI_Key", displayName: "Trading API Key", required: true)
                .WithCustomValue("TradingAPI_Secret", "Trading API Secret", required: true)
                .WithCustomValue("TradingAccount_Number", "Trading Account Number", required: true)
                .WithCustomValue("TradingPIN", "Trading PIN/2FA", required: false));

            var provider = services.BuildServiceProvider();
            var config = provider.GetRequiredService<ISecureConfiguration>();
            await config.InitializeAsync();
        }

        /// <summary>
        /// Helper to validate strong passwords
        /// </summary>
        private static bool ValidateStrongPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return false;
            
            // Just basic length check - user knows their bank's requirements
            return true;
        }

        /// <summary>
        /// Helper to validate seed phrases
        /// </summary>
        private static bool ValidateSeedPhrase(string seed)
        {
            if (string.IsNullOrWhiteSpace(seed))
                return false;
            
            var words = seed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length == 12 || words.Length == 24;
        }

        /// <summary>
        /// Helper to mask sensitive values for display
        /// </summary>
        private static string MaskValue(string value, int showChars = 4)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= showChars)
                return "****";
            
            return value.Substring(0, showChars) + new string('*', Math.Min(8, value.Length - showChars));
        }
    }

    /// <summary>
    /// Console application to manage personal financial vault
    /// </summary>
    public class PersonalVaultConsoleApp
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║   Personal Financial Credentials Vault  ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            
            // Configure your personal vault
            services.AddSecureConfiguration(builder => builder
                .ForApplication("PersonalVault", "My Financial Credentials")
                // Add all your accounts here...
                );
            
            var provider = services.BuildServiceProvider();
            var vault = provider.GetRequiredService<ISecureConfiguration>();
            
            // Initialize (first time will run setup)
            await vault.InitializeAsync();
            
            // Simple menu
            while (true)
            {
                Console.WriteLine("\n1. View credential (masked)");
                Console.WriteLine("2. Update credential");
                Console.WriteLine("3. Add new credential");
                Console.WriteLine("4. Export backup");
                Console.WriteLine("5. List all keys");
                Console.WriteLine("6. Exit");
                Console.Write("\nChoice: ");
                
                var choice = Console.ReadLine();
                
                switch (choice)
                {
                    case "1":
                        await ViewCredential(vault);
                        break;
                    case "2":
                        await UpdateCredential(vault);
                        break;
                    case "3":
                        await AddCredential(vault);
                        break;
                    case "4":
                        await ExportBackup(vault);
                        break;
                    case "5":
                        ListAllKeys(vault);
                        break;
                    case "6":
                        return;
                }
            }
        }

        private static async Task ViewCredential(ISecureConfiguration vault)
        {
            Console.Write("Enter key name: ");
            var key = Console.ReadLine();
            
            if (vault.TryGetValue(key, out var value))
            {
                Console.WriteLine($"Value: {MaskValue(value)}");
                Console.WriteLine("(Press C to copy to clipboard, any other key to continue)");
                
                if (Console.ReadKey(true).Key == ConsoleKey.C)
                {
                    // In real app, copy to clipboard
                    Console.WriteLine("Copied to clipboard!");
                }
            }
            else
            {
                Console.WriteLine("Key not found.");
            }
        }

        private static async Task UpdateCredential(ISecureConfiguration vault)
        {
            Console.Write("Enter key name: ");
            var key = Console.ReadLine();
            
            Console.Write("Enter new value: ");
            var value = ReadPassword();
            
            var result = await vault.SetValueAsync(key, value);
            Console.WriteLine(result.IsSuccess ? "✓ Updated" : $"✗ Failed: {result.ErrorMessage}");
        }

        private static async Task AddCredential(ISecureConfiguration vault)
        {
            Console.Write("Enter new key name: ");
            var key = Console.ReadLine();
            
            Console.Write("Enter value: ");
            var value = ReadPassword();
            
            var result = await vault.SetValueAsync(key, value);
            Console.WriteLine(result.IsSuccess ? "✓ Added" : $"✗ Failed: {result.ErrorMessage}");
        }

        private static async Task ExportBackup(ISecureConfiguration vault)
        {
            var backupPath = $@"C:\Backups\vault_backup_{DateTime.Now:yyyyMMdd_HHmmss}.encrypted";
            var result = await vault.ExportAsync(backupPath);
            
            if (result.IsSuccess)
            {
                Console.WriteLine($"✓ Backup exported to: {backupPath}");
            }
            else
            {
                Console.WriteLine($"✗ Export failed: {result.ErrorMessage}");
            }
        }

        private static void ListAllKeys(ISecureConfiguration vault)
        {
            var keys = vault.GetConfiguredKeys();
            Console.WriteLine($"\nConfigured credentials ({keys.Count}):");
            
            foreach (var key in keys.OrderBy(k => k))
            {
                Console.WriteLine($"  • {key}");
            }
        }

        private static string MaskValue(string value, int showChars = 4)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= showChars)
                return "****";
            
            return value.Substring(0, showChars) + new string('*', Math.Min(8, value.Length - showChars));
        }

        private static string ReadPassword()
        {
            var password = "";
            ConsoleKeyInfo key;
            
            do
            {
                key = Console.ReadKey(intercept: true);
                
                if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
            }
            while (key.Key != ConsoleKey.Enter);
            
            Console.WriteLine();
            return password;
        }
    }
}