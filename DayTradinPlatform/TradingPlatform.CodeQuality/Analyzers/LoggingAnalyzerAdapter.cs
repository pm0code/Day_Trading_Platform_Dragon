using Microsoft.CodeAnalysis;
using System.Threading.Tasks;
using TradingPlatform.CodeAnalysis;

namespace TradingPlatform.CodeQuality;

/// <summary>
/// Adapter for our custom logging analyzer
/// Focuses on TradingLogOrchestrator compliance and logging best practices
/// </summary>
public class LoggingAnalyzerAdapter : ICodeQualityAnalyzer
{
    public string Name => "Logging Compliance Analyzer";

    public async Task<AnalyzerReport> AnalyzeAsync(Solution solution)
    {
        var report = new AnalyzerReport { AnalyzerName = Name };
        var analyzer = new LoggingAnalyzer();
        
        // Use our existing logging analyzer
        var loggingReport = await analyzer.AnalyzeSolutionAsync(solution.FilePath!);
        
        // Convert violations to CodeIssues
        foreach (var violation in loggingReport.Violations)
        {
            var severity = violation.ViolationType switch
            {
                ViolationType.MicrosoftLoggingUsage => IssueSeverity.Critical,
                ViolationType.ParameterOrder => IssueSeverity.High,
                ViolationType.ParameterType => IssueSeverity.High,
                ViolationType.StringInterpolation => IssueSeverity.Medium,
                _ => IssueSeverity.Low
            };

            report.Issues.Add(new CodeIssue
            {
                Id = $"LOG{violation.ViolationType}",
                Category = "Logging",
                Severity = severity,
                Message = violation.Description,
                FilePath = violation.FilePath,
                Line = violation.LineNumber,
                CodeSnippet = violation.CurrentCode,
                SuggestedFix = violation.SuggestedFix,
                Properties = new Dictionary<string, object>
                {
                    ["MethodName"] = violation.MethodName,
                    ["ViolationType"] = violation.ViolationType.ToString()
                }
            });
        }

        // Add specific checks for LogError parameter order
        await CheckLogErrorParameterOrder(solution, report);
        
        return report;
    }

    private async Task CheckLogErrorParameterOrder(Solution solution, AnalyzerReport report)
    {
        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                var syntaxTree = await document.GetSyntaxTreeAsync();
                if (syntaxTree == null) continue;

                var root = await syntaxTree.GetRootAsync();
                var semanticModel = await document.GetSemanticModelAsync();
                if (semanticModel == null) continue;

                // Find all LogError calls
                var logErrorCalls = root.DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
                    .Where(inv => inv.ToString().Contains("LogError"));

                foreach (var call in logErrorCalls)
                {
                    var args = call.ArgumentList.Arguments;
                    if (args.Count >= 2)
                    {
                        // Check if first argument looks like an exception variable
                        var firstArg = args[0].Expression.ToString();
                        if (firstArg == "ex" || firstArg.EndsWith("Exception") || firstArg.Contains("exception"))
                        {
                            report.Issues.Add(new CodeIssue
                            {
                                Id = "LOG001",
                                Category = "Logging",
                                Severity = IssueSeverity.Critical,
                                Message = "LogError has exception as first parameter - should be (string message, Exception? exception, ...)",
                                FilePath = document.FilePath!,
                                Line = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                CodeSnippet = call.ToString(),
                                SuggestedFix = $"Swap parameters: LogError({args[1]}, {args[0]}, ...)"
                            });
                        }
                    }
                }
            }
        }
    }
}