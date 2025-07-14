namespace AIRES.Core.Domain.ValueObjects;

/// <summary>
/// Represents architectural guidance for resolving errors.
/// Immutable Value Object.
/// </summary>
public record ArchitecturalGuidance(
    string Title,
    string Description,
    string Rationale,
    string ImpactAssessment
);