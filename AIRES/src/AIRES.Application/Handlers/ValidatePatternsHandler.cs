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
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Validate input
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (request.Errors == null)
            {
                throw new ArgumentNullException(nameof(request), "Request.Errors cannot be null");
            }
            // Make ProjectCodebase optional with a default
            if (string.IsNullOrWhiteSpace(request.ProjectCodebase))
            {
                LogWarning("No project codebase provided, using placeholder");
                request = request with { ProjectCodebase = "// No project codebase provided" };
            }
            // Make ProjectStandards optional with defaults
            if (request.ProjectStandards == null || request.ProjectStandards.Count == 0)
            {
                LogWarning("No project standards provided, using defaults");
                request = request with { ProjectStandards = ImmutableList.Create("Follow best practices", "Ensure code quality") };
            }
            if (request.ContextAnalysis == null)
            {
                throw new ArgumentNullException(nameof(request), "Request.ContextAnalysis cannot be null");
            }
            
            UpdateMetric("ValidatePatterns.Requests", 1);
            if (!request.Errors.Any())
            {
                UpdateMetric("ValidatePatterns.EmptyInputs", 1);
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

            stopwatch.Stop();
            UpdateMetric("ValidatePatterns.ResponseTime", stopwatch.ElapsedMilliseconds);
            UpdateMetric("ValidatePatterns.Successes", 1);
            UpdateMetric("ValidatePatterns.IssuesFound", validationFinding.IssuesIdentified.Count);
            UpdateMetric("ValidatePatterns.CompliantPatterns", validationFinding.CompliantPatterns.Count);
            UpdateMetric("ValidatePatterns.CriticalViolations", criticalViolations.Count);
            UpdateMetric("ValidatePatterns.ComplianceRate", overallCompliance ? 1 : 0);
            
            LogInfo($"Pattern validation complete. Compliance: {overallCompliance}, Critical violations: {criticalViolations.Count} in {stopwatch.ElapsedMilliseconds}ms");
            LogMethodExit();
            
            return new PatternValidationResponse(
                validationFinding,
                overallCompliance,
                criticalViolations,
                recommendations
            );
        }
        catch (ArgumentNullException ex)
        {
            UpdateMetric("ValidatePatterns.ValidationErrors", 1);
            LogError("Invalid input parameters", ex);
            LogMethodExit();
            throw;
        }
        catch (ArgumentException ex)
        {
            UpdateMetric("ValidatePatterns.ValidationErrors", 1);
            LogError("Invalid input parameters", ex);
            LogMethodExit();
            throw;
        }
        catch (CodeGemmaValidationException)
        {
            UpdateMetric("ValidatePatterns.Failures", 1);
            throw; // Re-throw specific exceptions
        }
        catch (Exception ex)
        {
            UpdateMetric("ValidatePatterns.UnexpectedErrors", 1);
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