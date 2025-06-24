# Trading Platform Test Runner

This test runner validates the functionality of our data providers (Finnhub and AlphaVantage).

## Setup

### 1. Get Free API Keys

- **Finnhub**: Sign up at https://finnhub.io/ (60 calls/minute free tier)
- **AlphaVantage**: Sign up at https://www.alphavantage.co/support/#api-key (5 calls/minute free tier)

### 2. Configure API Keys

Choose one of these methods:

#### Method 1: Environment Variables (Recommended)
```bash
export FINNHUB_API_KEY="your_finnhub_api_key_here"
export ALPHAVANTAGE_API_KEY="your_alphavantage_api_key_here"
```

#### Method 2: User Secrets (Development)
```bash
cd TradingPlatform.TestRunner
dotnet user-secrets init
dotnet user-secrets set "ApiConfiguration:Finnhub:ApiKey" "your_finnhub_api_key_here"
dotnet user-secrets set "ApiConfiguration:AlphaVantage:ApiKey" "your_alphavantage_api_key_here"
```

#### Method 3: Update appsettings.json
Edit `appsettings.json` and replace the placeholder values.

## Running the Tests

```bash
cd TradingPlatform.TestRunner
dotnet run
```

## What It Tests

### Finnhub Provider
- Real-time quotes
- Company profiles
- Market news
- Market status (open/closed)
- Batch quotes for multiple symbols

### AlphaVantage Provider
- Real-time data
- Historical data (5-day window)
- Company overview
- Observable subscription pattern

### Market Data Aggregator
- Aggregated data from multiple sources
- Fallback mechanism testing

## Expected Output

Successful tests will show:
```
✓ Success: AAPL Price=$175.50, Volume=52,345,678
✓ Success: Microsoft Corporation (MSFT)
✓ Success: Retrieved 50 news items
✓ Market is currently: OPEN
```

Failed tests will show:
```
✗ Failed: No quote data returned
✗ Error: Invalid API key
```

## Troubleshooting

1. **"Invalid API key"**: Verify your API keys are correctly configured
2. **"Rate limit exceeded"**: Wait a minute and try again (free tier limits)
3. **"No data returned"**: Check if market is open (some endpoints only work during market hours)