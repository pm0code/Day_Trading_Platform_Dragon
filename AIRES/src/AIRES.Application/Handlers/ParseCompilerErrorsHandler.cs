using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using AIRES.Application.Commands;
using AIRES.Application.Exceptions;
using AIRES.Core.Domain.ValueObjects;
using AIRES.Foundation.Canonical;
using AIRES.Foundation.Logging;
using System.Collections.Immutable;
using AIRES.Infrastructure.Parsers;

namespace AIRES.Application.Handlers;

/// <summary>
/// Handler for parsing compiler errors from raw build output.
/// Uses ErrorParserFactory to support multiple error formats.
/// </summary>
public class ParseCompilerErrorsHandler : AIRESServiceBase, IRequestHandler<ParseCompilerErrorsCommand, ParseCompilerErrorsResponse>
{
    private readonly ErrorParserFactory _parserFactory;

    public ParseCompilerErrorsHandler(
        IAIRESLogger logger,
        ErrorParserFactory parserFactory) 
        : base(logger, nameof(ParseCompilerErrorsHandler))
    {
        _parserFactory = parserFactory ?? throw new ArgumentNullException(nameof(parserFactory));
    }

    public async Task<ParseCompilerErrorsResponse> Handle(ParseCompilerErrorsCommand request, CancellationToken cancellationToken)
    {
        LogMethodEntry();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Validate input
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            
            UpdateMetric("ParseCompilerErrors.Requests", 1);
            // Add async operation to satisfy CS1998
            await Task.Yield();
            
            if (string.IsNullOrWhiteSpace(request.RawCompilerOutput))
            {
                UpdateMetric("ParseCompilerErrors.EmptyInputs", 1);
                LogWarning("Empty compiler output provided");
                LogMethodExit();
                return new ParseCompilerErrorsResponse(
                    ImmutableList<CompilerError>.Empty,
                    "No compiler output to parse",
                    0,
                    0
                );
            }

            var allErrors = new List<CompilerError>();
            var lines = request.RawCompilerOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int errorCount = 0;
            int warningCount = 0;
            
            // Group lines to potentially handle multi-line errors
            var processedLines = new HashSet<string>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || processedLines.Contains(line))
                    continue;

                var trimmedLine = line.Trim();
                var parser = _parserFactory.GetParser(trimmedLine);
                
                if (parser != null)
                {
                    // Parse this line with the selected parser
                    var errors = parser.ParseErrors(new[] { trimmedLine });
                    
                    foreach (var error in errors)
                    {
                        allErrors.Add(error);
                        processedLines.Add(trimmedLine);
                        
                        if (error.Severity.Equals("error", StringComparison.OrdinalIgnoreCase))
                            errorCount++;
                        else if (error.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase))
                            warningCount++;
                            
                        UpdateMetric($"ParseCompilerErrors.{parser.ErrorType}Parsed", 1);
                    }
                }
                else
                {
                    // No parser found for this line
                    LogDebug($"No parser found for line: {trimmedLine.Substring(0, Math.Min(80, trimmedLine.Length))}...");
                    UpdateMetric("ParseCompilerErrors.UnparsedLines", 1);
                }
            }

            LogInfo($"Parsed {allErrors.Count} compiler diagnostics: {errorCount} errors, {warningCount} warnings");

            var summary = GenerateSummary(allErrors);
            
            stopwatch.Stop();
            UpdateMetric("ParseCompilerErrors.ResponseTime", stopwatch.ElapsedMilliseconds);
            UpdateMetric("ParseCompilerErrors.Successes", 1);
            UpdateMetric("ParseCompilerErrors.ErrorsParsed", allErrors.Count);
            UpdateMetric("ParseCompilerErrors.ErrorCount", errorCount);
            UpdateMetric("ParseCompilerErrors.WarningCount", warningCount);
            
            LogInfo($"Successfully parsed compiler errors in {stopwatch.ElapsedMilliseconds}ms");
            LogMethodExit();
            return new ParseCompilerErrorsResponse(
                allErrors.ToImmutableList(),
                summary,
                errorCount,
                warningCount
            );
        }
        catch (ArgumentNullException ex)
        {
            UpdateMetric("ParseCompilerErrors.ValidationErrors", 1);
            LogError("Invalid input parameters", ex);
            LogMethodExit();
            throw;
        }
        catch (Exception ex)
        {
            UpdateMetric("ParseCompilerErrors.Failures", 1);
            LogError("Failed to parse compiler errors", ex);
            LogMethodExit();
            throw new CompilerErrorParsingException("Failed to parse compiler output", ex);
        }
    }

    private string GenerateSummary(List<CompilerError> errors)
    {
        LogMethodEntry();
        
        if (!errors.Any())
        {
            LogMethodExit();
            return "No compiler errors or warnings found.";
        }

        var errorGroups = errors
            .GroupBy(e => e.Code)
            .OrderByDescending(g => g.Count())
            .Take(5);

        var summary = $"Found {errors.Count} compiler diagnostics. ";
        summary += "Most common: ";
        summary += string.Join(", ", errorGroups.Select(g => $"{g.Key} ({g.Count()}x)"));

        LogMethodExit();
        return summary;
    }
}