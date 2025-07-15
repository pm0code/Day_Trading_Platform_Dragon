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
using System.Text.RegularExpressions;

namespace AIRES.Application.Handlers;

/// <summary>
/// Handler for parsing compiler errors from raw build output.
/// </summary>
public class ParseCompilerErrorsHandler : AIRESServiceBase, IRequestHandler<ParseCompilerErrorsCommand, ParseCompilerErrorsResponse>
{
    // Regex pattern for parsing compiler errors
    // Format: FilePath(line,col): error|warning CODE: Message
    private static readonly Regex ErrorPattern = new Regex(
        @"^(?<file>[^(]+)\((?<line>\d+),(?<col>\d+)\):\s*(?<severity>error|warning)\s+(?<code>\w+):\s*(?<message>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public ParseCompilerErrorsHandler(IAIRESLogger logger) 
        : base(logger, nameof(ParseCompilerErrorsHandler))
    {
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

            var errors = new List<CompilerError>();
            var lines = request.RawCompilerOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int errorCount = 0;
            int warningCount = 0;

            foreach (var line in lines)
            {
                var match = ErrorPattern.Match(line.Trim());
                if (match.Success)
                {
                    var severity = match.Groups["severity"].Value;
                    var location = new ErrorLocation(
                        match.Groups["file"].Value,
                        int.Parse(match.Groups["line"].Value),
                        int.Parse(match.Groups["col"].Value)
                    );

                    var error = new CompilerError(
                        match.Groups["code"].Value,
                        match.Groups["message"].Value,
                        severity,
                        location,
                        line.Trim()
                    );

                    errors.Add(error);
                    
                    if (severity.Equals("error", StringComparison.OrdinalIgnoreCase))
                        errorCount++;
                    else if (severity.Equals("warning", StringComparison.OrdinalIgnoreCase))
                        warningCount++;
                }
            }

            LogInfo($"Parsed {errors.Count} compiler diagnostics: {errorCount} errors, {warningCount} warnings");

            var summary = GenerateSummary(errors);
            
            stopwatch.Stop();
            UpdateMetric("ParseCompilerErrors.ResponseTime", stopwatch.ElapsedMilliseconds);
            UpdateMetric("ParseCompilerErrors.Successes", 1);
            UpdateMetric("ParseCompilerErrors.ErrorsParsed", errors.Count);
            UpdateMetric("ParseCompilerErrors.ErrorCount", errorCount);
            UpdateMetric("ParseCompilerErrors.WarningCount", warningCount);
            
            LogInfo($"Successfully parsed compiler errors in {stopwatch.ElapsedMilliseconds}ms");
            LogMethodExit();
            return new ParseCompilerErrorsResponse(
                errors.ToImmutableList(),
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
        catch (RegexMatchTimeoutException ex)
        {
            UpdateMetric("ParseCompilerErrors.RegexTimeouts", 1);
            LogError("Regex timeout while parsing compiler errors", ex);
            LogMethodExit();
            throw new CompilerErrorParsingException("Parsing timeout - input may be too complex", ex);
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