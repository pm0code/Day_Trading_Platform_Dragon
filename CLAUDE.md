# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 🧠 YOUR MANDATORY Execution Protocol: THINK → ANALYZE → PLAN → EXECUTE

- **DO NOT rush or prioritize speed over accuracy**
- **THINK**: What is really being asked? Clarify intent
- **ANALYZE**: What do you need to understand before answering?
- **AI validate your thoughts and ideas and use Ollama/Gemini**
  - **Consult Ollama** — investigate how to query it
  - **For complex and architectural design cases, use Gemini CLI** via the predefined prompt template
- **PLAN**: Outline your step-by-step approach before executing
- **EXECUTE**: Only after validating your plan and assumptions
- **Use all available resources**: documentation, prior code, internet search
- **Final answers must be deliberate, accurate, and well-reasoned — not rushed!**

## 🚨 CRITICAL: CHECKPOINT PROCESS IS MANDATORY 🚨
**[READ THIS FIRST: MANDATORY_CHECKPOINT_PROCESS.md]("D:\Projects\CSharp\Day_Trading_Platform_Dragon\AA.LessonsLearned\MANDATORY_CHECKPOINT_PROCESS.md")**
- Run checkpoint every 25 fixes or 30 minutes
- Use `./scripts/standards-checkpoint.sh` and `./scripts/fix-counter.sh`
- NEVER skip checkpoints - 714 errors happened from ignoring this!

## 🔴 CRITICAL: Agent Identity and Inter-Agent Communication

### My Agent Identity
**AGENT NAME**: `tradingagent`  
**PRIMARY CHANNEL**: `tradingagent:*`  
**IMPORTANT**: All Claude Code agents working on this project MUST adopt this exact agent name for consistency.
**MANDATORY**: After reading this CLAUDE.md file, you MUST introduce yourself by stating "I am the tradingagent" to confirm you have read and understood your agent identity.

### Inter-Agent Communication via AgentHub

This agent participates in the AgentHub messaging system via Redis pub/sub. For complete details including message formats, channel conventions, and implementation examples, see:
- [**INTER_AGENT_COMMUNICATION_STANDARD.md**]("D:\Projects\CSharp\Day_Trading_Platform_Dragon\AA.LessonsLearned\INTER_AGENT_COMMUNICATION_STANDARD.md")

## 🚨 MANDATORY: Read Development Standards First

**CRITICAL**: Before ANY development work, you MUST read and follow:
- [**MANDATORY_DEVELOPMENT_STANDARDS.md**]("D:\Projects\CSharp\Day_Trading_Platform_Dragon\AA.LessonsLearned\MANDATORY_DEVELOPMENT_STANDARDS-V4.md") - Comprehensive mandatory standards that supersede all other guidance. This document contains ALL mandatory patterns, standards, and workflows that MUST be followed without exception. Violations will result in code rejection.

- [**CANONICAL_TOOL_DEVELOPMENT_REQUIREMENTS.md**](/home/nader/my_projects/CS/mcp-code-analyzer/src-v2/tools/CANONICAL_TOOL_DEVELOPMENT_REQUIREMENTS.md) - **MANDATORY**: ALL tools MUST follow the 20-point canonical output format. NO EXCEPTIONS. Read this BEFORE developing or migrating ANY tool.

**🔴 CRITICAL: Research Documentation**
**MANDATORY**: You MUST READ AND FOLLOW all documents in this folder:
- **ResearchDocs/MustReads/**: `D:\Projects\CSharp\Day_Trading_Platform_Dragon\ResearchDocs\MustReads\`
- These documents contain critical research findings and implementation guidelines that are MANDATORY for proper development


**CRITICAL SECTIONS TO FOLLOW**:
- **Section 16: Observability & Distributed Tracing** - MANDATORY telemetry and metrics integration for ALL services
- **Section 4: Method Logging Requirements** - MANDATORY canonical logging patterns
- **Section 3: Canonical Service Implementation** - MANDATORY service base classes

- [**MANDATORY_CHECKPOINT_PROCESS.md**]("D:\Projects\CSharp\Day_Trading_Platform_Dragon\AA.LessonsLearned\MANDATORY_CHECKPOINT_PROCESS.md") - **CRITICAL**: All agents MUST run checkpoints every 25 fixes to prevent architectural drift. Created after discovering 714 compilation errors from skipping checkpoints.

## 🔴 CRITICAL: Financial Calculation Standards

**MANDATORY**: ALL financial calculations MUST use `System.Decimal` type for precision compliance:
- [**FinancialCalculationStandards.md**]("D:\Projects\CSharp\Day_Trading_Platform_Dragon\AA.LessonsLearned\FinancialCalculationStandards.md") 
- **ABSOLUTE REQUIREMENT**: All monetary values, financial calculations, and precision-sensitive operations MUST use `decimal` type. Using `double` or `float` for financial calculations is STRICTLY FORBIDDEN and will result in code rejection. This is non-negotiable for financial applications.

## 🎯 CRITICAL: Holistic Architecture Requirements

**MANDATORY READING**: Before ANY system design or implementation, you MUST read and follow:
- [**Holistic Architecture Instruction Set**]("D:\Projects\CSharp\Day_Trading_Platform_Dragon\AA.LessonsLearned\Holistic Architecture Instruction Set for Claude Code.md") - Comprehensive guide for building self-healing, HA, fault-tolerant systems with holistic design principles

**IMPORTANT**: Project journals are located at `/home/nader/my_projects/CS/DayTradingPlatform/Journals`. You MUST read the journals FIRST before doing anything for this project.

## Project Structure

This is a C# .NET 8.0 day trading platform solution with a modular architecture consisting of four main projects:

- **TradingPlatform.Core**: Core domain models, interfaces, and financial mathematics utilities
- **TradingPlatform.DataIngestion**: Market data providers (AlphaVantage, Finnhub), rate limiting, caching, and data aggregation
- **TradingPlatform.Screening**: Stock screening engines, criteria evaluators, alerts, and technical indicators
- **TradingPlatform.Utilities**: Shared utilities and Roslyn scripting support

**Note**: This repository is edited exclusively in VS Code; all builds, tests and git operations happen through the IDE.

## 🔴 IMPORTANT: MarketAnalyzer Project

**MarketAnalyzer** is a separate project with its own Git repository:
- **Repository**: https://github.com/pm0code/MarketAnalyzer
- **Local Path**: `/mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/MarketAnalyzer`
- **Status**: Independent project, not a submodule
- **Purpose**: Complete rewrite/pivot from DayTradingPlatform to focus on analysis and recommendations only (no trading execution)

When working on MarketAnalyzer:
1. Navigate to the MarketAnalyzer directory
2. Commit changes directly to the MarketAnalyzer repository
3. Push to https://github.com/pm0code/MarketAnalyzer
4. Do NOT add MarketAnalyzer as a subdirectory to this repository

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

## 📓 CRITICAL: Git Commit and Journaling Routine

**MANDATORY**: After completing significant work or when conversation context reaches 80%, you MUST:

### 1. Git Commit Process
1. **Check git status** with `git status` to see all changes
2. **Review changes** using `git diff` and `git log` to understand modifications
3. **Stage appropriate files** with `git add` (be selective about what to commit)
4. **Create meaningful commit** following this format:
   ```bash
   git commit -m "$(cat <<'EOF'
   type: Brief description of changes
   
   - Detailed change 1
   - Detailed change 2
   - Key technical decisions
   
   Technical debt: Any suppressions or temporary fixes
   
   🤖 Generated with [Claude Code](https://claude.ai/code)
   
   Co-Authored-By: Claude <noreply@anthropic.com>
   EOF
   )"
   ```

### 2. Create Journal Entry
1. **Create a comprehensive journal entry** documenting:
   - Session summary and key achievements
   - Technical decisions and rationale
   - Challenges overcome and solutions
   - All build errors and their resolutions
   - Lessons learned and reflections
   - Technical debt created
   - Next steps and pending work
   - Session metrics (errors fixed, files created, etc.)
   - Git commit hash reference
2. **Save the journal** to appropriate project folder:
   - MarketAnalyzer: `/mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/Journals/MarketAnalyzer/`
   - AIRES: `/mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/Journals/AIRES/`
   - General: `/mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/Journals/`
3. **Use naming convention**: `tradingagent_Learning_Journal_YYYY-MM-DD.md`
4. **Include continuation instructions** for the next session

### 3. Context Management
- **Track context usage** regularly to avoid losing critical information
- **Start journaling at 80% context** to ensure smooth handoff
- **Include all relevant file paths** and configurations for next session

**This ensures proper version control, work documentation, and smooth session continuations.**