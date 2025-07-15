using System.Net.Http;
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
/// Mistral AI implementation for fetching and analyzing Microsoft documentation.
/// </summary>
public class MistralDocumentationService : AIRESServiceBase, IErrorDocumentationAIModel
{
    private readonly IOllamaClient _ollamaClient;
    private readonly HttpClient _httpClient;
    private readonly OllamaHealthCheckClient _healthCheckClient;
    private readonly TimeSpan _healthCheckCacheDuration = TimeSpan.FromMinutes(5);
    private const string MODEL_NAME = "mistral:7b-instruct-q4_K_M";
    private DateTime _lastHealthCheck = DateTime.MinValue;
    private HealthCheckResult? _cachedHealthResult;

    public MistralDocumentationService(
        IAIRESLogger logger,
        IOllamaClient ollamaClient,
        HttpClient httpClient,
        OllamaHealthCheckClient healthCheckClient)
        : base(logger, nameof(MistralDocumentationService))
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _healthCheckClient = healthCheckClient ?? throw new ArgumentNullException(nameof(healthCheckClient));
    }

    public async Task<ErrorDocumentationFinding> AnalyzeAsync(CompilerError error, string relevantCodeSnippet)
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
            if (string.IsNullOrWhiteSpace(relevantCodeSnippet))
            {
                throw new ArgumentException("Code snippet cannot be null or empty", nameof(relevantCodeSnippet));
            }
            
            // Perform health check before operation
            var healthCheck = await GetCachedHealthCheckAsync();
            if (healthCheck.Status != HealthStatus.Healthy)
            {
                LogWarning($"Mistral service health check failed: {healthCheck.Status}");
                LogWarning($"Health details: {healthCheck.GetDetailedReport()}");
                
                // Return degraded response with health information
                LogMethodExit();
                return new ErrorDocumentationFinding(
                    "Mistral",
                    $"Documentation Analysis for {error.Code}",
                    $"Service unhealthy ({healthCheck.Status}): {healthCheck.ErrorMessage ?? "Unknown error"}. Please check Ollama service and Mistral model availability.",
                    ""
                );
            }
            
            UpdateMetric("Mistral.Requests", 1);
            
            // First, fetch Microsoft documentation
            var msDocsUrl = $"https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/{error.Code.ToLower()}";
            var msDocsContent = await FetchMicrosoftDocsAsync(msDocsUrl).ConfigureAwait(false);

            // Build prompt for Mistral
            var prompt = BuildPrompt(error, msDocsContent, relevantCodeSnippet);

            // Call Mistral via Ollama
            var request = new OllamaRequest
            {
                Model = MODEL_NAME,
                Prompt = prompt,
                System = "You are a C# compiler error documentation expert. Analyze Microsoft documentation and provide clear, structured findings.",
                Temperature = 0.1,
                MaxTokens = 2000,
                TimeoutSeconds = 90 // 7B model typically needs 60-90s
            };

            var result = await _ollamaClient.GenerateAsync(request).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                UpdateMetric("Mistral.Failures", 1);
                LogError($"Failed to generate documentation analysis: {result.ErrorMessage}");
                LogMethodExit();
                return new ErrorDocumentationFinding(
                    "Mistral",
                    $"Documentation Analysis for {error.Code}",
                    $"Failed to analyze documentation: {result.ErrorMessage}",
                    msDocsUrl
                );
            }

            var response = result.Value!.Response;
            
            stopwatch.Stop();
            UpdateMetric("Mistral.ResponseTime", stopwatch.ElapsedMilliseconds);
            UpdateMetric("Mistral.Successes", 1);
            
            LogInfo($"Successfully generated documentation analysis for {error.Code} in {stopwatch.ElapsedMilliseconds}ms");
            LogMethodExit();
            
            return new ErrorDocumentationFinding(
                "Mistral",
                $"Documentation Analysis for {error.Code}",
                response,
                msDocsUrl
            );
        }
        catch (ArgumentNullException ex)
        {
            UpdateMetric("Mistral.ValidationErrors", 1);
            LogError("Invalid input parameters", ex);
            LogMethodExit();
            throw;
        }
        catch (ArgumentException ex)
        {
            UpdateMetric("Mistral.ValidationErrors", 1);
            LogError("Invalid input parameters", ex);
            LogMethodExit();
            throw;
        }
        catch (HttpRequestException ex)
        {
            UpdateMetric("Mistral.HttpErrors", 1);
            LogError($"HTTP error during documentation analysis for {error.Code}", ex);
            LogMethodExit();
            
            return new ErrorDocumentationFinding(
                "Mistral",
                $"Documentation Analysis for {error.Code}",
                $"Network error during analysis: {ex.Message}",
                ""
            );
        }
        catch (TaskCanceledException ex)
        {
            UpdateMetric("Mistral.Timeouts", 1);
            LogError($"Timeout during documentation analysis for {error.Code}", ex);
            LogMethodExit();
            
            return new ErrorDocumentationFinding(
                "Mistral",
                $"Documentation Analysis for {error.Code}",
                $"Analysis timed out. The operation took too long to complete.",
                ""
            );
        }
        catch (Exception ex)
        {
            UpdateMetric("Mistral.UnexpectedErrors", 1);
            LogError($"Unexpected error during documentation analysis for {error.Code}", ex);
            LogMethodExit();
            
            return new ErrorDocumentationFinding(
                "Mistral",
                $"Documentation Analysis for {error.Code}",
                $"Unexpected error during analysis: {ex.Message}",
                ""
            );
        }
    }

    private async Task<string> FetchMicrosoftDocsAsync(string url)
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Ensure User-Agent is set (only add if not already present)
            if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AIRES/1.0");
            }
            
            var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
            
            stopwatch.Stop();
            UpdateMetric("Mistral.HttpResponseTime", stopwatch.ElapsedMilliseconds);
            
            if (!response.IsSuccessStatusCode)
            {
                UpdateMetric("Mistral.HttpErrors", 1);
                LogWarning($"Failed to fetch Microsoft docs from {url}: {response.StatusCode}");
                LogMethodExit();
                return "Microsoft documentation not available.";
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            
            // Simple extraction of main content (in real implementation, use HTML parser)
            var startIndex = content.IndexOf("<article", StringComparison.OrdinalIgnoreCase);
            var endIndex = content.IndexOf("</article>", StringComparison.OrdinalIgnoreCase);
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                content = content.Substring(startIndex, endIndex - startIndex + 10);
                // Strip HTML tags (simplified)
                content = System.Text.RegularExpressions.Regex.Replace(content, "<.*?>", " ");
                content = System.Net.WebUtility.HtmlDecode(content);
            }
            
            UpdateMetric("Mistral.HttpSuccesses", 1);
            LogInfo($"Successfully fetched Microsoft docs in {stopwatch.ElapsedMilliseconds}ms");
            LogMethodExit();
            return content.Length > 5000 ? content.Substring(0, 5000) : content;
        }
        catch (HttpRequestException ex)
        {
            UpdateMetric("Mistral.HttpErrors", 1);
            LogError($"HTTP error fetching Microsoft docs from {url}", ex);
            LogMethodExit();
            return "Error fetching Microsoft documentation - network issue.";
        }
        catch (TaskCanceledException ex)
        {
            UpdateMetric("Mistral.HttpTimeouts", 1);
            LogError($"Timeout fetching Microsoft docs from {url}", ex);
            LogMethodExit();
            return "Error fetching Microsoft documentation - timeout.";
        }
        catch (Exception ex)
        {
            UpdateMetric("Mistral.HttpErrors", 1);
            LogError($"Error fetching Microsoft docs from {url}", ex);
            LogMethodExit();
            return "Error fetching Microsoft documentation.";
        }
    }

    private string BuildPrompt(CompilerError error, string msDocsContent, string codeSnippet)
    {
        return $@"Analyze the following C# compiler error and provide detailed documentation findings:

Error Code: {error.Code}
Error Message: {error.Message}
Error Location: {error.Location}

Relevant Code Snippet:
```csharp
{codeSnippet}
```

Microsoft Documentation Content:
{msDocsContent}

Please provide:
1. Root Cause Explanation: What exactly causes this error?
2. Common Scenarios: When does this error typically occur?
3. Resolution Steps: Clear, actionable steps to fix the error
4. Best Practices: How to avoid this error in the future
5. Related Errors: Other errors that might be connected

Format your response as a structured technical document.";
    }
    
    /// <summary>
    /// Performs a comprehensive health check of the Mistral documentation service.
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
            
            // Check Mistral model availability
            var modelHealth = await _healthCheckClient.CheckModelHealthAsync(MODEL_NAME);
            if (modelHealth.Status != HealthStatus.Healthy)
            {
                LogWarning($"Mistral model {MODEL_NAME} is not healthy");
                LogMethodExit();
                return modelHealth;
            }
            
            // Check HTTP client health (Microsoft Docs accessibility)
            var httpHealthCheck = await CheckHttpHealthAsync();
            if (httpHealthCheck.Status != HealthStatus.Healthy)
            {
                LogWarning("HTTP client health check failed");
                LogMethodExit();
                return httpHealthCheck;
            }
            
            stopwatch.Stop();
            var diagnostics = new Dictionary<string, object>
            {
                ["ServiceName"] = nameof(MistralDocumentationService),
                ["ModelName"] = MODEL_NAME,
                ["OllamaServiceStatus"] = serviceHealth.Status.ToString(),
                ["ModelStatus"] = modelHealth.Status.ToString(),
                ["HttpClientStatus"] = httpHealthCheck.Status.ToString(),
                ["TotalRequests"] = GetMetricValue("Mistral.Requests"),
                ["SuccessfulRequests"] = GetMetricValue("Mistral.Successes"),
                ["FailedRequests"] = GetMetricValue("Mistral.Failures"),
                ["AverageResponseTime"] = GetAverageResponseTime(),
                ["LastCheckTime"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
            
            LogInfo($"Mistral documentation service health check passed in {stopwatch.ElapsedMilliseconds}ms");
            LogMethodExit();
            
            return HealthCheckResult.Healthy(
                nameof(MistralDocumentationService),
                "AI Documentation Service",
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
                nameof(MistralDocumentationService),
                "AI Documentation Service",
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
    
    private async Task<HealthCheckResult> CheckHttpHealthAsync()
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Test connectivity to Microsoft Docs
            var testUrl = "https://learn.microsoft.com/en-us/dotnet/csharp/";
            var response = await _httpClient.GetAsync(testUrl);
            
            stopwatch.Stop();
            
            if (response.IsSuccessStatusCode)
            {
                LogDebug($"HTTP health check passed in {stopwatch.ElapsedMilliseconds}ms");
                LogMethodExit();
                
                return HealthCheckResult.Healthy(
                    "HTTP Client",
                    "Network Service",
                    stopwatch.ElapsedMilliseconds,
                    ImmutableDictionary<string, object>.Empty
                        .Add("TestUrl", testUrl)
                        .Add("StatusCode", (int)response.StatusCode)
                );
            }
            
            LogWarning($"HTTP health check failed with status {response.StatusCode}");
            LogMethodExit();
            
            return HealthCheckResult.Unhealthy(
                "HTTP Client",
                "Network Service",
                stopwatch.ElapsedMilliseconds,
                $"Microsoft Docs returned {response.StatusCode}",
                null,
                ImmutableList.Create($"HTTP status: {response.StatusCode}")
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogError("HTTP health check failed", ex);
            LogMethodExit();
            
            return HealthCheckResult.Unhealthy(
                "HTTP Client",
                "Network Service",
                stopwatch.ElapsedMilliseconds,
                $"Network error: {ex.Message}",
                ex,
                ImmutableList.Create("Unable to reach Microsoft Docs")
            );
        }
    }
    
    private double GetAverageResponseTime()
    {
        var totalTime = GetMetricValue("Mistral.ResponseTime");
        var successCount = GetMetricValue("Mistral.Successes");
        
        return successCount > 0 ? totalTime / successCount : 0;
    }
    
    private double GetMetricValue(string metricName)
    {
        var metrics = GetMetrics();
        var value = metrics.GetValueOrDefault(metricName);
        return value != null ? Convert.ToDouble(value) : 0;
    }
}