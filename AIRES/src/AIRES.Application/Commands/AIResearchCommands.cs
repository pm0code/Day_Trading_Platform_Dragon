using MediatR;
using AIRES.Core.Domain.ValueObjects;
using System.Collections.Immutable;

namespace AIRES.Application.Commands;

// ========== Commands ==========

/// <summary>
/// Command to parse compiler errors from raw build output.
/// </summary>
public record ParseCompilerErrorsCommand(string RawCompilerOutput) : IRequest<ParseCompilerErrorsResponse>;

/// <summary>
/// Command for Mistral to analyze documentation for compiler errors.
/// </summary>
public record AnalyzeDocumentationCommand(
    IImmutableList<CompilerError> Errors,
    string RelevantCode
) : IRequest<DocumentationAnalysisResponse>;

/// <summary>
/// Command for DeepSeek to analyze code context around errors.
/// </summary>
public record AnalyzeContextCommand(
    IImmutableList<CompilerError> Errors,
    DocumentationAnalysisResponse DocAnalysis,
    string SurroundingCode,
    string ProjectStructureXml
) : IRequest<ContextAnalysisResponse>;

/// <summary>
/// Command for CodeGemma to validate patterns against standards.
/// </summary>
public record ValidatePatternsCommand(
    IImmutableList<CompilerError> Errors,
    ContextAnalysisResponse ContextAnalysis,
    string ProjectCodebase,
    IImmutableList<string> ProjectStandards
) : IRequest<PatternValidationResponse>;

/// <summary>
/// Command for Gemma2 to generate final booklet.
/// </summary>
public record GenerateBookletCommand(
    Guid ErrorBatchId,
    IImmutableList<CompilerError> OriginalErrors,
    DocumentationAnalysisResponse DocAnalysis,
    ContextAnalysisResponse ContextAnalysis,
    PatternValidationResponse PatternValidation
) : IRequest<BookletGenerationResponse>;

// ========== Responses ==========

/// <summary>
/// Response from parsing compiler errors.
/// </summary>
public record ParseCompilerErrorsResponse(
    IImmutableList<CompilerError> Errors,
    string Summary,
    int TotalErrors,
    int TotalWarnings
);

/// <summary>
/// Response from Mistral documentation analysis.
/// </summary>
public record DocumentationAnalysisResponse(
    IImmutableList<ErrorDocumentationFinding> Findings,
    string OverallInsights,
    IImmutableDictionary<string, string> SuggestedFixes
);

/// <summary>
/// Response from DeepSeek context analysis.
/// </summary>
public record ContextAnalysisResponse(
    IImmutableList<ContextAnalysisFinding> Findings,
    string DeepCodeUnderstanding,
    IImmutableList<string> IdentifiedPainPoints,
    IImmutableDictionary<string, string> ContextualSolutions
);

/// <summary>
/// Response from CodeGemma pattern validation.
/// </summary>
public record PatternValidationResponse(
    PatternValidationFinding ValidationFinding,
    bool OverallCompliance,
    IImmutableList<string> CriticalViolations,
    IImmutableList<string> Recommendations
);

/// <summary>
/// Final booklet generation response.
/// </summary>
public record BookletGenerationResponse(
    ResearchBooklet Booklet,
    string BookletPath,
    long ProcessingTimeMs,
    IImmutableDictionary<string, long> StepTimings
);