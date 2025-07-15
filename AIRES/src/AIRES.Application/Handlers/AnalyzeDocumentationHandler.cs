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
/// Handler for Mistral documentation analysis command.
/// </summary>
public class AnalyzeDocumentationHandler : AIRESServiceBase, IRequestHandler<AnalyzeDocumentationCommand, DocumentationAnalysisResponse>
{
    private readonly IErrorDocumentationAIModel _mistralService;

    public AnalyzeDocumentationHandler(
        IAIRESLogger logger,
        IErrorDocumentationAIModel mistralService) 
        : base(logger, nameof(AnalyzeDocumentationHandler))
    {
        _mistralService = mistralService ?? throw new ArgumentNullException(nameof(mistralService));
    }

    public async Task<DocumentationAnalysisResponse> Handle(
        AnalyzeDocumentationCommand request, 
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
            // Make RelevantCode optional with a default
            if (string.IsNullOrWhiteSpace(request.RelevantCode))
            {
                LogWarning("No relevant code provided, using placeholder");
                request = request with { RelevantCode = "// No code context provided" };
            }
            
            UpdateMetric("AnalyzeDocumentation.Requests", 1);
            if (!request.Errors.Any())
            {
                UpdateMetric("AnalyzeDocumentation.EmptyInputs", 1);
                LogWarning("No errors provided for documentation analysis");
                LogMethodExit();
                return new DocumentationAnalysisResponse(
                    ImmutableList<ErrorDocumentationFinding>.Empty,
                    "No errors to analyze",
                    ImmutableDictionary<string, string>.Empty
                );
            }

            LogInfo($"Analyzing documentation for {request.Errors.Count} compiler errors");

            var findings = new List<ErrorDocumentationFinding>();
            var suggestedFixes = new Dictionary<string, string>();

            // Process each unique error code
            var errorGroups = request.Errors.GroupBy(e => e.Code);
            
            foreach (var errorGroup in errorGroups)
            {
                var representativeError = errorGroup.First();
                LogDebug($"Analyzing documentation for error {representativeError.Code}");

                try
                {
                    var finding = await _mistralService.AnalyzeAsync(
                        representativeError, 
                        request.RelevantCode
                    );
                    
                    findings.Add(finding);
                    
                    // Extract suggested fix if available
                    if (!string.IsNullOrWhiteSpace(finding.SuggestedDocsLink))
                    {
                        suggestedFixes[representativeError.Code] = finding.SuggestedDocsLink;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Failed to analyze documentation for error {representativeError.Code}", ex);
                    // Continue with other errors
                }
            }

            if (findings.Count == 0)
            {
                UpdateMetric("AnalyzeDocumentation.NoFindings", 1);
                LogError("Mistral failed to analyze any errors");
                LogMethodExit();
                throw new MistralAnalysisFailedException(
                    "Failed to generate documentation analysis for any errors",
                    "MISTRAL_NO_FINDINGS"
                );
            }

            var overallInsights = GenerateOverallInsights(findings);
            
            stopwatch.Stop();
            UpdateMetric("AnalyzeDocumentation.ResponseTime", stopwatch.ElapsedMilliseconds);
            UpdateMetric("AnalyzeDocumentation.Successes", 1);
            UpdateMetric("AnalyzeDocumentation.FindingsCount", findings.Count);
            UpdateMetric("AnalyzeDocumentation.ErrorsAnalyzed", errorGroups.Count());
            
            LogInfo($"Documentation analysis complete with {findings.Count} findings in {stopwatch.ElapsedMilliseconds}ms");
            LogMethodExit();
            
            return new DocumentationAnalysisResponse(
                findings.ToImmutableList(),
                overallInsights,
                suggestedFixes.ToImmutableDictionary()
            );
        }
        catch (ArgumentNullException ex)
        {
            UpdateMetric("AnalyzeDocumentation.ValidationErrors", 1);
            LogError("Invalid input parameters", ex);
            LogMethodExit();
            throw;
        }
        catch (ArgumentException ex)
        {
            UpdateMetric("AnalyzeDocumentation.ValidationErrors", 1);
            LogError("Invalid input parameters", ex);
            LogMethodExit();
            throw;
        }
        catch (MistralAnalysisFailedException)
        {
            UpdateMetric("AnalyzeDocumentation.Failures", 1);
            throw; // Re-throw specific exceptions
        }
        catch (Exception ex)
        {
            UpdateMetric("AnalyzeDocumentation.UnexpectedErrors", 1);
            LogError("Unexpected error during documentation analysis", ex);
            LogMethodExit();
            throw new MistralAnalysisFailedException(
                $"Failed to analyze documentation: {ex.Message}", 
                ex,
                "MISTRAL_UNEXPECTED_ERROR"
            );
        }
    }

    private string GenerateOverallInsights(List<ErrorDocumentationFinding> findings)
    {
        LogMethodEntry();
        
        var insights = "Documentation Analysis Summary:\n\n";
        
        foreach (var finding in findings)
        {
            insights += $"• {finding.Title}:\n";
            insights += $"  {finding.Content.Split('\n').FirstOrDefault()}\n\n";
        }
        
        insights += $"Total findings: {findings.Count}\n";
        insights += $"Documentation links provided: {findings.Count(f => !string.IsNullOrEmpty(f.SuggestedDocsLink))}";
        
        LogMethodExit();
        return insights;
    }
}