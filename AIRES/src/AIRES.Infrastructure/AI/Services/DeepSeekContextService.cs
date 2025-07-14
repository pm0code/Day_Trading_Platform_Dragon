using System.Threading.Tasks;
using AIRES.Core.Domain.Interfaces;
using AIRES.Core.Domain.ValueObjects;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Infrastructure.AI.Clients;

namespace AIRES.Infrastructure.AI.Services;

/// <summary>
/// DeepSeek AI implementation for analyzing code context around errors.
/// </summary>
public class DeepSeekContextService : AIRESServiceBase, IContextAnalyzerAIModel
{
    private readonly IOllamaClient _ollamaClient;
    private const string MODEL_NAME = "deepseek-coder:6.7b";

    public DeepSeekContextService(
        IAIRESLogger logger,
        IOllamaClient ollamaClient)
        : base(logger, nameof(DeepSeekContextService))
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
    }

    public async Task<ContextAnalysisFinding> AnalyzeAsync(
        CompilerError error, 
        string surroundingCode, 
        string projectStructureXml)
    {
        LogMethodEntry();
        
        try
        {
            var prompt = BuildPrompt(error, surroundingCode, projectStructureXml);

            var request = new OllamaRequest
            {
                Model = MODEL_NAME,
                Prompt = prompt,
                System = "You are DeepSeek Coder, an expert at analyzing C# code context and understanding complex code relationships. Provide detailed technical analysis.",
                Temperature = 0.2,
                MaxTokens = 3000,
                TimeoutSeconds = 120 // DeepSeek needs more time for context analysis
            };

            var result = await _ollamaClient.GenerateAsync(request).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                LogError($"Failed to generate context analysis: {result.ErrorMessage}");
                LogMethodExit();
                return new ContextAnalysisFinding(
                    "DeepSeek",
                    $"Context Analysis for {error.Code}",
                    $"Failed to analyze context: {result.ErrorMessage}",
                    surroundingCode,
                    projectStructureXml
                );
            }

            var response = result.Value!.Response;
            
            LogInfo($"Successfully generated context analysis for {error.Code}");
            LogMethodExit();
            
            return new ContextAnalysisFinding(
                "DeepSeek",
                $"Context Analysis for {error.Code} at {error.Location}",
                response,
                ExtractRelevantCodeSnippet(surroundingCode, error.Location),
                projectStructureXml
            );
        }
        catch (Exception ex)
        {
            LogError($"Unexpected error during context analysis for {error.Code}", ex);
            LogMethodExit();
            
            return new ContextAnalysisFinding(
                "DeepSeek",
                $"Context Analysis for {error.Code}",
                $"Error during analysis: {ex.Message}",
                "",
                ""
            );
        }
    }

    private string BuildPrompt(CompilerError error, string surroundingCode, string projectStructureXml)
    {
        return $@"Analyze the code context around this C# compiler error:

Error Code: {error.Code}
Error Message: {error.Message}
Error Location: {error.Location}

Surrounding Code Context:
```csharp
{surroundingCode}
```

Project Structure:
```xml
{projectStructureXml}
```

Please provide:
1. Context Understanding: Explain what the code is trying to do
2. Error Context: Why this error occurs in this specific context
3. Dependencies: What other parts of the code are affected
4. Architectural Impact: How this error relates to the overall design
5. Refactoring Suggestions: How the code structure could be improved
6. Type System Analysis: Identify any type mismatches or conversions needed

Focus on the relationships between different parts of the code and how they contribute to the error.";
    }

    private string ExtractRelevantCodeSnippet(string surroundingCode, ErrorLocation location)
    {
        LogMethodEntry();
        
        try
        {
            var lines = surroundingCode.Split('\n');
            var lineNumber = location.LineNumber;
            
            // Extract ±5 lines around the error
            var startLine = Math.Max(0, lineNumber - 5);
            var endLine = Math.Min(lines.Length - 1, lineNumber + 5);
            
            var relevantLines = new List<string>();
            for (int i = startLine; i <= endLine; i++)
            {
                var prefix = i == lineNumber - 1 ? ">>> " : "    ";
                relevantLines.Add($"{prefix}{lines[i]}");
            }
            
            LogMethodExit();
            return string.Join('\n', relevantLines);
        }
        catch (Exception ex)
        {
            LogError("Error extracting relevant code snippet", ex);
            LogMethodExit();
            return surroundingCode;
        }
    }
}