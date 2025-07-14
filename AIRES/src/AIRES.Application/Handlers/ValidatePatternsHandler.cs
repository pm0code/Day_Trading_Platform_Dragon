using MediatR;
using AIRES.Application.Commands;
using AIRES.Application.Exceptions;
using AIRES.Core.Domain.Interfaces;
using AIRES.Core.Domain.ValueObjects;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using System.Collections.Immutable;
using System.Linq;

namespace AIRES.Application.Handlers;

/// <summary>
/// Handler for CodeGemma pattern validation command.
/// Focused wrapper that calls the AI service and normalizes output.
/// </summary>
public class ValidatePatternsHandler : AIRESServiceBase, IRequestHandler<ValidatePatternsCommand, PatternValidationResponse>
{
    private readonly IPatternValidatorAIModel _codeGemmaService;

    public ValidatePatternsHandler(
        IAIRESLogger logger,
        IPatternValidatorAIModel codeGemmaService) 
        : base(logger, nameof(ValidatePatternsHandler))
    {
        _codeGemmaService = codeGemmaService ?? throw new ArgumentNullException(nameof(codeGemmaService));
    }

    public async Task<PatternValidationResponse> Handle(
        ValidatePatternsCommand request, 
        CancellationToken cancellationToken)
    {
        LogMethodEntry();
        
        try
        {
            if (!request.Errors.Any())
            {
                LogWarning("No errors provided for pattern validation");
                LogMethodExit();
                return new PatternValidationResponse(
                    new PatternValidationFinding(
                        "CodeGemma",
                        "No Errors to Validate",
                        "No compiler errors were provided for pattern validation",
                        ImmutableList<PatternIssue>.Empty,
                        ImmutableList<string>.Empty
                    ),
                    true,
                    ImmutableList<string>.Empty,
                    ImmutableList<string>.Empty
                );
            }

            LogInfo($"Validating patterns for {request.Errors.Count} compiler errors against {request.ProjectStandards.Count} standards");

            // Call CodeGemma service
            PatternValidationFinding validationFinding;
            try
            {
                validationFinding = await _codeGemmaService.AnalyzeBatchAsync(
                    request.Errors,
                    request.ProjectCodebase,
                    request.ProjectStandards
                );
            }
            catch (Exception ex)
            {
                LogError("CodeGemma pattern validation failed", ex);
                throw new CodeGemmaValidationException(
                    "Failed to validate patterns against standards",
                    ex,
                    "CODEGEMMA_VALIDATION_FAILED"
                );
            }

            // Analyze the validation results
            var (overallCompliance, criticalViolations, recommendations) = AnalyzeValidationResults(
                validationFinding,
                request.ContextAnalysis
            );

            LogInfo($"Pattern validation complete. Compliance: {overallCompliance}, Critical violations: {criticalViolations.Count}");
            LogMethodExit();
            
            return new PatternValidationResponse(
                validationFinding,
                overallCompliance,
                criticalViolations,
                recommendations
            );
        }
        catch (CodeGemmaValidationException)
        {
            throw; // Re-throw specific exceptions
        }
        catch (Exception ex)
        {
            LogError("Unexpected error during pattern validation", ex);
            LogMethodExit();
            throw new CodeGemmaValidationException(
                $"Failed to validate patterns: {ex.Message}", 
                ex,
                "CODEGEMMA_UNEXPECTED_ERROR"
            );
        }
    }

    private (bool overallCompliance, ImmutableList<string> criticalViolations, ImmutableList<string> recommendations) 
        AnalyzeValidationResults(PatternValidationFinding finding, ContextAnalysisResponse contextAnalysis)
    {
        LogMethodEntry();
        
        var criticalViolations = new List<string>();
        var recommendations = new List<string>();

        // Determine critical violations
        foreach (var issue in finding.IssuesIdentified)
        {
            if (IsCriticalIssue(issue))
            {
                criticalViolations.Add($"{issue.IssueType}: {issue.Description}");
            }
            
            // Generate recommendations based on the issue
            var recommendation = GenerateRecommendation(issue, contextAnalysis);
            if (!string.IsNullOrWhiteSpace(recommendation))
            {
                recommendations.Add(recommendation);
            }
        }

        // Overall compliance check
        bool overallCompliance = !criticalViolations.Any() && 
                                finding.CompliantPatterns.Count > finding.IssuesIdentified.Count;

        // Add general recommendations based on context
        if (contextAnalysis.IdentifiedPainPoints.Any())
        {
            recommendations.Add("Address identified pain points before implementing pattern fixes");
        }

        LogMethodExit();
        return (overallCompliance, criticalViolations.ToImmutableList(), recommendations.Distinct().ToImmutableList());
    }

    private bool IsCriticalIssue(PatternIssue issue)
    {
        LogMethodEntry();
        
        // Define what makes an issue critical
        var criticalTypes = new[]
        {
            "Missing LogMethodEntry",
            "Missing LogMethodExit",
            "Incorrect Base Class",
            "Missing Error Handling",
            "Direct Exception Throw",
            "Missing Result Pattern"
        };

        var isCritical = criticalTypes.Any(ct => 
            issue.IssueType.Contains(ct, StringComparison.OrdinalIgnoreCase));
        
        LogMethodExit();
        return isCritical;
    }

    private string GenerateRecommendation(PatternIssue issue, ContextAnalysisResponse contextAnalysis)
    {
        LogMethodEntry();
        
        string recommendation = issue.IssueType switch
        {
            var t when t.Contains("LogMethod") => 
                "Ensure all methods have LogMethodEntry() as first line and LogMethodExit() before all returns",
            
            var t when t.Contains("Base Class") => 
                "Inherit from AIRESServiceBase for all service implementations",
            
            var t when t.Contains("Error Handling") => 
                "Wrap all operations in try-catch with proper logging and AIRESResult return",
            
            var t when t.Contains("Result Pattern") => 
                "Use AIRESResult<T> or Result<T> for all method returns",
            
            _ => issue.SuggestedCorrection
        };

        // Enhance recommendation based on context
        if (contextAnalysis.IdentifiedPainPoints.Any(pp => pp.Contains(issue.Location?.FileName ?? "")))
        {
            recommendation += " (Priority: High - affects identified pain point)";
        }
        
        LogMethodExit();
        return recommendation;
    }
}