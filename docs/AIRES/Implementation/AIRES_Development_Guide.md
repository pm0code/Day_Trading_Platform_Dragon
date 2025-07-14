# AIRES Development Guide

**Version**: 3.0  
**Last Updated**: 2025-01-13  
**Author**: tradingagent  
**Status**: Complete Development Reference

## Table of Contents

1. [Development Overview](#development-overview)
2. [Setting Up Development Environment](#setting-up-development-environment)
3. [AIRES Architecture Patterns](#aires-architecture-patterns)
4. [Adding New AI Models](#adding-new-ai-models)
5. [Extending Error Parsers](#extending-error-parsers)
6. [Creating New Workers](#creating-new-workers)
7. [Testing Guidelines](#testing-guidelines)
8. [Debugging AIRES](#debugging-aires)

## Development Overview

AIRES follows strict development standards to maintain quality and consistency. All development must adhere to:

1. **Domain-Driven Design (DDD)** principles
2. **Canonical Service Patterns**
3. **AIRES-specific standards** (See MANDATORY_DEVELOPMENT_STANDARDS-AIRES-V1.md)
4. **Test-Driven Development (TDD)**

### Key Development Principles

- **Research First**: Understand before implementing
- **Quality Over Speed**: No rushed solutions
- **Canonical Patterns**: Follow established patterns
- **Comprehensive Testing**: 80%+ coverage minimum

## Setting Up Development Environment

### Prerequisites

```bash
# Required Software
- .NET 8.0 SDK or later
- Docker & Docker Compose
- PostgreSQL 15+
- Kafka (via Docker)
- Ollama (for local AI models)
- VS Code or Visual Studio 2022

# Required API Keys
- Gemini API Key
- DeepSeek API Key (optional)
```

### Initial Setup

1. **Clone Repository**
   ```bash
   git clone [repository-url]
   cd MarketAnalyzer/DevTools/BuildTools
   ```

2. **Install Dependencies**
   ```bash
   dotnet restore
   dotnet tool restore
   ```

3. **Set Up Infrastructure**
   ```bash
   # Start PostgreSQL and Kafka
   docker-compose -f docker/docker-compose.yml up -d
   
   # Start Ollama
   ollama serve
   
   # Pull required model
   ollama pull mistral:7b-instruct-q4_K_M
   ```

4. **Configure Environment**
   ```bash
   # Create .env file
   echo "GEMINI_API_KEY=your-key" > .env
   echo "DEEPSEEK_API_KEY=your-key" >> .env
   
   # Or export directly
   export GEMINI_API_KEY="your-key"
   export DEEPSEEK_API_KEY="your-key"
   ```

5. **Initialize Database**
   ```bash
   cd src
   dotnet ef database update
   ```

6. **Verify Setup**
   ```bash
   dotnet build
   dotnet test
   dotnet run -- --validate-config
   ```

## AIRES Architecture Patterns

### 1. Canonical Service Pattern

All AIRES services MUST extend the appropriate base class:

```csharp
public class MyAIRESService : CanonicalToolServiceBase, IMyAIRESService
{
    public MyAIRESService(ILogger<MyAIRESService> logger) 
        : base(logger, nameof(MyAIRESService))
    {
        LogMethodEntry();
        // Constructor logic
        LogMethodExit();
    }
    
    public async Task<ToolResult<MyResult>> ProcessAsync(MyRequest request)
    {
        LogMethodEntry();
        try
        {
            // Validation
            if (!IsValid(request))
            {
                LogMethodExit();
                return ToolResult<MyResult>.Failure(
                    "Invalid request", 
                    "VALIDATION_ERROR");
            }
            
            // Processing logic
            var result = await DoWorkAsync(request);
            
            LogMethodExit();
            return ToolResult<MyResult>.Success(result);
        }
        catch (Exception ex)
        {
            LogError("Processing failed", ex);
            LogMethodExit();
            return ToolResult<MyResult>.Failure(
                $"Processing failed: {ex.Message}", 
                "PROCESSING_ERROR");
        }
    }
}
```

### 2. AI Model Service Pattern

```csharp
public class NewAIModelService : CanonicalToolServiceBase, IAIModelService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    
    public async Task<AIResearchFinding> AnalyzeAsync(
        CompilerError error,
        List<AIResearchFinding>? priorFindings = null)
    {
        LogMethodEntry();
        
        try
        {
            // Build prompt
            var prompt = BuildPrompt(error, priorFindings);
            
            // Call AI model
            var response = await CallAIModelAsync(prompt);
            
            // Parse response
            var finding = ParseResponse(response);
            
            LogMethodExit();
            return finding;
        }
        catch (Exception ex)
        {
            LogError($"AI analysis failed for {error.ErrorCode}", ex);
            LogMethodExit();
            throw new AIAnalysisException(
                $"Failed to analyze {error.ErrorCode}", ex);
        }
    }
    
    private string BuildPrompt(
        CompilerError error, 
        List<AIResearchFinding>? priorFindings)
    {
        var promptBuilder = new StringBuilder();
        
        // System prompt
        promptBuilder.AppendLine(_configuration["AIModels:NewModel:SystemPrompt"]);
        
        // Error context
        promptBuilder.AppendLine($"Error Code: {error.ErrorCode}");
        promptBuilder.AppendLine($"Description: {error.Description}");
        promptBuilder.AppendLine($"Location: {error.Location}");
        
        // Prior findings
        if (priorFindings?.Any() == true)
        {
            promptBuilder.AppendLine("\nPrior Analysis:");
            foreach (var finding in priorFindings)
            {
                promptBuilder.AppendLine($"- {finding.Summary}");
            }
        }
        
        // Response format
        promptBuilder.AppendLine("\nProvide analysis in JSON format:");
        promptBuilder.AppendLine(GetResponseSchema());
        
        return promptBuilder.ToString();
    }
}
```

### 3. Worker Service Pattern

```csharp
public class NewModelWorkerService : BackgroundService
{
    private readonly IKafkaConsumerService _consumer;
    private readonly INewModelService _modelService;
    private readonly IKafkaProducerService _producer;
    private readonly ILogger<NewModelWorkerService> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting NewModelWorkerService");
        
        await _consumer.ConsumeAsync<ErrorAnalysisRequest>(
            "ai-input-topic",
            "new-model-group",
            async (message) => await ProcessMessageAsync(message),
            stoppingToken);
    }
    
    private async Task ProcessMessageAsync(ErrorAnalysisRequest request)
    {
        try
        {
            // Process through AI model
            var finding = await _modelService.AnalyzeAsync(request.Error);
            
            // Publish result
            await _producer.ProduceAsync(
                "ai-output-topic",
                request.ErrorBatchId,
                new AIAnalysisCompleted
                {
                    ErrorBatchId = request.ErrorBatchId,
                    Stage = "NewModel",
                    Finding = finding
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to process error {ErrorCode}", 
                request.Error.ErrorCode);
            
            // Send to dead letter queue
            await _producer.ProduceAsync(
                "aires-dead-letter-queue",
                request.ErrorBatchId,
                new ProcessingFailed
                {
                    ErrorBatchId = request.ErrorBatchId,
                    Stage = "NewModel",
                    Reason = ex.Message
                });
        }
    }
}
```

## Adding New AI Models

### Step 1: Define Model Configuration

Add to `appsettings.json`:

```json
{
  "AIModels": {
    "NewModel": {
      "BaseUrl": "https://api.newmodel.com/v1",
      "ApiKey": "${NEW_MODEL_API_KEY}",
      "Model": "model-name",
      "Temperature": 0.4,
      "MaxTokens": 4096,
      "SystemPrompt": "You are an expert at...",
      "ResponseFormat": "json"
    }
  }
}
```

### Step 2: Create Finding Type

```csharp
public record NewModelFinding : AIResearchFinding
{
    public string Analysis { get; init; }
    public List<string> KeyInsights { get; init; }
    public Dictionary<string, object> Metadata { get; init; }
    
    public NewModelFinding(
        string analysis,
        List<string> keyInsights,
        double confidenceScore)
        : base("NewModel", DateTime.UtcNow, confidenceScore)
    {
        Analysis = analysis;
        KeyInsights = keyInsights;
        Metadata = new Dictionary<string, object>();
    }
}
```

### Step 3: Implement Service

```csharp
public interface INewModelService
{
    Task<NewModelFinding> AnalyzeAsync(
        CompilerError error,
        List<AIResearchFinding>? priorFindings = null);
}

public class NewModelService : CanonicalToolServiceBase, INewModelService
{
    // Implementation as shown in patterns above
}
```

### Step 4: Register Service

```csharp
public static class NewModelServiceExtensions
{
    public static IServiceCollection AddNewModelService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<INewModelService, NewModelService>();
        
        services.AddHttpClient<NewModelService>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["AIModels:NewModel:BaseUrl"]);
            client.DefaultRequestHeaders.Add(
                "Authorization", 
                $"Bearer {configuration["AIModels:NewModel:ApiKey"]}");
        });
        
        services.AddHostedService<NewModelWorkerService>();
        
        return services;
    }
}
```

### Step 5: Update Pipeline

Modify `AIResearchOrchestratorService`:

```csharp
public async Task<ResearchBooklet> OrchestrateResearchAsync(
    ErrorBatch errorBatch)
{
    // Existing models...
    
    // Add new model
    var newModelTask = _newModelService.AnalyzeAsync(
        error, 
        priorFindings);
    
    // Include in results
    allFindings.Add(await newModelTask);
}
```

## Extending Error Parsers

### Creating Custom Parser

```csharp
public class CustomCompilerErrorParser : IErrorParser
{
    private readonly Regex _errorPattern = new Regex(
        @"(?<file>[^:]+):(?<line>\d+):(?<column>\d+):\s*(?<type>error|warning):\s*(?<code>\w+):\s*(?<message>.+)",
        RegexOptions.Compiled | RegexOptions.Multiline);
    
    public bool CanParse(string content)
    {
        return _errorPattern.IsMatch(content);
    }
    
    public IEnumerable<CompilerError> Parse(string content)
    {
        foreach (Match match in _errorPattern.Matches(content))
        {
            yield return new CompilerError(
                ErrorCode: match.Groups["code"].Value,
                Description: match.Groups["message"].Value,
                Location: new ErrorLocation(
                    FilePath: match.Groups["file"].Value,
                    LineNumber: int.Parse(match.Groups["line"].Value),
                    ColumnNumber: int.Parse(match.Groups["column"].Value)
                ),
                Severity: match.Groups["type"].Value,
                AdditionalInfo: null
            );
        }
    }
}
```

### Registering Parser

```csharp
services.AddSingleton<IErrorParser, CustomCompilerErrorParser>();

// In ErrorParserService
public ErrorBatch ParseBuildOutput(string filePath, string content)
{
    var parsers = _serviceProvider.GetServices<IErrorParser>();
    
    foreach (var parser in parsers)
    {
        if (parser.CanParse(content))
        {
            var errors = parser.Parse(content);
            return CreateErrorBatch(filePath, errors);
        }
    }
    
    throw new UnsupportedFormatException(
        "No parser available for this error format");
}
```

## Creating New Workers

### Kafka Message Processor

```csharp
public interface IMessageProcessor<TInput, TOutput>
{
    Task<TOutput> ProcessAsync(TInput input);
}

public class ErrorEnrichmentProcessor : 
    IMessageProcessor<CompilerError, EnrichedError>
{
    private readonly ILogger<ErrorEnrichmentProcessor> _logger;
    
    public async Task<EnrichedError> ProcessAsync(CompilerError input)
    {
        _logger.LogInformation(
            "Enriching error {ErrorCode}", 
            input.ErrorCode);
        
        // Add metadata, context, etc.
        var enriched = new EnrichedError
        {
            Original = input,
            ProjectContext = await GetProjectContext(input.Location),
            RelatedErrors = await FindRelatedErrors(input),
            HistoricalOccurrences = await GetHistory(input.ErrorCode)
        };
        
        return enriched;
    }
}
```

### Generic Worker Service

```csharp
public class GenericWorkerService<TInput, TOutput> : BackgroundService
{
    private readonly IKafkaConsumerService _consumer;
    private readonly IMessageProcessor<TInput, TOutput> _processor;
    private readonly IKafkaProducerService _producer;
    private readonly string _inputTopic;
    private readonly string _outputTopic;
    private readonly string _consumerGroup;
    
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await _consumer.ConsumeAsync<TInput>(
            _inputTopic,
            _consumerGroup,
            async (message) =>
            {
                var result = await _processor.ProcessAsync(message);
                await _producer.ProduceAsync(_outputTopic, null, result);
            },
            stoppingToken);
    }
}
```

## Testing Guidelines

### Unit Test Structure

```csharp
public class NewModelServiceTests
{
    private readonly Mock<HttpClient> _httpClientMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly NewModelService _service;
    
    public NewModelServiceTests()
    {
        _httpClientMock = new Mock<HttpClient>();
        _configMock = new Mock<IConfiguration>();
        
        _configMock.Setup(c => c["AIModels:NewModel:SystemPrompt"])
            .Returns("Test prompt");
        
        _service = new NewModelService(
            new TestLogger<NewModelService>(),
            _httpClientMock.Object,
            _configMock.Object);
    }
    
    [Fact]
    public async Task AnalyzeAsync_ValidError_ReturnsSuccessfulFinding()
    {
        // Arrange
        var error = new CompilerError(
            "CS0246",
            "Type not found",
            new ErrorLocation("test.cs", 10),
            "error",
            null);
        
        var expectedResponse = new
        {
            analysis = "Test analysis",
            confidence = 0.95,
            insights = new[] { "Insight 1", "Insight 2" }
        };
        
        _httpClientMock.Setup(/* mock HTTP call */)
            .ReturnsAsync(JsonSerializer.Serialize(expectedResponse));
        
        // Act
        var result = await _service.AnalyzeAsync(error);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(0.95, result.ConfidenceScore);
        Assert.Contains("Test analysis", result.ToString());
    }
    
    [Fact]
    public async Task AnalyzeAsync_APIError_ThrowsAIAnalysisException()
    {
        // Arrange
        var error = new CompilerError(/* ... */);
        
        _httpClientMock.Setup(/* mock HTTP call */)
            .ThrowsAsync(new HttpRequestException("API Error"));
        
        // Act & Assert
        await Assert.ThrowsAsync<AIAnalysisException>(
            () => _service.AnalyzeAsync(error));
    }
}
```

### Integration Test Example

```csharp
public class AIRESPipelineIntegrationTests : IClassFixture<AIRESTestFixture>
{
    private readonly AIRESTestFixture _fixture;
    
    public AIRESPipelineIntegrationTests(AIRESTestFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public async Task CompletePipeline_ValidErrorFile_GeneratesBooklet()
    {
        // Arrange
        var testFile = CreateTestErrorFile();
        
        // Act
        await _fixture.SubmitFileAsync(testFile);
        await Task.Delay(TimeSpan.FromSeconds(30)); // Wait for processing
        
        // Assert
        var booklets = await _fixture.GetGeneratedBookletsAsync();
        Assert.Single(booklets);
        Assert.Contains("CS0246", booklets[0].Content);
    }
}
```

## Debugging AIRES

### Enable Debug Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "MarketAnalyzer.BuildTools": "Trace"
    }
  }
}
```

### Debug Mode

```bash
# Run with debug output
dotnet run -- --debug

# Run specific component
dotnet run -- --test-mistral --debug

# Validate configuration only
dotnet run -- --validate-config
```

### Common Debugging Scenarios

1. **File Not Processing**
   ```csharp
   // Check file state
   var state = _fileStates.GetValueOrDefault(fileName);
   _logger.LogDebug("File {File} state: {State}", fileName, state);
   
   // Check queue
   _logger.LogDebug("Queue size: {Size}", _processingQueue.Count);
   ```

2. **AI Model Timeout**
   ```csharp
   // Add detailed timing
   var stopwatch = Stopwatch.StartNew();
   try
   {
       var result = await CallAIModelAsync(prompt);
       _logger.LogDebug("AI call took {Ms}ms", stopwatch.ElapsedMilliseconds);
   }
   catch (TaskCanceledException)
   {
       _logger.LogError("AI call timed out after {Ms}ms", 
           stopwatch.ElapsedMilliseconds);
   }
   ```

3. **Kafka Issues**
   ```csharp
   // Enable Kafka debug logging
   var config = new ConsumerConfig
   {
       Debug = "consumer,cgrp,topic,fetch"
   };
   
   // Log partition assignments
   consumer.OnPartitionsAssigned += (_, partitions) =>
   {
       _logger.LogDebug("Assigned partitions: {Partitions}", 
           string.Join(", ", partitions));
   };
   ```

### Performance Profiling

```csharp
// Add timing to critical paths
using (var activity = Activity.StartActivity("ProcessFile"))
{
    activity?.SetTag("file.name", fileName);
    activity?.SetTag("file.size", fileSize);
    
    // Processing logic
    
    activity?.SetTag("processing.duration", stopwatch.ElapsedMilliseconds);
}
```

### Memory Profiling

```csharp
// Monitor memory usage
var before = GC.GetTotalMemory(false);
await ProcessLargeFileAsync(file);
var after = GC.GetTotalMemory(false);

_logger.LogDebug("Memory delta: {Delta}MB", 
    (after - before) / 1024 / 1024);
```

## Development Best Practices

1. **Always Use Canonical Patterns**
   - Extend base classes
   - Use standard logging
   - Return ToolResult<T>

2. **Test Everything**
   - Unit tests for logic
   - Integration tests for workflows
   - Performance tests for AI calls

3. **Document Changes**
   - Update relevant .md files
   - Add XML documentation
   - Include examples

4. **Monitor Performance**
   - Add timing logs
   - Track memory usage
   - Profile hot paths

5. **Handle Failures Gracefully**
   - Retry transient errors
   - Log all exceptions
   - Provide meaningful error messages

---

**Next**: [Testing Guide](AIRES_Testing_Guide.md)