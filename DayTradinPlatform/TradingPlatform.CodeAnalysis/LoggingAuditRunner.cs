using Microsoft.Build.Locator;
using System;
using System.IO;
using System.Threading.Tasks;
using TradingPlatform.CodeAnalysis;

namespace TradingPlatform.CodeAnalysis;

public class LoggingAuditRunner
{
    static async Task Main(string[] args)
    {
        // Initialize MSBuild
        MSBuildLocator.RegisterDefaults();

        // Find solution file
        var currentDir = Directory.GetCurrentDirectory();
        var solutionPath = Path.Combine(Path.GetDirectoryName(currentDir)!, "DayTradinPlatform.sln");
        
        if (!File.Exists(solutionPath))
        {
            Console.WriteLine($"Solution not found at: {solutionPath}");
            return;
        }

        Console.WriteLine("=== Trading Platform Logging Audit ===");
        Console.WriteLine($"Solution: {solutionPath}");
        Console.WriteLine();

        var analyzer = new LoggingAnalyzer();
        
        try
        {
            var report = await analyzer.AnalyzeSolutionAsync(solutionPath);
            
            // Save report
            var reportPath = Path.Combine(currentDir, $"LoggingAuditReport_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.md");
            report.SaveToFile(reportPath);
            
            // Display summary
            Console.WriteLine("\n=== AUDIT COMPLETE ===");
            Console.WriteLine($"Total Violations Found: {report.TotalViolations}");
            Console.WriteLine($"Report saved to: {reportPath}");
            Console.WriteLine("\nViolations by Type:");
            foreach (var (type, count) in report.ViolationsByType)
            {
                Console.WriteLine($"  {type}: {count}");
            }
            
            Console.WriteLine("\nViolations by Project:");
            foreach (var (project, count) in report.ViolationsByProject)
            {
                Console.WriteLine($"  {project}: {count}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}