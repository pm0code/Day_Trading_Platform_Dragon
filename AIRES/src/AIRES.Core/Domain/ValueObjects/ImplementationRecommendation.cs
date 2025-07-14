namespace AIRES.Core.Domain.ValueObjects;

/// <summary>
/// Represents a specific implementation recommendation.
/// Immutable Value Object.
/// </summary>
public record ImplementationRecommendation(
    string Title,
    string Description,
    string CodeExample,
    int Priority,
    string EstimatedEffort
);