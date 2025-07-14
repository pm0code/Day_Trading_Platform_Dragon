using AIRES.Foundation.Results;

namespace AIRES.Infrastructure.AI.Clients;

/// <summary>
/// Defines the contract for Ollama LLM communication.
/// </summary>
public interface IOllamaClient
{
    /// <summary>
    /// Generates a response from the specified Ollama model.
    /// </summary>
    /// <param name="request">The generation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The model's response</returns>
    Task<AIRESResult<OllamaResponse>> GenerateAsync(
        OllamaRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a model is available.
    /// </summary>
    Task<AIRESResult<bool>> IsModelAvailableAsync(
        string modelName,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lists available models.
    /// </summary>
    Task<AIRESResult<List<OllamaModel>>> ListModelsAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to generate content from Ollama.
/// </summary>
public class OllamaRequest
{
    /// <summary>
    /// The model to use (e.g., "mistral:7b", "deepseek-coder:6.7b").
    /// </summary>
    public string Model { get; init; } = string.Empty;
    
    /// <summary>
    /// The system prompt (sets behavior).
    /// </summary>
    public string? System { get; init; }
    
    /// <summary>
    /// The user prompt.
    /// </summary>
    public string Prompt { get; init; } = string.Empty;
    
    /// <summary>
    /// Temperature for randomness (0.0 = deterministic, 1.0 = creative).
    /// </summary>
    public double Temperature { get; init; } = 0.1;
    
    /// <summary>
    /// Maximum tokens to generate.
    /// </summary>
    public int? MaxTokens { get; init; }
    
    /// <summary>
    /// Top-p sampling parameter.
    /// </summary>
    public double TopP { get; init; } = 0.95;
    
    /// <summary>
    /// Request timeout in seconds. Default 120s but should be set based on:
    /// - Model size (7B=90s, 13B=120s, 70B=300s typical)
    /// - Task complexity (simple lookup vs deep analysis)
    /// - Historical performance data (use P99 + 20% buffer)
    /// </summary>
    public int TimeoutSeconds { get; init; } = 120;
    
    /// <summary>
    /// Whether to stream the response.
    /// </summary>
    public bool Stream { get; init; } = false;
}

/// <summary>
/// Response from Ollama model.
/// </summary>
public class OllamaResponse
{
    /// <summary>
    /// The generated text.
    /// </summary>
    public string Response { get; init; } = string.Empty;
    
    /// <summary>
    /// The model that generated the response.
    /// </summary>
    public string Model { get; init; } = string.Empty;
    
    /// <summary>
    /// Whether generation is complete.
    /// </summary>
    public bool Done { get; init; }
    
    /// <summary>
    /// Time taken to generate.
    /// </summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>
    /// Tokens per second.
    /// </summary>
    public double? TokensPerSecond { get; init; }
    
    /// <summary>
    /// Total tokens generated.
    /// </summary>
    public int? TotalTokens { get; init; }
}

/// <summary>
/// Information about an available Ollama model.
/// </summary>
public class OllamaModel
{
    public string Name { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime ModifiedAt { get; init; }
    public string Digest { get; init; } = string.Empty;
}