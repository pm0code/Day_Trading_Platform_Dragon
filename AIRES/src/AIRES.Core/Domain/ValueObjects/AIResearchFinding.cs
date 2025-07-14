using System.Collections.Immutable;

namespace AIRES.Core.Domain.ValueObjects;

/// <summary>
/// Base abstract class for all AI research findings.
/// Immutable Value Object.
/// </summary>
public abstract record AIResearchFinding(string AIModelName, string Title, string Content);

/// <summary>
/// Finding from the Error Documentation Researcher (Mistral).
/// </summary>
public record ErrorDocumentationFinding(string AIModelName, string Title, string Content, string SuggestedDocsLink)
    : AIResearchFinding(AIModelName, Title, Content);

/// <summary>
/// Finding from the Context Analyzer (DeepSeek).
/// </summary>
public record ContextAnalysisFinding(string AIModelName, string Title, string Content, string RelevantCodeSnippet, string ProjectStructureOverview)
    : AIResearchFinding(AIModelName, Title, Content);

/// <summary>
/// Represents a single issue identified during pattern validation.
/// Immutable Value Object.
/// </summary>
public record PatternIssue(string IssueType, string Description, string SuggestedCorrection, ErrorLocation? Location = null);

/// <summary>
/// Finding from the Pattern Validator (CodeGemma).
/// Contains details about patterns identified as issues or compliant.
/// </summary>
public record PatternValidationFinding(string AIModelName, string Title, string Content, IImmutableList<PatternIssue> IssuesIdentified, IImmutableList<string> CompliantPatterns)
    : AIResearchFinding(AIModelName, Title, Content)
{
    public PatternValidationFinding(string aiModelName, string title, string content, IEnumerable<PatternIssue> issuesIdentified, IEnumerable<string> compliantPatterns)
        : this(aiModelName, title, content, issuesIdentified?.ToImmutableList() ?? ImmutableList<PatternIssue>.Empty, compliantPatterns?.ToImmutableList() ?? ImmutableList<string>.Empty) { }
}