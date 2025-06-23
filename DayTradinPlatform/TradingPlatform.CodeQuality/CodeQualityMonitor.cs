using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TradingPlatform.CodeAnalysis;

namespace TradingPlatform.CodeQuality;

/// <summary>
/// Comprehensive code quality monitoring system that integrates multiple FOSS analyzers
/// Based on recommendations from Comprehensive_Code_Analyzers.md
/// </summary>
public class CodeQualityMonitor
{
    private readonly string _solutionPath;
    private readonly List<ICodeQualityAnalyzer> _analyzers = new();
    private readonly CodeQualityReport _report = new();

    public CodeQualityMonitor(string solutionPath)
    {
        _solutionPath = solutionPath;
        InitializeAnalyzers();
    }

    private void InitializeAnalyzers()
    {
        // Add all our analyzers
        _analyzers.Add(new LoggingAnalyzerAdapter());
        _analyzers.Add(new RoslynDiagnosticsAnalyzer());
        _analyzers.Add(new SecurityAnalyzer());
        _analyzers.Add(new PerformanceAnalyzer());
        _analyzers.Add(new ArchitectureAnalyzer());
        _analyzers.Add(new CodeMetricsAnalyzer());
    }

    public async Task<CodeQualityReport> RunComprehensiveAnalysisAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        AnsiConsole.Write(
            new FigletText("Code Quality Monitor")
                .LeftJustified()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[bold]Trading Platform Code Quality Analysis[/]");
        AnsiConsole.MarkupLine($"[dim]Solution: {_solutionPath}[/]");
        AnsiConsole.WriteLine();

        // MSBuildLocator.RegisterDefaults() is already called in Main()

        using var workspace = MSBuildWorkspace.Create();
        
        // Configure workspace
        workspace.WorkspaceFailed += (sender, args) =>
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: {args.Diagnostic.Message}[/]");
        };

        await AnsiConsole.Progress()
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn(),
            })
            .StartAsync(async ctx =>
            {
                var loadTask = ctx.AddTask("[green]Loading solution...[/]");
                var solution = await workspace.OpenSolutionAsync(_solutionPath);
                loadTask.Increment(100);

                var analyzerTask = ctx.AddTask($"[green]Running {_analyzers.Count} analyzers...[/]", maxValue: _analyzers.Count);
                
                foreach (var analyzer in _analyzers)
                {
                    analyzerTask.Description = $"[green]Running {analyzer.Name}...[/]";
                    var analyzerReport = await analyzer.AnalyzeAsync(solution);
                    _report.MergeAnalyzerReport(analyzerReport);
                    analyzerTask.Increment(1);
                }
            });

        stopwatch.Stop();
        _report.AnalysisTime = stopwatch.Elapsed;
        
        DisplayReport(_report);
        return _report;
    }

    private void DisplayReport(CodeQualityReport report)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold blue]Analysis Complete[/]"));
        
        // Summary table
        var summaryTable = new Table();
        summaryTable.AddColumn("Metric");
        summaryTable.AddColumn("Value");
        
        summaryTable.AddRow("Total Issues", report.TotalIssues.ToString());
        summaryTable.AddRow("Critical Issues", $"[red]{report.CriticalIssues}[/]");
        summaryTable.AddRow("High Priority Issues", $"[orange1]{report.HighPriorityIssues}[/]");
        summaryTable.AddRow("Medium Priority Issues", $"[yellow]{report.MediumPriorityIssues}[/]");
        summaryTable.AddRow("Low Priority Issues", $"[green]{report.LowPriorityIssues}[/]");
        summaryTable.AddRow("Analysis Time", $"{report.AnalysisTime.TotalSeconds:F2} seconds");
        
        AnsiConsole.Write(summaryTable);
        
        // Issues by category
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold]Issues by Category[/]"));
        
        var categoryChart = new BarChart()
            .Width(60)
            .Label("[green bold]Issue Distribution[/]");
            
        foreach (var category in report.IssuesByCategory.OrderByDescending(x => x.Value))
        {
            categoryChart.AddItem(category.Key, category.Value, GetCategoryColor(category.Key));
        }
        
        AnsiConsole.Write(categoryChart);
        
        // Critical issues detail
        if (report.CriticalIssues > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold red]Critical Issues Requiring Immediate Attention[/]"));
            
            var criticalTable = new Table();
            criticalTable.AddColumn("File");
            criticalTable.AddColumn("Line");
            criticalTable.AddColumn("Issue");
            criticalTable.AddColumn("Category");
            
            foreach (var issue in report.Issues.Where(i => i.Severity == IssueSeverity.Critical).Take(10))
            {
                criticalTable.AddRow(
                    Path.GetFileName(issue.FilePath),
                    issue.Line.ToString(),
                    Markup.Escape(issue.Message),
                    issue.Category
                );
            }
            
            AnsiConsole.Write(criticalTable);
            
            if (report.CriticalIssues > 10)
            {
                AnsiConsole.MarkupLine($"[dim]... and {report.CriticalIssues - 10} more critical issues[/]");
            }
        }
    }

    private Color GetCategoryColor(string category)
    {
        return category switch
        {
            "Security" => Color.Red,
            "Performance" => Color.Orange1,
            "Logging" => Color.Yellow,
            "Architecture" => Color.Blue,
            "CodeStyle" => Color.Green,
            _ => Color.Grey
        };
    }

    public async Task SaveReportAsync(string outputPath)
    {
        var reportGenerator = new ReportGenerator();
        await reportGenerator.GenerateHtmlReport(_report, outputPath);
        await reportGenerator.GenerateMarkdownReport(_report, Path.ChangeExtension(outputPath, ".md"));
        await reportGenerator.GenerateJsonReport(_report, Path.ChangeExtension(outputPath, ".json"));
    }
}

public interface ICodeQualityAnalyzer
{
    string Name { get; }
    Task<AnalyzerReport> AnalyzeAsync(Solution solution);
}

public class CodeQualityReport
{
    public int TotalIssues => Issues.Count;
    public int CriticalIssues => Issues.Count(i => i.Severity == IssueSeverity.Critical);
    public int HighPriorityIssues => Issues.Count(i => i.Severity == IssueSeverity.High);
    public int MediumPriorityIssues => Issues.Count(i => i.Severity == IssueSeverity.Medium);
    public int LowPriorityIssues => Issues.Count(i => i.Severity == IssueSeverity.Low);
    
    public List<CodeIssue> Issues { get; set; } = new();
    public Dictionary<string, int> IssuesByCategory { get; set; } = new();
    public Dictionary<string, int> IssuesByProject { get; set; } = new();
    public Dictionary<string, List<CodeIssue>> IssuesByFile { get; set; } = new();
    public TimeSpan AnalysisTime { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    
    public void MergeAnalyzerReport(AnalyzerReport report)
    {
        Issues.AddRange(report.Issues);
        
        foreach (var issue in report.Issues)
        {
            // Update category counts
            if (!IssuesByCategory.ContainsKey(issue.Category))
                IssuesByCategory[issue.Category] = 0;
            IssuesByCategory[issue.Category]++;
            
            // Update project counts
            var projectName = GetProjectFromPath(issue.FilePath);
            if (!IssuesByProject.ContainsKey(projectName))
                IssuesByProject[projectName] = 0;
            IssuesByProject[projectName]++;
            
            // Update file grouping
            if (!IssuesByFile.ContainsKey(issue.FilePath))
                IssuesByFile[issue.FilePath] = new List<CodeIssue>();
            IssuesByFile[issue.FilePath].Add(issue);
        }
    }
    
    private string GetProjectFromPath(string filePath)
    {
        var parts = filePath.Split(Path.DirectorySeparatorChar);
        var projectIndex = Array.FindIndex(parts, p => p.StartsWith("TradingPlatform."));
        return projectIndex >= 0 ? parts[projectIndex] : "Unknown";
    }
}

public class AnalyzerReport
{
    public string AnalyzerName { get; set; } = "";
    public List<CodeIssue> Issues { get; set; } = new();
}

public class CodeIssue
{
    public string Id { get; set; } = "";
    public string Category { get; set; } = "";
    public IssueSeverity Severity { get; set; }
    public string Message { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public string? CodeSnippet { get; set; }
    public string? SuggestedFix { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}

public enum IssueSeverity
{
    Critical = 4,
    High = 3,
    Medium = 2,
    Low = 1,
    Info = 0
}