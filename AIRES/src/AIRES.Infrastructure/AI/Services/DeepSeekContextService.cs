using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using AIRES.Core.Domain.Interfaces;
using AIRES.Core.Domain.ValueObjects;
using AIRES.Core.Health;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;
using AIRES.Infrastructure.AI.Clients;
using System.Collections.Immutable;
using System.Diagnostics;

namespace AIRES.Infrastructure.AI.Services;

/// <summary>
/// DeepSeek AI implementation for analyzing code context around errors.
/// </summary>
public class DeepSeekContextService : AIRESServiceBase, IContextAnalyzerAIModel
{
    private readonly IOllamaClient _ollamaClient;
    private readonly OllamaHealthCheckClient _healthCheckClient;
    private readonly TimeSpan _healthCheckCacheDuration = TimeSpan.FromMinutes(5);
    private const string MODEL_NAME = "deepseek-coder:6.7b";
    private DateTime _lastHealthCheck = DateTime.MinValue;
    private HealthCheckResult? _cachedHealthResult;

    public DeepSeekContextService(
        IAIRESLogger logger,
        IOllamaClient ollamaClient,
        OllamaHealthCheckClient healthCheckClient)
        : base(logger, nameof(DeepSeekContextService))
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _healthCheckClient = healthCheckClient ?? throw new ArgumentNullException(nameof(healthCheckClient));
    }

    public async Task<ContextAnalysisFinding> AnalyzeAsync(
        CompilerError error, 
        string surroundingCode, 
        string projectStructureXml)
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Validate input parameters
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }
            if (string.IsNullOrWhiteSpace(surroundingCode))
            {
                throw new ArgumentException("Surrounding code cannot be null or empty", nameof(surroundingCode));
            }
            if (string.IsNullOrWhiteSpace(projectStructureXml))
            {
                throw new ArgumentException("Project structure XML cannot be null or empty", nameof(projectStructureXml));
            }
            
            // Perform health check before operation
            var healthCheck = await GetCachedHealthCheckAsync();
            if (healthCheck.Status != HealthStatus.Healthy)
            {
                LogWarning($"DeepSeek service health check failed: {healthCheck.Status}");
                LogWarning($"Health details: {healthCheck.GetDetailedReport()}");
                
                // Return degraded response with health information
                LogMethodExit();
                return new ContextAnalysisFinding(
                    "DeepSeek",
                    $"Context Analysis for {error.Code}",
                    $"Service unhealthy ({healthCheck.Status}): {healthCheck.ErrorMessage ?? "Unknown error"}. Please check Ollama service and DeepSeek model availability.",
                    surroundingCode,
                    projectStructureXml
                );
            }
            
            UpdateMetric("DeepSeek.Requests", 1);
            var prompt = BuildPrompt(error, surroundingCode, projectStructureXml);

            var request = new OllamaRequest
            {
                Model = MODEL_NAME,
                Prompt = prompt,
                System = "You are DeepSeek Coder, an expert at analyzing C# code context and understanding complex code relationships. Provide detailed technical analysis.",
                Temperature = 0.2,
                MaxTokens = 3000,
                TimeoutSeconds = 120 // DeepSeek needs more time for context analysis
            };

            var result = await _ollamaClient.GenerateAsync(request).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                UpdateMetric("DeepSeek.Failures", 1);
                LogError($"Failed to generate context analysis: {result.ErrorMessage}");
                LogMethodExit();
                return new ContextAnalysisFinding(
                    "DeepSeek",
                    $"Context Analysis for {error.Code}",
                    $"Failed to analyze context: {result.ErrorMessage}",
                    surroundingCode,
                    projectStructureXml
                );
            }

            var response = result.Value!.Response;
            
            stopwatch.Stop();
            UpdateMetric("DeepSeek.ResponseTime", stopwatch.ElapsedMilliseconds);
            UpdateMetric("DeepSeek.Successes", 1);
            
            LogInfo($"Successfully generated context analysis for {error.Code} in {stopwatch.ElapsedMilliseconds}ms");
            LogMethodExit();
            
            return new ContextAnalysisFinding(
                "DeepSeek",
                $"Context Analysis for {error.Code} at {error.Location}",
                response,
                ExtractRelevantCodeSnippet(surroundingCode, error.Location),
                projectStructureXml
            );
        }
        catch (ArgumentNullException ex)
        {
            UpdateMetric("DeepSeek.ValidationErrors", 1);
            LogError("Invalid input parameters", ex);
            LogMethodExit();
            throw;
        }
        catch (ArgumentException ex)
        {
            UpdateMetric("DeepSeek.ValidationErrors", 1);
            LogError("Invalid input parameters", ex);
            LogMethodExit();
            throw;
        }
        catch (TaskCanceledException ex)
        {
            UpdateMetric("DeepSeek.Timeouts", 1);
            LogError($"Timeout during context analysis for {error.Code}", ex);
            LogMethodExit();
            
            return new ContextAnalysisFinding(
                "DeepSeek",
                $"Context Analysis for {error.Code}",
                $"Analysis timed out. The operation took too long to complete.",
                "",
                ""
            );
        }
        catch (Exception ex)
        {
            UpdateMetric("DeepSeek.UnexpectedErrors", 1);
            LogError($"Unexpected error during context analysis for {error.Code}", ex);
            LogMethodExit();
            
            return new ContextAnalysisFinding(
                "DeepSeek",
                $"Context Analysis for {error.Code}",
                $"Unexpected error during analysis: {ex.Message}",
                "",
                ""
            );
        }
    }

    private string BuildPrompt(CompilerError error, string surroundingCode, string projectStructureXml)
    {
        return $@"Analyze the code context around this C# compiler error:

Error Code: {error.Code}
Error Message: {error.Message}
Error Location: {error.Location}

Surrounding Code Context:
```csharp
{surroundingCode}
```

Project Structure:
```xml
{projectStructureXml}
```

Please provide:
1. Context Understanding: Explain what the code is trying to do
2. Error Context: Why this error occurs in this specific context
3. Dependencies: What other parts of the code are affected
4. Architectural Impact: How this error relates to the overall design
5. Refactoring Suggestions: How the code structure could be improved
6. Type System Analysis: Identify any type mismatches or conversions needed

Focus on the relationships between different parts of the code and how they contribute to the error.";
    }

    private string ExtractRelevantCodeSnippet(string surroundingCode, ErrorLocation location)
    {
        LogMethodEntry();
        
        try
        {
            var lines = surroundingCode.Split('\n');
            var lineNumber = location.LineNumber;
            
            // Extract ±5 lines around the error
            var startLine = Math.Max(0, lineNumber - 5);
            var endLine = Math.Min(lines.Length - 1, lineNumber + 5);
            
            var relevantLines = new System.Collections.Generic.List<string>();
            for (int i = startLine; i <= endLine; i++)
            {
                var prefix = i == lineNumber - 1 ? ">>> " : "    ";
                relevantLines.Add($"{prefix}{lines[i]}");
            }
            
            LogMethodExit();
            return string.Join('\n', relevantLines);
        }
        catch (Exception ex)
        {
            LogError("Error extracting relevant code snippet", ex);
            LogMethodExit();
            return surroundingCode;
        }
    }
    
    /// <summary>
    /// Performs a comprehensive health check of the DeepSeek context service.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync()
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Check Ollama service health
            var serviceHealth = await _healthCheckClient.CheckServiceHealthAsync();
            if (serviceHealth.Status != HealthStatus.Healthy)
            {
                LogWarning("Ollama service is not healthy");
                LogMethodExit();
                return serviceHealth;
            }
            
            // Check DeepSeek model availability
            var modelHealth = await _healthCheckClient.CheckModelHealthAsync(MODEL_NAME);
            if (modelHealth.Status != HealthStatus.Healthy)
            {
                LogWarning($"DeepSeek model {MODEL_NAME} is not healthy");
                LogMethodExit();
                return modelHealth;
            }
            
            stopwatch.Stop();
            var diagnostics = new Dictionary<string, object>
            {
                ["ServiceName"] = nameof(DeepSeekContextService),
                ["ModelName"] = MODEL_NAME,
                ["OllamaServiceStatus"] = serviceHealth.Status.ToString(),
                ["ModelStatus"] = modelHealth.Status.ToString(),
                ["TotalRequests"] = GetMetricValue("DeepSeek.Requests"),
                ["SuccessfulRequests"] = GetMetricValue("DeepSeek.Successes"),
                ["FailedRequests"] = GetMetricValue("DeepSeek.Failures"),
                ["AverageResponseTime"] = GetAverageResponseTime(),
                ["TimeoutCount"] = GetMetricValue("DeepSeek.Timeouts"),
                ["ValidationErrors"] = GetMetricValue("DeepSeek.ValidationErrors"),
                ["LastCheckTime"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
            
            LogInfo($"DeepSeek context service health check passed in {stopwatch.ElapsedMilliseconds}ms");
            LogMethodExit();
            
            return HealthCheckResult.Healthy(
                nameof(DeepSeekContextService),
                "AI Context Analysis Service",
                stopwatch.ElapsedMilliseconds,
                diagnostics.ToImmutableDictionary()
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogError("Error during health check", ex);
            LogMethodExit();
            
            return HealthCheckResult.Unhealthy(
                nameof(DeepSeekContextService),
                "AI Context Analysis Service",
                stopwatch.ElapsedMilliseconds,
                $"Health check failed: {ex.Message}",
                ex,
                ImmutableList.Create($"Exception during health check: {ex.GetType().Name}")
            );
        }
    }
    
    private async Task<HealthCheckResult> GetCachedHealthCheckAsync()
    {
        LogMethodEntry();
        
        // Check if cached result is still valid
        if (_cachedHealthResult != null && 
            DateTime.UtcNow - _lastHealthCheck < _healthCheckCacheDuration)
        {
            LogDebug("Using cached health check result");
            LogMethodExit();
            return _cachedHealthResult;
        }
        
        // Perform new health check
        _cachedHealthResult = await CheckHealthAsync();
        _lastHealthCheck = DateTime.UtcNow;
        
        LogMethodExit();
        return _cachedHealthResult;
    }
    
    private double GetAverageResponseTime()
    {
        var totalTime = GetMetricValue("DeepSeek.ResponseTime");
        var successCount = GetMetricValue("DeepSeek.Successes");
        
        return successCount > 0 ? totalTime / successCount : 0;
    }
    
    private double GetMetricValue(string metricName)
    {
        var metrics = GetMetrics();
        var value = metrics.GetValueOrDefault(metricName);
        return value != null ? Convert.ToDouble(value) : 0;
    }
}