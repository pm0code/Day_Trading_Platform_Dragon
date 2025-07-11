# CS1929: Extension method type mismatch - 'Type' does not contain a definition for 'Method'

**Source**: Microsoft Official Documentation  
**URL**: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs1929  
**Date Created**: January 9, 2025  
**Context**: MarketAnalyzer Error Resolution - CS1929 Pattern Analysis  
**IMMEDIATE SAVE**: Per mandatory documentation routine

## Official Microsoft Definition

**Error Message**: "'type1' does not contain a definition for 'name' and the best extension method overload 'name(type2)' requires a receiver of type 'type2'"

## Root Causes (Microsoft Documentation)

1. **Type Mismatch**: Extension method expects different type than provided
2. **Missing Conversion**: Implicit conversion between types not available
3. **Generic Constraint**: Extension method has generic constraints not satisfied
4. **Namespace Missing**: Extension method not in scope (missing using directive)
5. **Wrong Type Arguments**: Generic extension method called with wrong type parameters

## Microsoft Recommended Solutions

### Solution 1: Type Conversion
```csharp
// ❌ WRONG: decimal[] calling double extension
decimal[] values = {1.0m, 2.0m, 3.0m};
var mean = values.Mean(); // CS1929 - Mean(IEnumerable<double>)

// ✅ CORRECT: Convert to expected type
var mean = values.Select(x => (double)x).Mean();
```

### Solution 2: Use Compatible Method
```csharp
// ✅ Find method that accepts your type
var mean = values.Cast<double>().Mean();
// OR
var mean = values.ToArray().Select(x => (double)x).Average();
```

### Solution 3: Create Type-Specific Extension
```csharp
public static class DecimalExtensions
{
    public static decimal Mean(this IEnumerable<decimal> values)
    {
        return values.Average();
    }
}
```

### Solution 4: Add Missing Using Directive
```csharp
using System.Linq; // For LINQ extension methods
using MathNet.Numerics.Statistics; // For statistics extensions
```

## MarketAnalyzer-Specific Context

### Current Error Location
**File**: WalkForwardDomainService.cs  
**Lines**: 340, 341, 342  
**Context**: Math.NET Statistics extensions expecting `double[]` but receiving `decimal[]`

### Error Pattern
```csharp
// ❌ CURRENT ERROR: CS1929
var inSampleMean = inSampleReturns.Mean(); // decimal[] calling Mean(IEnumerable<double>)
var outSampleMean = outOfSampleReturns.Mean(); // decimal[] calling Mean(IEnumerable<double>)
var stdDev = inSampleReturns.StandardDeviation(); // decimal[] calling StandardDeviation(IEnumerable<double>)
```

### Root Cause Analysis
- **Math.NET Statistics**: Extension methods designed for `double` precision
- **Financial Data**: Uses `decimal` for precision compliance  
- **Type Mismatch**: Need conversion between `decimal[]` and `IEnumerable<double>`

## Financial Domain Considerations

- **Precision vs Performance**: `decimal` (precision) vs `double` (performance)
- **Statistical Accuracy**: Math.NET optimized for `double` calculations
- **Financial Compliance**: Monetary calculations must use `decimal`
- **Conversion Strategy**: Safe conversion for statistical operations

## Pending Solution Strategy

**NEXT STEPS** (to be applied):
1. **Use Solution #1**: Convert `decimal[]` to `double` for statistics
2. **Maintain Precision**: Convert back to `decimal` for financial results
3. **Validate Math.NET**: Ensure using directive present
4. **Financial Safety**: Document precision implications

## Related Error Patterns

- **CS1061**: Type does not contain definition for member
- **CS1929**: Extension method type mismatch (this error)
- **CS0246**: Type or namespace not found

## Status

- ✅ **Microsoft Documentation**: Retrieved and documented immediately
- ⏳ **Codebase Investigation**: Pending - examine Math.NET usage patterns
- ⏳ **Solution Application**: Pending - apply type conversion
- ⏳ **Validation**: Pending - verify statistical accuracy maintained

**ROUTINE COMPLIANCE**: ✅ Documentation created immediately upon Microsoft lookup