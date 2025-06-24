// File: TradingPlatform.Auditing\AuditRunner.cs

using System.Diagnostics;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Auditing
{
    /// <summary>
    /// Console application runner for executing comprehensive audits
    /// </summary>
    public class AuditRunner
    {
        public static async Task<int> Main(string[] args)
        {
            Console.WriteLine("=== DayTradingPlatform Comprehensive Audit Runner ===");
            Console.WriteLine($"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();

            try
            {
                // Initialize MSBuild
                MSBuildLocator.RegisterDefaults();
                
                // Find solution file
                var currentDir = Directory.GetCurrentDirectory();
                var solutionPath = FindSolutionFile(currentDir);
                
                if (string.IsNullOrEmpty(solutionPath))
                {
                    Console.WriteLine("ERROR: Could not find .sln file in current directory or parent directories");
                    return 1;
                }

                Console.WriteLine($"Solution found: {Path.GetFileName(solutionPath)}");
                Console.WriteLine($"Path: {solutionPath}");
                Console.WriteLine();

                // Setup dependency injection
                var services = new ServiceCollection();
                ConfigureServices(services);
                
                using var serviceProvider = services.BuildServiceProvider();
                
                // Create and run audit service
                var auditService = new ComprehensiveAuditService(serviceProvider, solutionPath);
                
                Console.WriteLine("Starting comprehensive audit...");
                Console.WriteLine("This will run multiple analysis tools:");
                Console.WriteLine("  1. dotnet format");
                Console.WriteLine("  2. Build with analyzers");
                Console.WriteLine("  3. Roslyn analysis");
                Console.WriteLine("  4. Test coverage");
                Console.WriteLine("  5. Security analysis");
                Console.WriteLine("  6. Complexity analysis");
                Console.WriteLine("  7. Dependency analysis");
                Console.WriteLine("  8. Canonical compliance");
                Console.WriteLine();

                var stopwatch = Stopwatch.StartNew();
                var report = await auditService.RunFullAuditAsync();
                stopwatch.Stop();

                // Display results
                Console.WriteLine();
                Console.WriteLine("=== AUDIT RESULTS ===");
                Console.WriteLine($"Duration: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
                Console.WriteLine($"Overall Compliance Score: {report.OverallComplianceScore:F2}%");
                Console.WriteLine($"Total Issues: {report.TotalIssues}");
                Console.WriteLine($"Critical Issues: {report.CriticalIssues}");
                Console.WriteLine($"Warnings: {report.Warnings}");
                Console.WriteLine($"Info Messages: {report.InfoMessages}");
                Console.WriteLine();

                // Display tool-specific results
                Console.WriteLine("=== TOOL RESULTS ===");
                foreach (var (toolName, result) in report.ToolResults)
                {
                    Console.WriteLine($"\n{toolName}:");
                    Console.WriteLine($"  Status: {(result.Success ? "✅ PASSED" : "❌ FAILED")}");
                    
                    if (result.Metrics.Any())
                    {
                        Console.WriteLine("  Metrics:");
                        foreach (var (metric, value) in result.Metrics)
                        {
                            Console.WriteLine($"    - {metric}: {value}");
                        }
                    }

                    if (result.Issues.Any())
                    {
                        Console.WriteLine($"  Issues ({result.Issues.Count}):");
                        var issueSummary = result.Issues
                            .GroupBy(i => i.Severity)
                            .OrderByDescending(g => g.Key)
                            .Select(g => $"{g.Key}: {g.Count()}");
                        
                        foreach (var summary in issueSummary)
                        {
                            Console.WriteLine($"    - {summary}");
                        }

                        // Show first 5 issues as examples
                        Console.WriteLine("  Sample issues:");
                        foreach (var issue in result.Issues.Take(5))
                        {
                            Console.WriteLine($"    [{issue.Severity}] {issue.Message}");
                            if (!string.IsNullOrEmpty(issue.Location))
                            {
                                Console.WriteLine($"      Location: {issue.Location}");
                            }
                        }
                        
                        if (result.Issues.Count > 5)
                        {
                            Console.WriteLine($"    ... and {result.Issues.Count - 5} more");
                        }
                    }
                }

                // Check if we should fail the build
                if (report.CriticalIssues > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("❌ AUDIT FAILED: Critical issues found!");
                    Console.WriteLine("Please fix critical issues before proceeding.");
                    return 1;
                }
                else if (report.OverallComplianceScore < 70)
                {
                    Console.WriteLine();
                    Console.WriteLine("⚠️  WARNING: Compliance score below 70%");
                    Console.WriteLine("Consider addressing warnings to improve code quality.");
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("✅ AUDIT PASSED");
                }

                // Report location
                var reportFiles = Directory.GetFiles(Path.GetDirectoryName(solutionPath)!, "AuditReport_*.md");
                if (reportFiles.Any())
                {
                    var latestReport = reportFiles.OrderByDescending(f => f).First();
                    Console.WriteLine();
                    Console.WriteLine($"Detailed report saved to: {Path.GetFileName(latestReport)}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"❌ FATAL ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 2;
            }
        }

        private static string FindSolutionFile(string startPath)
        {
            var currentDir = new DirectoryInfo(startPath);
            
            while (currentDir != null)
            {
                var solutionFiles = currentDir.GetFiles("*.sln");
                if (solutionFiles.Length > 0)
                {
                    return solutionFiles[0].FullName;
                }
                
                currentDir = currentDir.Parent;
            }
            
            return string.Empty;
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Add trading logger
            services.AddSingleton<ITradingLogger>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<TradingLogger>>();
                return new TradingLogger(logger);
            });

            // Add other required services
            services.AddSingleton<IServiceProvider>(provider => provider);
        }
    }

    // Simple trading logger implementation for audit runner
    internal class TradingLogger : ITradingLogger
    {
        private readonly ILogger<TradingLogger> _logger;

        public TradingLogger(ILogger<TradingLogger> logger)
        {
            _logger = logger;
        }

        public void LogInformation(string message, object? context = null)
        {
            if (context != null)
            {
                _logger.LogInformation("{Message} {@Context}", message, context);
            }
            else
            {
                _logger.LogInformation(message);
            }
        }

        public void LogWarning(string message, object? context = null)
        {
            if (context != null)
            {
                _logger.LogWarning("{Message} {@Context}", message, context);
            }
            else
            {
                _logger.LogWarning(message);
            }
        }

        public void LogError(string message, Exception? exception = null, object? context = null)
        {
            if (exception != null)
            {
                _logger.LogError(exception, "{Message} {@Context}", message, context);
            }
            else if (context != null)
            {
                _logger.LogError("{Message} {@Context}", message, context);
            }
            else
            {
                _logger.LogError(message);
            }
        }

        public void LogDebug(string message, object? context = null)
        {
            if (context != null)
            {
                _logger.LogDebug("{Message} {@Context}", message, context);
            }
            else
            {
                _logger.LogDebug(message);
            }
        }

        public void LogMetric(string metricName, object value, Dictionary<string, object>? dimensions = null)
        {
            _logger.LogInformation("Metric: {MetricName} = {Value} {@Dimensions}", metricName, value, dimensions);
        }

        public void LogPerformance(string operationName, TimeSpan duration, Dictionary<string, object>? properties = null)
        {
            _logger.LogInformation("Performance: {Operation} took {Duration}ms {@Properties}", 
                operationName, duration.TotalMilliseconds, properties);
        }

        public void LogEntry(string methodName, object? parameters = null)
        {
            _logger.LogDebug("Entry: {Method} {@Parameters}", methodName, parameters);
        }

        public void LogExit(string methodName, object? result = null)
        {
            _logger.LogDebug("Exit: {Method} {@Result}", methodName, result);
        }
    }
}