#!/bin/bash

# Targeted analysis of key areas in Day Trading Platform
PROJECT_DIR="/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform"
ANALYZER_DIR="/home/nader/my_projects/C#/mcp-code-analyzer"
OUTPUT_FILE="/home/nader/my_projects/C#/DayTradingPlatform/key-areas-analysis.txt"

echo "🚀 MCP Code Analyzer - Key Areas Analysis" | tee "$OUTPUT_FILE"
echo "========================================" | tee -a "$OUTPUT_FILE"
echo "" | tee -a "$OUTPUT_FILE"
echo "Analyzing critical components..." | tee -a "$OUTPUT_FILE"
echo "" | tee -a "$OUTPUT_FILE"

# Define key areas to analyze
declare -a KEY_AREAS=(
    "TradingPlatform.Core/Mathematics"
    "TradingPlatform.Core/Canonical"
    "TradingPlatform.FixEngine/Core"
    "TradingPlatform.DataIngestion/Providers"
    "TradingPlatform.Screening"
    "TradingPlatform.RiskManagement"
    "TradingPlatform.ML/Models"
)

# Analyze each key area
for area in "${KEY_AREAS[@]}"; do
    echo "📁 Analyzing $area..." | tee -a "$OUTPUT_FILE"
    echo "------------------------" | tee -a "$OUTPUT_FILE"
    
    find "$PROJECT_DIR/$area" -name "*.cs" -type f 2>/dev/null | head -5 | while read -r file; do
        if [ -f "$file" ]; then
            echo "  📄 ${file##*/}" | tee -a "$OUTPUT_FILE"
            cd "$ANALYZER_DIR" && npx tsx scripts/analyze-file.ts "$file" 2>&1 | \
                grep -E "(✗|⚠|ℹ).*Line|Summary:" | \
                sed 's/^/    /' | tee -a "$OUTPUT_FILE"
            echo "" | tee -a "$OUTPUT_FILE"
        fi
    done
    echo "" | tee -a "$OUTPUT_FILE"
done

echo "Analysis complete. Results saved to: $OUTPUT_FILE"