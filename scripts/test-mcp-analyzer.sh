#!/bin/bash

# Simple test script for MCP Code Analyzer
echo "🧪 Testing MCP Code Analyzer Setup"
echo "================================="
echo ""

# Test with a single file that exists
TEST_FILE="/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Core/Mathematics/FinancialMath.cs"

if [ -f "$TEST_FILE" ]; then
    echo "✅ Test file found: $TEST_FILE"
    echo ""
    echo "Running analyzer on single file..."
    cd /home/nader/my_projects/C#/mcp-code-analyzer && npx tsx scripts/analyze-file.ts "$TEST_FILE"
else
    echo "❌ Test file not found: $TEST_FILE"
    echo "Listing available C# files..."
    find /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform -name "*.cs" -type f | head -10
fi