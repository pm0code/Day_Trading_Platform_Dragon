using MediatR;
using AIRES.Application.Commands;
using AIRES.Application.Exceptions;
using AIRES.Application.Interfaces;
using AIRES.Core.Domain.ValueObjects;
using AIRES.Core.Health;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;
using AIRES.Foundation.Alerting;
using AIRES.Infrastructure.AI;
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
    private readonly IAIRESAlertingService _alerting;
    private readonly OllamaHealthCheckClient _healthCheckClient;

    public AIResearchOrchestratorService(
        IAIRESLogger logger,
        IMediator mediator,
        IBookletPersistenceService persistenceService,
        IAIRESAlertingService alerting,
        OllamaHealthCheckClient healthCheckClient) 
        : base(logger, nameof(AIResearchOrchestratorService))
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        _alerting = alerting ?? throw new ArgumentNullException(nameof(alerting));
        _healthCheckClient = healthCheckClient ?? throw new ArgumentNullException(nameof(healthCheckClient));
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
        // Delegate to the overload with null progress
        return await GenerateResearchBookletAsync(
            rawCompilerOutput,
            codeContext,
            projectStructureXml,
            projectCodebase,
            projectStandards,
            progress: null,
            cancellationToken);
    }

    /// <summary>
    /// Orchestrates the complete AI research pipeline for compiler error resolution with progress reporting.
    /// </summary>
    public async Task<AIRESResult<BookletGenerationResponse>> GenerateResearchBookletAsync(
        string rawCompilerOutput,
        string codeContext,
        string projectStructureXml,
        string projectCodebase,
        IImmutableList<string> projectStandards,
        IProgress<(string stage, double percentage)>? progress,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        var stepTimings = new Dictionary<string, long>();

        try
        {
            LogInfo("Starting AI Research Pipeline");
            
            // Report initial progress
            progress?.Report(("Initializing AI Pipeline", 0));

            // Step 1: Parse Compiler Errors
            LogInfo("Step 1/5: Parsing compiler errors");
            progress?.Report(("Parsing Compiler Errors", 5));
            var parseStopwatch = Stopwatch.StartNew();
            
            var parseResult = await _mediator.Send(
                new ParseCompilerErrorsCommand(rawCompilerOutput), 
                cancellationToken);
            
            stepTimings["ParseErrors"] = parseStopwatch.ElapsedMilliseconds;
            LogInfo($"Parsed {parseResult.TotalErrors} errors and {parseResult.TotalWarnings} warnings in {parseStopwatch.ElapsedMilliseconds}ms");
            progress?.Report(("Parsing Complete", 10));

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
            progress?.Report(("Starting Mistral Documentation Analysis", 15));
            var mistralStopwatch = Stopwatch.StartNew();
            
            DocumentationAnalysisResponse docAnalysis;
            try
            {
                progress?.Report(("Mistral: Analyzing documentation", 20));
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
            progress?.Report(("Mistral Analysis Complete", 30));

            // Step 3: DeepSeek Context Analysis
            LogInfo("Step 3/5: DeepSeek analyzing context");
            progress?.Report(("Starting DeepSeek Context Analysis", 35));
            var deepseekStopwatch = Stopwatch.StartNew();
            
            ContextAnalysisResponse contextAnalysis;
            try
            {
                progress?.Report(("DeepSeek: Analyzing code context", 40));
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
            progress?.Report(("DeepSeek Analysis Complete", 50));

            // Step 4: CodeGemma Pattern Validation
            LogInfo("Step 4/5: CodeGemma validating patterns");
            progress?.Report(("Starting CodeGemma Pattern Validation", 55));
            var codegemmaStopwatch = Stopwatch.StartNew();
            
            PatternValidationResponse patternValidation;
            try
            {
                progress?.Report(("CodeGemma: Validating code patterns", 60));
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
            progress?.Report(("CodeGemma Validation Complete", 70));

            if (!patternValidation.OverallCompliance && patternValidation.CriticalViolations.Any())
            {
                LogWarning($"Critical pattern violations detected: {string.Join(", ", patternValidation.CriticalViolations)}");
            }

            // Step 5: Gemma2 Booklet Generation
            LogInfo("Step 5/5: Gemma2 generating research booklet");
            progress?.Report(("Starting Gemma2 Booklet Generation", 75));
            var gemma2Stopwatch = Stopwatch.StartNew();
            
            BookletGenerationResponse bookletResponse;
            try
            {
                progress?.Report(("Gemma2: Synthesizing research booklet", 80));
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
            progress?.Report(("Booklet Generation Complete", 90));
            
            // Save the booklet to disk
            LogInfo($"Saving booklet to disk: {bookletResponse.BookletPath}");
            progress?.Report(("Saving booklet to disk", 95));
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
            
            // Report completion
            progress?.Report(("Pipeline Complete", 100));
            
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
            // First check if Ollama service itself is healthy
            var ollamaServiceHealth = await _healthCheckClient.CheckServiceHealthAsync();
            
            var status = new Dictionary<string, bool>
            {
                ["OllamaService"] = ollamaServiceHealth.Status == HealthStatus.Healthy,
                ["ParseCompilerErrors"] = true, // Always available
                ["MistralDocumentation"] = await CheckServiceHealthAsync("Mistral"),
                ["DeepSeekContext"] = await CheckServiceHealthAsync("DeepSeek"),
                ["CodeGemmaPatterns"] = await CheckServiceHealthAsync("CodeGemma"),
                ["Gemma2Booklet"] = await CheckServiceHealthAsync("Gemma2")
            };
            
            // Log Ollama service diagnostics if unhealthy
            if (ollamaServiceHealth.Status != HealthStatus.Healthy)
            {
                LogWarning("Ollama service is not healthy");
                LogWarning($"Service health details:\n{ollamaServiceHealth.GetDetailedReport()}");
                
                await _alerting.RaiseAlertAsync(
                    AlertSeverity.Critical,
                    ServiceName,
                    "Ollama service is not healthy",
                    new Dictionary<string, object>
                    {
                        ["status"] = ollamaServiceHealth.Status.ToString(),
                        ["responseTime"] = ollamaServiceHealth.ResponseTimeMs,
                        ["errorMessage"] = ollamaServiceHealth.ErrorMessage ?? "Unknown",
                        ["diagnostics"] = ollamaServiceHealth.Diagnostics
                    });
            }

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
        LogMethodEntry();
        
        try
        {
            // Map service name to model name
            string modelName = serviceName switch
            {
                "Mistral" => "mistral:7b-instruct-q4_K_M",
                "DeepSeek" => "deepseek-coder:6.7b",
                "CodeGemma" => "codegemma:7b-instruct",
                "Gemma2" => "gemma2:9b",
                _ => throw new ArgumentException($"Unknown service name: {serviceName}")
            };
            
            // Check model health with comprehensive diagnostics
            var healthResult = await _healthCheckClient.CheckModelHealthAsync(modelName);
            
            // Log detailed diagnostics for root cause analysis
            if (healthResult.Status != HealthStatus.Healthy)
            {
                LogWarning($"Model {modelName} health check result: {healthResult.Status}");
                LogWarning($"Health check details:\n{healthResult.GetDetailedReport()}");
                
                // Alert if model is unhealthy
                if (healthResult.Status == HealthStatus.Unhealthy)
                {
                    await _alerting.RaiseAlertAsync(
                        AlertSeverity.Warning,
                        ServiceName,
                        $"AI Model {modelName} is unhealthy",
                        new Dictionary<string, object>
                        {
                            ["model"] = modelName,
                            ["status"] = healthResult.Status.ToString(),
                            ["responseTime"] = healthResult.ResponseTimeMs,
                            ["errorMessage"] = healthResult.ErrorMessage ?? "Unknown",
                            ["diagnostics"] = healthResult.Diagnostics
                        });
                }
            }
            else
            {
                LogDebug($"Model {modelName} is healthy. Response time: {healthResult.ResponseTimeMs}ms");
            }
            
            LogMethodExit();
            return healthResult.Status == HealthStatus.Healthy;
        }
        catch (Exception ex)
        {
            LogError($"Failed to check health for service {serviceName}", ex);
            LogMethodExit();
            return false;
        }
    }
}