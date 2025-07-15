using System;
using System.Collections.Generic;
using System.Linq;
using AIRES.Core.Domain.Interfaces;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;

namespace AIRES.Infrastructure.Parsers;

/// <summary>
/// Factory for selecting appropriate error parsers based on error content.
/// Implements a priority-based dispatcher pattern.
/// </summary>
public class ErrorParserFactory : AIRESServiceBase
{
    private readonly List<(int Priority, IErrorParser Parser)> _parsers;

    public ErrorParserFactory(
        IAIRESLogger logger,
        CSharpErrorParser csharpParser,
        MSBuildErrorParser msbuildParser,
        NetSdkErrorParser netsdkParser,
        GeneralErrorParser generalParser) 
        : base(logger, nameof(ErrorParserFactory))
    {
        // Priority order: Specific parsers first, general parser last
        _parsers = new List<(int Priority, IErrorParser Parser)>
        {
            (1, csharpParser),    // Highest priority - most specific pattern
            (2, msbuildParser),   // MSBuild errors
            (3, netsdkParser),    // .NET SDK errors
            (99, generalParser)   // Lowest priority - fallback
        };

        LogInfo($"Initialized with {_parsers.Count} parsers");
    }

    /// <summary>
    /// Gets the appropriate parser for the given error line.
    /// Returns the first parser that can handle the error based on priority.
    /// </summary>
    public IErrorParser? GetParser(string errorLine)
    {
        LogMethodEntry();
        UpdateMetric("ErrorParserFactory.GetParserRequests", 1);

        if (string.IsNullOrWhiteSpace(errorLine))
        {
            LogWarning("Empty error line provided");
            LogMethodExit();
            return null;
        }

        try
        {
            // Check parsers in priority order
            foreach (var (priority, parser) in _parsers.OrderBy(p => p.Priority))
            {
                if (parser.CanParse(errorLine))
                {
                    UpdateMetric($"ErrorParserFactory.Selected{parser.ErrorType}", 1);
                    LogDebug($"Selected {parser.GetType().Name} for error line");
                    LogMethodExit();
                    return parser;
                }
            }

            UpdateMetric("ErrorParserFactory.NoParserFound", 1);
            LogWarning($"No parser found for error line: {errorLine.Substring(0, Math.Min(100, errorLine.Length))}...");
            LogMethodExit();
            return null;
        }
        catch (Exception ex)
        {
            UpdateMetric("ErrorParserFactory.GetParserFailures", 1);
            LogError("Failed to get parser for error line", ex);
            LogMethodExit();
            throw;
        }
    }

    /// <summary>
    /// Gets all parsers that can handle the given error line.
    /// Useful for debugging or when multiple parsers might apply.
    /// </summary>
    public IEnumerable<IErrorParser> GetApplicableParsers(string errorLine)
    {
        LogMethodEntry();
        UpdateMetric("ErrorParserFactory.GetApplicableParsersRequests", 1);

        if (string.IsNullOrWhiteSpace(errorLine))
        {
            LogMethodExit();
            return Enumerable.Empty<IErrorParser>();
        }

        try
        {
            var applicableParsers = _parsers
                .Where(p => p.Parser.CanParse(errorLine))
                .OrderBy(p => p.Priority)
                .Select(p => p.Parser)
                .ToList();

            LogInfo($"Found {applicableParsers.Count} applicable parsers for error line");
            LogMethodExit();
            return applicableParsers;
        }
        catch (Exception ex)
        {
            UpdateMetric("ErrorParserFactory.GetApplicableParsersFailures", 1);
            LogError("Failed to get applicable parsers", ex);
            LogMethodExit();
            throw;
        }
    }

    /// <summary>
    /// Gets all registered parsers for diagnostic purposes.
    /// </summary>
    public IReadOnlyList<IErrorParser> GetAllParsers()
    {
        LogMethodEntry();
        var parsers = _parsers.OrderBy(p => p.Priority).Select(p => p.Parser).ToList();
        LogMethodExit();
        return parsers;
    }
}