namespace AIRES.Core.Domain.ValueObjects;

/// <summary>
/// Represents the location of an error in source code.
/// Immutable Value Object.
/// </summary>
public record ErrorLocation(
    string FilePath,
    int LineNumber,
    int ColumnNumber
)
{
    /// <summary>
    /// Gets the file name without path.
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);
    
    /// <summary>
    /// Checks if this location has valid line/column information.
    /// </summary>
    public bool HasPosition => LineNumber > 0 && ColumnNumber > 0;
    
    public override string ToString() => $"{FilePath}({LineNumber},{ColumnNumber})";
    
    /// <summary>
    /// Creates an ErrorLocation for when position is unknown.
    /// </summary>
    public static ErrorLocation Unknown(string filePath) => new(filePath, 0, 0);
}