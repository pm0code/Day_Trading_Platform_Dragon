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
using System.Threading.Channels;

namespace AIRES.Application.Services;

/// <summary>
/// CONCURRENT VERSION: Orchestrates the 4-stage AI research pipeline for error resolution.
/// Uses proper task continuations with dependency management and semaphore throttling.
/// Based on industry best practices and Gemini architectural guidance.
/// </summary>
public class ConcurrentAIResearchOrchestratorService : AIRESServiceBase, IAIResearchOrchestratorService
{
    private readonly IMediator _mediator;
    private readonly BookletPersistenceService _persistenceService;
    // TODO: Implement IAIRESAlertingService and uncomment
    // private readonly IAIRESAlertingService _alerting;
    private readonly SemaphoreSlim _ollamaSemaphore;
    private readonly List<Exception> _errorAggregator = new();

    public ConcurrentAIResearchOrchestratorService(
        IAIRESLogger logger,
        IMediator mediator,
        BookletPersistenceService persistenceService) 
        : base(logger, nameof(ConcurrentAIResearchOrchestratorService))
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        // TODO: Uncomment when IAIRESAlertingService is implemented
        // _alerting = alerting ?? throw new ArgumentNullException(nameof(alerting));
        
        // Limit concurrent Ollama requests based on testing
        // Start with 3, can be adjusted based on Ollama's capacity
        _ollamaSemaphore = new SemaphoreSlim(3);
    }

    /// <summary>
    /// Orchestrates the complete AI research pipeline with concurrent execution using proper dependency management.
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
        _errorAggregator.Clear();

        try
        {
            LogInfo("Starting CONCURRENT AI Research Pipeline with dependency management");

            // Step 1: Parse Compiler Errors (Sequential - foundation for all other steps)
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

            // Create progress channel for UI updates
            var progressChannel = Channel.CreateUnbounded<ProgressUpdate>();
            _ = ReportProgressAsync(progressChannel.Reader, cancellationToken);

            // Start all stages with proper dependency management
            DocumentationAnalysisResponse? docAnalysis = null;
            ContextAnalysisResponse? contextAnalysis = null;
            PatternValidationResponse? patternValidation = null;
            
            // Stage 2: Mistral Documentation Analysis (can start immediately)
            var mistralTask = ProcessWithRetryAsync(
                async () =>
                {
                    await progressChannel.Writer.WriteAsync(new ProgressUpdate("Mistral Documentation Analysis", 25), cancellationToken);
                    var result = await RunMistralAnalysisAsync(parseResult, codeContext, cancellationToken);
                    stepTimings["MistralAnalysis"] = GetMetricValue("MistralAnalysis.Duration");
                    docAnalysis = result;
                    return result;
                },
                "Mistral Analysis", 
                cancellationToken);

            // Stage 3: DeepSeek Context Analysis (depends on Mistral)
            var deepSeekTask = mistralTask.ContinueWith(async _ =>
            {
                if (docAnalysis == null)
                {
                    throw new DeepSeekContextAnalysisException("Mistral analysis failed", "DEPENDENCY_FAILURE");
                }
                
                await progressChannel.Writer.WriteAsync(new ProgressUpdate("DeepSeek Context Analysis", 45), cancellationToken);
                var result = await RunDeepSeekAnalysisAsync(
                    parseResult, 
                    docAnalysis, 
                    codeContext, 
                    projectStructureXml, 
                    cancellationToken);
                stepTimings["DeepSeekAnalysis"] = GetMetricValue("DeepSeekAnalysis.Duration");
                contextAnalysis = result;
                return result;
            }, 
            cancellationToken,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default).Unwrap();

            // Stage 4: CodeGemma Pattern Validation (depends on DeepSeek)
            var codeGemmaTask = deepSeekTask.ContinueWith(async _ =>
            {
                if (contextAnalysis == null)
                {
                    throw new CodeGemmaValidationException("DeepSeek analysis failed", "DEPENDENCY_FAILURE");
                }
                
                await progressChannel.Writer.WriteAsync(new ProgressUpdate("CodeGemma Pattern Validation", 65), cancellationToken);
                var result = await RunCodeGemmaValidationAsync(
                    parseResult,
                    contextAnalysis,
                    projectCodebase,
                    projectStandards,
                    cancellationToken);
                stepTimings["CodeGemmaValidation"] = GetMetricValue("CodeGemmaValidation.Duration");
                patternValidation = result;
                return result;
            },
            cancellationToken,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default).Unwrap();

            // Wait for all analysis stages to complete
            await Task.WhenAll(mistralTask, deepSeekTask, codeGemmaTask);
            
            // Check if all succeeded
            if (mistralTask.Status != TaskStatus.RanToCompletion ||
                deepSeekTask.Status != TaskStatus.RanToCompletion ||
                codeGemmaTask.Status != TaskStatus.RanToCompletion)
            {
                var failedStages = new List<string>();
                if (mistralTask.Status != TaskStatus.RanToCompletion) failedStages.Add("Mistral");
                if (deepSeekTask.Status != TaskStatus.RanToCompletion) failedStages.Add("DeepSeek");
                if (codeGemmaTask.Status != TaskStatus.RanToCompletion) failedStages.Add("CodeGemma");
                
                LogError($"Pipeline failed at stages: {string.Join(", ", failedStages)}");
                LogMethodExit();
                return AIRESResult<BookletGenerationResponse>.Failure(
                    "PIPELINE_STAGE_FAILURE",
                    $"Analysis pipeline failed at: {string.Join(", ", failedStages)}"
                );
            }

            // Ensure all values are populated
            if (docAnalysis == null || contextAnalysis == null || patternValidation == null)
            {
                throw new InvalidOperationException("One or more analysis results are null");
            }

            // Stage 5: Gemma2 Booklet Generation (Sequential - depends on all previous results)
            await progressChannel.Writer.WriteAsync(new ProgressUpdate("Gemma2 Booklet Generation", 85), cancellationToken);
            LogInfo("Step 5/5: Gemma2 generating research booklet");
            var gemma2Stopwatch = Stopwatch.StartNew();
            
            BookletGenerationResponse bookletResponse;
            try
            {
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
                // TODO: Uncomment when IAIRESAlertingService is implemented
                // await _alerting.RaiseAlertAsync(
                //     AlertSeverity.Critical,
                //     ServiceName,
                //     $"Gemma2 booklet generation failed: {ex.Message}",
                //     new Dictionary<string, object>
                //     {
                //         ["stage"] = "Gemma2",
                //         ["errorCode"] = ex.ErrorCode ?? "GEMMA2_GENERATION_ERROR",
                //         ["errorType"] = ex.GetType().Name
                //     });
                LogMethodExit();
                return AIRESResult<BookletGenerationResponse>.Failure(
                    ex.ErrorCode ?? "GEMMA2_GENERATION_ERROR",
                    $"Booklet generation failed: {ex.Message}",
                    ex
                );
            }
            
            stepTimings["Gemma2Generation"] = gemma2Stopwatch.ElapsedMilliseconds;
            
            // Save the booklet to disk
            await progressChannel.Writer.WriteAsync(new ProgressUpdate("Saving Booklet", 95), cancellationToken);
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

            // Complete progress
            await progressChannel.Writer.WriteAsync(new ProgressUpdate("Complete", 100), cancellationToken);
            progressChannel.Writer.Complete();
            
            // Update total processing time
            stopwatch.Stop();
            var totalResponse = new BookletGenerationResponse(
                bookletResponse.Booklet,
                saveResult.Value!,
                stopwatch.ElapsedMilliseconds,
                stepTimings.ToImmutableDictionary()
            );

            // Log any non-critical errors that were aggregated
            if (_errorAggregator.Any())
            {
                LogWarning($"Pipeline completed with {_errorAggregator.Count} non-critical errors");
                foreach (var error in _errorAggregator)
                {
                    LogWarning($"Non-critical error: {error.Message}");
                }
            }

            LogInfo($"CONCURRENT AI Research Pipeline completed successfully in {stopwatch.ElapsedMilliseconds}ms");
            LogInfo($"Booklet saved to: {saveResult.Value}");
            
            LogMethodExit();
            return AIRESResult<BookletGenerationResponse>.Success(totalResponse);
        }
        catch (Exception ex)
        {
            LogError("Unexpected error in CONCURRENT AI Research Pipeline", ex);
            // TODO: Uncomment when IAIRESAlertingService is implemented
            // await _alerting.RaiseAlertAsync(
            //     AlertSeverity.Critical,
            //     ServiceName,
            //     $"Concurrent orchestration failed unexpectedly: {ex.Message}",
            //     new Dictionary<string, object>
            //     {
            //         ["stage"] = "Orchestration",
            //         ["errorType"] = ex.GetType().Name
            //     });
            LogMethodExit();
            return AIRESResult<BookletGenerationResponse>.Failure(
                "CONCURRENT_ORCHESTRATOR_ERROR",
                $"An unexpected error occurred: {ex.Message}",
                ex
            );
        }
        finally
        {
            _ollamaSemaphore?.Dispose();
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
            await _ollamaSemaphore.WaitAsync(cancellationToken);
            try
            {
                LogInfo("Starting Mistral documentation analysis (concurrent)");
                var result = await _mediator.Send(
                    new AnalyzeDocumentationCommand(parseResult.Errors, codeContext),
                    cancellationToken);
                
                UpdateMetric("MistralAnalysis.Duration", sw.ElapsedMilliseconds);
                LogInfo($"Mistral analysis completed in {sw.ElapsedMilliseconds}ms with {result.Findings.Count} findings");
                LogMethodExit();
                return result;
            }
            finally
            {
                _ollamaSemaphore.Release();
            }
        }
        catch (MistralAnalysisFailedException ex)
        {
            LogError("Mistral documentation analysis failed", ex);
            // TODO: Uncomment when IAIRESAlertingService is implemented
            // await _alerting.RaiseAlertAsync(
            //     AlertSeverity.Warning,
            //     ServiceName,
            //     $"Mistral analysis failed: {ex.Message}",
            //     new Dictionary<string, object>
            //     {
            //         ["stage"] = "Mistral",
            //         ["errorType"] = ex.GetType().Name
            //     });
            LogMethodExit();
            throw;
        }
    }

    private async Task<ContextAnalysisResponse> RunDeepSeekAnalysisAsync(
        ParseCompilerErrorsResponse parseResult,
        DocumentationAnalysisResponse docAnalysis,
        string codeContext,
        string projectStructureXml,
        CancellationToken cancellationToken)
    {
        LogMethodEntry();
        var sw = Stopwatch.StartNew();
        
        try
        {
            await _ollamaSemaphore.WaitAsync(cancellationToken);
            try
            {
                LogInfo("Starting DeepSeek context analysis (concurrent)");
                var result = await _mediator.Send(
                    new AnalyzeContextCommand(
                        parseResult.Errors,
                        docAnalysis,
                        codeContext,
                        projectStructureXml),
                    cancellationToken);
                
                UpdateMetric("DeepSeekAnalysis.Duration", sw.ElapsedMilliseconds);
                LogInfo($"DeepSeek analysis completed in {sw.ElapsedMilliseconds}ms with {result.IdentifiedPainPoints.Count} pain points");
                LogMethodExit();
                return result;
            }
            finally
            {
                _ollamaSemaphore.Release();
            }
        }
        catch (DeepSeekContextAnalysisException ex)
        {
            LogError("DeepSeek context analysis failed", ex);
            // TODO: Uncomment when IAIRESAlertingService is implemented
            // await _alerting.RaiseAlertAsync(
            //     AlertSeverity.Warning,
            //     ServiceName,
            //     $"DeepSeek analysis failed: {ex.Message}",
            //     new Dictionary<string, object>
            //     {
            //         ["stage"] = "DeepSeek",
            //         ["errorType"] = ex.GetType().Name
            //     });
            LogMethodExit();
            throw;
        }
    }

    private async Task<PatternValidationResponse> RunCodeGemmaValidationAsync(
        ParseCompilerErrorsResponse parseResult,
        ContextAnalysisResponse contextAnalysis,
        string projectCodebase,
        IImmutableList<string> projectStandards,
        CancellationToken cancellationToken)
    {
        LogMethodEntry();
        var sw = Stopwatch.StartNew();
        
        try
        {
            await _ollamaSemaphore.WaitAsync(cancellationToken);
            try
            {
                LogInfo("Starting CodeGemma pattern validation (concurrent)");
                var result = await _mediator.Send(
                    new ValidatePatternsCommand(
                        parseResult.Errors,
                        contextAnalysis,
                        projectCodebase,
                        projectStandards),
                    cancellationToken);
                
                UpdateMetric("CodeGemmaValidation.Duration", sw.ElapsedMilliseconds);
                LogInfo($"CodeGemma validation completed in {sw.ElapsedMilliseconds}ms. Compliance: {result.OverallCompliance}");
                LogMethodExit();
                return result;
            }
            finally
            {
                _ollamaSemaphore.Release();
            }
        }
        catch (CodeGemmaValidationException ex)
        {
            LogError("CodeGemma pattern validation failed", ex);
            // TODO: Uncomment when IAIRESAlertingService is implemented
            // await _alerting.RaiseAlertAsync(
            //     AlertSeverity.Warning,
            //     ServiceName,
            //     $"CodeGemma validation failed: {ex.Message}",
            //     new Dictionary<string, object>
            //     {
            //         ["stage"] = "CodeGemma",
            //         ["errorType"] = ex.GetType().Name
            //     });
            LogMethodExit();
            throw;
        }
    }

    private async Task<TResult> ProcessWithRetryAsync<TResult>(
        Func<Task<TResult>> action,
        string taskName,
        CancellationToken cancellationToken,
        int maxRetries = 3)
    {
        LogMethodEntry();
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                LogInfo($"Attempt {attempt + 1} of {maxRetries + 1} for {taskName}");
                var result = await action();
                LogMethodExit();
                return result;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                LogWarning($"Attempt {attempt + 1} for {taskName} failed: {ex.Message}");
                _errorAggregator.Add(ex);
                
                // Exponential backoff: 2^attempt seconds
                var delaySeconds = Math.Pow(2, attempt);
                LogInfo($"Retrying {taskName} in {delaySeconds} seconds...");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
            catch (Exception ex)
            {
                LogError($"All retry attempts failed for {taskName}", ex);
                _errorAggregator.Add(ex);
                LogMethodExit();
                throw;
            }
        }
        
        LogMethodExit();
        throw new InvalidOperationException($"Should not reach here - {taskName}");
    }

    private async Task ReportProgressAsync(ChannelReader<ProgressUpdate> reader, CancellationToken cancellationToken)
    {
        LogMethodEntry();
        
        try
        {
            await foreach (var update in reader.ReadAllAsync(cancellationToken))
            {
                LogInfo($"Progress: {update.Stage} - {update.Percentage}%");
                // TODO: Report to UI via SignalR or IProgress<T>
            }
        }
        catch (Exception ex)
        {
            LogError("Error in progress reporting", ex);
        }
        
        LogMethodExit();
    }

    private long GetMetricValue(string metricName)
    {
        var metrics = GetMetrics();
        return (long)(metrics.GetValueOrDefault(metricName) ?? 0);
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
                ["ParseCompilerErrors"] = true,
                ["MistralDocumentation"] = await CheckServiceHealthAsync("Mistral"),
                ["DeepSeekContext"] = await CheckServiceHealthAsync("DeepSeek"),
                ["CodeGemmaPatterns"] = await CheckServiceHealthAsync("CodeGemma"),
                ["Gemma2Booklet"] = await CheckServiceHealthAsync("Gemma2"),
                ["ConcurrentMode"] = true,
                ["SemaphoreThrottling"] = true
            };

            LogInfo($"Pipeline status (CONCURRENT): {string.Join(", ", status.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            LogMethodExit();
            return AIRESResult<Dictionary<string, bool>>.Success(status);
        }
        catch (Exception ex)
        {
            LogError("Failed to get pipeline status", ex);
            // TODO: Uncomment when IAIRESAlertingService is implemented
            // await _alerting.RaiseAlertAsync(
            //     AlertSeverity.Information,
            //     ServiceName,
            //     $"Pipeline status check failed: {ex.Message}",
            //     new Dictionary<string, object>
            //     {
            //         ["operation"] = "GetPipelineStatus",
            //         ["errorType"] = ex.GetType().Name
            //     });
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
        await Task.Delay(10);
        return true;
    }

    private record ProgressUpdate(string Stage, int Percentage);
}