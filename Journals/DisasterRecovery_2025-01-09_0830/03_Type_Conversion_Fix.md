# Type Conversion Fix
**Date**: 2025-01-09 09:15 AM
**Agent**: tradingagent
**Fix Count**: 1/10

## Problem Analysis
The Application layer services are importing:
```csharp
using MarketAnalyzer.Domain.PortfolioManagement.ValueObjects;
```

This namespace previously contained a PositionSize type that was removed. The comment in BacktestingTypes.cs confirms:
```
// PositionSize type removed - use MarketAnalyzer.Foundation.Trading.PositionSize instead
```

## Root Cause
When PositionSize was removed from Domain layer, the using statements weren't updated. This causes:
1. Type ambiguity between Domain and Foundation types
2. Conversion errors between lists of different PositionSize types
3. Compiler confusion about which type to use

## Fix Strategy
Need to ensure all Application services use ONLY the Foundation.Trading.PositionSize type.

## Action
Reviewing all Application layer files that import Domain ValueObjects to determine what types they actually need.

---
**Status**: Analysis complete, ready to implement fix