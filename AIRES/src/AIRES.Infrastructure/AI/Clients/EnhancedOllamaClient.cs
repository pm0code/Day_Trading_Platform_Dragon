using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AIRES.Core.Configuration;
using AIRES.Core.Interfaces;
using AIRES.Core.Models;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Results;
using AIRES.Foundation.Logging;
using AIRES.Infrastructure.AI.Models;

namespace AIRES.Infrastructure.AI.Clients;

/// <summary>
/// Enhanced Ollama client with GPU detection and advanced load balancing.
/// </summary>
public class EnhancedOllamaClient : AIRESServiceBase, IOllamaClient
{
    private readonly IEnhancedLoadBalancerService _loadBalancer;
    private readonly IAIRESConfiguration _configuration;
    private readonly IAIRESLogger _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly bool _useLoadBalancing;
    private readonly HttpClient _fallbackClient;
    
    public EnhancedOllamaClient(
        IEnhancedLoadBalancerService loadBalancer,
        IAIRESConfiguration configuration,
        IAIRESLogger logger,
        IHttpClientFactory httpClientFactory) : base(logger)
    {
        _loadBalancer = loadBalancer ?? throw new ArgumentNullException(nameof(loadBalancer));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        
        _useLoadBalancing = _configuration.AI.EnableGpuLoadBalancing;
        
        // Create fallback client for single GPU mode
        _fallbackClient = _httpClientFactory.CreateClient("Ollama");
        _fallbackClient.BaseAddress = new Uri(_configuration.AI.OllamaBaseUrl);
        _fallbackClient.Timeout = TimeSpan.FromSeconds(_configuration.AI.OllamaTimeout);
    }
    
    /// <summary>
    /// Generates a response using the most appropriate GPU instance.
    /// </summary>
    public async Task<AIRESResult<OllamaResponse>> GenerateAsync(
        OllamaRequest request,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            if (!_useLoadBalancing)
            {
                // Single GPU mode - use fallback client
                _logger.LogDebug("Using single GPU mode");
                var singleResult = await GenerateWithClientAsync(
                    _fallbackClient, 
                    request, 
                    "SingleGPU",
                    cancellationToken);
                    
                LogMethodExit();
                return singleResult;
            }
            
            // Multi-GPU mode - use load balancer
            var requirements = new ModelRequirements
            {
                ModelName = request.Model,
                EstimatedMemoryMB = EstimateModelMemory(request.Model),
                RequiresFloat16 = request.Model.Contains("f16"),
                RequiresBFloat16 = request.Model.Contains("bf16")
            };
            
            GpuInstance instance;
            try
            {
                instance = await _loadBalancer.SelectInstanceAsync(requirements, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to select instance: {ex.Message}");
                LogMethodExit();
                return AIRESResult<OllamaResponse>.Failure("NO_INSTANCE", ex.Message);
            }
            _logger.LogInfo($"Selected instance {instance.Id} on GPU {instance.GpuId} (Port: {instance.Port})");
            
            // Create client for selected instance
            var client = _httpClientFactory.CreateClient($"Ollama-{instance.Id}");
            client.BaseAddress = new Uri(instance.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(_configuration.AI.OllamaTimeout);
            
            // Generate response
            var generateResult = await GenerateWithClientAsync(
                client,
                request,
                instance.Id,
                cancellationToken);
                
            // Report metrics
            var responseTime = stopwatch.ElapsedMilliseconds;
            if (generateResult.IsSuccess)
            {
                await _loadBalancer.ReportSuccessAsync(instance.Id, responseTime, cancellationToken);
                _logger.LogInfo($"Request completed successfully on {instance.Id} in {responseTime}ms");
            }
            else
            {
                await _loadBalancer.ReportFailureAsync(instance.Id, generateResult.ErrorCode, cancellationToken);
                _logger.LogWarning($"Request failed on {instance.Id}: {generateResult.ErrorMessage}");
            }
            
            LogMethodExit();
            return generateResult;
        }
        catch (Exception ex)
        {
            _logger.LogError("Enhanced Ollama client error", ex);
            LogMethodExit();
            return AIRESResult<OllamaResponse>.Failure("CLIENT_ERROR", ex.Message);
        }
    }
    
    /// <summary>
    /// Checks if a model is available.
    /// </summary>
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
                return AIRESResult<bool>.Failure(modelsResult.ErrorCode ?? "UNKNOWN", modelsResult.ErrorMessage ?? "Unknown error");
            }
            
            var exists = modelsResult.Value?.Any(m => m.Name == modelName) ?? false;
            
            LogMethodExit();
            return AIRESResult<bool>.Success(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to check model {modelName}", ex);
            LogMethodExit();
            return AIRESResult<bool>.Failure("CHECK_ERROR", ex.Message);
        }
    }
    
    /// <summary>
    /// Lists available models across all GPU instances.
    /// </summary>
    public async Task<AIRESResult<List<OllamaModel>>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            // For now, use the fallback client to list models
            // In future, aggregate models from all instances
            var response = await _fallbackClient.GetAsync("/api/tags", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to list models: {response.StatusCode}");
                LogMethodExit();
                return AIRESResult<List<OllamaModel>>.Failure("LIST_FAILED", $"HTTP {response.StatusCode}");
            }
            
            var responseData = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDoc = JsonDocument.Parse(responseData);
            
            var models = new List<OllamaModel>();
            if (jsonDoc.RootElement.TryGetProperty("models", out var modelsArray))
            {
                foreach (var modelElement in modelsArray.EnumerateArray())
                {
                    var model = new OllamaModel
                    {
                        Name = modelElement.GetProperty("name").GetString() ?? string.Empty,
                        Tag = modelElement.TryGetProperty("tag", out var tag) ? tag.GetString() ?? string.Empty : string.Empty,
                        Size = modelElement.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                        ModifiedAt = modelElement.TryGetProperty("modified_at", out var modifiedAt) 
                            ? DateTime.Parse(modifiedAt.GetString() ?? DateTime.UtcNow.ToString()) 
                            : DateTime.UtcNow,
                        Digest = modelElement.TryGetProperty("digest", out var digest) ? digest.GetString() ?? string.Empty : string.Empty
                    };
                    models.Add(model);
                }
            }
            
            _logger.LogInfo($"Listed {models.Count} models");
            LogMethodExit();
            return AIRESResult<List<OllamaModel>>.Success(models);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to list models", ex);
            LogMethodExit();
            return AIRESResult<List<OllamaModel>>.Failure("LIST_ERROR", ex.Message);
        }
    }
    
    /// <summary>
    /// Pulls a model to the most appropriate GPU instance.
    /// </summary>
    public async Task<AIRESResult<OllamaPullResponse>> PullModelAsync(
        string modelName,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            // For model pulling, select instance based on available memory
            var requirements = new ModelRequirements
            {
                ModelName = modelName,
                EstimatedMemoryMB = EstimateModelMemory(modelName)
            };
            
            HttpClient client;
            string instanceId;
            
            if (_useLoadBalancing)
            {
                GpuInstance instance;
                try
                {
                    instance = await _loadBalancer.SelectInstanceAsync(requirements, cancellationToken);
                }
                catch (Exception ex)
                {
                    LogMethodExit();
                    return AIRESResult<OllamaPullResponse>.Failure("NO_INSTANCE", ex.Message);
                }
                instanceId = instance.Id;
                client = _httpClientFactory.CreateClient($"Ollama-{instance.Id}");
                client.BaseAddress = new Uri(instance.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(30); // Longer timeout for model downloads
            }
            else
            {
                instanceId = "SingleGPU";
                client = _fallbackClient;
            }
            
            _logger.LogInfo($"Pulling model {modelName} to {instanceId}");
            
            var pullRequest = new { name = modelName };
            var response = await client.PostAsJsonAsync("/api/pull", pullRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                LogMethodExit();
                return AIRESResult<OllamaPullResponse>.Failure("PULL_FAILED", $"HTTP {response.StatusCode}");
            }
            
            var pullResponse = new OllamaPullResponse
            {
                Status = "success",
                Model = modelName
            };
            
            LogMethodExit();
            return AIRESResult<OllamaPullResponse>.Success(pullResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to pull model {modelName}", ex);
            LogMethodExit();
            return AIRESResult<OllamaPullResponse>.Failure("PULL_ERROR", ex.Message);
        }
    }
    
    /// <summary>
    /// Gets the health status of all GPU instances.
    /// </summary>
    public async Task<AIRESResult<LoadBalancerHealth>> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        try
        {
            if (!_useLoadBalancing)
            {
                // Single GPU mode - create simple health status
                var singleHealth = new LoadBalancerHealth
                {
                    TotalInstances = 1,
                    HealthyInstances = 1,
                    Instances = new List<InstanceHealth>
                    {
                        new InstanceHealth
                        {
                            InstanceId = "SingleGPU",
                            GpuId = 0,
                            Port = 11434,
                            IsHealthy = true,
                            HealthScore = 1.0
                        }
                    }
                };
                
                LogMethodExit();
                return AIRESResult<LoadBalancerHealth>.Success(singleHealth);
            }
            
            try
            {
                var health = await _loadBalancer.GetHealthStatusAsync(cancellationToken);
                LogMethodExit();
                return AIRESResult<LoadBalancerHealth>.Success(health);
            }
            catch (Exception ex)
            {
                LogMethodExit();
                return AIRESResult<LoadBalancerHealth>.Failure("HEALTH_ERROR", ex.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to get health status", ex);
            LogMethodExit();
            return AIRESResult<LoadBalancerHealth>.Failure("HEALTH_ERROR", ex.Message);
        }
    }
    
    private async Task<AIRESResult<OllamaResponse>> GenerateWithClientAsync(
        HttpClient client,
        OllamaRequest request,
        string instanceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            
            var response = await client.PostAsJsonAsync(
                "/api/generate",
                request,
                jsonOptions,
                cancellationToken);
                
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return AIRESResult<OllamaResponse>.Failure(
                    "GENERATE_FAILED",
                    $"HTTP {response.StatusCode}: {error}");
            }
            
            var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaResponse>(
                jsonOptions,
                cancellationToken);
                
            if (ollamaResponse == null)
            {
                return AIRESResult<OllamaResponse>.Failure("PARSE_ERROR", "Failed to parse response");
            }
            
            return AIRESResult<OllamaResponse>.Success(ollamaResponse);
        }
        catch (HttpRequestException ex)
        {
            return AIRESResult<OllamaResponse>.Failure("HTTP_ERROR", ex.Message);
        }
        catch (TaskCanceledException)
        {
            return AIRESResult<OllamaResponse>.Failure("TIMEOUT", "Request timed out");
        }
        catch (Exception ex)
        {
            return AIRESResult<OllamaResponse>.Failure("UNEXPECTED_ERROR", ex.Message);
        }
    }
    
    private int EstimateModelMemory(string modelName)
    {
        // Rough estimates based on model names
        return modelName.ToLower() switch
        {
            var n when n.Contains("70b") => 40000,
            var n when n.Contains("34b") => 20000,
            var n when n.Contains("13b") => 8000,
            var n when n.Contains("9b") => 6000,
            var n when n.Contains("7b") => 4500,
            var n when n.Contains("3b") => 2000,
            _ => 4000
        };
    }
}