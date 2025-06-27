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
