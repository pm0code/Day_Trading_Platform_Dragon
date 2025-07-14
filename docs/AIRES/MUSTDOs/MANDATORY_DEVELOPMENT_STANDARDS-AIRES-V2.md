# 🚨 AIRES MANDATORY DEVELOPMENT STANDARDS V2 🚨

**THIS DOCUMENT SUPERSEDES ALL OTHER GUIDANCE AND MUST BE FOLLOWED WITHOUT EXCEPTION**

Last Updated: 2025-01-13  
Version: 2.0  
System: AI Error Resolution System (AIRES)

## 🔴 CRITICAL REVISION: AIRES is a Development Tool

**FUNDAMENTAL CORRECTION**: AIRES is an INDEPENDENT Development Tool that must follow DevTools standards, NOT Trading domain standards.

### What Changed in V2:
1. **Logger Interface**: Use `ILogger` (not `ILogger<T>`, not `ITradingLogger`)
2. **Base Classes**: Use `CanonicalToolServiceBase` (not `CanonicalServiceBase`)
3. **Result Types**: Use `ToolResult<T>` (not `TradingResult<T>`)
4. **Complete Independence**: AIRES must NEVER reference Trading domain code

## 🔴 CRITICAL: READ THIS FIRST

This document establishes ALL mandatory development standards for the AI Error Resolution System (AIRES). Every developer, including AI assistants, MUST read and follow these standards. Violations will result in code rejection.

**AIRES MISSION**: Autonomous error analysis and research booklet generation through 6-stage AI pipeline (Mistral → DeepSeek → CodeGemma → Gemma2 → BookletGenerator → Archive)

**AIRES IDENTITY**: Independent Development Tool in the DevTools ecosystem

## 🚨 CRITICAL ADDITION: MANDATORY BOOKLET-FIRST DEVELOPMENT PROTOCOL

### 0.1 ABSOLUTE REQUIREMENT: Self-Referential AI Error Resolution

**MANDATORY**: NEVER fix any bug, error, or issue in AIRES without first generating a research booklet through AIRES itself.

```bash
# MANDATORY WORKFLOW FOR ALL AIRES FIXES:
1. Capture error output to file in AIRES input directory
2. Let AIRES process through its own AI pipeline
3. WAIT for complete analysis (Mistral, DeepSeek, CodeGemma, Gemma2)
4. Review generated booklet in docs/error-booklets/[DATE]/
5. Understand root cause from AIRES research
6. ONLY THEN apply fix based on booklet guidance
```

**AIRES INPUT DIRECTORY**: `/mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/AI_Codebase_Watchdog_System/input`  
**AIRES OUTPUT DIRECTORY**: `/mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/MarketAnalyzer/docs/error-booklets/`

**VIOLATIONS WILL RESULT IN**:
- Incomplete understanding of AIRES internal issues
- Superficial "quick fixes" that miss root causes
- Breakdown of AIRES autonomous operation
- Violation of self-referential engineering principles

**NO EXCEPTIONS** - Even for "simple" or "obvious" AIRES fixes!

### 0.2 Status Checkpoint Review (SCR) Protocol

**MANDATORY**: Implement fix counter system with checkpoint reviews every 10 fixes.

#### Fix Counter Protocol:
1. **Initialize**: Start each session with Fix Counter: [0/10]
2. **Track**: Increment counter with EVERY fix applied
3. **Report**: Show counter in EVERY response: 📊 Fix Counter: [X/10]
4. **Checkpoint**: At 10 fixes, perform mandatory Status Checkpoint Review (SCR)
5. **Reset**: After checkpoint, reset counter to [0/10]

**FAILURE TO RUN CHECKPOINTS = ARCHITECTURAL DRIFT = AIRES FAILURE**

### 0.3 Gemini API Integration for AIRES Architectural Validation

**MANDATORY**: For any AIRES architectural issues, async deadlocks, or complex technical decisions:

```bash
# Gemini API Usage for AIRES:
curl "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=AIzaSyDP7daxEmHxuSTA3ZObO4Rgkl2HswqpHcs" \
  -H 'Content-Type: application/json' \
  -X POST \
  -d '{"contents":[{"parts":[{"text":"Your AIRES architectural question here"}]}]}'
```

## Table of Contents

0. [**CRITICAL**: Mandatory Booklet-First Development Protocol](#critical-mandatory-booklet-first-development-protocol)
1. [Core AIRES Development Principles](#core-aires-development-principles)
2. [Research-First Mandate](#research-first-mandate)
3. [Canonical AIRES Service Implementation](#canonical-aires-service-implementation)
4. [Method Logging Requirements](#method-logging-requirements)
5. [AI Pipeline Precision Standards](#ai-pipeline-precision-standards)
6. [Error Handling Standards](#error-handling-standards)
7. [Testing Requirements](#testing-requirements)
8. [AIRES Architecture Standards](#aires-architecture-standards)
9. [Performance Requirements](#performance-requirements)
10. [Security Standards](#security-standards)
11. [Documentation Requirements](#documentation-requirements)
12. [Code Analysis and Quality](#code-analysis-and-quality)
13. [Standard Tools and Libraries](#standard-tools-and-libraries)
14. [Development Workflow](#development-workflow)
15. [Progress Reporting](#progress-reporting)
16. [**HIGH PRIORITY**: Observability & Distributed Tracing](#high-priority-observability--distributed-tracing)

-----

## 1. Core AIRES Development Principles

### 1.1 DevTools Independence Policy

- **NEVER** reference Trading domain code in AIRES
- **ALWAYS** use DevTools Foundation patterns
- **NEVER** mix Trading and DevTools patterns
- **ALWAYS** maintain complete architectural separation

### 1.2 Zero Custom Implementation Policy

- **NEVER** create custom implementations when DevTools canonical patterns exist
- **ALWAYS** use DevTools canonical service implementations
- **NEVER** duplicate functionality that exists in DevTools canonical services
- **ALWAYS** check for existing DevTools implementations before creating new ones

### 1.3 Research-First Development

- **MANDATORY**: 2-4 hours minimum research before building anything in AIRES
- **MANDATORY**: Create research reports documenting all AIRES findings
- **MANDATORY**: Read COMPLETE AIRES documentation - no guessing allowed
- **MANDATORY**: Get approval before AIRES implementation

### 1.4 Dead Code Removal

- **MANDATORY**: Remove all dead code after successful AIRES migrations
- **NEVER** leave commented-out code in AIRES production
- **ALWAYS** use version control for AIRES code history

-----

## 2. Research-First Mandate

### 2.1 Pre-Implementation Research Requirements

```markdown
BEFORE WRITING ANY AIRES CODE:
1. ✅ Research existing AIRES solutions (2-4 hours minimum)
2. ✅ Document findings in AIRES research report
3. ✅ Identify DevTools-standard patterns
4. ✅ Get approval for AIRES approach
5. ✅ Only then begin AIRES implementation
```

### 2.2 AIRES Research Report Template

```markdown
# AIRES Research Report: [Feature/Component Name]
Date: [YYYY-MM-DD]
Researcher: [Name/AI]
System: AI Error Resolution System (AIRES)
Type: Development Tool (DevTools ecosystem)

## Executive Summary
[Brief overview of AIRES findings]

## Research Conducted
- [ ] DevTools industry standards reviewed
- [ ] Existing DevTools patterns analyzed
- [ ] Similar AI pipeline implementations studied
- [ ] AIRES performance implications considered
- [ ] AIRES security implications reviewed

## Findings
1. **Standard DevTools Solutions Found:**
   - [List all relevant DevTools standards]
   
2. **Recommended AIRES Approach:**
   - [Detailed AIRES recommendation]
   
3. **AIRES Alternatives Considered:**
   - [List alternatives and why rejected]

## Approval
- [ ] AIRES approach approved by: [Name]
- [ ] Date: [YYYY-MM-DD]
```

-----

## 3. Canonical AIRES Service Implementation

### 3.1 Mandatory DevTools Canonical Services

ALL AIRES services MUST extend the appropriate DevTools canonical base class:

```csharp
// ❌ WRONG - Direct interface implementation
public class MyAIRESService : IMyAIRESService 
{
    // This violates AIRES standards
}

// ❌ WRONG - Using Trading domain base class
public class MyAIRESService : CanonicalServiceBase, IMyAIRESService
{
    // This violates DevTools independence
}

// ✅ CORRECT - Extends DevTools canonical base
public class MyAIRESService : CanonicalToolServiceBase, IMyAIRESService
{
    public MyAIRESService(ILogger logger) 
        : base(logger, nameof(MyAIRESService))
    {
        // Constructor implementation
    }
}
```

### 3.2 Available DevTools Canonical Base Classes

- `CanonicalToolServiceBase` - For all AIRES services
- `ToolResult<T>` - For all AIRES operation results
- **NO Trading domain classes allowed**

### 3.3 DevTools Canonical Service Features

All DevTools canonical services provide:

- ✅ Automatic method entry/exit logging
- ✅ Health checks and metrics
- ✅ Proper lifecycle management (Initialize, Start, Stop)
- ✅ ToolResult<T> pattern implementation
- ✅ Comprehensive error handling
- ✅ Performance tracking
- ✅ OpenTelemetry integration

-----

## 4. Method Logging Requirements

### 4.1 MANDATORY: ILogger Implementation

**CRITICAL**: ALL AIRES services MUST use `ILogger` interface (not generic, not typed).

```csharp
// ❌ WRONG - Using generic ILogger<T> in AIRES
public class MyAIRESService
{
    private readonly ILogger<MyAIRESService> _logger; // VIOLATION!
    
    public MyAIRESService(ILogger<MyAIRESService> logger)
    {
        _logger = logger;
    }
}

// ❌ WRONG - Using Trading logger
public class MyAIRESService
{
    private readonly ITradingLogger _logger; // VIOLATION!
}

// ✅ CORRECT - Using non-generic ILogger
public class MyAIRESService : CanonicalToolServiceBase, IMyAIRESService
{
    public MyAIRESService(ILogger logger) 
        : base(logger, nameof(MyAIRESService))
    {
        // Base class provides ILogger functionality
    }
}
```

### 4.2 MANDATORY: Every AIRES Method Must Log Entry and Exit

```csharp
public async Task<ToolResult<AnalysisResult>> ProcessErrorAsync(ErrorBatch errorBatch)
{
    LogMethodEntry(); // MANDATORY
    try
    {
        // AIRES method implementation
        
        LogMethodExit(); // MANDATORY
        return ToolResult<AnalysisResult>.Success(result);
    }
    catch (Exception ex)
    {
        LogError("Failed to process error through AIRES pipeline", ex);
        LogMethodExit(); // MANDATORY even in error cases
        return ToolResult<AnalysisResult>.Failure("AIRES_PROCESS_ERROR", "Processing failed", ex);
    }
}
```

### 4.3 AIRES Constructor and Property Logging

```csharp
public class AIRESAnalysisService : CanonicalToolServiceBase
{
    public AIRESAnalysisService(IConfiguration config, ILogger logger) 
        : base(logger, nameof(AIRESAnalysisService))
    {
        // Base class handles AIRES constructor logging
        _config = config;
    }
    
    private string _pipelineStatus = "Idle";
    public string PipelineStatus 
    { 
        get 
        { 
            LogTrace("Getting PipelineStatus"); // Use base class logging
            return _pipelineStatus; 
        }
        set 
        { 
            LogDebug($"Setting PipelineStatus to {value}"); // Use base class logging
            _pipelineStatus = value; 
        }
    }
}
```

-----

## 5. AI Pipeline Precision Standards

### 5.1 CRITICAL: Consistent AI Model Responses

```csharp
// ❌ NEVER use inconsistent response formats between AI models
public string ProcessWithMistral(string input) // WRONG!

// ❌ NEVER use Trading domain results
public async Task<TradingResult<AIResearchFinding>> ProcessWithMistralAsync(string input) // WRONG!

// ✅ ALWAYS use standardized ToolResult<T> for AI operations
public async Task<ToolResult<AIResearchFinding>> ProcessWithMistralAsync(string input) // CORRECT!
{
    LogMethodEntry();
    try
    {
        var finding = await _mistralClient.AnalyzeAsync(input);
        LogMethodExit();
        return ToolResult<AIResearchFinding>.Success(finding);
    }
    catch (Exception ex)
    {
        LogError("Mistral analysis failed", ex);
        LogMethodExit();
        return ToolResult<AIResearchFinding>.Failure("MISTRAL_ERROR", "Analysis failed", ex);
    }
}
```

### 5.2 AI Pipeline Requirements

- **Responses**: Structured JSON format for ALL AI models
- **Timeouts**: 30-120 seconds per AI model
- **Retries**: 3 attempts maximum with exponential backoff
- **Confidence**: All AI responses must include confidence scores

### 5.3 AI Pipeline Helpers

Use DevTools canonical helpers for AI operations:

```csharp
// Use DevTools patterns for AI model operations
var mistralResult = await ProcessWithToolResultAsync(input, "mistral");
var deepseekResult = await ProcessWithToolResultAsync(input, "deepseek");

// All results are ToolResult<T>, not TradingResult<T>
if (mistralResult.IsSuccess)
{
    // Process successful result
}
```

-----

## 6. Error Handling Standards

### 6.1 No Silent Failures Policy for AIRES

```csharp
// ❌ WRONG - Silent AIRES failure
try
{
    ProcessAIRESPipeline();
}
catch
{
    // Swallowing AIRES exception - NEVER DO THIS
}

// ✅ CORRECT - Comprehensive AIRES error handling
try
{
    ProcessAIRESPipeline();
}
catch (ValidationException vex)
{
    LogWarning("AIRES validation failed for pipeline processing", vex);
    return ToolResult<T>.Failure("AIRES_VALIDATION_ERROR", "Validation failed", vex);
}
catch (Exception ex)
{
    LogError("Unexpected error in AIRES pipeline", ex);
    return ToolResult<T>.Failure("AIRES_PROCESS_ERROR", $"AIRES processing failed: {ex.Message}", ex);
}
```

### 6.2 ToolResult<T> Pattern

ALL AIRES operations MUST return ToolResult<T>:

```csharp
public async Task<ToolResult<ResearchBooklet>> GenerateBookletAsync(string errorContent)
{
    LogMethodEntry();
    
    try
    {
        // Validation
        if (string.IsNullOrEmpty(errorContent))
        {
            LogMethodExit();
            return ToolResult<ResearchBooklet>.Failure(
                "INVALID_ERROR_CONTENT",
                "Error content is required for AIRES processing");
        }
        
        // AIRES Pipeline Operation
        var booklet = await _aiPipeline.ProcessAsync(errorContent);
        
        if (booklet == null)
        {
            LogMethodExit();
            return ToolResult<ResearchBooklet>.Failure(
                "BOOKLET_GENERATION_FAILED",
                "AIRES pipeline failed to generate booklet");
        }
        
        LogMethodExit();
        return ToolResult<ResearchBooklet>.Success(booklet);
    }
    catch (Exception ex)
    {
        LogError($"Failed to generate AIRES booklet for content", ex);
        LogMethodExit();
        return ToolResult<ResearchBooklet>.Failure(
            "AIRES_GENERATION_ERROR",
            $"AIRES booklet generation failed: {ex.Message}", 
            ex);
    }
}
```

-----

## 7. Testing Requirements

### 7.1 AIRES Minimum Coverage Requirements

- **Unit Tests**: 80% minimum coverage for AIRES components (90% target)
- **Integration Tests**: All AI service interactions
- **E2E Tests**: Complete AIRES pipeline workflows
- **Performance Tests**: All AI operations under load
- **Security Tests**: All AIRES authentication/authorization flows

### 7.2 AIRES Test Structure

```csharp
public class AIRESAnalysisServiceTests
{
    [Fact]
    public async Task ProcessError_ValidErrorBatch_ShouldGenerateBooklet()
    {
        // Arrange
        var errorBatch = new ErrorBatchBuilder()
            .WithErrorCode("CS1998")
            .WithDescription("Async method missing await")
            .WithFilePath("/src/TestFile.cs")
            .Build();
            
        // Act
        var result = await _airesService.ProcessErrorAsync(errorBatch);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Research Booklet Generated", result.Value.Status);
    }
    
    [Theory]
    [InlineData(null, "ERROR_CONTENT_REQUIRED")]
    [InlineData("", "ERROR_CONTENT_REQUIRED")]
    [InlineData("INVALID_FORMAT", "INVALID_ERROR_FORMAT")]
    public async Task ProcessError_InvalidInput_ShouldFail(
        string errorContent, string expectedError)
    {
        // AIRES test implementation
        var result = await _airesService.ProcessErrorAsync(errorContent);
        
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.ErrorCode);
    }
}
```

### 7.3 AI Pipeline Tests

```csharp
[Fact]
public async Task AIRESPipeline_ShouldMaintainConsistency()
{
    // Arrange
    var testError = "CS1998: Missing await in async method";
    
    // Act - Process through complete AIRES pipeline
    var mistralResult = await _mistralService.AnalyzeAsync(testError);
    var deepseekResult = await _deepseekService.AnalyzeAsync(testError, mistralResult.Value);
    var bookletResult = await _bookletService.GenerateAsync(new[] { mistralResult.Value, deepseekResult.Value });
    
    // Assert - Verify AIRES pipeline consistency with ToolResult
    Assert.True(mistralResult.IsSuccess);
    Assert.True(deepseekResult.IsSuccess);
    Assert.True(bookletResult.IsSuccess);
    Assert.Contains("CS1998", bookletResult.Value.Content);
}
```

-----

## 8. AIRES Architecture Standards

### 8.1 AIRES DevTools Architecture (Mandatory)

```
AIRES CLI Layer (Console/CLI)
    ↓
AIRES Application Layer (Pipeline Orchestration)
    ↓
AIRES Domain Layer (AI Models, Business Logic)
    ↓
AIRES Infrastructure Layer (Kafka, Database, File System)

ALL LAYERS USE DEVTOOLS PATTERNS - NO TRADING REFERENCES
```

### 8.2 AIRES Project Structure

```
MarketAnalyzer.DevTools.BuildTools/  # AIRES is a DevTool
├── src/
│   ├── Domain/                    # Domain models, interfaces
│   ├── Application/               # Use cases, AI pipeline orchestration
│   ├── Infrastructure/            # Data access, external AI services
│   └── Services/                  # Service implementations
├── tests/                         # All AIRES tests
└── docs/                          # AIRES documentation
```

### 8.3 AIRES Dependency Rules

- ✅ CLI → Application → Domain → Infrastructure
- ✅ All use DevTools Foundation (CanonicalToolServiceBase, ToolResult<T>)
- ❌ Domain → Application (NEVER)
- ❌ Domain → Infrastructure (NEVER)
- ❌ Domain → CLI (NEVER)
- ❌ ANY reference to Trading domain (NEVER)

-----

## 9. Performance Requirements

### 9.1 AIRES Latency Targets

- **File Processing**: < 5 seconds per file
- **AI Model Response**: < 30 seconds per model (Mistral, DeepSeek, etc.)
- **Complete Pipeline**: < 3 minutes end-to-end
- **Booklet Generation**: < 60 seconds

### 9.2 AIRES Optimization Techniques

```csharp
// Parallel AI processing for AIRES pipeline
public async Task<IEnumerable<AIResearchFinding>> ProcessThroughPipelineAsync(ErrorBatch batch)
{
    LogMethodEntry();
    
    // Parallel execution for independent AI models
    var mistralTask = _mistralService.AnalyzeAsync(batch);
    var deepseekTask = _deepseekService.AnalyzeAsync(batch);
    
    var results = await Task.WhenAll(mistralTask, deepseekTask);
    
    LogMethodExit();
    return results.SelectMany(r => r.IsSuccess ? new[] { r.Value } : Array.Empty<AIResearchFinding>());
}

// Async/await with ConfigureAwait(false) for AIRES
public async Task<ToolResult<T>> ProcessAsync<T>()
{
    var result = await SomeAIOperationAsync().ConfigureAwait(false);
    return ToolResult<T>.Success(result);
}

// Memory-efficient AIRES data structures
public readonly struct AIRESAnalysisRequest
{
    public readonly string ErrorCode;
    public readonly string Description;
    public readonly string FilePath;
    public readonly int LineNumber;
}
```

-----

## 10. Security Standards

### 10.1 AIRES Authentication & Authorization

- OAuth 2.0 / OpenID Connect for AIRES authentication
- Role-based access control (RBAC) for AIRES operations
- API key rotation every 90 days for AI services
- Multi-factor authentication (MFA) for AIRES admin operations

### 10.2 AIRES Data Protection

```csharp
// Use secure configuration for sensitive AI API keys
public class AIRESApiService : CanonicalToolServiceBase
{
    private readonly IConfiguration _configuration;
    
    public AIRESApiService(ILogger logger, IConfiguration configuration)
        : base(logger, nameof(AIRESApiService))
    {
        _configuration = configuration;
    }
    
    public async Task<string> GetMistralApiKeyAsync()
    {
        // Never hardcode AIRES secrets
        return _configuration["MistralApi:ApiKey"];
    }
}
```

### 10.3 AIRES Security Scanning

- **Static Analysis**: Run on every AIRES commit
- **Dynamic Analysis**: Run before AIRES deployment
- **Dependency Scanning**: Daily vulnerability checks for AI libraries
- **Penetration Testing**: Quarterly AIRES security assessment

-----

## 11. Documentation Requirements

### 11.1 AIRES Code Documentation

```csharp
/// <summary>
/// Processes error content through the complete AIRES AI pipeline.
/// </summary>
/// <param name="errorBatch">The error batch containing code errors and context</param>
/// <returns>A ToolResult containing the generated research booklet or error details</returns>
/// <exception cref="ValidationException">Thrown when error validation fails</exception>
/// <remarks>
/// This method orchestrates the 6-stage AIRES pipeline:
/// 1. Mistral documentation analysis
/// 2. DeepSeek context analysis  
/// 3. CodeGemma pattern validation
/// 4. Gemma2 content synthesis
/// 5. Booklet generation
/// 6. Archive storage
/// </remarks>
public async Task<ToolResult<ResearchBooklet>> ProcessThroughAIRESPipelineAsync(ErrorBatch errorBatch)
{
    // Implementation
}
```

### 11.2 AIRES Project Documentation

Required AIRES documentation files:

- `README.md` - AIRES project overview and setup
- `AIRES_ARCHITECTURE.md` - AIRES system architecture
- `AIRES_API.md` - AIRES API documentation
- `AIRES_DEPLOYMENT.md` - AIRES deployment procedures
- `AIRES_TROUBLESHOOTING.md` - Common AIRES issues
- `AIRES_CHANGELOG.md` - AIRES version history
- `AIRES_DEVTOOLS_INTEGRATION.md` - How AIRES fits in DevTools ecosystem

-----

## 12. Code Analysis and Quality

### 12.1 AIRES Roslyn Analyzers (Mandatory)

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="8.0.0" />
  <PackageReference Include="StyleCop.Analyzers" Version="1.2.0" />
  <PackageReference Include="SonarAnalyzer.CSharp" Version="9.0.0" />
  <!-- NO Trading domain analyzers -->
</ItemGroup>
```

### 12.2 AIRES Code Metrics Thresholds

- **Cyclomatic Complexity**: Max 10 per AIRES method
- **Lines of Code**: Max 50 per AIRES method
- **Class Coupling**: Max 10 dependencies per AIRES class
- **Maintainability Index**: Min 70 for AIRES components

### 12.3 AIRES Zero Warning Policy

**MANDATORY**: All AIRES code MUST compile with zero warnings.

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <WarningLevel>5</WarningLevel>
  <NoWarn></NoWarn>
</PropertyGroup>
```

-----

## 13. Standard Tools and Libraries

### 13.1 Mandatory AIRES Tools by Category

#### AIRES Logging
- **Serilog** - Structured logging (NO alternatives)
- **Seq** - Log aggregation (NO alternatives)  
- **ILogger** - Mandatory interface (NO ILogger<T>, NO ITradingLogger)

#### AIRES Testing
- **xUnit** - Unit testing framework
- **Moq** - Mocking framework for AI services
- **FluentAssertions** - Assertion library

#### AIRES Serialization
- **System.Text.Json** - Primary (NO Newtonsoft except legacy)
- **Protobuf-net** - Binary serialization for AI data

#### AIRES HTTP/API
- **Refit** - Type-safe HTTP client for AI APIs
- **Polly** - Resilience and retry policies for AI calls

#### AIRES Dependency Injection
- **Microsoft.Extensions.DependencyInjection** - ONLY

#### AIRES Database
- **Entity Framework Core** - ORM for AIRES data
- **PostgreSQL** - Primary database
- **LiteDB** - Local development database

#### AIRES Messaging
- **Apache Kafka** - AI pipeline messaging
- **Confluent.Kafka** - Kafka client library

#### AIRES Caching
- **Microsoft.Extensions.Caching** - In-memory
- **Redis** - Distributed caching for AI results

### 13.2 Prohibited Libraries in AIRES

- ❌ Newtonsoft.Json (except for legacy compatibility)
- ❌ log4net, NLog (use Serilog)
- ❌ Castle Windsor, Autofac (use MS DI)
- ❌ NUnit, MSTest (use xUnit)
- ❌ ILogger<T> (use ILogger)
- ❌ ITradingLogger (Trading domain - NEVER use)
- ❌ Any Trading domain libraries
- ❌ Any unlicensed or GPL libraries

-----

## 14. Development Workflow

### 14.1 AIRES Pre-Development Checklist

```markdown
- [ ] AIRES research completed (2-4 hours minimum)
- [ ] AIRES research report created and approved
- [ ] Existing DevTools canonical services identified
- [ ] AIRES architecture review completed
- [ ] AI pipeline security implications reviewed
- [ ] AIRES performance targets defined
- [ ] AIRES test plan created
- [ ] AIRES error booklet generated for any issues
- [ ] Fix counter initialized [0/10]
- [ ] Verified NO Trading domain references
```

### 14.2 AIRES Development Process

1. **Research Phase** (Mandatory)
   * Research existing DevTools solutions
   * Document AIRES findings
   * Get AIRES approach approval

2. **Booklet Generation Phase** (Mandatory)
   * Generate AIRES error booklet for any issues
   * Wait for AIRES AI team analysis
   * Review and understand AIRES recommendations

3. **Design Phase**
   * Create AIRES technical design
   * Review with AIRES team
   * Update AIRES documentation

4. **Implementation Phase**
   * Implement using DevTools canonical patterns
   * Follow all AIRES logging requirements
   * Maintain AIRES test coverage
   * Track fixes with counter

5. **Review Phase**
   * Self-review against AIRES checklist
   * Peer review for AIRES components
   * Automated AIRES analysis
   * Status Checkpoint Review at 10 fixes

6. **Deployment Phase**
   * All AIRES tests passing
   * Zero warnings in AIRES code
   * AIRES documentation updated

-----

## 15. Progress Reporting

### 15.1 AIRES Long-Running Operations

```csharp
public async Task<ToolResult<bool>> ProcessLargeErrorBatchAsync(
    IEnumerable<ErrorRecord> errors,
    IProgress<AIRESProcessingProgress> progress)
{
    LogMethodEntry();
    
    var totalCount = errors.Count();
    var processed = 0;
    
    foreach (var error in errors)
    {
        // Process through AIRES pipeline
        await ProcessThroughAIRESPipelineAsync(error);
        
        processed++;
        
        // Report AIRES progress every 10 items or 5%
        if (processed % 10 == 0 || processed % (totalCount / 20) == 0)
        {
            var progressInfo = new AIRESProcessingProgress
            {
                TotalErrors = totalCount,
                ProcessedErrors = processed,
                PercentComplete = (decimal)processed / totalCount * 100,
                EstimatedTimeRemaining = EstimateAIRESTimeRemaining(processed, totalCount),
                CurrentError = error.Id,
                Message = $"Processing error {error.Id} through AIRES pipeline"
            };
            
            progress?.Report(progressInfo);
            
            LogInfo($"AIRES processing progress: {processed}/{totalCount} ({progressInfo.PercentComplete:F1}%)");
        }
    }
    
    LogMethodExit();
    return ToolResult<bool>.Success(true);
}
```

-----

## 16. **HIGH PRIORITY**: Observability & Distributed Tracing

### 16.1 Comprehensive AIRES Observability Mandate

**MANDATORY**: All AIRES services and AI pipeline components MUST implement comprehensive observability, extending beyond basic logging to include metrics and distributed tracing. This is critical for understanding AI pipeline behavior, performance profiling, and rapid incident response.

### 16.2 AIRES Distributed Tracing Implementation

```csharp
// MANDATORY: OpenTelemetry integration in all AIRES services
public class AIRESAnalysisService : CanonicalToolServiceBase
{
    private static readonly ActivitySource ActivitySource = new("AIRES.AnalysisService");
    
    public async Task<ToolResult<ResearchBooklet>> ProcessErrorAsync(ErrorBatch errorBatch)
    {
        using var activity = ActivitySource.StartActivity("ProcessError");
        activity?.SetTag("aires.error.code", errorBatch.PrimaryErrorCode);
        activity?.SetTag("aires.error.count", errorBatch.Errors.Count.ToString());
        activity?.SetTag("aires.pipeline.stage", "entry");
        
        LogMethodEntry();
        try
        {
            // AIRES method implementation with tracing
            LogMethodExit();
            return ToolResult<ResearchBooklet>.Success(booklet);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogError("Failed to process error through AIRES", ex);
            LogMethodExit();
            throw;
        }
    }
}
```

### 16.3 AIRES Metrics and Alerting

- **MANDATORY**: Expose AIRES application and AI pipeline metrics (e.g., request rates, error rates, AI model latency, booklet generation success rates, pipeline throughput)
- Use Prometheus-compatible metrics exporters for AIRES
- Define clear alerting thresholds for critical AIRES metrics

### 16.4 Required AIRES Tools

- **OpenTelemetry SDKs**: For AIRES tracing and metrics instrumentation
- **Prometheus**: For AIRES metrics collection and storage
- **Grafana**: For AIRES metrics visualization and dashboards
- **Jaeger / Zipkin**: For AIRES distributed trace visualization

-----

## 🚨 ENFORCEMENT

### Automated Enforcement

1. **Pre-commit hooks** validate AIRES standards compliance
2. **CI/CD pipeline** rejects non-compliant AIRES code
3. **Roslyn analyzers** enforce AIRES patterns in real-time
4. **Code reviews** must verify AIRES standard compliance
5. **AIRES Error Resolution System** mandatory for all fixes
6. **Status Checkpoint Reviews** every 10 fixes

### Manual Enforcement

1. **AIRES architecture reviews** - Weekly
2. **AIRES code quality audits** - Monthly
3. **AI pipeline performance reviews** - Quarterly
4. **AIRES security audits** - Quarterly
5. **Booklet compliance reviews** - Per development cycle

### Violations

- **First violation**: Warning and AIRES education
- **Second violation**: AIRES code rejection
- **Third violation**: Escalation to management
- **Booklet bypass**: Immediate AIRES code rejection
- **Trading domain reference**: Immediate rejection

-----

## 📚 AIRES Quick Reference Card

```csharp
// Every AIRES service MUST follow this pattern:
public class MyAIRESService : CanonicalToolServiceBase, IMyAIRESService
{
    public MyAIRESService(ILogger logger) : base(logger, nameof(MyAIRESService)) { }
    
    public async Task<ToolResult<MyResult>> DoSomethingAsync(MyRequest request)
    {
        LogMethodEntry(); // MANDATORY
        
        try
        {
            // Validate
            if (!IsValid(request))
            {
                LogMethodExit();
                return ToolResult<MyResult>.Failure("AIRES_VALIDATION_ERROR", "Invalid request");
            }
            
            // Process through AIRES
            var result = await ProcessAsync(request).ConfigureAwait(false);
            
            // Return
            LogMethodExit(); // MANDATORY
            return ToolResult<MyResult>.Success(result);
        }
        catch (Exception ex)
        {
            LogError("AIRES operation failed", ex);
            LogMethodExit(); // MANDATORY
            return ToolResult<MyResult>.Failure("AIRES_OPERATION_ERROR", $"AIRES operation failed: {ex.Message}", ex);
        }
    }
}
```

## 📋 Mandatory AIRES Development Checklist

```markdown
### Before ANY AIRES Code Changes:
- [ ] Fix counter initialized: [0/10]
- [ ] AIRES Error Resolution System ready
- [ ] AIRES booklet generation process tested
- [ ] All DevTools canonical patterns reviewed
- [ ] ILogger interface confirmed (not ILogger<T>, not ITradingLogger)
- [ ] ToolResult<T> pattern understood (not TradingResult<T>)
- [ ] Zero warning policy configured for AIRES
- [ ] OpenTelemetry integration verified for AIRES
- [ ] NO Trading domain references

### During AIRES Development:
- [ ] Generate AIRES booklet for EVERY error/issue
- [ ] Wait for AIRES AI team analysis (Mistral, DeepSeek, CodeGemma, Gemma2)
- [ ] Read and understand AIRES booklet recommendations
- [ ] Apply fixes ONLY based on AIRES booklet guidance
- [ ] Track fixes: 📊 Fix Counter: [X/10]
- [ ] Perform SCR at 10 fixes
- [ ] Reset counter after checkpoint
- [ ] Verify DevTools patterns used throughout

### Before AIRES Commit:
- [ ] Zero compilation errors in AIRES
- [ ] Zero warnings (TreatWarningsAsErrors=true)
- [ ] All AIRES tests passing
- [ ] AIRES code coverage >80%
- [ ] DevTools canonical patterns validated
- [ ] AIRES documentation updated
- [ ] AI pipeline performance benchmarks verified
- [ ] NO Trading domain references
```

-----

## 🔗 Related AIRES Documents

- [AIRES_ARCHITECTURE.md](../../../docs/AIRES/Architecture/AIRES_System_Architecture.md) - AIRES system architecture
- [AIRES_CORE_COMPONENTS.md](../../../docs/AIRES/Core/AIRES_Core_Components.md) - AIRES core components
- [AIRES_OPERATIONS_MANUAL.md](../../../docs/AIRES/Operations/AIRES_Operations_Manual.md) - AIRES operations guide
- [AIRES_TROUBLESHOOTING.md](../../../docs/AIRES/Troubleshooting/AIRES_Troubleshooting_Guide.md) - Common AIRES issues

-----

## 🔴 CRITICAL CHANGES IN V2

1. **Logger Interface**: `ILogger` (not `ILogger<T>`, not `ITradingLogger`)
2. **Base Classes**: `CanonicalToolServiceBase` (not `CanonicalServiceBase`)
3. **Result Types**: `ToolResult<T>` (not `TradingResult<T>`)
4. **Complete Independence**: NO Trading domain references ever

**Remember: AIRES is a Development Tool in the DevTools ecosystem. It must NEVER reference Trading domain code.**

-----

**Remember: These AIRES standards are MANDATORY. No exceptions. No excuses. No fixes without AIRES booklets.**

*Last reviewed: 2025-01-13*  
*Next review: 2025-02-13*  
*Version: 2.0*  
*System: AI Error Resolution System (AIRES) - Development Tool*