using System.Collections.Immutable;

namespace AIRES.Foundation.Alerting;

/// <summary>
/// Immutable alert message for thread-safe operations.
/// </summary>
public record AlertMessage
{
    /// <summary>
    /// Unique identifier for this alert.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// Severity level of the alert.
    /// </summary>
    public AlertSeverity Severity { get; init; }
    
    /// <summary>
    /// Component that generated the alert.
    /// </summary>
    public string Component { get; init; } = string.Empty;
    
    /// <summary>
    /// Alert message text.
    /// </summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>
    /// Additional details about the alert.
    /// </summary>
    public ImmutableDictionary<string, object>? Details { get; init; }
    
    /// <summary>
    /// Timestamp when the alert was created.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Suggested action for resolving the alert.
    /// </summary>
    public string? SuggestedAction { get; init; }
    
    /// <summary>
    /// Gets a formatted string representation of the alert.
    /// </summary>
    public override string ToString()
    {
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Severity}] [{Component}] {Message}";
    }
}