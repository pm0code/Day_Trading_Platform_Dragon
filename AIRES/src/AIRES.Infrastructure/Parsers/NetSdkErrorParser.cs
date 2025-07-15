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
/// Parser for .NET SDK errors (NETSDKxxxx).
/// </summary>
public class NetSdkErrorParser : AIRESServiceBase, IErrorParser
{
    // Format 1: error NETSDKxxxx: Message
    private static readonly Regex NetSdkPattern = new Regex(
        @"^(?<severity>error|warning)\s+(?<code>NETSDK\d+):\s*(?<message>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Format 2: FilePath : error NETSDKxxxx: Message
    private static readonly Regex NetSdkWithFilePattern = new Regex(
        @"^(?<file>[^:]+):\s*(?<severity>error|warning)\s+(?<code>NETSDK\d+):\s*(?<message>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public NetSdkErrorParser(IAIRESLogger logger) 
        : base(logger, nameof(NetSdkErrorParser))
    {
    }

    public string ErrorType => "NETSDK";

    public bool CanParse(string errorLine)
    {
        LogMethodEntry();
        
        if (string.IsNullOrWhiteSpace(errorLine))
        {
            LogMethodExit();
            return false;
        }

        var trimmed = errorLine.Trim();
        var canParse = trimmed.Contains("NETSDK", StringComparison.OrdinalIgnoreCase) &&
                      (NetSdkPattern.IsMatch(trimmed) || NetSdkWithFilePattern.IsMatch(trimmed));
        
        LogDebug($"Can parse '{trimmed.Substring(0, Math.Min(50, trimmed.Length))}...': {canParse}");
        
        LogMethodExit();
        return canParse;
    }

    public IEnumerable<CompilerError> ParseErrors(IEnumerable<string> lines)
    {
        LogMethodEntry();
        UpdateMetric("NetSdkErrorParser.ParseRequests", 1);
        
        var errors = new List<CompilerError>();
        
        try
        {
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var trimmed = line.Trim();
                CompilerError? error = null;

                // Try Format 1: FilePath : error NETSDKxxxx: Message
                var match = NetSdkWithFilePattern.Match(trimmed);
                if (match.Success)
                {
                    var location = new ErrorLocation(
                        match.Groups["file"].Value,
                        0, // NETSDK errors typically don't have line numbers
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
                    // Try Format 2: error NETSDKxxxx: Message
                    match = NetSdkPattern.Match(trimmed);
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
                }

                if (error != null)
                {
                    errors.Add(error);
                    UpdateMetric($"NetSdkErrorParser.{error.Severity}sParsed", 1);
                    LogDebug($"Parsed {error.Severity} {error.Code}");
                }
            }

            UpdateMetric("NetSdkErrorParser.TotalParsed", errors.Count);
            LogInfo($"Parsed {errors.Count} .NET SDK errors");
            
            LogMethodExit();
            return errors;
        }
        catch (Exception ex)
        {
            UpdateMetric("NetSdkErrorParser.ParseFailures", 1);
            LogError("Failed to parse .NET SDK errors", ex);
            LogMethodExit();
            throw;
        }
    }
}