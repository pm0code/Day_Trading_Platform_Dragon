using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingPlatform.SecureConfiguration.Builders;
using TradingPlatform.SecureConfiguration.Core;
using TradingPlatform.SecureConfiguration.Extensions;

namespace TradingPlatform.SecureConfiguration.Examples
{
    /// <summary>
    /// Examples of how to use SecureConfiguration for various scenarios
    /// </summary>
    public static class UsageExamples
    {
        /// <summary>
        /// Example 1: Trading Platform Consumer (like your Day Trading Platform)
        /// </summary>
        public static async Task TradingPlatformExample()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            
            // Simple approach
            services.AddTradingPlatformConfiguration("DayTradingPlatform");
            
            var provider = services.BuildServiceProvider();
            var config = provider.GetRequiredService<ISecureConfiguration>();
            
            await config.InitializeAsync();
            
            // Use the configuration
            var alphaVantageKey = config.GetValue("AlphaVantage");
            var finnhubKey = config.GetValue("Finnhub");
        }

        /// <summary>
        /// Example 2: Financial Data Provider (like Finnhub itself)
        /// </summary>
        public static async Task FinancialProviderExample()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            
            services.AddSecureConfiguration(builder => builder
                .ForApplication("FinnhubAPI", "Finnhub Financial Data Service")
                
                // Database connections
                .WithDatabaseConnection("Primary", 
                    environmentVariable: "PRIMARY_DB_URL")
                .WithDatabaseConnection("ReadReplica", 
                    required: false, 
                    environmentVariable: "REPLICA_DB_URL")
                .WithDatabaseConnection("Analytics",
                    required: false,
                    environmentVariable: "ANALYTICS_DB_URL")
                
                // Internal service keys
                .WithApiKey("DataProviderKey", 
                    displayName: "Upstream Data Provider API Key")
                .WithApiKey("InternalServiceBus",
                    displayName: "Internal Message Bus Key")
                
                // OAuth/JWT for customer authentication
                .WithOAuthSecret("CustomerAuthJWT")
                .WithOAuthSecret("AdminPortalJWT")
                
                // Encryption for sensitive data
                .WithEncryptionKey("CustomerDataEncryption", 
                    EncryptionKeyType.AES256)
                
                // Webhook secrets for customer callbacks
                .WithWebhookSecret("CustomerWebhook")
                .WithWebhookSecret("EnterpriseWebhook")
                
                // SSL certificates
                .WithCertificate("ApiSSL", format: CertificateFormat.PEM)
                .WithCertificate("InternalSSL", format: CertificateFormat.PKCS12)
                
                // Custom configuration
                .WithCustomValue("RateLimitingRules", 
                    "Rate Limiting Configuration JSON", 
                    required: false)
                .WithCustomValue("IPWhitelist",
                    "Enterprise Customer IP Whitelist",
                    required: false));

            var provider = services.BuildServiceProvider();
            provider.InitializeSecureConfiguration();
        }

        /// <summary>
        /// Example 3: Investment Bank
        /// </summary>
        public static async Task InvestmentBankExample()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            
            services.AddSecureConfiguration(builder => builder
                .ForApplication("GoldmanSachsSystems", "Goldman Sachs Trading Infrastructure")
                
                // Multiple database connections
                .WithDatabaseConnection("TradingCore")
                .WithDatabaseConnection("RiskManagement")
                .WithDatabaseConnection("Compliance")
                .WithDatabaseConnection("ClientAccounts")
                .WithDatabaseConnection("AuditLog")
                
                // External market data providers
                .WithApiKey("Bloomberg", displayName: "Bloomberg Terminal API")
                .WithApiKey("Refinitiv", displayName: "Refinitiv Eikon API")
                .WithApiKey("ICE", displayName: "ICE Data Services")
                
                // Regulatory reporting
                .WithApiKey("SEC_EDGAR", displayName: "SEC EDGAR Filing API")
                .WithApiKey("FINRA_TRACE", displayName: "FINRA TRACE Reporting")
                
                // HSM integration
                .WithCertificate("TradingHSM", format: CertificateFormat.PKCS12)
                .WithCustomValue("HSM_PIN", "Hardware Security Module PIN", 
                    validator: pin => pin.Length == 8 && pin.All(char.IsDigit))
                
                // Encryption keys
                .WithEncryptionKey("ClientData", EncryptionKeyType.AES256)
                .WithEncryptionKey("TransactionSigning", EncryptionKeyType.RSA4096)
                .WithEncryptionKey("AuditLogEncryption", EncryptionKeyType.AES256)
                
                // Internal systems
                .WithOAuthSecret("EmployeePortal")
                .WithOAuthSecret("ClientPortal")
                .WithOAuthSecret("ComplianceSystem")
                
                // Set validation rules
                .WithValidation(rules =>
                {
                    rules.MinimumKeyLength = 32;
                    rules.RequireComplexPasswords = true;
                    rules.ValidateCertificateExpiry = true;
                    rules.CertificateExpiryWarning = TimeSpan.FromDays(60);
                })
                
                // Set encryption options
                .WithEncryption(options =>
                {
                    options.UseHardwareSecurityModule = true;
                    options.HsmProvider = "Thales";
                    options.EnableKeyRotation = true;
                    options.KeyRotationInterval = TimeSpan.FromDays(30);
                }));

            var provider = services.BuildServiceProvider();
            provider.InitializeSecureConfiguration();
        }

        /// <summary>
        /// Example 4: Cryptocurrency Exchange
        /// </summary>
        public static async Task CryptoExchangeExample()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            
            services.AddCryptoExchangeConfiguration("Binance");
            
            // Or with custom configuration
            services.AddSecureConfiguration(builder => builder
                .ForApplication("BinanceExchange", "Binance Cryptocurrency Exchange")
                
                // Blockchain keys - EXTREMELY SENSITIVE
                .WithEncryptionKey("BitcoinColdWallet", 
                    EncryptionKeyType.Secp256k1)
                .WithEncryptionKey("EthereumColdWallet", 
                    EncryptionKeyType.Secp256k1)
                .WithEncryptionKey("BitcoinHotWallet", 
                    EncryptionKeyType.Secp256k1)
                .WithEncryptionKey("EthereumHotWallet", 
                    EncryptionKeyType.Secp256k1)
                
                // Master seed for HD wallets
                .WithCustomValue("HDWalletMasterSeed",
                    "BIP39 Master Seed Phrase (24 words)",
                    validator: seed => seed.Split(' ').Length == 24)
                
                // Node connections
                .WithApiKey("InfuraAPI", displayName: "Infura Ethereum Node API")
                .WithApiKey("BitcoinRPC", displayName: "Bitcoin Core RPC Credentials")
                
                // Trading engine databases
                .WithDatabaseConnection("OrderBook")
                .WithDatabaseConnection("UserBalances")
                .WithDatabaseConnection("TradeHistory")
                .WithDatabaseConnection("Blockchain_Bitcoin")
                .WithDatabaseConnection("Blockchain_Ethereum")
                
                // Customer authentication
                .WithOAuthSecret("TradingAPI_HMAC")
                .WithOAuthSecret("WebsocketJWT")
                .WithCustomValue("2FA_Secret", "TOTP Secret Key")
                
                // Webhooks for deposits/withdrawals
                .WithWebhookSecret("DepositConfirmation")
                .WithWebhookSecret("WithdrawalApproval")
                
                // Compliance and KYC
                .WithApiKey("Chainalysis", displayName: "Chainalysis KYT API")
                .WithApiKey("Jumio", displayName: "Jumio KYC Verification")
                
                // High security mode
                .WithValidation(rules =>
                {
                    rules.MinimumKeyLength = 64; // Very long keys
                    rules.RequireComplexPasswords = true;
                })
                .WithEncryption(options =>
                {
                    options.UseHardwareSecurityModule = true;
                    options.HsmProvider = "Gemalto";
                    options.EnableKeyRotation = true;
                    options.KeyRotationInterval = TimeSpan.FromDays(7); // Weekly rotation
                }));

            var provider = services.BuildServiceProvider();
            provider.InitializeSecureConfiguration();
        }

        /// <summary>
        /// Example 5: Central Bank Digital Currency (CBDC) System
        /// </summary>
        public static async Task CentralBankExample()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            
            services.AddSecureConfiguration(builder => builder
                .ForApplication("FederalReserveCBDC", "Federal Reserve Digital Dollar System")
                
                // Master keys for currency issuance
                .WithEncryptionKey("CurrencyIssuanceKey", 
                    EncryptionKeyType.RSA4096)
                .WithEncryptionKey("MonetaryPolicyKey", 
                    EncryptionKeyType.RSA4096)
                
                // Inter-bank settlement
                .WithCertificate("FedwireAccess", format: CertificateFormat.PKCS12)
                .WithApiKey("SWIFT_GPI", displayName: "SWIFT Global Payments")
                
                // Commercial bank connections
                .WithDatabaseConnection("BankRegistry")
                .WithDatabaseConnection("SettlementLedger")
                .WithDatabaseConnection("MonetaryPolicy")
                
                // Audit and compliance
                .WithEncryptionKey("AuditTrailSigning", 
                    EncryptionKeyType.Ed25519)
                .WithDatabaseConnection("ImmutableAuditLog")
                
                // Extreme security
                .WithValidation(rules =>
                {
                    rules.MinimumKeyLength = 128;
                    rules.ValidateCertificateExpiry = true;
                    rules.CertificateExpiryWarning = TimeSpan.FromDays(180);
                })
                .WithEncryption(options =>
                {
                    options.UseHardwareSecurityModule = true;
                    options.HsmProvider = "IBM_Z_CryptoExpress";
                    options.EnableKeyRotation = false; // Manual rotation only
                }));

            var provider = services.BuildServiceProvider();
            provider.InitializeSecureConfiguration();
        }

        /// <summary>
        /// Example 6: Headless/CI/CD Configuration
        /// </summary>
        public static async Task HeadlessCICDExample()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            
            services.AddSecureConfiguration(builder => builder
                .ForApplication("TradingPlatform", "Automated Deployment")
                .WithApiKey("AlphaVantage", environmentVariable: "ALPHAVANTAGE_KEY")
                .WithApiKey("Finnhub", environmentVariable: "FINNHUB_KEY")
                .WithDatabaseConnection("MainDb", environmentVariable: "DATABASE_URL")
                .WithMode(ConfigurationMode.Headless)
                .WithSecretsFile("/secure/vault/secrets.json"));

            var provider = services.BuildServiceProvider();
            
            // In CI/CD, this runs without user interaction
            provider.InitializeSecureConfiguration();
        }

        /// <summary>
        /// Example 7: Using configuration in ASP.NET Core
        /// </summary>
        public class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                // Add secure configuration for a financial data API service
                services.AddSecureConfiguration(builder => builder
                    .ForFinancialDataProvider("MarketDataAPI")
                    .WithLogger(services.BuildServiceProvider()
                        .GetRequiredService<ILogger<ISecureConfiguration>>()));
                
                // Add other services
                services.AddControllers();
            }

            public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
            {
                // Initialize secure configuration on startup
                app.ApplicationServices.InitializeSecureConfiguration();
                
                // Rest of configuration...
            }
        }
    }
}