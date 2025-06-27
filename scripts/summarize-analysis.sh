#!/bin/bash

ANALYSIS_FILE="/home/nader/my_projects/C#/DayTradingPlatform/key-areas-analysis.txt"
SUMMARY_FILE="/home/nader/my_projects/C#/DayTradingPlatform/analysis-summary.txt"

echo "📊 MCP Code Analysis Summary" | tee "$SUMMARY_FILE"
echo "===========================" | tee -a "$SUMMARY_FILE"
echo "" | tee -a "$SUMMARY_FILE"

# Count total issues by type
ERRORS=$(grep -c "✗" "$ANALYSIS_FILE" 2>/dev/null || echo "0")
WARNINGS=$(grep -c "⚠" "$ANALYSIS_FILE" 2>/dev/null || echo "0")
INFO=$(grep -c "ℹ" "$ANALYSIS_FILE" 2>/dev/null || echo "0")

echo "🔍 Issue Counts:" | tee -a "$SUMMARY_FILE"
echo "  ✗ Errors: $ERRORS" | tee -a "$SUMMARY_FILE"
echo "  ⚠ Warnings: $WARNINGS" | tee -a "$SUMMARY_FILE"
echo "  ℹ Info: $INFO" | tee -a "$SUMMARY_FILE"
echo "" | tee -a "$SUMMARY_FILE"

# Analyze by file
echo "📁 Files Analyzed:" | tee -a "$SUMMARY_FILE"
grep "📄" "$ANALYSIS_FILE" | sort | uniq | tee -a "$SUMMARY_FILE"
echo "" | tee -a "$SUMMARY_FILE"

# Common issue patterns
echo "🔥 Most Common Issues:" | tee -a "$SUMMARY_FILE"
grep -E "(Line [0-9]+:)" "$ANALYSIS_FILE" | sed 's/.*Line [0-9]*: //' | sort | uniq -c | sort -rn | head -10 | tee -a "$SUMMARY_FILE"
echo "" | tee -a "$SUMMARY_FILE"

# Financial-related issues
echo "💰 Financial/Decimal Issues:" | tee -a "$SUMMARY_FILE"
grep -i "decimal\|financial\|money\|price" "$ANALYSIS_FILE" | wc -l | tee -a "$SUMMARY_FILE"
echo "" | tee -a "$SUMMARY_FILE"

# Performance issues
echo "⚡ Performance Issues:" | tee -a "$SUMMARY_FILE"
grep -i "performance\|LINQ\|async\|allocation" "$ANALYSIS_FILE" | wc -l | tee -a "$SUMMARY_FILE"
echo "" | tee -a "$SUMMARY_FILE"

echo "Full analysis saved to: $ANALYSIS_FILE" | tee -a "$SUMMARY_FILE"
echo "Summary saved to: $SUMMARY_FILE" | tee -a "$SUMMARY_FILE"