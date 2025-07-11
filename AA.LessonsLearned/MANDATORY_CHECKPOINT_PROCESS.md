# 🚨 MANDATORY CHECKPOINT PROCESS FOR ALL AGENTS 🚨

**Created**: 2025-07-09  
**Status**: MANDATORY - All agents MUST follow this process  
**Purpose**: Prevent architectural drift and ensure continuous compliance with mandatory standards

## 🔴 CRITICAL WARNING TO ALL AGENTS - READ THIS FIRST! 🔴

**BEFORE YOU DO ANYTHING - READ THESE 3 DOCUMENTS TO REFRESH YOUR NORTH STAR:**

1. **PRD**: `/MainDocs/After_Pivot/PRD_High-Performance Day Trading Analysis & Recommendation System.md`
2. **EDD**: `/MainDocs/After_Pivot/EDD_MarketAnalyzer_Engineering_Design_Document_2025-07-07.md`  
3. **ARCHITECTURE**: `/MainDocs/After_Pivot/Architecture/Complete_System_Architecture_Analysis_2025-01-09_14-30.md`

**STERN REMINDER**: You are NOT a line fixer! You are a SYSTEM ARCHITECT! These documents contain:
- The BUSINESS REQUIREMENTS you must fulfill
- The TECHNICAL DESIGN you must implement  
- The ARCHITECTURAL PATTERNS you must follow

**FAILURE TO READ THESE DOCUMENTS FIRST WILL RESULT IN:**
- Line fixing instead of architectural solutions
- Violation of domain boundaries
- Business logic in wrong layers
- Primitive obsession instead of proper domain objects
- Missing domain services
- 500+ compilation errors from architectural violations

**YOUR NORTH STAR IS THE ARCHITECTURE, NOT THE COMPILER ERRORS!**

**EVERY CHECKPOINT MUST START WITH: "I have read the PRD, EDD, and Architecture documents"**

## Overview

This document establishes a MANDATORY checkpoint process that ALL agents (Claude Code, GitHub Copilot, or any future AI assistants) MUST follow when working on this codebase. Failure to follow this process has historically led to 700+ compilation errors and severe architectural violations.

## The Problem We're Solving

Even when agents KNOW the mandatory standards, they can drift from them during extended coding sessions. Real examples:
- 73+ methods missing LogMethodEntry/Exit discovered after "completion"
- 488 duplicate type definitions created in violation of DDD principles
- 714 compilation errors accumulated from ignoring build status
- Architectural boundaries violated by creating types in wrong layers

## The Checkpoint Process

### 1. Frequency Requirements

**MANDATORY**: Run checkpoint after EVERY:
- 10 fixes/changes (use fix-counter.sh)
- 30 minutes of coding (whichever comes first)
- Major architectural change
- Before ANY commit
- When switching between different parts of the codebase

### 2. Checkpoint Tools

#### A. Standards Checkpoint Script
```bash
# Location: /scripts/standards-checkpoint.sh
./scripts/standards-checkpoint.sh

# This script automatically checks:
# - Canonical pattern compliance
# - Financial precision (decimal usage)
# - Error handling patterns
# - Build status and progress
# - Architectural boundaries
# - Null safety compliance
```

#### B. Fix Counter Script
```bash
# Location: /scripts/fix-counter.sh
./scripts/fix-counter.sh

# Increments counter and alerts at threshold
# Automatically reminds you when checkpoint is due
```

### 3. Checkpoint Actions

When checkpoint threshold is reached, you MUST:

1. **STOP all coding immediately**
2. **Run the checkpoint script**
3. **Review all warnings and violations**
4. **Fix any critical violations before proceeding**
5. **Document any deferred fixes in TODO list**
6. **Reset counter and continue**

### 4. Critical Standards to Verify

#### A. Every Service MUST:
```csharp
public class MyService : CanonicalServiceBase, IMyService  // ✅ EXTENDS BASE
{
    public MyService(ITradingLogger logger) 
        : base(logger, "MyService")
    {
        // Constructor
    }
}
```

#### B. Every Method MUST:
```csharp
public async Task<TradingResult<T>> MethodName(params)
{
    LogMethodEntry();  // ✅ FIRST LINE
    try
    {
        // Implementation
        
        LogMethodExit();  // ✅ BEFORE RETURN
        return TradingResult<T>.Success(result);
    }
    catch (Exception ex)
    {
        LogError("Description", ex);
        LogMethodExit();  // ✅ EVEN IN CATCH
        return TradingResult<T>.Failure("ERROR_CODE", "Message", ex);
    }
}
```

#### C. Financial Calculations MUST:
```csharp
decimal price = 100.50m;      // ✅ ALWAYS decimal
// float price = 100.50f;     // ❌ NEVER float
// double price = 100.50;     // ❌ NEVER double
```

#### D. Build Status MUST:
- 0 Errors before moving to next task
- 0 Warnings before marking complete
- Run `dotnet build` after EVERY significant change

## Checkpoint Response Matrix

Based on checkpoint results, take these actions:

| Violation Type | Severity | Required Action |
|---------------|----------|-----------------|
| Services not extending CanonicalServiceBase | CRITICAL | Fix immediately |
| Missing LogMethodEntry/Exit | CRITICAL | Fix before next change |
| Float/double for money | CRITICAL | Fix immediately |
| Build errors increasing | CRITICAL | Stop and fix |
| Duplicate types | HIGH | Add to TODO, fix in batch |
| Wrong error code format | MEDIUM | Fix in next batch |
| Null reference warnings | MEDIUM | Fix systematically |

## Integration with Development Workflow

### Before Starting Any Task:
```bash
# 1. Check current state
./scripts/standards-checkpoint.sh

# 2. Note baseline metrics
echo "Starting task: <description>"
echo "Baseline errors: $(dotnet build 2>&1 | grep 'error CS' | wc -l)"

# 3. Reset fix counter
echo "0" > /tmp/marketanalyzer-fix-counter.txt
```

### During Development:
```bash
# After each fix
./scripts/fix-counter.sh

# When prompted or every 30 minutes
./scripts/standards-checkpoint.sh
```

### Before Committing:
```bash
# MANDATORY final checkpoint
./scripts/standards-checkpoint.sh

# Verify clean build
dotnet build

# Only commit if:
# - 0 errors
# - 0 warnings  
# - All checkpoints passed
```

## Consequences of Skipping Checkpoints

**HISTORICAL EVIDENCE** of what happens without checkpoints:
1. 714 compilation errors accumulated
2. Duplicate types created across layers
3. Systematic architectural violations
4. Days of cleanup work required
5. Technical debt becomes insurmountable

## Checkpoint Accountability

When working on this codebase, you MUST:
1. Acknowledge reading this document
2. Confirm checkpoint process understanding
3. Report checkpoint results in responses
4. Never skip checkpoints for "speed"

## Example Usage in Practice

```markdown
## Development Session Example

Starting new session...
Running initial checkpoint...
✅ Baseline: 680 errors, 0 warnings

Fixing PositionSize constructor errors...
Fix #1 ✅ - Updated to use Builder pattern
Fix #2 ✅ - Removed duplicate type
...
Fix #25 ✅ - Fixed null reference

🚨 CHECKPOINT THRESHOLD REACHED!
Running standards checkpoint...

Results:
- Services missing base class: 18 (unchanged) ✅
- New build errors: 655 (25 fixed) ✅
- No new architectural violations ✅

Continuing with next batch...
```

## Updates and Maintenance

This process is MANDATORY and can only be updated by:
1. Project architect/lead
2. After team consensus
3. With documented justification
4. Version controlled with clear changelog

## Remember

> "The codebase doesn't care about your good intentions. It only cares about what you actually implement."

**QUALITY OVER SPEED - ALWAYS!**

---
*This document is part of mandatory development standards. All agents must acknowledge and follow.*