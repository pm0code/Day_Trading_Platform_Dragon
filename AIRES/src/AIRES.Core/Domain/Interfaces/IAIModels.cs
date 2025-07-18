using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using AIRES.Core.Domain.ValueObjects;

namespace AIRES.Core.Domain.Interfaces;

/// <summary>
/// Represents the AI model (Mistral) responsible for generating error documentation.
/// </summary>
public interface IErrorDocumentationAIModel
{
    Task<ErrorDocumentationFinding> AnalyzeAsync(CompilerError error, string relevantCodeSnippet);
}

/// <summary>
/// Represents the AI model (DeepSeek) responsible for analyzing code context.
/// </summary>
public interface IContextAnalyzerAIModel
{
    Task<ContextAnalysisFinding> AnalyzeAsync(CompilerError error, string surroundingCode, string projectStructureXml);
}

/// <summary>
/// Represents the AI model (CodeGemma) responsible for validating coding patterns.
/// </summary>
public interface IPatternValidatorAIModel
{
    // This model could analyze a batch of errors and relevant code for the whole project
    Task<PatternValidationFinding> AnalyzeBatchAsync(IEnumerable<CompilerError> errors, string projectCodebase, IImmutableList<string> projectStandardPatterns);
}

/// <summary>
/// Represents the AI model (Gemma2) responsible for synthesizing findings into a comprehensive booklet draft.
/// </summary>
public interface IBookletGeneratorAIModel
{
    /// <summary>
    /// Represents the raw content draft generated by Gemma2, before being structured into ResearchBooklet.
    /// This is an internal temporary value object, not part of the public domain.
    /// </summary>
    public record BookletContentDraft(
        string Title,
        string Summary,
        ArchitecturalGuidance? ArchitecturalGuidance,
        IImmutableList<ImplementationRecommendation> ImplementationRecommendations
    );

    Task<BookletContentDraft> GenerateBookletContentAsync(
        Guid errorBatchId,
        IImmutableList<CompilerError> originalErrors,
        IImmutableList<AIResearchFinding> allDetailedFindings,
        PatternValidationFinding? consolidatedPatternFindings);
}