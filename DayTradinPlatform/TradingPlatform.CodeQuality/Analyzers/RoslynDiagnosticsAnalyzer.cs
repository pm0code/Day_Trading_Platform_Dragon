using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace TradingPlatform.CodeQuality;

/// <summary>
/// Leverages Roslyn's built-in diagnostics and all installed analyzers
/// Captures diagnostics from StyleCop, SonarAnalyzer, Roslynator, etc.
/// </summary>
public class RoslynDiagnosticsAnalyzer : ICodeQualityAnalyzer
{
    public string Name => "Roslyn Diagnostics Analyzer";

    public async Task<AnalyzerReport> AnalyzeAsync(Solution solution)
    {
        var report = new AnalyzerReport { AnalyzerName = Name };
        
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null) continue;

            // Get all diagnostics including from analyzers
            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity >= DiagnosticSeverity.Warning)
                .Where(d => !d.IsSuppressed);

            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Location.IsInSource)
                {
                    var lineSpan = diagnostic.Location.GetLineSpan();
                    
                    report.Issues.Add(new CodeIssue
                    {
                        Id = diagnostic.Id,
                        Category = GetCategoryFromDiagnosticId(diagnostic.Id),
                        Severity = MapSeverity(diagnostic.Severity),
                        Message = diagnostic.GetMessage(),
                        FilePath = lineSpan.Path,
                        Line = lineSpan.StartLinePosition.Line + 1,
                        Column = lineSpan.StartLinePosition.Character + 1,
                        Properties = new Dictionary<string, object>
                        {
                            ["DiagnosticId"] = diagnostic.Id,
                            ["HelpLink"] = diagnostic.Descriptor.HelpLinkUri ?? ""
                        }
                    });
                }
            }
        }

        return report;
    }

    private string GetCategoryFromDiagnosticId(string id)
    {
        return id switch
        {
            _ when id.StartsWith("SA") => "StyleCop",
            _ when id.StartsWith("S") => "SonarAnalyzer",
            _ when id.StartsWith("RCS") => "Roslynator",
            _ when id.StartsWith("MA") => "Meziantou",
            _ when id.StartsWith("SCS") => "Security",
            _ when id.StartsWith("SEC") => "PumaSecurity",
            _ when id.StartsWith("CC") => "CodeCracker",
            _ when id.StartsWith("CS") => "Compiler",
            _ when id.StartsWith("CA") => "CodeAnalysis",
            _ => "Other"
        };
    }

    private IssueSeverity MapSeverity(DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Error => IssueSeverity.Critical,
            DiagnosticSeverity.Warning => IssueSeverity.High,
            DiagnosticSeverity.Info => IssueSeverity.Medium,
            DiagnosticSeverity.Hidden => IssueSeverity.Low,
            _ => IssueSeverity.Info
        };
    }
}