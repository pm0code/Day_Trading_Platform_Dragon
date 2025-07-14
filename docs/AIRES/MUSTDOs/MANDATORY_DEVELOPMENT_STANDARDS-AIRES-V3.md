# 🚨 AIRES MANDATORY DEVELOPMENT STANDARDS V3 🚨

**THIS DOCUMENT SUPERSEDES ALL OTHER GUIDANCE AND MUST BE FOLLOWED WITHOUT EXCEPTION**

Last Updated: 2025-01-13  
Version: 3.0  
System: AI Error Resolution System (AIRES)

## 🔴 CRITICAL REVISION: AIRES is a STANDALONE System

**FUNDAMENTAL PRINCIPLE**: AIRES is an INDEPENDENT, STANDALONE system with its own patterns, infrastructure, and standards. It does NOT inherit from or depend on ANY other project.

### What Changed in V3:
1. **Complete Independence**: AIRES is its own system with zero external dependencies
2. **Own Patterns**: Use AIRES-specific patterns (AIRESServiceBase, AIRESResult<T>, IAIRESLogger)
3. **Own Infrastructure**: AIRES has its own canonical implementations
4. **No External References**: AIRES must NEVER reference Trading, DevTools, or any other domain

## 🔴 CRITICAL: READ THIS FIRST

This document establishes ALL mandatory development standards for the AI Error Resolution System (AIRES). Every developer, including AI assistants, MUST read and follow these standards. Violations will result in code rejection.

**AIRES MISSION**: Autonomous error analysis and research booklet generation through 4-stage AI pipeline (Mistral → DeepSeek → CodeGemma → Gemma2)

**AIRES IDENTITY**: Standalone, Independent System with Complete Autonomy

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

**AIRES INPUT/OUTPUT DIRECTORIES**: Configured via aires.ini file at startup for flexibility across different projects. No hardcoded paths - AIRES adapts to each project's needs.

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

**SCR Template**: Use the comprehensive AIRES-specific SCR template located at:
[AIRES_Status_Checkpoint_Review_Template.md](../../../AIRES/docs/AIRES_Status_Checkpoint_Review_Template.md)

**FAILURE TO RUN CHECKPOINTS = ARCHITECTURAL DRIFT = AIRES FAILURE**

### 0.3 Gemini API Integration for AIRES Architectural Validation

**MANDATORY**: For any AIRES architectural issues, async deadlocks, or complex technical decisions:

```bash
# Gemini API Usage for AIRES:
curl "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-exp:generateContent?key=AIzaSyDP7daxEmHxuSTA3ZObO4Rgkl2HswqpHcs" \
  -H 'Content-Type: application/json' \
  -X POST \
  -d '{"contents":[{"parts":[{"text":"Your AIRES architectural question here"}]}]}'
```

## Table of Contents

0. [**CRITICAL**: Mandatory Booklet-First Development Protocol](#critical-mandatory-booklet-first-development-protocol)
   - [0.1 Self-Referential AI Error Resolution](#01-absolute-requirement-self-referential-ai-error-resolution)
   - [0.2 Status Checkpoint Review (SCR) Protocol](#02-status-checkpoint-review-scr-protocol)
   - [0.3 Gemini API Integration](#03-gemini-api-integration-for-aires-architectural-validation)
1. [Core AIRES Development Principles](#core-aires-development-principles)
   - [1.1 Complete Independence Policy](#11-complete-independence-policy)
   - [1.2 AIRES-First Implementation Policy](#12-aires-first-implementation-policy)
   - [1.3 Research-First Development & FOSS Utilization](#13-research-first-development--foss-utilization)
   - [1.4 Zero Errors, Zero Warnings Policy (0/0 Policy)](#14-zero-errors-zero-warnings-policy-00-policy)
   - [1.5 Clean Code Policy](#15-clean-code-policy)
2. [Research-First Mandate](#research-first-mandate)
3. [AIRES Canonical Service Implementation](#aires-canonical-service-implementation)
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

### 1.1 Complete Independence Policy

- **AIRES is STANDALONE**: Zero dependencies on external projects
- **AIRES defines its OWN patterns**: AIRESServiceBase, AIRESResult<T>, IAIRESLogger
- **AIRES has its OWN infrastructure**: Complete implementation stack
- **NO external references**: No Trading, DevTools, or other domain code

### 1.2 AIRES-First Implementation Policy

- **ALWAYS** use AIRES canonical patterns
- **NEVER** import patterns from other projects
- **ALWAYS** implement within AIRES ecosystem
- **NEVER** create dependencies on external systems

### 1.3 Research-First Development & FOSS Utilization

- **MANDATORY**: 2-4 hours minimum research before building anything in AIRES
- **MANDATORY**: Check FOSS repositories FIRST - don't reinvent the wheel!
- **MANDATORY**: Create research reports documenting all AIRES findings
- **MANDATORY**: Read COMPLETE AIRES documentation - no guessing allowed
- **MANDATORY**: Get approval before AIRES implementation

#### FOSS Research Requirements:
Before implementing ANY new component, check these trusted FOSS sources:
- **GitHub** - Search for existing solutions with proper licenses
- **SourceForge** - Established open-source projects
- **FossHub** - Clean, trusted downloads
- **AlternativeTo** - Find open-source alternatives
- **NuGet Gallery** - For .NET packages (filter by license)

If a suitable FOSS solution exists with compatible licensing (MIT, Apache 2.0, BSD), USE IT rather than building from scratch.

### 1.4 Zero Errors, Zero Warnings Policy (0/0 Policy)

**ABSOLUTE REQUIREMENT**: AIRES follows a strict 0/0 policy:
- **0 Compilation Errors**: Code MUST compile cleanly
- **0 Warnings**: ALL warnings MUST be resolved
- **TreatWarningsAsErrors**: Set to true in all projects
- **No Warning Suppression**: Unless documented with justification
- **Before ANY Commit**: Must achieve 0/0 status
- **Before ANY PR**: Must maintain 0/0 status

This is NON-NEGOTIABLE. Code with warnings or errors is considered incomplete.

### 1.5 Clean Code Policy

- **MANDATORY**: Remove all dead code after successful AIRES implementations
- **NEVER** leave commented-out code in AIRES production
- **ALWAYS** use version control for AIRES code history

-----

## 2. Research-First Mandate

### 2.1 Pre-Implementation Research Requirements

```markdown
BEFORE WRITING ANY AIRES CODE:
1. ✅ Research existing AIRES solutions (2-4 hours minimum)
2. ✅ Document findings in AIRES research report
3. ✅ Identify AIRES-standard patterns
4. ✅ Get approval for AIRES approach
5. ✅ Only then begin AIRES implementation
```

### 2.2 AIRES Research Report Template

```markdown
# AIRES Research Report: [Feature/Component Name]
Date: [YYYY-MM-DD]
Researcher: [Name/AI]
System: AI Error Resolution System (AIRES)
Type: Standalone System

## Executive Summary
[Brief overview of AIRES findings]

## Research Conducted
- [ ] AIRES patterns reviewed
- [ ] Industry standards for AI pipelines analyzed
- [ ] Similar error resolution systems studied
- [ ] AIRES performance implications considered
- [ ] AIRES security implications reviewed

## Findings
1. **AIRES Standard Solutions Found:**
   - [List all relevant AIRES patterns]
   
2. **Recommended AIRES Approach:**
   - [Detailed AIRES recommendation]
   
3. **AIRES Alternatives Considered:**
   - [List alternatives and why rejected]

## Approval
- [ ] AIRES approach approved by: [Name]
- [ ] Date: [YYYY-MM-DD]
```

-----

## 3. AIRES Canonical Service Implementation

### 3.1 Mandatory AIRES Canonical Services

ALL AIRES services MUST extend the AIRES canonical base class:

```csharp
// ❌ WRONG - Direct interface implementation
public class MyAIRESService : IMyAIRESService 
{
    // This violates AIRES standards
}

// ❌ WRONG - Using external base classes
public class MyAIRESService : SomeOtherServiceBase, IMyAIRESService
{
    // This violates AIRES independence
}

// ✅ CORRECT - Extends AIRES canonical base
public class MyAIRESService : AIRESServiceBase, IMyAIRESService
{
    public MyAIRESService(IAIRESLogger logger) 
        : base(logger, nameof(MyAIRESService))
    {
        // Constructor implementation
    }
}
```

### 3.2 AIRES Canonical Base Classes

- `AIRESServiceBase` - For all AIRES services
- `AIRESResult<T>` - For all AIRES operation results
- `IAIRESLogger` - AIRES logging interface
- **NO external base classes allowed**

### 3.3 AIRES Canonical Service Features

All AIRES canonical services provide:

- ✅ Automatic method entry/exit logging
- ✅ Health checks and metrics
- ✅ Proper lifecycle management (Initialize, Start, Stop)
- ✅ AIRESResult<T> pattern implementation
- ✅ Comprehensive error handling
- ✅ Performance tracking
- ✅ OpenTelemetry integration

-----

## 4. Method Logging Requirements

### 4.1 MANDATORY: IAIRESLogger Implementation

**CRITICAL**: ALL AIRES services MUST use `IAIRESLogger` interface - AIRES's own logging abstraction.

```csharp
// ❌ WRONG - Using external logger
public class MyAIRESService
{
    private readonly ILogger _logger; // VIOLATION!
    
    public MyAIRESService(ILogger logger)
    {
        _logger = logger;
    }
}

// ❌ WRONG - Using typed logger
public class MyAIRESService
{
    private readonly ILogger<MyAIRESService> _logger; // VIOLATION!
}

// ✅ CORRECT - Using AIRES logger
public class MyAIRESService : AIRESServiceBase, IMyAIRESService
{
    public MyAIRESService(IAIRESLogger logger) 
        : base(logger, nameof(MyAIRESService))
    {
        // Base class provides IAIRESLogger functionality
    }
}
```

### 4.2 MANDATORY: Every AIRES Method Must Log Entry and Exit

```csharp
public async Task<AIRESResult<AnalysisResult>> ProcessErrorAsync(ErrorBatch errorBatch)
{
    LogMethodEntry(); // MANDATORY
    try
    {
        // AIRES method implementation
        
        LogMethodExit(); // MANDATORY
        return AIRESResult<AnalysisResult>.Success(result);
    }
    catch (Exception ex)
    {
        LogError("Failed to process error through AIRES pipeline", ex);
        LogMethodExit(); // MANDATORY even in error cases
        return AIRESResult<AnalysisResult>.Failure("AIRES_PROCESS_ERROR", "Processing failed", ex);
    }
}
```

### 4.3 AIRES Constructor and Property Logging

```csharp
public class AIRESAnalysisService : AIRESServiceBase
{
    public AIRESAnalysisService(IConfiguration config, IAIRESLogger logger) 
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

// ❌ NEVER use external result types
public async Task<SomeOtherResult<AIResearchFinding>> ProcessWithMistralAsync(string input) // WRONG!

// ✅ ALWAYS use standardized AIRESResult<T> for AI operations
public async Task<AIRESResult<AIResearchFinding>> ProcessWithMistralAsync(string input) // CORRECT!
{
    LogMethodEntry();
    try
    {
        var finding = await _mistralClient.AnalyzeAsync(input);
        LogMethodExit();
        return AIRESResult<AIResearchFinding>.Success(finding);
    }
    catch (Exception ex)
    {
        LogError("Mistral analysis failed", ex);
        LogMethodExit();
        return AIRESResult<AIResearchFinding>.Failure("MISTRAL_ERROR", "Analysis failed", ex);
    }
}
```

### 5.2 AI Pipeline Requirements

- **Responses**: Structured JSON format for ALL AI models
- **Timeouts**: 30-120 seconds per AI model
- **Retries**: 3 attempts maximum with exponential backoff
- **Confidence**: All AI responses must include confidence scores

### 5.3 AI Pipeline Helpers

Use AIRES canonical helpers for AI operations:

```csharp
// Use AIRES patterns for AI model operations
var mistralResult = await ProcessWithAIRESResultAsync(input, "mistral");
var deepseekResult = await ProcessWithAIRESResultAsync(input, "deepseek");

// All results are AIRESResult<T>
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
    return AIRESResult<T>.Failure("AIRES_VALIDATION_ERROR", "Validation failed", vex);
}
catch (Exception ex)
{
    LogError("Unexpected error in AIRES pipeline", ex);
    return AIRESResult<T>.Failure("AIRES_PROCESS_ERROR", $"AIRES processing failed: {ex.Message}", ex);
}
```

### 6.2 AIRESResult<T> Pattern

ALL AIRES operations MUST return AIRESResult<T>:

```csharp
public async Task<AIRESResult<ResearchBooklet>> GenerateBookletAsync(string errorContent)
{
    LogMethodEntry();
    
    try
    {
        // Validation
        if (string.IsNullOrEmpty(errorContent))
        {
            LogMethodExit();
            return AIRESResult<ResearchBooklet>.Failure(
                "INVALID_ERROR_CONTENT",
                "Error content is required for AIRES processing");
        }
        
        // AIRES Pipeline Operation
        var booklet = await _aiPipeline.ProcessAsync(errorContent);
        
        if (booklet == null)
        {
            LogMethodExit();
            return AIRESResult<ResearchBooklet>.Failure(
                "BOOKLET_GENERATION_FAILED",
                "AIRES pipeline failed to generate booklet");
        }
        
        LogMethodExit();
        return AIRESResult<ResearchBooklet>.Success(booklet);
    }
    catch (Exception ex)
    {
        LogError($"Failed to generate AIRES booklet for content", ex);
        LogMethodExit();
        return AIRESResult<ResearchBooklet>.Failure(
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
    
    // Assert - Verify AIRES pipeline consistency with AIRESResult
    Assert.True(mistralResult.IsSuccess);
    Assert.True(deepseekResult.IsSuccess);
    Assert.True(bookletResult.IsSuccess);
    Assert.Contains("CS1998", bookletResult.Value.Content);
}
```

-----

## 8. AIRES Architecture Standards

### 8.1 AIRES Standalone Architecture (Mandatory)

```
AIRES CLI Layer (Console/CLI)
    ↓
AIRES Application Layer (Pipeline Orchestration)
    ↓
AIRES Domain Layer (AI Models, Business Logic)
    ↓
AIRES Infrastructure Layer (Kafka, Database, File System)

ALL LAYERS USE AIRES PATTERNS - COMPLETE INDEPENDENCE
```

### 8.2 AIRES Project Structure

```
AIRES/
├── src/
│   ├── AIRES.Core/               # Domain models, interfaces
│   ├── AIRES.Foundation/         # AIRES canonical patterns
│   ├── AIRES.Application/        # Use cases, AI pipeline orchestration
│   ├── AIRES.Infrastructure/     # Data access, external AI services
│   ├── AIRES.CLI/               # Command-line interface
│   └── AIRES.Watchdog/          # Autonomous monitoring
├── tests/                       # All AIRES tests
├── docs/                        # AIRES documentation
└── tools/                       # AIRES utilities
```

### 8.3 AIRES Dependency Rules

- ✅ CLI → Application → Domain → Infrastructure
- ✅ All use AIRES Foundation (AIRESServiceBase, AIRESResult<T>)
- ❌ Domain → Application (NEVER)
- ❌ Domain → Infrastructure (NEVER)
- ❌ Domain → CLI (NEVER)
- ❌ ANY reference to external projects (NEVER)

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
public async Task<AIRESResult<T>> ProcessAsync<T>()
{
    var result = await SomeAIOperationAsync().ConfigureAwait(false);
    return AIRESResult<T>.Success(result);
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
public class AIRESApiService : AIRESServiceBase
{
    private readonly IConfiguration _configuration;
    
    public AIRESApiService(IAIRESLogger logger, IConfiguration configuration)
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
/// <returns>An AIRESResult containing the generated research booklet or error details</returns>
/// <exception cref="ValidationException">Thrown when error validation fails</exception>
/// <remarks>
/// This method orchestrates the 4-stage AIRES pipeline:
/// 1. Mistral documentation analysis
/// 2. DeepSeek context analysis  
/// 3. CodeGemma pattern validation
/// 4. Gemma2 content synthesis
/// </remarks>
public async Task<AIRESResult<ResearchBooklet>> ProcessThroughAIRESPipelineAsync(ErrorBatch errorBatch)
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
- `AIRES_INDEPENDENCE.md` - AIRES standalone architecture

-----

## 12. Code Analysis and Quality

### 12.1 AIRES Roslyn Analyzers (Mandatory)

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="8.0.0" />
  <PackageReference Include="StyleCop.Analyzers" Version="1.2.0" />
  <PackageReference Include="SonarAnalyzer.CSharp" Version="9.0.0" />
  <!-- NO external domain analyzers -->
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
- **IAIRESLogger** - AIRES logging interface

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

### 13.2 Prohibited in AIRES

- ❌ External domain patterns (Trading, DevTools, etc.)
- ❌ External base classes
- ❌ External result types
- ❌ External logger interfaces
- ❌ Any dependency on external projects
- ❌ Newtonsoft.Json (except for legacy compatibility)
- ❌ log4net, NLog (use Serilog via IAIRESLogger)
- ❌ Castle Windsor, Autofac (use MS DI)
- ❌ NUnit, MSTest (use xUnit)

-----

## 14. Development Workflow

### 14.1 AIRES Pre-Development Checklist

```markdown
- [ ] AIRES research completed (2-4 hours minimum)
- [ ] AIRES research report created and approved
- [ ] Existing AIRES canonical services identified
- [ ] AIRES architecture review completed
- [ ] AI pipeline security implications reviewed
- [ ] AIRES performance targets defined
- [ ] AIRES test plan created
- [ ] AIRES error booklet generated for any issues
- [ ] Fix counter initialized [0/10]
- [ ] Verified AIRES complete independence
```

### 14.2 AIRES Development Process

1. **Research Phase** (Mandatory)
   * Research existing AIRES solutions
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
   * Implement using AIRES canonical patterns
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
public async Task<AIRESResult<bool>> ProcessLargeErrorBatchAsync(
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
    return AIRESResult<bool>.Success(true);
}
```

-----

## 16. **HIGH PRIORITY**: Observability & Distributed Tracing

### 16.1 Comprehensive AIRES Observability Mandate

**MANDATORY**: All AIRES services and AI pipeline components MUST implement comprehensive observability, extending beyond basic logging to include metrics and distributed tracing. This is critical for understanding AI pipeline behavior, performance profiling, and rapid incident response.

### 16.2 AIRES Distributed Tracing Implementation

```csharp
// MANDATORY: OpenTelemetry integration in all AIRES services
public class AIRESAnalysisService : AIRESServiceBase
{
    private static readonly ActivitySource ActivitySource = new("AIRES.AnalysisService");
    
    public async Task<AIRESResult<ResearchBooklet>> ProcessErrorAsync(ErrorBatch errorBatch)
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
            return AIRESResult<ResearchBooklet>.Success(booklet);
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
- **External dependency**: Immediate rejection

-----

## 📚 AIRES Quick Reference Card

```csharp
// Every AIRES service MUST follow this pattern:
public class MyAIRESService : AIRESServiceBase, IMyAIRESService
{
    public MyAIRESService(IAIRESLogger logger) : base(logger, nameof(MyAIRESService)) { }
    
    public async Task<AIRESResult<MyResult>> DoSomethingAsync(MyRequest request)
    {
        LogMethodEntry(); // MANDATORY
        
        try
        {
            // Validate
            if (!IsValid(request))
            {
                LogMethodExit();
                return AIRESResult<MyResult>.Failure("AIRES_VALIDATION_ERROR", "Invalid request");
            }
            
            // Process through AIRES
            var result = await ProcessAsync(request).ConfigureAwait(false);
            
            // Return
            LogMethodExit(); // MANDATORY
            return AIRESResult<MyResult>.Success(result);
        }
        catch (Exception ex)
        {
            LogError("AIRES operation failed", ex);
            LogMethodExit(); // MANDATORY
            return AIRESResult<MyResult>.Failure("AIRES_OPERATION_ERROR", $"AIRES operation failed: {ex.Message}", ex);
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
- [ ] All AIRES canonical patterns reviewed
- [ ] IAIRESLogger interface confirmed
- [ ] AIRESResult<T> pattern understood
- [ ] Zero warning policy configured for AIRES
- [ ] OpenTelemetry integration verified for AIRES
- [ ] NO external project references

### During AIRES Development:
- [ ] Generate AIRES booklet for EVERY error/issue
- [ ] Wait for AIRES AI team analysis (Mistral, DeepSeek, CodeGemma, Gemma2)
- [ ] Read and understand AIRES booklet recommendations
- [ ] Apply fixes ONLY based on AIRES booklet guidance
- [ ] Track fixes: 📊 Fix Counter: [X/10]
- [ ] Perform SCR at 10 fixes
- [ ] Reset counter after checkpoint
- [ ] Verify AIRES patterns used throughout

### Before AIRES Commit:
- [ ] Zero compilation errors in AIRES
- [ ] Zero warnings (TreatWarningsAsErrors=true)
- [ ] All AIRES tests passing
- [ ] AIRES code coverage >80%
- [ ] AIRES canonical patterns validated
- [ ] AIRES documentation updated
- [ ] AI pipeline performance benchmarks verified
- [ ] NO external project references
```

-----

## 🔗 Related AIRES Documents

- [AIRES_ARCHITECTURE.md](../../../docs/AIRES/Architecture/AIRES_System_Architecture.md) - AIRES system architecture
- [AIRES_CORE_COMPONENTS.md](../../../docs/AIRES/Core/AIRES_Core_Components.md) - AIRES core components
- [AIRES_OPERATIONS_MANUAL.md](../../../docs/AIRES/Operations/AIRES_Operations_Manual.md) - AIRES operations guide
- [AIRES_TROUBLESHOOTING.md](../../../docs/AIRES/Troubleshooting/AIRES_Troubleshooting_Guide.md) - Common AIRES issues
- [AIRES_INDEPENDENCE.md](../../../docs/AIRES/Architecture/AIRES_Independence_Architecture.md) - AIRES standalone design

-----

## 🔴 CRITICAL CHANGES IN V3

1. **Complete Independence**: AIRES is STANDALONE with zero external dependencies
2. **Own Patterns**: AIRESServiceBase, AIRESResult<T>, IAIRESLogger
3. **Own Infrastructure**: Complete AIRES implementation stack
4. **No External References**: No Trading, DevTools, or other domains

**Remember: AIRES is a completely independent, standalone system. It must NEVER reference or depend on any external project code.**

-----

**Remember: These AIRES standards are MANDATORY. No exceptions. No excuses. No fixes without AIRES booklets.**

*Last reviewed: 2025-01-13*  
*Next review: 2025-02-13*  
*Version: 3.0*  
*System: AI Error Resolution System (AIRES) - Standalone System*