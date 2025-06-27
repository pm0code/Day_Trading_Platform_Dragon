#!/bin/bash

# Full project analysis script for Day Trading Platform
PROJECT_DIR="/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform"
ANALYZER_DIR="/home/nader/my_projects/C#/mcp-code-analyzer"

echo "🚀 MCP Code Analyzer - Full Project Analysis"
echo "==========================================="
echo ""
echo "Project: $PROJECT_DIR"
echo "Analyzer: $ANALYZER_DIR"
echo ""

# Run the full trading analysis script
cd "$ANALYZER_DIR" && ./scripts/analyze-trading.sh "$PROJECT_DIR"