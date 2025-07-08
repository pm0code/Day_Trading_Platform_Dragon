using System.Collections.Concurrent;
using System.Diagnostics;
using MarketAnalyzer.Foundation;
using MarketAnalyzer.Infrastructure.AI.Configuration;
using MarketAnalyzer.Infrastructure.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketAnalyzer.Infrastructure.AI.Services;

/// <summary>
/// Orchestrates LLM requests between local (Ollama) and cloud (Gemini) providers
/// using intelligent routing based on latency, cost, complexity, and availability.
/// </summary>
public class LLMOrchestrationService : CanonicalServiceBase, ILLMProvider
{
    private readonly OllamaProvider _ollamaProvider;
    private readonly GeminiProvider _geminiProvider;
    private readonly LLMOrchestrationOptions _options;
    private readonly ConcurrentDictionary<string, ProviderHealth> _providerHealth;
    private readonly ConcurrentDictionary<LLMPromptType, RoutingStats> _routingStats;

    /// <summary>
    /// Gets the provider name.
    /// </summary>
    public string ProviderName => "LLMOrchestration";

    /// <summary>
    /// Gets whether this provider runs locally (hybrid).
    /// </summary>
    public bool IsLocal => false; // Hybrid service

    public LLMOrchestrationService(
        OllamaProvider ollamaProvider,
        GeminiProvider geminiProvider,
        IOptions<LLMOrchestrationOptions> options,
        ILogger<LLMOrchestrationService> logger)
        : base(logger)
    {
        LogMethodEntry();
        try
        {
            _ollamaProvider = ollamaProvider ?? throw new ArgumentNullException(nameof(ollamaProvider));
            _geminiProvider = geminiProvider ?? throw new ArgumentNullException(nameof(geminiProvider));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _providerHealth = new ConcurrentDictionary<string, ProviderHealth>();
            _routingStats = new ConcurrentDictionary<LLMPromptType, RoutingStats>();

            LogInfo("LLMOrchestrationService initialized with hybrid routing enabled");
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize LLMOrchestrationService", ex);
            throw;
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<bool>> OnInitializeAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        try
        {
            // Initialize both providers
            var ollamaInit = await _ollamaProvider.InitializeAsync(cancellationToken).ConfigureAwait(false);
            var geminiInit = await _geminiProvider.InitializeAsync(cancellationToken).ConfigureAwait(false);

            if (!ollamaInit.IsSuccess && !geminiInit.IsSuccess)
            {
                return TradingResult<bool>.Failure("ALL_PROVIDERS_FAILED", 
                    "Both Ollama and Gemini providers failed to initialize");
            }

            // Update provider health
            _providerHealth["Ollama"] = new ProviderHealth 
            { 
                IsAvailable = ollamaInit.IsSuccess, 
                LastCheck = DateTime.UtcNow,
                ConsecutiveFailures = ollamaInit.IsSuccess ? 0 : 1
            };
            
            _providerHealth["Gemini"] = new ProviderHealth 
            { 
                IsAvailable = geminiInit.IsSuccess, 
                LastCheck = DateTime.UtcNow,
                ConsecutiveFailures = geminiInit.IsSuccess ? 0 : 1
            };

            LogInfo($"Orchestration initialized - Ollama: {ollamaInit.IsSuccess}, Gemini: {geminiInit.IsSuccess}");
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Exception during orchestration initialization", ex);
            return TradingResult<bool>.Failure("INIT_EXCEPTION", $"Initialization failed: {ex.Message}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<bool>> OnStartAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        try
        {
            // Start both providers
            await Task.WhenAll(
                _ollamaProvider.StartAsync(cancellationToken),
                _geminiProvider.StartAsync(cancellationToken)
            ).ConfigureAwait(false);

            LogInfo("LLMOrchestrationService started successfully");
            return TradingResult<bool>.Success(true);
        }
        finally
        {
            LogMethodExit();
        }
    }

    protected override async Task<TradingResult<bool>> OnStopAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();
        try
        {
            // Stop both providers
            await Task.WhenAll(
                _ollamaProvider.StopAsync(cancellationToken),
                _geminiProvider.StopAsync(cancellationToken)
            ).ConfigureAwait(false);

            _providerHealth.Clear();
            _routingStats.Clear();

            LogInfo("LLMOrchestrationService stopped successfully");
            return TradingResult<bool>.Success(true);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<LLMHealthStatus>> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            var health = new LLMHealthStatus
            {
                LastChecked = DateTime.UtcNow
            };

            // Check both providers in parallel
            var ollamaHealthTask = _ollamaProvider.GetHealthAsync(cancellationToken);
            var geminiHealthTask = _geminiProvider.GetHealthAsync(cancellationToken);

            await Task.WhenAll(ollamaHealthTask, geminiHealthTask).ConfigureAwait(false);

            var ollamaHealth = ollamaHealthTask.Result;
            var geminiHealth = geminiHealthTask.Result;

            // Aggregate health status
            health.IsHealthy = (ollamaHealth.IsSuccess && ollamaHealth.Value!.IsHealthy) || 
                              (geminiHealth.IsSuccess && geminiHealth.Value!.IsHealthy);

            if (ollamaHealth.IsSuccess && geminiHealth.IsSuccess)
            {
                health.AverageLatencyMs = (ollamaHealth.Value!.AverageLatencyMs + geminiHealth.Value!.AverageLatencyMs) / 2;
                health.AvailableModels = ollamaHealth.Value.AvailableModels + geminiHealth.Value.AvailableModels;
                health.LoadedModels = ollamaHealth.Value.LoadedModels + geminiHealth.Value.LoadedModels;
            }

            health.Status = health.IsHealthy ? "Orchestration service is healthy" : "All providers are unhealthy";

            LogInfo($"Orchestration health check: {health.Status}");
            return TradingResult<LLMHealthStatus>.Success(health);
        }
        catch (Exception ex)
        {
            LogError("Health check failed", ex);
            return TradingResult<LLMHealthStatus>.Failure("HEALTH_CHECK_ERROR", ex.Message, ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<LLMResponse>> GenerateCompletionAsync(
        LLMRequest request,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            // Determine optimal provider based on routing strategy
            var selectedProvider = await SelectProviderAsync(request).ConfigureAwait(false);
            
            LogInfo($"Routing request to {selectedProvider.ProviderName} based on {selectedProvider.Reason}");

            var sw = Stopwatch.StartNew();
            TradingResult<LLMResponse> result;

            try
            {
                if (selectedProvider.ProviderName == "Ollama")
                {
                    result = await _ollamaProvider.GenerateCompletionAsync(request, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    result = await _geminiProvider.GenerateCompletionAsync(request, cancellationToken).ConfigureAwait(false);
                }

                sw.Stop();

                // Handle fallback if enabled and primary failed
                if (!result.IsSuccess && request.AllowFallback && _options.EnableFallback)
                {
                    LogWarning($"{selectedProvider.ProviderName} failed, attempting fallback");
                    
                    var fallbackProvider = selectedProvider.ProviderName == "Ollama" ? "Gemini" : "Ollama";
                    LogInfo($"Falling back to {fallbackProvider}");

                    if (fallbackProvider == "Ollama")
                    {
                        result = await _ollamaProvider.GenerateCompletionAsync(request, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        result = await _geminiProvider.GenerateCompletionAsync(request, cancellationToken).ConfigureAwait(false);
                    }

                    if (result.IsSuccess)
                    {
                        IncrementCounter("FallbackSuccess");
                        LogInfo($"Fallback to {fallbackProvider} succeeded");
                    }
                }

                // Update provider health based on result
                await UpdateProviderHealthAsync(selectedProvider.ProviderName, result.IsSuccess, sw.ElapsedMilliseconds).ConfigureAwait(false);

                // Update routing statistics
                UpdateRoutingStats(request.PromptType, selectedProvider.ProviderName, result.IsSuccess);

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                await UpdateProviderHealthAsync(selectedProvider.ProviderName, false, sw.ElapsedMilliseconds).ConfigureAwait(false);
                LogError($"Provider {selectedProvider.ProviderName} threw exception", ex);
                throw;
            }
        }
        catch (Exception ex)
        {
            LogError("Orchestration failed", ex);
            return TradingResult<LLMResponse>.Failure("ORCHESTRATION_ERROR", ex.Message, ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<LLMResponse>> GenerateStreamingCompletionAsync(
        LLMRequest request,
        Action<string> onToken,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            // For streaming, prefer local provider for lower latency
            var selectedProvider = await SelectProviderForStreamingAsync(request).ConfigureAwait(false);
            
            LogInfo($"Routing streaming request to {selectedProvider.ProviderName}");

            if (selectedProvider.ProviderName == "Ollama")
            {
                return await _ollamaProvider.GenerateStreamingCompletionAsync(request, onToken, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return await _geminiProvider.GenerateStreamingCompletionAsync(request, onToken, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogError("Streaming orchestration failed", ex);
            return TradingResult<LLMResponse>.Failure("STREAMING_ERROR", ex.Message, ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<LLMEmbedding>> GenerateEmbeddingAsync(
        string text,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            // For embeddings, prefer local provider for data privacy
            if (_providerHealth.TryGetValue("Ollama", out var ollamaHealth) && ollamaHealth.IsAvailable)
            {
                LogInfo("Using Ollama for embeddings (local privacy)");
                return await _ollamaProvider.GenerateEmbeddingAsync(text, model, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                LogInfo("Falling back to Gemini for embeddings (Ollama unavailable)");
                return await _geminiProvider.GenerateEmbeddingAsync(text, model, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogError("Embedding orchestration failed", ex);
            return TradingResult<LLMEmbedding>.Failure("EMBEDDING_ERROR", ex.Message, ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<List<LLMModelInfo>>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            // Get models from both providers
            var ollamaModelsTask = _ollamaProvider.ListModelsAsync(cancellationToken);
            var geminiModelsTask = _geminiProvider.ListModelsAsync(cancellationToken);

            await Task.WhenAll(ollamaModelsTask, geminiModelsTask).ConfigureAwait(false);

            var allModels = new List<LLMModelInfo>();

            if (ollamaModelsTask.Result.IsSuccess)
            {
                allModels.AddRange(ollamaModelsTask.Result.Value!);
            }

            if (geminiModelsTask.Result.IsSuccess)
            {
                allModels.AddRange(geminiModelsTask.Result.Value!);
            }

            LogInfo($"Listed {allModels.Count} models from both providers");
            return TradingResult<List<LLMModelInfo>>.Success(allModels);
        }
        catch (Exception ex)
        {
            LogError("Failed to list models", ex);
            return TradingResult<List<LLMModelInfo>>.Failure("LIST_MODELS_ERROR", ex.Message, ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<bool>> PreloadModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            // Try to preload on both providers
            var ollamaTask = _ollamaProvider.PreloadModelAsync(modelName, cancellationToken);
            var geminiTask = _geminiProvider.PreloadModelAsync(modelName, cancellationToken);

            var results = await Task.WhenAll(ollamaTask, geminiTask).ConfigureAwait(false);

            var success = results.Any(r => r.IsSuccess);
            LogInfo($"Model {modelName} preload - Ollama: {ollamaTask.Result.IsSuccess}, Gemini: {geminiTask.Result.IsSuccess}");

            return TradingResult<bool>.Success(success);
        }
        catch (Exception ex)
        {
            LogError($"Failed to preload model {modelName}", ex);
            return TradingResult<bool>.Failure("PRELOAD_ERROR", ex.Message, ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public decimal EstimateRequestCost(LLMRequest request)
    {
        LogMethodEntry();
        try
        {
            // Estimate cost for both providers and return the minimum
            var ollamaCost = _ollamaProvider.EstimateRequestCost(request);
            var geminiCost = _geminiProvider.EstimateRequestCost(request);

            return Math.Min(ollamaCost, geminiCost);
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<LLMUsageStatistics>> GetUsageStatisticsAsync()
    {
        LogMethodEntry();
        try
        {
            // Aggregate statistics from both providers
            var ollamaStatsTask = _ollamaProvider.GetUsageStatisticsAsync();
            var geminiStatsTask = _geminiProvider.GetUsageStatisticsAsync();

            await Task.WhenAll(ollamaStatsTask, geminiStatsTask).ConfigureAwait(false);

            var aggregatedStats = new LLMUsageStatistics
            {
                PeriodStart = StartTime,
                PeriodEnd = DateTime.UtcNow
            };

            if (ollamaStatsTask.Result.IsSuccess)
            {
                var ollamaStats = ollamaStatsTask.Result.Value!;
                aggregatedStats.TotalRequests += ollamaStats.TotalRequests;
                aggregatedStats.SuccessfulRequests += ollamaStats.SuccessfulRequests;
                aggregatedStats.FailedRequests += ollamaStats.FailedRequests;
                aggregatedStats.TotalTokens += ollamaStats.TotalTokens;
                aggregatedStats.TotalCost += ollamaStats.TotalCost;
            }

            if (geminiStatsTask.Result.IsSuccess)
            {
                var geminiStats = geminiStatsTask.Result.Value!;
                aggregatedStats.TotalRequests += geminiStats.TotalRequests;
                aggregatedStats.SuccessfulRequests += geminiStats.SuccessfulRequests;
                aggregatedStats.FailedRequests += geminiStats.FailedRequests;
                aggregatedStats.TotalTokens += geminiStats.TotalTokens;
                aggregatedStats.TotalCost += geminiStats.TotalCost;
            }

            // Add routing statistics
            foreach (var kvp in _routingStats)
            {
                aggregatedStats.PromptTypeUsage[kvp.Key] = kvp.Value.TotalRequests;
            }

            return TradingResult<LLMUsageStatistics>.Success(aggregatedStats);
        }
        catch (Exception ex)
        {
            LogError("Failed to get usage statistics", ex);
            return TradingResult<LLMUsageStatistics>.Failure("STATS_ERROR", ex.Message, ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    #region Private Helper Methods

    private Task<ProviderSelection> SelectProviderAsync(LLMRequest request)
    {
        LogMethodEntry();
        try
        {
            // Get current provider health
            var ollamaAvailable = _providerHealth.TryGetValue("Ollama", out var ollamaHealth) && ollamaHealth.IsAvailable;
            var geminiAvailable = _providerHealth.TryGetValue("Gemini", out var geminiHealth) && geminiHealth.IsAvailable;

            // If only one provider is available, use it
            if (ollamaAvailable && !geminiAvailable)
            {
                return Task.FromResult(new ProviderSelection("Ollama", "Only available provider"));
            }
            
            if (!ollamaAvailable && geminiAvailable)
            {
                return Task.FromResult(new ProviderSelection("Gemini", "Only available provider"));
            }

            if (!ollamaAvailable && !geminiAvailable)
            {
                LogWarning("No providers available, defaulting to Ollama");
                return Task.FromResult(new ProviderSelection("Ollama", "Default fallback"));
            }

            // Both providers available - use routing strategy
            var result = request.PromptType switch
            {
                // Real-time use cases - prefer local for speed
                LLMPromptType.TradingSignal => new ProviderSelection("Ollama", "Real-time signal requires low latency"),
                LLMPromptType.QuickSummary => new ProviderSelection("Ollama", "Quick response needed"),
                LLMPromptType.TechnicalIndicator => new ProviderSelection("Ollama", "Technical analysis is local-friendly"),

                // Complex analysis - prefer cloud for quality
                LLMPromptType.MarketAnalysis => SelectBasedOnComplexity(request),
                LLMPromptType.RiskAssessment => SelectBasedOnComplexity(request),
                LLMPromptType.ReportGeneration => new ProviderSelection("Gemini", "Complex report generation"),

                // Data processing - prefer local for privacy
                LLMPromptType.DataExtraction => new ProviderSelection("Ollama", "Data privacy on local"),
                LLMPromptType.NewsSentiment => new ProviderSelection("Ollama", "Sentiment analysis is local-friendly"),

                // Code generation - prefer cloud for quality
                LLMPromptType.CodeGeneration => new ProviderSelection("Gemini", "Code generation requires advanced reasoning"),

                // Default to local for general use
                _ => new ProviderSelection("Ollama", "Default local preference")
            };
            
            return Task.FromResult(result);
        }
        finally
        {
            LogMethodExit();
        }
    }

    private ProviderSelection SelectBasedOnComplexity(LLMRequest request)
    {
        LogMethodEntry();
        try
        {
            // Analyze request complexity
            var promptLength = request.Prompt.Length + (request.SystemPrompt?.Length ?? 0);
            var isComplex = promptLength > _options.ComplexityThreshold || 
                           request.MaxTokens > _options.ComplexTokenThreshold;

            if (isComplex)
            {
                return new ProviderSelection("Gemini", "Complex analysis requires cloud model");
            }
            else
            {
                return new ProviderSelection("Ollama", "Simple analysis can use local model");
            }
        }
        finally
        {
            LogMethodExit();
        }
    }

    private Task<ProviderSelection> SelectProviderForStreamingAsync(LLMRequest request)
    {
        LogMethodEntry();
        try
        {
            // For streaming, strongly prefer local for lower latency
            var ollamaAvailable = _providerHealth.TryGetValue("Ollama", out var ollamaHealth) && ollamaHealth.IsAvailable;
            
            ProviderSelection result;
            if (ollamaAvailable)
            {
                result = new ProviderSelection("Ollama", "Local streaming for low latency");
            }
            else
            {
                result = new ProviderSelection("Gemini", "Ollama unavailable for streaming");
            }
            
            return Task.FromResult(result);
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async Task UpdateProviderHealthAsync(string providerName, bool success, long latencyMs)
    {
        LogMethodEntry();
        try
        {
            _providerHealth.AddOrUpdate(providerName, 
                new ProviderHealth 
                { 
                    IsAvailable = success, 
                    LastCheck = DateTime.UtcNow,
                    AverageLatencyMs = latencyMs,
                    ConsecutiveFailures = success ? 0 : 1
                },
                (key, existing) => new ProviderHealth
                {
                    IsAvailable = success,
                    LastCheck = DateTime.UtcNow,
                    AverageLatencyMs = (existing.AverageLatencyMs + latencyMs) / 2, // Simple moving average
                    ConsecutiveFailures = success ? 0 : existing.ConsecutiveFailures + 1
                });

            await Task.CompletedTask.ConfigureAwait(false);
        }
        finally
        {
            LogMethodExit();
        }
    }

    private void UpdateRoutingStats(LLMPromptType promptType, string provider, bool success)
    {
        LogMethodEntry();
        try
        {
            _routingStats.AddOrUpdate(promptType,
                new RoutingStats { TotalRequests = 1, SuccessfulRequests = success ? 1 : 0 },
                (key, existing) => new RoutingStats
                {
                    TotalRequests = existing.TotalRequests + 1,
                    SuccessfulRequests = existing.SuccessfulRequests + (success ? 1 : 0)
                });
        }
        finally
        {
            LogMethodExit();
        }
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ollamaProvider?.Dispose();
            _geminiProvider?.Dispose();
        }
        
        base.Dispose(disposing);
    }

    #region Supporting Types

    private class ProviderSelection
    {
        public string ProviderName { get; }
        public string Reason { get; }

        public ProviderSelection(string providerName, string reason)
        {
            ProviderName = providerName;
            Reason = reason;
        }
    }

    private class ProviderHealth
    {
        public bool IsAvailable { get; set; }
        public DateTime LastCheck { get; set; }
        public double AverageLatencyMs { get; set; }
        public int ConsecutiveFailures { get; set; }
    }

    private class RoutingStats
    {
        public long TotalRequests { get; set; }
        public long SuccessfulRequests { get; set; }
    }

    #endregion
}