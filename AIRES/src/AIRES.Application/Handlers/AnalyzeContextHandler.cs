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
/// Handler for DeepSeek context analysis command.
/// Focused wrapper that calls the AI service and normalizes output.
/// </summary>
public class AnalyzeContextHandler : AIRESServiceBase, IRequestHandler<AnalyzeContextCommand, ContextAnalysisResponse>
{
    private readonly IContextAnalyzerAIModel _deepSeekService;

    public AnalyzeContextHandler(
        IAIRESLogger logger,
        IContextAnalyzerAIModel deepSeekService) 
        : base(logger, nameof(AnalyzeContextHandler))
    {
        _deepSeekService = deepSeekService ?? throw new ArgumentNullException(nameof(deepSeekService));
    }

    public async Task<ContextAnalysisResponse> Handle(
        AnalyzeContextCommand request, 
        CancellationToken cancellationToken)
    {
        LogMethodEntry();
        
        try
        {
            if (!request.Errors.Any())
            {
                LogWarning("No errors provided for context analysis");
                LogMethodExit();
                return new ContextAnalysisResponse(
                    ImmutableList<ContextAnalysisFinding>.Empty,
                    "No errors to analyze",
                    ImmutableList<string>.Empty,
                    ImmutableDictionary<string, string>.Empty
                );
            }

            LogInfo($"Analyzing context for {request.Errors.Count} compiler errors");

            var findings = new List<ContextAnalysisFinding>();
            var painPoints = new List<string>();
            var contextualSolutions = new Dictionary<string, string>();

            // Process each unique error location for context analysis
            var errorGroups = request.Errors.GroupBy(e => e.Location.FilePath);
            
            foreach (var errorGroup in errorGroups)
            {
                var representativeError = errorGroup.First();
                LogDebug($"Analyzing context for errors in {representativeError.Location.FileName}");

                try
                {
                    var finding = await _deepSeekService.AnalyzeAsync(
                        representativeError,
                        request.SurroundingCode,
                        request.ProjectStructureXml
                    );
                    
                    findings.Add(finding);
                    
                    // Extract pain points from the analysis
                    ExtractPainPoints(finding, painPoints);
                    
                    // Extract contextual solutions
                    ExtractContextualSolutions(finding, errorGroup, contextualSolutions);
                }
                catch (Exception ex)
                {
                    LogError($"Failed to analyze context for {representativeError.Location.FileName}", ex);
                    // Continue with other files
                }
            }

            if (findings.Count == 0)
            {
                LogError("DeepSeek failed to analyze any contexts");
                LogMethodExit();
                throw new DeepSeekContextAnalysisException(
                    "Failed to generate context analysis for any errors",
                    "DEEPSEEK_NO_FINDINGS"
                );
            }

            var deepUnderstanding = GenerateDeepUnderstanding(findings, request.DocAnalysis);
            
            LogInfo($"Context analysis complete with {findings.Count} findings and {painPoints.Count} pain points");
            LogMethodExit();
            
            return new ContextAnalysisResponse(
                findings.ToImmutableList(),
                deepUnderstanding,
                painPoints.Distinct().ToImmutableList(),
                contextualSolutions.ToImmutableDictionary()
            );
        }
        catch (DeepSeekContextAnalysisException)
        {
            throw; // Re-throw specific exceptions
        }
        catch (Exception ex)
        {
            LogError("Unexpected error during context analysis", ex);
            LogMethodExit();
            throw new DeepSeekContextAnalysisException(
                $"Failed to analyze context: {ex.Message}", 
                ex,
                "DEEPSEEK_UNEXPECTED_ERROR"
            );
        }
    }

    private void ExtractPainPoints(ContextAnalysisFinding finding, List<string> painPoints)
    {
        LogMethodEntry();
        
        // Simple extraction - in real implementation, parse the finding content
        var lines = finding.Content.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("pain point", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("issue", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("problem", StringComparison.OrdinalIgnoreCase))
            {
                painPoints.Add(line.Trim());
            }
        }
        
        LogMethodExit();
    }

    private void ExtractContextualSolutions(
        ContextAnalysisFinding finding, 
        IGrouping<string, CompilerError> errorGroup,
        Dictionary<string, string> solutions)
    {
        LogMethodEntry();
        
        // Extract solutions for each error code in this file
        foreach (var error in errorGroup)
        {
            var key = $"{error.Code}:{error.Location.FileName}";
            if (!solutions.ContainsKey(key))
            {
                // In real implementation, parse specific solutions from finding
                solutions[key] = $"Apply context-aware fix for {error.Code} based on surrounding code structure";
            }
        }
        
        LogMethodExit();
    }

    private string GenerateDeepUnderstanding(
        List<ContextAnalysisFinding> findings, 
        DocumentationAnalysisResponse docAnalysis)
    {
        LogMethodEntry();
        
        var understanding = "Deep Context Analysis:\n\n";
        
        // Combine insights from context analysis with documentation analysis
        understanding += $"Documentation Insights:\n{docAnalysis.OverallInsights}\n\n";
        
        understanding += "Code Context Findings:\n";
        foreach (var finding in findings)
        {
            understanding += $"\n{finding.Title}:\n";
            understanding += $"- Project Structure: {finding.ProjectStructureOverview}\n";
            understanding += $"- Relevant Code: {finding.RelevantCodeSnippet.Split('\n').FirstOrDefault()}\n";
        }
        
        understanding += $"\nTotal context analyses: {findings.Count}";
        
        LogMethodExit();
        return understanding;
    }
}