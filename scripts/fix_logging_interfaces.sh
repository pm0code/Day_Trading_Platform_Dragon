#!/bin/bash
# Script to fix logging interface references after separating ITradingLogger and ITradingOperationsLogger

echo "Fixing logging interface references..."

# Files that need ITradingOperationsLogger (trading-specific operations)
TRADING_APP_FILES=(
    "TradingPlatform.TradingApp/Services/TradingWindowManager.cs"
    "TradingPlatform.TradingApp/Services/MonitorService.cs"
    "TradingPlatform.TradingApp/App.xaml.cs"
    "TradingPlatform.TradingApp/Views/TradingScreens/OrderExecutionScreen.xaml.cs"
    "TradingPlatform.TradingApp/Views/TradingScreens/PrimaryChartingScreen.xaml.cs"
    "TradingPlatform.TradingApp/Views/TradingScreens/PortfolioRiskScreen.xaml.cs"
    "TradingPlatform.TradingApp/Views/TradingScreens/MarketScannerScreen.xaml.cs"
    "TradingPlatform.Gateway/Program.cs"
    "TradingPlatform.Gateway/Services/GatewayOrchestrator.cs"
)

# Update files that need ITradingOperationsLogger
for file in "${TRADING_APP_FILES[@]}"; do
    if [ -f "$file" ]; then
        echo "Processing $file..."
        # Add qualification to avoid ambiguity
        sed -i 's/ITradingLogger _logger/Core.Interfaces.ITradingLogger _logger/g' "$file"
        sed -i 's/ITradingLogger logger/Core.Interfaces.ITradingLogger logger/g' "$file"
        
        # Fix duplicate ITradingLogger parameters in constructors
        sed -i 's/ITradingLogger logger, ITradingLogger tradingLogger/Core.Interfaces.ITradingLogger logger, ITradingOperationsLogger tradingLogger/g' "$file"
        sed -i 's/ITradingLogger _tradingLogger/ITradingOperationsLogger _tradingLogger/g' "$file"
        sed -i 's/ITradingLogger tradingLogger/ITradingOperationsLogger tradingLogger/g' "$file"
    fi
done

echo "Fixing complete!"