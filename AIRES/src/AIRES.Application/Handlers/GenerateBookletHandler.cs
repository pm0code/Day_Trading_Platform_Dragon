using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using AIRES.Application.Commands;
using AIRES.Application.Exceptions;
using AIRES.Core.Domain.Interfaces;
using AIRES.Core.Domain.ValueObjects;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using System.Collections.Immutable;

namespace AIRES.Application.Handlers;

/// <summary>
/// Handler for Gemma2 booklet generation command.
/// Returns content only - persistence is handled separately as per Gemini's guidance.
/// </summary>
public class GenerateBookletHandler : AIRESServiceBase, IRequestHandler<GenerateBookletCommand, BookletGenerationResponse>
{
    private readonly IBookletGeneratorAIModel _gemma2Service;

    public GenerateBookletHandler(
        IAIRESLogger logger,
        IBookletGeneratorAIModel gemma2Service) 
        : base(logger, nameof(GenerateBookletHandler))
    {
        _gemma2Service = gemma2Service ?? throw new ArgumentNullException(nameof(gemma2Service));
    }

    public async Task<BookletGenerationResponse> Handle(
        GenerateBookletCommand request, 
        CancellationToken cancellationToken)
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Validate input
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (request.ErrorBatchId == Guid.Empty)
            {
                throw new ArgumentException("Request.ErrorBatchId cannot be empty", nameof(request));
            }
            if (request.OriginalErrors == null)
            {
                throw new ArgumentNullException(nameof(request), "Request.OriginalErrors cannot be null");
            }
            if (request.DocAnalysis == null)
            {
                throw new ArgumentNullException(nameof(request), "Request.DocAnalysis cannot be null");
            }
            if (request.ContextAnalysis == null)
            {
                throw new ArgumentNullException(nameof(request), "Request.ContextAnalysis cannot be null");
            }
            if (request.PatternValidation == null)
            {
                throw new ArgumentNullException(nameof(request), "Request.PatternValidation cannot be null");
            }
            
            UpdateMetric("GenerateBooklet.Requests", 1);
            if (!request.OriginalErrors.Any())
            {
                LogWarning("No errors provided for booklet generation");
                LogMethodExit();
                throw new Gemma2GenerationException(
                    "Cannot generate booklet without compiler errors",
                    "GEMMA2_NO_ERRORS"
                );
            }

            LogInfo($"Generating research booklet for batch {request.ErrorBatchId} with {request.OriginalErrors.Count} errors");

            // Prepare all findings for booklet generation
            var allFindings = PrepareAllFindings(request);

            // Call Gemma2 service to generate booklet content
            IBookletGeneratorAIModel.BookletContentDraft bookletDraft;
            try
            {
                bookletDraft = await _gemma2Service.GenerateBookletContentAsync(
                    request.ErrorBatchId,
                    request.OriginalErrors,
                    allFindings,
                    request.PatternValidation.ValidationFinding
                );
            }
            catch (Exception ex)
            {
                LogError("Gemma2 booklet generation failed", ex);
                throw new Gemma2GenerationException(
                    "Failed to generate booklet content",
                    ex,
                    "GEMMA2_GENERATION_FAILED"
                );
            }

            // Create the research booklet from the draft
            var booklet = CreateResearchBooklet(
                request.ErrorBatchId,
                bookletDraft,
                request,
                allFindings
            );

            // Generate booklet path (but don't write file - that's persistence layer's job)
            var bookletPath = GenerateBookletPath(request.ErrorBatchId);

            // Create response with timing information
            stopwatch.Stop();
            var response = new BookletGenerationResponse(
                booklet,
                bookletPath,
                stopwatch.ElapsedMilliseconds,
                ImmutableDictionary<string, long>.Empty // Will be populated by orchestrator
            );

            UpdateMetric("GenerateBooklet.ResponseTime", stopwatch.ElapsedMilliseconds);
            UpdateMetric("GenerateBooklet.Successes", 1);
            UpdateMetric("GenerateBooklet.BookletsGenerated", 1);
            UpdateMetric("GenerateBooklet.SectionsCreated", booklet.Sections.Count);
            UpdateMetric("GenerateBooklet.FindingsIncluded", allFindings.Count);
            
            LogInfo($"Booklet generation complete. Title: {booklet.Title} in {stopwatch.ElapsedMilliseconds}ms");
            LogMethodExit();
            
            return response;
        }
        catch (ArgumentNullException ex)
        {
            UpdateMetric("GenerateBooklet.ValidationErrors", 1);
            LogError("Invalid input parameters", ex);
            LogMethodExit();
            throw;
        }
        catch (ArgumentException ex)
        {
            UpdateMetric("GenerateBooklet.ValidationErrors", 1);
            LogError("Invalid input parameters", ex);
            LogMethodExit();
            throw;
        }
        catch (Gemma2GenerationException)
        {
            UpdateMetric("GenerateBooklet.Failures", 1);
            throw; // Re-throw specific exceptions
        }
        catch (Exception ex)
        {
            UpdateMetric("GenerateBooklet.UnexpectedErrors", 1);
            LogError("Unexpected error during booklet generation", ex);
            LogMethodExit();
            throw new Gemma2GenerationException(
                $"Failed to generate booklet: {ex.Message}", 
                ex,
                "GEMMA2_UNEXPECTED_ERROR"
            );
        }
    }

    private ImmutableList<AIResearchFinding> PrepareAllFindings(GenerateBookletCommand request)
    {
        LogMethodEntry();
        
        var findings = new List<AIResearchFinding>();

        // Add documentation findings
        findings.AddRange(request.DocAnalysis.Findings);

        // Add context findings
        findings.AddRange(request.ContextAnalysis.Findings);

        // Add pattern validation as a finding
        if (request.PatternValidation.ValidationFinding != null)
        {
            findings.Add(request.PatternValidation.ValidationFinding);
        }

        LogInfo($"Prepared {findings.Count} total findings for booklet generation");
        LogMethodExit();
        return findings.ToImmutableList();
    }

    private ResearchBooklet CreateResearchBooklet(
        Guid batchId,
        IBookletGeneratorAIModel.BookletContentDraft draft,
        GenerateBookletCommand request,
        ImmutableList<AIResearchFinding> allFindings)
    {
        LogMethodEntry();
        
        // Create structured sections from the draft
        var sections = new List<BookletSection>();

        // Executive Summary
        sections.Add(new BookletSection(
            "Executive Summary",
            draft.Summary,
            1
        ));

        // Architectural Guidance
        if (draft.ArchitecturalGuidance != null)
        {
            sections.Add(new BookletSection(
                "Architectural Guidance",
                FormatArchitecturalGuidance(draft.ArchitecturalGuidance),
                2
            ));
        }

        // Implementation Recommendations
        if (draft.ImplementationRecommendations.Any())
        {
            sections.Add(new BookletSection(
                "Implementation Recommendations",
                FormatImplementationRecommendations(draft.ImplementationRecommendations),
                3
            ));
        }

        // Pattern Validation Results
        if (request.PatternValidation.CriticalViolations.Any())
        {
            sections.Add(new BookletSection(
                "Critical Pattern Violations",
                FormatCriticalViolations(request.PatternValidation.CriticalViolations),
                4
            ));
        }

        // Create the booklet
        var booklet = new ResearchBooklet(
            batchId,
            draft.Title,
            request.OriginalErrors,
            allFindings,
            sections.ToImmutableList(),
            GenerateMetadata(request)
        );

        LogMethodExit();
        return booklet;
    }

    private string FormatArchitecturalGuidance(ArchitecturalGuidance guidance)
    {
        return $"## {guidance.Title}\n\n" +
               $"**Description:** {guidance.Description}\n\n" +
               $"**Rationale:** {guidance.Rationale}\n\n" +
               $"**Impact:** {guidance.ImpactAssessment}\n";
    }

    private string FormatImplementationRecommendations(IImmutableList<ImplementationRecommendation> recommendations)
    {
        var content = "";
        foreach (var rec in recommendations.OrderBy(r => r.Priority))
        {
            content += $"### {rec.Priority}. {rec.Title}\n\n";
            content += $"{rec.Description}\n\n";
            content += $"**Code Example:**\n```csharp\n{rec.CodeExample}\n```\n\n";
            content += $"**Estimated Effort:** {rec.EstimatedEffort}\n\n";
        }
        return content;
    }

    private string FormatCriticalViolations(IImmutableList<string> violations)
    {
        var content = "The following critical pattern violations must be addressed:\n\n";
        foreach (var violation in violations)
        {
            content += $"- ⚠️ {violation}\n";
        }
        return content;
    }

    private ImmutableDictionary<string, string> GenerateMetadata(GenerateBookletCommand request)
    {
        return new Dictionary<string, string>
        {
            ["TotalErrors"] = request.OriginalErrors.Count.ToString(),
            ["ErrorCodes"] = string.Join(", ", request.OriginalErrors.Select(e => e.Code).Distinct()),
            ["PatternCompliance"] = request.PatternValidation.OverallCompliance.ToString(),
            ["CriticalViolations"] = request.PatternValidation.CriticalViolations.Count.ToString(),
            ["GeneratedBy"] = "AIRES AI Research Pipeline",
            ["AIModels"] = "Mistral, DeepSeek, CodeGemma, Gemma2"
        }.ToImmutableDictionary();
    }

    private string GenerateBookletPath(Guid batchId)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        return $"booklets/aires_research_{batchId}_{timestamp}.md";
    }
}