using Xunit;
using Moq;
using MediatR;
using AIRES.Application.Commands;
using AIRES.Application.Services;
using AIRES.Application.Exceptions;
using AIRES.Application.Interfaces;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;
using AIRES.Foundation.Alerting;
using AIRES.Infrastructure.AI;
using AIRES.Core.Health;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using Moq.Protected;
using AIRES.Core.Domain.ValueObjects;

namespace AIRES.Application.Tests.Services;

public class ConcurrentAIResearchOrchestratorServiceTests : IDisposable
{
    private readonly Mock<IAIRESLogger> _mockLogger;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IBookletPersistenceService> _mockPersistenceService;
    private readonly Mock<IAIRESAlertingService> _mockAlertingService;
    private readonly OllamaHealthCheckClient _healthCheckClient;
    private readonly ConcurrentAIResearchOrchestratorService _service;

    public ConcurrentAIResearchOrchestratorServiceTests()
    {
        this._mockLogger = new Mock<IAIRESLogger>();
        this._mockMediator = new Mock<IMediator>();
        this._mockPersistenceService = new Mock<IBookletPersistenceService>();
        this._mockAlertingService = new Mock<IAIRESAlertingService>();

        // Create a mocked HttpClient for OllamaHealthCheckClient
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
        
        this._healthCheckClient = new OllamaHealthCheckClient(
            this._mockLogger.Object,
            httpClient,
            "http://localhost:11434");

        this._service = new ConcurrentAIResearchOrchestratorService(
            this._mockLogger.Object,
            this._mockMediator.Object,
            this._mockPersistenceService.Object,
            this._mockAlertingService.Object,
            this._healthCheckClient);
    }

    public void Dispose()
    {
        this._service?.Dispose();
        this._healthCheckClient?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_WithNoErrors_ReturnsFailure()
    {
        // Arrange
        var parseResult = new ParseCompilerErrorsResponse(
            ImmutableList<CompilerError>.Empty,
            "No errors found",
            0, 0);

        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parseResult);

        // Act
        var result = await this._service.GenerateResearchBookletAsync(
            "raw compiler output",
            "code context",
            "<project/>",
            "project codebase",
            ImmutableList<string>.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("NO_ERRORS_FOUND", result.ErrorCode);
        Assert.Equal("No compiler errors found in the provided output", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_SuccessfulPipeline_ReturnsSuccess()
    {
        // Arrange
        var errors = ImmutableList.Create(CreateTestError());
        
        var parseResult = new ParseCompilerErrorsResponse(errors, "Found 1 error", 1, 0);
        var docAnalysis = CreateTestDocumentationAnalysis();
        var contextAnalysis = CreateTestContextAnalysis();
        var patternValidation = CreateTestPatternValidation();
        var booklet = CreateTestBooklet(errors, docAnalysis, contextAnalysis, patternValidation);
        var bookletResponse = new BookletGenerationResponse(
            booklet,
            "/path/to/booklet.json",
            1000,
            ImmutableDictionary<string, long>.Empty);

        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parseResult);
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeDocumentationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(docAnalysis);
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeContextCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contextAnalysis);
        this._mockMediator.Setup(m => m.Send(It.IsAny<ValidatePatternsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(patternValidation);
        this._mockMediator.Setup(m => m.Send(It.IsAny<GenerateBookletCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookletResponse);

        this._mockPersistenceService.Setup(p => p.SaveBookletAsync(It.IsAny<ResearchBooklet>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIRESResult<string>.Success("/saved/path/booklet.json"));

        // Act
        var result = await this._service.GenerateResearchBookletAsync(
            "raw compiler output",
            "code context",
            "<project/>",
            "project codebase",
            ImmutableList<string>.Empty);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("/saved/path/booklet.json", result.Value.BookletPath);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_MistralFailure_ReturnsFailureAndRaisesAlert()
    {
        // Arrange
        var errors = ImmutableList.Create(CreateTestError());
        var parseResult = new ParseCompilerErrorsResponse(errors, "Found 1 error", 1, 0);

        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parseResult);
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeDocumentationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MistralAnalysisFailedException("Mistral error", "MISTRAL_ERROR"));

        // Act
        var result = await this._service.GenerateResearchBookletAsync(
            "raw compiler output",
            "code context",
            "<project/>",
            "project codebase",
            ImmutableList<string>.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        // Concurrent orchestrator wraps exceptions in CONCURRENT_ORCHESTRATOR_ERROR
        Assert.Equal("CONCURRENT_ORCHESTRATOR_ERROR", result.ErrorCode);
        
        // Verify alert was raised
        this._mockAlertingService.Verify(a => a.RaiseAlertAsync(
            AlertSeverity.Warning,
            "ConcurrentAIResearchOrchestratorService",
            It.Is<string>(s => s.Contains("Mistral analysis failed")),
            It.IsAny<Dictionary<string, object>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_DeepSeekFailure_ReturnsFailure()
    {
        // Arrange
        var errors = ImmutableList.Create(CreateTestError());
        var parseResult = new ParseCompilerErrorsResponse(errors, "Found 1 error", 1, 0);
        var docAnalysis = CreateTestDocumentationAnalysis();

        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parseResult);
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeDocumentationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(docAnalysis);
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeContextCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DeepSeekContextAnalysisException("DeepSeek error", "DEEPSEEK_ERROR"));

        // Act
        var result = await this._service.GenerateResearchBookletAsync(
            "raw compiler output",
            "code context",
            "<project/>",
            "project codebase",
            ImmutableList<string>.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        // Due to task continuations, exceptions are wrapped
        Assert.Equal("CONCURRENT_ORCHESTRATOR_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_CodeGemmaFailure_ReturnsFailure()
    {
        // Arrange
        var errors = ImmutableList.Create(CreateTestError());
        var parseResult = new ParseCompilerErrorsResponse(errors, "Found 1 error", 1, 0);
        var docAnalysis = CreateTestDocumentationAnalysis();
        var contextAnalysis = CreateTestContextAnalysis();

        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parseResult);
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeDocumentationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(docAnalysis);
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeContextCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contextAnalysis);
        this._mockMediator.Setup(m => m.Send(It.IsAny<ValidatePatternsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CodeGemmaValidationException("CodeGemma error", "CODEGEMMA_ERROR"));

        // Act
        var result = await this._service.GenerateResearchBookletAsync(
            "raw compiler output",
            "code context",
            "<project/>",
            "project codebase",
            ImmutableList<string>.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        // Due to task continuations, exceptions are wrapped
        Assert.Equal("CONCURRENT_ORCHESTRATOR_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_Gemma2Failure_ReturnsFailureAndRaisesCriticalAlert()
    {
        // Arrange
        var errors = ImmutableList.Create(CreateTestError());
        var parseResult = new ParseCompilerErrorsResponse(errors, "Found 1 error", 1, 0);
        var docAnalysis = CreateTestDocumentationAnalysis();
        var contextAnalysis = CreateTestContextAnalysis();
        var patternValidation = CreateTestPatternValidation();

        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parseResult);
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeDocumentationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(docAnalysis);
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeContextCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contextAnalysis);
        this._mockMediator.Setup(m => m.Send(It.IsAny<ValidatePatternsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(patternValidation);
        this._mockMediator.Setup(m => m.Send(It.IsAny<GenerateBookletCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Gemma2GenerationException("Gemma2 error", "GEMMA2_ERROR"));

        // Act
        var result = await this._service.GenerateResearchBookletAsync(
            "raw compiler output",
            "code context",
            "<project/>",
            "project codebase",
            ImmutableList<string>.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("GEMMA2_ERROR", result.ErrorCode);
        
        // Verify critical alert was raised
        this._mockAlertingService.Verify(a => a.RaiseAlertAsync(
            AlertSeverity.Critical,
            "ConcurrentAIResearchOrchestratorService",
            It.Is<string>(s => s.Contains("Gemma2 booklet generation failed")),
            It.IsAny<Dictionary<string, object>>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_BookletSaveFailure_ReturnsFailure()
    {
        // Arrange
        var errors = ImmutableList.Create(CreateTestError());
        var parseResult = new ParseCompilerErrorsResponse(errors, "Found 1 error", 1, 0);
        var docAnalysis = CreateTestDocumentationAnalysis();
        var contextAnalysis = CreateTestContextAnalysis();
        var patternValidation = CreateTestPatternValidation();
        var booklet = CreateTestBooklet(errors, docAnalysis, contextAnalysis, patternValidation);
        var bookletResponse = new BookletGenerationResponse(
            booklet,
            "/path/to/booklet.json",
            1000,
            ImmutableDictionary<string, long>.Empty);

        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parseResult);
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeDocumentationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(docAnalysis);
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeContextCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contextAnalysis);
        this._mockMediator.Setup(m => m.Send(It.IsAny<ValidatePatternsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(patternValidation);
        this._mockMediator.Setup(m => m.Send(It.IsAny<GenerateBookletCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookletResponse);

        this._mockPersistenceService.Setup(p => p.SaveBookletAsync(It.IsAny<ResearchBooklet>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIRESResult<string>.Failure("SAVE_ERROR", "Failed to save booklet"));

        // Act
        var result = await this._service.GenerateResearchBookletAsync(
            "raw compiler output",
            "code context",
            "<project/>",
            "project codebase",
            ImmutableList<string>.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        // The error code comes from the mock return value
        Assert.Equal("SAVE_ERROR", result.ErrorCode);
        Assert.Equal("Failed to save booklet: Failed to save booklet", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_WithCancellation_ReturnsFailure()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await this._service.GenerateResearchBookletAsync(
            "raw compiler output",
            "code context",
            "<project/>",
            "project codebase",
            ImmutableList<string>.Empty,
            cts.Token);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("CONCURRENT_ORCHESTRATOR_ERROR", result.ErrorCode);
        Assert.Contains("operation was canceled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_UnexpectedError_ReturnsFailureAndRaisesCriticalAlert()
    {
        // Arrange
        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        // Act
        var result = await this._service.GenerateResearchBookletAsync(
            "raw compiler output",
            "code context",
            "<project/>",
            "project codebase",
            ImmutableList<string>.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("CONCURRENT_ORCHESTRATOR_ERROR", result.ErrorCode);
        Assert.Contains("Unexpected error", result.ErrorMessage);

        // Verify critical alert was raised
        this._mockAlertingService.Verify(a => a.RaiseAlertAsync(
            AlertSeverity.Critical,
            "ConcurrentAIResearchOrchestratorService",
            It.Is<string>(s => s.Contains("Concurrent orchestration failed unexpectedly")),
            It.IsAny<Dictionary<string, object>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPipelineStatusAsync_Success_ReturnsHealthStatus()
    {
        // Act
        var result = await this._service.GetPipelineStatusAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value["ParseCompilerErrors"]);
        Assert.True(result.Value["ConcurrentMode"]);
        Assert.True(result.Value["SemaphoreThrottling"]);
        Assert.Contains("MistralDocumentation", result.Value.Keys);
        Assert.Contains("DeepSeekContext", result.Value.Keys);
        Assert.Contains("CodeGemmaPatterns", result.Value.Keys);
        Assert.Contains("Gemma2Booklet", result.Value.Keys);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_VerifiesLoggingCalls()
    {
        // Arrange
        var errors = ImmutableList.Create(CreateTestError());
        var parseResult = new ParseCompilerErrorsResponse(errors, "Found 1 error", 1, 0);

        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parseResult);
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeDocumentationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MistralAnalysisFailedException("Test error", "TEST_ERROR"));

        // Act
        await this._service.GenerateResearchBookletAsync(
            "raw compiler output",
            "code context",
            "<project/>",
            "project codebase",
            ImmutableList<string>.Empty);

        // Assert
        this._mockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s.Contains("Starting CONCURRENT AI Research Pipeline"))), Times.Once);
        this._mockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s.Contains("Parsing compiler errors"))), Times.Once);
        this._mockLogger.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_VerifiesDependencyChain_DeepSeekNotRunIfMistralFails()
    {
        // Arrange
        var errors = ImmutableList.Create(CreateTestError());
        var parseResult = new ParseCompilerErrorsResponse(errors, "Found 1 error", 1, 0);

        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parseResult);
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeDocumentationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MistralAnalysisFailedException("Mistral failed", "MISTRAL_ERROR"));

        // Act
        var result = await this._service.GenerateResearchBookletAsync(
            "raw compiler output",
            "code context",
            "<project/>",
            "project codebase",
            ImmutableList<string>.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        
        // Verify that DeepSeek, CodeGemma, and Gemma2 were NOT called
        this._mockMediator.Verify(m => m.Send(It.IsAny<AnalyzeContextCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        this._mockMediator.Verify(m => m.Send(It.IsAny<ValidatePatternsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        this._mockMediator.Verify(m => m.Send(It.IsAny<GenerateBookletCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_VerifiesDependencyChain_CodeGemmaNotRunIfDeepSeekFails()
    {
        // Arrange
        var errors = ImmutableList.Create(CreateTestError());
        var parseResult = new ParseCompilerErrorsResponse(errors, "Found 1 error", 1, 0);
        var docAnalysis = CreateTestDocumentationAnalysis();

        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parseResult);
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeDocumentationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(docAnalysis);
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeContextCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DeepSeekContextAnalysisException("DeepSeek failed", "DEEPSEEK_ERROR"));

        // Act
        var result = await this._service.GenerateResearchBookletAsync(
            "raw compiler output",
            "code context",
            "<project/>",
            "project codebase",
            ImmutableList<string>.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        
        // Verify that CodeGemma and Gemma2 were NOT called
        this._mockMediator.Verify(m => m.Send(It.IsAny<ValidatePatternsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        this._mockMediator.Verify(m => m.Send(It.IsAny<GenerateBookletCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // TODO: Add test for semaphore throttling
    // TODO: Add test for retry logic
    // TODO: Add test for progress reporting

    #region Test Helpers

    private static CompilerError CreateTestError(string code = "CS0001", string message = "Test error")
    {
        var location = new ErrorLocation("Test.cs", 1, 1);
        return new CompilerError(code, message, "Error", location, $"{location}: error {code}: {message}");
    }

    private static DocumentationAnalysisResponse CreateTestDocumentationAnalysis()
    {
        return new DocumentationAnalysisResponse(
            ImmutableList<ErrorDocumentationFinding>.Empty,
            "Summary",
            ImmutableDictionary<string, string>.Empty);
    }

    private static ContextAnalysisResponse CreateTestContextAnalysis()
    {
        return new ContextAnalysisResponse(
            ImmutableList<ContextAnalysisFinding>.Empty,
            "Analysis summary",
            ImmutableList<string>.Empty,
            ImmutableDictionary<string, string>.Empty);
    }

    private static PatternValidationResponse CreateTestPatternValidation()
    {
        var finding = new PatternValidationFinding(
            "CodeGemma",
            "Test Pattern Validation",
            "No pattern violations found",
            ImmutableList<PatternIssue>.Empty,
            ImmutableList<string>.Empty);
        return new PatternValidationResponse(
            finding,
            true,
            ImmutableList<string>.Empty,
            ImmutableList<string>.Empty);
    }

    private static ResearchBooklet CreateTestBooklet(
        IImmutableList<CompilerError> errors,
        DocumentationAnalysisResponse docAnalysis,
        ContextAnalysisResponse contextAnalysis,
        PatternValidationResponse patternValidation)
    {
        var findings = ImmutableList<AIResearchFinding>.Empty
            .AddRange(docAnalysis.Findings)
            .AddRange(contextAnalysis.Findings)
            .Add(patternValidation.ValidationFinding);

        var sections = ImmutableList.Create(
            new BookletSection("Compiler Errors",
                $"Found {errors.Count} compiler errors", 1),
            new BookletSection("Documentation Analysis",
                docAnalysis.OverallInsights, 2),
            new BookletSection("Context Analysis",
                contextAnalysis.DeepCodeUnderstanding, 3),
            new BookletSection("Pattern Validation",
                patternValidation.ValidationFinding.Content, 4)
        );

        var metadata = ImmutableDictionary<string, string>.Empty
            .Add("GeneratedBy", "AIRES")
            .Add("Version", "1.0.0")
            .Add("ProcessingTimeMs", "1000");

        return new ResearchBooklet(
            Guid.NewGuid(),
            "AI Error Resolution Research Booklet",
            errors,
            findings,
            sections,
            metadata);
    }

    #endregion
}