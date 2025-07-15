using System.Collections.Generic;
using AIRES.Core.Domain.ValueObjects;

namespace AIRES.Core.Domain.Interfaces;

/// <summary>
/// Interface for parsing specific types of build errors.
/// </summary>
public interface IErrorParser
{
    /// <summary>
    /// Determines if this parser can handle the given error line.
    /// </summary>
    /// <param name="errorLine">The error line to check.</param>
    /// <returns>True if this parser can handle the error format.</returns>
    bool CanParse(string errorLine);
    
    /// <summary>
    /// Parses build errors from the given lines.
    /// </summary>
    /// <param name="lines">The lines containing potential errors.</param>
    /// <returns>Collection of parsed compiler errors.</returns>
    IEnumerable<CompilerError> ParseErrors(IEnumerable<string> lines);
    
    /// <summary>
    /// Gets the error type this parser handles (e.g., "CS", "MSB", "NETSDK").
    /// </summary>
    string ErrorType { get; }
}