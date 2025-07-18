using AIRES.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AIRES.Application.Services;

/// <summary>
/// Factory for creating the appropriate orchestrator based on execution mode.
/// </summary>
public class OrchestratorFactory : IOrchestratorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public OrchestratorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IAIResearchOrchestratorService CreateOrchestrator(bool useParallel)
    {
        if (useParallel)
        {
            // Use the properly designed concurrent orchestrator instead of the naive parallel one
            return _serviceProvider.GetRequiredService<ConcurrentAIResearchOrchestratorService>();
        }
        else
        {
            return _serviceProvider.GetRequiredService<AIResearchOrchestratorService>();
        }
    }
}

/// <summary>
/// Factory interface for creating orchestrators.
/// </summary>
public interface IOrchestratorFactory
{
    IAIResearchOrchestratorService CreateOrchestrator(bool useParallel);
}