# 🚨 MANDATORY DEVELOPMENT STANDARDS AND PATTERNS - AIRES 🚨

**THIS DOCUMENT SUPERSEDES ALL OTHER GUIDANCE AND MUST BE FOLLOWED WITHOUT EXCEPTION**

Last Updated: 2025-07-13
Version: 5.0 (AIRES-Specific)

## 🔴 CRITICAL: READ THIS FIRST

This document consolidates ALL mandatory development standards for AIRES (AI Error Resolution System) as a STANDALONE project. Every developer, including AI assistants, MUST read and follow these standards. Violations will result in code rejection.

**FUNDAMENTAL PRINCIPLE**: AIRES is an INDEPENDENT, STANDALONE system with its own patterns, standards, and architecture. It does NOT inherit from or depend on any trading platform patterns.

## 🚨 CRITICAL ADDITION: MANDATORY BOOKLET-FIRST DEVELOPMENT PROTOCOL

### 0.1 ABSOLUTE REQUIREMENT: Self-Referential Error Resolution

**MANDATORY**: AIRES must use itself for its own development. NEVER fix any bug, error, or issue without first generating a research booklet through AIRES.

```bash
# MANDATORY WORKFLOW FOR ALL AIRES FIXES:
1. Capture error output to file
2. Run through AIRES CLI: 
   cd /mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/AIRES
   dotnet run -- process error_output.txt
3. WAIT for AI team analysis (Mistral, DeepSeek, CodeGemma, Gemma2)
4. Review generated booklet in ./docs/error-booklets/[DATE]/
5. Understand root cause from research
6. ONLY THEN apply fix based on booklet guidance
```

**VIOLATIONS WILL RESULT IN**:
- Incomplete understanding of issues
- Superficial "quick fixes" that miss root causes
- Architectural drift and technical debt accumulation
- Violation of systematic engineering principles

**NO EXCEPTIONS** - Even for "simple" or "obvious" fixes!

### 0.2 Status Checkpoint Review (SCR) Protocol

**MANDATORY**: Implement fix counter system with checkpoint reviews every 10 fixes.

#### Fix Counter Protocol:
1. **Initialize**: Start each session with Fix Counter: [0/10]
2. **Track**: Increment counter with EVERY fix applied
3. **Report**: Show counter in EVERY response: 📊 Fix Counter: [X/10]
4. **Checkpoint**: At 10 fixes, perform mandatory Status Checkpoint Review (SCR)
5. **Reset**: After checkpoint, reset counter to [0/10]

#### Status Checkpoint Review Requirements:
**MANDATORY**: Use comprehensive SCR template located at:
`/mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/AIRES/docs/AIRES_Status_Checkpoint_Review_Template.md`

**Process**:
1. Copy template to checkpoints folder with timestamp
2. Complete ALL sections thoroughly
3. Document metrics and risk assessment
4. Make approval decision based on findings
5. List action items for next batch

**FAILURE TO RUN CHECKPOINTS = ARCHITECTURAL DRIFT = PROJECT FAILURE**

### 0.3 Gemini API Integration for Architectural Validation

**MANDATORY**: For any architectural C# issues, design patterns, or complex technical decisions:

```bash
# Gemini API Usage:
curl "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-exp:generateContent?key=AIzaSyDP7daxEmHxuSTA3ZObO4Rgkl2HswqpHcs" \
  -H 'Content-Type: application/json' \
  -X POST \
  -d '{"contents":[{"parts":[{"text":"Your architectural C# question here"}]}]}'
```

1. **Consult Gemini API** for expert architectural guidance
2. **Include Gemini's analysis** in decision-making process
3. **Cross-reference** booklet recommendations with Gemini insights
4. **Document** Gemini's architectural recommendations

## Table of Contents

0. [**CRITICAL**: Mandatory Booklet-First Development Protocol](#critical-mandatory-booklet-first-development-protocol)
1. [Core Development Principles](#core-development-principles)
2. [Research-First Mandate](#research-first-mandate)
3. [AIRES Canonical Service Implementation](#aires-canonical-service-implementation)
4. [Method Logging Requirements](#method-logging-requirements)
5. [AIRES Precision Standards](#aires-precision-standards)
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
17. [**HIGH PRIORITY**: Alerting and Monitoring](#high-priority-alerting-and-monitoring)
18. [**HIGH PRIORITY**: API Design Principles](#high-priority-api-design-principles)
19. [**MANDATORY**: Comprehensive Testing](#mandatory-comprehensive-testing)
20. [**MANDATORY**: No Mock Implementations](#mandatory-no-mock-implementations)

-----

## 1. Core Development Principles

### 1.1 AIRES Independence Policy

- **NEVER** use trading platform patterns (CanonicalServiceBase, TradingResult<T>, ITradingLogger)
- **ALWAYS** use AIRES patterns (AIRESServiceBase, AIRESResult<T>, IAIRESLogger)
- **NEVER** create dependencies on external systems except Ollama
- **ALWAYS** maintain AIRES as a standalone system

### 1.2 Zero Mock Implementation Policy

- **NEVER** create mock implementations that pretend to work
- **ALWAYS** implement real functionality or clearly mark as TODO
- **NEVER** use hardcoded delays (Thread.Sleep) to simulate work
- **ALWAYS** provide real progress tracking and telemetry

### 1.3 Research-First Development

- **MANDATORY**: 2-4 hours minimum research before building anything
- **MANDATORY**: Create research reports documenting all findings
- **MANDATORY**: Use AIRES itself for error resolution
- **MANDATORY**: Get approval before implementation

### 1.4 Clean Code Policy

- **MANDATORY**: Remove all dead code after successful implementations
- **NEVER** leave commented-out code in production
- **NEVER** use generic filenames (Program.cs, Helper.cs, Utils.cs)
- **ALWAYS** use descriptive names that reflect functionality

### 1.5 Zero Errors, Zero Warnings Policy (0/0 Policy)

**ABSOLUTE REQUIREMENT**: AIRES follows a strict 0/0 policy:
- **0 Compilation Errors**: Code MUST compile cleanly
- **0 Warnings**: ALL warnings MUST be resolved
- **TreatWarningsAsErrors**: Set to true in all projects
- **No Suppressions Without Documentation**: Any warning suppression must be documented with justification and removal plan

**Enforcement**:
- Build will fail on any warning
- CI/CD pipeline will reject code with warnings
- Code reviews must verify 0/0 compliance

-----

## 2. Research-First Mandate

### 2.1 Pre-Implementation Research Requirements

```markdown
BEFORE WRITING ANY CODE:
1. ✅ Research existing solutions (2-4 hours minimum)
2. ✅ Look for FOSS alternatives (MIT, Apache 2.0, BSD licensed)
3. ✅ Document findings in a research report
4. ✅ Identify industry-standard patterns
5. ✅ Get approval for approach
6. ✅ Only then begin implementation
```

### 2.2 Research Report Template

```markdown
# Research Report: [Feature/Component Name]
Date: [YYYY-MM-DD]
Researcher: [Name/AI]

## Executive Summary
[Brief overview of findings]

## Research Conducted
- [ ] Industry standards reviewed
- [ ] FOSS alternatives evaluated
- [ ] Existing patterns analyzed
- [ ] Similar implementations studied
- [ ] Performance implications considered
- [ ] Security implications reviewed

## Findings
1. **Standard Solutions Found:**
   - [List all relevant standards]
   
2. **FOSS Alternatives:**
   - [List alternatives with licenses]
   
3. **Recommended AIRES Approach:**
   - [Detailed recommendation]
   
4. **Alternatives Considered:**
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
public class MyAIRESService : CanonicalServiceBase, IMyAIRESService
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

### 3.3 AIRES Service Features

All AIRES services provide:
- ✅ Automatic method entry/exit logging
- ✅ Health checks and metrics
- ✅ Proper lifecycle management (Initialize, Start, Stop)
- ✅ AIRESResult<T> pattern implementation
- ✅ Comprehensive error handling
- ✅ Performance tracking
- ✅ Alert integration

-----

## 4. Method Logging Requirements

### 4.1 MANDATORY: IAIRESLogger Implementation

**CRITICAL**: ALL services MUST use `IAIRESLogger` interface, not `ILogger<T>` or `ITradingLogger`.

```csharp
// ❌ WRONG - Using Microsoft ILogger
public class MyService
{
    private readonly ILogger<MyService> _logger; // VIOLATION!
}

// ❌ WRONG - Using ITradingLogger
public class MyService
{
    private readonly ITradingLogger _logger; // VIOLATION!
}

// ✅ CORRECT - Using IAIRESLogger
public class MyService : AIRESServiceBase, IMyService
{
    public MyService(IAIRESLogger logger) 
        : base(logger, nameof(MyService))
    {
        // Base class provides IAIRESLogger functionality
    }
}
```

### 4.2 MANDATORY: Every Method Must Log Entry and Exit

```csharp
public async Task<AIRESResult<ResearchBooklet>> GenerateBookletAsync(string errorFile)
{
    LogMethodEntry(); // MANDATORY
    try
    {
        // Method implementation
        
        LogMethodExit(); // MANDATORY
        return AIRESResult<ResearchBooklet>.Success(booklet);
    }
    catch (Exception ex)
    {
        LogError("Failed to generate booklet", ex);
        LogMethodExit(); // MANDATORY even in error cases
        return AIRESResult<ResearchBooklet>.Failure("BOOKLET_GENERATION_ERROR", "Generation failed", ex);
    }
}
```

### 4.3 MANDATORY: Private Methods Must Also Log

```csharp
private void ProcessInternal(string data)
{
    LogMethodEntry(); // MANDATORY even for private methods
    try
    {
        // Implementation
        LogMethodExit(); // MANDATORY
    }
    catch (Exception ex)
    {
        LogError("Internal processing failed", ex);
        LogMethodExit(); // MANDATORY
        throw;
    }
}
```

-----

## 5. AIRES Precision Standards

### 5.1 Error Processing Precision

- **Error Codes**: Exact match from compiler output
- **Line Numbers**: Precise location tracking
- **File Paths**: Full absolute paths
- **Context**: Sufficient surrounding code

### 5.2 AI Response Handling

- **Timeouts**: 120 seconds per AI model
- **Retries**: 3 attempts with exponential backoff
- **Response Validation**: Verify AI responses are valid
- **Fallback**: Graceful degradation if AI fails

-----

## 6. Error Handling Standards

### 6.1 No Silent Failures Policy

```csharp
// ❌ WRONG - Silent failure
try
{
    ProcessData();
}
catch
{
    // Swallowing exception - NEVER DO THIS
}

// ✅ CORRECT - Comprehensive error handling with alerting
try
{
    ProcessData();
}
catch (OllamaException oex)
{
    LogWarning("Ollama service issue", oex);
    await _alertingService.RaiseAlertAsync(AlertSeverity.Warning, "OllamaClient", oex.Message);
    return AIRESResult.Failure("AI_SERVICE_ERROR", "Ollama unavailable", oex);
}
catch (Exception ex)
{
    LogError("Unexpected error in data processing", ex);
    await _alertingService.RaiseAlertAsync(AlertSeverity.Critical, "DataProcessor", ex.Message);
    return AIRESResult.Failure("PROCESS_ERROR", $"Processing failed: {ex.Message}", ex);
}
```

### 6.2 AIRESResult<T> Pattern

ALL operations MUST return AIRESResult<T>:

```csharp
public async Task<AIRESResult<CompilerError[]>> ParseErrorsAsync(string rawOutput)
{
    LogMethodEntry();
    
    try
    {
        // Validation
        if (string.IsNullOrEmpty(rawOutput))
        {
            LogMethodExit();
            return AIRESResult<CompilerError[]>.Failure(
                "INVALID_INPUT", 
                "Raw output is required");
        }
        
        // Operation
        var errors = ParseCompilerOutput(rawOutput);
        
        LogMethodExit();
        return AIRESResult<CompilerError[]>.Success(errors);
    }
    catch (Exception ex)
    {
        LogError($"Failed to parse compiler output", ex);
        LogMethodExit();
        return AIRESResult<CompilerError[]>.Failure(
            "PARSE_ERROR",
            $"Failed to parse: {ex.Message}", 
            ex);
    }
}
```

-----

## 7. Testing Requirements

### 7.1 Minimum Coverage Requirements

- **Unit Tests**: 80% minimum coverage
- **Integration Tests**: All AI pipeline scenarios
- **System Tests**: End-to-end CLI testing
- **Performance Tests**: Response time validation
- **NO TEST IMPLEMENTATION**: Currently 0% - CRITICAL VIOLATION

### 7.2 Test Structure

```csharp
public class AIResearchOrchestratorServiceTests
{
    [Fact]
    public async Task GenerateBooklet_ValidErrors_ShouldSucceed()
    {
        // Arrange
        var mockOllama = new Mock<IOllamaClient>();
        mockOllama.Setup(x => x.GenerateAsync(It.IsAny<OllamaRequest>()))
                  .ReturnsAsync(new OllamaResponse { /* ... */ });
        
        var service = new AIResearchOrchestratorService(
            _logger, _mediator, mockOllama.Object);
            
        // Act
        var result = await service.GenerateBookletAsync(testErrors);
        
        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().NotBeNull();
        result.Value.Sections.Should().HaveCount(4);
    }
}
```

See: [AIRES_Testing_Requirements.md](AIRES_Testing_Requirements.md)

-----

## 8. AIRES Architecture Standards

### 8.1 Clean Architecture (Mandatory)

```
CLI Layer (Commands)
    ↓
Application Layer (Orchestration, MediatR)
    ↓
Domain Layer (Models, Interfaces)
    ↓
Infrastructure Layer (AI Services, File I/O)
    ↓
Foundation Layer (Base Classes, Logging)
```

### 8.2 Project Structure

```
AIRES/
├── src/
│   ├── AIRES.CLI/              # Command-line interface
│   ├── AIRES.Application/      # Orchestration, handlers
│   ├── AIRES.Core/             # Domain models, interfaces
│   ├── AIRES.Infrastructure/   # AI services, configuration
│   ├── AIRES.Foundation/       # Base classes, results
│   └── AIRES.Watchdog/         # Autonomous monitoring
├── tests/
│   ├── AIRES.Core.Tests/
│   ├── AIRES.Foundation.Tests/
│   └── AIRES.Integration.Tests/
├── docs/
│   ├── MUSTDOs/               # Mandatory standards
│   ├── error-booklets/        # Generated booklets
│   ├── journals/              # Development journals
│   └── checkpoints/           # SCR documents
└── config/
    └── aires.ini              # Configuration
```

### 8.3 Dependency Rules

- ✅ CLI → Application → Core → Infrastructure
- ✅ All → Foundation
- ❌ Core → Application (NEVER)
- ❌ Core → Infrastructure (NEVER)
- ❌ Foundation → Any other (NEVER)

-----

## 9. Performance Requirements

### 9.1 Response Time Targets

- **Error Parsing**: < 100ms
- **AI Model Response**: < 30s per model
- **Total Pipeline**: < 2 minutes for typical batch
- **Booklet Generation**: < 500ms

### 9.2 Resource Limits

- **Memory Usage**: < 500MB normal operation
- **CPU Usage**: < 50% sustained
- **Disk I/O**: Minimize for booklet writes
- **Network**: Local only (Ollama)

-----

## 10. Security Standards

### 10.1 Input Validation

- Sanitize all error file content
- Validate file sizes (max 10MB)
- Check file extensions (.txt, .log only)
- Prevent path traversal attacks

### 10.2 No External Network Access

- **Ollama Only**: Local HTTP to localhost:11434
- **No Cloud APIs**: All AI inference local
- **No Telemetry**: Unless explicitly configured
- **File System**: Restricted to configured directories

-----

## 11. Documentation Requirements

### 11.1 Code Documentation

```csharp
/// <summary>
/// Generates a comprehensive research booklet for compiler errors.
/// </summary>
/// <param name="errorFile">Path to the compiler error output file</param>
/// <returns>An AIRESResult containing the generated booklet or error details</returns>
/// <exception cref="FileNotFoundException">When error file doesn't exist</exception>
/// <remarks>
/// This method orchestrates the 4-stage AI pipeline:
/// 1. Mistral - Documentation research
/// 2. DeepSeek - Context analysis
/// 3. CodeGemma - Pattern validation
/// 4. Gemma2 - Booklet synthesis
/// </remarks>
public async Task<AIRESResult<ResearchBooklet>> GenerateBookletAsync(string errorFile)
```

### 11.2 Required Documentation

- `PRD_AIRES_Product_Requirements_Document.md` - Product vision
- `EDD_AIRES_Engineering_Design_Document.md` - Technical design
- `MANDATORY_DEVELOPMENT_STANDARDS-AIRES-V5.md` - This document
- `AIRES_Testing_Requirements.md` - Test standards
- `AIRES_Alerting_and_Monitoring_Requirements.md` - Alerting design
- `README.md` - Setup and usage

-----

## 12. Code Analysis and Quality

### 12.1 Mandatory Analyzers

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="8.0.0" />
  <PackageReference Include="StyleCop.Analyzers" Version="1.2.0" />
  <PackageReference Include="SonarAnalyzer.CSharp" Version="9.0.0" />
</ItemGroup>
```

### 12.2 Zero Warning Policy

**MANDATORY**: All code MUST compile with zero warnings.

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <WarningLevel>5</WarningLevel>
  <NoWarn></NoWarn>
</PropertyGroup>
```

### 12.3 Current Violations

- **GlobalSuppressions.cs**: Contains 40+ suppressions - TECHNICAL DEBT
- Each suppression MUST have removal plan
- New suppressions require justification

-----

## 13. Standard Tools and Libraries

### 13.1 AIRES-Specific Tools

#### Logging
- **Serilog** - Structured logging
- **IAIRESLogger** - AIRES interface (NO ILogger<T>)

#### AI Integration
- **Ollama HTTP Client** - Local AI inference
- **HttpClient with Polly** - Resilience

#### CLI
- **Spectre.Console** - Rich CLI interface
- **Spectre.Console.Cli** - Command framework

#### Testing
- **xUnit** - Unit testing
- **Moq** - Mocking
- **FluentAssertions** - Assertions
- **WireMock** - HTTP mocking for Ollama

#### Serialization
- **System.Text.Json** - JSON processing

### 13.2 Prohibited Libraries

- ❌ Newtonsoft.Json
- ❌ Any trading platform libraries
- ❌ Cloud AI APIs (use Ollama only)
- ❌ ITradingLogger, TradingResult<T>

-----

## 14. Development Workflow

### 14.1 Pre-Development Checklist

```markdown
- [ ] Research completed (2-4 hours minimum)
- [ ] FOSS alternatives evaluated
- [ ] Research report created and approved
- [ ] AIRES booklet generated for any existing errors
- [ ] Architecture review completed
- [ ] Fix counter initialized [0/10]
```

### 14.2 Development Process

1. **Booklet Generation Phase** (Mandatory)
   - Generate AIRES booklet for any issues
   - Wait for AI team analysis
   - Review and understand recommendations

2. **Implementation Phase**
   - Implement using AIRES canonical patterns
   - Follow all logging requirements
   - NO MOCK IMPLEMENTATIONS
   - Track fixes with counter

3. **Testing Phase**
   - Write unit tests first (TDD)
   - Integration tests for AI pipeline
   - System tests for CLI

4. **Review Phase**
   - Self-review against checklist
   - Status Checkpoint Review at 10 fixes
   - Peer review
   - Zero warnings verification

-----

## 15. Progress Reporting

### 15.1 Real Progress Tracking

```csharp
public async Task<AIRESResult> ProcessBatchAsync(
    string[] errorFiles,
    IProgress<ProcessingProgress> progress)
{
    LogMethodEntry();
    
    var totalCount = errorFiles.Length;
    var processed = 0;
    
    foreach (var file in errorFiles)
    {
        // Real processing - NO FAKE DELAYS
        var result = await ProcessFileAsync(file);
        
        processed++;
        
        var progressInfo = new ProcessingProgress
        {
            TotalItems = totalCount,
            ProcessedItems = processed,
            PercentComplete = (decimal)processed / totalCount * 100,
            CurrentFile = file,
            Stage = "Processing",
            Message = $"Completed {file}"
        };
        
        progress?.Report(progressInfo);
        LogInfo($"Progress: {processed}/{totalCount} files");
    }
    
    LogMethodExit();
    return AIRESResult.Success();
}
```

-----

## 16. **HIGH PRIORITY**: Observability & Distributed Tracing

### 16.1 Comprehensive Telemetry

**MANDATORY**: All services MUST implement comprehensive telemetry:

```csharp
public class OllamaClient : AIRESServiceBase, IOllamaClient
{
    private static readonly ActivitySource ActivitySource = new("AIRES.OllamaClient");
    
    public async Task<AIRESResult<OllamaResponse>> GenerateAsync(OllamaRequest request)
    {
        using var activity = ActivitySource.StartActivity("Ollama.Generate");
        activity?.SetTag("model", request.Model);
        activity?.SetTag("prompt.length", request.Prompt.Length);
        
        LogMethodEntry();
        
        try
        {
            var response = await _httpClient.PostAsync(...);
            activity?.SetTag("response.status", response.StatusCode);
            
            LogMethodExit();
            return AIRESResult<OllamaResponse>.Success(result);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogError("Ollama request failed", ex);
            LogMethodExit();
            throw;
        }
    }
}
```

### 16.2 Required Metrics

- Request rates and latencies
- Error rates by type
- AI model response times
- Booklet generation success rate
- Resource utilization

-----

## 17. **HIGH PRIORITY**: Alerting and Monitoring

### 17.1 Multi-Channel Alerting

**MANDATORY**: Implement IAIRESAlertingService in all components:

```csharp
public interface IAIRESAlertingService
{
    Task RaiseAlertAsync(AlertSeverity severity, string component, string message, Dictionary<string, object>? details = null);
    Task<HealthCheckResult> GetHealthStatusAsync();
}
```

### 17.2 Alert Channels

1. **Console Output** - Structured with severity
2. **Log Files** - JSON format with retention
3. **Alert Files** - For agent monitoring
4. **Windows Event Log** - System integration
5. **Health Endpoint** - /health status

See: [AIRES_Alerting_and_Monitoring_Requirements.md](AIRES_Alerting_and_Monitoring_Requirements.md)

-----

## 18. **HIGH PRIORITY**: API Design Principles

### 18.1 CLI as API

AIRES CLI commands ARE the API:

```bash
# Process specific file
aires process <file> [--output <dir>]

# Start watchdog mode
aires start [--config <path>]

# Check status
aires status [--detailed]

# View/set configuration
aires config show
aires config set <key> <value>
```

### 18.2 Exit Codes

- 0: Success
- 1: General failure
- 2: Invalid arguments
- 3: File not found
- 4: AI service unavailable
- 5: Configuration error

-----

## 19. **MANDATORY**: Comprehensive Testing

### 19.1 Current State: CRITICAL VIOLATION

- **Current Coverage**: 0%
- **Required Coverage**: 80% minimum
- **Status**: NO TESTS EXIST

### 19.2 Required Test Types

1. **Unit Tests** - All public methods
2. **Integration Tests** - AI pipeline
3. **System Tests** - CLI commands
4. **Performance Tests** - Response times

See: [AIRES_Testing_Requirements.md](AIRES_Testing_Requirements.md)

-----

## 20. **MANDATORY**: No Mock Implementations

### 20.1 Current Violations

Based on comprehensive audit:
- **ProcessCommand**: Complete mock with fake progress
- **StartCommand**: Mock delays, no real watchdog
- **StatusCommand**: Hardcoded fake data
- **ConfigCommand**: Hardcoded values

### 20.2 Requirements

- **REAL IMPLEMENTATION ONLY**: No fake progress bars
- **REAL TELEMETRY**: Actual progress tracking
- **REAL ERRORS**: Actual error handling
- **REAL FUNCTIONALITY**: Or clear TODO markers

-----

## 🚨 ENFORCEMENT

### Automated Enforcement

1. **Pre-commit hooks** validate standards compliance
2. **Build fails on warnings** (TreatWarningsAsErrors=true)
3. **No commits without tests** (when implemented)
4. **AIRES booklet** mandatory for all fixes
5. **Status Checkpoint Reviews** every 10 fixes

### Manual Enforcement

1. **Code reviews** must verify:
   - No mock implementations
   - Proper logging (entry/exit)
   - AIRESResult<T> usage
   - No hardcoded values
   - Real functionality

### Violations

- **Mock implementation**: Immediate rejection
- **Missing logging**: Code rejection
- **No booklet**: Fix rejection
- **Failed checkpoint**: Work stoppage

-----

## 📚 Quick Reference Card

```csharp
// Every AIRES service MUST follow this pattern:
public class MyAIRESService : AIRESServiceBase, IMyAIRESService
{
    private readonly IAIRESAlertingService _alerting;
    
    public MyAIRESService(IAIRESLogger logger, IAIRESAlertingService alerting) 
        : base(logger, nameof(MyAIRESService)) 
    {
        _alerting = alerting;
    }
    
    public async Task<AIRESResult<MyResult>> DoSomethingAsync(MyRequest request)
    {
        LogMethodEntry(); // MANDATORY
        
        try
        {
            // Validate
            if (!IsValid(request))
            {
                LogMethodExit();
                return AIRESResult<MyResult>.Failure("VALIDATION_ERROR", "Invalid request");
            }
            
            // Process with real implementation
            var result = await ProcessAsync(request); // NO MOCKS!
            
            // Return
            LogMethodExit(); // MANDATORY
            return AIRESResult<MyResult>.Success(result);
        }
        catch (Exception ex)
        {
            LogError("Operation failed", ex);
            await _alerting.RaiseAlertAsync(AlertSeverity.Warning, ServiceName, ex.Message);
            LogMethodExit(); // MANDATORY
            return AIRESResult<MyResult>.Failure("OPERATION_ERROR", $"Operation failed: {ex.Message}", ex);
        }
    }
}
```

## 📋 AIRES Development Checklist

```markdown
### Before ANY Code Changes:
- [ ] Fix counter initialized: [0/10]
- [ ] AIRES ready to process errors
- [ ] Booklet generation tested
- [ ] All AIRES patterns reviewed
- [ ] IAIRESLogger interface confirmed
- [ ] AIRESResult<T> pattern understood
- [ ] Zero warning policy configured
- [ ] NO MOCK IMPLEMENTATIONS planned

### During Development:
- [ ] Generate booklet for EVERY error/issue
- [ ] Wait for AI team analysis
- [ ] Read and understand booklet recommendations
- [ ] Apply fixes ONLY based on booklet guidance
- [ ] Track fixes: 📊 Fix Counter: [X/10]
- [ ] Perform SCR at 10 fixes
- [ ] Reset counter after checkpoint
- [ ] Write tests for new code

### Before Commit:
- [ ] Zero compilation errors
- [ ] Zero warnings (TreatWarningsAsErrors=true)
- [ ] All tests passing (when implemented)
- [ ] No mock implementations
- [ ] No hardcoded values
- [ ] Proper logging everywhere
- [ ] Real functionality only
- [ ] Documentation updated
```

-----

## 🔗 Related Documents

- [PRD_AIRES_Product_Requirements_Document.md](../PRD_AIRES_Product_Requirements_Document.md)
- [EDD_AIRES_Engineering_Design_Document.md](../EDD_AIRES_Engineering_Design_Document.md)
- [AIRES_Testing_Requirements.md](AIRES_Testing_Requirements.md)
- [AIRES_Alerting_and_Monitoring_Requirements.md](AIRES_Alerting_and_Monitoring_Requirements.md)
- [AIRES_Status_Checkpoint_Review_Template.md](../AIRES_Status_Checkpoint_Review_Template.md)
- [Gemini_AI_Consultation_Guide.md](../Gemini_AI_Consultation_Guide.md)

-----

**Remember: These standards are MANDATORY. No exceptions. No excuses. No mocks. No fixes without booklets.**

*Last reviewed: 2025-07-13*
*Next review: 2025-02-13*
*Version: 5.0 (AIRES-Specific)*