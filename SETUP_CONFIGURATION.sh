#!/bin/bash

# Setup script for API configuration
echo "Setting up API configuration for Day Trading Platform..."

# Navigate to the project directory
cd "$(dirname "$0")/DayTradinPlatform" || exit 1

# Check if appsettings.json already exists
if [ -f "appsettings.json" ]; then
    echo "⚠️  appsettings.json already exists. Backing up to appsettings.json.backup"
    cp appsettings.json appsettings.json.backup
fi

# Copy template to actual config file
cp appsettings.json.template appsettings.json

echo "✅ Configuration template copied to appsettings.json"
echo ""
echo "Next steps:"
echo "1. Edit DayTradinPlatform/appsettings.json"
echo "2. Replace YOUR_ALPHAVANTAGE_API_KEY_HERE with your actual AlphaVantage API key"
echo "3. Replace YOUR_FINNHUB_API_KEY_HERE with your actual Finnhub API key"
echo "4. (Optional) Add IEX Cloud key if you have one"
echo ""
echo "Get free API keys from:"
echo "- AlphaVantage: https://www.alphavantage.co/support/#api-key"
echo "- Finnhub: https://finnhub.io/register"
echo ""
echo "The appsettings.json file is already in .gitignore to prevent accidental commits."