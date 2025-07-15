using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AIRES.Core.Configuration;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;
using AIRES.Infrastructure.AI.Models;
using AIRES.Infrastructure.AI.Services;

namespace AIRES.Infrastructure.AI.Clients;

/// <summary>
/// Load-balanced Ollama client that distributes requests across multiple GPU instances.
/// Implements IOllamaClient interface with full canonical compliance.
/// </summary>
public class LoadBalancedOllamaClient : AIRESServiceBase, IOllamaClient
{
    private readonly OllamaLoadBalancerService _loadBalancer;
    private readonly IAIRESConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions;

    public LoadBalancedOllamaClient(
        IAIRESLogger logger,
        OllamaLoadBalancerService loadBalancer,
        IAIRESConfiguration configuration)
        : base(logger, nameof(LoadBalancedOllamaClient))
    {
        _loadBalancer = loadBalancer ?? throw new ArgumentNullException(nameof(loadBalancer));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc />
    public async Task<AIRESResult<OllamaResponse>> GenerateAsync(
        OllamaRequest request,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        OllamaInstance? instance = null;
        
        try
        {
            // Validate request
            LogDebug("Validating generate request");
            if (request == null)
            {
                LogError("Generate request is null");
                return AIRESResult<OllamaResponse>.Failure("InvalidRequest", "Request cannot be null");
            }
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                LogError("Model name is empty or null");
                return AIRESResult<OllamaResponse>.Failure("InvalidModel", "Model name is required");
            }
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                LogError("Prompt is empty or null");
                return AIRESResult<OllamaResponse>.Failure("InvalidPrompt", "Prompt is required");
            }
            LogDebug($"Request validated - Model: {request.Model}, Prompt length: {request.Prompt.Length}");
            
            // Get next healthy instance
            LogDebug("Requesting next healthy Ollama instance from load balancer");
            instance = await _loadBalancer.GetNextInstanceAsync();
            LogInfo($"Assigned to {instance.Name} (Port: {instance.Port}, Healthy: {instance.IsHealthy})");
            UpdateMetric($"LoadBalancedOllama.GPU{instance.GpuId}.Requests", 1);
            
            // Ensure model is available on this instance
            await EnsureModelAvailableAsync(request.Model, instance);
            
            LogInfo($"Generating response with model: {request.Model} on {instance.Name}");
            
            // Create request body
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
                    max_tokens = request.MaxTokens
                }
            };
            
            var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            LogDebug($"Request JSON size: {json.Length} bytes");
            
            // Create client for this instance
            using var httpClient = _loadBalancer.CreateClientForInstance(instance);
            httpClient.Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds);
            
            // Make request
            LogDebug($"Sending POST request to {instance.BaseUrl}/api/generate");
            var response = await httpClient.PostAsync("/api/generate", content, cancellationToken);
            LogDebug($"Received response: {response.StatusCode} ({(int)response.StatusCode})");
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                LogError($"Ollama returned error: {response.StatusCode} - {errorContent}");
                return AIRESResult<OllamaResponse>.Failure(
                    $"OllamaError_{response.StatusCode}",
                    $"Ollama service returned {response.StatusCode}: {errorContent}");
            }
            
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            LogDebug($"Response JSON size: {responseJson.Length} bytes");
            
            var result = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseJson, _jsonOptions);
            
            if (result?.Response == null)
            {
                LogError($"Invalid response from Ollama: {responseJson.Substring(0, Math.Min(200, responseJson.Length))}...");
                return AIRESResult<OllamaResponse>.Failure(
                    "InvalidResponse",
                    "Received null or invalid response from Ollama");
            }
            
            LogDebug($"Successfully parsed response, length: {result.Response.Length} characters");
            
            stopwatch.Stop();
            await _loadBalancer.ReportSuccessAsync(instance, stopwatch.ElapsedMilliseconds);
            
            UpdateMetric("LoadBalancedOllama.SuccessfulGenerations", 1);
            UpdateMetric($"LoadBalancedOllama.GPU{instance.GpuId}.ResponseTime", stopwatch.ElapsedMilliseconds);
            LogInfo($"Generated response in {stopwatch.ElapsedMilliseconds}ms on {instance.Name}");
            
            // Map to canonical OllamaResponse
            var ollamaResponse = new OllamaResponse
            {
                Response = result.Response,
                Model = request.Model,
                Done = result.Done ?? true,
                Duration = TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds),
                TokensPerSecond = result.TokensPerSecond,
                TotalTokens = result.TotalTokens
            };
            
            LogMethodExit();
            return AIRESResult<OllamaResponse>.Success(ollamaResponse);
        }
        catch (HttpRequestException ex)
        {
            UpdateMetric("LoadBalancedOllama.NetworkErrors", 1);
            LogError($"Network error while generating response on {instance?.Name}", ex);
            
            if (instance != null)
                await _loadBalancer.ReportFailureAsync(instance, ex);
            
            LogMethodExit();
            return AIRESResult<OllamaResponse>.Failure(
                "NetworkError",
                $"Failed to communicate with Ollama service: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            UpdateMetric("LoadBalancedOllama.Timeouts", 1);
            LogError($"Request timeout on {instance?.Name}", ex);
            
            if (instance != null)
                await _loadBalancer.ReportFailureAsync(instance, ex);
            
            LogMethodExit();
            return AIRESResult<OllamaResponse>.Failure(
                "Timeout",
                $"Request to Ollama service timed out after {request.TimeoutSeconds} seconds");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            UpdateMetric("LoadBalancedOllama.Cancelled", 1);
            LogInfo("Request was cancelled by caller");
            
            LogMethodExit();
            return AIRESResult<OllamaResponse>.Failure(
                "Cancelled",
                "Operation was cancelled");
        }
        catch (Exception ex)
        {
            UpdateMetric("LoadBalancedOllama.UnexpectedErrors", 1);
            LogError($"Unexpected error on {instance?.Name}", ex);
            
            if (instance != null)
                await _loadBalancer.ReportFailureAsync(instance, ex);
            
            LogMethodExit();
            return AIRESResult<OllamaResponse>.Failure(
                "UnexpectedError",
                $"An unexpected error occurred: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<AIRESResult<bool>> IsModelAvailableAsync(
        string modelName,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                LogError("Model name is null or empty");
                return AIRESResult<bool>.Failure(
                    "InvalidModelName",
                    "Model name cannot be null or empty");
            }
            
            // Check if model is available on any healthy instance
            var instances = await _loadBalancer.GetInstanceStatusAsync();
            LogDebug($"Checking model availability across {instances.Count} instances");
            
            foreach (var instance in instances.Where(i => i.IsHealthy))
            {
                if (await CheckModelOnInstanceAsync(modelName, instance, cancellationToken))
                {
                    LogInfo($"Model {modelName} is available on {instance.Name}");
                    LogMethodExit();
                    return AIRESResult<bool>.Success(true);
                }
            }
            
            LogInfo($"Model {modelName} is not available on any healthy instance");
            LogMethodExit();
            return AIRESResult<bool>.Success(false);
        }
        catch (Exception ex)
        {
            LogError($"Error checking model availability: {modelName}", ex);
            LogMethodExit();
            return AIRESResult<bool>.Failure(
                "CheckFailed",
                $"Failed to check model availability: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<AIRESResult<List<OllamaModel>>> ListModelsAsync(
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            // Get models from first healthy instance
            var instance = await _loadBalancer.GetNextInstanceAsync();
            using var httpClient = _loadBalancer.CreateClientForInstance(instance);
            
            LogDebug($"Listing models from {instance.Name}");
            var response = await httpClient.GetAsync("/api/tags", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                LogError($"Failed to list models: {response.StatusCode} - {errorContent}");
                return AIRESResult<List<OllamaModel>>.Failure(
                    $"ListModelsFailed_{response.StatusCode}",
                    $"Failed to retrieve model list: {errorContent}");
            }
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OllamaTagsResponse>(json, _jsonOptions);
            
            var models = new List<OllamaModel>();
            if (result?.Models != null)
            {
                foreach (var model in result.Models)
                {
                    models.Add(new OllamaModel
                    {
                        Name = model.Name ?? string.Empty,
                        Tag = ExtractTag(model.Name ?? string.Empty),
                        Size = model.Size ?? 0,
                        ModifiedAt = model.ModifiedAt ?? DateTime.MinValue,
                        Digest = model.Digest ?? string.Empty
                    });
                }
            }
            
            LogInfo($"Found {models.Count} available models on {instance.Name}");
            LogMethodExit();
            return AIRESResult<List<OllamaModel>>.Success(models);
        }
        catch (Exception ex)
        {
            LogError("Failed to list models", ex);
            LogMethodExit();
            return AIRESResult<List<OllamaModel>>.Failure(
                "ListModelsFailed",
                $"Failed to retrieve model list: {ex.Message}");
        }
    }

    private static string ExtractTag(string modelName)
    {
        var colonIndex = modelName.IndexOf(':');
        return colonIndex > 0 ? modelName.Substring(colonIndex + 1) : "latest";
    }

    private async Task EnsureModelAvailableAsync(string modelName, OllamaInstance instance)
    {
        LogMethodEntry();
        
        try
        {
            if (!await CheckModelOnInstanceAsync(modelName, instance, CancellationToken.None))
            {
                LogWarning($"Model {modelName} not available on {instance.Name}, attempting to pull");
                UpdateMetric($"LoadBalancedOllama.ModelPulls", 1);
                
                using var httpClient = _loadBalancer.CreateClientForInstance(instance);
                
                var pullRequest = new { name = modelName };
                var json = JsonSerializer.Serialize(pullRequest, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync("/api/pull", content);
                response.EnsureSuccessStatusCode();
                
                LogInfo($"Successfully pulled model {modelName} on {instance.Name}");
            }
            
            LogMethodExit();
        }
        catch (Exception ex)
        {
            LogError($"Failed to ensure model {modelName} is available", ex);
            LogMethodExit();
            throw new InvalidOperationException($"Failed to ensure model {modelName} is available: {ex.Message}", ex);
        }
    }

    private async Task<bool> CheckModelOnInstanceAsync(
        string modelName, 
        OllamaInstance instance,
        CancellationToken cancellationToken)
    {
        LogMethodEntry();
        
        try
        {
            using var httpClient = _loadBalancer.CreateClientForInstance(instance);
            var response = await httpClient.GetAsync("/api/tags", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                LogWarning($"Failed to get model list from {instance.Name}: {response.StatusCode}");
                LogMethodExit();
                return false;
            }
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OllamaTagsResponse>(json, _jsonOptions);
            
            if (result?.Models == null)
            {
                LogMethodExit();
                return false;
            }
            
            var isAvailable = result.Models.Any(m => 
                (m.Name ?? string.Empty).Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
                (m.Name ?? string.Empty).StartsWith($"{modelName}:", StringComparison.OrdinalIgnoreCase));
            
            LogDebug($"Model {modelName} {(isAvailable ? "is" : "is not")} available on {instance.Name}");
            LogMethodExit();
            return isAvailable;
        }
        catch (Exception ex)
        {
            LogError($"Error checking model on {instance.Name}", ex);
            LogMethodExit();
            return false;
        }
    }

    // Internal DTOs for JSON deserialization
    private class OllamaGenerateResponse
    {
        public string? Response { get; set; }
        public bool? Done { get; set; }
        public double? TokensPerSecond { get; set; }
        public int? TotalTokens { get; set; }
    }

    private class OllamaTagsResponse
    {
        public List<OllamaModelDto>? Models { get; set; }
    }

    private class OllamaModelDto
    {
        public string? Name { get; set; }
        public long? Size { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string? Digest { get; set; }
    }
}