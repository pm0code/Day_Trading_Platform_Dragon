using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AIRES.Core.Health;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using System.Collections.Immutable;

namespace AIRES.Infrastructure.AI;

/// <summary>
/// Client for comprehensive Ollama service and model health diagnostics.
/// </summary>
public class OllamaHealthCheckClient : AIRESServiceBase
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly Dictionary<string, HealthCheckResult> _healthCache = new();
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromSeconds(30);
    private readonly object _cacheLock = new();

    public OllamaHealthCheckClient(IAIRESLogger logger, HttpClient httpClient, string baseUrl)
        : base(logger, nameof(OllamaHealthCheckClient))
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(5); // Short timeout for health checks
    }

    /// <summary>
    /// Performs comprehensive health check of Ollama service with detailed diagnostics.
    /// </summary>
    public async Task<HealthCheckResult> CheckServiceHealthAsync()
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        var diagnostics = new Dictionary<string, object>
        {
            ["BaseUrl"] = _baseUrl,
            ["CheckStartTime"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")
        };
        
        try
        {
            var response = await _httpClient.GetAsync("/");
            stopwatch.Stop();
            
            diagnostics["HttpStatusCode"] = (int)response.StatusCode;
            diagnostics["HttpStatusDescription"] = response.StatusCode.ToString();
            diagnostics["ResponseHeaders"] = response.Headers.ToString();
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                diagnostics["ResponseContent"] = content;
                
                if (content.Contains("Ollama is running", StringComparison.OrdinalIgnoreCase))
                {
                    LogInfo($"Ollama service is healthy. Response time: {stopwatch.ElapsedMilliseconds}ms");
                    LogMethodExit();
                    return HealthCheckResult.Healthy(
                        "Ollama Service",
                        "AI Service",
                        stopwatch.ElapsedMilliseconds,
                        diagnostics.ToImmutableDictionary());
                }
                else
                {
                    var reasons = ImmutableList.Create(
                        $"Unexpected response content: {content.Substring(0, Math.Min(content.Length, 100))}");
                    
                    LogWarning($"Ollama service returned unexpected content");
                    LogMethodExit();
                    return HealthCheckResult.Degraded(
                        "Ollama Service",
                        "AI Service",
                        stopwatch.ElapsedMilliseconds,
                        reasons,
                        diagnostics.ToImmutableDictionary());
                }
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                diagnostics["ErrorContent"] = content;
                
                var reasons = ImmutableList.Create(
                    $"HTTP {response.StatusCode}: {response.ReasonPhrase}",
                    $"Response: {content.Substring(0, Math.Min(content.Length, 200))}");
                
                LogError($"Ollama service unhealthy: {response.StatusCode}");
                LogMethodExit();
                return HealthCheckResult.Unhealthy(
                    "Ollama Service",
                    "AI Service",
                    stopwatch.ElapsedMilliseconds,
                    $"Service returned {response.StatusCode}",
                    null,
                    reasons,
                    diagnostics.ToImmutableDictionary());
            }
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            diagnostics["ExceptionType"] = ex.GetType().FullName ?? "Unknown";
            diagnostics["ExceptionMessage"] = ex.Message;
            diagnostics["InnerException"] = ex.InnerException?.Message ?? "None";
            
            var reasons = ImmutableList.Create(
                "Unable to connect to Ollama service",
                $"Connection error: {ex.Message}");
            
            if (ex.InnerException != null)
            {
                reasons = reasons.Add($"Inner exception: {ex.InnerException.Message}");
            }
            
            LogError($"Ollama service unreachable: {ex.Message}", ex);
            LogMethodExit();
            return HealthCheckResult.Unhealthy(
                "Ollama Service",
                "AI Service",
                stopwatch.ElapsedMilliseconds,
                "Service unreachable - connection failed",
                ex,
                reasons,
                diagnostics.ToImmutableDictionary());
        }
        catch (TaskCanceledException tcEx)
        {
            stopwatch.Stop();
            diagnostics["TimeoutAfterMs"] = _httpClient.Timeout.TotalMilliseconds;
            diagnostics["ActualElapsedMs"] = stopwatch.ElapsedMilliseconds;
            
            var reasons = ImmutableList.Create(
                $"Request timed out after {_httpClient.Timeout.TotalSeconds} seconds",
                "Service may be overloaded or unresponsive");
            
            LogError("Ollama service health check timed out");
            LogMethodExit();
            return HealthCheckResult.Unhealthy(
                "Ollama Service",
                "AI Service",
                stopwatch.ElapsedMilliseconds,
                "Health check timed out - service unresponsive",
                tcEx,
                reasons,
                diagnostics.ToImmutableDictionary());
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            diagnostics["UnexpectedExceptionType"] = ex.GetType().FullName ?? "Unknown";
            diagnostics["UnexpectedExceptionMessage"] = ex.Message;
            diagnostics["StackTrace"] = ex.StackTrace ?? "No stack trace";
            
            LogError("Unexpected error during Ollama service health check", ex);
            LogMethodExit();
            return HealthCheckResult.Unhealthy(
                "Ollama Service",
                "AI Service",
                stopwatch.ElapsedMilliseconds,
                $"Unexpected error: {ex.Message}",
                ex,
                ImmutableList.Create("Unexpected system error during health check"),
                diagnostics.ToImmutableDictionary());
        }
    }

    /// <summary>
    /// Performs comprehensive health check of a specific Ollama model with detailed diagnostics.
    /// </summary>
    public async Task<HealthCheckResult> CheckModelHealthAsync(string modelName)
    {
        LogMethodEntry();
        
        if (string.IsNullOrWhiteSpace(modelName))
        {
            LogWarning("Model name is null or empty");
            LogMethodExit();
            return HealthCheckResult.Unhealthy(
                "Unknown Model",
                "AI Model",
                0,
                "Model name is null or empty",
                new ArgumentException("Model name cannot be null or empty", nameof(modelName)),
                ImmutableList.Create("Invalid model name provided"),
                ImmutableDictionary<string, object>.Empty.Add("ProvidedModelName", modelName ?? "null"));
        }

        // Check cache first
        lock (_cacheLock)
        {
            if (_healthCache.TryGetValue(modelName, out var cached))
            {
                if (DateTime.UtcNow - cached.CheckedAt < _cacheExpiration)
                {
                    LogDebug($"Using cached health status for model {modelName}");
                    LogMethodExit();
                    return cached;
                }
            }
        }

        var stopwatch = Stopwatch.StartNew();
        var diagnostics = new Dictionary<string, object>
        {
            ["ModelName"] = modelName,
            ["CheckStartTime"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            ["BaseUrl"] = _baseUrl
        };

        try
        {
            var request = new { name = modelName };
            var response = await _httpClient.PostAsJsonAsync("/api/show", request);
            stopwatch.Stop();
            
            diagnostics["HttpStatusCode"] = (int)response.StatusCode;
            diagnostics["HttpStatusDescription"] = response.StatusCode.ToString();
            diagnostics["RequestDuration"] = stopwatch.ElapsedMilliseconds;
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var modelInfo = JsonDocument.Parse(content);
                
                // Extract model information
                if (modelInfo.RootElement.TryGetProperty("modelfile", out var modelfile))
                {
                    diagnostics["Modelfile"] = modelfile.GetString() ?? "Unknown";
                }
                if (modelInfo.RootElement.TryGetProperty("parameters", out var parameters))
                {
                    diagnostics["Parameters"] = parameters.GetString() ?? "None";
                }
                if (modelInfo.RootElement.TryGetProperty("template", out var template))
                {
                    diagnostics["Template"] = template.GetString() ?? "None";
                }
                if (modelInfo.RootElement.TryGetProperty("details", out var details))
                {
                    if (details.TryGetProperty("parameter_size", out var paramSize))
                    {
                        diagnostics["ParameterSize"] = paramSize.GetString() ?? "Unknown";
                    }
                    if (details.TryGetProperty("quantization_level", out var quantLevel))
                    {
                        diagnostics["QuantizationLevel"] = quantLevel.GetString() ?? "Unknown";
                    }
                }
                
                var result = HealthCheckResult.Healthy(
                    modelName,
                    "AI Model",
                    stopwatch.ElapsedMilliseconds,
                    diagnostics.ToImmutableDictionary());
                
                // Update cache
                lock (_cacheLock)
                {
                    _healthCache[modelName] = result;
                }
                
                LogInfo($"Model {modelName} is healthy. Response time: {stopwatch.ElapsedMilliseconds}ms");
                LogMethodExit();
                return result;
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                var content = await response.Content.ReadAsStringAsync();
                diagnostics["ErrorResponse"] = content;
                
                var reasons = ImmutableList.Create(
                    $"Model '{modelName}' not found in Ollama",
                    "Model may need to be pulled or is incorrectly named",
                    $"Server response: {content}");
                
                var result = HealthCheckResult.Unhealthy(
                    modelName,
                    "AI Model",
                    stopwatch.ElapsedMilliseconds,
                    $"Model not found: {modelName}",
                    null,
                    reasons,
                    diagnostics.ToImmutableDictionary());
                
                // Update cache
                lock (_cacheLock)
                {
                    _healthCache[modelName] = result;
                }
                
                LogError($"Model {modelName} not found in Ollama");
                LogMethodExit();
                return result;
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                diagnostics["ErrorContent"] = content;
                
                var reasons = ImmutableList.Create(
                    $"Unexpected response: HTTP {response.StatusCode}",
                    $"Response content: {content.Substring(0, Math.Min(content.Length, 200))}");
                
                var result = HealthCheckResult.Unhealthy(
                    modelName,
                    "AI Model",
                    stopwatch.ElapsedMilliseconds,
                    $"Model check failed with {response.StatusCode}",
                    null,
                    reasons,
                    diagnostics.ToImmutableDictionary());
                
                LogError($"Model {modelName} health check failed: {response.StatusCode}");
                LogMethodExit();
                return result;
            }
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            diagnostics["ExceptionType"] = ex.GetType().FullName ?? "Unknown";
            diagnostics["ExceptionMessage"] = ex.Message;
            
            var reasons = ImmutableList.Create(
                $"Failed to connect to Ollama for model check",
                $"Connection error: {ex.Message}",
                "Ollama service may be down");
            
            var result = HealthCheckResult.Unhealthy(
                modelName,
                "AI Model",
                stopwatch.ElapsedMilliseconds,
                "Cannot verify model - connection failed",
                ex,
                reasons,
                diagnostics.ToImmutableDictionary());
            
            LogError($"Failed to check model {modelName} health", ex);
            LogMethodExit();
            return result;
        }
        catch (TaskCanceledException tcEx)
        {
            stopwatch.Stop();
            diagnostics["TimeoutAfterMs"] = _httpClient.Timeout.TotalMilliseconds;
            
            var reasons = ImmutableList.Create(
                $"Model check timed out after {_httpClient.Timeout.TotalSeconds} seconds",
                "Ollama service may be overloaded",
                "Model may be too large or corrupted");
            
            var result = HealthCheckResult.Unhealthy(
                modelName,
                "AI Model",
                stopwatch.ElapsedMilliseconds,
                "Model health check timed out",
                tcEx,
                reasons,
                diagnostics.ToImmutableDictionary());
            
            LogError($"Model {modelName} health check timed out");
            LogMethodExit();
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            diagnostics["UnexpectedError"] = ex.ToString();
            
            var result = HealthCheckResult.Unhealthy(
                modelName,
                "AI Model",
                stopwatch.ElapsedMilliseconds,
                $"Unexpected error: {ex.Message}",
                ex,
                ImmutableList.Create("System error during model health check"),
                diagnostics.ToImmutableDictionary());
            
            LogError($"Unexpected error checking model {modelName} health", ex);
            LogMethodExit();
            return result;
        }
    }

    /// <summary>
    /// Gets a list of all available models.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetAvailableModelsAsync()
    {
        LogMethodEntry();
        
        try
        {
            var response = await _httpClient.GetAsync("/api/list");
            if (!response.IsSuccessStatusCode)
            {
                LogWarning($"Failed to get model list: {response.StatusCode}");
                LogMethodExit();
                return Array.Empty<string>();
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            
            var models = new List<string>();
            if (doc.RootElement.TryGetProperty("models", out var modelsArray))
            {
                foreach (var model in modelsArray.EnumerateArray())
                {
                    if (model.TryGetProperty("name", out var nameElement))
                    {
                        var name = nameElement.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            models.Add(name);
                        }
                    }
                }
            }
            
            LogInfo($"Found {models.Count} available models");
            LogMethodExit();
            return models;
        }
        catch (Exception ex)
        {
            LogError("Failed to get available models", ex);
            LogMethodExit();
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Clears the health check cache.
    /// </summary>
    public void ClearCache()
    {
        LogMethodEntry();
        lock (_cacheLock)
        {
            _healthCache.Clear();
        }
        LogDebug("Health check cache cleared");
        LogMethodExit();
    }
}