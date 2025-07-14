using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
    private readonly IConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public OllamaClient(
        IAIRESLogger logger,
        HttpClient httpClient,
        IConfiguration configuration)
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
        var baseUrl = _configuration["AI_Services:OllamaBaseUrl"] ?? _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
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
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
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
}