# Gemini AI Consultation Guide for AIRES Development

## Overview
Gemini AI is available for architectural consultations during AIRES development. This guide explains when and how to use Gemini for complex design decisions.

## Access Information
- **API Key**: `AIzaSyDP7daxEmHxuSTA3ZObO4Rgkl2HswqpHcs`
- **Model**: `gemini-2.5-flash`
- **Endpoint**: `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent`

## When to Consult Gemini

### MANDATORY Consultation Scenarios:
1. **Architectural Design Decisions**
   - Service layer design
   - API contract definitions
   - Database schema design
   - Inter-service communication patterns

2. **Complex C# Patterns**
   - Advanced generics usage
   - Expression trees
   - Reflection-based solutions
   - Performance-critical implementations

3. **Financial Domain Modeling**
   - Trading system patterns
   - Financial calculations
   - Risk management approaches
   - Market data handling

4. **AI Pipeline Architecture**
   - Orchestration patterns
   - Error handling strategies
   - Retry and circuit breaker design
   - Asynchronous processing

## How to Use Gemini

### Method 1: Shell Script (Recommended)
```bash
# Navigate to scripts directory
cd /mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/scripts

# Run consultation
./consult-gemini.sh "Should AIRES use a message queue between AI stages or direct method calls?"
```

### Method 2: Direct API Call
```bash
curl "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=AIzaSyDP7daxEmHxuSTA3ZObO4Rgkl2HswqpHcs" \
  -H 'Content-Type: application/json' \
  -X POST \
  -d '{
    "contents":[{
      "parts":[{
        "text":"Your architectural question here"
      }]
    }]
  }'
```

### Method 3: From C# Code (Future Implementation)
```csharp
public interface IGeminiConsultationService
{
    Task<ArchitecturalGuidance> ConsultAsync(string question);
}
```

## Prompt Template Structure

When consulting Gemini, use this structure:

```
Context: [Describe AIRES and current implementation state]
Problem: [Specific architectural challenge]
Constraints: [Technical limitations, performance requirements]
Question: [Clear, specific question]

Please provide:
1. Architectural Analysis
2. Best Practices
3. Potential Issues
4. Recommended Solution
5. Alternative Approaches
```

## Example Consultations

### Example 1: Service Communication Pattern
```bash
./consult-gemini.sh "In AIRES, should the AIResearchOrchestratorService communicate with AI models via direct dependency injection or through a message bus pattern for better scalability?"
```

### Example 2: Error Handling Strategy
```bash
./consult-gemini.sh "What's the best pattern for handling cascading failures in the AIRES 4-stage AI pipeline when one AI model times out?"
```

### Example 3: Performance Optimization
```bash
./consult-gemini.sh "Should AIRES process multiple compiler errors in parallel through the AI pipeline, or maintain sequential processing for consistency?"
```

## Integration with Development Workflow

1. **Before Implementation**: Consult Gemini for architectural approach
2. **During Implementation**: Validate design decisions
3. **Code Review**: Get second opinion on complex patterns
4. **Performance Issues**: Optimization strategies

## Best Practices

1. **Be Specific**: Provide context about AIRES and the specific problem
2. **Include Constraints**: Mention performance, platform, or technical limitations
3. **Request Alternatives**: Always ask for multiple approaches
4. **Document Decisions**: Save Gemini's recommendations in design docs

## Response Processing

Gemini responses should be:
1. Analyzed for applicability to AIRES
2. Cross-referenced with existing patterns
3. Validated against project constraints
4. Documented in architectural decision records

## Common AIRES Architectural Questions

1. **Resilience Patterns**
   - "How should AIRES handle Ollama server unavailability?"
   - "What's the best circuit breaker configuration for AI services?"

2. **Performance Optimization**
   - "Should AIRES cache AI responses for similar errors?"
   - "How to optimize booklet generation for large error batches?"

3. **Scalability Design**
   - "How to design AIRES for processing 1000+ errors/minute?"
   - "Should watchdog use file system events or polling?"

4. **Testing Strategies**
   - "How to unit test AI service orchestration?"
   - "Best approach for mocking Ollama responses?"

## Response Format

Gemini typically provides:
- **Architectural Analysis**: High-level design evaluation
- **Implementation Details**: Specific C# code examples
- **Trade-offs**: Pros and cons of each approach
- **Recommendations**: Clear guidance on best approach

## Logging Gemini Consultations

All Gemini consultations should be logged:
```bash
# Save consultation
./consult-gemini.sh "Question" > /docs/AIRES/Architecture/Gemini_Consultation_$(date +%Y%m%d_%H%M%S).md
```

## Error Handling

If Gemini is unavailable:
1. Check API key validity
2. Verify network connectivity
3. Use fallback to Ollama for general guidance
4. Document the issue and proceed with best judgment

---

Remember: Gemini is a tool for validation and exploration. Always apply critical thinking and ensure recommendations align with AIRES project goals and constraints.