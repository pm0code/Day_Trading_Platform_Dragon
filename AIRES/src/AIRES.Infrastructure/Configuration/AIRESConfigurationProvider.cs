using AIRES.Core.Domain.Interfaces;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;

namespace AIRES.Infrastructure.Configuration;

/// <summary>
/// Implementation of IAIRESConfigurationProvider that uses AIRESConfiguration.
/// </summary>
public class AIRESConfigurationProvider : AIRESServiceBase, IAIRESConfigurationProvider
{
    private readonly AIRESConfigurationService _configService;
    private AIRESConfiguration? _cachedConfig;

    public AIRESConfigurationProvider(
        IAIRESLogger logger,
        AIRESConfigurationService configService)
        : base(logger, nameof(AIRESConfigurationProvider))
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public string InputDirectory => GetConfig()?.Directories?.InputDirectory ?? "./input";
    public string OutputDirectory => GetConfig()?.Directories?.OutputDirectory ?? "./docs/error-booklets";
    public string TempDirectory => GetConfig()?.Directories?.TempDirectory ?? "./temp";
    public string OllamaBaseUrl => GetConfig()?.AIServices?.OllamaBaseUrl ?? "http://localhost:11434";
    public int AIServiceTimeout => GetConfig()?.AIServices?.OllamaTimeout ?? 120;
    public bool IsParallelProcessingEnabled => GetConfig()?.Pipeline?.EnableParallelProcessing ?? true;
    public int MaxRetries => GetConfig()?.Pipeline?.MaxRetries ?? 3;
    public int RetryDelaySeconds => GetConfig()?.Pipeline?.RetryDelay ?? 2;

    public string GetModelName(string aiService)
    {
        LogMethodEntry();
        
        var config = GetConfig();
        if (config?.AIServices == null)
        {
            LogWarning($"Configuration not loaded, using default model for {aiService}");
            LogMethodExit();
            return $"{aiService}:latest";
        }

        var modelName = aiService.ToLowerInvariant() switch
        {
            "mistral" => config.AIServices.MistralModel,
            "deepseek" => config.AIServices.DeepSeekModel,
            "codegemma" => config.AIServices.CodeGemmaModel,
            "gemma2" => config.AIServices.Gemma2Model,
            _ => $"{aiService}:latest"
        };

        LogDebug($"Model for {aiService}: {modelName}");
        LogMethodExit();
        return modelName;
    }

    private AIRESConfiguration? GetConfig()
    {
        if (_cachedConfig != null)
            return _cachedConfig;

        // This is synchronous access to async method - not ideal but necessary for property access
        // In production, configuration should be loaded at startup
        var task = _configService.GetConfigurationAsync();
        task.Wait();
        
        if (task.Result.IsSuccess)
        {
            _cachedConfig = task.Result.Value;
        }
        else
        {
            LogWarning("Failed to load configuration, using defaults");
        }

        return _cachedConfig;
    }

    /// <summary>
    /// Initializes the configuration provider by loading configuration.
    /// </summary>
    protected override async Task<AIRESResult<bool>> OnInitializeAsync(CancellationToken cancellationToken)
    {
        LogMethodEntry();

        try
        {
            var configResult = await _configService.LoadConfigurationAsync();
            if (!configResult.IsSuccess)
            {
                LogError($"Failed to load configuration: {configResult.ErrorMessage}");
                LogMethodExit();
                return AIRESResult<bool>.Failure(configResult.ErrorCode!, configResult.ErrorMessage!);
            }

            _cachedConfig = configResult.Value;
            LogInfo("Configuration provider initialized successfully");
            LogMethodExit();
            return AIRESResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize configuration provider", ex);
            LogMethodExit();
            return AIRESResult<bool>.Failure("CONFIG_INIT_ERROR", "Failed to initialize configuration", ex);
        }
    }
}