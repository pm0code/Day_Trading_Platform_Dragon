# Configuration Setup for Day Trading Platform

## Quick Setup (Solo Use)

1. **Copy the template file:**
   ```bash
   cp appsettings.json.template appsettings.json
   ```

2. **Edit appsettings.json and add your API keys:**
   - Replace `YOUR_ALPHAVANTAGE_API_KEY_HERE` with your actual AlphaVantage key
   - Replace `YOUR_FINNHUB_API_KEY_HERE` with your actual Finnhub key
   - IexCloud is optional - leave blank if not using

3. **Alternative: Use environment variables:**
   ```bash
   export ALPHAVANTAGE_API_KEY="your-key-here"
   export FINNHUB_API_KEY="your-key-here"
   export IEXCLOUD_API_KEY="your-key-here"  # Optional
   ```

## Security Notes

- The `appsettings.json` file is already in `.gitignore` to prevent accidental commits
- Never commit your actual API keys to the repository
- For solo use, this simple approach is sufficient
- The ApiConfiguration class will read from either source

## Getting API Keys

- **AlphaVantage**: https://www.alphavantage.co/support/#api-key (Free tier available)
- **Finnhub**: https://finnhub.io/register (Free tier available)
- **IEX Cloud**: https://iexcloud.io/console/tokens (Optional, paid service)

## Usage in Code

The `ApiConfiguration` class automatically handles reading from both sources:
```csharp
// In Program.cs or Startup
services.AddSingleton<ApiConfiguration>();

// In your service
public MyService(ApiConfiguration config)
{
    var alphaVantageKey = config.AlphaVantageApiKey; // Throws if not configured
    var finnhubKey = config.FinnhubApiKey;           // Throws if not configured
    var iexKey = config.IexCloudApiKey;              // Returns empty string if not configured
}
```