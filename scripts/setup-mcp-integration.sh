#!/bin/bash

# Setup MCP Integration for Day Trading Platform
# This script configures real-time MCP analysis to prevent code quality degradation

PROJECT_ROOT="/home/nader/my_projects/C#/DayTradingPlatform"
MCP_DIR="/home/nader/my_projects/C#/mcp-code-analyzer"

echo "🔧 Setting up MCP Integration for Day Trading Platform"
echo "===================================================="
echo ""

# 1. Create MCP configuration
echo "📋 Creating MCP configuration..."
mkdir -p "$PROJECT_ROOT/.mcp"
cat > "$PROJECT_ROOT/.mcp/analyzer.config.json" << 'EOF'
{
  "name": "Day Trading Platform",
  "language": "csharp",
  "profile": "day-trading",
  "tools": [
    "analyze",
    "validateFinancialLogic",
    "validateCSharpFinancials",
    "analyzeLatency",
    "checkSecurity",
    "analyzeScalability",
    "architecture",
    "detectDuplication"
  ],
  "criticalRules": {
    "decimal-for-money": "error",
    "no-blocking-operations": "error",
    "order-validation-required": "error",
    "risk-limits-enforced": "error",
    "no-console-writeline": "error",
    "null-reference-check": "warning"
  },
  "performance": {
    "maxLatencyMicros": 100,
    "cacheEnabled": true
  },
  "ignore": [
    "**/bin/**",
    "**/obj/**",
    "**/Generated/**",
    "**/Migrations/**"
  ],
  "realTimeAnalysis": true,
  "failOn": ["error"]
}
EOF

# 2. Create Git pre-commit hook
echo "🪝 Setting up Git pre-commit hook..."
cat > "$PROJECT_ROOT/.git/hooks/pre-commit" << 'EOF'
#!/bin/bash
# MCP Pre-commit Hook - Prevent committing code with critical issues

echo "🔍 Running MCP Code Analysis..."

# Get list of staged C# files
STAGED_FILES=$(git diff --cached --name-only --diff-filter=ACM | grep '\.cs$')

if [ -z "$STAGED_FILES" ]; then
  echo "✅ No C# files to analyze"
  exit 0
fi

# Analyze each staged file
ERRORS=0
for FILE in $STAGED_FILES; do
  echo "Analyzing: $FILE"
  cd /home/nader/my_projects/C#/mcp-code-analyzer
  
  # Run analysis
  RESULT=$(npx tsx scripts/analyze-file.ts "$PROJECT_ROOT/$FILE" 2>&1)
  
  # Check for errors
  if echo "$RESULT" | grep -q "✗"; then
    echo "❌ Critical errors found in $FILE:"
    echo "$RESULT" | grep "✗"
    ERRORS=$((ERRORS + 1))
  fi
  
  # Check for financial issues
  if echo "$RESULT" | grep -q -E "(float|double).*[Pp]rice|[Mm]oney|[Aa]mount"; then
    echo "💰 Financial precision issue in $FILE"
    ERRORS=$((ERRORS + 1))
  fi
done

if [ $ERRORS -gt 0 ]; then
  echo ""
  echo "❌ Commit blocked: $ERRORS critical issues found"
  echo "Please fix the issues and try again."
  exit 1
fi

echo "✅ MCP analysis passed"
exit 0
EOF

chmod +x "$PROJECT_ROOT/.git/hooks/pre-commit"

# 3. Create VS Code tasks
echo "📝 Creating VS Code tasks..."
mkdir -p "$PROJECT_ROOT/.vscode"
cat > "$PROJECT_ROOT/.vscode/tasks.json" << 'EOF'
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "MCP: Analyze Current File",
      "type": "shell",
      "command": "cd /home/nader/my_projects/C#/mcp-code-analyzer && npx tsx scripts/analyze-file.ts ${file}",
      "group": {
        "kind": "test",
        "isDefault": true
      },
      "presentation": {
        "reveal": "always",
        "panel": "dedicated"
      },
      "problemMatcher": []
    },
    {
      "label": "MCP: Analyze Project",
      "type": "shell",
      "command": "cd /home/nader/my_projects/C#/mcp-code-analyzer && ./scripts/analyze-trading.sh /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform",
      "group": "test",
      "presentation": {
        "reveal": "always",
        "panel": "dedicated"
      },
      "problemMatcher": []
    },
    {
      "label": "MCP: Start Real-time Monitor",
      "type": "shell",
      "command": "cd /home/nader/my_projects/C#/mcp-code-analyzer && npm run start -- --watch /home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform --subscribe analyze,validateFinancialLogic,analyzeLatency",
      "isBackground": true,
      "problemMatcher": []
    }
  ]
}
EOF

# 4. Create file watcher script
echo "👁️ Creating file watcher script..."
cat > "$PROJECT_ROOT/scripts/mcp-file-watcher.sh" << 'EOF'
#!/bin/bash
# Real-time MCP file watcher

MCP_DIR="/home/nader/my_projects/C#/mcp-code-analyzer"
PROJECT_DIR="/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform"

echo "👁️ Starting MCP real-time file watcher..."
echo "Monitoring: $PROJECT_DIR"
echo "Press Ctrl+C to stop"
echo ""

# Watch for file changes
inotifywait -m -r -e modify,create --format '%w%f' "$PROJECT_DIR" | while read FILE; do
  if [[ "$FILE" =~ \.cs$ ]] && [[ ! "$FILE" =~ /(bin|obj)/ ]]; then
    echo "📄 File changed: ${FILE#$PROJECT_DIR/}"
    
    # Quick analysis
    cd "$MCP_DIR"
    npx tsx scripts/analyze-file.ts "$FILE" 2>&1 | grep -E "(✗|⚠|Summary:)" | sed 's/^/  /'
    echo ""
  fi
done
EOF

chmod +x "$PROJECT_ROOT/scripts/mcp-file-watcher.sh"

# 5. Create CI/CD integration script
echo "🚀 Creating CI/CD integration script..."
cat > "$PROJECT_ROOT/scripts/mcp-ci-check.sh" << 'EOF'
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
EOF

chmod +x "$PROJECT_ROOT/scripts/mcp-ci-check.sh"

echo ""
echo "✅ MCP Integration Setup Complete!"
echo ""
echo "📋 What's been configured:"
echo "  1. MCP configuration file (.mcp/analyzer.config.json)"
echo "  2. Git pre-commit hook (blocks commits with errors)"
echo "  3. VS Code tasks (Ctrl+Shift+P > Run Task > MCP:...)"
echo "  4. File watcher script (scripts/mcp-file-watcher.sh)"
echo "  5. CI/CD integration script (scripts/mcp-ci-check.sh)"
echo ""
echo "🚀 Next steps:"
echo "  1. Run: ./scripts/mcp-file-watcher.sh (for real-time monitoring)"
echo "  2. In VS Code: Use Ctrl+Shift+P > Run Task > MCP: Analyze Current File"
echo "  3. Before commits: Pre-commit hook will run automatically"
echo ""
echo "📍 Master Todo List location:"
echo "  /home/nader/my_projects/C#/DayTradingPlatform/MainDocs/V1.x/Master_ToDo_List.md"