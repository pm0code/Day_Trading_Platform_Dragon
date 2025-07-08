namespace MarketAnalyzer.Infrastructure.AI.Configuration;

/// <summary>
/// Configuration options for the Ollama LLM provider.
/// Based on state-of-the-art best practices for 2025.
/// </summary>
public class OllamaOptions
{
    /// <summary>
    /// Gets or sets the base URL for the Ollama API.
    /// Default: http://localhost:11434
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Gets or sets the default model to use if not specified in requests.
    /// Recommended: llama3.2:3b-instruct-q4_K_M for balanced performance.
    /// </summary>
    public string DefaultModel { get; set; } = "llama3.2:3b-instruct-q4_K_M";

    /// <summary>
    /// Gets or sets the model to use for embeddings.
    /// Default: nomic-embed-text
    /// </summary>
    public string? EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Gets or sets the maximum number of concurrent requests.
    /// Research shows 32 is optimal for Ollama.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 32;

    /// <summary>
    /// Gets or sets the request timeout in seconds.
    /// Longer timeout for large models.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the connection idle timeout in minutes.
    /// </summary>
    public int ConnectionIdleTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the connection lifetime in minutes.
    /// </summary>
    public int ConnectionLifetimeMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets the cache TTL in minutes for responses.
    /// </summary>
    public int CacheTTLMinutes { get; set; } = 60;

    /// <summary>
    /// Gets the models to preload on startup.
    /// </summary>
    public List<string>? PreloadModels { get; init; }

    /// <summary>
    /// Gets the model-specific configurations.
    /// </summary>
    public Dictionary<string, ModelConfig> ModelConfigs { get; init; } = new()
    {
        ["general"] = new ModelConfig { ModelName = "llama3.2:3b-instruct-q4_K_M", MaxTokens = 2048 },
        ["analysis"] = new ModelConfig { ModelName = "mixtral:8x7b-instruct-q4_K_M", MaxTokens = 4096 },
        ["code"] = new ModelConfig { ModelName = "deepseek-coder:6.7b-instruct-q4_K_M", MaxTokens = 8192 },
        ["risk"] = new ModelConfig { ModelName = "llama3.2:7b-instruct-q4_K_M", MaxTokens = 2048 },
        ["summary"] = new ModelConfig { ModelName = "phi3:mini-4k-instruct-q4_K_M", MaxTokens = 1024 }
    };

    /// <summary>
    /// Gets or sets whether to enable request batching.
    /// </summary>
    public bool EnableBatching { get; set; } = true;

    /// <summary>
    /// Gets or sets the batch size for request batching.
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets the batch timeout in milliseconds.
    /// </summary>
    public int BatchTimeoutMs { get; set; } = 50;

    /// <summary>
    /// Gets or sets whether to enable semantic caching.
    /// </summary>
    public bool EnableSemanticCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the semantic similarity threshold for cache hits.
    /// </summary>
    public float SemanticCacheThreshold { get; set; } = 0.95f;
}

/// <summary>
/// Model-specific configuration.
/// </summary>
public class ModelConfig
{
    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum tokens for this model.
    /// </summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the temperature override for this model.
    /// </summary>
    public decimal? Temperature { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports streaming.
    /// </summary>
    public bool SupportsStreaming { get; set; } = true;

    /// <summary>
    /// Gets or sets the context window size.
    /// </summary>
    public int? ContextWindow { get; set; }
}