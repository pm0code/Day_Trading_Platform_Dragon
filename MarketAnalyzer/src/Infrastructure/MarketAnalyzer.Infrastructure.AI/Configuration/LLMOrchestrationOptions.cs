using MarketAnalyzer.Infrastructure.AI.Services;

namespace MarketAnalyzer.Infrastructure.AI.Configuration;

/// <summary>
/// Configuration options for LLM orchestration and routing between providers.
/// </summary>
public class LLMOrchestrationOptions
{
    /// <summary>
    /// Gets or sets whether to enable automatic fallback between providers.
    /// </summary>
    public bool EnableFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets the complexity threshold in characters.
    /// Prompts above this threshold are routed to cloud providers.
    /// </summary>
    public int ComplexityThreshold { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the complex token threshold.
    /// Requests above this token count are routed to cloud providers.
    /// </summary>
    public int ComplexTokenThreshold { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the latency threshold in milliseconds.
    /// Requests requiring response faster than this use local providers.
    /// </summary>
    public int LatencyThresholdMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets the cost threshold in USD.
    /// Requests above this cost are routed to local providers.
    /// </summary>
    public decimal CostThreshold { get; set; } = 0.01m;

    /// <summary>
    /// Gets or sets the maximum allowed consecutive failures before marking provider unhealthy.
    /// </summary>
    public int MaxConsecutiveFailures { get; set; } = 3;

    /// <summary>
    /// Gets or sets the health check interval in minutes.
    /// </summary>
    public int HealthCheckIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to enable cost optimization routing.
    /// </summary>
    public bool EnableCostOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable latency optimization routing.
    /// </summary>
    public bool EnableLatencyOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable privacy-aware routing.
    /// Sensitive data processing is routed to local providers.
    /// </summary>
    public bool EnablePrivacyRouting { get; set; } = true;

    /// <summary>
    /// Gets or sets the routing strategy.
    /// </summary>
    public RoutingStrategy Strategy { get; set; } = RoutingStrategy.Balanced;

    /// <summary>
    /// Gets or sets the default provider when routing is inconclusive.
    /// </summary>
    public string DefaultProvider { get; set; } = "Ollama";

    /// <summary>
    /// Gets the provider-specific routing rules.
    /// </summary>
    public Dictionary<string, ProviderRoutingRule> ProviderRules { get; init; } = new()
    {
        ["Ollama"] = new ProviderRoutingRule
        {
            PreferredPromptTypes = new[] 
            { 
                LLMPromptType.TradingSignal, 
                LLMPromptType.QuickSummary,
                LLMPromptType.DataExtraction,
                LLMPromptType.NewsSentiment,
                LLMPromptType.TechnicalIndicator
            },
            MaxComplexity = 2000,
            MaxCostPerRequest = 0.0m // Free local inference
        },
        ["Gemini"] = new ProviderRoutingRule
        {
            PreferredPromptTypes = new[] 
            { 
                LLMPromptType.MarketAnalysis, 
                LLMPromptType.RiskAssessment,
                LLMPromptType.ReportGeneration,
                LLMPromptType.CodeGeneration
            },
            MaxComplexity = int.MaxValue,
            MaxCostPerRequest = 0.10m
        }
    };

    /// <summary>
    /// Gets or sets load balancing configuration.
    /// </summary>
    public LoadBalancingConfig LoadBalancing { get; set; } = new();

    /// <summary>
    /// Gets or sets circuit breaker configuration.
    /// </summary>
    public CircuitBreakerConfig CircuitBreaker { get; set; } = new();
}

/// <summary>
/// Routing strategy for LLM requests.
/// </summary>
public enum RoutingStrategy
{
    /// <summary>
    /// Always use local provider when available.
    /// </summary>
    LocalFirst,

    /// <summary>
    /// Always use cloud provider when available.
    /// </summary>
    CloudFirst,

    /// <summary>
    /// Balance between cost, latency, and quality.
    /// </summary>
    Balanced,

    /// <summary>
    /// Optimize for lowest cost.
    /// </summary>
    CostOptimized,

    /// <summary>
    /// Optimize for lowest latency.
    /// </summary>
    LatencyOptimized,

    /// <summary>
    /// Round-robin between available providers.
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Route based on current load.
    /// </summary>
    LoadBalanced
}

/// <summary>
/// Provider-specific routing rules.
/// </summary>
public class ProviderRoutingRule
{
    /// <summary>
    /// Gets or sets the prompt types this provider is preferred for.
    /// </summary>
    public LLMPromptType[] PreferredPromptTypes { get; set; } = Array.Empty<LLMPromptType>();

    /// <summary>
    /// Gets or sets the maximum complexity this provider should handle.
    /// </summary>
    public int MaxComplexity { get; set; } = int.MaxValue;

    /// <summary>
    /// Gets or sets the maximum cost per request for this provider.
    /// </summary>
    public decimal MaxCostPerRequest { get; set; } = decimal.MaxValue;

    /// <summary>
    /// Gets or sets the priority weight for this provider (higher = more preferred).
    /// </summary>
    public int Priority { get; set; } = 5;

    /// <summary>
    /// Gets the time ranges when this provider is preferred.
    /// </summary>
    public List<TimeRange> PreferredTimeRanges { get; init; } = new();

    /// <summary>
    /// Gets or sets whether this provider should be used for sensitive data.
    /// </summary>
    public bool AllowSensitiveData { get; set; } = true;
}

/// <summary>
/// Time range for provider preferences.
/// </summary>
public class TimeRange
{
    /// <summary>
    /// Gets or sets the start time (24-hour format).
    /// </summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time (24-hour format).
    /// </summary>
    public TimeOnly EndTime { get; set; }

    /// <summary>
    /// Gets the days of week this range applies to.
    /// </summary>
    public DayOfWeek[] DaysOfWeek { get; init; } = Array.Empty<DayOfWeek>();
}

/// <summary>
/// Load balancing configuration.
/// </summary>
public class LoadBalancingConfig
{
    /// <summary>
    /// Gets or sets whether to enable load balancing.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the load balancing algorithm.
    /// </summary>
    public LoadBalancingAlgorithm Algorithm { get; set; } = LoadBalancingAlgorithm.WeightedRoundRobin;

    /// <summary>
    /// Gets the provider weights for weighted algorithms.
    /// </summary>
    public Dictionary<string, int> ProviderWeights { get; init; } = new()
    {
        ["Ollama"] = 3, // Prefer local 3:1 ratio
        ["Gemini"] = 1
    };

    /// <summary>
    /// Gets or sets the load threshold percentage (0-100).
    /// Above this threshold, requests are distributed to other providers.
    /// </summary>
    public int LoadThresholdPercent { get; set; } = 80;

    /// <summary>
    /// Gets or sets the interval for updating load statistics.
    /// </summary>
    public TimeSpan LoadUpdateInterval { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Load balancing algorithms.
/// </summary>
public enum LoadBalancingAlgorithm
{
    /// <summary>
    /// Simple round-robin distribution.
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Weighted round-robin based on provider weights.
    /// </summary>
    WeightedRoundRobin,

    /// <summary>
    /// Route to provider with least current connections.
    /// </summary>
    LeastConnections,

    /// <summary>
    /// Route to provider with fastest response time.
    /// </summary>
    FastestResponse,

    /// <summary>
    /// Route based on provider health scores.
    /// </summary>
    HealthBased
}

/// <summary>
/// Circuit breaker configuration for provider resilience.
/// </summary>
public class CircuitBreakerConfig
{
    /// <summary>
    /// Gets or sets whether to enable circuit breaker pattern.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the failure threshold to open the circuit.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the time window for failure counting.
    /// </summary>
    public TimeSpan FailureTimeWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the duration to keep circuit open.
    /// </summary>
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets the number of test requests in half-open state.
    /// </summary>
    public int HalfOpenTestRequests { get; set; } = 3;

    /// <summary>
    /// Gets or sets the success threshold to close the circuit from half-open.
    /// </summary>
    public int SuccessThreshold { get; set; } = 2;
}