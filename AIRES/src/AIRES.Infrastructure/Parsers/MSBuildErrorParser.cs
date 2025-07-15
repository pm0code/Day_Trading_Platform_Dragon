using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AIRES.Core.Domain.Interfaces;
using AIRES.Core.Domain.ValueObjects;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;

namespace AIRES.Infrastructure.Parsers;

/// <summary>
/// Parser for MSBuild errors (MSBxxxx).
/// Handles multiple MSBuild error formats.
/// </summary>
public class MSBuildErrorParser : AIRESServiceBase, IErrorParser
{
    // Format 1: FilePath: error MSBxxxx: Message
    private static readonly Regex MSBuildWithFilePattern = new Regex(
        @"^(?<file>[^:]+):\s*(?<severity>error|warning)\s+(?<code>MSB\d+):\s*(?<message>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Format 2: error MSBxxxx: Message (no file)
    private static readonly Regex MSBuildNoFilePattern = new Regex(
        @"^(?<severity>error|warning)\s+(?<code>MSB\d+):\s*(?<message>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Format 3: MSBxxxx: Message (just code)
    private static readonly Regex MSBuildCodeOnlyPattern = new Regex(
        @"^(?<code>MSB\d+):\s*(?<message>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public MSBuildErrorParser(IAIRESLogger logger) 
        : base(logger, nameof(MSBuildErrorParser))
    {
    }

    public string ErrorType => "MSB";

    public bool CanParse(string errorLine)
    {
        LogMethodEntry();
        
        if (string.IsNullOrWhiteSpace(errorLine))
        {
            LogMethodExit();
            return false;
        }

        var trimmed = errorLine.Trim();
        var canParse = trimmed.Contains("MSB", StringComparison.OrdinalIgnoreCase) &&
                      (MSBuildWithFilePattern.IsMatch(trimmed) || 
                       MSBuildNoFilePattern.IsMatch(trimmed) ||
                       MSBuildCodeOnlyPattern.IsMatch(trimmed));
        
        LogDebug($"Can parse '{trimmed.Substring(0, Math.Min(50, trimmed.Length))}...': {canParse}");
        
        LogMethodExit();
        return canParse;
    }

    public IEnumerable<CompilerError> ParseErrors(IEnumerable<string> lines)
    {
        LogMethodEntry();
        UpdateMetric("MSBuildErrorParser.ParseRequests", 1);
        
        var errors = new List<CompilerError>();
        
        try
        {
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var trimmed = line.Trim();
                CompilerError? error = null;

                // Try Format 1: FilePath: error MSBxxxx: Message
                var match = MSBuildWithFilePattern.Match(trimmed);
                if (match.Success)
                {
                    var location = new ErrorLocation(
                        match.Groups["file"].Value,
                        0, // MSBuild errors often don't have line numbers
                        0
                    );

                    error = new CompilerError(
                        match.Groups["code"].Value,
                        match.Groups["message"].Value,
                        match.Groups["severity"].Value,
                        location,
                        trimmed
                    );
                }
                else
                {
                    // Try Format 2: error MSBxxxx: Message
                    match = MSBuildNoFilePattern.Match(trimmed);
                    if (match.Success)
                    {
                        var location = new ErrorLocation("Unknown", 0, 0);
                        error = new CompilerError(
                            match.Groups["code"].Value,
                            match.Groups["message"].Value,
                            match.Groups["severity"].Value,
                            location,
                            trimmed
                        );
                    }
                    else
                    {
                        // Try Format 3: MSBxxxx: Message
                        match = MSBuildCodeOnlyPattern.Match(trimmed);
                        if (match.Success)
                        {
                            var location = new ErrorLocation("Unknown", 0, 0);
                            error = new CompilerError(
                                match.Groups["code"].Value,
                                match.Groups["message"].Value,
                                "error", // Default to error when severity not specified
                                location,
                                trimmed
                            );
                        }
                    }
                }

                if (error != null)
                {
                    errors.Add(error);
                    UpdateMetric($"MSBuildErrorParser.{error.Severity}sParsed", 1);
                    LogDebug($"Parsed {error.Severity} {error.Code}");
                }
            }

            UpdateMetric("MSBuildErrorParser.TotalParsed", errors.Count);
            LogInfo($"Parsed {errors.Count} MSBuild errors");
            
            LogMethodExit();
            return errors;
        }
        catch (Exception ex)
        {
            UpdateMetric("MSBuildErrorParser.ParseFailures", 1);
            LogError("Failed to parse MSBuild errors", ex);
            LogMethodExit();
            throw;
        }
    }
}