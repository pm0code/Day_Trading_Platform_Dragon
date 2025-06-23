using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingPlatform.CodeAnalysis
{
    /// <summary>
    /// Roslyn-based analyzer for comprehensive logging audit
    /// Systematically analyzes ALL logging calls across the entire solution
    /// </summary>
    public class LoggingAnalyzer
    {
        private readonly MSBuildWorkspace _workspace;
        private readonly List<LoggingViolation> _violations = new();
        private readonly Dictionary<string, MethodSignature> _canonicalSignatures;

        public LoggingAnalyzer()
        {
            _workspace = MSBuildWorkspace.Create();
            _canonicalSignatures = InitializeCanonicalSignatures();
        }

        /// <summary>
        /// Define the canonical TradingLogOrchestrator method signatures
        /// </summary>
        private Dictionary<string, MethodSignature> InitializeCanonicalSignatures()
        {
            return new Dictionary<string, MethodSignature>
            {
                ["LogError"] = new MethodSignature
                {
                    MethodName = "LogError",
                    Parameters = new[]
                    {
                        new Parameter { Name = "message", Type = "string", Position = 0 },
                        new Parameter { Name = "exception", Type = "Exception?", Position = 1, IsOptional = true },
                        new Parameter { Name = "operationContext", Type = "string?", Position = 2, IsOptional = true },
                        new Parameter { Name = "userImpact", Type = "string?", Position = 3, IsOptional = true },
                        new Parameter { Name = "troubleshootingHints", Type = "string?", Position = 4, IsOptional = true },
                        new Parameter { Name = "additionalData", Type = "object?", Position = 5, IsOptional = true }
                    }
                },
                ["LogInfo"] = new MethodSignature
                {
                    MethodName = "LogInfo",
                    Parameters = new[]
                    {
                        new Parameter { Name = "message", Type = "string", Position = 0 },
                        new Parameter { Name = "additionalData", Type = "object?", Position = 1, IsOptional = true }
                    }
                },
                ["LogWarning"] = new MethodSignature
                {
                    MethodName = "LogWarning",
                    Parameters = new[]
                    {
                        new Parameter { Name = "message", Type = "string", Position = 0 },
                        new Parameter { Name = "impact", Type = "string?", Position = 1, IsOptional = true },
                        new Parameter { Name = "recommendedAction", Type = "string?", Position = 2, IsOptional = true },
                        new Parameter { Name = "additionalData", Type = "object?", Position = 3, IsOptional = true }
                    }
                }
            };
        }

        /// <summary>
        /// Perform comprehensive logging audit on entire solution
        /// </summary>
        public async Task<LoggingAuditReport> AnalyzeSolutionAsync(string solutionPath)
        {
            Console.WriteLine($"Loading solution: {solutionPath}");
            var solution = await _workspace.OpenSolutionAsync(solutionPath);
            
            var projectCount = 0;
            var fileCount = 0;

            foreach (var project in solution.Projects)
            {
                projectCount++;
                Console.WriteLine($"Analyzing project: {project.Name}");

                foreach (var document in project.Documents)
                {
                    if (!document.FilePath?.EndsWith(".cs") ?? true) continue;
                    
                    fileCount++;
                    var syntaxTree = await document.GetSyntaxTreeAsync();
                    if (syntaxTree == null) continue;

                    var semanticModel = await document.GetSemanticModelAsync();
                    if (semanticModel == null) continue;

                    var root = await syntaxTree.GetRootAsync();
                    
                    // Find all method invocations
                    var invocations = root.DescendantNodes()
                        .OfType<InvocationExpressionSyntax>()
                        .Where(inv => IsLoggingCall(inv));

                    foreach (var invocation in invocations)
                    {
                        AnalyzeLoggingCall(invocation, semanticModel, document.FilePath);
                    }
                }
            }

            return GenerateReport(projectCount, fileCount);
        }

        /// <summary>
        /// Check if invocation is a logging call we care about
        /// </summary>
        private bool IsLoggingCall(InvocationExpressionSyntax invocation)
        {
            var methodName = GetMethodName(invocation);
            return methodName != null && _canonicalSignatures.ContainsKey(methodName);
        }

        /// <summary>
        /// Analyze individual logging call for violations
        /// </summary>
        private void AnalyzeLoggingCall(InvocationExpressionSyntax invocation, SemanticModel semanticModel, string filePath)
        {
            var methodName = GetMethodName(invocation);
            if (methodName == null) return;

            var canonicalSignature = _canonicalSignatures[methodName];
            var arguments = invocation.ArgumentList.Arguments;

            // Check for Microsoft.Extensions.Logging usage
            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol?.ContainingNamespace?.ToString()?.Contains("Microsoft.Extensions.Logging") ?? false)
            {
                _violations.Add(new LoggingViolation
                {
                    FilePath = filePath,
                    LineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    ViolationType = ViolationType.MicrosoftLoggingUsage,
                    MethodName = methodName,
                    CurrentCode = invocation.ToString(),
                    Description = "Using Microsoft.Extensions.Logging instead of TradingLogOrchestrator"
                });
                return;
            }

            // Analyze parameter types and order
            var violations = AnalyzeParameters(invocation, arguments, canonicalSignature, semanticModel, filePath);
            _violations.AddRange(violations);
        }

        /// <summary>
        /// Analyze parameters for type and order violations
        /// </summary>
        private List<LoggingViolation> AnalyzeParameters(
            InvocationExpressionSyntax invocation,
            SeparatedSyntaxList<ArgumentSyntax> arguments,
            MethodSignature canonical,
            SemanticModel semanticModel,
            string filePath)
        {
            var violations = new List<LoggingViolation>();

            // Special handling for LogError - check if exception is in wrong position
            if (canonical.MethodName == "LogError" && arguments.Count >= 2)
            {
                var firstArgType = semanticModel.GetTypeInfo(arguments[0].Expression).Type;
                var secondArgType = semanticModel.GetTypeInfo(arguments[1].Expression).Type;

                // Check if first parameter is Exception (wrong order)
                if (IsExceptionType(firstArgType))
                {
                    violations.Add(new LoggingViolation
                    {
                        FilePath = filePath,
                        LineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        ViolationType = ViolationType.ParameterOrder,
                        MethodName = canonical.MethodName,
                        CurrentCode = invocation.ToString(),
                        Description = "LogError has Exception as first parameter - should be (string message, Exception? exception, ...)",
                        SuggestedFix = GenerateLogErrorFix(invocation, arguments)
                    });
                }
            }

            // Check for string interpolation instead of structured logging
            if (arguments.Count > 0 && arguments[0].Expression is InterpolatedStringExpressionSyntax)
            {
                violations.Add(new LoggingViolation
                {
                    FilePath = filePath,
                    LineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    ViolationType = ViolationType.StringInterpolation,
                    MethodName = canonical.MethodName,
                    CurrentCode = invocation.ToString(),
                    Description = "Using string interpolation instead of structured logging parameters"
                });
            }

            return violations;
        }

        /// <summary>
        /// Generate fix for LogError parameter order
        /// </summary>
        private string GenerateLogErrorFix(InvocationExpressionSyntax invocation, SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (arguments.Count < 2) return invocation.ToString();

            var receiver = (invocation.Expression as MemberAccessExpressionSyntax)?.Expression?.ToString() ?? "";
            var messageArg = arguments[1].ToString();
            var exceptionArg = arguments[0].ToString();
            
            var additionalArgs = arguments.Count > 2 
                ? ", " + string.Join(", ", arguments.Skip(2).Select(a => a.ToString()))
                : "";

            return $"{receiver}.LogError({messageArg}, {exceptionArg}{additionalArgs})";
        }

        private bool IsExceptionType(ITypeSymbol? type)
        {
            if (type == null) return false;
            
            var current = type;
            while (current != null)
            {
                if (current.Name == "Exception" && current.ContainingNamespace?.ToString() == "System")
                    return true;
                current = current.BaseType;
            }
            return false;
        }

        private string? GetMethodName(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                return memberAccess.Name.ToString();
            }
            return null;
        }

        /// <summary>
        /// Generate comprehensive audit report
        /// </summary>
        private LoggingAuditReport GenerateReport(int projectCount, int fileCount)
        {
            var report = new LoggingAuditReport
            {
                GeneratedAt = DateTime.Now,
                ProjectsAnalyzed = projectCount,
                FilesAnalyzed = fileCount,
                TotalViolations = _violations.Count,
                ViolationsByType = _violations.GroupBy(v => v.ViolationType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ViolationsByProject = _violations.GroupBy(v => GetProjectName(v.FilePath))
                    .ToDictionary(g => g.Key, g => g.Count()),
                Violations = _violations.OrderBy(v => v.FilePath).ThenBy(v => v.LineNumber).ToList()
            };

            return report;
        }

        private string GetProjectName(string filePath)
        {
            var parts = filePath.Split(Path.DirectorySeparatorChar);
            var projectIndex = Array.FindIndex(parts, p => p.StartsWith("TradingPlatform."));
            return projectIndex >= 0 ? parts[projectIndex] : "Unknown";
        }
    }

    public class LoggingViolation
    {
        public string FilePath { get; set; } = "";
        public int LineNumber { get; set; }
        public ViolationType ViolationType { get; set; }
        public string MethodName { get; set; } = "";
        public string CurrentCode { get; set; } = "";
        public string Description { get; set; } = "";
        public string? SuggestedFix { get; set; }
    }

    public enum ViolationType
    {
        MicrosoftLoggingUsage,
        ParameterOrder,
        ParameterType,
        StringInterpolation,
        MissingStructuredData,
        IncorrectMethod
    }

    public class LoggingAuditReport
    {
        public DateTime GeneratedAt { get; set; }
        public int ProjectsAnalyzed { get; set; }
        public int FilesAnalyzed { get; set; }
        public int TotalViolations { get; set; }
        public Dictionary<ViolationType, int> ViolationsByType { get; set; } = new();
        public Dictionary<string, int> ViolationsByProject { get; set; } = new();
        public List<LoggingViolation> Violations { get; set; } = new();

        public void SaveToFile(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Trading Platform - Comprehensive Logging Audit Report");
            sb.AppendLine($"Generated: {GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine($"- Projects Analyzed: {ProjectsAnalyzed}");
            sb.AppendLine($"- Files Analyzed: {FilesAnalyzed}");
            sb.AppendLine($"- Total Violations: {TotalViolations}");
            sb.AppendLine();
            
            sb.AppendLine("## Violations by Type");
            foreach (var (type, count) in ViolationsByType.OrderByDescending(kvp => kvp.Value))
            {
                sb.AppendLine($"- {type}: {count}");
            }
            sb.AppendLine();

            sb.AppendLine("## Violations by Project");
            foreach (var (project, count) in ViolationsByProject.OrderByDescending(kvp => kvp.Value))
            {
                sb.AppendLine($"- {project}: {count}");
            }
            sb.AppendLine();

            sb.AppendLine("## Detailed Violations");
            foreach (var violation in Violations)
            {
                sb.AppendLine($"### {violation.FilePath}:{violation.LineNumber}");
                sb.AppendLine($"**Type**: {violation.ViolationType}");
                sb.AppendLine($"**Method**: {violation.MethodName}");
                sb.AppendLine($"**Description**: {violation.Description}");
                sb.AppendLine($"**Current Code**: `{violation.CurrentCode}`");
                if (!string.IsNullOrEmpty(violation.SuggestedFix))
                {
                    sb.AppendLine($"**Suggested Fix**: `{violation.SuggestedFix}`");
                }
                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString());
        }
    }

    public class MethodSignature
    {
        public string MethodName { get; set; } = "";
        public Parameter[] Parameters { get; set; } = Array.Empty<Parameter>();
    }

    public class Parameter
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public int Position { get; set; }
        public bool IsOptional { get; set; }
    }
}