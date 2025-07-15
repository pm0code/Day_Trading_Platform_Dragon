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
/// Fallback parser for general build errors that don't match specific patterns.
/// Handles various error formats from different build tools.
/// </summary>
public class GeneralErrorParser : AIRESServiceBase, IErrorParser
{
    // Format 1: Error: Message
    private static readonly Regex SimpleErrorPattern = new Regex(
        @"^(?<severity>Error|Warning):\s*(?<message>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Format 2: FilePath : error : Message
    private static readonly Regex FileErrorPattern = new Regex(
        @"^(?<file>[^:]+)\s*:\s*(?<severity>error|warning)\s*:\s*(?<message>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Format 3: [Error] Message or [Warning] Message
    private static readonly Regex BracketErrorPattern = new Regex(
        @"^\[(?<severity>Error|Warning)\]\s*(?<message>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Format 4: ERROR: Message or WARNING: Message (all caps)
    private static readonly Regex CapsErrorPattern = new Regex(
        @"^(?<severity>ERROR|WARNING):\s*(?<message>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public GeneralErrorParser(IAIRESLogger logger) 
        : base(logger, nameof(GeneralErrorParser))
    {
    }

    public string ErrorType => "GENERAL";

    public bool CanParse(string errorLine)
    {
        LogMethodEntry();
        
        if (string.IsNullOrWhiteSpace(errorLine))
        {
            LogMethodExit();
            return false;
        }

        var trimmed = errorLine.Trim();
        
        // This is a fallback parser, so it should accept most error-like patterns
        // but not those already handled by specific parsers
        if (trimmed.Contains("CS", StringComparison.OrdinalIgnoreCase) && 
            Regex.IsMatch(trimmed, @"\bCS\d+\b"))
        {
            LogMethodExit();
            return false; // Let CSharpErrorParser handle this
        }

        if (trimmed.Contains("MSB", StringComparison.OrdinalIgnoreCase) && 
            Regex.IsMatch(trimmed, @"\bMSB\d+\b"))
        {
            LogMethodExit();
            return false; // Let MSBuildErrorParser handle this
        }

        if (trimmed.Contains("NETSDK", StringComparison.OrdinalIgnoreCase) && 
            Regex.IsMatch(trimmed, @"\bNETSDK\d+\b"))
        {
            LogMethodExit();
            return false; // Let NetSdkErrorParser handle this
        }

        var canParse = SimpleErrorPattern.IsMatch(trimmed) ||
                      FileErrorPattern.IsMatch(trimmed) ||
                      BracketErrorPattern.IsMatch(trimmed) ||
                      CapsErrorPattern.IsMatch(trimmed);
        
        LogDebug($"Can parse '{trimmed.Substring(0, Math.Min(50, trimmed.Length))}...': {canParse}");
        
        LogMethodExit();
        return canParse;
    }

    public IEnumerable<CompilerError> ParseErrors(IEnumerable<string> lines)
    {
        LogMethodEntry();
        UpdateMetric("GeneralErrorParser.ParseRequests", 1);
        
        var errors = new List<CompilerError>();
        var errorCounter = 0;
        
        try
        {
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var trimmed = line.Trim();
                CompilerError? error = null;

                // Try each pattern in order
                var match = FileErrorPattern.Match(trimmed);
                if (match.Success)
                {
                    var location = new ErrorLocation(
                        match.Groups["file"].Value,
                        0,
                        0
                    );

                    error = new CompilerError(
                        $"GEN{++errorCounter:D4}", // Generate unique code
                        match.Groups["message"].Value,
                        match.Groups["severity"].Value.ToLowerInvariant(),
                        location,
                        trimmed
                    );
                }
                else
                {
                    // Try simple patterns
                    match = SimpleErrorPattern.Match(trimmed);
                    if (!match.Success)
                        match = BracketErrorPattern.Match(trimmed);
                    if (!match.Success)
                        match = CapsErrorPattern.Match(trimmed);

                    if (match.Success)
                    {
                        var location = new ErrorLocation("Unknown", 0, 0);
                        error = new CompilerError(
                            $"GEN{++errorCounter:D4}", // Generate unique code
                            match.Groups["message"].Value,
                            match.Groups["severity"].Value.ToLowerInvariant(),
                            location,
                            trimmed
                        );
                    }
                }

                if (error != null)
                {
                    errors.Add(error);
                    UpdateMetric($"GeneralErrorParser.{error.Severity}sParsed", 1);
                    LogDebug($"Parsed general {error.Severity} {error.Code}");
                }
            }

            UpdateMetric("GeneralErrorParser.TotalParsed", errors.Count);
            LogInfo($"Parsed {errors.Count} general build errors");
            
            LogMethodExit();
            return errors;
        }
        catch (Exception ex)
        {
            UpdateMetric("GeneralErrorParser.ParseFailures", 1);
            LogError("Failed to parse general errors", ex);
            LogMethodExit();
            throw;
        }
    }
}