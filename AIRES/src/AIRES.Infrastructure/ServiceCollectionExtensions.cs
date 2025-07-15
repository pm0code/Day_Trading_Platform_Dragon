using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using AIRES.Core.Configuration;
using AIRES.Core.Domain.Interfaces;
using AIRES.Core.Interfaces;
using AIRES.Foundation.Logging;
using AIRES.Infrastructure.AI;
using AIRES.Infrastructure.AI.Clients;
using AIRES.Infrastructure.AI.Services;
using AIRES.Infrastructure.AI.Models;
using AIRES.Infrastructure.Configuration;
using AIRES.Infrastructure.Parsers;
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
        
        // Register GPU detection and enhanced load balancing
        services.AddSingleton<IGpuDetectionService, GpuDetectionService>();
        services.AddSingleton<IEnhancedLoadBalancerService, EnhancedLoadBalancerService>();
        
        // Register Ollama Load Balancer Service
        services.AddSingleton<OllamaLoadBalancerService>();
        services.AddSingleton<IOllamaLoadBalancerService>(provider => provider.GetRequiredService<OllamaLoadBalancerService>());
        
        // Register predefined HTTP clients for GPU instances
        services.AddHttpClient("Ollama", client =>
        {
            client.BaseAddress = new Uri("http://localhost:11434");
            client.Timeout = TimeSpan.FromMinutes(5);
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());
        
        services.AddHttpClient("Ollama-GPU0", client =>
        {
            client.BaseAddress = new Uri("http://localhost:11434");
            client.Timeout = TimeSpan.FromMinutes(5);
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());
        
        services.AddHttpClient("Ollama-GPU1", client =>
        {
            client.BaseAddress = new Uri("http://localhost:11435");
            client.Timeout = TimeSpan.FromMinutes(5);
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());
        
        // Register dynamic HTTP clients for enhanced load balancer
        services.AddHttpClient("Ollama-GPU0-Instance0", client =>
        {
            client.BaseAddress = new Uri("http://localhost:11434");
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        
        services.AddHttpClient("Ollama-GPU1-Instance0", client =>
        {
            client.BaseAddress = new Uri("http://localhost:11435");
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        
        // Register Load-Balanced Ollama client as IOllamaClient with conditional logic
        services.AddSingleton<LoadBalancedOllamaClient>();
        services.AddSingleton<EnhancedOllamaClient>();
        services.AddSingleton<IOllamaClient>(provider =>
        {
            var configuration = provider.GetRequiredService<IAIRESConfiguration>();
            
            if (configuration.AI.EnableGpuLoadBalancing)
            {
                // Use enhanced client with GPU detection
                return provider.GetRequiredService<EnhancedOllamaClient>();
            }
            else
            {
                // Use simple load balanced client
                return provider.GetRequiredService<LoadBalancedOllamaClient>();
            }
        });
        
        // Register Ollama Health Check client with custom factory
        services.AddHttpClient("OllamaHealthCheck", (serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IAIRESConfiguration>();
            client.BaseAddress = new Uri(config.AIServices.OllamaBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5); // Short timeout for health checks
        });
        
        services.AddSingleton<OllamaHealthCheckClient>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<IAIRESLogger>();
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("OllamaHealthCheck");
            var config = serviceProvider.GetRequiredService<IAIRESConfiguration>();
            return new OllamaHealthCheckClient(logger, httpClient, config.AIServices.OllamaBaseUrl);
        });
        
        // Register AI services with their own HTTP clients where needed
        services.AddHttpClient<MistralDocumentationService>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .AddPolicyHandler(GetRetryPolicy());
        
        // Register AI model implementations
        services.AddScoped<IErrorDocumentationAIModel, MistralDocumentationService>();
        services.AddScoped<IContextAnalyzerAIModel, DeepSeekContextService>();
        services.AddScoped<IPatternValidatorAIModel, CodeGemmaPatternService>();
        services.AddScoped<IBookletGeneratorAIModel, Gemma2BookletService>();
        
        // Register error parsers (stateless, so singleton)
        services.AddSingleton<CSharpErrorParser>();
        services.AddSingleton<MSBuildErrorParser>();
        services.AddSingleton<NetSdkErrorParser>();
        services.AddSingleton<GeneralErrorParser>();
        
        // Register error parser factory
        services.AddSingleton<ErrorParserFactory>();
        
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