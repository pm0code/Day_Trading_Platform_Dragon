namespace AIRES.Core.Domain.ValueObjects;

/// <summary>
/// Represents a section within a research booklet.
/// Immutable Value Object.
/// </summary>
public record BookletSection(
    string Title,
    string Content,
    int Order
)
{
    /// <summary>
    /// Creates a section with auto-generated order based on title.
    /// </summary>
    public static BookletSection Create(string title, string content)
    {
        var order = title switch
        {
            var t when t.Contains("Summary", StringComparison.OrdinalIgnoreCase) => 1,
            var t when t.Contains("Architectural", StringComparison.OrdinalIgnoreCase) => 2,
            var t when t.Contains("Implementation", StringComparison.OrdinalIgnoreCase) => 3,
            var t when t.Contains("Pattern", StringComparison.OrdinalIgnoreCase) => 4,
            var t when t.Contains("Testing", StringComparison.OrdinalIgnoreCase) => 5,
            _ => 99
        };
        
        return new BookletSection(title, content, order);
    }
}