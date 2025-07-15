# GlobalSuppressions.cs Removal Plan

**Date**: January 14, 2025  
**Status**: Technical Debt Documentation  
**Priority**: Low

## Overview

The AIRES project currently has 11 GlobalSuppressions.cs files containing identical suppressions for over 60 different warnings. These suppressions were added to enable `TreatWarningsAsErrors` while allowing the build to succeed. Each suppression represents technical debt that should be systematically addressed.

## Current State

### Files with Suppressions
1. `/AIRES/GlobalSuppressions.cs` (root)
2. `/src/AIRES.Application/GlobalSuppressions.cs`
3. `/src/AIRES.CLI/GlobalSuppressions.cs`
4. `/src/AIRES.Core/GlobalSuppressions.cs`
5. `/src/AIRES.Foundation/GlobalSuppressions.cs`
6. `/src/AIRES.Infrastructure/GlobalSuppressions.cs`
7. `/src/AIRES.Watchdog/GlobalSuppressions.cs`
8. `/tests/AIRES.Application.Tests/GlobalSuppressions.cs`
9. `/tests/AIRES.Core.Tests/GlobalSuppressions.cs`
10. `/tests/AIRES.Foundation.Tests/GlobalSuppressions.cs`
11. `/tests/AIRES.Integration.Tests/GlobalSuppressions.cs`

### Categories of Suppressions

#### 1. Documentation (High Priority)
- CS1591: Missing XML comment for publicly visible type or member
- SA1600-SA1602, SA1611, SA1615, SA1618, SA1623, SA1629, SA1642: Various StyleCop documentation rules
- **Impact**: Poor API documentation, reduced IntelliSense support
- **Effort**: Medium - requires adding XML comments throughout codebase

#### 2. Code Organization (Medium Priority)
- SA1200, SA1201, SA1202, SA1203, SA1208, SA1210: Ordering rules
- SA1402: File may only contain a single type
- SA1649: File name should match first type name
- **Impact**: Code maintainability and readability
- **Effort**: Low - mostly automated fixes

#### 3. Code Style (Low Priority)
- SA1101: PrefixLocalCallsWithThis
- SA1309, SA1310: Field naming conventions
- SA1028, SA1513, SA1515, SA1516, SA1518: Spacing and formatting
- SA1124: DoNotUseRegions
- **Impact**: Code consistency
- **Effort**: Low - can be automated with code cleanup

#### 4. Performance (Medium Priority)
- CA1822: Mark members as static
- CA1840: Use Environment.CurrentManagedThreadId
- CA1845: Use span-based string.Concat
- CA1852: Seal internal types
- CA1859: Use concrete types when possible
- CA1860: Avoid using 'Enumerable.Any()' extension
- **Impact**: Runtime performance
- **Effort**: Medium - requires careful analysis

#### 5. Design & Best Practices (High Priority)
- CA1031: Do not catch general exception types
- CA1062: Validate arguments of public methods
- CA1510: Use ArgumentNullException throw helper
- CA1000: Do not declare static members on generic types
- **Impact**: Code robustness and maintainability
- **Effort**: High - requires design decisions

#### 6. Globalization (Low Priority)
- CA1304, CA1305, CA1310, CA1311: Culture and string comparison
- **Impact**: Internationalization support
- **Effort**: Low - add culture specifications

#### 7. NuGet Dependencies (High Priority)
- NU1608: MediatR version conflict
- **Impact**: Build stability
- **Effort**: Low - update package references

## Removal Strategy

### Phase 1: Critical Issues (1-2 days)
1. **Fix NuGet Dependencies**
   - Update MediatR.Extensions.Microsoft.DependencyInjection to version 12.x
   - Resolve any package conflicts

2. **Add Essential Documentation**
   - Document all public interfaces and their members
   - Document critical service classes
   - Use AI assistance to generate initial documentation

### Phase 2: Design Issues (3-5 days)
1. **Exception Handling**
   - Replace catch(Exception) with specific exception types
   - Add proper exception documentation

2. **Argument Validation**
   - Add null checks to all public methods
   - Use ArgumentNullException.ThrowIfNull() helper

3. **Performance Improvements**
   - Mark appropriate members as static
   - Seal internal types where applicable

### Phase 3: Code Organization (2-3 days)
1. **File Organization**
   - Split files with multiple types
   - Rename files to match type names
   - Remove regions

2. **Using Directives**
   - Order System directives first
   - Alphabetize by namespace
   - Move inside namespace where appropriate

### Phase 4: Style Compliance (1-2 days)
1. **Automated Fixes**
   - Run code cleanup for spacing issues
   - Add 'this.' prefix where required
   - Fix line endings and whitespace

2. **Manual Style Fixes**
   - Add braces to single-line statements
   - Fix parameter formatting
   - Add trailing commas in initializers

### Phase 5: Final Polish (1 day)
1. **Globalization**
   - Add StringComparison.Ordinal where appropriate
   - Specify CultureInfo.InvariantCulture for formatting

2. **Remaining Documentation**
   - Document remaining internal members
   - Add parameter and return value documentation
   - Ensure all documentation ends with periods

## Implementation Approach

### For Each Suppression Type:
1. Remove the suppression from all GlobalSuppressions.cs files
2. Build to identify all locations with warnings
3. Fix warnings systematically by project:
   - Start with Core (foundation)
   - Then Foundation
   - Then Infrastructure
   - Then Application
   - Finally CLI and Watchdog
4. Run tests after each project to ensure no regressions
5. Commit after each suppression type is fully resolved

### Tools to Use:
- Visual Studio Code's "Quick Fix" for automated corrections
- .editorconfig for enforcing style rules
- dotnet format for bulk formatting fixes
- Custom Roslyn analyzers for project-specific rules

## Success Criteria

1. All GlobalSuppressions.cs files deleted
2. Zero build warnings with TreatWarningsAsErrors enabled
3. All public APIs have XML documentation
4. Code follows consistent style guidelines
5. All tests continue to pass

## Estimated Timeline

- **Total Effort**: 8-13 days of focused work
- **Recommended Approach**: Address in sprints during downtime
- **Priority**: Low - functional code takes precedence

## Notes

- Some suppressions (e.g., CA2007 ConfigureAwait) may be legitimate and should be moved to project-specific .editorconfig rules instead
- Consider using Directory.Build.props to set common analyzer rules
- Document any suppressions that are kept with clear justification
- Create automated tests to prevent regression of fixed issues

## Current Technical Debt Impact

While these suppressions allow the code to build, they represent:
- Reduced code quality and maintainability
- Missing API documentation
- Potential performance issues
- Inconsistent code style
- Risk of catching overly broad exceptions

The systematic removal of these suppressions will improve overall code quality and make the codebase more maintainable for future development.