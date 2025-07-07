#!/bin/bash
# Script to fix package versions across all projects to use .NET 8.0 versions

echo "Fixing package versions to .NET 8.0 standard..."

# List of files to update
files=(
    "TradingPlatform.Analytics/TradingPlatform.Analytics.csproj"
    "TradingPlatform.Analytics.Tests/TradingPlatform.Analytics.Tests.csproj"
    "TradingPlatform.CostManagement/TradingPlatform.CostManagement.csproj"
    "TradingPlatform.DisplayManagement/TradingPlatform.DisplayManagement.csproj"
    "TradingPlatform.Foundation/TradingPlatform.Foundation.csproj"
    "TradingPlatform.Gateway/TradingPlatform.Gateway.csproj"
    "TradingPlatform.Logging/TradingPlatform.Logging.csproj"
    "TradingPlatform.Messaging/TradingPlatform.Messaging.csproj"
    "TradingPlatform.ML/TradingPlatform.ML.csproj"
    "TradingPlatform.PaperTrading/TradingPlatform.PaperTrading.csproj"
    "TradingPlatform.RiskManagement/TradingPlatform.RiskManagement.csproj"
    "TradingPlatform.Testing/TradingPlatform.Testing.csproj"
    "TradingPlatform.TestRunner/TradingPlatform.TestRunner.csproj"
    "TradingPlatform.TimeSeries/TradingPlatform.TimeSeries.csproj"
    "TradingPlatform.TradingApp/TradingPlatform.TradingApp.csproj"
    "TradingPlatform.WindowsOptimization/TradingPlatform.WindowsOptimization.csproj"
)

# Fix package versions
for file in "${files[@]}"; do
    if [ -f "$file" ]; then
        echo "Updating $file..."
        
        # Microsoft Extensions packages
        sed -i 's/Version="9\.0\.0"/Version="8.0.0"/g' "$file"
        
        # System packages  
        sed -i 's/<PackageReference Include="System\.Threading\.Channels" Version="9\.0\.0"/<PackageReference Include="System.Threading.Channels" Version="8.0.0"/g' "$file"
        sed -i 's/<PackageReference Include="System\.Collections\.Immutable" Version="9\.0\.0"/<PackageReference Include="System.Collections.Immutable" Version="8.0.0"/g' "$file"
        sed -i 's/<PackageReference Include="System\.Text\.Json" Version="9\.0\.0"/<PackageReference Include="System.Text.Json" Version="8.0.4"/g' "$file"
        sed -i 's/<PackageReference Include="System\.Diagnostics\.DiagnosticSource" Version="9\.0\.0"/<PackageReference Include="System.Diagnostics.DiagnosticSource" Version="8.0.0"/g' "$file"
        sed -i 's/<PackageReference Include="System\.Management" Version="9\.0\.0"/<PackageReference Include="System.Management" Version="8.0.0"/g' "$file"
        
        # Serilog packages
        sed -i 's/<PackageReference Include="Serilog\.AspNetCore" Version="9\.0\.0"/<PackageReference Include="Serilog.AspNetCore" Version="8.0.0"/g' "$file"
        sed -i 's/<PackageReference Include="Serilog\.Sinks\.Seq" Version="9\.0\.0"/<PackageReference Include="Serilog.Sinks.Seq" Version="8.0.0"/g' "$file"
        
        # Code Analysis
        sed -i 's/<PackageReference Include="Microsoft\.CodeAnalysis\.NetAnalyzers" Version="9\.0\.0"/<PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="8.0.0"/g' "$file"
    fi
done

echo "Package version fixes complete!"
echo ""
echo "Next steps:"
echo "1. Run 'dotnet restore' to restore packages"
echo "2. Run 'dotnet build' to verify the build succeeds"