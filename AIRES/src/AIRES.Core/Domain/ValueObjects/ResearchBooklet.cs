using System.Collections.Immutable;

namespace AIRES.Core.Domain.ValueObjects;

/// <summary>
/// Represents a complete AI research booklet for error resolution.
/// Immutable Value Object.
/// </summary>
public record ResearchBooklet(
    Guid ErrorBatchId,
    string Title,
    IImmutableList<CompilerError> OriginalErrors,
    IImmutableList<AIResearchFinding> AllFindings,
    IImmutableList<BookletSection> Sections,
    IImmutableDictionary<string, string> Metadata
)
{
    /// <summary>
    /// Gets when this booklet was generated.
    /// </summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets the total processing time if available.
    /// </summary>
    public long? ProcessingTimeMs => 
        Metadata.TryGetValue("ProcessingTimeMs", out var time) && long.TryParse(time, out var ms) 
            ? ms 
            : null;
    
    /// <summary>
    /// Gets whether this booklet contains critical findings.
    /// </summary>
    public bool HasCriticalFindings => 
        Metadata.TryGetValue("CriticalViolations", out var violations) && 
        int.TryParse(violations, out var count) && 
        count > 0;
    
    /// <summary>
    /// Creates a minimal booklet for error cases.
    /// </summary>
    public static ResearchBooklet CreateErrorBooklet(
        Guid batchId,
        string errorMessage,
        IImmutableList<CompilerError> errors)
    {
        return new ResearchBooklet(
            batchId,
            "Error Resolution Failed",
            errors,
            ImmutableList<AIResearchFinding>.Empty,
            ImmutableList<BookletSection>.Empty.Add(
                new BookletSection("Error", errorMessage, 1)
            ),
            ImmutableDictionary<string, string>.Empty.Add("Status", "Failed")
        );
    }
}