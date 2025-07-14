# AIRES Known Issues and Workarounds

**Version**: 3.0  
**Last Updated**: 2025-01-13  
**Author**: tradingagent  
**Status**: Active Issue Tracking

## Table of Contents

1. [Critical Issues](#critical-issues)
2. [High Priority Issues](#high-priority-issues)
3. [Medium Priority Issues](#medium-priority-issues)
4. [Low Priority Issues](#low-priority-issues)
5. [Resolved Issues](#resolved-issues)
6. [Issue Tracking](#issue-tracking)

## Critical Issues

### ISSUE-001: ILogger<T> Usage Violates Standards

**Status**: 🔴 OPEN  
**Severity**: CRITICAL  
**Component**: All Services  
**Reported**: 2025-01-13  

**Description**:
All AIRES services use `ILogger<T>` instead of the mandatory `ITradingLogger` interface, violating MANDATORY_DEVELOPMENT_STANDARDS-V4.md Section 4.1.

**Impact**:
- Non-compliance with architectural standards
- Inconsistent logging patterns
- Integration issues with other MarketAnalyzer components

**Root Cause**:
Initial development used standard .NET logging before canonical patterns were established.

**Workaround**:
Continue using current logging - functional but non-compliant.

**Permanent Fix**:
```csharp
// Replace all instances
public MyService(ILogger<MyService> logger) // WRONG
public MyService(ITradingLogger logger)     // CORRECT
```

**Effort**: 16 hours  
**Files Affected**: 59 service files

---

### ISSUE-002: Mixed Base Class Usage

**Status**: 🔴 OPEN  
**Severity**: CRITICAL  
**Component**: Domain and Infrastructure Services  
**Reported**: 2025-01-13  

**Description**:
Services inconsistently use `CanonicalServiceBase` (trading) and `CanonicalToolServiceBase` (DevTools), causing architectural confusion.

**Impact**:
- Incorrect patterns applied
- Result type mismatches
- Maintenance complexity

**Examples**:
```csharp
// WRONG - Trading base in DevTools
public class ErrorParser : CanonicalServiceBase

// CORRECT - DevTools base
public class ErrorParser : CanonicalToolServiceBase
```

**Workaround**:
Current mixed usage works but violates standards.

**Permanent Fix**:
Migrate all AIRES services to `CanonicalToolServiceBase`.

**Effort**: 12 hours  
**Files Affected**: 15 service files

## High Priority Issues

### ISSUE-003: FileSystemWatcher Unreliable on WSL

**Status**: 🟡 OPEN (Mitigated)  
**Severity**: HIGH  
**Component**: AutonomousErrorResolutionService  
**Reported**: 2025-01-12  

**Description**:
FileSystemWatcher doesn't reliably detect file changes on WSL-mounted Windows drives.

**Impact**:
- Files may not be processed immediately
- Delays in booklet generation

**Root Cause**:
Known WSL2 limitation with Windows filesystem events.

**Current Workaround**:
```csharp
// Polling fallback implemented
private async Task PollingFallbackAsync()
{
    while (!_cancellationToken.IsCancellationRequested)
    {
        var files = Directory.GetFiles(_inputDirectory, "*.txt");
        // Process any new files
        await Task.Delay(TimeSpan.FromSeconds(10));
    }
}
```

**Permanent Fix Options**:
1. Run AIRES on native Windows
2. Use Linux filesystem exclusively
3. Implement inotify-based watcher

**Effort**: Already mitigated

---

### ISSUE-004: AIResearchOrchestratorService Missing Base Class

**Status**: 🔴 OPEN  
**Severity**: HIGH  
**Component**: AIResearchOrchestratorService  
**Reported**: 2025-01-13  

**Description**:
Critical domain service doesn't extend any canonical base class, missing all standard patterns.

**Impact**:
- No standardized logging
- No health checks
- No lifecycle management

**Code**:
```csharp
// CURRENT - No base class
public class AIResearchOrchestratorService
{
    private readonly ILogger<AIResearchOrchestratorService> _logger;
}

// REQUIRED
public class AIResearchOrchestratorService : CanonicalToolServiceBase
{
    public AIResearchOrchestratorService(ITradingLogger logger)
        : base(logger, nameof(AIResearchOrchestratorService))
}
```

**Workaround**:
Service functional but non-compliant.

**Permanent Fix**:
Complete refactor to use canonical patterns.

**Effort**: 4 hours  
**Dependencies**: Must fix logger issue first

---

### ISSUE-005: Result Type Inconsistency

**Status**: 🔴 OPEN  
**Severity**: HIGH  
**Component**: All Public APIs  
**Reported**: 2025-01-13  

**Description**:
Mixed use of `TradingResult<T>` (trading domain) and `ToolResult<T>` (DevTools domain).

**Impact**:
- API inconsistency
- Integration complexity
- Type conversion overhead

**Examples**:
```csharp
// Found in DevTools context (WRONG)
public async Task<TradingResult<ErrorBatch>> ParseAsync()

// Should be
public async Task<ToolResult<ErrorBatch>> ParseAsync()
```

**Workaround**:
Both types functionally similar, system works.

**Permanent Fix**:
Standardize all AIRES APIs on `ToolResult<T>`.

**Effort**: 8 hours  
**Files Affected**: 28 files with public methods

## Medium Priority Issues

### ISSUE-006: Kafka Partition Rebalancing Noise

**Status**: 🟡 OPEN  
**Severity**: MEDIUM  
**Component**: Kafka Infrastructure  
**Reported**: 2025-01-11  

**Description**:
Frequent partition rebalancing causing log noise and minor processing delays.

**Symptoms**:
```
Partitions revoked: ai-input-errors[[0]], ai-input-errors[[1]]
Partitions assigned: ai-input-errors[[2]]
```

**Impact**:
- Log file bloat
- 1-2 second processing delays during rebalance

**Root Cause**:
Multiple consumer instances with aggressive heartbeat settings.

**Workaround**:
Adjust consumer configuration:
```json
{
  "Kafka": {
    "Consumer": {
      "SessionTimeoutMs": 60000,
      "HeartbeatIntervalMs": 20000
    }
  }
}
```

**Permanent Fix**:
Implement stable consumer group management.

**Effort**: 4 hours

---

### ISSUE-007: Large File Memory Usage

**Status**: 🟡 OPEN  
**Severity**: MEDIUM  
**Component**: FileProcessor  
**Reported**: 2025-01-10  

**Description**:
Processing build output files > 10MB causes high memory usage and potential OOM.

**Impact**:
- Large projects may fail processing
- System instability with multiple large files

**Current Mitigation**:
```csharp
// File size check
if (new FileInfo(filePath).Length > 10 * 1024 * 1024)
{
    return ToolResult.Failure("File too large", "FILE_SIZE_EXCEEDED");
}
```

**Permanent Fix Options**:
1. Implement streaming parser
2. Process files in chunks
3. Use memory-mapped files

**Effort**: 8 hours

---

### ISSUE-008: No Test Coverage

**Status**: 🔴 OPEN  
**Severity**: MEDIUM  
**Component**: Entire System  
**Reported**: 2025-01-13  

**Description**:
Minimal to no unit tests exist for AIRES components.

**Impact**:
- Refactoring risk
- No regression protection
- Quality concerns

**Current State**:
- Test project exists but empty
- No integration tests
- No performance tests

**Required Coverage**:
- Unit tests: 80% minimum
- Integration tests: Critical paths
- Performance tests: AI pipeline

**Effort**: 16-24 hours

## Low Priority Issues

### ISSUE-009: Gemini API Rate Limit Handling

**Status**: 🟢 OPEN (Handled)  
**Severity**: LOW  
**Component**: Gemini Integration  
**Reported**: 2025-01-09  

**Description**:
Occasional 429 rate limit errors from Gemini API.

**Impact**:
- Delayed booklet generation (retry succeeds)

**Current Handling**:
```csharp
// Polly retry policy
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(
        3,
        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (outcome, timespan, retryCount, context) =>
        {
            logger.LogWarning($"Retry {retryCount} after {timespan}");
        }));
```

**Enhancement**:
Implement token bucket rate limiter.

**Effort**: 4 hours

---

### ISSUE-010: Booklet Naming Length

**Status**: 🟢 OPEN  
**Severity**: LOW  
**Component**: BookletGenerator  
**Reported**: 2025-01-08  

**Description**:
Generated booklet filenames can exceed Windows path limits.

**Example**:
```
CS0117_Type_MarketAnalyzerTradingPlatformDataIngestionServicesAlphaVantageDataProvider_does_not_contain_a_definition_for_GetHistoricalDataAsync_20250113_143022.md
```

**Impact**:
- File creation fails on long error descriptions
- Windows path limit (260 chars) exceeded

**Workaround**:
```csharp
// Truncate filename
var safeFileName = fileName.Length > 200 
    ? fileName.Substring(0, 200) + "_truncated" 
    : fileName;
```

**Permanent Fix**:
Implement intelligent filename shortening.

**Effort**: 2 hours

## Resolved Issues

### ISSUE-R001: Database Connection Timeout

**Status**: ✅ RESOLVED  
**Severity**: HIGH  
**Component**: PostgreSQL Integration  
**Reported**: 2025-01-11  
**Resolved**: 2025-01-12  

**Description**:
Database operations timing out during startup migrations.

**Resolution**:
Increased command timeout and added connection resilience:
```csharp
options.UseNpgsql(connectionString, npgsqlOptions =>
{
    npgsqlOptions.CommandTimeout(60);
    npgsqlOptions.EnableRetryOnFailure(3);
});
```

---

### ISSUE-R002: Ollama Model Not Found

**Status**: ✅ RESOLVED  
**Severity**: CRITICAL  
**Component**: Mistral Integration  
**Reported**: 2025-01-10  
**Resolved**: 2025-01-10  

**Description**:
Mistral model not available in Ollama.

**Resolution**:
Added startup check and auto-pull:
```bash
ollama pull mistral:7b-instruct-q4_K_M
```

## Issue Tracking

### Reporting New Issues

Use this template:
```markdown
### ISSUE-XXX: [Brief Description]

**Status**: 🔴 OPEN  
**Severity**: CRITICAL|HIGH|MEDIUM|LOW  
**Component**: [Affected Component]  
**Reported**: [Date]  

**Description**:
[Detailed description]

**Impact**:
- [Impact 1]
- [Impact 2]

**Root Cause**:
[If known]

**Workaround**:
[Temporary solution if any]

**Permanent Fix**:
[Proposed solution]

**Effort**: [Estimated hours]  
**Files Affected**: [Count or list]
```

### Issue Priority Matrix

| Severity | Compliance Impact | Operational Impact | Priority |
|----------|------------------|-------------------|----------|
| CRITICAL | Violates standards | System broken | P0 - Immediate |
| HIGH | Major violation | Major feature broken | P1 - This week |
| MEDIUM | Minor violation | Feature degraded | P2 - This month |
| LOW | Best practice | Minor inconvenience | P3 - Backlog |

### Tracking Metrics

- **Open Issues**: 10 (2 Critical, 3 High, 3 Medium, 2 Low)
- **Resolved This Month**: 2
- **Average Resolution Time**: 1.5 days
- **Compliance Debt**: 40-60 hours

---

**Next**: [Roadmap](AIRES_Roadmap.md)