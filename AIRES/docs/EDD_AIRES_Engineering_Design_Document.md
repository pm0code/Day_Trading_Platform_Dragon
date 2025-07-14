# AIRES Engineering Design Document (EDD)

**Version**: 2.0  
**Date**: 2025-07-13  
**Status**: In Development  
**Product**: AI Error Resolution System (AIRES)

## 1. Introduction

### 1.1 Purpose
This document provides the detailed technical design for AIRES, including architecture, implementation details, and technical decisions.

### 1.2 Scope
Covers all technical aspects of AIRES as a standalone Windows desktop application for AI-powered compiler error resolution.

### 1.3 Audience
- Development team
- Technical reviewers
- System architects
- QA engineers

## 2. System Architecture

### 2.1 High-Level Architecture

```
┌────────────────────────────────────────────────────────────┐
│                         CLI Layer                           │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐     │
│  │  Start   │ │ Process  │ │  Status  │ │  Config  │     │
│  │ Command  │ │ Command  │ │ Command  │ │ Command  │     │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘     │
└────────────────────────────┬───────────────────────────────┘
                             │
┌────────────────────────────▼───────────────────────────────┐
│                    Application Layer                        │
│  ┌─────────────────────┐ ┌─────────────────────────────┐ │
│  │   Orchestrator      │ │     MediatR Pipeline        │ │
│  │   Service           │ │  ┌─────┐ ┌─────┐ ┌─────┐  │ │
│  │                     │ │  │Parse│→│Analyze│→│Generate││ │
│  │                     │ │  └─────┘ └─────┘ └─────┘  │ │
│  └─────────────────────┘ └─────────────────────────────┘ │
└────────────────────────────┬───────────────────────────────┘
                             │
┌────────────────────────────▼───────────────────────────────┐
│                      Domain Layer                           │
│  ┌─────────────┐ ┌──────────────┐ ┌───────────────────┐  │
│  │Value Objects│ │  Interfaces  │ │   Domain Events   │  │
│  └─────────────┘ └──────────────┘ └───────────────────┘  │
└────────────────────────────┬───────────────────────────────┘
                             │
┌────────────────────────────▼───────────────────────────────┐
│                  Infrastructure Layer                       │
│  ┌──────────────┐ ┌──────────────┐ ┌─────────────────┐   │
│  │ AI Services  │ │Configuration │ │    Alerting     │   │
│  │  (Ollama)    │ │  Provider    │ │    Service      │   │
│  └──────────────┘ └──────────────┘ └─────────────────┘   │
└────────────────────────────┬───────────────────────────────┘
                             │
┌────────────────────────────▼───────────────────────────────┐
│                    Foundation Layer                         │
│  ┌──────────────┐ ┌──────────────┐ ┌─────────────────┐   │
│  │AIRESService  │ │ AIRESResult  │ │  IAIRESLogger   │   │
│  │    Base      │ │   Pattern    │ │                 │   │
│  └──────────────┘ └──────────────┘ └─────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 Component Design

#### 2.2.1 CLI Layer
```csharp
// Command pattern implementation
public class ProcessCommand : AsyncCommand<ProcessCommand.Settings>
{
    private readonly IAIResearchOrchestratorService _orchestrator;
    private readonly IAIRESLogger _logger;
    
    public override async Task<int> ExecuteAsync(...)
    {
        LogMethodEntry();
        // Implementation
        LogMethodExit();
    }
}
```

#### 2.2.2 Application Layer
```csharp
// CQRS pattern with MediatR
public class ParseCompilerErrorsCommand : IRequest<ParseCompilerErrorsResponse>
{
    public string RawCompilerOutput { get; }
}

public class ParseCompilerErrorsHandler : IRequestHandler<...>
{
    public async Task<ParseCompilerErrorsResponse> Handle(...)
    {
        // Implementation with comprehensive logging
    }
}
```

#### 2.2.3 Domain Layer
```csharp
// Value objects with immutability
public record CompilerError(
    string Code,
    string Message,
    ErrorLocation Location,
    string Severity
);

// Rich domain interfaces
public interface IErrorDocumentationAIModel
{
    Task<AIRESResult<DocumentationFinding>> AnalyzeAsync(
        CompilerError error,
        string codeContext
    );
}
```

#### 2.2.4 Infrastructure Layer
```csharp
// AI service implementation
public class OllamaClient : AIRESServiceBase, IOllamaClient
{
    protected override async Task<AIRESResult<bool>> OnInitializeAsync()
    {
        LogMethodEntry();
        // Ollama connection setup
        LogMethodExit();
    }
}
```

#### 2.2.5 Foundation Layer
```csharp
// Canonical base class
public abstract class AIRESServiceBase : IDisposable
{
    protected void LogMethodEntry([CallerMemberName] string methodName = "")
    {
        Logger.LogDebug($"[{ServiceName}] Entering {methodName}");
    }
    
    protected void LogMethodExit([CallerMemberName] string methodName = "")
    {
        Logger.LogDebug($"[{ServiceName}] Exiting {methodName}");
    }
}
```

## 3. Data Flow Design

### 3.1 Error Processing Pipeline

```
Error File → Parse → Validate → AI Analysis → Synthesis → Booklet
    │          │         │           │            │          │
    └──────────┴─────────┴───────────┴────────────┴──────────┘
                        Comprehensive Logging
```

### 3.2 AI Pipeline Stages

1. **Mistral Stage**: Documentation research
   - Input: Compiler errors
   - Output: Official documentation findings
   - Timeout: 120 seconds

2. **DeepSeek Stage**: Context analysis
   - Input: Errors + code context
   - Output: Contextual understanding
   - Timeout: 120 seconds

3. **CodeGemma Stage**: Pattern validation
   - Input: Previous findings
   - Output: Best practice validation
   - Timeout: 120 seconds

4. **Gemma2 Stage**: Booklet synthesis
   - Input: All findings
   - Output: Complete booklet
   - Timeout: 120 seconds

## 4. Technical Design Decisions

### 4.1 Canonical Patterns

#### Decision: AIRES-Specific Patterns
- **Rationale**: Complete independence from trading platform
- **Implementation**: 
  - AIRESServiceBase (not CanonicalServiceBase)
  - AIRESResult<T> (not TradingResult<T>)
  - IAIRESLogger (not ITradingLogger)

#### Decision: Comprehensive Logging
- **Rationale**: Full observability required
- **Implementation**:
  - LogMethodEntry/Exit in ALL methods
  - Structured logging with Serilog
  - Correlation IDs for request tracking

### 4.2 Architecture Patterns

#### Decision: CQRS with MediatR
- **Rationale**: Clear separation of concerns
- **Benefits**:
  - Testable handlers
  - Pipeline behaviors
  - Decoupled components

#### Decision: Repository Pattern Avoided
- **Rationale**: Simple file-based storage
- **Implementation**: Direct file I/O in services

### 4.3 Error Handling

#### Decision: Result Pattern
- **Rationale**: Explicit error handling
- **Implementation**:
```csharp
public class AIRESResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }
}
```

## 5. Implementation Details

### 5.1 Dependency Injection

```csharp
services.AddAIRESFoundation();
services.AddAIRESCore();
services.AddAIRESInfrastructure();
services.AddAIRESApplication();
services.AddAIRESWatchdog();
services.AddAIRESAlerting(); // New
```

### 5.2 Configuration Management

```ini
[Directories]
InputDirectory = ./input
OutputDirectory = ./docs/error-booklets
TempDirectory = ./temp

[AI_Services]
OllamaBaseUrl = http://localhost:11434
MistralModel = mistral:latest
DeepSeekModel = deepseek-coder:latest
CodeGemmaModel = codegemma:latest
Gemma2Model = gemma2:latest

[Alerting]
EnableFileAlerts = true
AlertDirectory = ./alerts
EnableWindowsEventLog = true
```

### 5.3 Logging Configuration

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.File(
        new JsonFormatter(),
        "logs/aires-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30
    )
    .CreateLogger();
```

## 6. Security Design

### 6.1 Input Validation
- Sanitize error file content
- Validate file size limits
- Check file extensions
- Path traversal prevention

### 6.2 Process Isolation
- Run with least privileges
- No network access (Ollama is local)
- Secure file permissions
- No sensitive data in logs

## 7. Performance Design

### 7.1 Optimization Strategies
- Parallel AI model calls where possible
- Streaming for large files
- Connection pooling for Ollama
- Async/await throughout

### 7.2 Resource Management
- Dispose pattern for all resources
- Memory stream usage
- File handle management
- Thread pool optimization

## 8. Testing Strategy

### 8.1 Unit Testing
```csharp
[Fact]
public async Task ParseCompilerErrors_ValidInput_ReturnsErrors()
{
    // Arrange
    var handler = new ParseCompilerErrorsHandler(_logger);
    
    // Act
    var result = await handler.Handle(command, CancellationToken.None);
    
    // Assert
    result.Should().NotBeNull();
    result.Errors.Should().HaveCount(5);
}
```

### 8.2 Integration Testing
- Mock Ollama responses with WireMock
- Test complete pipeline
- Verify file operations
- Test error scenarios

### 8.3 System Testing
- End-to-end CLI testing
- Performance benchmarks
- Load testing
- Stress testing

## 9. Monitoring and Alerting Design

### 9.1 Health Checks
```csharp
public class AIRESHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(...)
    {
        // Check Ollama connectivity
        // Check disk space
        // Check memory usage
        // Return aggregated health
    }
}
```

### 9.2 Metrics Collection
- Processing time per stage
- Success/failure rates
- Resource utilization
- Error frequencies

### 9.3 Alert Channels
- Console output (structured)
- Log files (JSON format)
- Alert files (for agents)
- Windows Event Log
- Future: Webhooks

## 10. Deployment Design

### 10.1 Package Structure
```
aires/
├── aires.exe
├── aires.ini
├── libs/
│   └── *.dll
├── logs/
├── input/
├── output/
└── alerts/
```

### 10.2 Installation
- Self-contained .NET 8 deployment
- No GAC dependencies
- xcopy deployment supported
- Configuration via INI file

## 11. Current Implementation Gaps

Based on comprehensive audit findings:

### 11.1 Critical Gaps
1. **CLI Commands**: All commands are mocks with no real implementation
2. **Logging**: Missing LogMethodEntry/Exit in many components
3. **Testing**: Zero test implementations
4. **Alerting**: Not implemented
5. **Telemetry**: No OpenTelemetry integration

### 11.2 Technical Debt
- 273 GlobalSuppressions for warnings
- Generic filenames (Program.cs)
- Hardcoded values in commands
- Missing error handling
- No performance optimization

## 12. Future Enhancements

### 12.1 Short Term
- Implement real CLI commands
- Add comprehensive logging
- Create test suite
- Implement alerting

### 12.2 Long Term
- Web dashboard
- Custom model training
- IDE integration
- Multi-language support

## 13. References

- PRD_AIRES_Product_Requirements_Document.md
- MANDATORY_DEVELOPMENT_STANDARDS-AIRES-V5.md
- AIRES_Testing_Requirements.md
- AIRES_Alerting_and_Monitoring_Requirements.md

---

**Document History**
- v1.0 (2025-01-12): Initial draft
- v2.0 (2025-07-13): Complete rewrite with gap analysis