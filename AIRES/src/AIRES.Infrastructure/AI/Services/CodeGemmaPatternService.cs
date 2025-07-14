using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using AIRES.Core.Domain.Interfaces;
using AIRES.Core.Domain.ValueObjects;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Infrastructure.AI.Clients;

namespace AIRES.Infrastructure.AI.Services;

/// <summary>
/// CodeGemma AI implementation for validating code patterns against standards.
/// </summary>
public class CodeGemmaPatternService : AIRESServiceBase, IPatternValidatorAIModel
{
    private readonly IOllamaClient _ollamaClient;
    private const string MODEL_NAME = "codegemma:7b";

    public CodeGemmaPatternService(
        IAIRESLogger logger,
        IOllamaClient ollamaClient)
        : base(logger, nameof(CodeGemmaPatternService))
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
    }

    public async Task<PatternValidationFinding> AnalyzeBatchAsync(
        IEnumerable<CompilerError> errors, 
        string projectCodebase, 
        IImmutableList<string> projectStandardPatterns)
    {
        LogMethodEntry();
        
        try
        {
            var errorsList = errors.ToList();
            var prompt = BuildPrompt(errorsList, projectCodebase, projectStandardPatterns);

            var request = new OllamaRequest
            {
                Model = MODEL_NAME,
                Prompt = prompt,
                System = "You are CodeGemma, an expert at validating C# code patterns against development standards. Focus on canonical patterns, logging requirements, and architectural compliance.",
                Temperature = 0.1,
                MaxTokens = 4000,
                TimeoutSeconds = 150 // Pattern analysis needs more time
            };

            var result = await _ollamaClient.GenerateAsync(request).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                LogError($"Failed to generate pattern validation: {result.ErrorMessage}");
                LogMethodExit();
                return CreateFailureFinding(errorsList, result.ErrorMessage!);
            }

            var response = result.Value!.Response;
            var parsedFindings = ParsePatternFindings(response);
            
            LogInfo($"Successfully validated {parsedFindings.IssuesIdentified.Count} pattern issues");
            LogMethodExit();
            
            return parsedFindings;
        }
        catch (Exception ex)
        {
            LogError("Unexpected error during pattern validation", ex);
            LogMethodExit();
            
            return CreateFailureFinding(errors.ToList(), ex.Message);
        }
    }

    private string BuildPrompt(List<CompilerError> errors, string projectCodebase, IImmutableList<string> standards)
    {
        var errorSummary = string.Join("\n", errors.Select(e => $"- {e.Code}: {e.Message} at {e.Location}"));
        var standardsList = string.Join("\n", standards.Select(s => $"- {s}"));

        return $@"Analyze the following compiler errors against the project's mandatory development standards:

Compiler Errors:
{errorSummary}

Project Codebase Overview:
{projectCodebase}

Mandatory Standards to Validate:
{standardsList}

Please analyze and provide:
1. Pattern Violations: List each violation of the mandatory standards
2. Canonical Pattern Compliance: Check for LogMethodEntry/Exit, AIRESResult usage, etc.
3. Logging Compliance: Verify all methods have proper entry/exit logging
4. Service Base Class Usage: Ensure services inherit from AIRESServiceBase
5. Error Handling Patterns: Validate proper try-catch-finally with logging
6. Naming Conventions: Check for SCREAMING_SNAKE_CASE error codes

For each issue found, provide:
- Issue Type (e.g., 'Missing LogMethodEntry', 'Incorrect Base Class')
- Description of the violation
- Suggested correction with code example
- Location if applicable

Also list any patterns that ARE compliant to acknowledge good practices.

Format your response as structured JSON for easy parsing.";
    }

    private PatternValidationFinding ParsePatternFindings(string response)
    {
        LogMethodEntry();
        
        try
        {
            // In a real implementation, parse JSON response
            // For now, create a structured finding from the text
            var issues = new List<PatternIssue>();
            var compliantPatterns = new List<string>();

            // Simple parsing logic - in production, use proper JSON parsing
            var lines = response.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("Issue:") || line.Contains("Violation:"))
                {
                    issues.Add(new PatternIssue(
                        "Pattern Violation",
                        line,
                        "See documentation for correct implementation",
                        null
                    ));
                }
                else if (line.Contains("Compliant:") || line.Contains("Good:"))
                {
                    compliantPatterns.Add(line);
                }
            }

            LogMethodExit();
            return new PatternValidationFinding(
                "CodeGemma",
                "Pattern Validation Report",
                response,
                issues,
                compliantPatterns
            );
        }
        catch (Exception ex)
        {
            LogError("Error parsing pattern findings", ex);
            LogMethodExit();
            
            return new PatternValidationFinding(
                "CodeGemma",
                "Pattern Validation Report",
                response,
                new List<PatternIssue>(),
                new List<string>()
            );
        }
    }

    private PatternValidationFinding CreateFailureFinding(List<CompilerError> errors, string errorMessage)
    {
        return new PatternValidationFinding(
            "CodeGemma",
            "Pattern Validation Failed",
            $"Failed to validate patterns: {errorMessage}",
            new List<PatternIssue>
            {
                new PatternIssue(
                    "Validation Error",
                    errorMessage,
                    "Resolve the error and retry validation",
                    null
                )
            },
            new List<string>()
        );
    }
}