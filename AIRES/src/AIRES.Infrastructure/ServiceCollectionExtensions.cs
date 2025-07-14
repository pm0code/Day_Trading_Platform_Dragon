using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using AIRES.Core.Configuration;
using AIRES.Core.Domain.Interfaces;
using AIRES.Infrastructure.AI.Clients;
using AIRES.Infrastructure.AI.Services;
using AIRES.Infrastructure.Configuration;
using Polly;
using Polly.Extensions.Http;

namespace AIRES.Infrastructure;

/// <summary>
/// Extension methods for registering AIRES Infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds AIRES Infrastructure services to the service collection.
    /// </summary>
    public static IServiceCollection AddAIRESInfrastructure(this IServiceCollection services)
    {
        // Register configuration services
        services.AddSingleton<AIRESConfigurationService>();
        services.AddSingleton<IAIRESConfiguration>(provider => provider.GetRequiredService<AIRESConfigurationService>());
        services.AddSingleton<AIRESConfigurationProvider>();
        services.AddSingleton<IAIRESConfigurationProvider>(provider => provider.GetRequiredService<AIRESConfigurationProvider>());
        
        // Register Ollama HTTP client
        services.AddHttpClient<IOllamaClient, OllamaClient>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .AddPolicyHandler(GetRetryPolicy());
        
        // Register AI services with their own HTTP clients where needed
        services.AddHttpClient<MistralDocumentationService>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .AddPolicyHandler(GetRetryPolicy());
        
        // Register AI model implementations
        services.AddScoped<IErrorDocumentationAIModel, MistralDocumentationService>();
        services.AddScoped<IContextAnalyzerAIModel, DeepSeekContextService>();
        services.AddScoped<IPatternValidatorAIModel, CodeGemmaPatternService>();
        services.AddScoped<IBookletGeneratorAIModel, Gemma2BookletService>();
        
        return services;
    }
    
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log to console for now
                    Console.WriteLine($"Retry {retryCount} after {timespan}ms");
                });
    }
}