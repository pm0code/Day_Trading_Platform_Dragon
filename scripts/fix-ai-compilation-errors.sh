#!/bin/bash

# Fix script for AI infrastructure compilation errors

echo "Fixing AI infrastructure compilation errors..."

# Fix 1: Replace all .Data with .Value in TradingResult usage
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/\.Data\!/\.Value\!/g' {} \;
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/\.Data\]/\.Value\]/g' {} \;
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/\.Data;/\.Value;/g' {} \;
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/result\.Data/result\.Value/g' {} \;

# Fix 2: Replace MarketQuote property names
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/quote\.Open/quote\.DayOpen/g' {} \;
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/quote\.High/quote\.DayHigh/g' {} \;
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/quote\.Low/quote\.DayLow/g' {} \;
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/quote\.Close/quote\.CurrentPrice/g' {} \;
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/q\.Open/q\.DayOpen/g' {} \;
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/q\.High/q\.DayHigh/g' {} \;
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/q\.Low/q\.DayLow/g' {} \;
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/q\.Close/q\.CurrentPrice/g' {} \;

# Fix 3: Replace Bid/Ask property names
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/quote\.Ask/quote\.AskPrice ?? quote\.CurrentPrice/g' {} \;
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/quote\.Bid/quote\.BidPrice ?? quote\.CurrentPrice/g' {} \;

# Fix 4: Replace LogDebug with LogInfo (CanonicalServiceBase doesn't have LogDebug)
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/LogDebug(/LogInfo(/g' {} \;

# Fix 5: Fix the TradingError usage in Failure calls
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/, modelResult\.Error);/, modelResult\.Error?.Exception);/g' {} \;
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/, result\.Error);/, result\.Error?.Exception);/g' {} \;
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/, encodingResults\[i\]\.Error);/, encodingResults\[i\]\.Error?.Exception);/g' {} \;
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/, fusionResult\.Error);/, fusionResult\.Error?.Exception);/g' {} \;
find ../MarketAnalyzer/src/Infrastructure/MarketAnalyzer.Infrastructure.AI -name "*.cs" -type f -exec sed -i 's/, normalizedFeatures\.Error);/, normalizedFeatures\.Error?.Exception);/g' {} \;

echo "Fixes applied. Building to verify..."
cd ../MarketAnalyzer
dotnet build src/Infrastructure/MarketAnalyzer.Infrastructure.AI/MarketAnalyzer.Infrastructure.AI.csproj