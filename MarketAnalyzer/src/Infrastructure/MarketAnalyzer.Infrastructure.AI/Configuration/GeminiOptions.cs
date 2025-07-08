namespace MarketAnalyzer.Infrastructure.AI.Configuration;

/// <summary>
/// Configuration options for the Google Gemini LLM provider.
/// Based on state-of-the-art best practices for 2025.
/// </summary>
public class GeminiOptions
{
    /// <summary>
    /// Gets or sets the Gemini API key.
    /// Required for authentication.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base URL for the Gemini API.
    /// Default: https://generativelanguage.googleapis.com
    /// </summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";

    /// <summary>
    /// Gets or sets the default model to use if not specified in requests.
    /// Recommended: gemini-1.5-flash for best speed/cost ratio.
    /// </summary>
    public string DefaultModel { get; set; } = "gemini-1.5-flash";

    /// <summary>
    /// Gets or sets the fast model for quick responses.
    /// Default: gemini-1.5-flash
    /// </summary>
    public string FastModel { get; set; } = "gemini-1.5-flash";

    /// <summary>
    /// Gets or sets the analysis model for complex reasoning.
    /// Default: gemini-1.5-pro
    /// </summary>
    public string AnalysisModel { get; set; } = "gemini-1.5-pro";

    /// <summary>
    /// Gets or sets the code generation model.
    /// Default: gemini-1.5-pro
    /// </summary>
    public string CodeModel { get; set; } = "gemini-1.5-pro";

    /// <summary>
    /// Gets or sets the model to use for embeddings.
    /// Default: text-embedding-004
    /// </summary>
    public string? EmbeddingModel { get; set; } = "text-embedding-004";

    /// <summary>
    /// Gets or sets the maximum number of concurrent requests.
    /// Gemini has rate limits per minute, not concurrent.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// Gets or sets the request timeout in seconds.
    /// Cloud requests may take longer than local.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the cache TTL in minutes for responses.
    /// </summary>
    public int CacheTTLMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the requests per minute limit.
    /// Gemini free tier: 15 RPM, Pro: 1000 RPM
    /// </summary>
    public int RequestsPerMinute { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the tokens per minute limit.
    /// Gemini free tier: 1M TPM, Pro: 4M TPM
    /// </summary>
    public int TokensPerMinute { get; set; } = 4_000_000;

    /// <summary>
    /// Gets or sets whether to enable cost tracking.
    /// </summary>
    public bool EnableCostTracking { get; set; } = true;

    /// <summary>
    /// Gets or sets the cost alert threshold in USD.
    /// </summary>
    public decimal CostAlertThreshold { get; set; } = 10.0m;

    /// <summary>
    /// Gets or sets whether to enable automatic fallback to local provider.
    /// </summary>
    public bool EnableFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum cost per request in USD.
    /// Prevents expensive requests from running away.
    /// </summary>
    public decimal MaxCostPerRequest { get; set; } = 0.10m;

    /// <summary>
    /// Gets or sets whether to use structured output (JSON mode).
    /// </summary>
    public bool UseStructuredOutput { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable response caching.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable semantic caching.
    /// </summary>
    public bool EnableSemanticCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the semantic similarity threshold for cache hits.
    /// </summary>
    public float SemanticCacheThreshold { get; set; } = 0.95f;

    /// <summary>
    /// Gets or sets the retry configuration.
    /// </summary>
    public RetryConfig Retry { get; set; } = new();

    /// <summary>
    /// Gets or sets the safety settings for content filtering.
    /// </summary>
    public SafetyConfig Safety { get; set; } = new();
}

/// <summary>
/// Retry configuration for Gemini API calls.
/// </summary>
public class RetryConfig
{
    /// <summary>
    /// Gets or sets the maximum number of retries.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay in milliseconds.
    /// </summary>
    public int BaseDelayMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum delay in milliseconds.
    /// </summary>
    public int MaxDelayMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets whether to use exponential backoff.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to add jitter to prevent thundering herd.
    /// </summary>
    public bool UseJitter { get; set; } = true;
}

/// <summary>
/// Safety configuration for content filtering.
/// </summary>
public class SafetyConfig
{
    /// <summary>
    /// Gets or sets the harassment threshold.
    /// Values: BLOCK_NONE, BLOCK_ONLY_HIGH, BLOCK_MEDIUM_AND_ABOVE, BLOCK_LOW_AND_ABOVE
    /// </summary>
    public string HarassmentThreshold { get; set; } = "BLOCK_ONLY_HIGH";

    /// <summary>
    /// Gets or sets the hate speech threshold.
    /// </summary>
    public string HateSpeechThreshold { get; set; } = "BLOCK_ONLY_HIGH";

    /// <summary>
    /// Gets or sets the sexually explicit threshold.
    /// </summary>
    public string SexuallyExplicitThreshold { get; set; } = "BLOCK_ONLY_HIGH";

    /// <summary>
    /// Gets or sets the dangerous content threshold.
    /// </summary>
    public string DangerousContentThreshold { get; set; } = "BLOCK_ONLY_HIGH";

    /// <summary>
    /// Gets or sets whether to enable safety filtering.
    /// </summary>
    public bool EnableSafetyFiltering { get; set; } = true;
}