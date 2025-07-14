# Gemini AI Guidance: AIResearchOrchestratorService Implementation

## Date: 2025-07-13

## Summary of Recommendations

1. **Use Commands** (not Queries) for AI model calls - they perform actions
2. **Handle errors explicitly** in orchestrator - don't let them bubble up
3. **Sequential execution** required due to data dependencies
4. **Return Result<T> type** for explicit success/failure handling

## Key Architecture Decisions

### 1. Command Pattern for AI Calls
- Each AI model interaction is a Command (IRequest<TResponse>)
- Commands represent actions that transform data
- Clear intent and separation of concerns

### 2. Error Handling Strategy
- Specific exceptions for each AI model (MistralAnalysisFailedException, etc.)
- Try-catch in orchestrator for each step
- Result pattern for business logic failures
- Fail-fast principle - stop pipeline on first failure

### 3. Sequential Pipeline
Data flow dependencies:
1. Parse compiler errors → 
2. Mistral (needs errors) → 
3. DeepSeek (needs Mistral output) → 
4. CodeGemma (needs DeepSeek output) → 
5. Gemma2 (needs all previous outputs)

### 4. Result Type Pattern
```csharp
public class Result<T>
{
    public T? Value { get; }
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }
}
```

## Implementation Structure

### Commands
- ParseCompilerErrorsCommand
- AnalyzeDocumentationCommand (Mistral)
- AnalyzeContextCommand (DeepSeek)
- ValidatePatternsCommand (CodeGemma)
- GenerateBookletCommand (Gemma2)

### Handlers
- One handler per command
- Encapsulates AI service interaction
- Throws specific exceptions on failure

### Orchestrator
- Injects IMediator
- Sequential execution with error handling
- Returns Result<BookletGenerationResponse>
- Comprehensive logging at each step

## Benefits of This Approach

1. **Maintainability**: Loose coupling, single responsibility
2. **Testability**: Mock IMediator for orchestrator tests, mock specific services for handlers
3. **Extensibility**: Easy to add pipeline behaviors (logging, validation, retry)
4. **Clear Intent**: Commands express actions clearly

---
This guidance forms the foundation for our MediatR-based implementation.