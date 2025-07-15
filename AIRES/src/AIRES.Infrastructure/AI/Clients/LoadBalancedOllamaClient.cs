using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AIRES.Core.Configuration;
using AIRES.Core.Domain.Interfaces;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Infrastructure.AI.Models;
using AIRES.Infrastructure.AI.Services;

namespace AIRES.Infrastructure.AI.Clients;

/// <summary>
/// Load-balanced Ollama client that distributes requests across multiple GPU instances.
/// Provides fault tolerance and improved performance.
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

    public async Task<string> GenerateAsync(GenerateRequest request)
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        OllamaInstance? instance = null;
        
        try
        {
            // Validate request
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Model))
                throw new ArgumentException("Model name is required", nameof(request));
            if (string.IsNullOrWhiteSpace(request.Prompt))
                throw new ArgumentException("Prompt is required", nameof(request));
            
            // Get next healthy instance
            instance = await _loadBalancer.GetNextInstanceAsync();
            UpdateMetric($"LoadBalancedOllama.GPU{instance.GpuId}.Requests", 1);
            
            // Ensure model is available on this instance
            await EnsureModelAvailableAsync(request.Model, instance);
            
            LogInfo($"Generating response with model: {request.Model} on {instance.Name}");
            
            // Create request body
            var requestBody = new
            {
                model = request.Model,
                prompt = request.Prompt,
                stream = false,
                options = new
                {
                    temperature = request.Temperature ?? 0.7,
                    top_p = request.TopP ?? 0.9,
                    max_tokens = request.MaxTokens ?? 2048
                }
            };
            
            var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            // Create client for this instance
            using var httpClient = _loadBalancer.CreateClientForInstance(instance);
            
            // Make request
            var response = await httpClient.PostAsync("/api/generate", content);
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GenerateResponse>(responseJson, _jsonOptions);
            
            if (result?.Response == null)
            {
                throw new InvalidOperationException("Received null or invalid response from Ollama");
            }
            
            stopwatch.Stop();
            await _loadBalancer.ReportSuccessAsync(instance, stopwatch.ElapsedMilliseconds);
            
            UpdateMetric("LoadBalancedOllama.SuccessfulGenerations", 1);
            UpdateMetric($"LoadBalancedOllama.GPU{instance.GpuId}.ResponseTime", stopwatch.ElapsedMilliseconds);
            LogInfo($"Generated response in {stopwatch.ElapsedMilliseconds}ms on {instance.Name}");
            
            LogMethodExit();
            return result.Response;
        }
        catch (HttpRequestException ex)
        {
            UpdateMetric("LoadBalancedOllama.NetworkErrors", 1);
            LogError($"Network error while generating response on {instance?.Name}", ex);
            
            if (instance != null)
                await _loadBalancer.ReportFailureAsync(instance, ex);
            
            LogMethodExit();
            throw new OllamaClientException("Failed to communicate with Ollama service", ex);
        }
        catch (TaskCanceledException ex)
        {
            UpdateMetric("LoadBalancedOllama.Timeouts", 1);
            LogError($"Request timeout on {instance?.Name}", ex);
            
            if (instance != null)
                await _loadBalancer.ReportFailureAsync(instance, ex);
            
            LogMethodExit();
            throw new OllamaClientException("Request to Ollama service timed out", ex);
        }
        catch (Exception ex)
        {
            UpdateMetric("LoadBalancedOllama.UnexpectedErrors", 1);
            LogError($"Unexpected error on {instance?.Name}", ex);
            
            if (instance != null)
                await _loadBalancer.ReportFailureAsync(instance, ex);
            
            LogMethodExit();
            throw;
        }
    }

    public async Task<bool> IsModelAvailableAsync(string modelName)
    {
        LogMethodEntry();
        
        try
        {
            // Check if model is available on any healthy instance
            var instances = await _loadBalancer.GetInstanceStatusAsync();
            
            foreach (var instance in instances.Where(i => i.IsHealthy))
            {
                if (await CheckModelOnInstanceAsync(modelName, instance))
                {
                    LogMethodExit();
                    return true;
                }
            }
            
            LogMethodExit();
            return false;
        }
        catch (Exception ex)
        {
            LogError($"Error checking model availability: {modelName}", ex);
            LogMethodExit();
            throw;
        }
    }

    public async Task<List<ModelInfo>> ListModelsAsync()
    {
        LogMethodEntry();
        
        try
        {
            // Get models from first healthy instance
            var instance = await _loadBalancer.GetNextInstanceAsync();
            using var httpClient = _loadBalancer.CreateClientForInstance(instance);
            
            var response = await httpClient.GetAsync("/api/tags");
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ModelsResponse>(json, _jsonOptions);
            
            LogInfo($"Found {result?.Models?.Count ?? 0} available models on {instance.Name}");
            LogMethodExit();
            return result?.Models ?? new List<ModelInfo>();
        }
        catch (Exception ex)
        {
            LogError("Failed to list models", ex);
            LogMethodExit();
            throw new OllamaClientException("Failed to retrieve model list", ex);
        }
    }

    public async Task EnsureModelAvailableAsync(string modelName)
    {
        await EnsureModelAvailableAsync(modelName, null);
    }

    private async Task EnsureModelAvailableAsync(string modelName, OllamaInstance? preferredInstance)
    {
        LogMethodEntry();
        
        try
        {
            var instance = preferredInstance ?? await _loadBalancer.GetNextInstanceAsync();
            
            if (!await CheckModelOnInstanceAsync(modelName, instance))
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
            throw new OllamaClientException($"Failed to ensure model {modelName} is available", ex);
        }
    }

    private async Task<bool> CheckModelOnInstanceAsync(string modelName, OllamaInstance instance)
    {
        LogMethodEntry();
        
        try
        {
            using var httpClient = _loadBalancer.CreateClientForInstance(instance);
            var models = await ListModelsOnInstanceAsync(httpClient);
            
            var isAvailable = models.Exists(m => 
                m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
                m.Name.StartsWith($"{modelName}:", StringComparison.OrdinalIgnoreCase));
            
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

    private async Task<List<ModelInfo>> ListModelsOnInstanceAsync(HttpClient httpClient)
    {
        LogMethodEntry();
        
        try
        {
            var response = await httpClient.GetAsync("/api/tags");
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ModelsResponse>(json, _jsonOptions);
            
            LogMethodExit();
            return result?.Models ?? new List<ModelInfo>();
        }
        catch (Exception ex)
        {
            LogError("Failed to list models on instance", ex);
            LogMethodExit();
            throw;
        }
    }

    private class GenerateResponse
    {
        public string? Response { get; set; }
    }

    private class ModelsResponse
    {
        public List<ModelInfo>? Models { get; set; }
    }
}