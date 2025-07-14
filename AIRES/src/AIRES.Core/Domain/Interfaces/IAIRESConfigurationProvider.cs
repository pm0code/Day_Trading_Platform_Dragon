namespace AIRES.Core.Domain.Interfaces;

/// <summary>
/// Interface for providing AIRES configuration values.
/// Allows services to access configuration without direct dependency on infrastructure.
/// </summary>
public interface IAIRESConfigurationProvider
{
    /// <summary>
    /// Gets the input directory path for monitoring error files.
    /// </summary>
    string InputDirectory { get; }

    /// <summary>
    /// Gets the output directory path for saving booklets.
    /// </summary>
    string OutputDirectory { get; }

    /// <summary>
    /// Gets the temporary directory path for processing.
    /// </summary>
    string TempDirectory { get; }

    /// <summary>
    /// Gets the Ollama service base URL.
    /// </summary>
    string OllamaBaseUrl { get; }

    /// <summary>
    /// Gets the timeout in seconds for AI service calls.
    /// </summary>
    int AIServiceTimeout { get; }

    /// <summary>
    /// Gets the model name for a specific AI service.
    /// </summary>
    string GetModelName(string aiService);

    /// <summary>
    /// Gets whether parallel processing is enabled.
    /// </summary>
    bool IsParallelProcessingEnabled { get; }

    /// <summary>
    /// Gets the maximum retry count for AI operations.
    /// </summary>
    int MaxRetries { get; }

    /// <summary>
    /// Gets the retry delay in seconds.
    /// </summary>
    int RetryDelaySeconds { get; }
}