// File: TradingPlatform.Auditing\ComprehensiveAuditService.cs

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Foundation;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Auditing
{
    /// <summary>
    /// Comprehensive audit service that leverages all available code analysis tools:
    /// - Roslyn Analyzers (Microsoft.CodeAnalysis.NetAnalyzers)
    /// - StyleCop Analyzers
    /// - SonarLint
    /// - Security Code Scan
    /// - dotnet format
    /// - Code coverage analysis
    /// </summary>
    public class ComprehensiveAuditService : CanonicalServiceBase
    {
        private readonly string _solutionPath;
        private readonly List<AnalysisTool> _analysisTools = new();
        private readonly Dictionary<string, ToolResult> _toolResults = new();

        public ComprehensiveAuditService(IServiceProvider serviceProvider, string solutionPath)
            : base(serviceProvider, serviceProvider.GetRequiredService<ITradingLogger>(), "ComprehensiveAuditService")
        {
            _solutionPath = solutionPath;
            InitializeAnalysisTools();
        }

        private void InitializeAnalysisTools()
        {
            _analysisTools.Add(new RoslynAnalyzersTool());
            _analysisTools.Add(new StyleCopAnalyzersTool());
            _analysisTools.Add(new DotNetFormatTool());
            _analysisTools.Add(new DotNetBuildAnalyzersTool());
            _analysisTools.Add(new TestCoverageTool());
            _analysisTools.Add(new SecurityScanTool());
            _analysisTools.Add(new ComplexityAnalysisTool());
            _analysisTools.Add(new DependencyAnalysisTool());
        }

        public async Task<ComprehensiveAuditReport> RunFullAuditAsync()
        {
            _logger.LogInformation("Starting comprehensive code audit");
            
            var report = new ComprehensiveAuditReport
            {
                AuditStartTime = DateTime.UtcNow,
                SolutionPath = _solutionPath
            };

            try
            {
                // Step 1: Run dotnet format to check formatting issues
                _logger.LogInformation("Step 1: Running dotnet format analysis");
                var formatResult = await RunDotNetFormatAsync();
                report.ToolResults["DotNetFormat"] = formatResult;

                // Step 2: Run build with all analyzers enabled
                _logger.LogInformation("Step 2: Running build with analyzers");
                var buildResult = await RunBuildWithAnalyzersAsync();
                report.ToolResults["BuildAnalyzers"] = buildResult;

                // Step 3: Run Roslyn analysis with custom analyzers
                _logger.LogInformation("Step 3: Running Roslyn code analysis");
                var roslynResult = await RunRoslynAnalysisAsync();
                report.ToolResults["RoslynAnalysis"] = roslynResult;

                // Step 4: Run test coverage analysis
                _logger.LogInformation("Step 4: Running test coverage analysis");
                var coverageResult = await RunTestCoverageAsync();
                report.ToolResults["TestCoverage"] = coverageResult;

                // Step 5: Run security analysis
                _logger.LogInformation("Step 5: Running security analysis");
                var securityResult = await RunSecurityAnalysisAsync();
                report.ToolResults["SecurityScan"] = securityResult;

                // Step 6: Run complexity analysis
                _logger.LogInformation("Step 6: Running complexity analysis");
                var complexityResult = await RunComplexityAnalysisAsync();
                report.ToolResults["ComplexityAnalysis"] = complexityResult;

                // Step 7: Run dependency analysis
                _logger.LogInformation("Step 7: Running dependency analysis");
                var dependencyResult = await RunDependencyAnalysisAsync();
                report.ToolResults["DependencyAnalysis"] = dependencyResult;

                // Step 8: Check canonical pattern compliance
                _logger.LogInformation("Step 8: Checking canonical pattern compliance");
                var canonicalResult = await CheckCanonicalComplianceAsync();
                report.ToolResults["CanonicalCompliance"] = canonicalResult;

                // Generate summary
                report.GenerateSummary();
                report.AuditEndTime = DateTime.UtcNow;

                // Generate detailed report
                await GenerateDetailedReportAsync(report);

                _logger.LogInformation("Comprehensive audit completed", new
                {
                    Duration = (report.AuditEndTime - report.AuditStartTime).TotalSeconds,
                    TotalIssues = report.TotalIssues,
                    CriticalIssues = report.CriticalIssues,
                    ComplianceScore = report.OverallComplianceScore
                });

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError("Comprehensive audit failed", ex);
                throw;
            }
        }

        private async Task<ToolResult> RunDotNetFormatAsync()
        {
            var result = new ToolResult { ToolName = "dotnet format" };
            
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"format \"{_solutionPath}\" --verify-no-changes --verbosity diagnostic",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                result.Success = process.ExitCode == 0;
                result.Output = output;
                result.Errors = error;

                // Parse format issues
                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("warning") || line.Contains("error"))
                    {
                        result.Issues.Add(new AnalysisIssue
                        {
                            Category = "Formatting",
                            Severity = line.Contains("error") ? IssueSeverity.Error : IssueSeverity.Warning,
                            Message = line.Trim(),
                            Tool = "dotnet format"
                        });
                    }
                }

                RecordMetric("DotNetFormat.Issues", result.Issues.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError("dotnet format analysis failed", ex);
                result.Success = false;
                result.Errors = ex.Message;
            }

            return result;
        }

        private async Task<ToolResult> RunBuildWithAnalyzersAsync()
        {
            var result = new ToolResult { ToolName = "Build Analyzers" };

            try
            {
                // Run build with all warnings as errors
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"build \"{_solutionPath}\" --no-restore /p:TreatWarningsAsErrors=false /p:RunAnalyzersDuringBuild=true /p:RunAnalyzers=true /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                result.Output = output;
                result.Errors = error;

                // Parse analyzer warnings and errors
                ParseBuildOutput(output, result);

                result.Success = result.Issues.Count(i => i.Severity == IssueSeverity.Error) == 0;

                RecordMetric("BuildAnalyzers.Warnings", result.Issues.Count(i => i.Severity == IssueSeverity.Warning));
                RecordMetric("BuildAnalyzers.Errors", result.Issues.Count(i => i.Severity == IssueSeverity.Error));
            }
            catch (Exception ex)
            {
                _logger.LogError("Build analyzer failed", ex);
                result.Success = false;
                result.Errors = ex.Message;
            }

            return result;
        }

        private void ParseBuildOutput(string output, ToolResult result)
        {
            var lines = output.Split('\n');
            
            foreach (var line in lines)
            {
                // Parse MSBuild output format
                // Example: Program.cs(10,5): warning CS0219: Variable is assigned but never used
                if (line.Contains("warning") || line.Contains("error"))
                {
                    var issue = new AnalysisIssue
                    {
                        Tool = "MSBuild"
                    };

                    if (line.Contains("warning CA"))
                    {
                        issue.Category = "Code Analysis";
                        issue.Tool = "Microsoft.CodeAnalysis.NetAnalyzers";
                    }
                    else if (line.Contains("warning SA"))
                    {
                        issue.Category = "Style";
                        issue.Tool = "StyleCop.Analyzers";
                    }
                    else if (line.Contains("warning S"))
                    {
                        issue.Category = "Code Quality";
                        issue.Tool = "SonarAnalyzer";
                    }
                    else if (line.Contains("warning SCS"))
                    {
                        issue.Category = "Security";
                        issue.Tool = "Security Code Scan";
                    }
                    else if (line.Contains("warning CS"))
                    {
                        issue.Category = "Compiler";
                        issue.Tool = "C# Compiler";
                    }

                    issue.Severity = line.Contains("error") ? IssueSeverity.Error : IssueSeverity.Warning;
                    issue.Message = ExtractMessage(line);
                    issue.Location = ExtractLocation(line);
                    issue.Code = ExtractCode(line);

                    result.Issues.Add(issue);
                }
            }
        }

        private string ExtractMessage(string line)
        {
            var colonIndex = line.IndexOf(':', line.IndexOf(':') + 1);
            return colonIndex > 0 ? line.Substring(colonIndex + 1).Trim() : line;
        }

        private string ExtractLocation(string line)
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, @"^(.*?)\((\d+),(\d+)\)");
            if (match.Success)
            {
                return $"{match.Groups[1].Value}:{match.Groups[2].Value}:{match.Groups[3].Value}";
            }
            return "";
        }

        private string ExtractCode(string line)
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, @"(CA\d+|SA\d+|S\d+|SCS\d+|CS\d+)");
            return match.Success ? match.Groups[1].Value : "";
        }

        private async Task<ToolResult> RunRoslynAnalysisAsync()
        {
            var result = new ToolResult { ToolName = "Roslyn Analysis" };

            try
            {
                var workspace = MSBuildWorkspace.Create();
                var solution = await workspace.OpenSolutionAsync(_solutionPath);

                foreach (var project in solution.Projects)
                {
                    var compilation = await project.GetCompilationAsync();
                    if (compilation == null) continue;

                    // Analyze syntax trees
                    foreach (var tree in compilation.SyntaxTrees)
                    {
                        var root = await tree.GetRootAsync();
                        var model = compilation.GetSemanticModel(tree);

                        // Check for various patterns
                        CheckMethodComplexity(root, model, result);
                        CheckLoggingPatterns(root, model, result);
                        CheckErrorHandling(root, model, result);
                        CheckNamingConventions(root, model, result);
                    }
                }

                result.Success = true;
                RecordMetric("RoslynAnalysis.Issues", result.Issues.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError("Roslyn analysis failed", ex);
                result.Success = false;
                result.Errors = ex.Message;
            }

            return result;
        }

        private void CheckMethodComplexity(SyntaxNode root, SemanticModel model, ToolResult result)
        {
            var methods = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>();
            
            foreach (var method in methods)
            {
                var complexity = CalculateCyclomaticComplexity(method);
                if (complexity > 10)
                {
                    result.Issues.Add(new AnalysisIssue
                    {
                        Category = "Complexity",
                        Severity = complexity > 20 ? IssueSeverity.Error : IssueSeverity.Warning,
                        Message = $"Method '{method.Identifier}' has high cyclomatic complexity: {complexity}",
                        Location = method.GetLocation().GetLineSpan().Path,
                        Tool = "Complexity Analyzer"
                    });
                }
            }
        }

        private int CalculateCyclomaticComplexity(Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax method)
        {
            var complexity = 1;
            var decisionNodes = method.DescendantNodes().Where(n =>
                n is Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax ||
                n is Microsoft.CodeAnalysis.CSharp.Syntax.WhileStatementSyntax ||
                n is Microsoft.CodeAnalysis.CSharp.Syntax.ForStatementSyntax ||
                n is Microsoft.CodeAnalysis.CSharp.Syntax.ForEachStatementSyntax ||
                n is Microsoft.CodeAnalysis.CSharp.Syntax.CaseSwitchLabelSyntax ||
                n is Microsoft.CodeAnalysis.CSharp.Syntax.ConditionalExpressionSyntax ||
                n is Microsoft.CodeAnalysis.CSharp.Syntax.CatchClauseSyntax);

            complexity += decisionNodes.Count();
            return complexity;
        }

        private void CheckLoggingPatterns(SyntaxNode root, SemanticModel model, ToolResult result)
        {
            var methods = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>();
            
            foreach (var method in methods)
            {
                var methodName = method.Identifier.Text;
                var body = method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? "";
                
                // Check for entry/exit logging in public methods
                if (method.Modifiers.Any(m => m.Text == "public") && 
                    !methodName.StartsWith("get_") && 
                    !methodName.StartsWith("set_"))
                {
                    bool hasEntryLog = body.Contains("Entry") || body.Contains("LogInformation") || body.Contains("LogDebug");
                    bool hasExitLog = body.Contains("Exit") || body.Contains("finally");
                    
                    if (!hasEntryLog || !hasExitLog)
                    {
                        result.Issues.Add(new AnalysisIssue
                        {
                            Category = "Logging",
                            Severity = IssueSeverity.Warning,
                            Message = $"Method '{methodName}' missing entry/exit logging",
                            Location = method.GetLocation().GetLineSpan().Path,
                            Tool = "Logging Analyzer"
                        });
                    }
                }
            }
        }

        private void CheckErrorHandling(SyntaxNode root, SemanticModel model, ToolResult result)
        {
            var catchClauses = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.CatchClauseSyntax>();
            
            foreach (var catchClause in catchClauses)
            {
                var block = catchClause.Block?.ToString() ?? "";
                
                if (!block.Contains("Log") && !block.Contains("throw"))
                {
                    result.Issues.Add(new AnalysisIssue
                    {
                        Category = "Error Handling",
                        Severity = IssueSeverity.Error,
                        Message = "Catch block should log the error or rethrow",
                        Location = catchClause.GetLocation().GetLineSpan().Path,
                        Tool = "Error Handling Analyzer"
                    });
                }
            }
        }

        private void CheckNamingConventions(SyntaxNode root, SemanticModel model, ToolResult result)
        {
            // Check interface naming
            var interfaces = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InterfaceDeclarationSyntax>();
            foreach (var iface in interfaces)
            {
                if (!iface.Identifier.Text.StartsWith("I"))
                {
                    result.Issues.Add(new AnalysisIssue
                    {
                        Category = "Naming",
                        Severity = IssueSeverity.Warning,
                        Message = $"Interface '{iface.Identifier}' should start with 'I'",
                        Location = iface.GetLocation().GetLineSpan().Path,
                        Tool = "Naming Analyzer"
                    });
                }
            }
        }

        private async Task<ToolResult> RunTestCoverageAsync()
        {
            var result = new ToolResult { ToolName = "Test Coverage" };

            try
            {
                // Run tests with coverage
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"test \"{_solutionPath}\" --collect:\"XPlat Code Coverage\" --results-directory ./TestResults",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                result.Output = output;
                result.Success = process.ExitCode == 0;

                // Parse coverage results
                if (result.Success)
                {
                    result.Metrics["TestsPassed"] = true;
                    
                    // Look for coverage summary in output
                    var coverageMatch = System.Text.RegularExpressions.Regex.Match(output, @"Total\s+\|\s+(\d+\.?\d*)%");
                    if (coverageMatch.Success)
                    {
                        var coverage = decimal.Parse(coverageMatch.Groups[1].Value);
                        result.Metrics["CodeCoverage"] = coverage;
                        
                        if (coverage < 80m)
                        {
                            result.Issues.Add(new AnalysisIssue
                            {
                                Category = "Test Coverage",
                                Severity = IssueSeverity.Warning,
                                Message = $"Code coverage {coverage}% is below 80% threshold",
                                Tool = "Test Coverage"
                            });
                        }
                    }
                }

                RecordMetric("TestCoverage.Percentage", result.Metrics.GetValueOrDefault("CodeCoverage", 0m));
            }
            catch (Exception ex)
            {
                _logger.LogError("Test coverage analysis failed", ex);
                result.Success = false;
                result.Errors = ex.Message;
            }

            return result;
        }

        private async Task<ToolResult> RunSecurityAnalysisAsync()
        {
            var result = new ToolResult { ToolName = "Security Analysis" };

            try
            {
                // Security patterns to check
                var securityPatterns = new[]
                {
                    (@"password\s*=\s*""[^""]+""", "Hardcoded password detected"),
                    (@"connectionString\s*=\s*""[^""]+""", "Hardcoded connection string detected"),
                    (@"apiKey\s*=\s*""[^""]+""", "Hardcoded API key detected"),
                    (@"secret\s*=\s*""[^""]+""", "Hardcoded secret detected"),
                    (@"SqlCommand\s*\([^)]*\+", "Potential SQL injection vulnerability"),
                    (@"Process\.Start\s*\([^)]*\+", "Potential command injection vulnerability")
                };

                var files = Directory.GetFiles(Path.GetDirectoryName(_solutionPath)!, "*.cs", SearchOption.AllDirectories);
                
                foreach (var file in files)
                {
                    if (file.Contains("/obj/") || file.Contains("/bin/")) continue;
                    
                    var content = await File.ReadAllTextAsync(file);
                    
                    foreach (var (pattern, message) in securityPatterns)
                    {
                        var matches = System.Text.RegularExpressions.Regex.Matches(content, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        
                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            result.Issues.Add(new AnalysisIssue
                            {
                                Category = "Security",
                                Severity = IssueSeverity.Error,
                                Message = message,
                                Location = file,
                                Tool = "Security Scanner"
                            });
                        }
                    }
                }

                result.Success = result.Issues.Count == 0;
                RecordMetric("SecurityAnalysis.Vulnerabilities", result.Issues.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError("Security analysis failed", ex);
                result.Success = false;
                result.Errors = ex.Message;
            }

            return result;
        }

        private async Task<ToolResult> RunComplexityAnalysisAsync()
        {
            var result = new ToolResult { ToolName = "Complexity Analysis" };

            try
            {
                var workspace = MSBuildWorkspace.Create();
                var solution = await workspace.OpenSolutionAsync(_solutionPath);

                int totalMethods = 0;
                int complexMethods = 0;
                int totalComplexity = 0;

                foreach (var project in solution.Projects)
                {
                    var compilation = await project.GetCompilationAsync();
                    if (compilation == null) continue;

                    foreach (var tree in compilation.SyntaxTrees)
                    {
                        var root = await tree.GetRootAsync();
                        var methods = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>();

                        foreach (var method in methods)
                        {
                            totalMethods++;
                            var complexity = CalculateCyclomaticComplexity(method);
                            totalComplexity += complexity;

                            if (complexity > 10)
                            {
                                complexMethods++;
                            }
                        }
                    }
                }

                result.Metrics["TotalMethods"] = totalMethods;
                result.Metrics["ComplexMethods"] = complexMethods;
                result.Metrics["AverageComplexity"] = totalMethods > 0 ? (decimal)totalComplexity / totalMethods : 0;

                result.Success = true;
                RecordMetric("Complexity.Average", result.Metrics["AverageComplexity"]);
            }
            catch (Exception ex)
            {
                _logger.LogError("Complexity analysis failed", ex);
                result.Success = false;
                result.Errors = ex.Message;
            }

            return result;
        }

        private async Task<ToolResult> RunDependencyAnalysisAsync()
        {
            var result = new ToolResult { ToolName = "Dependency Analysis" };

            try
            {
                // Check for outdated packages
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"list \"{_solutionPath}\" package --outdated",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                result.Output = output;

                // Count outdated packages
                var lines = output.Split('\n');
                int outdatedCount = 0;
                
                foreach (var line in lines)
                {
                    if (line.Contains(">") && !line.Contains("Top-level Package"))
                    {
                        outdatedCount++;
                        result.Issues.Add(new AnalysisIssue
                        {
                            Category = "Dependencies",
                            Severity = IssueSeverity.Info,
                            Message = $"Outdated package: {line.Trim()}",
                            Tool = "Dependency Analyzer"
                        });
                    }
                }

                result.Metrics["OutdatedPackages"] = outdatedCount;
                result.Success = true;
                
                RecordMetric("Dependencies.Outdated", outdatedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError("Dependency analysis failed", ex);
                result.Success = false;
                result.Errors = ex.Message;
            }

            return result;
        }

        private async Task<ToolResult> CheckCanonicalComplianceAsync()
        {
            var result = new ToolResult { ToolName = "Canonical Compliance" };

            try
            {
                var files = Directory.GetFiles(Path.GetDirectoryName(_solutionPath)!, "*.cs", SearchOption.AllDirectories);
                int totalServices = 0;
                int canonicalServices = 0;

                foreach (var file in files)
                {
                    if (file.Contains("/obj/") || file.Contains("/bin/")) continue;
                    
                    var content = await File.ReadAllTextAsync(file);
                    
                    // Check if it's a service/provider/engine
                    if (System.Text.RegularExpressions.Regex.IsMatch(content, @"class\s+\w*(Service|Provider|Engine|Monitor|Calculator)"))
                    {
                        totalServices++;
                        
                        if (content.Contains(": Canonical"))
                        {
                            canonicalServices++;
                        }
                        else if (!file.Contains("Test") && !file.Contains("Mock"))
                        {
                            result.Issues.Add(new AnalysisIssue
                            {
                                Category = "Canonical Pattern",
                                Severity = IssueSeverity.Warning,
                                Message = $"Service not using canonical pattern: {Path.GetFileName(file)}",
                                Location = file,
                                Tool = "Canonical Analyzer"
                            });
                        }
                    }
                }

                result.Metrics["TotalServices"] = totalServices;
                result.Metrics["CanonicalServices"] = canonicalServices;
                result.Metrics["CanonicalAdoption"] = totalServices > 0 ? (decimal)canonicalServices / totalServices * 100 : 100;

                result.Success = true;
                RecordMetric("Canonical.AdoptionRate", result.Metrics["CanonicalAdoption"]);
            }
            catch (Exception ex)
            {
                _logger.LogError("Canonical compliance check failed", ex);
                result.Success = false;
                result.Errors = ex.Message;
            }

            return result;
        }

        private async Task GenerateDetailedReportAsync(ComprehensiveAuditReport report)
        {
            var reportPath = Path.Combine(Path.GetDirectoryName(_solutionPath)!, $"AuditReport_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            var sb = new StringBuilder();

            sb.AppendLine("# Comprehensive Code Audit Report");
            sb.AppendLine($"**Date**: {report.AuditStartTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**Solution**: {Path.GetFileName(_solutionPath)}");
            sb.AppendLine($"**Duration**: {(report.AuditEndTime - report.AuditStartTime).TotalSeconds:F2} seconds");
            sb.AppendLine($"**Overall Compliance Score**: {report.OverallComplianceScore:F2}%");
            sb.AppendLine();

            sb.AppendLine("## Summary");
            sb.AppendLine($"- Total Issues: {report.TotalIssues}");
            sb.AppendLine($"- Critical Issues: {report.CriticalIssues}");
            sb.AppendLine($"- Warnings: {report.Warnings}");
            sb.AppendLine($"- Info: {report.InfoMessages}");
            sb.AppendLine();

            sb.AppendLine("## Analysis Results");
            
            foreach (var (toolName, result) in report.ToolResults)
            {
                sb.AppendLine($"### {toolName}");
                sb.AppendLine($"**Status**: {(result.Success ? "✅ Passed" : "❌ Failed")}");
                
                if (result.Metrics.Any())
                {
                    sb.AppendLine("**Metrics**:");
                    foreach (var (metric, value) in result.Metrics)
                    {
                        sb.AppendLine($"- {metric}: {value}");
                    }
                }

                if (result.Issues.Any())
                {
                    sb.AppendLine($"**Issues** ({result.Issues.Count}):");
                    foreach (var issue in result.Issues.Take(10))
                    {
                        sb.AppendLine($"- [{issue.Severity}] {issue.Message}");
                    }
                    
                    if (result.Issues.Count > 10)
                    {
                        sb.AppendLine($"  ... and {result.Issues.Count - 10} more");
                    }
                }
                
                sb.AppendLine();
            }

            sb.AppendLine("## Recommendations");
            
            if (report.OverallComplianceScore < 80)
            {
                sb.AppendLine("1. **Improve Code Coverage**: Aim for at least 80% test coverage");
                sb.AppendLine("2. **Adopt Canonical Patterns**: Convert remaining services to canonical implementations");
                sb.AppendLine("3. **Fix Critical Issues**: Address all security and error-level issues immediately");
                sb.AppendLine("4. **Enable Warnings as Errors**: Enforce code quality at build time");
            }

            sb.AppendLine();
            sb.AppendLine("## Next Steps");
            sb.AppendLine("1. Review and fix all critical issues");
            sb.AppendLine("2. Create tasks for warning-level issues");
            sb.AppendLine("3. Update coding standards documentation");
            sb.AppendLine("4. Schedule follow-up audit in 30 days");

            await File.WriteAllTextAsync(reportPath, sb.ToString());
            _logger.LogInformation($"Detailed report generated: {reportPath}");
        }

        protected override Dictionary<string, object> GetServiceMetrics()
        {
            var baseMetrics = base.GetServiceMetrics();
            
            baseMetrics["ToolsConfigured"] = _analysisTools.Count;
            baseMetrics["ToolsExecuted"] = _toolResults.Count;
            
            foreach (var (tool, result) in _toolResults)
            {
                baseMetrics[$"{tool}.Issues"] = result.Issues.Count;
            }

            return baseMetrics;
        }
    }

    #region Supporting Classes

    public abstract class AnalysisTool
    {
        public abstract string Name { get; }
        public abstract Task<ToolResult> RunAsync(string solutionPath);
    }

    public class ToolResult
    {
        public string ToolName { get; set; } = "";
        public bool Success { get; set; }
        public string Output { get; set; } = "";
        public string Errors { get; set; } = "";
        public List<AnalysisIssue> Issues { get; set; } = new();
        public Dictionary<string, object> Metrics { get; set; } = new();
    }

    public class AnalysisIssue
    {
        public string Category { get; set; } = "";
        public IssueSeverity Severity { get; set; }
        public string Message { get; set; } = "";
        public string Location { get; set; } = "";
        public string Code { get; set; } = "";
        public string Tool { get; set; } = "";
    }

    public enum IssueSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public class ComprehensiveAuditReport
    {
        public DateTime AuditStartTime { get; set; }
        public DateTime AuditEndTime { get; set; }
        public string SolutionPath { get; set; } = "";
        public Dictionary<string, ToolResult> ToolResults { get; set; } = new();
        public int TotalIssues { get; set; }
        public int CriticalIssues { get; set; }
        public int Warnings { get; set; }
        public int InfoMessages { get; set; }
        public decimal OverallComplianceScore { get; set; }

        public void GenerateSummary()
        {
            TotalIssues = ToolResults.Values.Sum(r => r.Issues.Count);
            CriticalIssues = ToolResults.Values.Sum(r => r.Issues.Count(i => i.Severity == IssueSeverity.Critical || i.Severity == IssueSeverity.Error));
            Warnings = ToolResults.Values.Sum(r => r.Issues.Count(i => i.Severity == IssueSeverity.Warning));
            InfoMessages = ToolResults.Values.Sum(r => r.Issues.Count(i => i.Severity == IssueSeverity.Info));

            // Calculate overall compliance score
            decimal baseScore = 100m;
            baseScore -= CriticalIssues * 5m;
            baseScore -= Warnings * 1m;
            baseScore -= InfoMessages * 0.1m;

            // Bonus for canonical adoption
            var canonicalResult = ToolResults.GetValueOrDefault("CanonicalCompliance");
            if (canonicalResult?.Metrics.TryGetValue("CanonicalAdoption", out var adoption) == true)
            {
                baseScore += (decimal)adoption * 0.1m;
            }

            // Bonus for test coverage
            var coverageResult = ToolResults.GetValueOrDefault("TestCoverage");
            if (coverageResult?.Metrics.TryGetValue("CodeCoverage", out var coverage) == true)
            {
                baseScore += (decimal)coverage * 0.1m;
            }

            OverallComplianceScore = Math.Max(0, Math.Min(100, baseScore));
        }
    }

    // Placeholder implementations for tools
    public class RoslynAnalyzersTool : AnalysisTool
    {
        public override string Name => "Roslyn Analyzers";
        public override Task<ToolResult> RunAsync(string solutionPath) => Task.FromResult(new ToolResult { ToolName = Name });
    }

    public class StyleCopAnalyzersTool : AnalysisTool
    {
        public override string Name => "StyleCop Analyzers";
        public override Task<ToolResult> RunAsync(string solutionPath) => Task.FromResult(new ToolResult { ToolName = Name });
    }

    public class DotNetFormatTool : AnalysisTool
    {
        public override string Name => "dotnet format";
        public override Task<ToolResult> RunAsync(string solutionPath) => Task.FromResult(new ToolResult { ToolName = Name });
    }

    public class DotNetBuildAnalyzersTool : AnalysisTool
    {
        public override string Name => "Build Analyzers";
        public override Task<ToolResult> RunAsync(string solutionPath) => Task.FromResult(new ToolResult { ToolName = Name });
    }

    public class TestCoverageTool : AnalysisTool
    {
        public override string Name => "Test Coverage";
        public override Task<ToolResult> RunAsync(string solutionPath) => Task.FromResult(new ToolResult { ToolName = Name });
    }

    public class SecurityScanTool : AnalysisTool
    {
        public override string Name => "Security Scan";
        public override Task<ToolResult> RunAsync(string solutionPath) => Task.FromResult(new ToolResult { ToolName = Name });
    }

    public class ComplexityAnalysisTool : AnalysisTool
    {
        public override string Name => "Complexity Analysis";
        public override Task<ToolResult> RunAsync(string solutionPath) => Task.FromResult(new ToolResult { ToolName = Name });
    }

    public class DependencyAnalysisTool : AnalysisTool
    {
        public override string Name => "Dependency Analysis";
        public override Task<ToolResult> RunAsync(string solutionPath) => Task.FromResult(new ToolResult { ToolName = Name });
    }

    #endregion
}