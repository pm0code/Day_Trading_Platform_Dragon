# Type Resolution Strategy
**Date**: 2025-01-09 09:30 AM
**Agent**: tradingagent
**Fix Count**: 1/10

## The Real Problem

The Domain layer's PositionSize was removed (as confirmed by BacktestingTypes.cs line 390), but:
1. Application services still have `using MarketAnalyzer.Domain.PortfolioManagement.ValueObjects;`
2. The compiler is trying to resolve PositionSize from this namespace
3. This creates type conversion errors between non-existent Domain.PositionSize and Foundation.PositionSize

## Evidence
- BacktestingEngineService line 199: Passing `sizingResult.Value` (List<PositionSize>)
- ExecuteTradesAsync expects: `List<Foundation.Trading.PositionSize>`
- Error CS1503: Cannot convert between the two list types

## Solution Strategy

### Option 1: Remove Domain ValueObjects using (if not needed)
- Check what types are actually used from Domain.ValueObjects
- If only backtesting-specific types, keep the using
- If nothing critical, remove it

### Option 2: Explicit type qualification
- Use fully qualified names for PositionSize
- `Foundation.Trading.PositionSize` everywhere

### Option 3: Using alias
```csharp
using PositionSize = MarketAnalyzer.Foundation.Trading.PositionSize;
```

## Decision
Going with Option 2 - explicit qualification, as it's already partially done in the codebase.

---
**Next Action**: Check all Application services for PositionSize usage patterns