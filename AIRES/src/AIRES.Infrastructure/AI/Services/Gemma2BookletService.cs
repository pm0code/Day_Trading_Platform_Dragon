using System;
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
/// Gemma2 AI implementation for synthesizing findings into comprehensive booklets.
/// </summary>
public class Gemma2BookletService : AIRESServiceBase, IBookletGeneratorAIModel
{
    private readonly IOllamaClient _ollamaClient;
    private const string MODEL_NAME = "gemma2:9b";

    public Gemma2BookletService(
        IAIRESLogger logger,
        IOllamaClient ollamaClient)
        : base(logger, nameof(Gemma2BookletService))
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
    }

    public async Task<IBookletGeneratorAIModel.BookletContentDraft> GenerateBookletContentAsync(
        Guid errorBatchId,
        IImmutableList<CompilerError> originalErrors,
        IImmutableList<AIResearchFinding> allDetailedFindings,
        PatternValidationFinding? consolidatedPatternFindings)
    {
        LogMethodEntry();
        
        try
        {
            var prompt = BuildPrompt(errorBatchId, originalErrors, allDetailedFindings, consolidatedPatternFindings);

            var request = new OllamaRequest
            {
                Model = MODEL_NAME,
                Prompt = prompt,
                System = "You are Gemma2, an expert at synthesizing technical research into comprehensive, actionable documentation. Create clear, well-structured booklets that guide developers to resolve complex issues.",
                Temperature = 0.3,
                MaxTokens = 6000,
                TimeoutSeconds = 180 // Gemma2 9B needs more time for synthesis
            };

            var result = await _ollamaClient.GenerateAsync(request).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                LogError($"Failed to generate booklet content: {result.ErrorMessage}");
                LogMethodExit();
                return CreateFailureBooklet(errorBatchId, originalErrors, result.ErrorMessage!);
            }

            var response = result.Value!.Response;
            var bookletDraft = ParseBookletContent(response, errorBatchId, originalErrors);
            
            LogInfo($"Successfully generated booklet with {bookletDraft.ImplementationRecommendations.Count} recommendations");
            LogMethodExit();
            
            return bookletDraft;
        }
        catch (Exception ex)
        {
            LogError("Unexpected error during booklet generation", ex);
            LogMethodExit();
            
            return CreateFailureBooklet(errorBatchId, originalErrors, ex.Message);
        }
    }

    private string BuildPrompt(
        Guid errorBatchId,
        IImmutableList<CompilerError> errors,
        IImmutableList<AIResearchFinding> findings,
        PatternValidationFinding? patternFindings)
    {
        var errorSummary = string.Join("\n", errors.Select(e => $"- {e.Code}: {e.Message}"));
        var findingsSummary = string.Join("\n\n", findings.Select(f => $"**{f.AIModelName} - {f.Title}**\n{f.Content}"));
        var patternSummary = patternFindings != null 
            ? $"\n\n**Pattern Validation Results**\n{patternFindings.Content}" 
            : "";

        return $@"Synthesize the following AI research findings into a comprehensive error resolution booklet:

Error Batch ID: {errorBatchId}

Original Compiler Errors:
{errorSummary}

AI Research Findings:
{findingsSummary}
{patternSummary}

Create a booklet with:
1. Executive Summary: High-level overview of the errors and their impact
2. Architectural Guidance: Strategic approach to resolving these errors
3. Implementation Recommendations: Specific, prioritized steps to fix each error
4. Code Examples: Concrete before/after code snippets
5. Testing Strategy: How to verify the fixes work correctly

Structure your response as:
TITLE: [Concise title for the booklet]
SUMMARY: [2-3 paragraph executive summary]
ARCHITECTURAL_GUIDANCE: [Strategic guidance section]
RECOMMENDATIONS: [Numbered list of specific recommendations]

Make the content actionable, clear, and focused on helping developers resolve these errors efficiently.";
    }

    private IBookletGeneratorAIModel.BookletContentDraft ParseBookletContent(
        string response,
        Guid errorBatchId,
        IImmutableList<CompilerError> errors)
    {
        LogMethodEntry();
        
        try
        {
            // Parse the structured response
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string title = $"Error Resolution Booklet - {errorBatchId}";
            string summary = "";
            ArchitecturalGuidance? architecturalGuidance = null;
            var recommendations = new List<ImplementationRecommendation>();

            var currentSection = "";
            var sectionContent = new List<string>();

            foreach (var line in lines)
            {
                if (line.StartsWith("TITLE:"))
                {
                    title = line.Substring(6).Trim();
                }
                else if (line.StartsWith("SUMMARY:"))
                {
                    currentSection = "SUMMARY";
                    sectionContent.Clear();
                }
                else if (line.StartsWith("ARCHITECTURAL_GUIDANCE:"))
                {
                    if (currentSection == "SUMMARY")
                    {
                        summary = string.Join(" ", sectionContent);
                    }
                    currentSection = "ARCHITECTURAL";
                    sectionContent.Clear();
                }
                else if (line.StartsWith("RECOMMENDATIONS:"))
                {
                    if (currentSection == "ARCHITECTURAL")
                    {
                        var guidanceText = string.Join("\n", sectionContent);
                        architecturalGuidance = new ArchitecturalGuidance(
                            "Architectural Approach",
                            guidanceText,
                            "Based on AI analysis of error patterns",
                            "High impact on code quality and maintainability"
                        );
                    }
                    currentSection = "RECOMMENDATIONS";
                    sectionContent.Clear();
                }
                else
                {
                    sectionContent.Add(line);
                }
            }

            // Process final section
            if (currentSection == "RECOMMENDATIONS")
            {
                int priority = 1;
                foreach (var rec in sectionContent.Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    recommendations.Add(new ImplementationRecommendation(
                        $"Recommendation {priority}",
                        rec,
                        "// See booklet for detailed code example",
                        priority,
                        "Medium"
                    ));
                    priority++;
                }
            }

            // Ensure we have content
            if (string.IsNullOrEmpty(summary))
            {
                summary = $"This booklet provides comprehensive guidance for resolving {errors.Count} compiler errors through AI-assisted analysis.";
            }

            if (architecturalGuidance == null)
            {
                architecturalGuidance = new ArchitecturalGuidance(
                    "Standard Approach",
                    "Follow canonical patterns and development standards",
                    "Ensures consistency and maintainability",
                    "Medium impact"
                );
            }

            if (!recommendations.Any())
            {
                recommendations.Add(new ImplementationRecommendation(
                    "General Recommendation",
                    "Review the AI findings and apply suggested fixes",
                    "// Implement fixes as suggested in findings",
                    1,
                    "Variable"
                ));
            }

            LogMethodExit();
            return new IBookletGeneratorAIModel.BookletContentDraft(
                title,
                summary,
                architecturalGuidance,
                recommendations.ToImmutableList()
            );
        }
        catch (Exception ex)
        {
            LogError("Error parsing booklet content", ex);
            LogMethodExit();
            
            return CreateFailureBooklet(errorBatchId, errors, "Failed to parse booklet content");
        }
    }

    private IBookletGeneratorAIModel.BookletContentDraft CreateFailureBooklet(
        Guid errorBatchId,
        IImmutableList<CompilerError> errors,
        string errorMessage)
    {
        return new IBookletGeneratorAIModel.BookletContentDraft(
            $"Error Resolution Failed - {errorBatchId}",
            $"Failed to generate booklet: {errorMessage}",
            new ArchitecturalGuidance(
                "Manual Review Required",
                "The AI synthesis failed. Manual review of errors is required.",
                errorMessage,
                "High - requires manual intervention"
            ),
            ImmutableList<ImplementationRecommendation>.Empty.Add(
                new ImplementationRecommendation(
                    "Manual Resolution",
                    "Review compiler errors manually and apply fixes",
                    "// Manual fix required",
                    1,
                    "Unknown"
                )
            )
        );
    }
}