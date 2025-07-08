using MarketAnalyzer.Infrastructure.AI.Configuration;
using MarketAnalyzer.Infrastructure.AI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;
using Microsoft.ML;
using Microsoft.Extensions.Caching.Memory;

namespace MarketAnalyzer.Infrastructure.AI.Extensions;

/// <summary>
/// Service collection extensions for registering AI/ML infrastructure services.
/// Uses industry-standard ML libraries: ONNX Runtime, ML.NET, TorchSharp.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds ML inference services to the service collection.
    /// Configures ONNX Runtime, ML.NET, and supporting services.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMLInference(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure AIOptions using the Options pattern
        services.Configure<AIOptions>(configuration.GetSection(AIOptions.SectionName));

        // Register ML inference service (temporarily commented out)
        // services.AddSingleton<IMLInferenceService, MLInferenceService>();

        // Add ML.NET services
        services.AddSingleton<MLContext>();

        // Add memory cache for model results
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Adds LLM services to the service collection.
    /// Configures Ollama, Gemini, and LLM orchestration services.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddLLMServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options using the Options pattern
        services.Configure<OllamaOptions>(configuration.GetSection("Ollama"));
        services.Configure<GeminiOptions>(configuration.GetSection("Gemini"));
        services.Configure<LLMOrchestrationOptions>(configuration.GetSection("LLMOrchestration"));

        // Add HTTP client factory
        services.AddHttpClient();

        // Register LLM providers
        services.AddSingleton<OllamaProvider>();
        services.AddSingleton<GeminiProvider>();
        services.AddSingleton<LLMOrchestrationService>();
        
        // Register orchestration service as the main ILLMProvider
        services.AddSingleton<ILLMProvider>(provider => provider.GetRequiredService<LLMOrchestrationService>());

        // Add memory cache for LLM responses
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Adds ML.NET specific services for traditional ML models.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMLNet(this IServiceCollection services)
    {
        // Register MLContext as singleton
        services.AddSingleton<MLContext>(provider =>
        {
            var mlContext = new MLContext(seed: 42);
            
            // Configure ML.NET for better performance
            mlContext.GpuDeviceId = 0;
            mlContext.FallbackToCpu = true;
            
            return mlContext;
        });

        return services;
    }

    /// <summary>
    /// Adds GPU acceleration services if available.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="preferredGpuIndex">The preferred GPU index</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddGpuAcceleration(
        this IServiceCollection services,
        int preferredGpuIndex = 0)
    {
        // GPU detection and configuration would go here
        // This is a placeholder for GPU-specific initialization
        
        services.AddSingleton<IGpuDetectionService>(provider =>
        {
            // Simplified GPU detection service
            return new GpuDetectionService(preferredGpuIndex);
        });

        return services;
    }

    /// <summary>
    /// Adds model repository services for model management.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="modelsPath">The path to the models directory</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddModelRepository(
        this IServiceCollection services,
        string modelsPath)
    {
        services.AddSingleton<IModelRepository>(provider =>
        {
            return new FileSystemModelRepository(modelsPath);
        });

        return services;
    }
}

/// <summary>
/// Simple GPU detection service interface.
/// </summary>
public interface IGpuDetectionService
{
    bool IsGpuAvailable { get; }
    int GpuCount { get; }
    string GetGpuInfo(int index);
}

/// <summary>
/// Basic GPU detection implementation.
/// </summary>
internal class GpuDetectionService : IGpuDetectionService
{
    public bool IsGpuAvailable { get; }
    public int GpuCount { get; }
    private readonly int _preferredIndex;

    public GpuDetectionService(int preferredIndex)
    {
        _preferredIndex = preferredIndex;
        
        // Check for CUDA availability
        try
        {
            var providers = Microsoft.ML.OnnxRuntime.OrtEnv.Instance().GetAvailableProviders();
            IsGpuAvailable = providers.Contains("CUDAExecutionProvider") || 
                            providers.Contains("DmlExecutionProvider");
            GpuCount = IsGpuAvailable ? 1 : 0; // Simplified
        }
        catch
        {
            IsGpuAvailable = false;
            GpuCount = 0;
        }
    }

    public string GetGpuInfo(int index)
    {
        return IsGpuAvailable ? $"GPU {index} available" : "No GPU available";
    }
}

/// <summary>
/// Model repository interface for model management.
/// </summary>
public interface IModelRepository
{
    Task<byte[]> GetModelAsync(string modelName);
    Task<bool> ModelExistsAsync(string modelName);
    Task SaveModelAsync(string modelName, byte[] modelData);
    Task<string[]> ListModelsAsync();
}

/// <summary>
/// File system based model repository.
/// </summary>
internal class FileSystemModelRepository : IModelRepository
{
    private readonly string _modelsPath;

    public FileSystemModelRepository(string modelsPath)
    {
        _modelsPath = modelsPath;
        Directory.CreateDirectory(_modelsPath);
    }

    public async Task<byte[]> GetModelAsync(string modelName)
    {
        var path = Path.Combine(_modelsPath, $"{modelName}.onnx");
        return await File.ReadAllBytesAsync(path).ConfigureAwait(false);
    }

    public Task<bool> ModelExistsAsync(string modelName)
    {
        var path = Path.Combine(_modelsPath, $"{modelName}.onnx");
        return Task.FromResult(File.Exists(path));
    }

    public async Task SaveModelAsync(string modelName, byte[] modelData)
    {
        var path = Path.Combine(_modelsPath, $"{modelName}.onnx");
        await File.WriteAllBytesAsync(path, modelData).ConfigureAwait(false);
    }

    public Task<string[]> ListModelsAsync()
    {
        var files = Directory.GetFiles(_modelsPath, "*.onnx")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .ToArray();
        
        return Task.FromResult(files);
    }
}