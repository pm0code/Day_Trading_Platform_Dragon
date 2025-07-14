using System.Collections.Immutable;
using AIRES.Application.Commands;
using AIRES.Foundation.Results;

namespace AIRES.Application.Interfaces;

/// <summary>
/// Interface for the AI Research Orchestrator Service.
/// Orchestrates the 4-stage AI pipeline for error resolution.
/// </summary>
public interface IAIResearchOrchestratorService
{
    /// <summary>
    /// Generates a comprehensive research booklet for compiler errors.
    /// </summary>
    /// <param name="rawCompilerOutput">The raw compiler output containing errors</param>
    /// <param name="codeContext">Relevant code context around the errors</param>
    /// <param name="projectStructureXml">Project structure in XML format</param>
    /// <param name="projectCodebase">Relevant project codebase snippets</param>
    /// <param name="projectStandards">List of project coding standards</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the generated booklet or error details</returns>
    Task<AIRESResult<BookletGenerationResponse>> GenerateResearchBookletAsync(
        string rawCompilerOutput,
        string codeContext,
        string projectStructureXml,
        string projectCodebase,
        IImmutableList<string> projectStandards,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the status of the AI research pipeline.
    /// </summary>
    /// <returns>Dictionary indicating health status of each pipeline stage</returns>
    Task<AIRESResult<Dictionary<string, bool>>> GetPipelineStatusAsync();
}