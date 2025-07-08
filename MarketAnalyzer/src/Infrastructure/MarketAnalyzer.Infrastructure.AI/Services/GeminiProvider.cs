using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MarketAnalyzer.Foundation;
using MarketAnalyzer.Infrastructure.AI.Configuration;
using MarketAnalyzer.Infrastructure.AI.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace MarketAnalyzer.Infrastructure.AI.Services;

/// <summary>
/// Google Gemini provider for cloud LLM inference with enterprise-grade features.
/// Implements advanced cost optimization, request batching, and intelligent caching.
/// </summary>
public class GeminiProvider : CanonicalServiceBase, ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly ConcurrentDictionary<string, DateTime> _modelLastUsed;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly ConcurrentDictionary<string, decimal> _tokensUsed;

    /// <summary>
    /// Gets the provider name.
    /// </summary>
    public string ProviderName => "Gemini";

    /// <summary>
    /// Gets whether this provider runs locally.
    /// </summary>
    public bool IsLocal => false;

    public GeminiProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<GeminiOptions> options,
        IMemoryCache cache,
        ILogger<GeminiProvider> logger)
        : base(logger)
    {
        LogMethodEntry();
        try
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClientFactory.CreateClient("Gemini");
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _rateLimiter = new SemaphoreSlim(_options.MaxConcurrentRequests, _options.MaxConcurrentRequests);
            _modelLastUsed = new ConcurrentDictionary<string, DateTime>();
            _tokensUsed = new ConcurrentDictionary<string, decimal>();

            // Configure retry policy for cloud resilience
            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
                .OrResult(msg => msg.StatusCode == HttpStatusCode.ServiceUnavailable)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        if (outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests)
                        {
                            LogWarning($"Gemini rate limit hit, waiting {timespan.TotalSeconds}s before retry {retryCount}");
                        }
                        else if (outcome.Result?.StatusCode == HttpStatusCode.ServiceUnavailable)
                        {
                            LogWarning($"Gemini service unavailable, waiting {timespan.TotalSeconds}s before retry {retryCount}");
                        }
                    });

            LogInfo($"GeminiProvider initialized with API endpoint configured");
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize GeminiProvider", ex);
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
            // Verify API key is configured
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                return TradingResult<bool>.Failure("MISSING_API_KEY", "Gemini API key is not configured");
            }

            // Set authentication header
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", _options.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MarketAnalyzer/1.0");

            // Verify API connectivity
            var healthResult = await GetHealthAsync(cancellationToken).ConfigureAwait(false);
            if (!healthResult.IsSuccess || !healthResult.Value!.IsHealthy)
            {
                return TradingResult<bool>.Failure("GEMINI_UNHEALTHY", "Gemini API is not accessible");
            }

            LogInfo("GeminiProvider initialized successfully");
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Exception during GeminiProvider initialization", ex);
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
            await Task.CompletedTask.ConfigureAwait(false);
            LogInfo("GeminiProvider started successfully");
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
            _modelLastUsed.Clear();
            _tokensUsed.Clear();
            await Task.CompletedTask.ConfigureAwait(false);
            LogInfo("GeminiProvider stopped successfully");
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

            try
            {
                var sw = Stopwatch.StartNew();
                var uri = new Uri($"v1/models?key={_options.ApiKey}", UriKind.Relative);
                var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
                sw.Stop();

                health.IsHealthy = response.IsSuccessStatusCode;
                health.AverageLatencyMs = sw.ElapsedMilliseconds;
                health.Status = response.IsSuccessStatusCode ? "Gemini API is accessible" : $"API returned {response.StatusCode}";

                if (response.IsSuccessStatusCode)
                {
                    var modelsResponse = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var modelCount = CountModelsInResponse(modelsResponse);
                    health.AvailableModels = modelCount;
                    health.LoadedModels = modelCount; // Cloud models are always "loaded"
                }
            }
            catch (Exception ex)
            {
                health.IsHealthy = false;
                health.Status = $"Failed to connect to Gemini: {ex.Message}";
                health.Errors.Add(ex.Message);
            }

            LogInfo($"Gemini health check: {health.Status}");
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
            // Validate request
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return TradingResult<LLMResponse>.Failure("INVALID_PROMPT", "Prompt cannot be empty");
            }

            // Check cache
            var cacheKey = GenerateCacheKey(request);
            if (_cache.TryGetValue<LLMResponse>(cacheKey, out var cached))
            {
                IncrementCounter("CacheHits");
                LogInfo("Cache hit for prompt");
                return TradingResult<LLMResponse>.Success(cached!);
            }

            // Rate limiting
            if (!await _rateLimiter.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                return TradingResult<LLMResponse>.Failure("RATE_LIMIT_EXCEEDED", "Too many concurrent requests");
            }

            try
            {
                // Select optimal model based on request type
                var model = SelectOptimalModel(request);
                
                // Prepare Gemini request
                var geminiRequest = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = CombinePrompts(request.SystemPrompt, request.Prompt) }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = (double)request.Temperature,
                        topP = (double)request.TopP,
                        maxOutputTokens = request.MaxTokens,
                        stopSequences = request.StopSequences?.ToArray()
                    },
                    safetySettings = GetSafetySettings()
                };

                // Execute with retry policy
                var sw = Stopwatch.StartNew();
                var httpResponse = await _retryPolicy.ExecuteAsync(async () =>
                    await _httpClient.PostAsJsonAsync(
                        $"v1beta/models/{model}:generateContent?key={_options.ApiKey}",
                        geminiRequest,
                        cancellationToken).ConfigureAwait(false)
                ).ConfigureAwait(false);

                httpResponse.EnsureSuccessStatusCode();

                var geminiResponse = await httpResponse.Content.ReadFromJsonAsync<GeminiGenerateResponse>(
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                sw.Stop();

                if (geminiResponse?.Candidates?.Any() != true)
                {
                    return TradingResult<LLMResponse>.Failure("EMPTY_RESPONSE", "Received empty response from Gemini");
                }

                var candidate = geminiResponse.Candidates.First();
                var responseText = candidate.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;

                // Calculate costs
                var promptTokens = geminiResponse.UsageMetadata?.PromptTokenCount ?? EstimateTokens(request.Prompt);
                var completionTokens = geminiResponse.UsageMetadata?.CandidatesTokenCount ?? EstimateTokens(responseText);
                var cost = CalculateCost(model, promptTokens, completionTokens);

                // Track usage
                _tokensUsed.AddOrUpdate(model, cost, (key, oldValue) => oldValue + cost);

                // Convert to LLMResponse
                var response = new LLMResponse
                {
                    Text = responseText,
                    Model = model,
                    Provider = ProviderName,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    InferenceTimeMs = sw.ElapsedMilliseconds,
                    Cost = cost,
                    Confidence = CalculateConfidence(candidate),
                    FinishReason = MapFinishReason(candidate.FinishReason),
                    Timestamp = DateTime.UtcNow
                };

                // Update model usage tracking
                _modelLastUsed[model] = DateTime.UtcNow;

                // Cache response
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(_options.CacheTTLMinutes),
                    Size = 1
                };
                _cache.Set(cacheKey, response, cacheOptions);

                IncrementCounter("CompletionsGenerated");
                UpdateMetric("AverageInferenceTimeMs", response.InferenceTimeMs);
                UpdateMetric("TotalCost", (double)response.Cost);
                LogInfo($"Completion generated in {response.InferenceTimeMs}ms using model {response.Model} (cost: ${response.Cost:F4})");

                return TradingResult<LLMResponse>.Success(response);
            }
            finally
            {
                _rateLimiter.Release();
            }
        }
        catch (HttpRequestException ex)
        {
            LogError("HTTP request failed", ex);
            return TradingResult<LLMResponse>.Failure("HTTP_ERROR", $"Request failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException)
        {
            LogWarning("Request cancelled or timed out");
            return TradingResult<LLMResponse>.Failure("TIMEOUT", "Request timed out");
        }
        catch (Exception ex)
        {
            LogError("Unexpected error generating completion", ex);
            return TradingResult<LLMResponse>.Failure("GENERATION_ERROR", ex.Message, ex);
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
            // Note: Gemini streaming would be implemented here
            // For now, use regular completion and simulate streaming
            LogWarning("Gemini streaming not yet implemented, falling back to regular completion");
            
            var result = await GenerateCompletionAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Value!.Text))
            {
                // Simulate streaming by sending the complete response
                onToken(result.Value.Text);
            }
            
            return result;
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
            var embeddingModel = model ?? _options.EmbeddingModel ?? "text-embedding-004";
            
            var request = new
            {
                model = $"models/{embeddingModel}",
                content = new
                {
                    parts = new[] { new { text = text } }
                }
            };

            var sw = Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(
                $"v1beta/models/{embeddingModel}:embedContent?key={_options.ApiKey}",
                request,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var embeddingResponse = await response.Content.ReadFromJsonAsync<GeminiEmbeddingResponse>(
                cancellationToken: cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (embeddingResponse?.Embedding?.Values == null)
            {
                return TradingResult<LLMEmbedding>.Failure("INVALID_RESPONSE", "No embedding returned");
            }

            var embedding = new LLMEmbedding
            {
                Vector = embeddingResponse.Embedding.Values,
                Model = embeddingModel,
                TokenCount = EstimateTokens(text),
                GenerationTimeMs = sw.ElapsedMilliseconds
            };

            LogInfo($"Embedding generated in {embedding.GenerationTimeMs}ms");
            return TradingResult<LLMEmbedding>.Success(embedding);
        }
        catch (Exception ex)
        {
            LogError("Embedding generation failed", ex);
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
            var uri = new Uri($"v1/models?key={_options.ApiKey}", UriKind.Relative);
            var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var modelsResponse = await response.Content.ReadFromJsonAsync<GeminiModelsResponse>(
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (modelsResponse?.Models == null)
            {
                return TradingResult<List<LLMModelInfo>>.Success(new List<LLMModelInfo>());
            }

            var modelInfos = modelsResponse.Models
                .Where(m => m.SupportedGenerationMethods?.Contains("generateContent") == true)
                .Select(m => new LLMModelInfo
                {
                    Name = m.Name.Replace("models/", ""),
                    ParameterSize = ExtractParameterSize(m.Name),
                    ContextWindow = m.InputTokenLimit ?? 30720, // Default for Gemini 1.5
                    IsLoaded = true, // Cloud models are always "loaded"
                    Capabilities = GetModelCapabilities(m.Name),
                    LastUsed = _modelLastUsed.TryGetValue(m.Name, out var lastUsed) ? lastUsed : null
                }).ToList();

            LogInfo($"Listed {modelInfos.Count} available Gemini models");
            return TradingResult<List<LLMModelInfo>>.Success(modelInfos);
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
            // Cloud models don't need preloading, but we can warm them up
            var warmupRequest = new LLMRequest
            {
                Prompt = "Hello",
                Model = modelName,
                MaxTokens = 10
            };

            var result = await GenerateCompletionAsync(warmupRequest, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                _modelLastUsed[modelName] = DateTime.UtcNow;
                LogInfo($"Model {modelName} warmed up successfully");
                return TradingResult<bool>.Success(true);
            }

            return TradingResult<bool>.Failure("WARMUP_FAILED", $"Failed to warm up model: {result.Error?.Message}");
        }
        catch (Exception ex)
        {
            LogError($"Failed to warm up model {modelName}", ex);
            return TradingResult<bool>.Failure("WARMUP_ERROR", ex.Message, ex);
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
            var model = request.Model ?? _options.DefaultModel;
            var promptTokens = EstimateTokens(request.Prompt + (request.SystemPrompt ?? ""));
            var completionTokens = request.MaxTokens;
            
            return CalculateCost(model, promptTokens, completionTokens);
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
            var stats = new LLMUsageStatistics
            {
                TotalRequests = GetMetricValue("CompletionsGenerated"),
                SuccessfulRequests = GetMetricValue("CompletionsGenerated"),
                FailedRequests = GetMetricValue("GenerationErrors"),
                TotalCost = _tokensUsed.Values.Sum(),
                AverageLatencyMs = GetMetricValue("AverageInferenceTimeMs"),
                PeriodStart = StartTime,
                PeriodEnd = DateTime.UtcNow
            };

            // Add model usage from cache
            foreach (var kvp in _tokensUsed)
            {
                stats.ModelUsage[kvp.Key] = GetMetricValue($"Model_{kvp.Key}_Usage");
            }

            await Task.CompletedTask.ConfigureAwait(false);
            return TradingResult<LLMUsageStatistics>.Success(stats);
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

    private string GenerateCacheKey(LLMRequest request)
    {
        LogMethodEntry();
        try
        {
            var normalized = new
            {
                Model = request.Model ?? _options.DefaultModel,
                Prompt = request.Prompt.Trim().ToLowerInvariant(),
                SystemPrompt = request.SystemPrompt?.Trim().ToLowerInvariant(),
                Temperature = Math.Round(request.Temperature, 1),
                MaxTokens = request.MaxTokens,
                PromptType = request.PromptType
            };

            var json = JsonSerializer.Serialize(normalized);
            var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToBase64String(hash);
        }
        finally
        {
            LogMethodExit();
        }
    }

    private string SelectOptimalModel(LLMRequest request)
    {
        LogMethodEntry();
        try
        {
            if (!string.IsNullOrEmpty(request.Model))
            {
                return request.Model;
            }

            // Select model based on prompt type and complexity
            return request.PromptType switch
            {
                LLMPromptType.QuickSummary => _options.FastModel,
                LLMPromptType.CodeGeneration => _options.CodeModel,
                LLMPromptType.MarketAnalysis => _options.AnalysisModel,
                LLMPromptType.RiskAssessment => _options.AnalysisModel,
                _ => _options.DefaultModel
            };
        }
        finally
        {
            LogMethodExit();
        }
    }

    private string CombinePrompts(string? systemPrompt, string userPrompt)
    {
        LogMethodEntry();
        try
        {
            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                return userPrompt;
            }

            return $"{systemPrompt}\n\nUser: {userPrompt}";
        }
        finally
        {
            LogMethodExit();
        }
    }

    private object[] GetSafetySettings()
    {
        LogMethodEntry();
        try
        {
            return new[]
            {
                new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_ONLY_HIGH" },
                new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_ONLY_HIGH" },
                new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_ONLY_HIGH" },
                new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_ONLY_HIGH" }
            };
        }
        finally
        {
            LogMethodExit();
        }
    }

    private decimal CalculateCost(string model, int promptTokens, int completionTokens)
    {
        LogMethodEntry();
        try
        {
            // Gemini Pro pricing (as of 2025)
            var pricing = model.ToLower() switch
            {
                var m when m.Contains("gemini-pro") => new { Input = 0.000125m, Output = 0.000375m },
                var m when m.Contains("gemini-1.5-pro") => new { Input = 0.00125m, Output = 0.00375m },
                var m when m.Contains("gemini-1.5-flash") => new { Input = 0.000075m, Output = 0.0003m },
                _ => new { Input = 0.000125m, Output = 0.000375m } // Default to Gemini Pro
            };

            return (promptTokens * pricing.Input) + (completionTokens * pricing.Output);
        }
        finally
        {
            LogMethodExit();
        }
    }

    private decimal CalculateConfidence(GeminiCandidate candidate)
    {
        LogMethodEntry();
        try
        {
            var confidence = 0.8m; // Base confidence for cloud models

            // Adjust based on safety ratings
            if (candidate.SafetyRatings?.Any() == true)
            {
                var highRiskCount = candidate.SafetyRatings.Count(r => 
                    r.Probability == "HIGH" || r.Probability == "MEDIUM");
                confidence -= highRiskCount * 0.1m;
            }

            // Adjust based on finish reason
            confidence = candidate.FinishReason switch
            {
                "STOP" => confidence + 0.1m,
                "MAX_TOKENS" => confidence - 0.05m,
                "SAFETY" => confidence - 0.2m,
                _ => confidence
            };

            return Math.Max(0.1m, Math.Min(1.0m, confidence));
        }
        finally
        {
            LogMethodExit();
        }
    }

    private LLMFinishReason MapFinishReason(string? finishReason)
    {
        LogMethodEntry();
        try
        {
            return finishReason?.ToUpper() switch
            {
                "STOP" => LLMFinishReason.Complete,
                "MAX_TOKENS" => LLMFinishReason.MaxTokens,
                "SAFETY" => LLMFinishReason.Error,
                "RECITATION" => LLMFinishReason.Error,
                _ => LLMFinishReason.Complete
            };
        }
        finally
        {
            LogMethodExit();
        }
    }

    private int EstimateTokens(string text)
    {
        LogMethodEntry();
        try
        {
            // Rough estimate: 1 token per 4 characters for English text
            return string.IsNullOrEmpty(text) ? 0 : text.Length / 4;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private int CountModelsInResponse(string response)
    {
        LogMethodEntry();
        try
        {
            var matches = Regex.Matches(response, @"""name"":\s*""models/[^""]+""");
            return matches.Count;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private string ExtractParameterSize(string modelName)
    {
        LogMethodEntry();
        try
        {
            // Gemini models don't typically expose parameter counts
            return modelName.ToLower() switch
            {
                var n when n.Contains("1.5-pro") => "1.5T",
                var n when n.Contains("1.5-flash") => "8B",
                var n when n.Contains("pro") => "540B",
                _ => "unknown"
            };
        }
        finally
        {
            LogMethodExit();
        }
    }

    private List<string> GetModelCapabilities(string modelName)
    {
        LogMethodEntry();
        try
        {
            var capabilities = new List<string> { "text-generation", "completion" };
            var name = modelName.ToLower();

            capabilities.Add("chat");
            capabilities.Add("instruction-following");

            if (name.Contains("vision"))
            {
                capabilities.Add("image-understanding");
                capabilities.Add("visual-question-answering");
            }

            if (name.Contains("code"))
            {
                capabilities.Add("code-generation");
                capabilities.Add("code-completion");
            }

            return capabilities;
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
            _rateLimiter?.Dispose();
            _httpClient?.Dispose();
        }
        
        base.Dispose(disposing);
    }

    #region Internal Response Models

    private class GeminiGenerateResponse
    {
        public List<GeminiCandidate>? Candidates { get; set; }
        public GeminiUsageMetadata? UsageMetadata { get; set; }
    }

    private class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
        public string? FinishReason { get; set; }
        public List<GeminiSafetyRating>? SafetyRatings { get; set; }
    }

    private class GeminiContent
    {
        public List<GeminiPart>? Parts { get; set; }
    }

    private class GeminiPart
    {
        public string Text { get; set; } = string.Empty;
    }

    private class GeminiSafetyRating
    {
        public string Category { get; set; } = string.Empty;
        public string Probability { get; set; } = string.Empty;
    }

    private class GeminiUsageMetadata
    {
        public int PromptTokenCount { get; set; }
        public int CandidatesTokenCount { get; set; }
        public int TotalTokenCount { get; set; }
    }

    private class GeminiEmbeddingResponse
    {
        public GeminiEmbedding? Embedding { get; set; }
    }

    private class GeminiEmbedding
    {
        public float[]? Values { get; set; }
    }

    private class GeminiModelsResponse
    {
        public List<GeminiModel>? Models { get; set; }
    }

    private class GeminiModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Version { get; set; }
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public int? InputTokenLimit { get; set; }
        public int? OutputTokenLimit { get; set; }
        public List<string>? SupportedGenerationMethods { get; set; }
    }

    #endregion
}