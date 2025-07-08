# CLAUDE.md - MarketAnalyzer Project Guidance

This file provides comprehensive guidance to Claude Code (claude.ai/code) when working with the MarketAnalyzer project.

## 🔴 CRITICAL: Agent Identity and Inter-Agent Communication

### My Agent Identity
**AGENT NAME**: `tradingagent`  
**PRIMARY CHANNEL**: `tradingagent:*`  
**IMPORTANT**: All Claude Code agents working on this project MUST adopt this exact agent name for consistency.
**MANDATORY**: After reading this CLAUDE.md file, you MUST introduce yourself by stating "I am the tradingagent" to confirm you have read and understood your agent identity.

## 🎯 Project Overview

**MarketAnalyzer** is a high-performance, single-user desktop application for Windows 11 x64 that provides real-time market analysis, technical indicators, AI-driven insights, and trading recommendations.

### Key Pivot Decision (July 7, 2025)
- **FROM**: Full trading platform with FIX Engine and order execution
- **TO**: Analysis and recommendation system (no direct trading)
- **RATIONALE**: Users will execute trades on their existing broker platforms based on our recommendations

## 🚨 MANDATORY: Read Development Standards First

**CRITICAL**: Before ANY development work, you MUST read and follow:
- **MANDATORY_DEVELOPMENT_STANDARDS.md**: `D:\Projects\CSharp\Day_Trading_Platform_Dragon\AA.LessonsLearned\MUSTDOs\MANDATORY_DEVELOPMENT_STANDARDS-V3.md`
- **MANDATORY_STANDARDS_COMPLIANCE_ENFORCEMENT.md**: `D:\Projects\CSharp\Day_Trading_Platform_Dragon\AA.LessonsLearned\MUSTDOs\MANDATORY_STANDARDS_COMPLIANCE_ENFORCEMENT.md`
- **Holistic Architecture Instruction Set**: `D:\Projects\CSharp\Day_Trading_Platform_Dragon\AA.LessonsLearned\MUSTDOs\Holistic Architecture Instruction Set for Claude Code.md`

**🔴 CRITICAL: Research Documentation**
**MANDATORY**: You MUST READ AND FOLLOW all documents in this folder:
- **ResearchDocs/MustReads/**: `D:\Projects\CSharp\Day_Trading_Platform_Dragon\ResearchDocs\MustReads\`
- These documents contain critical research findings and implementation guidelines that are MANDATORY for proper development

## 🔴 CRITICAL: Financial Calculation Standards

**MANDATORY**: ALL financial calculations MUST use `System.Decimal` type for precision compliance:
- **ABSOLUTE REQUIREMENT**: All monetary values, financial calculations, and precision-sensitive operations MUST use `decimal` type
- Using `double` or `float` for financial calculations is STRICTLY FORBIDDEN and will result in code rejection
- This is non-negotiable for financial applications

## 🔴 CRITICAL MANDATE: MCP Real-Time Monitoring

**ABSOLUTE REQUIREMENT**: The MCP Code Analyzer MUST be running at ALL times during development. This is non-negotiable.

### MCP Monitoring Checklist:
1. **Before ANY coding session**: Start MCP file watcher
2. **During development**: Verify MCP is catching issues in real-time
3. **Before EVERY commit**: Ensure pre-commit hook runs MCP analysis
4. **In VS Code**: Use MCP tasks for current file analysis
5. **CI/CD**: MCP analysis must pass before any merge

**FAILURE TO RUN MCP = TECHNICAL DEBT ACCUMULATION = PROJECT FAILURE**

## 📋 Essential Documents

1. **PRD**: `/MainDocs/After_Pivot/PRD_High-Performance Day Trading Analysis & Recommendation System.md`
2. **EDD**: `/MainDocs/After_Pivot/EDD_MarketAnalyzer_Engineering_Design_Document_2025-07-07.md`
3. **Todo List**: `/MainDocs/After_Pivot/MasterTodoList_MarketAnalyzer_2025-07-07.md`

## 🏗️ Architecture Summary

### Technology Stack
- **Framework**: .NET 8/9
- **UI**: WinUI 3 with MVVM (Community Toolkit)
- **Market Data**: Finnhub API ($50/month plan)
- **Technical Analysis**: Skender.Stock.Indicators
- **AI/ML**: ML.NET + ONNX Runtime
- **Database**: LiteDB
- **Charting**: LiveCharts2 + ScottPlot

### Project Structure
```
MarketAnalyzer/
├── src/
│   ├── Foundation/          # Canonical patterns, base classes
│   ├── Domain/             # Core business logic
│   ├── Infrastructure/     # External integrations
│   ├── Application/        # Use cases, orchestration
│   └── Presentation/       # WinUI 3 desktop app
├── tests/
├── docs/
├── scripts/
└── tools/
```

## 🔴 CRITICAL: Mandatory Development Standards

### Every Method MUST Have:
```csharp
public async Task<TradingResult<T>> MethodName(params)
{
    LogMethodEntry(); // MANDATORY - First line
    try
    {
        // Implementation
        
        LogMethodExit(); // MANDATORY - Before return
        return TradingResult<T>.Success(result);
    }
    catch (Exception ex)
    {
        LogError("Description", ex);
        LogMethodExit(); // MANDATORY - Even in catch
        return TradingResult<T>.Failure("ERROR_CODE", "Message", ex);
    }
}
```

### Canonical Patterns (MANDATORY - NO EXCEPTIONS)
1. **ALL** services inherit from `CanonicalServiceBase`
2. **ALL** methods use `LogMethodEntry()`/`LogMethodExit()` - INCLUDING PRIVATE METHODS
3. **ALL** operations return `TradingResult<T>`
4. **ALL** financial values use `decimal` (NEVER float/double)
5. **ALL** errors use SCREAMING_SNAKE_CASE codes
6. **ALL** catch blocks MUST have `LogMethodExit()` before throw/return
7. **ZERO** build warnings allowed - treat warnings as errors
8. **ZERO** errors allowed before moving to next task

## 🚀 Current Status

### Phase: 0 - Project Setup & Foundation
**Start Date**: July 7, 2025  
**Current Task**: Setting up project structure and foundation components

### Completed:
- [x] Created EDD (Engineering Design Document)
- [x] Created MasterTodoList with 8 phases
- [x] Created initial directory structure
- [ ] Git repository initialization (pending)
- [ ] Solution and project files (pending)

### Next Steps:
1. Initialize Git repository
2. Create MarketAnalyzer.sln
3. Create Foundation projects
4. Copy mandatory standards from DayTradinPlatform

## 💡 Key Architectural Decisions

### 1. Clean Separation from Legacy
- MarketAnalyzer is a complete rewrite, not a refactor
- No dependencies on DayTradinPlatform code
- Cherry-pick only proven patterns (canonical base classes)

### 2. Performance First
- Target: <100ms API response, <50ms calculations
- Hardware: i9-14900K (24 cores), RTX 4070 Ti + RTX 3060 Ti
- Memory: 32GB DDR5 (upgrading to 64GB)

### 3. Single User Focus
- No authentication needed
- Local data storage
- Simplified security model
- Focus on performance over multi-tenancy

### 4. AI/ML Integration
- GPU-accelerated inference (ONNX Runtime)
- Pre-trained models for sentiment analysis
- Custom models for price prediction
- Real-time pattern recognition

## ⚠️ Important Context

### From DayTradinPlatform Experience:
1. **FixEngine Issues**: Architectural mismatch with base classes - avoiding in new design
2. **Canonical Patterns**: Proven successful, will reuse with improvements
3. **GPU Acceleration**: Already implemented, will migrate and enhance
4. **Build Standards**: ZERO ERRORS, ZERO WARNINGS before moving on

### Finnhub API Limits:
- 60 calls/minute
- 30 calls/second burst
- Must implement rate limiting and caching

### Hardware Optimization:
- Use P-cores for critical paths
- E-cores for background tasks
- GPU0 (RTX 4070 Ti) for primary AI inference
- GPU1 (RTX 3060 Ti) for parallel operations

## 📝 Development Workflow

### MANDATORY Pre-Development Checklist:
- [ ] Read MANDATORY_DEVELOPMENT_STANDARDS-V3.md
- [ ] Read MANDATORY_STANDARDS_COMPLIANCE_ENFORCEMENT.md
- [ ] Read Holistic Architecture Instruction Set
- [ ] Verify MCP Code Analyzer is running
- [ ] Review canonical patterns checklist

### For EVERY Feature:
1. Write unit tests first (TDD)
2. Implement with canonical patterns
3. Add comprehensive logging (LogMethodEntry/Exit in ALL methods)
4. Profile performance
5. Verify ZERO warnings and errors

### Before EVERY Commit:
1. Build with ZERO errors/warnings
2. Run all tests
3. Run MCP analysis
4. Update documentation
5. Verify all methods have entry/exit logging

## 🎯 Success Criteria

### Technical Metrics
- API response time: <100ms
- Indicator calculation: <50ms  
- AI inference: <200ms
- Memory usage: <2GB
- Cache hit rate: >90%

### Quality Metrics
- Code coverage: >90%
- Zero build warnings
- All public APIs documented
- Canonical patterns 100% compliance

## 🔧 Troubleshooting

### If you see build errors:
1. Check Directory.Build.props for package versions
2. Ensure .NET 8 SDK installed
3. Verify NuGet package restore
4. Check for missing project references

### If performance is poor:
1. Check GPU acceleration is enabled
2. Verify caching is working
3. Profile with BenchmarkDotNet
4. Check for blocking I/O operations

## 📞 Communication

### When working on MarketAnalyzer:
- **FIRST**: Announce "I am the tradingagent" to confirm identity
- Always announce: "I am working on MarketAnalyzer"
- Reference this CLAUDE.md for guidance
- Follow the phased approach in MasterTodoList
- Maintain strict separation from DayTradinPlatform code
- Run MCP Code Analyzer continuously

### Git Commit Format:
```
feat|fix|docs|refactor: Brief description

- Detailed change 1
- Detailed change 2

Related: #issue-number
```

---

## ⚠️ Critical Reminders

### Build Standards (MANDATORY):
- **NEVER** leave a project in unbuildable state
- **NEVER** commit with warnings or errors
- **NEVER** skip LogMethodEntry/LogMethodExit
- **NEVER** use float/double for money
- **ALWAYS** run MCP analyzer
- **ALWAYS** follow canonical patterns

### Model Guidance:
- Use Opus for complex code generation, architectural design, and detailed technical tasks

**Remember**: This is a fresh start with lessons learned. Build it right from the beginning!