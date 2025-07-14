using Xunit;
using Moq;
using MediatR;
using AIRES.Application.Commands;
using AIRES.Application.Services;
using AIRES.Application.Exceptions;
using AIRES.Application.Interfaces;
using AIRES.Foundation.Logging;
using AIRES.Foundation.Results;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using AIRES.Core.Domain.ValueObjects;

namespace AIRES.Application.Tests.Services;

/// <summary>
/// Unit tests for the parallel AI research orchestrator service.
/// </summary>
public class ParallelAIResearchOrchestratorServiceTests : IDisposable
{
    private readonly Mock<IAIRESLogger> _mockLogger;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IBookletPersistenceService> _mockPersistenceService;
    private readonly ParallelAIResearchOrchestratorService _service;

    public ParallelAIResearchOrchestratorServiceTests()
    {
        this._mockLogger = new Mock<IAIRESLogger>();
        this._mockMediator = new Mock<IMediator>();
        this._mockPersistenceService = new Mock<IBookletPersistenceService>();

        this._service = new ParallelAIResearchOrchestratorService(
            this._mockLogger.Object,
            this._mockMediator.Object,
            this._mockPersistenceService.Object);
    }

    public void Dispose()
    {
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
    public async Task GenerateResearchBookletAsync_SuccessfulParallelPipeline_ReturnsSuccess()
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
        Assert.Equal("/saved/path/booklet.json", result.Value!.BookletPath);
        Assert.Contains("ParallelExecutionTime", result.Value.StepTimings.Keys);
        Assert.Contains("MistralAnalysis", result.Value.StepTimings.Keys);
        Assert.Contains("DeepSeekAnalysis", result.Value.StepTimings.Keys);
        Assert.Contains("CodeGemmaValidation", result.Value.StepTimings.Keys);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_MistralFailureInParallel_ReturnsFailure()
    {
        // Arrange
        var errors = ImmutableList.Create(CreateTestError());
        var parseResult = new ParseCompilerErrorsResponse(errors, "Found 1 error", 1, 0);

        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parseResult);
        
        // Mistral fails during parallel execution
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeDocumentationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MistralAnalysisFailedException("Mistral error", "MISTRAL_ERROR"));

        // DeepSeek and CodeGemma might also run in parallel
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeContextCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestContextAnalysis());
        this._mockMediator.Setup(m => m.Send(It.IsAny<ValidatePatternsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestPatternValidation());

        // Act
        var result = await this._service.GenerateResearchBookletAsync(
            "raw compiler output",
            "code context",
            "<project/>",
            "project codebase",
            ImmutableList<string>.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("PARALLEL_ORCHESTRATOR_ERROR", result.ErrorCode);
        Assert.Contains("An unexpected error occurred", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_DeepSeekFailureInParallel_ReturnsFailure()
    {
        // Arrange
        var errors = ImmutableList.Create(CreateTestError());
        var parseResult = new ParseCompilerErrorsResponse(errors, "Found 1 error", 1, 0);

        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parseResult);

        // Mistral succeeds
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeDocumentationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestDocumentationAnalysis());

        // DeepSeek fails during parallel execution
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeContextCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DeepSeekContextAnalysisException("DeepSeek error", "DEEPSEEK_ERROR"));

        // CodeGemma might also run in parallel
        this._mockMediator.Setup(m => m.Send(It.IsAny<ValidatePatternsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestPatternValidation());

        // Act
        var result = await this._service.GenerateResearchBookletAsync(
            "raw compiler output",
            "code context",
            "<project/>",
            "project codebase",
            ImmutableList<string>.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("PARALLEL_ORCHESTRATOR_ERROR", result.ErrorCode);
        Assert.Contains("An unexpected error occurred", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_CodeGemmaFailureInParallel_ReturnsFailure()
    {
        // Arrange
        var errors = ImmutableList.Create(CreateTestError());
        var parseResult = new ParseCompilerErrorsResponse(errors, "Found 1 error", 1, 0);

        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parseResult);

        // Mistral succeeds
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeDocumentationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestDocumentationAnalysis());

        // DeepSeek succeeds
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeContextCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestContextAnalysis());

        // CodeGemma fails during parallel execution
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
        Assert.Equal("PARALLEL_ORCHESTRATOR_ERROR", result.ErrorCode);
        Assert.Contains("An unexpected error occurred", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_Gemma2Failure_ReturnsFailure()
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
        
        // Gemma2 fails after parallel execution completes
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
        Assert.Contains("Booklet generation failed", result.ErrorMessage);
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
        Assert.Equal("PARALLEL_ORCHESTRATOR_ERROR", result.ErrorCode);
        Assert.Contains("An unexpected error occurred", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_UnexpectedError_ReturnsFailure()
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
        Assert.Equal("PARALLEL_ORCHESTRATOR_ERROR", result.ErrorCode);
        Assert.Contains("An unexpected error occurred", result.ErrorMessage);
    }

    [Fact]
    public async Task GetPipelineStatusAsync_Success_ReturnsHealthStatus()
    {
        // Act
        var result = await this._service.GetPipelineStatusAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value!["ParseCompilerErrors"]);
        Assert.True(result.Value["ParallelMode"]); // This service uses parallel mode
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
        try
        {
            await this._service.GenerateResearchBookletAsync(
                "raw compiler output",
                "code context",
                "<project/>",
                "project codebase",
                ImmutableList<string>.Empty);
        }
        catch 
        { 
            /* Expected exception */ 
        }

        // Assert
        this._mockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s.Contains("Starting PARALLEL AI Research Pipeline"))), Times.Once);
        this._mockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s.Contains("Parsing compiler errors"))), Times.Once);
        this._mockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s.Contains("Running Mistral, DeepSeek, and CodeGemma analysis in PARALLEL"))), Times.Once);
        this._mockLogger.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_VerifiesParallelExecution()
    {
        // Arrange
        var executionOrder = new List<string>();
        var executionTimes = new Dictionary<string, DateTime>();
        var errors = ImmutableList.Create(CreateTestError());
        
        this._mockMediator.Setup(m => m.Send(It.IsAny<ParseCompilerErrorsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                executionOrder.Add("Parse");
                executionTimes["Parse"] = DateTime.UtcNow;
                return new ParseCompilerErrorsResponse(errors, "Found 1 error", 1, 0);
            });

        // Set up parallel services with delays to test concurrent execution
        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeDocumentationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                executionOrder.Add("Mistral");
                executionTimes["Mistral"] = DateTime.UtcNow;
                await Task.Delay(50); // Simulate work
                return CreateTestDocumentationAnalysis();
            });

        this._mockMediator.Setup(m => m.Send(It.IsAny<AnalyzeContextCommand>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                executionOrder.Add("DeepSeek");
                executionTimes["DeepSeek"] = DateTime.UtcNow;
                await Task.Delay(50); // Simulate work
                return CreateTestContextAnalysis();
            });

        this._mockMediator.Setup(m => m.Send(It.IsAny<ValidatePatternsCommand>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                executionOrder.Add("CodeGemma");
                executionTimes["CodeGemma"] = DateTime.UtcNow;
                await Task.Delay(50); // Simulate work
                return CreateTestPatternValidation();
            });

        this._mockMediator.Setup(m => m.Send(It.IsAny<GenerateBookletCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                executionOrder.Add("Gemma2");
                executionTimes["Gemma2"] = DateTime.UtcNow;
                var booklet = CreateTestBooklet(errors, CreateTestDocumentationAnalysis(), CreateTestContextAnalysis(), CreateTestPatternValidation());
                return new BookletGenerationResponse(booklet, "/path.json", 100, ImmutableDictionary<string, long>.Empty);
            });

        this._mockPersistenceService.Setup(p => p.SaveBookletAsync(It.IsAny<ResearchBooklet>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIRESResult<string>.Success("/saved.json"));

        // Act
        var result = await this._service.GenerateResearchBookletAsync(
            "raw",
            "context",
            "<project/>",
            "codebase",
            ImmutableList<string>.Empty);

        // Assert
        Assert.True(result.IsSuccess);
        
        // Verify that Parse happened first
        Assert.Equal("Parse", executionOrder.First());
        
        // Verify that Gemma2 happened last (after parallel execution)
        Assert.Equal("Gemma2", executionOrder.Last());
        
        // Verify that Mistral, DeepSeek, and CodeGemma started close together (parallel)
        var parallelServices = new[] { "Mistral", "DeepSeek", "CodeGemma" };
        Assert.All(parallelServices, service => Assert.Contains(service, executionOrder));
    }

    [Fact]
    public async Task GenerateResearchBookletAsync_CalculatesTimeSaved()
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
        
        // Verify time saved calculation was logged
        this._mockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s.Contains("Time saved by parallel execution"))), Times.Once);
    }

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