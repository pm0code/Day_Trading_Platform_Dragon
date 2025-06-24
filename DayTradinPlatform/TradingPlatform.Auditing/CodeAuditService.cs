// File: TradingPlatform.Auditing\CodeAuditService.cs

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Foundation;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Auditing
{
    /// <summary>
    /// Canonical implementation of code audit service that validates adherence
    /// to the mandatory Standard Development Workflow.
    /// </summary>
    public class CodeAuditService : CanonicalServiceBase
    {
        private readonly List<IAuditRule> _auditRules = new();
        private readonly Dictionary<string, AuditResult> _auditResults = new();
        private long _filesAudited;
        private long _violationsFound;
        private long _criticalViolations;

        public CodeAuditService(IServiceProvider serviceProvider)
            : base(serviceProvider, serviceProvider.GetRequiredService<ITradingLogger>(), "CodeAuditService")
        {
            InitializeAuditRules();
        }

        private void InitializeAuditRules()
        {
            _auditRules.Add(new CanonicalPatternRule());
            _auditRules.Add(new LoggingPatternRule());
            _auditRules.Add(new ErrorHandlingRule());
            _auditRules.Add(new DocumentationRule());
            _auditRules.Add(new TestCoverageRule());
            _auditRules.Add(new NamingConventionRule());
            _auditRules.Add(new DependencyRule());
            _auditRules.Add(new SecurityRule());
        }

        public async Task<AuditReport> AuditProjectAsync(string projectPath)
        {
            _logger.LogInformation("Starting comprehensive code audit", new { ProjectPath = projectPath });

            var report = new AuditReport
            {
                AuditStartTime = DateTime.UtcNow,
                ProjectPath = projectPath
            };

            try
            {
                // Get all C# files
                var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("/obj/") && !f.Contains("/bin/"))
                    .ToList();

                _logger.LogInformation($"Found {csFiles.Count} C# files to audit");

                // Audit each file
                foreach (var file in csFiles)
                {
                    var fileResult = await AuditFileAsync(file);
                    report.FileResults.Add(fileResult);
                    
                    if (fileResult.Violations.Any())
                    {
                        _violationsFound += fileResult.Violations.Count;
                        _criticalViolations += fileResult.Violations.Count(v => v.Severity == ViolationSeverity.Critical);
                    }
                }

                // Calculate summary
                report.TotalFiles = csFiles.Count;
                report.FilesWithViolations = report.FileResults.Count(r => r.Violations.Any());
                report.TotalViolations = (int)_violationsFound;
                report.CriticalViolations = (int)_criticalViolations;
                report.ComplianceScore = CalculateComplianceScore(report);
                report.AuditEndTime = DateTime.UtcNow;

                _logger.LogInformation("Code audit completed", new
                {
                    TotalFiles = report.TotalFiles,
                    Violations = report.TotalViolations,
                    ComplianceScore = report.ComplianceScore
                });

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError("Code audit failed", ex);
                throw;
            }
        }

        private async Task<FileAuditResult> AuditFileAsync(string filePath)
        {
            _filesAudited++;
            var result = new FileAuditResult
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath)
            };

            try
            {
                var content = await File.ReadAllTextAsync(filePath);
                
                // Run all audit rules
                foreach (var rule in _auditRules)
                {
                    var violations = await rule.CheckAsync(filePath, content);
                    result.Violations.AddRange(violations);
                }

                // Check for specific patterns
                result.HasCanonicalPattern = content.Contains(": CanonicalServiceBase") || 
                                            content.Contains(": CanonicalProvider") ||
                                            content.Contains(": CanonicalEngine") ||
                                            content.Contains(": CanonicalRiskEvaluator") ||
                                            content.Contains(": CanonicalCriteriaEvaluator");
                
                result.HasProperLogging = CheckLoggingPattern(content);
                result.HasErrorHandling = CheckErrorHandling(content);
                result.HasDocumentation = CheckDocumentation(content);

                RecordMetric($"Files.{GetFileCategory(filePath)}", 1);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error auditing file {filePath}", ex);
                result.Violations.Add(new AuditViolation
                {
                    Rule = "FileAccess",
                    Severity = ViolationSeverity.Critical,
                    Message = $"Could not audit file: {ex.Message}",
                    Line = 0
                });
            }

            return result;
        }

        private bool CheckLoggingPattern(string content)
        {
            // Check for method entry/exit logging
            var methodPattern = @"(public|private|protected|internal)\s+.*?\s+\w+\s*\([^)]*\)\s*{";
            var logEntryPattern = @"_logger\.Log(Information|Debug|Trace).*Entry";
            var logExitPattern = @"_logger\.Log(Information|Debug|Trace).*Exit";

            var methods = Regex.Matches(content, methodPattern);
            if (methods.Count == 0) return true; // No methods to check

            var entries = Regex.Matches(content, logEntryPattern).Count;
            var exits = Regex.Matches(content, logExitPattern).Count;

            // Should have roughly equal entry/exit logs for methods
            return entries > 0 && exits > 0;
        }

        private bool CheckErrorHandling(string content)
        {
            // Check for try-catch blocks and proper error logging
            var tryPattern = @"\btry\s*{";
            var catchPattern = @"\bcatch\s*\(";
            var logErrorPattern = @"_logger\.LogError";

            var tries = Regex.Matches(content, tryPattern).Count;
            var catches = Regex.Matches(content, catchPattern).Count;
            var errorLogs = Regex.Matches(content, logErrorPattern).Count;

            return catches >= tries && errorLogs > 0;
        }

        private bool CheckDocumentation(string content)
        {
            // Check for XML documentation
            var xmlDocPattern = @"///\s*<summary>";
            var classPattern = @"(public|internal)\s+(class|interface|enum)\s+\w+";
            var publicMethodPattern = @"public\s+.*?\s+\w+\s*\([^)]*\)";

            var xmlDocs = Regex.Matches(content, xmlDocPattern).Count;
            var classes = Regex.Matches(content, classPattern).Count;
            var publicMethods = Regex.Matches(content, publicMethodPattern).Count;

            // Should have documentation for classes and public methods
            var expectedDocs = classes + publicMethods;
            return xmlDocs >= expectedDocs * 0.8; // 80% documented
        }

        private decimal CalculateComplianceScore(AuditReport report)
        {
            if (report.TotalFiles == 0) return 100m;

            // Base score from files without violations
            decimal baseScore = (decimal)(report.TotalFiles - report.FilesWithViolations) / report.TotalFiles * 100;

            // Penalty for violations
            decimal violationPenalty = report.TotalViolations * 0.5m;
            decimal criticalPenalty = report.CriticalViolations * 2m;

            decimal finalScore = Math.Max(0, baseScore - violationPenalty - criticalPenalty);
            return Math.Round(finalScore, 2);
        }

        private string GetFileCategory(string filePath)
        {
            if (filePath.Contains("/Tests/") || filePath.Contains(".Tests.")) return "Tests";
            if (filePath.Contains("/Canonical/")) return "Canonical";
            if (filePath.Contains("/Services/")) return "Services";
            if (filePath.Contains("/Models/")) return "Models";
            if (filePath.Contains("/Interfaces/")) return "Interfaces";
            return "Other";
        }

        protected override Dictionary<string, object> GetServiceMetrics()
        {
            var baseMetrics = base.GetServiceMetrics();
            
            baseMetrics["FilesAudited"] = _filesAudited;
            baseMetrics["ViolationsFound"] = _violationsFound;
            baseMetrics["CriticalViolations"] = _criticalViolations;
            baseMetrics["AuditRules"] = _auditRules.Count;

            return baseMetrics;
        }
    }

    #region Audit Rules

    public interface IAuditRule
    {
        string RuleName { get; }
        Task<List<AuditViolation>> CheckAsync(string filePath, string content);
    }

    public class CanonicalPatternRule : IAuditRule
    {
        public string RuleName => "CanonicalPattern";

        public Task<List<AuditViolation>> CheckAsync(string filePath, string content)
        {
            var violations = new List<AuditViolation>();

            // Skip test files and interfaces
            if (filePath.Contains("/Tests/") || filePath.Contains("/Interfaces/")) 
                return Task.FromResult(violations);

            // Check if service/provider/engine follows canonical pattern
            if ((content.Contains("Service") || content.Contains("Provider") || content.Contains("Engine") || 
                 content.Contains("Monitor") || content.Contains("Calculator")) &&
                content.Contains("class") &&
                !content.Contains("abstract") &&
                !content.Contains(": Canonical"))
            {
                // Check if it's a non-canonical implementation
                if (!content.Contains("Canonical") && !content.Contains("Mock") && !content.Contains("Test"))
                {
                    violations.Add(new AuditViolation
                    {
                        Rule = RuleName,
                        Severity = ViolationSeverity.Major,
                        Message = "Service/Provider/Engine should inherit from canonical base class",
                        Line = 0,
                        FilePath = filePath
                    });
                }
            }

            return Task.FromResult(violations);
        }
    }

    public class LoggingPatternRule : IAuditRule
    {
        public string RuleName => "LoggingPattern";

        public Task<List<AuditViolation>> CheckAsync(string filePath, string content)
        {
            var violations = new List<AuditViolation>();

            // Skip interfaces and models
            if (filePath.Contains("/Interfaces/") || filePath.Contains("/Models/")) 
                return Task.FromResult(violations);

            // Check for logger field
            if (!content.Contains("ITradingLogger") && !content.Contains("ILogger"))
            {
                violations.Add(new AuditViolation
                {
                    Rule = RuleName,
                    Severity = ViolationSeverity.Minor,
                    Message = "Class should have a logger field",
                    Line = 0,
                    FilePath = filePath
                });
            }

            // Check for method logging in canonical classes
            if (content.Contains(": Canonical"))
            {
                var methodPattern = @"(public|protected)\s+.*?\s+(\w+)\s*\([^)]*\)\s*{";
                var matches = Regex.Matches(content, methodPattern);
                
                foreach (Match match in matches)
                {
                    var methodName = match.Groups[2].Value;
                    if (!methodName.StartsWith("get_") && !methodName.StartsWith("set_"))
                    {
                        var methodBody = GetMethodBody(content, match.Index);
                        if (!methodBody.Contains($"Log") && !methodBody.Contains("Entry") && !methodBody.Contains("Exit"))
                        {
                            violations.Add(new AuditViolation
                            {
                                Rule = RuleName,
                                Severity = ViolationSeverity.Major,
                                Message = $"Method '{methodName}' should have entry/exit logging",
                                Line = GetLineNumber(content, match.Index),
                                FilePath = filePath
                            });
                        }
                    }
                }
            }

            return Task.FromResult(violations);
        }

        private string GetMethodBody(string content, int startIndex)
        {
            var braceCount = 0;
            var inMethod = false;
            var methodBody = new StringBuilder();

            for (int i = startIndex; i < content.Length; i++)
            {
                if (content[i] == '{')
                {
                    braceCount++;
                    inMethod = true;
                }
                else if (content[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0 && inMethod)
                    {
                        return methodBody.ToString();
                    }
                }

                if (inMethod)
                {
                    methodBody.Append(content[i]);
                }
            }

            return methodBody.ToString();
        }

        private int GetLineNumber(string content, int index)
        {
            return content.Substring(0, index).Count(c => c == '\n') + 1;
        }
    }

    public class ErrorHandlingRule : IAuditRule
    {
        public string RuleName => "ErrorHandling";

        public Task<List<AuditViolation>> CheckAsync(string filePath, string content)
        {
            var violations = new List<AuditViolation>();

            // Check for catch blocks without logging
            var catchPattern = @"catch\s*\([^)]*\)\s*{([^}]*)";
            var matches = Regex.Matches(content, catchPattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var catchBody = match.Groups[1].Value;
                if (!catchBody.Contains("Log") && !catchBody.Contains("throw"))
                {
                    violations.Add(new AuditViolation
                    {
                        Rule = RuleName,
                        Severity = ViolationSeverity.Major,
                        Message = "Catch block should log the error or rethrow",
                        Line = GetLineNumber(content, match.Index),
                        FilePath = filePath
                    });
                }
            }

            return Task.FromResult(violations);
        }

        private int GetLineNumber(string content, int index)
        {
            return content.Substring(0, index).Count(c => c == '\n') + 1;
        }
    }

    public class DocumentationRule : IAuditRule
    {
        public string RuleName => "Documentation";

        public Task<List<AuditViolation>> CheckAsync(string filePath, string content)
        {
            var violations = new List<AuditViolation>();

            // Check for missing XML documentation on public members
            var publicPattern = @"public\s+(class|interface|enum|struct|delegate|event|property|method|field)";
            var xmlDocPattern = @"///\s*<summary>";

            var publicMembers = Regex.Matches(content, publicPattern);
            
            foreach (Match match in publicMembers)
            {
                // Check if there's XML documentation before this member
                var lineStart = content.LastIndexOf('\n', match.Index) + 1;
                var previousLine = content.LastIndexOf('\n', lineStart - 2) + 1;
                var precedingText = content.Substring(previousLine, match.Index - previousLine);

                if (!precedingText.Contains("///"))
                {
                    violations.Add(new AuditViolation
                    {
                        Rule = RuleName,
                        Severity = ViolationSeverity.Minor,
                        Message = "Public member should have XML documentation",
                        Line = GetLineNumber(content, match.Index),
                        FilePath = filePath
                    });
                }
            }

            return Task.FromResult(violations);
        }

        private int GetLineNumber(string content, int index)
        {
            return content.Substring(0, index).Count(c => c == '\n') + 1;
        }
    }

    public class TestCoverageRule : IAuditRule
    {
        public string RuleName => "TestCoverage";

        public Task<List<AuditViolation>> CheckAsync(string filePath, string content)
        {
            var violations = new List<AuditViolation>();

            // This is a placeholder - in reality, would analyze test coverage reports
            if (filePath.Contains("/Services/") && !filePath.Contains("Test"))
            {
                var className = GetClassName(content);
                var testFile = filePath.Replace("/Services/", "/Tests/").Replace(".cs", "Tests.cs");
                
                if (!File.Exists(testFile))
                {
                    violations.Add(new AuditViolation
                    {
                        Rule = RuleName,
                        Severity = ViolationSeverity.Major,
                        Message = $"No test file found for {className}",
                        Line = 0,
                        FilePath = filePath
                    });
                }
            }

            return Task.FromResult(violations);
        }

        private string GetClassName(string content)
        {
            var match = Regex.Match(content, @"class\s+(\w+)");
            return match.Success ? match.Groups[1].Value : "Unknown";
        }
    }

    public class NamingConventionRule : IAuditRule
    {
        public string RuleName => "NamingConvention";

        public Task<List<AuditViolation>> CheckAsync(string filePath, string content)
        {
            var violations = new List<AuditViolation>();

            // Check interface naming
            var interfacePattern = @"interface\s+([A-Za-z_]\w*)";
            var interfaceMatches = Regex.Matches(content, interfacePattern);
            
            foreach (Match match in interfaceMatches)
            {
                var name = match.Groups[1].Value;
                if (!name.StartsWith("I"))
                {
                    violations.Add(new AuditViolation
                    {
                        Rule = RuleName,
                        Severity = ViolationSeverity.Minor,
                        Message = $"Interface '{name}' should start with 'I'",
                        Line = GetLineNumber(content, match.Index),
                        FilePath = filePath
                    });
                }
            }

            // Check private field naming
            var privateFieldPattern = @"private\s+(?:readonly\s+)?[\w<>,\s]+\s+([a-zA-Z_]\w*)\s*[;=]";
            var fieldMatches = Regex.Matches(content, privateFieldPattern);
            
            foreach (Match match in fieldMatches)
            {
                var name = match.Groups[1].Value;
                if (!name.StartsWith("_") && !name.StartsWith("s_"))
                {
                    violations.Add(new AuditViolation
                    {
                        Rule = RuleName,
                        Severity = ViolationSeverity.Minor,
                        Message = $"Private field '{name}' should start with underscore",
                        Line = GetLineNumber(content, match.Index),
                        FilePath = filePath
                    });
                }
            }

            return Task.FromResult(violations);
        }

        private int GetLineNumber(string content, int index)
        {
            return content.Substring(0, index).Count(c => c == '\n') + 1;
        }
    }

    public class DependencyRule : IAuditRule
    {
        public string RuleName => "Dependency";

        public Task<List<AuditViolation>> CheckAsync(string filePath, string content)
        {
            var violations = new List<AuditViolation>();

            // Check for hardcoded dependencies (new keyword for services)
            var newServicePattern = @"new\s+(.*?Service|.*?Provider|.*?Manager|.*?Repository)\s*\(";
            var matches = Regex.Matches(content, newServicePattern);

            foreach (Match match in matches)
            {
                var serviceName = match.Groups[1].Value;
                if (!content.Contains("class Test") && !filePath.Contains("/Tests/"))
                {
                    violations.Add(new AuditViolation
                    {
                        Rule = RuleName,
                        Severity = ViolationSeverity.Major,
                        Message = $"Service '{serviceName}' should be injected, not instantiated with 'new'",
                        Line = GetLineNumber(content, match.Index),
                        FilePath = filePath
                    });
                }
            }

            return Task.FromResult(violations);
        }

        private int GetLineNumber(string content, int index)
        {
            return content.Substring(0, index).Count(c => c == '\n') + 1;
        }
    }

    public class SecurityRule : IAuditRule
    {
        public string RuleName => "Security";

        public Task<List<AuditViolation>> CheckAsync(string filePath, string content)
        {
            var violations = new List<AuditViolation>();

            // Check for hardcoded secrets
            var secretPatterns = new[]
            {
                @"(password|pwd|secret|key|token)\s*=\s*""[^""]+""",
                @"(ApiKey|ClientSecret|ConnectionString)\s*=\s*""[^""]+"""
            };

            foreach (var pattern in secretPatterns)
            {
                var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (!match.Value.Contains("ConfigurationManager") && 
                        !match.Value.Contains("GetEnvironmentVariable") &&
                        !match.Value.Contains("Configuration["))
                    {
                        violations.Add(new AuditViolation
                        {
                            Rule = RuleName,
                            Severity = ViolationSeverity.Critical,
                            Message = "Potential hardcoded secret detected",
                            Line = GetLineNumber(content, match.Index),
                            FilePath = filePath
                        });
                    }
                }
            }

            return Task.FromResult(violations);
        }

        private int GetLineNumber(string content, int index)
        {
            return content.Substring(0, index).Count(c => c == '\n') + 1;
        }
    }

    #endregion

    #region Audit Models

    public class AuditReport
    {
        public DateTime AuditStartTime { get; set; }
        public DateTime AuditEndTime { get; set; }
        public string ProjectPath { get; set; } = "";
        public List<FileAuditResult> FileResults { get; set; } = new();
        public int TotalFiles { get; set; }
        public int FilesWithViolations { get; set; }
        public int TotalViolations { get; set; }
        public int CriticalViolations { get; set; }
        public decimal ComplianceScore { get; set; }
    }

    public class FileAuditResult
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public List<AuditViolation> Violations { get; set; } = new();
        public bool HasCanonicalPattern { get; set; }
        public bool HasProperLogging { get; set; }
        public bool HasErrorHandling { get; set; }
        public bool HasDocumentation { get; set; }
    }

    public class AuditViolation
    {
        public string Rule { get; set; } = "";
        public ViolationSeverity Severity { get; set; }
        public string Message { get; set; } = "";
        public int Line { get; set; }
        public string FilePath { get; set; } = "";
    }

    public enum ViolationSeverity
    {
        Minor,
        Major,
        Critical
    }

    #endregion
}