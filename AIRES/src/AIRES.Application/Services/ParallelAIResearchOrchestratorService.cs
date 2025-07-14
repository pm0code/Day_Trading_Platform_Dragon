using MediatR;
using AIRES.Application.Commands;
using AIRES.Application.Exceptions;
using AIRES.Application.Interfaces;
using AIRES.Core.Domain.ValueObjects;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;
using System.Diagnostics;
using System.Collections.Immutable;

namespace AIRES.Application.Services;

/// <summary>
/// PARALLEL VERSION: Orchestrates the 4-stage AI research pipeline for error resolution.
/// Runs Mistral, DeepSeek, and CodeGemma in parallel for improved performance.
/// </summary>
public class ParallelAIResearchOrchestratorService : AIRESServiceBase, IAIResearchOrchestratorService
{
    private readonly IMediator _mediator;
    private readonly IBookletPersistenceService _persistenceService;

    public ParallelAIResearchOrchestratorService(
        IAIRESLogger logger,
        IMediator mediator,
        IBookletPersistenceService persistenceService) 
        : base(logger, nameof(ParallelAIResearchOrchestratorService))
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
    }

    /// <summary>
    /// Orchestrates the complete AI research pipeline with parallel execution where possible.
    /// </summary>
    public async Task<AIRESResult<BookletGenerationResponse>> GenerateResearchBookletAsync(
        string rawCompilerOutput,
        string codeContext,
        string projectStructureXml,
        string projectCodebase,
        IImmutableList<string> projectStandards,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        var stepTimings = new Dictionary<string, long>();

        try
        {
            LogInfo("Starting PARALLEL AI Research Pipeline");

            // Step 1: Parse Compiler Errors (MUST be sequential - everything depends on this)
            LogInfo("Step 1/5: Parsing compiler errors");
            var parseStopwatch = Stopwatch.StartNew();
            
            var parseResult = await _mediator.Send(
                new ParseCompilerErrorsCommand(rawCompilerOutput), 
                cancellationToken);
            
            stepTimings["ParseErrors"] = parseStopwatch.ElapsedMilliseconds;
            LogInfo($"Parsed {parseResult.TotalErrors} errors and {parseResult.TotalWarnings} warnings in {parseStopwatch.ElapsedMilliseconds}ms");

            if (parseResult.Errors.Count == 0)
            {
                LogWarning("No compiler errors found. Nothing to analyze.");
                LogMethodExit();
                return AIRESResult<BookletGenerationResponse>.Failure(
                    "NO_ERRORS_FOUND",
                    "No compiler errors found in the provided output"
                );
            }

            // Steps 2-4: Run Mistral, DeepSeek, and CodeGemma IN PARALLEL
            LogInfo("Steps 2-4: Running Mistral, DeepSeek, and CodeGemma analysis in PARALLEL");
            var parallelStopwatch = Stopwatch.StartNew();

            // Create tasks for parallel execution
            var mistralTask = RunMistralAnalysisAsync(parseResult, codeContext, cancellationToken);
            var deepSeekTask = RunDeepSeekAnalysisAsync(parseResult, codeContext, projectStructureXml, cancellationToken);
            var codeGemmaTask = RunCodeGemmaValidationAsync(parseResult, projectStandards, cancellationToken);

            // Wait for all three to complete
            await Task.WhenAll(mistralTask, deepSeekTask, codeGemmaTask);
            
            var docAnalysis = await mistralTask;
            var contextAnalysis = await deepSeekTask;
            var patternValidation = await codeGemmaTask;

            var parallelTime = parallelStopwatch.ElapsedMilliseconds;
            LogInfo($"Parallel analysis completed in {parallelTime}ms");
            
            // Record individual timings (these ran in parallel, so they overlap)
            stepTimings["MistralAnalysis"] = GetTaskTiming("MistralAnalysis");
            stepTimings["DeepSeekAnalysis"] = GetTaskTiming("DeepSeekAnalysis");
            stepTimings["CodeGemmaValidation"] = GetTaskTiming("CodeGemmaValidation");
            stepTimings["ParallelExecutionTime"] = parallelTime;

            // Step 5: Gemma2 Booklet Generation (Sequential - depends on all previous results)
            LogInfo("Step 5/5: Gemma2 generating research booklet");
            var gemma2Stopwatch = Stopwatch.StartNew();
            
            BookletGenerationResponse bookletResponse;
            try
            {
                // Generate booklet
                bookletResponse = await _mediator.Send(
                    new GenerateBookletCommand(
                        Guid.NewGuid(),
                        parseResult.Errors,
                        docAnalysis,
                        contextAnalysis,
                        patternValidation),
                    cancellationToken);
            }
            catch (Gemma2GenerationException ex)
            {
                LogError("Gemma2 booklet generation failed", ex);
                LogMethodExit();
                return AIRESResult<BookletGenerationResponse>.Failure(
                    ex.ErrorCode ?? "GEMMA2_GENERATION_ERROR",
                    $"Booklet generation failed: {ex.Message}",
                    ex
                );
            }
            
            stepTimings["Gemma2Generation"] = gemma2Stopwatch.ElapsedMilliseconds;
            
            // Save the booklet to disk
            LogInfo($"Saving booklet to disk: {bookletResponse.BookletPath}");
            var saveResult = await _persistenceService.SaveBookletAsync(
                bookletResponse.Booklet,
                bookletResponse.BookletPath,
                cancellationToken);
                
            if (!saveResult.IsSuccess)
            {
                LogError($"Failed to save booklet: {saveResult.ErrorMessage}");
                LogMethodExit();
                return AIRESResult<BookletGenerationResponse>.Failure(
                    saveResult.ErrorCode ?? "BOOKLET_SAVE_ERROR",
                    $"Failed to save booklet: {saveResult.ErrorMessage}"
                );
            }
            
            // Update total processing time
            stopwatch.Stop();
            var totalResponse = new BookletGenerationResponse(
                bookletResponse.Booklet,
                saveResult.Value!, // Use the actual saved path
                stopwatch.ElapsedMilliseconds,
                stepTimings.ToImmutableDictionary()
            );

            LogInfo($"PARALLEL AI Research Pipeline completed successfully in {stopwatch.ElapsedMilliseconds}ms");
            LogInfo($"Time saved by parallel execution: {CalculateTimeSaved(stepTimings)}ms");
            LogInfo($"Booklet saved to: {saveResult.Value}");
            
            LogMethodExit();
            return AIRESResult<BookletGenerationResponse>.Success(totalResponse);
        }
        catch (Exception ex)
        {
            LogError("Unexpected error in PARALLEL AI Research Pipeline", ex);
            LogMethodExit();
            return AIRESResult<BookletGenerationResponse>.Failure(
                "PARALLEL_ORCHESTRATOR_ERROR",
                $"An unexpected error occurred: {ex.Message}",
                ex
            );
        }
    }

    private async Task<DocumentationAnalysisResponse> RunMistralAnalysisAsync(
        ParseCompilerErrorsResponse parseResult,
        string codeContext,
        CancellationToken cancellationToken)
    {
        LogMethodEntry();
        var sw = Stopwatch.StartNew();
        
        try
        {
            LogInfo("Starting Mistral documentation analysis (parallel)");
            var result = await _mediator.Send(
                new AnalyzeDocumentationCommand(parseResult.Errors, codeContext),
                cancellationToken);
            
            UpdateMetric("MistralAnalysis", sw.ElapsedMilliseconds);
            LogInfo($"Mistral analysis completed in {sw.ElapsedMilliseconds}ms with {result.Findings.Count} findings");
            LogMethodExit();
            return result;
        }
        catch (MistralAnalysisFailedException ex)
        {
            LogError("Mistral documentation analysis failed", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task<ContextAnalysisResponse> RunDeepSeekAnalysisAsync(
        ParseCompilerErrorsResponse parseResult,
        string codeContext,
        string projectStructureXml,
        CancellationToken cancellationToken)
    {
        LogMethodEntry();
        var sw = Stopwatch.StartNew();
        
        try
        {
            LogInfo("Starting DeepSeek context analysis (parallel)");
            
            // Create a minimal DocumentationAnalysisResponse for DeepSeek
            // Since we're running in parallel, DeepSeek doesn't wait for Mistral
            var minimalDocAnalysis = new DocumentationAnalysisResponse(
                ImmutableList<ErrorDocumentationFinding>.Empty,
                "Parallel execution - documentation analysis running concurrently",
                ImmutableDictionary<string, string>.Empty
            );
            
            var result = await _mediator.Send(
                new AnalyzeContextCommand(
                    parseResult.Errors,
                    minimalDocAnalysis,
                    codeContext,
                    projectStructureXml),
                cancellationToken);
            
            UpdateMetric("DeepSeekAnalysis", sw.ElapsedMilliseconds);
            LogInfo($"DeepSeek analysis completed in {sw.ElapsedMilliseconds}ms with {result.IdentifiedPainPoints.Count} pain points");
            LogMethodExit();
            return result;
        }
        catch (DeepSeekContextAnalysisException ex)
        {
            LogError("DeepSeek context analysis failed", ex);
            LogMethodExit();
            throw;
        }
    }

    private async Task<PatternValidationResponse> RunCodeGemmaValidationAsync(
        ParseCompilerErrorsResponse parseResult,
        IImmutableList<string> projectStandards,
        CancellationToken cancellationToken)
    {
        LogMethodEntry();
        var sw = Stopwatch.StartNew();
        
        try
        {
            LogInfo("Starting CodeGemma pattern validation (parallel)");
            // Create a minimal ContextAnalysisResponse for CodeGemma
            // Since we're running in parallel, CodeGemma doesn't wait for DeepSeek
            var minimalContextAnalysis = new ContextAnalysisResponse(
                ImmutableList<ContextAnalysisFinding>.Empty,
                "Parallel execution - context analysis running concurrently",
                ImmutableList<string>.Empty,
                ImmutableDictionary<string, string>.Empty
            );
            
            var result = await _mediator.Send(
                new ValidatePatternsCommand(
                    parseResult.Errors,
                    minimalContextAnalysis,
                    "", // projectCodebase - empty for now
                    projectStandards),
                cancellationToken);
            
            UpdateMetric("CodeGemmaValidation", sw.ElapsedMilliseconds);
            LogInfo($"CodeGemma validation completed in {sw.ElapsedMilliseconds}ms. Compliance: {result.OverallCompliance}");
            LogMethodExit();
            return result;
        }
        catch (CodeGemmaValidationException ex)
        {
            LogError("CodeGemma pattern validation failed", ex);
            LogMethodExit();
            throw;
        }
    }

    private long GetTaskTiming(string metricName)
    {
        var metrics = GetMetrics();
        return (long)(metrics.GetValueOrDefault(metricName) ?? 0);
    }

    private long CalculateTimeSaved(Dictionary<string, long> timings)
    {
        // Calculate how much time we saved by running in parallel
        var sequentialTime = timings.GetValueOrDefault("MistralAnalysis") +
                           timings.GetValueOrDefault("DeepSeekAnalysis") +
                           timings.GetValueOrDefault("CodeGemmaValidation");
        
        var parallelTime = timings.GetValueOrDefault("ParallelExecutionTime");
        
        return Math.Max(0, sequentialTime - parallelTime);
    }

    /// <summary>
    /// Gets the status of the AI research pipeline.
    /// </summary>
    public async Task<AIRESResult<Dictionary<string, bool>>> GetPipelineStatusAsync()
    {
        LogMethodEntry();
        
        try
        {
            var status = new Dictionary<string, bool>
            {
                ["ParseCompilerErrors"] = true, // Always available
                ["MistralDocumentation"] = await CheckServiceHealthAsync("Mistral"),
                ["DeepSeekContext"] = await CheckServiceHealthAsync("DeepSeek"),
                ["CodeGemmaPatterns"] = await CheckServiceHealthAsync("CodeGemma"),
                ["Gemma2Booklet"] = await CheckServiceHealthAsync("Gemma2"),
                ["ParallelMode"] = true // Indicator that this is the parallel version
            };

            LogInfo($"Pipeline status (PARALLEL): {string.Join(", ", status.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            LogMethodExit();
            return AIRESResult<Dictionary<string, bool>>.Success(status);
        }
        catch (Exception ex)
        {
            LogError("Failed to get pipeline status", ex);
            LogMethodExit();
            return AIRESResult<Dictionary<string, bool>>.Failure(
                "PIPELINE_STATUS_ERROR",
                "Failed to retrieve pipeline status",
                ex
            );
        }
    }

    private async Task<bool> CheckServiceHealthAsync(string serviceName)
    {
        // TODO: Implement actual health checks for each AI service
        await Task.Delay(10); // Placeholder
        return true;
    }
}