using Microsoft.Extensions.DependencyInjection;
using MediatR;
using AIRES.Application.Interfaces;
using AIRES.Application.Services;
using System.Reflection;
using FluentValidation;

namespace AIRES.Application;

/// <summary>
/// Extension methods for registering AIRES Application services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds AIRES Application services to the service collection.
    /// </summary>
    public static IServiceCollection AddAIRESApplication(this IServiceCollection services)
    {
        // Register MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        
        // Register FluentValidation
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        
        // Register all Orchestrator Services
        services.AddScoped<AIResearchOrchestratorService>();
        services.AddScoped<ParallelAIResearchOrchestratorService>();
        services.AddScoped<ConcurrentAIResearchOrchestratorService>();
        
        // Register default as sequential (for backward compatibility)
        services.AddScoped<IAIResearchOrchestratorService>(provider => provider.GetRequiredService<AIResearchOrchestratorService>());
        
        // Register Factory
        services.AddScoped<IOrchestratorFactory, OrchestratorFactory>();
        
        // Register Persistence Service
        services.AddScoped<IBookletPersistenceService, BookletPersistenceService>();
        
        // Register pipeline behaviors (future: logging, validation, etc.)
        // services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        // services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        
        return services;
    }
}