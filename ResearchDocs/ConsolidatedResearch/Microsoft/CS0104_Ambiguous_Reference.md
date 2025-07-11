# CS0104: 'type' is an ambiguous reference between 'namespace1.type' and 'namespace2.type'

**Source**: Microsoft C# Compiler Error Reference  
**Research Date**: January 9, 2025  
**Context**: MarketAnalyzer Error Resolution - CS0104 Pattern Analysis  
**IMMEDIATE SAVE**: Per mandatory documentation routine  

## Error Message

**CS0104**: "'type' is an ambiguous reference between 'namespace1.type' and 'namespace2.type'"

## Root Causes (C# Language Specification)

1. **Multiple Types Same Name**: Two or more types with identical names exist in different namespaces
2. **Using Directives Conflict**: Multiple using directives bring types with same name into scope
3. **Nested Type Conflicts**: Inner and outer types or imported types have same name
4. **Assembly Reference Conflicts**: Multiple referenced assemblies contain types with same name

## Microsoft Recommended Solutions

### Solution 1: Use Fully Qualified Names
```csharp
// ❌ BEFORE: CS0104 error
using MarketAnalyzer.Domain.Entities;
using MarketAnalyzer.Domain.PortfolioManagement.ValueObjects;

public void ProcessSignal(Signal signal) // Ambiguous!
{
    // ...
}

// ✅ AFTER: Fully qualified name
public void ProcessSignal(MarketAnalyzer.Domain.PortfolioManagement.ValueObjects.Signal signal)
{
    // ...
}
```

### Solution 2: Using Alias Directives
```csharp
// ❌ BEFORE: CS0104 error  
using MarketAnalyzer.Domain.Entities;
using MarketAnalyzer.Domain.PortfolioManagement.ValueObjects;

// ✅ AFTER: Alias to disambiguate
using EntitySignal = MarketAnalyzer.Domain.Entities.Signal;
using PortfolioSignal = MarketAnalyzer.Domain.PortfolioManagement.ValueObjects.Signal;

public void ProcessSignal(PortfolioSignal signal) // Clear!
{
    // ...
}
```

### Solution 3: Remove Unnecessary Using Directives
```csharp
// ❌ BEFORE: CS0104 error
using MarketAnalyzer.Domain.Entities; // Contains Signal
using MarketAnalyzer.Domain.PortfolioManagement.ValueObjects; // Also contains Signal

// ✅ AFTER: Remove unused namespace
using MarketAnalyzer.Domain.PortfolioManagement.ValueObjects; // Only use what's needed
```

### Solution 4: Rename One of the Types
```csharp
// ❌ BEFORE: Two types named Signal
namespace MarketAnalyzer.Domain.Entities { public class Signal { } }
namespace MarketAnalyzer.Domain.PortfolioManagement.ValueObjects { public class Signal { } }

// ✅ AFTER: Rename to be more specific
namespace MarketAnalyzer.Domain.Entities { public class MarketSignal { } }
namespace MarketAnalyzer.Domain.PortfolioManagement.ValueObjects { public class TradingSignal { } }
```

## MarketAnalyzer-Specific Context

### Current Error Pattern
**Files Affected**: 
- IPositionSizingService.cs - 40 occurrences
- RiskAdjustedSignalService.cs - Multiple occurrences

**Conflicting Types**:
1. **MarketAnalyzer.Domain.Entities.Signal** - General domain entity
2. **MarketAnalyzer.Domain.PortfolioManagement.ValueObjects.Signal** - Portfolio-specific value object

### Architectural Analysis

The conflict represents a domain modeling issue:
- **Entities.Signal**: Likely represents market signals (price movements, indicators)
- **ValueObjects.Signal**: Likely represents trading signals (buy/sell decisions)

These are semantically different concepts that should have distinct names.

## Resolution Strategy

### Recommended Approach: Solution 2 - Using Aliases

For Application layer services dealing with portfolio management:
```csharp
using MarketAnalyzer.Domain.PortfolioManagement.Aggregates;
using MarketAnalyzer.Domain.PortfolioManagement.ValueObjects;
using TradingSignal = MarketAnalyzer.Domain.PortfolioManagement.ValueObjects.Signal;
```

This approach:
- Maintains clarity about which Signal type is used
- Doesn't require changing all method signatures
- Makes the code self-documenting
- Follows DDD bounded context principles

### Alternative: Domain Refactoring

Consider renaming for semantic clarity:
- `Domain.Entities.Signal` → `Domain.Entities.MarketSignal`
- `Domain.PortfolioManagement.ValueObjects.Signal` → `Domain.PortfolioManagement.ValueObjects.TradingSignal`

## Applied Solution Strategy

**Using Alias Approach for Application Layer**

Since the Application.PortfolioManagement services primarily work with portfolio-specific signals, we'll use targeted aliases to maintain clarity while minimizing changes.

## Status

- ✅ **Microsoft Research**: CS0104 patterns identified and documented
- ✅ **Codebase Analysis**: Conflict between Entity and ValueObject Signal types
- ⏳ **Solution Application**: Ready to apply using alias pattern
- ⏳ **Architectural Validation**: Consider future refactoring for semantic clarity

**NEXT ACTIONS**: Apply using alias pattern to affected files