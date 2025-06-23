using Microsoft.Build.Locator;
using Spectre.Console;
using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

namespace TradingPlatform.CodeQuality;

/// <summary>
/// Main entry point for code quality monitoring
/// Provides CLI interface for running comprehensive code analysis
/// </summary>
public class CodeQualityRunner
{
    public static async Task<int> Main(string[] args)
    {
        // Register MSBuild before any Microsoft.Build operations
        MSBuildLocator.RegisterDefaults();

        var rootCommand = new RootCommand("Trading Platform Code Quality Monitor - Comprehensive analysis using FOSS tools");

        var solutionOption = new Option<string>(
            name: "--solution",
            description: "Path to the solution file",
            getDefaultValue: () => FindSolutionFile());
        
        var outputOption = new Option<string>(
            name: "--output",
            description: "Output directory for reports",
            getDefaultValue: () => "CodeQualityReports");
        
        var watchOption = new Option<bool>(
            name: "--watch",
            description: "Enable continuous monitoring mode");

        rootCommand.AddOption(solutionOption);
        rootCommand.AddOption(outputOption);
        rootCommand.AddOption(watchOption);

        rootCommand.SetHandler(async (string solution, string output, bool watch) =>
        {
            if (!File.Exists(solution))
            {
                AnsiConsole.MarkupLine($"[red]Error: Solution file not found: {solution}[/]");
                Environment.Exit(1);
            }

            Directory.CreateDirectory(output);

            if (watch)
            {
                await RunContinuousMonitoring(solution, output);
            }
            else
            {
                await RunSingleAnalysis(solution, output);
            }
        }, solutionOption, outputOption, watchOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunSingleAnalysis(string solutionPath, string outputDir)
    {
        try
        {
            var monitor = new CodeQualityMonitor(solutionPath);
            var report = await monitor.RunComprehensiveAnalysisAsync();
            
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var reportPath = Path.Combine(outputDir, $"CodeQualityReport_{timestamp}.html");
            
            await monitor.SaveReportAsync(reportPath);
            
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]Reports saved to:[/]");
            AnsiConsole.MarkupLine($"  HTML: {reportPath}");
            AnsiConsole.MarkupLine($"  Markdown: {Path.ChangeExtension(reportPath, ".md")}");
            AnsiConsole.MarkupLine($"  JSON: {Path.ChangeExtension(reportPath, ".json")}");
            
            // Exit with non-zero code if critical issues found
            if (report.CriticalIssues > 0)
            {
                AnsiConsole.MarkupLine($"[red]Critical issues found: {report.CriticalIssues}[/]");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            Environment.Exit(1);
        }
    }

    private static async Task RunContinuousMonitoring(string solutionPath, string outputDir)
    {
        AnsiConsole.MarkupLine("[yellow]Continuous monitoring mode - Press Ctrl+C to stop[/]");
        
        var watcher = new FileSystemWatcher(Path.GetDirectoryName(solutionPath)!)
        {
            Filter = "*.cs",
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };

        var lastAnalysis = DateTime.MinValue;
        var debounceTime = TimeSpan.FromSeconds(5);

        watcher.Changed += async (sender, e) =>
        {
            if (DateTime.Now - lastAnalysis > debounceTime)
            {
                lastAnalysis = DateTime.Now;
                AnsiConsole.MarkupLine($"[dim]Change detected in {e.Name}. Running analysis...[/]");
                await RunSingleAnalysis(solutionPath, outputDir);
            }
        };

        watcher.EnableRaisingEvents = true;
        
        // Run initial analysis
        await RunSingleAnalysis(solutionPath, outputDir);
        
        // Keep the program running
        await Task.Delay(Timeout.Infinite);
    }

    private static string FindSolutionFile()
    {
        var currentDir = Directory.GetCurrentDirectory();
        
        // Look for solution in current directory
        var solutions = Directory.GetFiles(currentDir, "*.sln");
        if (solutions.Length > 0)
            return solutions[0];
        
        // Look in parent directory
        var parentDir = Directory.GetParent(currentDir)?.FullName;
        if (parentDir != null)
        {
            solutions = Directory.GetFiles(parentDir, "*.sln");
            if (solutions.Length > 0)
                return solutions[0];
        }
        
        return "DayTradinPlatform.sln";
    }
}