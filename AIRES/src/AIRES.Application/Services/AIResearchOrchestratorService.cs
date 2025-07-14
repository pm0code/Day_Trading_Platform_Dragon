using MediatR;
using AIRES.Application.Commands;
using AIRES.Application.Exceptions;
using AIRES.Application.Interfaces;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;
using System.Diagnostics;
using System.Collections.Immutable;

namespace AIRES.Application.Services;

/// <summary>
/// Orchestrates the 4-stage AI research pipeline for error resolution.
/// Uses MediatR for loose coupling and maintainability as recommended by Gemini AI.
/// </summary>
public class AIResearchOrchestratorService : AIRESServiceBase, IAIResearchOrchestratorService
{
    private readonly IMediator _mediator;
    private readonly IBookletPersistenceService _persistenceService;

    public AIResearchOrchestratorService(
        IAIRESLogger logger,
        IMediator mediator,
        IBookletPersistenceService persistenceService) 
        : base(logger, nameof(AIResearchOrchestratorService))
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
    }

    /// <summary>
    /// Orchestrates the complete AI research pipeline for compiler error resolution.
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
            LogInfo("Starting AI Research Pipeline");

            // Step 1: Parse Compiler Errors
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

            // Step 2: Mistral Documentation Analysis
            LogInfo("Step 2/5: Mistral analyzing documentation");
            var mistralStopwatch = Stopwatch.StartNew();
            
            DocumentationAnalysisResponse docAnalysis;
            try
            {
                docAnalysis = await _mediator.Send(
                    new AnalyzeDocumentationCommand(parseResult.Errors, codeContext),
                    cancellationToken);
            }
            catch (MistralAnalysisFailedException ex)
            {
                LogError("Mistral documentation analysis failed", ex);
                LogMethodExit();
                return AIRESResult<BookletGenerationResponse>.Failure(
                    ex.ErrorCode ?? "MISTRAL_ANALYSIS_ERROR",
                    $"Documentation analysis failed: {ex.Message}",
                    ex
                );
            }
            
            stepTimings["MistralAnalysis"] = mistralStopwatch.ElapsedMilliseconds;
            LogInfo($"Mistral analysis complete with {docAnalysis.Findings.Count} findings in {mistralStopwatch.ElapsedMilliseconds}ms");

            // Step 3: DeepSeek Context Analysis
            LogInfo("Step 3/5: DeepSeek analyzing context");
            var deepseekStopwatch = Stopwatch.StartNew();
            
            ContextAnalysisResponse contextAnalysis;
            try
            {
                contextAnalysis = await _mediator.Send(
                    new AnalyzeContextCommand(
                        parseResult.Errors,
                        docAnalysis,
                        codeContext,
                        projectStructureXml),
                    cancellationToken);
            }
            catch (DeepSeekContextAnalysisException ex)
            {
                LogError("DeepSeek context analysis failed", ex);
                LogMethodExit();
                return AIRESResult<BookletGenerationResponse>.Failure(
                    ex.ErrorCode ?? "DEEPSEEK_CONTEXT_ERROR",
                    $"Context analysis failed: {ex.Message}",
                    ex
                );
            }
            
            stepTimings["DeepSeekAnalysis"] = deepseekStopwatch.ElapsedMilliseconds;
            LogInfo($"DeepSeek analysis complete with {contextAnalysis.IdentifiedPainPoints.Count} pain points in {deepseekStopwatch.ElapsedMilliseconds}ms");

            // Step 4: CodeGemma Pattern Validation
            LogInfo("Step 4/5: CodeGemma validating patterns");
            var codegemmaStopwatch = Stopwatch.StartNew();
            
            PatternValidationResponse patternValidation;
            try
            {
                patternValidation = await _mediator.Send(
                    new ValidatePatternsCommand(
                        parseResult.Errors,
                        contextAnalysis,
                        projectCodebase,
                        projectStandards),
                    cancellationToken);
            }
            catch (CodeGemmaValidationException ex)
            {
                LogError("CodeGemma pattern validation failed", ex);
                LogMethodExit();
                return AIRESResult<BookletGenerationResponse>.Failure(
                    ex.ErrorCode ?? "CODEGEMMA_VALIDATION_ERROR",
                    $"Pattern validation failed: {ex.Message}",
                    ex
                );
            }
            
            stepTimings["CodeGemmaValidation"] = codegemmaStopwatch.ElapsedMilliseconds;
            LogInfo($"CodeGemma validation complete. Overall compliance: {patternValidation.OverallCompliance} in {codegemmaStopwatch.ElapsedMilliseconds}ms");

            if (!patternValidation.OverallCompliance && patternValidation.CriticalViolations.Any())
            {
                LogWarning($"Critical pattern violations detected: {string.Join(", ", patternValidation.CriticalViolations)}");
            }

            // Step 5: Gemma2 Booklet Generation
            LogInfo("Step 5/5: Gemma2 generating research booklet");
            var gemma2Stopwatch = Stopwatch.StartNew();
            
            BookletGenerationResponse bookletResponse;
            try
            {
                var errorBatchId = Guid.NewGuid();
                bookletResponse = await _mediator.Send(
                    new GenerateBookletCommand(
                        errorBatchId,
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

            LogInfo($"AI Research Pipeline completed successfully in {stopwatch.ElapsedMilliseconds}ms");
            LogInfo($"Booklet saved to: {saveResult.Value}");
            
            LogMethodExit();
            return AIRESResult<BookletGenerationResponse>.Success(totalResponse);
        }
        catch (Exception ex)
        {
            LogError("Unexpected error in AI Research Pipeline", ex);
            LogMethodExit();
            return AIRESResult<BookletGenerationResponse>.Failure(
                "ORCHESTRATOR_UNEXPECTED_ERROR",
                $"An unexpected error occurred: {ex.Message}",
                ex
            );
        }
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
                ["Gemma2Booklet"] = await CheckServiceHealthAsync("Gemma2")
            };

            LogInfo($"Pipeline status: {string.Join(", ", status.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
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