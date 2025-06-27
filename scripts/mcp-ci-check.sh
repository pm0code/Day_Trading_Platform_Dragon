#!/bin/bash
# MCP CI/CD Integration Script

PROJECT_DIR="/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform"
MCP_DIR="/home/nader/my_projects/C#/mcp-code-analyzer"
EXIT_CODE=0

echo "🔍 MCP Code Analysis - CI/CD Check"
echo "=================================="
echo ""

# Run full project analysis
cd "$MCP_DIR"
./scripts/analyze-trading.sh "$PROJECT_DIR" > /tmp/mcp-ci-results.txt 2>&1

# Check for critical errors
ERRORS=$(grep -c "✗" /tmp/mcp-ci-results.txt || echo "0")
WARNINGS=$(grep -c "⚠" /tmp/mcp-ci-results.txt || echo "0")

echo "📊 Results:"
echo "  Errors: $ERRORS"
echo "  Warnings: $WARNINGS"
echo ""

if [ $ERRORS -gt 0 ]; then
  echo "❌ Build failed: Critical errors found"
  grep "✗" /tmp/mcp-ci-results.txt
  EXIT_CODE=1
fi

# Check specific requirements
echo "🔍 Checking specific requirements..."

# Financial precision check
FLOAT_ISSUES=$(grep -i -E "(float|double).*[Pp]rice|[Mm]oney|[Aa]mount" /tmp/mcp-ci-results.txt | wc -l)
if [ $FLOAT_ISSUES -gt 0 ]; then
  echo "❌ Financial precision violations: $FLOAT_ISSUES"
  EXIT_CODE=1
fi

# Performance check
PERF_ISSUES=$(grep -i "LINQ in hot path\|blocking operation" /tmp/mcp-ci-results.txt | wc -l)
if [ $PERF_ISSUES -gt 0 ]; then
  echo "❌ Performance violations: $PERF_ISSUES"
  EXIT_CODE=1
fi

if [ $EXIT_CODE -eq 0 ]; then
  echo "✅ All MCP checks passed!"
else
  echo ""
  echo "Full results saved to: /tmp/mcp-ci-results.txt"
fi

exit $EXIT_CODE
