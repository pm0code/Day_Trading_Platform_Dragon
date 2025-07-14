using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AIRES.Core.Configuration;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace AIRES.Infrastructure.AI.Clients;

/// <summary>
/// HTTP client for Ollama LLM communication.
/// Extends AIRESServiceBase for canonical logging and lifecycle management.
/// </summary>
public class OllamaClient : AIRESServiceBase, IOllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly IAIRESConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public OllamaClient(
        IAIRESLogger logger,
        HttpClient httpClient,
        IAIRESConfiguration configuration)
        : base(logger, nameof(OllamaClient))
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        
        // Configure JSON options
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        
        // Configure base URL from configuration
        var baseUrl = _configuration.AIServices.OllamaBaseUrl;
        _httpClient.BaseAddress = new Uri(baseUrl);
        LogInfo($"Configured Ollama base URL: {baseUrl}");
        
        // Configure retry policy with exponential backoff
        // TODO: Add circuit breaker when upgrading to Polly 8.x resilience strategies
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                _configuration.Pipeline.MaxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) * _configuration.Pipeline.RetryDelay),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var message = outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString() ?? "Unknown error";
                    LogWarning($"Retry {retryCount} after {timespan}ms due to: {message}");
                    IncrementCounter("OllamaRetryCount");
                });
    }

    public async Task<AIRESResult<OllamaResponse>> GenerateAsync(
        OllamaRequest request,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            if (request == null)
            {
                LogMethodExit();
                return AIRESResult<OllamaResponse>.Failure("OLLAMA_NULL_REQUEST", "Request cannot be null");
            }
            
            // Ensure model is available before proceeding
            var ensureResult = await EnsureModelAvailableAsync(request.Model, cancellationToken);
            if (!ensureResult.IsSuccess)
            {
                LogMethodExit();
                return AIRESResult<OllamaResponse>.Failure(ensureResult.ErrorCode!, ensureResult.ErrorMessage!);
            }
            
            LogInfo($"Generating response with model: {request.Model}");
            LogDebug($"Prompt length: {request.Prompt.Length} characters");
            IncrementCounter("GenerateRequests");
            
            // Prepare request body
            var requestBody = new
            {
                model = request.Model,
                prompt = request.Prompt,
                system = request.System,
                stream = request.Stream,
                options = new
                {
                    temperature = request.Temperature,
                    top_p = request.TopP,
                    num_predict = request.MaxTokens
                }
            };
            
            var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            // Configure timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));
            
            // Make request with retry policy
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await _retryPolicy.ExecuteAsync(
                async (ct) => await _httpClient.PostAsync("/api/generate", content, ct),
                cts.Token);
            
            response.EnsureSuccessStatusCode();
            
            // Parse response
            var responseJson = await response.Content.ReadAsStringAsync(cts.Token);
            var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseJson, _jsonOptions);
            
            if (ollamaResponse == null)
            {
                LogMethodExit();
                return AIRESResult<OllamaResponse>.Failure("OLLAMA_INVALID_RESPONSE", "Failed to parse Ollama response");
            }
            
            stopwatch.Stop();
            LogInfo($"Generated response in {stopwatch.ElapsedMilliseconds}ms");
            UpdateMetric("GenerationDuration", stopwatch.ElapsedMilliseconds);
            IncrementCounter("SuccessfulGenerations");
            
            LogMethodExit();
            return AIRESResult<OllamaResponse>.Success(ollamaResponse);
        }
        catch (TimeoutRejectedException tex)
        {
            LogError($"Ollama request timed out after {request.TimeoutSeconds}s", tex);
            IncrementCounter("TimeoutErrors");
            LogMethodExit();
            return AIRESResult<OllamaResponse>.Failure("OLLAMA_TIMEOUT", $"Request timed out after {request.TimeoutSeconds} seconds", tex);
        }
        catch (HttpRequestException hex)
        {
            LogError("HTTP request failed", hex);
            IncrementCounter("HttpErrors");
            LogMethodExit();
            return AIRESResult<OllamaResponse>.Failure("OLLAMA_HTTP_ERROR", "Failed to communicate with Ollama server", hex);
        }
        catch (Exception ex)
        {
            LogError("Unexpected error during Ollama generation", ex);
            LogMethodExit();
            return AIRESResult<OllamaResponse>.Failure("OLLAMA_UNEXPECTED_ERROR", "An unexpected error occurred", ex);
        }
    }

    public async Task<AIRESResult<bool>> IsModelAvailableAsync(
        string modelName,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            var modelsResult = await ListModelsAsync(cancellationToken);
            if (!modelsResult.IsSuccess)
            {
                LogMethodExit();
                return AIRESResult<bool>.Failure(modelsResult.ErrorCode!, modelsResult.ErrorMessage!);
            }
            
            var isAvailable = modelsResult.Value!.Any(m => m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase));
            LogDebug($"Model '{modelName}' availability: {isAvailable}");
            
            LogMethodExit();
            return AIRESResult<bool>.Success(isAvailable);
        }
        catch (Exception ex)
        {
            LogError($"Error checking model availability for {modelName}", ex);
            LogMethodExit();
            return AIRESResult<bool>.Failure("OLLAMA_MODEL_CHECK_ERROR", "Failed to check model availability", ex);
        }
    }

    public async Task<AIRESResult<List<OllamaModel>>> ListModelsAsync(
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OllamaModelsResponse>(json, _jsonOptions);
            
            if (result?.Models == null)
            {
                LogMethodExit();
                return AIRESResult<List<OllamaModel>>.Success(new List<OllamaModel>());
            }
            
            var models = result.Models.Select(m => new OllamaModel
            {
                Name = m.Name ?? string.Empty,
                Tag = ExtractTag(m.Name ?? string.Empty),
                Size = m.Size,
                ModifiedAt = m.ModifiedAt,
                Digest = m.Digest ?? string.Empty
            }).ToList();
            
            LogInfo($"Found {models.Count} available models");
            UpdateMetric("AvailableModelsCount", models.Count);
            LogMethodExit();
            return AIRESResult<List<OllamaModel>>.Success(models);
        }
        catch (Exception ex)
        {
            LogError("Error listing models", ex);
            LogMethodExit();
            return AIRESResult<List<OllamaModel>>.Failure("OLLAMA_LIST_MODELS_ERROR", "Failed to list available models", ex);
        }
    }
    
    private static string ExtractTag(string modelName)
    {
        // No logging needed for static utility method
        var parts = modelName.Split(':');
        return parts.Length > 1 ? parts[1] : "latest";
    }
    
    /// <summary>
    /// Ensures a model is available, pulling it if necessary.
    /// </summary>
    private async Task<AIRESResult<bool>> EnsureModelAvailableAsync(
        string modelName,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            // Check if model is already available
            var availabilityResult = await IsModelAvailableAsync(modelName, cancellationToken);
            if (availabilityResult.IsSuccess && availabilityResult.Value)
            {
                LogDebug($"Model '{modelName}' is already available");
                LogMethodExit();
                return AIRESResult<bool>.Success(true);
            }
            
            LogWarning($"Model '{modelName}' is not available. Attempting to pull it...");
            
            // Pull the model
            var pullResult = await PullModelAsync(modelName, cancellationToken);
            if (!pullResult.IsSuccess)
            {
                LogMethodExit();
                return pullResult;
            }
            
            LogInfo($"Successfully pulled model '{modelName}'");
            LogMethodExit();
            return AIRESResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError($"Error ensuring model availability for {modelName}", ex);
            LogMethodExit();
            return AIRESResult<bool>.Failure("OLLAMA_ENSURE_MODEL_ERROR", $"Failed to ensure model '{modelName}' is available", ex);
        }
    }
    
    /// <summary>
    /// Pulls a model from the Ollama registry.
    /// </summary>
    public async Task<AIRESResult<bool>> PullModelAsync(
        string modelName,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            LogInfo($"Pulling model '{modelName}' from Ollama registry...");
            IncrementCounter("ModelPullRequests");
            
            var requestBody = new { name = modelName };
            var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            // Model pulling can take a long time, use extended timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(30)); // 30 minutes for large models
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.PostAsync("/api/pull", content, cts.Token);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
                LogError($"Failed to pull model '{modelName}': {response.StatusCode} - {errorContent}");
                LogMethodExit();
                return AIRESResult<bool>.Failure("OLLAMA_PULL_FAILED", $"Failed to pull model '{modelName}': {response.StatusCode}");
            }
            
            // Read streaming response to track progress
            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream);
            
            string? line;
            while ((line = await reader.ReadLineAsync(cts.Token)) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    try
                    {
                        var progress = JsonSerializer.Deserialize<OllamaPullProgress>(line, _jsonOptions);
                        if (progress?.Status != null)
                        {
                            LogDebug($"Pull progress: {progress.Status}");
                        }
                    }
                    catch
                    {
                        // Ignore JSON parsing errors for progress updates
                    }
                }
            }
            
            stopwatch.Stop();
            LogInfo($"Model '{modelName}' pulled successfully in {stopwatch.Elapsed.TotalSeconds:F1} seconds");
            UpdateMetric("ModelPullDuration", stopwatch.ElapsedMilliseconds);
            IncrementCounter("SuccessfulModelPulls");
            
            LogMethodExit();
            return AIRESResult<bool>.Success(true);
        }
        catch (TaskCanceledException)
        {
            LogError($"Model pull for '{modelName}' was cancelled or timed out");
            IncrementCounter("ModelPullTimeouts");
            LogMethodExit();
            return AIRESResult<bool>.Failure("OLLAMA_PULL_TIMEOUT", $"Model pull for '{modelName}' timed out");
        }
        catch (Exception ex)
        {
            LogError($"Error pulling model '{modelName}'", ex);
            IncrementCounter("ModelPullErrors");
            LogMethodExit();
            return AIRESResult<bool>.Failure("OLLAMA_PULL_ERROR", $"Failed to pull model '{modelName}'", ex);
        }
    }
    
    // Internal response DTOs
    private sealed class OllamaModelsResponse
    {
        public List<OllamaModelDto>? Models { get; set; }
    }
    
    private sealed class OllamaModelDto
    {
        public string? Name { get; set; }
        public long Size { get; set; }
        public DateTime ModifiedAt { get; set; }
        public string? Digest { get; set; }
    }
    
    private sealed class OllamaPullProgress
    {
        public string? Status { get; set; }
        public string? Digest { get; set; }
        public long? Total { get; set; }
        public long? Completed { get; set; }
    }
}