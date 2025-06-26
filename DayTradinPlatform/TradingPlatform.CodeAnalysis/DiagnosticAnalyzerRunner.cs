using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using TradingPlatform.CodeAnalysis.Analyzers;
using TradingPlatform.CodeAnalysis.Integration;

namespace TradingPlatform.CodeAnalysis
{
    /// <summary>
    /// Main entry point for running TradingPlatform analyzers.
    /// Provides both command-line and programmatic access to code analysis.
    /// </summary>
    public class DiagnosticAnalyzerRunner
    {
        private readonly RealTimeFeedbackService _feedbackService;
        private readonly List<DiagnosticAnalyzer> _analyzers;

        public DiagnosticAnalyzerRunner()
        {
            _feedbackService = new RealTimeFeedbackService();
            _analyzers = LoadAnalyzers();
        }

        public static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: TradingPlatform.CodeAnalysis <solution-path>");
                Console.WriteLine("Options:");
                Console.WriteLine("  --output <path>     Output directory for reports");
                Console.WriteLine("  --severity <level>  Minimum severity (Error, Warning, Info)");
                Console.WriteLine("  --real-time         Enable real-time feedback to AI assistants");
                return;
            }

            // Register MSBuild
            MSBuildLocator.RegisterDefaults();

            var runner = new DiagnosticAnalyzerRunner();
            await runner.AnalyzeSolutionAsync(args[0]);
        }

        public async Task AnalyzeSolutionAsync(string solutionPath)
        {
            Console.WriteLine($"Loading solution: {solutionPath}");
            
            using var workspace = MSBuildWorkspace.Create();
            workspace.WorkspaceFailed += (o, e) => Console.WriteLine(e.Diagnostic.Message);
            
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            Console.WriteLine($"Loaded {solution.Projects.Count()} projects");

            var allDiagnostics = new List<Diagnostic>();

            foreach (var project in solution.Projects)
            {
                Console.WriteLine($"\nAnalyzing project: {project.Name}");
                var diagnostics = await AnalyzeProjectAsync(project);
                allDiagnostics.AddRange(diagnostics);

                // Send real-time feedback
                foreach (var diagnostic in diagnostics)
                {
                    if (diagnostic.Severity >= DiagnosticSeverity.Warning)
                    {
                        var filePath = diagnostic.Location.GetLineSpan().Path;
                        _feedbackService.EnqueueDiagnostic(diagnostic, filePath, project.Name);
                    }
                }
            }

            // Generate reports
            await GenerateReportsAsync(allDiagnostics, Path.GetDirectoryName(solutionPath));
            
            Console.WriteLine($"\n✓ Analysis complete. Found {allDiagnostics.Count} issues:");
            Console.WriteLine($"  - Errors: {allDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Error)}");
            Console.WriteLine($"  - Warnings: {allDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning)}");
            Console.WriteLine($"  - Info: {allDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Info)}");
        }

        private async Task<ImmutableArray<Diagnostic>> AnalyzeProjectAsync(Project project)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null)
                return ImmutableArray<Diagnostic>.Empty;

            var compilationWithAnalyzers = compilation.WithAnalyzers(
                _analyzers.ToImmutableArray(),
                new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty));

            var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
            return diagnostics;
        }

        private List<DiagnosticAnalyzer> LoadAnalyzers()
        {
            return new List<DiagnosticAnalyzer>
            {
                new FinancialPrecisionAnalyzer(),
                new CanonicalServiceAnalyzer(),
                new TradingResultAnalyzer(),
                // Add more analyzers as they are implemented
            };
        }

        private async Task GenerateReportsAsync(List<Diagnostic> diagnostics, string outputPath)
        {
            var reportPath = Path.Combine(outputPath, "CodeAnalysis");
            Directory.CreateDirectory(reportPath);

            // Generate JSON report
            var jsonReport = new
            {
                timestamp = DateTime.UtcNow,
                summary = new
                {
                    total = diagnostics.Count,
                    errors = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error),
                    warnings = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning),
                    info = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Info)
                },
                diagnostics = diagnostics.Select(d => new
                {
                    id = d.Id,
                    severity = d.Severity.ToString(),
                    message = d.GetMessage(),
                    location = new
                    {
                        file = d.Location.GetLineSpan().Path,
                        line = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                        column = d.Location.GetLineSpan().StartLinePosition.Character + 1
                    },
                    category = d.Descriptor.Category
                }).ToList()
            };

            var jsonPath = Path.Combine(reportPath, "diagnostics.json");
            await File.WriteAllTextAsync(jsonPath, 
                Newtonsoft.Json.JsonConvert.SerializeObject(jsonReport, Newtonsoft.Json.Formatting.Indented));

            Console.WriteLine($"Reports generated in: {reportPath}");
        }

        public void Dispose()
        {
            _feedbackService?.Dispose();
        }
    }
}