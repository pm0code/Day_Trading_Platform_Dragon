#!/bin/bash

# Comprehensive analysis of all C# files in Day Trading Platform
PROJECT_DIR="/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform"
ANALYZER_DIR="/home/nader/my_projects/C#/mcp-code-analyzer"
OUTPUT_FILE="/home/nader/my_projects/C#/DayTradingPlatform/mcp-analysis-results.txt"

echo "🚀 MCP Code Analyzer - Full Project Analysis" | tee "$OUTPUT_FILE"
echo "===========================================" | tee -a "$OUTPUT_FILE"
echo "" | tee -a "$OUTPUT_FILE"
echo "Starting at: $(date)" | tee -a "$OUTPUT_FILE"
echo "Project: $PROJECT_DIR" | tee -a "$OUTPUT_FILE"
echo "" | tee -a "$OUTPUT_FILE"

# Count total C# files
TOTAL_FILES=$(find "$PROJECT_DIR" -name "*.cs" -type f | grep -v "/bin/" | grep -v "/obj/" | wc -l)
echo "Total C# files to analyze: $TOTAL_FILES" | tee -a "$OUTPUT_FILE"
echo "" | tee -a "$OUTPUT_FILE"

# Initialize counters
ANALYZED=0
CRITICAL_COUNT=0
WARNING_COUNT=0
INFO_COUNT=0

# Create temporary file for detailed results
TEMP_RESULTS="/tmp/mcp-analysis-detailed.txt"
> "$TEMP_RESULTS"

# Analyze each file
find "$PROJECT_DIR" -name "*.cs" -type f | grep -v "/bin/" | grep -v "/obj/" | while read -r file; do
    ANALYZED=$((ANALYZED + 1))
    echo "[$ANALYZED/$TOTAL_FILES] Analyzing: ${file#$PROJECT_DIR/}" | tee -a "$OUTPUT_FILE"
    
    # Run analyzer and capture output
    cd "$ANALYZER_DIR" && npx tsx scripts/analyze-file.ts "$file" 2>&1 | grep -E "(Line [0-9]+:|Summary:|errors|warnings|info)" >> "$TEMP_RESULTS"
done

echo "" | tee -a "$OUTPUT_FILE"
echo "============================================" | tee -a "$OUTPUT_FILE"
echo "📊 ANALYSIS COMPLETE" | tee -a "$OUTPUT_FILE"
echo "============================================" | tee -a "$OUTPUT_FILE"
echo "" | tee -a "$OUTPUT_FILE"

# Parse results and create summary
echo "📋 DETAILED RESULTS:" | tee -a "$OUTPUT_FILE"
echo "" | tee -a "$OUTPUT_FILE"
cat "$TEMP_RESULTS" | tee -a "$OUTPUT_FILE"

echo "" | tee -a "$OUTPUT_FILE"
echo "Completed at: $(date)" | tee -a "$OUTPUT_FILE"
echo "" | tee -a "$OUTPUT_FILE"
echo "Full results saved to: $OUTPUT_FILE" | tee -a "$OUTPUT_FILE"