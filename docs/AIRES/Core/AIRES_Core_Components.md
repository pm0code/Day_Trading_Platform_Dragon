# AIRES Core Components Documentation

**Version**: 3.0  
**Last Updated**: 2025-01-13  
**Author**: tradingagent  
**Status**: Complete Component Reference

## Table of Contents

1. [Component Overview](#component-overview)
2. [Domain Components](#domain-components)
3. [Infrastructure Services](#infrastructure-services)
4. [AI Pipeline Components](#ai-pipeline-components)
5. [Application Services](#application-services)
6. [Configuration Components](#configuration-components)
7. [Data Access Components](#data-access-components)
8. [Messaging Components](#messaging-components)

## Component Overview

AIRES consists of carefully designed components following Domain-Driven Design principles. Each component has a specific responsibility and clear boundaries.

### Component Categories

| Category | Purpose | Key Components |
|----------|---------|----------------|
| Domain | Business logic and rules | ErrorBatch, ResearchBooklet |
| Infrastructure | External integrations | AI services, Kafka, Database |
| Application | Use case orchestration | AutonomousErrorResolutionService |
| Configuration | System settings | WatchdogConfiguration |
| Data Access | Persistence layer | Repositories, DbContext |
| Messaging | Async communication | Kafka producers/consumers |

## Domain Components

### 1. ErrorBatch Aggregate

**Location**: `/Domain/Aggregates/ErrorBatch.cs`

```csharp
public class ErrorBatch
{
    public string Id { get; private set; }
    public string SourceFile { get; private set; }
    public DateTime DetectedAt { get; private set; }
    public string BuildOutputContent { get; private set; }
    public IReadOnlyList<CompilerError> Errors { get; private set; }
    public ProcessingStatus Status { get; private set; }
    public string? ProcessingNotes { get; private set; }
    
    // Business methods
    public void AddError(CompilerError error)
    public void StartProcessing()
    public void CompleteProcessing()
    public void FailProcessing(string reason)
}
```

**Purpose**: Represents a batch of related compiler errors from a single build output.

**Key Features**:
- Immutable after creation (except status)
- Enforces business rules at aggregate boundary
- Tracks processing lifecycle
- Maintains error collection integrity

### 2. ResearchBooklet Aggregate

**Location**: `/Domain/Aggregates/ResearchBooklet.cs`

```csharp
public class ResearchBooklet
{
    public string Id { get; private set; }
    public string ErrorBatchId { get; private set; }
    public DateTime GeneratedAt { get; private set; }
    public BookletStatus Status { get; private set; }
    public string ExecutiveSummary { get; private set; }
    public IReadOnlyList<CompilerError> CompilerErrors { get; private set; }
    public IReadOnlyList<AIResearchFinding> AIFindings { get; private set; }
    public ArchitecturalGuidance? ArchitecturalGuidance { get; private set; }
    public IReadOnlyList<ImplementationRecommendation> Recommendations { get; private set; }
    
    // Lifecycle methods
    public void Submit()
    public void MarkUnderReview(string reviewerId)
    public void Approve(string reviewerId, string comments)
    public void Reject(string reviewerId, string reason)
}
```

**Purpose**: Complete research output from AI analysis with review lifecycle.

**Key Features**:
- Comprehensive error analysis results
- Review workflow support
- Immutable findings collection
- Architectural compliance tracking

### 3. Value Objects

#### CompilerError

```csharp
public record CompilerError(
    string ErrorCode,
    string Description,
    ErrorLocation Location,
    string Severity,
    string? AdditionalInfo
)
{
    public bool IsCritical => Severity == "error";
    public bool IsWarning => Severity == "warning";
}
```

#### ErrorLocation

```csharp
public record ErrorLocation(
    string FilePath,
    int LineNumber,
    int? ColumnNumber = null
)
{
    public string GetRelativePath(string basePath) => 
        Path.GetRelativePath(basePath, FilePath);
}
```

#### AIResearchFinding (Base)

```csharp
public abstract record AIResearchFinding(
    string ModelName,
    DateTime GeneratedAt,
    double ConfidenceScore
);
```

### 4. Domain Services

#### ErrorParserService

**Location**: `/Domain/Services/ErrorParserService.cs`

```csharp
public class ErrorParserService : CanonicalToolServiceBase
{
    public ErrorBatch ParseBuildOutput(string filePath, string content)
    {
        // Regex patterns for different compiler outputs
        // Supports: MSBuild, dotnet CLI, Visual Studio
        // Returns structured ErrorBatch
    }
}
```

**Supported Error Formats**:
- CS#### errors (C# compiler)
- MSB#### errors (MSBuild)
- NU#### errors (NuGet)
- CA#### warnings (Code Analysis)

#### AIResearchOrchestratorService

**Location**: `/Domain/Services/AIResearchOrchestratorService.cs`

```csharp
public class AIResearchOrchestratorService : CanonicalToolServiceBase
{
    public async Task<ResearchBooklet> OrchestrateResearchAsync(
        ErrorBatch errorBatch)
    {
        // Coordinates AI pipeline
        // Manages parallel and sequential execution
        // Aggregates findings into booklet
    }
}
```

**Orchestration Flow**:
1. Parallel AI analysis (Mistral, DeepSeek, CodeGemma)
2. Sequential synthesis (Gemma2)
3. Booklet assembly
4. Quality validation

## Infrastructure Services

### 1. AI Model Services

#### MistralDocumentationService

**Location**: `/Infrastructure/AI/MistralDocumentationService.cs`

```csharp
public class MistralDocumentationService : CanonicalToolServiceBase
{
    private readonly IOllamaClient _ollamaClient;
    
    public async Task<ErrorDocumentationFinding> AnalyzeErrorAsync(
        CompilerError error)
    {
        // Queries Microsoft documentation
        // Extracts official solutions
        // Returns structured findings
    }
}
```

**Key Features**:
- Ollama integration for local inference
- Structured prompt engineering
- JSON response parsing
- Retry logic with backoff

#### DeepSeekContextService

**Location**: `/Infrastructure/AI/DeepSeekContextService.cs`

```csharp
public class DeepSeekContextService : CanonicalToolServiceBase
{
    private readonly HttpClient _httpClient;
    
    public async Task<ContextAnalysisFinding> AnalyzeContextAsync(
        CompilerError error,
        ErrorDocumentationFinding? priorFindings)
    {
        // Analyzes code context
        // Provides debugging insights
        // Considers prior findings
    }
}
```

**Analysis Capabilities**:
- Code pattern recognition
- Context extraction
- Debugging approach suggestions
- Root cause analysis

#### CodeGemmaPatternService

**Location**: `/Infrastructure/AI/CodeGemmaPatternService.cs`

```csharp
public class CodeGemmaPatternService : CanonicalToolServiceBase
{
    private readonly IGeminiClient _geminiClient;
    
    public async Task<PatternValidationFinding> ValidatePatternsAsync(
        CompilerError error,
        List<AIResearchFinding> priorFindings)
    {
        // Validates against canonical patterns
        // Checks architectural compliance
        // Suggests pattern-based solutions
    }
}
```

**Validation Checks**:
- Canonical service patterns
- Financial precision requirements
- Error handling patterns
- Logging compliance

#### Gemma2BookletService

**Location**: `/Infrastructure/AI/Gemma2BookletService.cs`

```csharp
public class Gemma2BookletService : CanonicalToolServiceBase
{
    private readonly IGeminiClient _geminiClient;
    
    public async Task<BookletSynthesisFinding> SynthesizeBookletAsync(
        ErrorBatch errorBatch,
        List<AIResearchFinding> allFindings)
    {
        // Synthesizes all findings
        // Creates executive summary
        // Generates recommendations
    }
}
```

**Synthesis Process**:
- Cross-reference all findings
- Identify common patterns
- Prioritize solutions
- Generate actionable guidance

### 2. External Integration Clients

#### OllamaClient

**Location**: `/Clients/OllamaClient.cs`

```csharp
public class OllamaClient : CanonicalToolServiceBase, IOllamaClient
{
    private readonly HttpClient _httpClient;
    
    public async Task<OllamaGenerateResponse> GenerateAsync(
        OllamaGenerateRequest request)
    {
        // Communicates with Ollama server
        // Handles streaming responses
        // Manages model loading
    }
}
```

**Configuration**:
- Base URL: `http://localhost:11434`
- Timeout: 120 seconds
- Retry policy: 3 attempts
- Models: mistral:7b-instruct-q4_K_M

#### GeminiClient

**Location**: `/Clients/GeminiClient.cs`

```csharp
public interface IGeminiClient
{
    Task<string> GenerateContentAsync(
        string prompt,
        double temperature = 0.5,
        int maxOutputTokens = 8192);
}
```

**Configuration**:
- API Key: Environment variable
- Model: gemini-2.5-flash
- Rate limiting: 60 RPM
- Token limits: 8192 output

## Application Services

### AutonomousErrorResolutionService

**Location**: `/Services/AutonomousErrorResolutionService.cs`

```csharp
public class AutonomousErrorResolutionService : 
    CanonicalToolServiceBase, IHostedService
{
    private FileSystemWatcher? _fileWatcher;
    private readonly ConcurrentQueue<string> _processingQueue;
    private readonly ConcurrentDictionary<string, FileTrackingInfo> _fileStates;
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Initialize file watcher
        // Start background processing
        // Enable health monitoring
    }
}
```

**Key Responsibilities**:
- File system monitoring
- Queue management
- State tracking
- Health monitoring

**File State Machine**:
```
Unknown → Acquired → Detected → InProgress → Completed → Archived
                ↓                    ↓            ↓
            TransientError ←─────── Failed → ErrorArchived
```

### FileProcessor

**Location**: `/Services/FileProcessor.cs`

```csharp
public class FileProcessor : CanonicalToolServiceBase, IFileProcessor
{
    public async Task<ToolResult> ProcessFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        // Read file content
        // Parse errors
        // Orchestrate AI pipeline
        // Generate booklet
        // Archive file
    }
}
```

**Processing Steps**:
1. Validate file exists and is readable
2. Parse compiler errors
3. Create error batch
4. Execute AI pipeline
5. Generate markdown booklet
6. Save to output directory
7. Move original to archive

## Configuration Components

### WatchdogConfiguration

**Location**: `/Configuration/WatchdogConfiguration.cs`

```csharp
public class WatchdogConfiguration : 
    CanonicalToolServiceBase, IWatchdogConfiguration
{
    public string InputDirectory { get; private set; }
    public string OutputDirectory { get; private set; }
    public bool EnableAutonomousMonitoring { get; private set; }
    public string FileFilter { get; private set; } = "*.txt";
    public int MaxConcurrentProcessing { get; private set; } = 1;
}
```

**Configuration Sources**:
1. Primary: `/AI_Codebase_Watchdog_System/config/watchdog.ini`
2. Secondary: `appsettings.json`
3. Environment variables
4. Default values

### Service Registration Extensions

Each major component has a registration extension:

```csharp
public static class MistralWorkerServiceExtensions
{
    public static IServiceCollection AddMistralWorkerService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register Mistral-specific services
        // Configure Kafka consumer
        // Set up health checks
    }
}
```

## Data Access Components

### AiresDbContext

**Location**: `/Data/AiresDbContext.cs`

```csharp
public class AiresDbContext : DbContext
{
    public DbSet<FileProcessingRecord> FileProcessingRecords { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure entity mappings
        // Set up indexes
        // Define constraints
    }
}
```

**Entities**:
- FileProcessingRecord: Tracks file processing state
- OutboxMessage: Reliable message delivery

### Repositories

#### FileProcessingRepository

```csharp
public class FileProcessingRepository : IFileProcessingRepository
{
    public async Task<FileProcessingRecord?> GetByFileNameAsync(
        string fileName)
    public async Task<FileProcessingRecord> CreateAsync(
        FileProcessingRecord record)
    public async Task UpdateStateAsync(
        string fileName, 
        FileProcessingState newState)
}
```

#### OutboxRepository

```csharp
public class OutboxRepository : IOutboxRepository
{
    public async Task<List<OutboxMessage>> GetUnprocessedMessagesAsync(
        string processorId, 
        int batchSize)
    public async Task MarkAsProcessedAsync(
        Guid messageId, 
        string processorId)
}
```

## Messaging Components

### Kafka Infrastructure

#### KafkaProducerService

**Location**: `/Infrastructure/Messaging/KafkaProducerService.cs`

```csharp
public class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    
    public async Task ProduceAsync<T>(
        string topic, 
        string key, 
        T value)
    {
        // Serialize to JSON
        // Send to Kafka
        // Handle delivery reports
    }
}
```

**Configuration**:
- Bootstrap servers: localhost:9092
- Acks: All
- Retries: 3
- Batch size: 16384

#### KafkaConsumerService

**Location**: `/Infrastructure/Messaging/KafkaConsumerService.cs`

```csharp
public class KafkaConsumerService : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // Subscribe to topics
        // Consume messages
        // Process with handler
        // Commit offsets
    }
}
```

**Consumer Groups**:
- mistral-worker-group
- deepseek-worker-group
- codegemma-worker-group
- gemma2-worker-group

### Message Types

```csharp
public record FileAcquiredMessage(
    string FileName,
    string FilePath,
    DateTime AcquiredAt);

public record ErrorAnalysisRequest(
    string ErrorBatchId,
    List<CompilerError> Errors);

public record AIAnalysisCompleted(
    string ErrorBatchId,
    string Stage,
    AIResearchFinding Finding);

public record BookletGenerationRequest(
    string ErrorBatchId,
    List<AIResearchFinding> AllFindings);
```

## Component Interactions

### Startup Sequence

```
1. Configuration Loading
   ├─ Read watchdog.ini
   ├─ Load appsettings.json
   └─ Apply environment overrides

2. Service Registration
   ├─ Register domain services
   ├─ Register infrastructure
   ├─ Configure Kafka
   └─ Set up health checks

3. Database Migration
   ├─ Apply pending migrations
   ├─ Verify connection
   └─ Initialize repositories

4. Background Service Start
   ├─ Start file watcher
   ├─ Start Kafka consumers
   ├─ Start outbox relayer
   └─ Enable health monitoring
```

### Request Flow

```
File Created → FileWatcher → Queue → FileProcessor
                                          ↓
                                    ErrorParser
                                          ↓
                                    AI Orchestrator
                                    ↙    ↓    ↘
                            Mistral  DeepSeek  CodeGemma
                                    ↘    ↓    ↙
                                      Gemma2
                                        ↓
                                 Booklet Generator
                                        ↓
                                   File Archive
```

---

**Next**: [Workflow Guide](AIRES_Workflow_Guide.md)