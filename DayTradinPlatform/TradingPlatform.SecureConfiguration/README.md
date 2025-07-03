# TradingPlatform.SecureConfiguration

A professional-grade secure configuration management library suitable for ANY application requiring encrypted storage of sensitive data - from personal password managers to enterprise financial systems.

## Features

- 🔐 **Bank-Grade Security**: AES-256 encryption with DPAPI protection
- 🚀 **Zero-Touch Setup**: Interactive wizard on first run, never ask again
- 🏢 **Enterprise Ready**: Thread-safe, comprehensive logging, full async
- 🔧 **Flexible**: Works for any use case - personal to enterprise
- 📦 **Standalone**: Can be packaged as NuGet for reuse
- 🧪 **Fully Tested**: Unit, integration, and E2E tests included

## Use Cases

### 1. Personal Password Manager
Store all your financial credentials securely:
```csharp
services.AddSecureConfiguration(builder => builder
    .ForApplication("PersonalVault", "My Financial Credentials")
    .WithCustomValue("BankOfAmerica_Username", "Bank of America - Username")
    .WithCustomValue("BankOfAmerica_Password", "Bank of America - Password")
    .WithCustomValue("Fidelity_Password", "Fidelity - Password")
    .WithCustomValue("Bitcoin_Wallet_Seed", "Bitcoin Wallet Seed (24 words)")
    // ... add all your accounts
);
```

### 2. Trading Platform (Consumer)
For applications that consume financial APIs:
```csharp
services.AddTradingPlatformConfiguration("DayTradingPlatform");
// Or custom:
services.AddSecureConfiguration(builder => builder
    .ForApplication("TradingBot", "Automated Trading System")
    .WithApiKey("AlphaVantage")
    .WithApiKey("Finnhub")
    .WithDatabaseConnection("TradingDb")
);
```

### 3. Financial Data Provider (Like Finnhub)
For companies providing financial data APIs:
```csharp
services.AddFinancialProviderConfiguration("YourCompany");
// Includes: databases, JWT secrets, encryption keys, webhooks, certificates
```

### 4. Investment Bank
For large financial institutions:
```csharp
services.AddBankingConfiguration("GoldmanSachs");
// Includes: HSM integration, multiple databases, regulatory APIs, audit logs
```

### 5. Cryptocurrency Exchange
For crypto trading platforms:
```csharp
services.AddCryptoExchangeConfiguration("Binance");
// Includes: wallet keys, blockchain nodes, KYC APIs, cold storage
```

## Quick Start

### 1. Install
```bash
dotnet add package TradingPlatform.SecureConfiguration
```

### 2. Configure
```csharp
// In Program.cs or Startup.cs
services.AddSecureConfiguration(builder => builder
    .ForApplication("MyApp", "My Application")
    .WithApiKey("ServiceA", required: true)
    .WithDatabaseConnection("MainDb")
    .WithCustomValue("SecretValue", "My Secret")
);
```

### 3. Use
```csharp
public class MyService
{
    private readonly ISecureConfiguration _config;
    
    public MyService(ISecureConfiguration config)
    {
        _config = config;
    }
    
    public async Task DoWork()
    {
        // First run will trigger setup wizard
        await _config.InitializeAsync();
        
        // Get values
        var apiKey = _config.GetValue("ServiceA");
        var dbConn = _config.GetValue("MainDb");
    }
}
```

## First-Run Experience

On first run, users see:
```
╔══════════════════════════════════════════════╗
║        SECURE CONFIGURATION SETUP             ║
║            My Application                     ║
╠══════════════════════════════════════════════╣
║         First-Time Configuration              ║
╚══════════════════════════════════════════════╝

This appears to be your first time running the application.
Let's set up your secure configuration.

🔒 Your values will be encrypted using AES-256
🔑 The encryption key is protected by Windows DPAPI
💾 You'll never need to enter these values again

Press any key to continue...

--- Required Configuration ---
These values must be provided to continue.

Service A API Key (required):
> ********

✓ Service A API Key configured
```

## Security Architecture

1. **Encryption**: AES-256-CBC with random key per save
2. **Key Protection**: Windows DPAPI (user-specific)
3. **Storage**: LocalApplicationData folder
4. **Memory**: Secure disposal, no string internment
5. **Access**: Thread-safe with locking

## Advanced Features

### Custom Validators
```csharp
.WithCustomValue("PIN", "Security PIN", 
    validator: pin => pin.Length == 6 && pin.All(char.IsDigit))
```

### Environment Variables (CI/CD)
```csharp
.WithApiKey("Service", environmentVariable: "SERVICE_API_KEY")
.WithMode(ConfigurationMode.Headless)
```

### Export/Import
```csharp
// Backup
await config.ExportAsync(@"C:\Backup\config.encrypted");

// Restore
await config.ImportAsync(@"C:\Backup\config.encrypted");
```

### Key Rotation
```csharp
.WithEncryption(options =>
{
    options.EnableKeyRotation = true;
    options.KeyRotationInterval = TimeSpan.FromDays(90);
})
```

## Platform Support

- ✅ Windows 10/11 (Full DPAPI support)
- 🚧 Linux (Coming soon - using SecretService)
- 🚧 macOS (Coming soon - using Keychain)

## License

MIT License - See LICENSE file for details

## Contributing

Contributions welcome! This is designed to be a community resource for secure configuration management.