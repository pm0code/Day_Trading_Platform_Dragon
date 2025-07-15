using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AIRES.Core.Domain.Interfaces;
using AIRES.Core.Domain.ValueObjects;
using AIRES.Core.Health;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Infrastructure.AI.Clients;

namespace AIRES.Infrastructure.AI.Services;

/// <summary>
/// CodeGemma AI implementation for validating code patterns against standards.
/// </summary>
public class CodeGemmaPatternService : AIRESServiceBase, IPatternValidatorAIModel
{
    private readonly IOllamaClient _ollamaClient;
    private readonly OllamaHealthCheckClient _healthCheckClient;
    private readonly TimeSpan _healthCheckCacheDuration = TimeSpan.FromMinutes(5);
    private const string MODEL_NAME = "codegemma:7b";
    private DateTime _lastHealthCheck = DateTime.MinValue;
    private HealthCheckResult? _cachedHealthResult;

    public CodeGemmaPatternService(
        IAIRESLogger logger,
        IOllamaClient ollamaClient,
        OllamaHealthCheckClient healthCheckClient)
        : base(logger, nameof(CodeGemmaPatternService))
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _healthCheckClient = healthCheckClient ?? throw new ArgumentNullException(nameof(healthCheckClient));
    }

    public async Task<PatternValidationFinding> AnalyzeBatchAsync(
        IEnumerable<CompilerError> errors, 
        string projectCodebase, 
        IImmutableList<string> projectStandardPatterns)
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Validate input parameters
            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }
            if (string.IsNullOrWhiteSpace(projectCodebase))
            {
                throw new ArgumentException("Project codebase cannot be null or empty", nameof(projectCodebase));
            }
            if (projectStandardPatterns == null || projectStandardPatterns.Count == 0)
            {
                throw new ArgumentException("Project standard patterns cannot be null or empty", nameof(projectStandardPatterns));
            }
            
            // Perform health check before operation
            var healthCheck = await GetCachedHealthCheckAsync();
            if (healthCheck.Status != HealthStatus.Healthy)
            {
                LogWarning($"CodeGemma service health check failed: {healthCheck.Status}");
                LogWarning($"Health details: {healthCheck.GetDetailedReport()}");
                
                // Return degraded response with health information
                LogMethodExit();
                return CreateFailureFinding(
                    errors.ToList(), 
                    $"Service unhealthy ({healthCheck.Status}): {healthCheck.ErrorMessage ?? "Unknown error"}. Please check Ollama service and CodeGemma model availability."
                );
            }
            
            UpdateMetric("CodeGemma.Requests", 1);
            var errorsList = errors.ToList();
            var prompt = BuildPrompt(errorsList, projectCodebase, projectStandardPatterns);

            var request = new OllamaRequest
            {
                Model = MODEL_NAME,
                Prompt = prompt,
                System = "You are CodeGemma, an expert at validating C# code patterns against development standards. Focus on canonical patterns, logging requirements, and architectural compliance.",
                Temperature = 0.1,
                MaxTokens = 4000,
                TimeoutSeconds = 150 // Pattern analysis needs more time
            };

            var result = await _ollamaClient.GenerateAsync(request).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                UpdateMetric("CodeGemma.Failures", 1);
                LogError($"Failed to generate pattern validation: {result.ErrorMessage}");
                LogMethodExit();
                return CreateFailureFinding(errorsList, result.ErrorMessage!);
            }

            var response = result.Value!.Response;
            var parsedFindings = ParsePatternFindings(response);
            
            stopwatch.Stop();
            UpdateMetric("CodeGemma.ResponseTime", stopwatch.ElapsedMilliseconds);
            UpdateMetric("CodeGemma.Successes", 1);
            UpdateMetric("CodeGemma.PatternsValidated", parsedFindings.IssuesIdentified.Count);
            
            LogInfo($"Successfully validated {parsedFindings.IssuesIdentified.Count} pattern issues in {stopwatch.ElapsedMilliseconds}ms");
            LogMethodExit();
            
            return parsedFindings;
        }
        catch (ArgumentNullException ex)
        {
            UpdateMetric("CodeGemma.ValidationErrors", 1);
            LogError("Invalid input parameters", ex);
            LogMethodExit();
            throw;
        }
        catch (ArgumentException ex)
        {
            UpdateMetric("CodeGemma.ValidationErrors", 1);
            LogError("Invalid input parameters", ex);
            LogMethodExit();
            throw;
        }
        catch (TaskCanceledException ex)
        {
            UpdateMetric("CodeGemma.Timeouts", 1);
            LogError("Timeout during pattern validation", ex);
            LogMethodExit();
            
            return CreateFailureFinding(errors.ToList(), "Pattern validation timed out. The operation took too long to complete.");
        }
        catch (Exception ex)
        {
            UpdateMetric("CodeGemma.UnexpectedErrors", 1);
            LogError("Unexpected error during pattern validation", ex);
            LogMethodExit();
            
            return CreateFailureFinding(errors.ToList(), ex.Message);
        }
    }

    private string BuildPrompt(List<CompilerError> errors, string projectCodebase, IImmutableList<string> standards)
    {
        var errorSummary = string.Join("\n", errors.Select(e => $"- {e.Code}: {e.Message} at {e.Location}"));
        var standardsList = string.Join("\n", standards.Select(s => $"- {s}"));

        return $@"Analyze the following compiler errors against the project's mandatory development standards:

Compiler Errors:
{errorSummary}

Project Codebase Overview:
{projectCodebase}

Mandatory Standards to Validate:
{standardsList}

Please analyze and provide:
1. Pattern Violations: List each violation of the mandatory standards
2. Canonical Pattern Compliance: Check for LogMethodEntry/Exit, AIRESResult usage, etc.
3. Logging Compliance: Verify all methods have proper entry/exit logging
4. Service Base Class Usage: Ensure services inherit from AIRESServiceBase
5. Error Handling Patterns: Validate proper try-catch-finally with logging
6. Naming Conventions: Check for SCREAMING_SNAKE_CASE error codes

For each issue found, provide:
- Issue Type (e.g., 'Missing LogMethodEntry', 'Incorrect Base Class')
- Description of the violation
- Suggested correction with code example
- Location if applicable

Also list any patterns that ARE compliant to acknowledge good practices.

Format your response as structured JSON for easy parsing.";
    }

    private PatternValidationFinding ParsePatternFindings(string response)
    {
        LogMethodEntry();
        
        try
        {
            // In a real implementation, parse JSON response
            // For now, create a structured finding from the text
            var issues = new List<PatternIssue>();
            var compliantPatterns = new List<string>();

            // Simple parsing logic - in production, use proper JSON parsing
            var lines = response.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("Issue:") || line.Contains("Violation:"))
                {
                    issues.Add(new PatternIssue(
                        "Pattern Violation",
                        line,
                        "See documentation for correct implementation",
                        null
                    ));
                }
                else if (line.Contains("Compliant:") || line.Contains("Good:"))
                {
                    compliantPatterns.Add(line);
                }
            }

            LogMethodExit();
            return new PatternValidationFinding(
                "CodeGemma",
                "Pattern Validation Report",
                response,
                issues,
                compliantPatterns
            );
        }
        catch (Exception ex)
        {
            UpdateMetric("CodeGemma.ParseErrors", 1);
            LogError("Error parsing pattern findings", ex);
            LogMethodExit();
            
            return new PatternValidationFinding(
                "CodeGemma",
                "Pattern Validation Report",
                response,
                new List<PatternIssue>(),
                new List<string>()
            );
        }
    }

    private PatternValidationFinding CreateFailureFinding(List<CompilerError> errors, string errorMessage)
    {
        return new PatternValidationFinding(
            "CodeGemma",
            "Pattern Validation Failed",
            $"Failed to validate patterns: {errorMessage}",
            new List<PatternIssue>
            {
                new PatternIssue(
                    "Validation Error",
                    errorMessage,
                    "Resolve the error and retry validation",
                    null
                )
            },
            new List<string>()
        );
    }
    
    /// <summary>
    /// Performs a comprehensive health check of the CodeGemma pattern service.
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
            
            // Check CodeGemma model availability
            var modelHealth = await _healthCheckClient.CheckModelHealthAsync(MODEL_NAME);
            if (modelHealth.Status != HealthStatus.Healthy)
            {
                LogWarning($"CodeGemma model {MODEL_NAME} is not healthy");
                LogMethodExit();
                return modelHealth;
            }
            
            stopwatch.Stop();
            var diagnostics = new Dictionary<string, object>
            {
                ["ServiceName"] = nameof(CodeGemmaPatternService),
                ["ModelName"] = MODEL_NAME,
                ["OllamaServiceStatus"] = serviceHealth.Status.ToString(),
                ["ModelStatus"] = modelHealth.Status.ToString(),
                ["TotalRequests"] = GetMetricValue("CodeGemma.Requests"),
                ["SuccessfulRequests"] = GetMetricValue("CodeGemma.Successes"),
                ["FailedRequests"] = GetMetricValue("CodeGemma.Failures"),
                ["AverageResponseTime"] = GetAverageResponseTime(),
                ["TimeoutCount"] = GetMetricValue("CodeGemma.Timeouts"),
                ["ValidationErrors"] = GetMetricValue("CodeGemma.ValidationErrors"),
                ["ParseErrors"] = GetMetricValue("CodeGemma.ParseErrors"),
                ["PatternsValidated"] = GetMetricValue("CodeGemma.PatternsValidated"),
                ["LastCheckTime"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
            
            LogInfo($"CodeGemma pattern service health check passed in {stopwatch.ElapsedMilliseconds}ms");
            LogMethodExit();
            
            return HealthCheckResult.Healthy(
                nameof(CodeGemmaPatternService),
                "AI Pattern Validation Service",
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
                nameof(CodeGemmaPatternService),
                "AI Pattern Validation Service",
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
        var totalTime = GetMetricValue("CodeGemma.ResponseTime");
        var successCount = GetMetricValue("CodeGemma.Successes");
        
        return successCount > 0 ? totalTime / successCount : 0;
    }
    
    private double GetMetricValue(string metricName)
    {
        var metrics = GetMetrics();
        var value = metrics.GetValueOrDefault(metricName);
        return value != null ? Convert.ToDouble(value) : 0;
    }
}