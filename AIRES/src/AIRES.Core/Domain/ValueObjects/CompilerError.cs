namespace AIRES.Core.Domain.ValueObjects;

/// <summary>
/// Represents a single compiler error parsed from build output.
/// Immutable Value Object.
/// </summary>
public record CompilerError(
    string Id,           // Unique ID for this specific error instance
    string Code,         // Error code (e.g., CS0117, CS1998)
    string Message,      // Error message text
    string Severity,     // "Error", "Warning", "Message"
    ErrorLocation Location,
    string RawErrorLine  // Original line from compiler output
)
{
    /// <summary>
    /// Convenience constructor to generate Id automatically.
    /// </summary>
    public CompilerError(string code, string message, string severity, ErrorLocation location, string rawErrorLine)
        : this(Guid.NewGuid().ToString("N"), code, message, severity, location, rawErrorLine) 
    { 
    }
    
    /// <summary>
    /// Checks if this is an actual error (not warning or message).
    /// </summary>
    public bool IsError => Severity.Equals("Error", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// Gets a display-friendly representation of the error.
    /// </summary>
    public string ToDisplayString() => $"{Code}: {Message} [{Location}]";
}