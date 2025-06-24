// File: TradingPlatform.Auditing\RoslynCodeAuditService.cs

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Foundation;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Auditing
{
    /// <summary>
    /// Advanced code audit service using Roslyn CodeAnalysis for comprehensive
    /// static analysis and pattern enforcement.
    /// </summary>
    public class RoslynCodeAuditService : CanonicalServiceBase
    {
        private readonly List<DiagnosticAnalyzer> _analyzers = new();
        private readonly Dictionary<string, ProjectAuditResult> _projectResults = new();
        private long _totalDiagnostics;
        private long _errorCount;
        private long _warningCount;

        public RoslynCodeAuditService(IServiceProvider serviceProvider)
            : base(serviceProvider, serviceProvider.GetRequiredService<ITradingLogger>(), "RoslynCodeAuditService")
        {
            InitializeAnalyzers();
        }

        private void InitializeAnalyzers()
        {
            // Add custom analyzers
            _analyzers.Add(new CanonicalPatternAnalyzer());
            _analyzers.Add(new LoggingPatternAnalyzer());
            _analyzers.Add(new ErrorHandlingAnalyzer());
            _analyzers.Add(new MethodComplexityAnalyzer());
            _analyzers.Add(new DependencyInjectionAnalyzer());
            _analyzers.Add(new SecurityAnalyzer());
            _analyzers.Add(new NamingConventionAnalyzer());
            _analyzers.Add(new DocumentationAnalyzer());
        }

        public async Task<RoslynAuditReport> AuditSolutionAsync(string solutionPath)
        {
            _logger.LogInformation("Starting Roslyn-based solution audit", new { SolutionPath = solutionPath });

            var report = new RoslynAuditReport
            {
                AuditStartTime = DateTime.UtcNow,
                SolutionPath = solutionPath
            };

            try
            {
                // Load the solution
                var workspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace.Create();
                var solution = await workspace.OpenSolutionAsync(solutionPath);

                _logger.LogInformation($"Loaded solution with {solution.Projects.Count()} projects");

                // Analyze each project
                foreach (var project in solution.Projects)
                {
                    var projectResult = await AnalyzeProjectAsync(project);
                    report.ProjectResults.Add(projectResult);
                }

                // Generate summary
                report.TotalProjects = solution.Projects.Count();
                report.TotalFiles = report.ProjectResults.Sum(p => p.FileCount);
                report.TotalDiagnostics = (int)_totalDiagnostics;
                report.ErrorCount = (int)_errorCount;
                report.WarningCount = (int)_warningCount;
                report.InfoCount = report.TotalDiagnostics - report.ErrorCount - report.WarningCount;
                report.ComplianceScore = CalculateComplianceScore(report);
                report.AuditEndTime = DateTime.UtcNow;

                _logger.LogInformation("Roslyn audit completed", new
                {
                    Projects = report.TotalProjects,
                    Files = report.TotalFiles,
                    Diagnostics = report.TotalDiagnostics,
                    ComplianceScore = report.ComplianceScore
                });

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError("Roslyn audit failed", ex);
                throw;
            }
        }

        private async Task<ProjectAuditResult> AnalyzeProjectAsync(Project project)
        {
            _logger.LogInformation($"Analyzing project: {project.Name}");

            var result = new ProjectAuditResult
            {
                ProjectName = project.Name,
                ProjectPath = project.FilePath ?? ""
            };

            try
            {
                // Get compilation
                var compilation = await project.GetCompilationAsync();
                if (compilation == null)
                {
                    _logger.LogWarning($"Could not compile project {project.Name}");
                    return result;
                }

                // Create compilation with analyzers
                var compilationWithAnalyzers = compilation.WithAnalyzers(
                    ImmutableArray.CreateRange(_analyzers),
                    new CompilationWithAnalyzersOptions(
                        new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
                        onAnalyzerException: null,
                        concurrentAnalysis: true,
                        logAnalyzerExecutionTime: true));

                // Get all diagnostics
                var diagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync();
                
                // Process diagnostics
                foreach (var diagnostic in diagnostics)
                {
                    // Skip compiler errors/warnings
                    if (diagnostic.Id.StartsWith("CS")) continue;

                    var diagnosticResult = new DiagnosticResult
                    {
                        Id = diagnostic.Id,
                        Category = diagnostic.Category,
                        Severity = diagnostic.Severity.ToString(),
                        Message = diagnostic.GetMessage(),
                        Location = diagnostic.Location.GetLineSpan().Path,
                        Line = diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1,
                        Column = diagnostic.Location.GetLineSpan().StartLinePosition.Character + 1
                    };

                    result.Diagnostics.Add(diagnosticResult);
                    _totalDiagnostics++;

                    if (diagnostic.Severity == DiagnosticSeverity.Error)
                        _errorCount++;
                    else if (diagnostic.Severity == DiagnosticSeverity.Warning)
                        _warningCount++;
                }

                // Analyze code metrics
                foreach (var document in project.Documents)
                {
                    var metrics = await AnalyzeDocumentMetricsAsync(document);
                    if (metrics != null)
                    {
                        result.FileMetrics.Add(metrics);
                    }
                }

                result.FileCount = project.Documents.Count();
                result.TotalLines = result.FileMetrics.Sum(m => m.Lines);
                result.AverageComplexity = result.FileMetrics.Any() 
                    ? result.FileMetrics.Average(m => m.CyclomaticComplexity) 
                    : 0;

                RecordMetric($"Project.{project.Name}.Diagnostics", result.Diagnostics.Count);
                RecordMetric($"Project.{project.Name}.Files", result.FileCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error analyzing project {project.Name}", ex);
                return result;
            }
        }

        private async Task<FileMetrics?> AnalyzeDocumentMetricsAsync(Document document)
        {
            try
            {
                var tree = await document.GetSyntaxTreeAsync();
                if (tree == null) return null;

                var root = await tree.GetRootAsync();
                var semanticModel = await document.GetSemanticModelAsync();

                var metrics = new FileMetrics
                {
                    FileName = Path.GetFileName(document.FilePath ?? ""),
                    FilePath = document.FilePath ?? "",
                    Lines = tree.GetText().Lines.Count
                };

                // Count various metrics
                var methodVisitor = new MethodMetricsVisitor(semanticModel!);
                methodVisitor.Visit(root);

                metrics.MethodCount = methodVisitor.MethodCount;
                metrics.ClassCount = methodVisitor.ClassCount;
                metrics.CyclomaticComplexity = methodVisitor.TotalComplexity;
                metrics.MaxMethodComplexity = methodVisitor.MaxComplexity;
                metrics.HasCanonicalPattern = methodVisitor.HasCanonicalPattern;
                metrics.LoggingCoverage = methodVisitor.CalculateLoggingCoverage();

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error analyzing document {document.Name}", ex);
                return null;
            }
        }

        private decimal CalculateComplianceScore(RoslynAuditReport report)
        {
            if (report.TotalFiles == 0) return 100m;

            // Base score calculation
            decimal baseScore = 100m;

            // Deduct for errors (5 points each)
            baseScore -= report.ErrorCount * 5m;

            // Deduct for warnings (1 point each)
            baseScore -= report.WarningCount * 1m;

            // Deduct for info (0.1 points each)
            baseScore -= report.InfoCount * 0.1m;

            // Bonus for canonical pattern adoption
            var canonicalAdoption = report.ProjectResults
                .SelectMany(p => p.FileMetrics)
                .Count(m => m.HasCanonicalPattern);
            
            var adoptionRate = (decimal)canonicalAdoption / Math.Max(1, report.TotalFiles);
            baseScore += adoptionRate * 10m;

            return Math.Max(0, Math.Round(baseScore, 2));
        }

        protected override Dictionary<string, object> GetServiceMetrics()
        {
            var baseMetrics = base.GetServiceMetrics();
            
            baseMetrics["TotalDiagnostics"] = _totalDiagnostics;
            baseMetrics["ErrorCount"] = _errorCount;
            baseMetrics["WarningCount"] = _warningCount;
            baseMetrics["AnalyzersCount"] = _analyzers.Count;
            baseMetrics["ProjectsAnalyzed"] = _projectResults.Count;

            return baseMetrics;
        }
    }

    #region Method Metrics Visitor

    internal class MethodMetricsVisitor : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        
        public int MethodCount { get; private set; }
        public int ClassCount { get; private set; }
        public int TotalComplexity { get; private set; }
        public int MaxComplexity { get; private set; }
        public bool HasCanonicalPattern { get; private set; }
        
        private int _methodsWithLogging;
        private int _currentMethodComplexity;

        public MethodMetricsVisitor(SemanticModel semanticModel)
        {
            _semanticModel = semanticModel;
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            ClassCount++;
            
            // Check for canonical pattern
            if (node.BaseList != null)
            {
                foreach (var baseType in node.BaseList.Types)
                {
                    var typeName = baseType.Type.ToString();
                    if (typeName.Contains("Canonical"))
                    {
                        HasCanonicalPattern = true;
                    }
                }
            }

            base.VisitClassDeclaration(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            MethodCount++;
            _currentMethodComplexity = CalculateCyclomaticComplexity(node);
            TotalComplexity += _currentMethodComplexity;
            MaxComplexity = Math.Max(MaxComplexity, _currentMethodComplexity);

            // Check for logging
            if (HasLogging(node))
            {
                _methodsWithLogging++;
            }

            base.VisitMethodDeclaration(node);
        }

        private int CalculateCyclomaticComplexity(MethodDeclarationSyntax method)
        {
            var complexity = 1; // Base complexity

            // Count decision points
            var walker = new ComplexityWalker();
            walker.Visit(method);
            
            return complexity + walker.DecisionPoints;
        }

        private bool HasLogging(MethodDeclarationSyntax method)
        {
            var body = method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? "";
            return body.Contains("_logger.Log") || body.Contains("Log");
        }

        public decimal CalculateLoggingCoverage()
        {
            if (MethodCount == 0) return 100m;
            return (decimal)_methodsWithLogging / MethodCount * 100m;
        }
    }

    internal class ComplexityWalker : CSharpSyntaxWalker
    {
        public int DecisionPoints { get; private set; }

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            DecisionPoints++;
            base.VisitIfStatement(node);
        }

        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            DecisionPoints++;
            base.VisitWhileStatement(node);
        }

        public override void VisitForStatement(ForStatementSyntax node)
        {
            DecisionPoints++;
            base.VisitForStatement(node);
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            DecisionPoints++;
            base.VisitForEachStatement(node);
        }

        public override void VisitSwitchStatement(SwitchStatementSyntax node)
        {
            DecisionPoints += node.Sections.Count;
            base.VisitSwitchStatement(node);
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            DecisionPoints++;
            base.VisitConditionalExpression(node);
        }

        public override void VisitCatchClause(CatchClauseSyntax node)
        {
            DecisionPoints++;
            base.VisitCatchClause(node);
        }
    }

    #endregion

    #region Audit Models

    public class RoslynAuditReport
    {
        public DateTime AuditStartTime { get; set; }
        public DateTime AuditEndTime { get; set; }
        public string SolutionPath { get; set; } = "";
        public List<ProjectAuditResult> ProjectResults { get; set; } = new();
        public int TotalProjects { get; set; }
        public int TotalFiles { get; set; }
        public int TotalDiagnostics { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }
        public decimal ComplianceScore { get; set; }
    }

    public class ProjectAuditResult
    {
        public string ProjectName { get; set; } = "";
        public string ProjectPath { get; set; } = "";
        public int FileCount { get; set; }
        public int TotalLines { get; set; }
        public double AverageComplexity { get; set; }
        public List<DiagnosticResult> Diagnostics { get; set; } = new();
        public List<FileMetrics> FileMetrics { get; set; } = new();
    }

    public class DiagnosticResult
    {
        public string Id { get; set; } = "";
        public string Category { get; set; } = "";
        public string Severity { get; set; } = "";
        public string Message { get; set; } = "";
        public string Location { get; set; } = "";
        public int Line { get; set; }
        public int Column { get; set; }
    }

    public class FileMetrics
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public int Lines { get; set; }
        public int MethodCount { get; set; }
        public int ClassCount { get; set; }
        public int CyclomaticComplexity { get; set; }
        public int MaxMethodComplexity { get; set; }
        public bool HasCanonicalPattern { get; set; }
        public decimal LoggingCoverage { get; set; }
    }

    #endregion
}