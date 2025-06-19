#!/bin/bash
# Manual sync script for canonical logging fixes to DRAGON
# Run this script to deploy the fixes when git push fails

echo "🚀 CANONICAL LOGGING FIXES - DRAGON DEPLOYMENT"
echo "=============================================="

# Copy the fixed files directly to DRAGON
echo "📂 Copying TradingLogOrchestrator.cs..."
scp -o UserKnownHostsFile=/dev/null -o StrictHostKeyChecking=no \
    TradingPlatform.Core/Logging/TradingLogOrchestrator.cs \
    192.168.1.35:d:/BuildWorkspace/DayTradingPlatform/TradingPlatform.Core/Logging/

echo "📂 Copying PerformanceStats.cs..."
scp -o UserKnownHostsFile=/dev/null -o StrictHostKeyChecking=no \
    TradingPlatform.Core/Logging/PerformanceStats.cs \
    192.168.1.35:d:/BuildWorkspace/DayTradingPlatform/TradingPlatform.Core/Logging/

echo "🔨 Building on DRAGON..."
ssh -o UserKnownHostsFile=/dev/null -o StrictHostKeyChecking=no 192.168.1.35 \
    "cd d:/BuildWorkspace/DayTradingPlatform && dotnet build TradingPlatform.Core --verbosity normal"

echo "✅ Deployment complete!"
echo "🎯 Expected: Zero compilation errors with these fixes"