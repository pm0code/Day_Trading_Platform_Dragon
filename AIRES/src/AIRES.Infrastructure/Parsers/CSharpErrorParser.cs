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
/// Parser for C# compiler errors (CSxxxx).
/// </summary>
public class CSharpErrorParser : AIRESServiceBase, IErrorParser
{
    // Format: FilePath(line,col): error|warning CSxxxx: Message
    private static readonly Regex CSharpErrorPattern = new Regex(
        @"^(?<file>[^(]+)\((?<line>\d+),(?<col>\d+)\):\s*(?<severity>error|warning)\s+(?<code>CS\d+):\s*(?<message>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public CSharpErrorParser(IAIRESLogger logger) 
        : base(logger, nameof(CSharpErrorParser))
    {
    }

    public string ErrorType => "CS";

    public bool CanParse(string errorLine)
    {
        LogMethodEntry();
        
        if (string.IsNullOrWhiteSpace(errorLine))
        {
            LogMethodExit();
            return false;
        }

        var canParse = CSharpErrorPattern.IsMatch(errorLine.Trim());
        LogDebug($"Can parse '{errorLine.Substring(0, Math.Min(50, errorLine.Length))}...': {canParse}");
        
        LogMethodExit();
        return canParse;
    }

    public IEnumerable<CompilerError> ParseErrors(IEnumerable<string> lines)
    {
        LogMethodEntry();
        UpdateMetric("CSharpErrorParser.ParseRequests", 1);
        
        var errors = new List<CompilerError>();
        
        try
        {
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var match = CSharpErrorPattern.Match(line.Trim());
                if (match.Success)
                {
                    var location = new ErrorLocation(
                        match.Groups["file"].Value,
                        int.Parse(match.Groups["line"].Value),
                        int.Parse(match.Groups["col"].Value)
                    );

                    var error = new CompilerError(
                        match.Groups["code"].Value,
                        match.Groups["message"].Value,
                        match.Groups["severity"].Value,
                        location,
                        line.Trim()
                    );

                    errors.Add(error);
                    UpdateMetric($"CSharpErrorParser.{match.Groups["severity"].Value}sParsed", 1);
                    LogDebug($"Parsed {match.Groups["severity"].Value} {match.Groups["code"].Value}");
                }
            }

            UpdateMetric("CSharpErrorParser.TotalParsed", errors.Count);
            LogInfo($"Parsed {errors.Count} C# compiler errors");
            
            LogMethodExit();
            return errors;
        }
        catch (Exception ex)
        {
            UpdateMetric("CSharpErrorParser.ParseFailures", 1);
            LogError("Failed to parse C# errors", ex);
            LogMethodExit();
            throw;
        }
    }
}