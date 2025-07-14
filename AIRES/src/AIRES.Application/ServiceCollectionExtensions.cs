using Microsoft.Extensions.DependencyInjection;
using MediatR;
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
        
        // Register Orchestrator Service
        services.AddScoped<AIResearchOrchestratorService>();
        
        // Register Persistence Service
        services.AddScoped<BookletPersistenceService>();
        
        // Register pipeline behaviors (future: logging, validation, etc.)
        // services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        // services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        
        return services;
    }
}