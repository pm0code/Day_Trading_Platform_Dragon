# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 🚨 MANDATORY: Read Development Standards First

**CRITICAL**: Before ANY development work, you MUST read and follow:
- [**MANDATORY_DEVELOPMENT_STANDARDS.md**](/home/nader/my_projects/CS/AA.LessonsLearned/MANDATORY_DEVELOPMENT_STANDARDS.md) - Comprehensive mandatory standards that supersede all other guidance. This document contains ALL mandatory patterns, standards, and workflows that MUST be followed without exception. Violations will result in code rejection.

**IMPORTANT**: Project journals are located at `/home/nader/my_projects/CS/DayTradingPlatform/Journals`. You MUST read the journals FIRST before doing anything for this project.

## Project Structure

This is a C# .NET 8.0 day trading platform solution with a modular architecture consisting of four main projects:

- **TradingPlatform.Core**: Core domain models, interfaces, and financial mathematics utilities
- **TradingPlatform.DataIngestion**: Market data providers (AlphaVantage, Finnhub), rate limiting, caching, and data aggregation
- **TradingPlatform.Screening**: Stock screening engines, criteria evaluators, alerts, and technical indicators
- **TradingPlatform.Utilities**: Shared utilities and Roslyn scripting support

**Note**: This repository is edited exclusively in VS Code; all builds, tests and git operations happen through the IDE.

## Build Commands

**Navigate to solution directory first:**
```bash
cd DayTradinPlatform
```

**Common build commands:**
```bash
# Restore packages
dotnet restore

# Build solution (Debug)
dotnet build

# Build solution (Release)
dotnet build --configuration Release

# Run application
dotnet run

# Clean build artifacts
dotnet clean
```

**Platform-specific builds (solution is configured for x64):**
```bash
dotnet build --configuration Release --runtime win-x64
```

## Configuration

External API integrations require configuration for:
- AlphaVantage API keys and endpoints
- Finnhub API keys and endpoints  
- Rate limiting parameters
- Cache settings

## MainDocs References

Key documentation files in the MainDocs directory:

- **Day Trading Stock Recommendation Platform - PRD.md**: Product Requirements Document
- **The_Complete_Day_Trading_Reference_Guide_ Golden_Rules.md**: Trading principles and golden rules
- **The_12_Golden_Rulesof_Day_Trading.md**: Core trading principles framework

## Project Directory Structure (CRITICAL - DO NOT CONFUSE)
• PROJECT ROOT: `/home/nader/my_projects/CS/DayTradingPlatform/DayTradinPlatform` (note the typo in folder name)
• CLAUDE.md: `/home/nader/my_projects/CS/DayTradingPlatform/CLAUDE.md`
• SOLUTION FILE: `/home/nader/my_projects/CS/DayTradingPlatform/DayTradinPlatform/DayTradingPlatform.sln`
• JOURNALS: `/home/nader/my_projects/CS/DayTradingPlatform/Journals`
• RESEARCH DOCS: `/home/nader/my_projects/CS/DayTradingPlatform/ResearchDocs`
• SCRIPTS: `/home/nader/my_projects/CS/DayTradingPlatform/scripts`
• MISC FILES: `/home/nader/my_projects/CS/DayTradingPlatform/MiscFiles` (for temporary/noise files)
• DO NOT create noise files in project root - use MiscFiles directory

## Machine Credentials

• DRAGON is a Windows 11 X64 machine @ 192.168.1.35
• RYZEN is an Ubuntu Linux machine @ 192.168.1.36
• For both machines:
  - User: admin
  - Password: 1qwertyuio0

## Model Guidance

• /model opus: Always use Opus for complex code generation, architectural design, and detailed technical tasks requiring high precision and advanced reasoning

## 🔴 CRITICAL MANDATE: MCP Real-Time Monitoring

**ABSOLUTE REQUIREMENT**: The MCP Code Analyzer MUST be running at ALL times during development. This is non-negotiable.

### MCP Monitoring Checklist:
1. **Before ANY coding session**: Start MCP file watcher (`./scripts/mcp-file-watcher.sh`)
2. **During development**: Verify MCP is catching issues in real-time
3. **Before EVERY commit**: Ensure pre-commit hook runs MCP analysis
4. **In VS Code**: Use MCP tasks for current file analysis
5. **CI/CD**: MCP analysis must pass before any merge

### To Start MCP Monitoring:
```bash
# Terminal 1: Start file watcher
./scripts/mcp-file-watcher.sh

# Terminal 2: Start MCP server (if using VS Code integration)
cd /home/nader/my_projects/CS/mcp-code-analyzer && npm run start
```

**FAILURE TO RUN MCP = TECHNICAL DEBT ACCUMULATION = PROJECT FAILURE**

## Agent Identity and Communication

### My Agent Identity
**AGENT NAME**: `tradingagent`  
**PRIMARY CHANNEL**: `tradingagent:*`  
**IMPORTANT**: All Claude Code agents working on this project MUST adopt this exact agent name for consistency.

### Inter-Agent Communication

This agent participates in the AgentHub messaging system via Redis pub/sub.

### Channels
- **Receives on**: `tradingagent:*`
- **Sends to**: Various agent channels as needed
- **Monitors**: `agent:broadcast`, `alert:*`, `alerts:*`, all channels (`*:*`) for system-wide awareness

### CRITICAL: Message Format
**ALWAYS include both 'agent' and 'from' fields with value `tradingagent` to ensure proper display in AgentHub:**

```javascript
{
  agent: "tradingagent",  // CRITICAL: Prevents "unknown" in AgentHub
  from: "tradingagent",   // For inter-agent standard
  to: "target-agent",
  type: "notification",
  subject: "Subject line",
  message: "Message content",
  timestamp: "ISO 8601 timestamp",
  metadata: { ... }
}
```

### Publishing Messages
```bash
# Use the utility (RECOMMENDED)
node /home/nader/my_projects/CS/linear-bug-tracker/src/utils/publish-to-agenthub.js "tradingagent:notification" "Your message" "tradingagent"

# Or in code
node /home/nader/my_projects/CS/DayTradingPlatform/scripts/tradingagent-redis.js
```

### Important Documentation
**Complete Inter-Agent Communication Standard**: `/home/nader/my_projects/CS/AA.LessonsLearned/INTER_AGENT_COMMUNICATION_STANDARD.md`