using MarketAnalyzer.Foundation;
using MarketAnalyzer.Infrastructure.AI.Models;

namespace MarketAnalyzer.Infrastructure.AI.Services;

/// <summary>
/// Defines the contract for Large Language Model (LLM) providers.
/// Supports both local (Ollama) and cloud (Gemini) implementations.
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// Gets the provider name (e.g., "Ollama", "Gemini").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets whether this provider runs locally.
    /// </summary>
    bool IsLocal { get; }

    /// <summary>
    /// Gets the current health status of the provider.
    /// </summary>
    /// <returns>A trading result containing the provider health status</returns>
    Task<TradingResult<LLMHealthStatus>> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a completion for the given prompt.
    /// </summary>
    /// <param name="request">The LLM request containing prompt and parameters</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing the LLM response</returns>
    Task<TradingResult<LLMResponse>> GenerateCompletionAsync(
        LLMRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates streaming completion for the given prompt.
    /// </summary>
    /// <param name="request">The LLM request with streaming enabled</param>
    /// <param name="onToken">Callback for each token received</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing the complete response</returns>
    Task<TradingResult<LLMResponse>> GenerateStreamingCompletionAsync(
        LLMRequest request,
        Action<string> onToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embeddings for the given text.
    /// </summary>
    /// <param name="text">The text to embed</param>
    /// <param name="model">Optional model name for embeddings</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing the embeddings</returns>
    Task<TradingResult<LLMEmbedding>> GenerateEmbeddingAsync(
        string text,
        string? model = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists available models for this provider.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing available models</returns>
    Task<TradingResult<List<LLMModelInfo>>> ListModelsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Preloads a model for faster inference.
    /// </summary>
    /// <param name="modelName">The name of the model to preload</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result indicating success</returns>
    Task<TradingResult<bool>> PreloadModelAsync(
        string modelName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates the cost for a request (for cloud providers).
    /// </summary>
    /// <param name="request">The LLM request to estimate</param>
    /// <returns>The estimated cost in USD (0 for local providers)</returns>
    decimal EstimateRequestCost(LLMRequest request);

    /// <summary>
    /// Gets usage statistics for this provider.
    /// </summary>
    /// <returns>A trading result containing usage statistics</returns>
    Task<TradingResult<LLMUsageStatistics>> GetUsageStatisticsAsync();
}

/// <summary>
/// Represents an LLM request with all necessary parameters.
/// </summary>
public class LLMRequest
{
    /// <summary>
    /// Gets or sets the prompt text.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the system prompt (optional).
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Gets or sets the model to use (optional, uses provider default if not specified).
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the maximum tokens to generate.
    /// </summary>
    public int MaxTokens { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the temperature (0-2, higher = more creative).
    /// </summary>
    public decimal Temperature { get; set; } = 0.7m;

    /// <summary>
    /// Gets or sets the top-p value for nucleus sampling.
    /// </summary>
    public decimal TopP { get; set; } = 0.9m;

    /// <summary>
    /// Gets the stop sequences.
    /// </summary>
    public List<string> StopSequences { get; init; } = new();

    /// <summary>
    /// Gets or sets whether to stream the response.
    /// </summary>
    public bool Stream { get; set; }

    /// <summary>
    /// Gets or sets the request priority (higher = more important).
    /// </summary>
    public int Priority { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to allow fallback to another provider.
    /// </summary>
    public bool AllowFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets the request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the prompt type for specialized handling.
    /// </summary>
    public LLMPromptType PromptType { get; set; } = LLMPromptType.General;

    /// <summary>
    /// Gets the additional metadata for the request.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Represents an LLM response.
/// </summary>
public class LLMResponse
{
    /// <summary>
    /// Gets or sets the generated text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model used.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider that generated this response.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of prompt tokens.
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of completion tokens.
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Gets or sets the total tokens used.
    /// </summary>
    public int TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>
    /// Gets or sets the inference time in milliseconds.
    /// </summary>
    public double InferenceTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the cost in USD (0 for local providers).
    /// </summary>
    public decimal Cost { get; set; }

    /// <summary>
    /// Gets or sets the confidence score (0-1).
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Gets or sets the finish reason.
    /// </summary>
    public LLMFinishReason FinishReason { get; set; }

    /// <summary>
    /// Gets the structured data extracted from the response.
    /// </summary>
    public Dictionary<string, object> StructuredData { get; init; } = new();

    /// <summary>
    /// Gets the response metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Gets or sets the timestamp when the response was generated.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents LLM embeddings.
/// </summary>
public class LLMEmbedding
{
    /// <summary>
    /// Gets or sets the embedding vector.
    /// </summary>
    public float[] Vector { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Gets or sets the model used for embeddings.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of dimensions.
    /// </summary>
    public int Dimensions => Vector.Length;

    /// <summary>
    /// Gets or sets the token count of the input text.
    /// </summary>
    public int TokenCount { get; set; }

    /// <summary>
    /// Gets or sets the generation time in milliseconds.
    /// </summary>
    public double GenerationTimeMs { get; set; }
}

/// <summary>
/// Represents information about an available LLM model.
/// </summary>
public class LLMModelInfo
{
    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model size in parameters (e.g., "7B", "13B").
    /// </summary>
    public string ParameterSize { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quantization level (e.g., "Q4_0", "Q8_0").
    /// </summary>
    public string? Quantization { get; set; }

    /// <summary>
    /// Gets or sets the context window size.
    /// </summary>
    public int ContextWindow { get; set; }

    /// <summary>
    /// Gets or sets whether the model is currently loaded.
    /// </summary>
    public bool IsLoaded { get; set; }

    /// <summary>
    /// Gets or sets the model size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the required VRAM in bytes.
    /// </summary>
    public long RequiredVRAM { get; set; }

    /// <summary>
    /// Gets the model capabilities.
    /// </summary>
    public List<string> Capabilities { get; init; } = new();

    /// <summary>
    /// Gets or sets when the model was last used.
    /// </summary>
    public DateTime? LastUsed { get; set; }
}

/// <summary>
/// Represents the health status of an LLM provider.
/// </summary>
public class LLMHealthStatus
{
    /// <summary>
    /// Gets or sets whether the provider is healthy.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the provider status message.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the average latency in milliseconds.
    /// </summary>
    public double AverageLatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the number of available models.
    /// </summary>
    public int AvailableModels { get; set; }

    /// <summary>
    /// Gets or sets the number of loaded models.
    /// </summary>
    public int LoadedModels { get; set; }

    /// <summary>
    /// Gets or sets memory usage information.
    /// </summary>
    public MemoryUsageInfo? MemoryUsage { get; set; }

    /// <summary>
    /// Gets the error messages.
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Gets or sets the last check timestamp.
    /// </summary>
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents memory usage information.
/// </summary>
public class MemoryUsageInfo
{
    /// <summary>
    /// Gets or sets the total memory in bytes.
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Gets or sets the used memory in bytes.
    /// </summary>
    public long UsedBytes { get; set; }

    /// <summary>
    /// Gets or sets the available memory in bytes.
    /// </summary>
    public long AvailableBytes { get; set; }

    /// <summary>
    /// Gets the memory usage percentage.
    /// </summary>
    public double UsagePercentage => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;
}

/// <summary>
/// Represents usage statistics for an LLM provider.
/// </summary>
public class LLMUsageStatistics
{
    /// <summary>
    /// Gets or sets the total number of requests.
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Gets or sets the number of successful requests.
    /// </summary>
    public long SuccessfulRequests { get; set; }

    /// <summary>
    /// Gets or sets the number of failed requests.
    /// </summary>
    public long FailedRequests { get; set; }

    /// <summary>
    /// Gets or sets the total tokens processed.
    /// </summary>
    public long TotalTokens { get; set; }

    /// <summary>
    /// Gets or sets the total cost in USD.
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Gets or sets the average latency in milliseconds.
    /// </summary>
    public double AverageLatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the P95 latency in milliseconds.
    /// </summary>
    public double P95LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the P99 latency in milliseconds.
    /// </summary>
    public double P99LatencyMs { get; set; }

    /// <summary>
    /// Gets the usage by model.
    /// </summary>
    public Dictionary<string, long> ModelUsage { get; init; } = new();

    /// <summary>
    /// Gets the usage by prompt type.
    /// </summary>
    public Dictionary<LLMPromptType, long> PromptTypeUsage { get; init; } = new();

    /// <summary>
    /// Gets or sets the period start time.
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// Gets or sets the period end time.
    /// </summary>
    public DateTime PeriodEnd { get; set; }
}

/// <summary>
/// Types of LLM prompts for specialized handling.
/// </summary>
public enum LLMPromptType
{
    /// <summary>
    /// General purpose prompt.
    /// </summary>
    General,

    /// <summary>
    /// Market analysis and insights.
    /// </summary>
    MarketAnalysis,

    /// <summary>
    /// Risk assessment and evaluation.
    /// </summary>
    RiskAssessment,

    /// <summary>
    /// Trading signal explanation.
    /// </summary>
    TradingSignal,

    /// <summary>
    /// Technical indicator narrative.
    /// </summary>
    TechnicalIndicator,

    /// <summary>
    /// News sentiment analysis.
    /// </summary>
    NewsSentiment,

    /// <summary>
    /// Report generation.
    /// </summary>
    ReportGeneration,

    /// <summary>
    /// Code generation for strategies.
    /// </summary>
    CodeGeneration,

    /// <summary>
    /// Data extraction from text.
    /// </summary>
    DataExtraction,

    /// <summary>
    /// Quick summary of data.
    /// </summary>
    QuickSummary
}

/// <summary>
/// Reasons why LLM generation finished.
/// </summary>
public enum LLMFinishReason
{
    /// <summary>
    /// Completed normally.
    /// </summary>
    Complete,

    /// <summary>
    /// Reached maximum token limit.
    /// </summary>
    MaxTokens,

    /// <summary>
    /// Hit a stop sequence.
    /// </summary>
    StopSequence,

    /// <summary>
    /// Request timeout.
    /// </summary>
    Timeout,

    /// <summary>
    /// Request was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// An error occurred.
    /// </summary>
    Error
}