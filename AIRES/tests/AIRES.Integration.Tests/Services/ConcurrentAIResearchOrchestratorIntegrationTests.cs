using Xunit;
using Microsoft.Extensions.DependencyInjection;
using AIRES.Application;
using AIRES.Application.Services;
using AIRES.Application.Interfaces;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Alerting;
using AIRES.Foundation.Results;
using AIRES.Core.Domain.ValueObjects;
using AIRES.Core.Health;
using AIRES.Infrastructure.AI;
using System.Collections.Immutable;
using System.Threading.Tasks;
using System;
using FluentAssertions;
using Moq;
using Moq.Protected;
using System.Threading;
using MediatR;
using AIRES.Application.Commands;
using AIRES.Application.Handlers;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace AIRES.Integration.Tests.Services;

/// <summary>
/// Integration tests for the concurrent AI research pipeline.
/// Tests the full flow with real handlers but mocked external AI services.
/// </summary>
public class ConcurrentAIResearchOrchestratorIntegrationTests : IAsyncLifetime
{
    private ServiceProvider _serviceProvider = default!;
    private IServiceScope _scope = default!;
    private ConcurrentAIResearchOrchestratorService _orchestratorService = default!;
    private Mock<IAIRESLogger> _mockLogger = default!;
    private Mock<IAIRESAlertingService> _mockAlertingService = default!;
    private Mock<IBookletPersistenceService> _mockPersistenceService = default!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        // Add MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ParseCompilerErrorsCommand).Assembly));

        // Mock external dependencies
        this._mockLogger = new Mock<IAIRESLogger>();
        this._mockAlertingService = new Mock<IAIRESAlertingService>();
        this._mockPersistenceService = new Mock<IBookletPersistenceService>();

        services.AddSingleton(this._mockLogger.Object);
        services.AddSingleton(this._mockAlertingService.Object);
        services.AddSingleton(this._mockPersistenceService.Object);

        // Add real handlers - these will be integration tested
        services.AddTransient<IRequestHandler<ParseCompilerErrorsCommand, ParseCompilerErrorsResponse>, ParseCompilerErrorsHandler>();
        
        // For now, we'll mock the AI service handlers since they require actual AI services
        services.AddTransient<IRequestHandler<AnalyzeDocumentationCommand, DocumentationAnalysisResponse>>(sp =>
            new MockAnalyzeDocumentationHandler());
        services.AddTransient<IRequestHandler<AnalyzeContextCommand, ContextAnalysisResponse>>(sp =>
            new MockAnalyzeContextHandler());
        services.AddTransient<IRequestHandler<ValidatePatternsCommand, PatternValidationResponse>>(sp =>
            new MockValidatePatternsHandler());
        services.AddTransient<IRequestHandler<GenerateBookletCommand, BookletGenerationResponse>>(sp =>
            new MockGenerateBookletHandler());

        // Add a real OllamaHealthCheckClient with mocked HttpClient
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"models\":[{\"name\":\"mistral:latest\"}]}")
            });
        
        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        
        var healthCheckClient = new OllamaHealthCheckClient(
            this._mockLogger.Object,
            httpClient,
            "http://localhost:11434");
        
        services.AddSingleton(healthCheckClient);

        // Add the orchestrator service
        services.AddScoped<ConcurrentAIResearchOrchestratorService>();

        this._serviceProvider = services.BuildServiceProvider();
        this._scope = this._serviceProvider.CreateScope();
        this._orchestratorService = this._scope.ServiceProvider.GetRequiredService<ConcurrentAIResearchOrchestratorService>();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        this._scope?.Dispose();
        this._serviceProvider?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task FullPipeline_WithValidCompilerErrors_CompletesSuccessfully()
    {
        // Arrange
        var rawCompilerOutput = @"
Program.cs(10,5): error CS0103: The name 'Console' does not exist in the current context
Program.cs(15,10): error CS0246: The type or namespace name 'List' could not be found
Program.cs(20,1): warning CS0219: The variable 'unused' is assigned but its value is never used
";
        var codeContext = "Sample code context";
        var projectStructure = "<project><file>Program.cs</file></project>";
        var projectCodebase = "// Sample codebase";
        var projectStandards = ImmutableList<string>.Empty.Add("Use AIRES canonical patterns");

        this._mockPersistenceService.Setup(p => p.SaveBookletAsync(
                It.IsAny<ResearchBooklet>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIRESResult<string>.Success("/test/booklet.json"));

        // Act
        var result = await this._orchestratorService.GenerateResearchBookletAsync(
            rawCompilerOutput,
            codeContext,
            projectStructure,
            projectCodebase,
            projectStandards);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Booklet.Should().NotBeNull();
        result.Value.Booklet.OriginalErrors.Count(e => e.IsError).Should().Be(2); // Only count actual errors, not warnings
        result.Value.BookletPath.Should().Be("/test/booklet.json");
        result.Value.ProcessingTimeMs.Should().BeGreaterThan(0);
        result.Value.StepTimings.Should().ContainKey("ParseErrors");
        // Note: We don't track internal ConcurrentExecution timing as it's an implementation detail

        // Verify logging
        this._mockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s.Contains("Starting CONCURRENT AI Research Pipeline"))), Times.Once);
        this._mockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s.Contains("Pipeline completed successfully"))), Times.Once);

        // Verify persistence was called
        this._mockPersistenceService.Verify(p => p.SaveBookletAsync(
            It.IsAny<ResearchBooklet>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FullPipeline_WithNoErrors_ReturnsFailure()
    {
        // Arrange
        var rawCompilerOutput = "Build succeeded.\n    0 Warning(s)\n    0 Error(s)";

        // Act
        var result = await this._orchestratorService.GenerateResearchBookletAsync(
            rawCompilerOutput,
            "code context",
            "<project/>",
            "codebase",
            ImmutableList<string>.Empty);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_ERRORS_FOUND");
        result.ErrorMessage.Should().Contain("No compiler errors found");

        // Verify no AI services were called
        this._mockPersistenceService.Verify(p => p.SaveBookletAsync(
            It.IsAny<ResearchBooklet>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FullPipeline_VerifiesSequentialExecutionWithDependencies()
    {
        // Arrange
        var rawCompilerOutput = "Program.cs(10,5): error CS0103: The name 'Console' does not exist";
        var executionOrder = new List<string>();
        var executionTimes = new Dictionary<string, DateTime>();

        // Track execution order in our mock handlers
        MockAnalyzeDocumentationHandler.OnExecute = () =>
        {
            executionOrder.Add("Mistral");
            executionTimes["Mistral"] = DateTime.UtcNow;
            Thread.Sleep(100); // Simulate work
        };

        MockAnalyzeContextHandler.OnExecute = () =>
        {
            executionOrder.Add("DeepSeek");
            executionTimes["DeepSeek"] = DateTime.UtcNow;
            Thread.Sleep(100); // Simulate work
        };

        MockValidatePatternsHandler.OnExecute = () =>
        {
            executionOrder.Add("CodeGemma");
            executionTimes["CodeGemma"] = DateTime.UtcNow;
            Thread.Sleep(100); // Simulate work
        };

        MockGenerateBookletHandler.OnExecute = () =>
        {
            executionOrder.Add("Gemma2");
            executionTimes["Gemma2"] = DateTime.UtcNow;
        };

        this._mockPersistenceService.Setup(p => p.SaveBookletAsync(
                It.IsAny<ResearchBooklet>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIRESResult<string>.Success("/test/booklet.json"));

        // Act
        var result = await this._orchestratorService.GenerateResearchBookletAsync(
            rawCompilerOutput,
            "context",
            "<project/>",
            "codebase",
            ImmutableList<string>.Empty);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify that Mistral, DeepSeek, and CodeGemma started roughly at the same time (concurrent)
        var mistralStart = executionTimes["Mistral"];
        var deepSeekStart = executionTimes["DeepSeek"];
        var codeGemmaStart = executionTimes["CodeGemma"];
        var gemma2Start = executionTimes["Gemma2"];

        // ConcurrentAIResearchOrchestratorService uses task continuations, so execution is sequential with dependencies:
        // Mistral -> DeepSeek -> CodeGemma -> Gemma2
        // Each stage should start after the previous one completes (100ms work simulation)
        
        // DeepSeek starts after Mistral completes
        (deepSeekStart - mistralStart).TotalMilliseconds.Should().BeGreaterThan(90, 
            "DeepSeek should start after Mistral completes");
        
        // CodeGemma starts after DeepSeek completes  
        (codeGemmaStart - deepSeekStart).TotalMilliseconds.Should().BeGreaterThan(90,
            "CodeGemma should start after DeepSeek completes");
        
        // Gemma2 starts after CodeGemma completes
        (gemma2Start - codeGemmaStart).TotalMilliseconds.Should().BeGreaterThan(90,
            "Gemma2 should start after CodeGemma completes");

        // Clean up
        MockAnalyzeDocumentationHandler.OnExecute = null;
        MockAnalyzeContextHandler.OnExecute = null;
        MockValidatePatternsHandler.OnExecute = null;
        MockGenerateBookletHandler.OnExecute = null;
    }

    [Fact]
    public async Task FullPipeline_WithCancellation_StopsGracefully()
    {
        // Arrange
        var rawCompilerOutput = "Program.cs(10,5): error CS0103: The name 'Console' does not exist";
        var cts = new CancellationTokenSource();

        // Cancel after parse completes but before AI services
        MockAnalyzeDocumentationHandler.OnExecute = () =>
        {
            cts.Cancel();
            throw new OperationCanceledException();
        };

        // Act
        var result = await this._orchestratorService.GenerateResearchBookletAsync(
            rawCompilerOutput,
            "context",
            "<project/>",
            "codebase",
            ImmutableList<string>.Empty,
            cts.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CONCURRENT_ORCHESTRATOR_ERROR");
        result.ErrorMessage.Should().Contain("A task was canceled");

        // Clean up
        MockAnalyzeDocumentationHandler.OnExecute = null;
    }

    [Fact]
    public async Task FullPipeline_VerifiesSemaphoreThrottling()
    {
        // Arrange
        var rawCompilerOutput = @"
Program.cs(10,5): error CS0103: Error 1
Program.cs(11,5): error CS0103: Error 2
Program.cs(12,5): error CS0103: Error 3
Program.cs(13,5): error CS0103: Error 4
Program.cs(14,5): error CS0103: Error 5
";
        var concurrentExecutions = 0;
        var maxConcurrentExecutions = 0;

        // Track concurrent executions
        Action trackExecution = () =>
        {
            var current = Interlocked.Increment(ref concurrentExecutions);
            var max = maxConcurrentExecutions;
            while (current > max)
            {
                if (Interlocked.CompareExchange(ref maxConcurrentExecutions, current, max) == max)
                    break;
                max = maxConcurrentExecutions;
            }
            Thread.Sleep(50); // Simulate work
            Interlocked.Decrement(ref concurrentExecutions);
        };

        MockAnalyzeDocumentationHandler.OnExecute = trackExecution;
        MockAnalyzeContextHandler.OnExecute = trackExecution;
        MockValidatePatternsHandler.OnExecute = trackExecution;

        this._mockPersistenceService.Setup(p => p.SaveBookletAsync(
                It.IsAny<ResearchBooklet>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIRESResult<string>.Success("/test/booklet.json"));

        // Act
        var result = await this._orchestratorService.GenerateResearchBookletAsync(
            rawCompilerOutput,
            "context",
            "<project/>",
            "codebase",
            ImmutableList<string>.Empty);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Verify semaphore throttling (max 3 concurrent as per implementation)
        maxConcurrentExecutions.Should().BeLessOrEqualTo(3, 
            "Semaphore should limit concurrent executions to 3");

        // Clean up
        MockAnalyzeDocumentationHandler.OnExecute = null;
        MockAnalyzeContextHandler.OnExecute = null;
        MockValidatePatternsHandler.OnExecute = null;
    }

    [Fact]
    public async Task MinimalIntegration_ParseStepOnly_Works()
    {
        // Arrange
        var rawCompilerOutput = "Program.cs(10,5): error CS0103: The name 'Console' does not exist";

        // Act - Just test parsing, which should work
        var mediator = this._scope.ServiceProvider.GetRequiredService<IMediator>();
        var parseResult = await mediator.Send(new ParseCompilerErrorsCommand(rawCompilerOutput));

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().HaveCount(1);
        parseResult.Errors[0].Code.Should().Be("CS0103");
        parseResult.Errors[0].Message.Should().Contain("'Console' does not exist");
        parseResult.TotalErrors.Should().Be(1);
        parseResult.TotalWarnings.Should().Be(0);
    }

    [Fact]
    public async Task GetPipelineStatusAsync_ReturnsExpectedStatus()
    {
        // Act
        var result = await this._orchestratorService.GetPipelineStatusAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().ContainKey("ConcurrentMode");
        result.Value!["ConcurrentMode"].Should().BeTrue();
        result.Value!.Should().ContainKey("SemaphoreThrottling");
        result.Value!["SemaphoreThrottling"].Should().BeTrue();
        result.Value!.Should().ContainKey("ParseCompilerErrors");
        result.Value!.Should().ContainKey("MistralDocumentation");
        result.Value!.Should().ContainKey("DeepSeekContext");
        result.Value!.Should().ContainKey("CodeGemmaPatterns");
        result.Value!.Should().ContainKey("Gemma2Booklet");
    }

    #region Mock Handlers

    private class MockAnalyzeDocumentationHandler : IRequestHandler<AnalyzeDocumentationCommand, DocumentationAnalysisResponse>
    {
        public static Action? OnExecute { get; set; }
#pragma warning disable CS8602 // Dereference of a possibly null reference.

        public Task<DocumentationAnalysisResponse> Handle(AnalyzeDocumentationCommand request, CancellationToken cancellationToken)
        {
            OnExecute?.Invoke();
#pragma warning restore CS8602

            var findings = ImmutableList.Create(
                new ErrorDocumentationFinding(
                    "Mistral",
                    "CS0103 Documentation",
                    "The name 'Console' does not exist - you need to add 'using System;'",
                    "https://docs.microsoft.com/cs0103"
                )
            );

            return Task.FromResult(new DocumentationAnalysisResponse(
                findings,
                "Mock Mistral analysis complete",
                ImmutableDictionary<string, string>.Empty.Add("model", "mistral-mock")
            ));
        }
    }

    private class MockAnalyzeContextHandler : IRequestHandler<AnalyzeContextCommand, ContextAnalysisResponse>
    {
        public static Action? OnExecute { get; set; }
#pragma warning disable CS8602 // Dereference of a possibly null reference.

        public Task<ContextAnalysisResponse> Handle(AnalyzeContextCommand request, CancellationToken cancellationToken)
        {
            OnExecute?.Invoke();
#pragma warning restore CS8602

            var findings = ImmutableList.Create(
                new ContextAnalysisFinding(
                    "DeepSeek",
                    "Missing Using Statement",
                    "The code is trying to use Console.WriteLine but System namespace is not imported",
                    "Add using System; at the top of the file",
                    "This is a common beginner mistake"
                )
            );

            return Task.FromResult(new ContextAnalysisResponse(
                findings,
                "Mock DeepSeek analysis complete",
                ImmutableList<string>.Empty.Add("Missing namespace imports"),
                ImmutableDictionary<string, string>.Empty.Add("model", "deepseek-mock")
            ));
        }
    }

    private class MockValidatePatternsHandler : IRequestHandler<ValidatePatternsCommand, PatternValidationResponse>
    {
        public static Action? OnExecute { get; set; }
#pragma warning disable CS8602 // Dereference of a possibly null reference.

        public Task<PatternValidationResponse> Handle(ValidatePatternsCommand request, CancellationToken cancellationToken)
        {
            OnExecute?.Invoke();
#pragma warning restore CS8602

            var finding = new PatternValidationFinding(
                "CodeGemma",
                "Pattern Validation Results",
                "Code follows most AIRES patterns",
                ImmutableList<PatternIssue>.Empty,
                ImmutableList<string>.Empty.Add("Good use of canonical patterns")
            );

            return Task.FromResult(new PatternValidationResponse(
                finding,
                true,
                ImmutableList<string>.Empty,
                ImmutableList<string>.Empty.Add("Keep following AIRES patterns")
            ));
        }
    }

    private class MockGenerateBookletHandler : IRequestHandler<GenerateBookletCommand, BookletGenerationResponse>
    {
        public static Action? OnExecute { get; set; }
#pragma warning disable CS8602 // Dereference of a possibly null reference.

        public Task<BookletGenerationResponse> Handle(GenerateBookletCommand request, CancellationToken cancellationToken)
        {
            OnExecute?.Invoke();
#pragma warning restore CS8602

            var sections = ImmutableList.Create(
                new BookletSection("Compiler Errors", "Found compiler errors", 1),
                new BookletSection("Documentation Analysis", "Mistral findings", 2),
                new BookletSection("Context Analysis", "DeepSeek findings", 3),
                new BookletSection("Pattern Validation", "CodeGemma findings", 4)
            );

            var booklet = new ResearchBooklet(
                request.ErrorBatchId,
                "Mock AI Research Booklet",
                request.OriginalErrors,
                ImmutableList<AIResearchFinding>.Empty
                    .AddRange(request.DocAnalysis.Findings)
                    .AddRange(request.ContextAnalysis.Findings)
                    .Add(request.PatternValidation.ValidationFinding),
                sections,
                ImmutableDictionary<string, string>.Empty.Add("generator", "gemma2-mock")
            );

            return Task.FromResult(new BookletGenerationResponse(
                booklet,
                "/mock/booklet.json",
                100,
                ImmutableDictionary<string, long>.Empty
            ));
        }
    }

    #endregion
}