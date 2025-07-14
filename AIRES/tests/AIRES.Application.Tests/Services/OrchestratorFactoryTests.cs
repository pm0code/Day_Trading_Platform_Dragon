using Xunit;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using AIRES.Application.Services;
using AIRES.Application.Interfaces;
using System;

namespace AIRES.Application.Tests.Services;

public class OrchestratorFactoryTests
{
    [Fact]
    public void CreateOrchestrator_WithUseParallelFalse_ReturnsSequentialOrchestrator()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockSequentialOrchestrator = new Mock<AIResearchOrchestratorService>(
            new Mock<Foundation.Logging.IAIRESLogger>().Object,
            new Mock<MediatR.IMediator>().Object,
            new Mock<IBookletPersistenceService>().Object);

        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(AIResearchOrchestratorService)))
            .Returns(mockSequentialOrchestrator.Object);

        var factory = new OrchestratorFactory(mockServiceProvider.Object);

        // Act
        var result = factory.CreateOrchestrator(useParallel: false);

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<AIResearchOrchestratorService>(result);
        mockServiceProvider.Verify(sp => sp.GetService(typeof(AIResearchOrchestratorService)), Times.Once);
        mockServiceProvider.Verify(sp => sp.GetService(typeof(ConcurrentAIResearchOrchestratorService)), Times.Never);
    }

    [Fact]
    public void CreateOrchestrator_WithUseParallelTrue_ReturnsConcurrentOrchestrator()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockConcurrentOrchestrator = new Mock<ConcurrentAIResearchOrchestratorService>(
            new Mock<Foundation.Logging.IAIRESLogger>().Object,
            new Mock<MediatR.IMediator>().Object,
            new Mock<IBookletPersistenceService>().Object,
            new Mock<Foundation.Alerting.IAIRESAlertingService>().Object);

        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(ConcurrentAIResearchOrchestratorService)))
            .Returns(mockConcurrentOrchestrator.Object);

        var factory = new OrchestratorFactory(mockServiceProvider.Object);

        // Act
        var result = factory.CreateOrchestrator(useParallel: true);

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<ConcurrentAIResearchOrchestratorService>(result);
        mockServiceProvider.Verify(sp => sp.GetService(typeof(ConcurrentAIResearchOrchestratorService)), Times.Once);
        mockServiceProvider.Verify(sp => sp.GetService(typeof(AIResearchOrchestratorService)), Times.Never);
    }

    [Fact]
    public void CreateOrchestrator_WhenServiceNotRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider
            .Setup(sp => sp.GetService(It.IsAny<Type>()))
            .Returns((object?)null);

        var factory = new OrchestratorFactory(mockServiceProvider.Object);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => factory.CreateOrchestrator(useParallel: false));
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_DoesNotThrow()
    {
        // Arrange & Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var factory = new OrchestratorFactory(null!);
#pragma warning restore CS8625
        
        // Assert - constructor doesn't throw, but CreateOrchestrator will
        Assert.NotNull(factory);
    }
}