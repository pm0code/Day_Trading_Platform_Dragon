using AIRES.Core.Configuration;
using AIRES.Core.Domain.Interfaces;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;

namespace AIRES.Infrastructure.Configuration;

/// <summary>
/// Implementation of IAIRESConfigurationProvider that uses IAIRESConfiguration.
/// </summary>
public class AIRESConfigurationProvider : AIRESServiceBase, IAIRESConfigurationProvider
{
    private readonly IAIRESConfiguration _configuration;

    public AIRESConfigurationProvider(
        IAIRESLogger logger,
        IAIRESConfiguration configuration)
        : base(logger, nameof(AIRESConfigurationProvider))
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public string InputDirectory => _configuration.Directories.InputDirectory;
    public string OutputDirectory => _configuration.Directories.OutputDirectory;
    public string TempDirectory => _configuration.Directories.TempDirectory;
    public string OllamaBaseUrl => _configuration.AIServices.OllamaBaseUrl;
    public int AIServiceTimeout => _configuration.AIServices.OllamaTimeout;
    public bool IsParallelProcessingEnabled => _configuration.Pipeline.EnableParallelProcessing;
    public int MaxRetries => _configuration.Pipeline.MaxRetries;
    public int RetryDelaySeconds => _configuration.Pipeline.RetryDelay;

    public string GetModelName(string aiService)
    {
        LogMethodEntry();
        
        var modelName = aiService.ToLowerInvariant() switch
        {
            "mistral" => _configuration.AIServices.MistralModel,
            "deepseek" => _configuration.AIServices.DeepSeekModel,
            "codegemma" => _configuration.AIServices.CodeGemmaModel,
            "gemma2" => _configuration.AIServices.Gemma2Model,
            _ => $"{aiService}:latest"
        };

        LogDebug($"Model for {aiService}: {modelName}");
        LogMethodExit();
        return modelName;
    }

    /// <summary>
    /// Initializes the configuration provider.
    /// </summary>
    protected override async Task<AIRESResult<bool>> OnInitializeAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();

        try
        {
            // Configuration is already injected, just log success
            LogInfo("Configuration provider initialized successfully");
            LogMethodExit();
            return await Task.FromResult(AIRESResult<bool>.Success(true));
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize configuration provider", ex);
            LogMethodExit();
            return AIRESResult<bool>.Failure("CONFIG_INIT_ERROR", "Failed to initialize configuration", ex);
        }
    }
}