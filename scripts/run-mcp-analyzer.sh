#!/bin/bash

# Wrapper script to run MCP Code Analyzer from its original location
MCP_ANALYZER_DIR="/home/nader/my_projects/C#/mcp-code-analyzer"
TRADING_PROJECT_DIR="/home/nader/my_projects/C#/DayTradingPlatform"

echo "🚀 Running MCP Code Analyzer from original location"
echo "=================================================="
echo ""
echo "Analyzer location: $MCP_ANALYZER_DIR"
echo "Target project: $TRADING_PROJECT_DIR"
echo ""

# Change to MCP analyzer directory and run the analysis
cd "$MCP_ANALYZER_DIR" && ./scripts/analyze-trading.sh "$TRADING_PROJECT_DIR"