using System;
using AIRES.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AIRES.CLI.Health;

/// <summary>
/// Represents a scoped service with its scope for proper disposal.
/// </summary>
public interface IScopedService<T> : IDisposable
{
    T Service { get; }
}

/// <summary>
/// Factory for creating scoped IAIResearchOrchestratorService instances.
/// </summary>
public interface IAIResearchOrchestratorServiceFactory
{
    IScopedService<IAIResearchOrchestratorService> CreateScoped();
}

/// <summary>
/// Factory for creating scoped IBookletPersistenceService instances.
/// </summary>
public interface IBookletPersistenceServiceFactory
{
    IScopedService<IBookletPersistenceService> CreateScoped();
}

/// <summary>
/// Implementation of IScopedService.
/// </summary>
internal class ScopedService<T> : IScopedService<T>
{
    private readonly IServiceScope _scope;
    
    public T Service { get; }
    
    public ScopedService(IServiceScope scope, T service)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Service = service ?? throw new ArgumentNullException(nameof(service));
    }
    
    public void Dispose()
    {
        _scope.Dispose();
    }
}

/// <summary>
/// Implementation of IAIResearchOrchestratorServiceFactory.
/// </summary>
public class AIResearchOrchestratorServiceFactory : IAIResearchOrchestratorServiceFactory
{
    private readonly IServiceProvider _serviceProvider;

    public AIResearchOrchestratorServiceFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IScopedService<IAIResearchOrchestratorService> CreateScoped()
    {
        var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAIResearchOrchestratorService>();
        return new ScopedService<IAIResearchOrchestratorService>(scope, service);
    }
}

/// <summary>
/// Implementation of IBookletPersistenceServiceFactory.
/// </summary>
public class BookletPersistenceServiceFactory : IBookletPersistenceServiceFactory
{
    private readonly IServiceProvider _serviceProvider;

    public BookletPersistenceServiceFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IScopedService<IBookletPersistenceService> CreateScoped()
    {
        var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookletPersistenceService>();
        return new ScopedService<IBookletPersistenceService>(scope, service);
    }
}