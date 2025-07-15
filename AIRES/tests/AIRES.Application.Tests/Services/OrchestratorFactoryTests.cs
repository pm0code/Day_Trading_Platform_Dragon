using Xunit;
using Microsoft.Extensions.DependencyInjection;
using AIRES.Application.Services;
using AIRES.Application.Interfaces;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Alerting;
using AIRES.Infrastructure.AI;
using AIRES.TestInfrastructure;
using System;
using System.Net;
using MediatR;

namespace AIRES.Application.Tests.Services;

/// <summary>
/// Unit tests for OrchestratorFactory using REAL implementations.
/// NO MOCKS - following AIRES zero mock implementation policy.
/// </summary>
public class OrchestratorFactoryTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly OrchestratorFactory _factory;
    private readonly TestCaptureLogger _logger;
    
    public OrchestratorFactoryTests()
    {
        // Configure test services with real implementations
        var httpHandler = new TestHttpMessageHandler()
            .When(HttpMethod.Get, "http://test.ollama.localhost:11434/")
            .RespondWith(HttpStatusCode.OK, "Ollama is running")
            .DefaultResponse(HttpStatusCode.NotFound);
            
        var testConfig = new TestConfiguration
        {
            OllamaBaseUrl = "http://test.ollama.localhost:11434",
            HttpMessageHandler = httpHandler,
            UseConsoleAlerting = true
        };
        
        _serviceProvider = TestCompositionRoot.ConfigureTestServices(testConfig, services =>
        {
            // Register orchestrator services
            services.AddScoped<AIResearchOrchestratorService>();
            services.AddScoped<ConcurrentAIResearchOrchestratorService>();
            services.AddScoped<ParallelAIResearchOrchestratorService>();
            services.AddScoped<IOrchestratorFactory, OrchestratorFactory>();
            
            // Register MediatR (required by orchestrators)
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AIRES.Application.Commands.ParseCompilerErrorsCommand).Assembly));
        });
        
        _factory = _serviceProvider.GetRequiredService<IOrchestratorFactory>();
        _logger = (TestCaptureLogger)_serviceProvider.GetRequiredService<IAIRESLogger>();
    }
    
    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        GC.SuppressFinalize(this);
    }
    
    [Fact]
    public void CreateOrchestrator_WithUseParallelFalse_ReturnsSequentialOrchestrator()
    {
        // Act
        var result = _factory.CreateOrchestrator(useParallel: false);
        
        // Assert
        Assert.NotNull(result);
        Assert.IsType<AIResearchOrchestratorService>(result);
        
        // Verify we can call a method on the returned service
        var status = result.GetPipelineStatusAsync().Result;
        Assert.True(status.IsSuccess);
        Assert.False(status.Value["ParallelMode"]);
    }
    
    [Fact]
    public void CreateOrchestrator_WithUseParallelTrue_ReturnsConcurrentOrchestrator()
    {
        // Act
        var result = _factory.CreateOrchestrator(useParallel: true);
        
        // Assert
        Assert.NotNull(result);
        Assert.IsType<ConcurrentAIResearchOrchestratorService>(result);
        
        // Verify we can call a method on the returned service
        var status = result.GetPipelineStatusAsync().Result;
        Assert.True(status.IsSuccess);
        Assert.True(status.Value["ConcurrentMode"]);
    }
    
    [Fact]
    public void CreateOrchestrator_MultipleCallsWithSameSetting_ReturnsDifferentInstances()
    {
        // Act
        var result1 = _factory.CreateOrchestrator(useParallel: false);
        var result2 = _factory.CreateOrchestrator(useParallel: false);
        
        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotSame(result1, result2); // Different instances
        Assert.IsType<AIResearchOrchestratorService>(result1);
        Assert.IsType<AIResearchOrchestratorService>(result2);
    }
    
    [Fact]
    public void CreateOrchestrator_AlternatingCalls_ReturnsCorrectTypes()
    {
        // Act & Assert - Sequential
        var sequential = _factory.CreateOrchestrator(useParallel: false);
        Assert.NotNull(sequential);
        Assert.IsType<AIResearchOrchestratorService>(sequential);
        
        // Act & Assert - Concurrent
        var concurrent = _factory.CreateOrchestrator(useParallel: true);
        Assert.NotNull(concurrent);
        Assert.IsType<ConcurrentAIResearchOrchestratorService>(concurrent);
        
        // Act & Assert - Sequential again
        var sequential2 = _factory.CreateOrchestrator(useParallel: false);
        Assert.NotNull(sequential2);
        Assert.IsType<AIResearchOrchestratorService>(sequential2);
        Assert.NotSame(sequential, sequential2); // Different instances
    }
    
    [Fact]
    public void CreateOrchestrator_VerifyDependenciesAreInjected()
    {
        // Arrange
        _logger.Clear();
        
        // Act - Create orchestrator and use it
        var orchestrator = _factory.CreateOrchestrator(useParallel: false);
        var statusResult = orchestrator.GetPipelineStatusAsync().Result;
        
        // Assert
        Assert.True(statusResult.IsSuccess);
        
        // Verify logging happened (proves IAIRESLogger was injected)
        Assert.True(_logger.LogEntries.Count > 0);
        Assert.True(_logger.ContainsMessage("Entering method"));
        Assert.True(_logger.ContainsMessage("Exiting method"));
    }
    
    [Fact]
    public void OrchestratorFactory_WithNullServiceProvider_ThrowsOnCreate()
    {
        // Arrange
        var factoryWithNullProvider = new OrchestratorFactory(null!);
        
        // Act & Assert
        Assert.Throws<NullReferenceException>(() => factoryWithNullProvider.CreateOrchestrator(useParallel: false));
    }
    
    [Fact]
    public void CreateOrchestrator_WhenServiceNotRegistered_ThrowsInvalidOperationException()
    {
        // Arrange - Create a minimal service provider without orchestrators
        var minimalServices = new ServiceCollection();
        minimalServices.AddSingleton<IAIRESLogger, TestCaptureLogger>();
        var minimalProvider = minimalServices.BuildServiceProvider();
        var minimalFactory = new OrchestratorFactory(minimalProvider);
        
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => minimalFactory.CreateOrchestrator(useParallel: false));
        Assert.Contains("Unable to resolve", exception.Message);
    }
    
    [Fact]
    public void OrchestratorFactory_ImplementsInterfaceCorrectly()
    {
        // Assert
        Assert.IsAssignableFrom<IOrchestratorFactory>(_factory);
        
        // Verify the interface method works
        IAIResearchOrchestratorService orchestrator = _factory.CreateOrchestrator(false);
        Assert.NotNull(orchestrator);
    }
}