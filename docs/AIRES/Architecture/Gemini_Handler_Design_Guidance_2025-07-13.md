# Gemini AI Guidance: Handler Design Principles

## Date: 2025-07-13

## Key Recommendations

### 1. Handlers as Focused Wrappers
Handlers should:
- ✅ Prepare request body for AI service
- ✅ Call the AI service
- ✅ Handle errors (network, API, timeouts)
- ✅ Transform raw AI response to domain objects

Handlers should NOT:
- ❌ Perform complex business logic
- ❌ Do inter-stage data enrichment
- ❌ Handle persistence (file writing)

### 2. Single Responsibility Principle
Each handler's responsibility: "Interact with this specific AI service and normalize its output"

### 3. GenerateBookletHandler Design
- Should return content only (string/DTO)
- NOT handle file writing
- Persistence is a separate concern

### 4. Benefits
- **Testability**: Mock AI calls easily
- **Flexibility**: Switch AI providers without changing pipeline
- **Clearer Flow**: Explicit data flow
- **Scalability**: Reusable handlers

## Implementation Pattern

```csharp
public class AIHandler : IRequestHandler<Command, Response>
{
    public async Task<Response> Handle(Command request, CancellationToken ct)
    {
        // 1. Transform request to AI format
        var aiRequest = PrepareAIRequest(request);
        
        // 2. Call AI service
        var aiResponse = await _aiService.CallAsync(aiRequest);
        
        // 3. Handle errors
        if (!aiResponse.Success)
            throw new AIServiceException(...);
            
        // 4. Transform to domain object
        return TransformResponse(aiResponse);
    }
}
```

## Separation of Concerns
- **Handlers**: AI interaction only
- **Orchestrator**: Inter-stage enrichment and flow control
- **Persistence Service**: File/database operations

This ensures clean, maintainable, testable code.