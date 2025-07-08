using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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
/// Ollama provider for local LLM inference following state-of-the-art best practices.
/// Implements connection pooling, retry strategies, context management, and performance optimizations.
/// </summary>
public class OllamaProvider : CanonicalServiceBase, ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly ConcurrentDictionary<string, LLMModelInfo> _modelInfoCache;
    private readonly ConcurrentDictionary<string, DateTime> _modelLastUsed;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    /// <summary>
    /// Gets the provider name.
    /// </summary>
    public string ProviderName => "Ollama";

    /// <summary>
    /// Gets whether this provider runs locally.
    /// </summary>
    public bool IsLocal => true;

    public OllamaProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<OllamaOptions> options,
        IMemoryCache cache,
        ILogger<OllamaProvider> logger)
        : base(logger)
    {
        LogMethodEntry();
        try
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClientFactory.CreateClient("Ollama");
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _rateLimiter = new SemaphoreSlim(_options.MaxConcurrentRequests, _options.MaxConcurrentRequests);
            _modelInfoCache = new ConcurrentDictionary<string, LLMModelInfo>();
            _modelLastUsed = new ConcurrentDictionary<string, DateTime>();

            // Configure retry policy based on research
            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == HttpStatusCode.ServiceUnavailable)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        if (outcome.Result?.StatusCode == HttpStatusCode.ServiceUnavailable)
                        {
                            LogWarning($"Ollama service unavailable, waiting {timespan.TotalSeconds}s before retry {retryCount}");
                        }
                    });

            LogInfo($"OllamaProvider initialized with base URL: {_options.BaseUrl}");
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize OllamaProvider", ex);
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
            // Verify Ollama service is accessible
            var healthResult = await GetHealthAsync(cancellationToken).ConfigureAwait(false);
            if (!healthResult.IsSuccess || !healthResult.Value!.IsHealthy)
            {
                return TradingResult<bool>.Failure("OLLAMA_UNHEALTHY", "Ollama service is not healthy");
            }

            // Preload default models if configured
            if (_options.PreloadModels?.Any() == true)
            {
                foreach (var model in _options.PreloadModels)
                {
                    var preloadResult = await PreloadModelAsync(model, cancellationToken).ConfigureAwait(false);
                    if (!preloadResult.IsSuccess)
                    {
                        LogWarning($"Failed to preload model {model}: {preloadResult.Error?.Message}");
                    }
                }
            }

            LogInfo("OllamaProvider initialized successfully");
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Exception during OllamaProvider initialization", ex);
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
            LogInfo("OllamaProvider started successfully");
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
            _modelInfoCache.Clear();
            _modelLastUsed.Clear();
            await Task.CompletedTask.ConfigureAwait(false);
            LogInfo("OllamaProvider stopped successfully");
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

            // Check API availability
            try
            {
                var sw = Stopwatch.StartNew();
                var uri = new Uri("/api/tags", UriKind.Relative);
                var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
                sw.Stop();

                health.IsHealthy = response.IsSuccessStatusCode;
                health.AverageLatencyMs = sw.ElapsedMilliseconds;
                health.Status = response.IsSuccessStatusCode ? "Ollama service is running" : $"API returned {response.StatusCode}";

                if (response.IsSuccessStatusCode)
                {
                    var models = await ListModelsAsync(cancellationToken).ConfigureAwait(false);
                    if (models.IsSuccess)
                    {
                        health.AvailableModels = models.Value!.Count;
                        health.LoadedModels = models.Value!.Count(m => m.IsLoaded);
                    }
                }
            }
            catch (Exception ex)
            {
                health.IsHealthy = false;
                health.Status = $"Failed to connect to Ollama: {ex.Message}";
                health.Errors.Add(ex.Message);
            }

            LogInfo($"Ollama health check: {health.Status}");
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
                // Optimize prompt based on research
                var optimizedPrompt = OptimizePrompt(request.Prompt, request.MaxTokens);

                // Prepare Ollama request
                var ollamaRequest = new
                {
                    model = request.Model ?? _options.DefaultModel,
                    prompt = optimizedPrompt,
                    system = request.SystemPrompt,
                    stream = false,
                    options = new
                    {
                        temperature = (double)request.Temperature,
                        top_p = (double)request.TopP,
                        num_predict = request.MaxTokens,
                        stop = request.StopSequences?.ToArray(),
                        num_ctx = GetModelContextLength(request.Model ?? _options.DefaultModel)
                    }
                };

                // Execute with retry policy
                var sw = Stopwatch.StartNew();
                var httpResponse = await _retryPolicy.ExecuteAsync(async () =>
                    await _httpClient.PostAsJsonAsync("/api/generate", ollamaRequest, cancellationToken).ConfigureAwait(false)
                ).ConfigureAwait(false);
                
                httpResponse.EnsureSuccessStatusCode();
                
                var ollamaResponse = await httpResponse.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                sw.Stop();

                if (ollamaResponse == null)
                {
                    return TradingResult<LLMResponse>.Failure("INVALID_RESPONSE", "Received null response from Ollama");
                }

                // Convert to LLMResponse
                var response = new LLMResponse
                {
                    Text = ollamaResponse.Response,
                    Model = ollamaResponse.Model ?? request.Model ?? _options.DefaultModel,
                    Provider = ProviderName,
                    PromptTokens = ollamaResponse.PromptEvalCount ?? EstimateTokens(optimizedPrompt),
                    CompletionTokens = ollamaResponse.EvalCount ?? EstimateTokens(ollamaResponse.Response),
                    InferenceTimeMs = sw.ElapsedMilliseconds,
                    Cost = 0m, // Local inference
                    Confidence = CalculateConfidence(ollamaResponse, request),
                    FinishReason = ollamaResponse.Done ? LLMFinishReason.Complete : LLMFinishReason.MaxTokens,
                    Timestamp = DateTime.UtcNow
                };

                // Update model usage tracking
                _modelLastUsed[response.Model] = DateTime.UtcNow;

                // Cache response
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(_options.CacheTTLMinutes),
                    Size = 1
                };
                _cache.Set(cacheKey, response, cacheOptions);

                IncrementCounter("CompletionsGenerated");
                UpdateMetric("AverageInferenceTimeMs", response.InferenceTimeMs);
                LogInfo($"Completion generated in {response.InferenceTimeMs}ms using model {response.Model}");

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
            if (!await _rateLimiter.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                return TradingResult<LLMResponse>.Failure("RATE_LIMIT_EXCEEDED", "Too many concurrent requests");
            }

            try
            {
                var optimizedPrompt = OptimizePrompt(request.Prompt, request.MaxTokens);
                var ollamaRequest = new
                {
                    model = request.Model ?? _options.DefaultModel,
                    prompt = optimizedPrompt,
                    system = request.SystemPrompt,
                    stream = true,
                    options = new
                    {
                        temperature = (double)request.Temperature,
                        top_p = (double)request.TopP,
                        num_predict = request.MaxTokens,
                        stop = request.StopSequences?.ToArray()
                    }
                };

                var sw = Stopwatch.StartNew();
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
                {
                    Content = JsonContent.Create(ollamaRequest)
                };

                var httpResponse = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                httpResponse.EnsureSuccessStatusCode();

                var responseBuilder = new StringBuilder();
                var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(stream);

                string? line;
                OllamaGenerateResponse? lastResponse = null;
                var promptTokens = 0;
                var completionTokens = 0;

                while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var streamResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(line);
                        if (streamResponse != null)
                        {
                            responseBuilder.Append(streamResponse.Response);
                            onToken(streamResponse.Response);

                            if (streamResponse.Done)
                            {
                                lastResponse = streamResponse;
                                promptTokens = streamResponse.PromptEvalCount ?? 0;
                                completionTokens = streamResponse.EvalCount ?? 0;
                                break;
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        LogWarning($"Failed to parse streaming response: {ex.Message}");
                    }
                }

                sw.Stop();

                var response = new LLMResponse
                {
                    Text = responseBuilder.ToString(),
                    Model = lastResponse?.Model ?? request.Model ?? _options.DefaultModel,
                    Provider = ProviderName,
                    PromptTokens = promptTokens > 0 ? promptTokens : EstimateTokens(optimizedPrompt),
                    CompletionTokens = completionTokens > 0 ? completionTokens : EstimateTokens(responseBuilder.ToString()),
                    InferenceTimeMs = sw.ElapsedMilliseconds,
                    Cost = 0m,
                    Confidence = 0.85m, // Default for streaming
                    FinishReason = lastResponse?.Done == true ? LLMFinishReason.Complete : LLMFinishReason.MaxTokens,
                    Timestamp = DateTime.UtcNow
                };

                LogInfo($"Streaming completion generated in {response.InferenceTimeMs}ms");
                return TradingResult<LLMResponse>.Success(response);
            }
            finally
            {
                _rateLimiter.Release();
            }
        }
        catch (Exception ex)
        {
            LogError("Streaming generation failed", ex);
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
            var embeddingModel = model ?? _options.EmbeddingModel ?? "nomic-embed-text";
            
            var request = new
            {
                model = embeddingModel,
                prompt = text
            };

            var sw = Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var embeddingResponse = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (embeddingResponse?.Embedding == null)
            {
                return TradingResult<LLMEmbedding>.Failure("INVALID_RESPONSE", "No embedding returned");
            }

            var embedding = new LLMEmbedding
            {
                Vector = embeddingResponse.Embedding,
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
            var uri = new Uri("/api/tags", UriKind.Relative);
            var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var modelsResponse = await response.Content.ReadFromJsonAsync<OllamaModelsResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            
            if (modelsResponse?.Models == null)
            {
                return TradingResult<List<LLMModelInfo>>.Success(new List<LLMModelInfo>());
            }

            var modelInfos = modelsResponse.Models.Select(m => new LLMModelInfo
            {
                Name = m.Name,
                ParameterSize = ExtractParameterSize(m.Name),
                Quantization = ExtractQuantization(m.Name),
                ContextWindow = GetModelContextLength(m.Name),
                IsLoaded = true, // Ollama only returns loaded models
                SizeBytes = m.Size,
                RequiredVRAM = EstimateVRAM(m.Name),
                Capabilities = GetModelCapabilities(m.Name),
                LastUsed = _modelLastUsed.TryGetValue(m.Name, out var lastUsed) ? lastUsed : null
            }).ToList();

            // Update cache
            foreach (var info in modelInfos)
            {
                _modelInfoCache[info.Name] = info;
            }

            LogInfo($"Listed {modelInfos.Count} available models");
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
            // Check if model exists
            var modelsResult = await ListModelsAsync(cancellationToken).ConfigureAwait(false);
            if (!modelsResult.IsSuccess)
            {
                return TradingResult<bool>.Failure("LIST_MODELS_FAILED", "Failed to list available models");
            }

            var modelExists = modelsResult.Value!.Any(m => m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase));
            if (!modelExists)
            {
                LogInfo($"Model {modelName} not found locally, attempting to pull...");
                
                // Note: Ollama doesn't support pulling via API, this would need to be done via CLI
                return TradingResult<bool>.Failure("MODEL_NOT_FOUND", $"Model {modelName} not found. Please pull it using: ollama pull {modelName}");
            }

            // Warm up the model
            var warmupRequest = new
            {
                model = modelName,
                prompt = "Hello",
                keep_alive = "24h"
            };

            var response = await _httpClient.PostAsJsonAsync("/api/generate", warmupRequest, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                _modelLastUsed[modelName] = DateTime.UtcNow;
                LogInfo($"Model {modelName} preloaded and warmed up");
                return TradingResult<bool>.Success(true);
            }

            return TradingResult<bool>.Failure("PRELOAD_FAILED", $"Failed to preload model: {response.StatusCode}");
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
            // Ollama is local, so no API costs
            return 0m;
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
                TotalTokens = GetMetricValue("TotalTokensProcessed"),
                TotalCost = 0m, // Local inference
                AverageLatencyMs = GetMetricValue("AverageInferenceTimeMs"),
                PeriodStart = StartTime,
                PeriodEnd = DateTime.UtcNow
            };

            // Add model usage from cache
            foreach (var kvp in _modelLastUsed)
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
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
            return Convert.ToBase64String(hash);
        }
        finally
        {
            LogMethodExit();
        }
    }

    private string OptimizePrompt(string prompt, int maxTokens)
    {
        LogMethodEntry();
        try
        {
            // Remove redundant whitespace
            prompt = Regex.Replace(prompt, @"\s+", " ").Trim();

            // Compress numbers in financial data
            prompt = CompressNumbers(prompt);

            // Use abbreviations for common financial terms
            var abbreviations = new Dictionary<string, string>
            {
                ["moving average"] = "MA",
                ["relative strength index"] = "RSI",
                ["exponential moving average"] = "EMA",
                ["simple moving average"] = "SMA",
                ["earnings per share"] = "EPS",
                ["price to earnings"] = "P/E",
                ["market capitalization"] = "market cap"
            };

            foreach (var (full, abbr) in abbreviations)
            {
                prompt = prompt.Replace(full, abbr, StringComparison.OrdinalIgnoreCase);
            }

            // Truncate if still too long
            var estimatedTokens = EstimateTokens(prompt);
            if (estimatedTokens > maxTokens * 0.8) // Leave room for response
            {
                var targetLength = (int)(prompt.Length * (maxTokens * 0.8 / estimatedTokens));
                prompt = prompt.Substring(0, Math.Min(targetLength, prompt.Length)) + "...";
            }

            return prompt;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private string CompressNumbers(string text)
    {
        LogMethodEntry();
        try
        {
            return Regex.Replace(text, @"\b(\d{1,3}(?:,\d{3})+(?:\.\d+)?)\b", m =>
            {
                var value = decimal.Parse(m.Value.Replace(",", ""));
                return value switch
                {
                    >= 1_000_000_000m => $"{value / 1_000_000_000m:F1}B",
                    >= 1_000_000m => $"{value / 1_000_000m:F1}M",
                    >= 1_000m => $"{value / 1_000m:F1}K",
                    _ => value.ToString("F2")
                };
            });
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
            // Rough estimate: 1 token per 4 characters (based on research)
            return string.IsNullOrEmpty(text) ? 0 : text.Length / 4;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private decimal CalculateConfidence(OllamaGenerateResponse response, LLMRequest request)
    {
        LogMethodEntry();
        try
        {
            var confidence = 0.8m; // Base confidence for local models

            // Adjust based on completion
            if (response.Done)
            {
                confidence += 0.05m;
            }

            // Adjust based on temperature (lower = higher confidence)
            confidence += (1m - request.Temperature) * 0.1m;

            // Adjust based on response length
            if (response.EvalCount.HasValue && request.MaxTokens > 0)
            {
                var completionRatio = (decimal)response.EvalCount.Value / request.MaxTokens;
                if (completionRatio < 0.1m)
                {
                    confidence *= 0.8m;
                }
            }

            return Math.Max(0.1m, Math.Min(1.0m, confidence));
        }
        finally
        {
            LogMethodExit();
        }
    }

    private int GetModelContextLength(string modelName)
    {
        LogMethodEntry();
        try
        {
            var name = modelName.ToLower();
            return name switch
            {
                var n when n.Contains("llama3.1") => 128000,
                var n when n.Contains("llama3") => 8192,
                var n when n.Contains("llama2") => 4096,
                var n when n.Contains("codellama") => 16384,
                var n when n.Contains("mixtral") => 32768,
                var n when n.Contains("mistral") => 8192,
                var n when n.Contains("phi") => 2048,
                var n when n.Contains("yi") && n.Contains("34b") => 200000,
                _ => 4096 // Conservative default
            };
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
            var match = Regex.Match(modelName, @"(\d+)b", RegexOptions.IgnoreCase);
            return match.Success ? match.Value : "unknown";
        }
        finally
        {
            LogMethodExit();
        }
    }

    private string? ExtractQuantization(string modelName)
    {
        LogMethodEntry();
        try
        {
            var match = Regex.Match(modelName, @"(q\d_[kK0-9_]+|f16|f32)", RegexOptions.IgnoreCase);
            return match.Success ? match.Value : null;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private long EstimateVRAM(string modelName)
    {
        LogMethodEntry();
        try
        {
            // Extract parameter count
            var paramMatch = Regex.Match(modelName, @"(\d+)b", RegexOptions.IgnoreCase);
            if (!paramMatch.Success) return 4_000_000_000L; // Default 4GB

            var paramCount = long.Parse(paramMatch.Groups[1].Value) * 1_000_000_000L;

            // Determine bits per parameter from quantization
            var quantMatch = Regex.Match(modelName, @"q(\d)", RegexOptions.IgnoreCase);
            var bitsPerParam = quantMatch.Success ? float.Parse(quantMatch.Groups[1].Value) + 0.5f : 4.5f;

            // Calculate base memory
            var baseMemory = (long)(paramCount * bitsPerParam / 8);

            // Add 20% overhead for KV cache and activations
            return (long)(baseMemory * 1.2);
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

            if (name.Contains("instruct") || name.Contains("chat"))
            {
                capabilities.Add("chat");
                capabilities.Add("instruction-following");
            }

            if (name.Contains("code"))
            {
                capabilities.Add("code-generation");
                capabilities.Add("code-completion");
            }

            if (name.Contains("vision"))
            {
                capabilities.Add("image-understanding");
                capabilities.Add("visual-question-answering");
            }

            if (name == "nomic-embed-text" || name.Contains("embed"))
            {
                capabilities.Clear();
                capabilities.Add("embeddings");
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

    private class OllamaGenerateResponse
    {
        public string Response { get; set; } = string.Empty;
        public string? Model { get; set; }
        public bool Done { get; set; }
        public int? PromptEvalCount { get; set; }
        public int? EvalCount { get; set; }
        public long? TotalDuration { get; set; }
    }

    private class OllamaEmbeddingResponse
    {
        public float[]? Embedding { get; set; }
    }

    private class OllamaModelsResponse
    {
        public List<OllamaModelDetails>? Models { get; set; }
    }

    private class OllamaModelDetails
    {
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    #endregion
}