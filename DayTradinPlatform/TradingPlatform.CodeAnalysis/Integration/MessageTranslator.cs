using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.CodeAnalysis.Integration.Models;

namespace TradingPlatform.CodeAnalysis.Integration
{
    /// <summary>
    /// Translates diagnostic information into AI-friendly messages for Claude and Augment.
    /// Provides context-aware natural language feedback and actionable suggestions.
    /// </summary>
    public class MessageTranslator
    {
        private readonly Dictionary<string, RuleTranslation> _ruleTranslations;

        public MessageTranslator()
        {
            _ruleTranslations = InitializeRuleTranslations();
        }

        public ClaudeMessage TranslateForClaude(DiagnosticInfo diagnostic)
        {
            var translation = GetRuleTranslation(diagnostic.Rule);
            
            return new ClaudeMessage
            {
                Type = "code_analysis_feedback",
                Context = new CodeContext
                {
                    File = diagnostic.File,
                    Line = diagnostic.Line,
                    Rule = diagnostic.Rule,
                    Project = diagnostic.ProjectName
                },
                Feedback = translation.GetNaturalLanguageFeedback(diagnostic),
                Severity = diagnostic.Severity,
                Suggestions = translation.Suggestions,
                Examples = translation.GetCodeExamples(diagnostic),
                DocumentationLinks = translation.DocumentationLinks,
                RelatedRules = translation.RelatedRules,
                Confidence = translation.Confidence
            };
        }

        public AugmentMessage TranslateForAugment(DiagnosticInfo diagnostic)
        {
            var translation = GetRuleTranslation(diagnostic.Rule);
            
            return new AugmentMessage
            {
                EventType = "diagnostic_detected",
                Timestamp = diagnostic.Timestamp,
                Location = new FileLocation
                {
                    FilePath = diagnostic.File,
                    LineNumber = diagnostic.Line,
                    ColumnNumber = diagnostic.Column,
                    CharacterRange = new CharacterRange
                    {
                        Start = 0, // Would need actual character positions
                        End = 0
                    }
                },
                Diagnostic = new AugmentDiagnostic
                {
                    RuleId = diagnostic.Rule,
                    Category = diagnostic.Category,
                    Title = translation.Title,
                    Description = diagnostic.Message,
                    Severity = diagnostic.Severity.ToLowerInvariant()
                },
                ActionRequired = GetActionRequired(diagnostic.Severity),
                Priority = MapSeverityToPriority(diagnostic.Severity),
                Metadata = translation.GetMetadata(diagnostic),
                FixSuggestions = translation.GetFixSuggestions(diagnostic),
                ImpactAnalysis = translation.GetImpactAnalysis()
            };
        }

        private RuleTranslation GetRuleTranslation(string ruleId)
        {
            return _ruleTranslations.TryGetValue(ruleId, out var translation) 
                ? translation 
                : new RuleTranslation { Title = "Code Quality Issue" };
        }

        private string GetActionRequired(string severity)
        {
            return severity switch
            {
                "Error" => "immediate_fix_required",
                "Warning" => "review_and_fix",
                "Info" => "consider_improvement",
                _ => "review"
            };
        }

        private int MapSeverityToPriority(string severity)
        {
            return severity switch
            {
                "Error" => 1,
                "Warning" => 2,
                "Info" => 3,
                _ => 4
            };
        }

        private Dictionary<string, RuleTranslation> InitializeRuleTranslations()
        {
            return new Dictionary<string, RuleTranslation>
            {
                ["TP0001"] = new RuleTranslation
                {
                    Title = "Use decimal for monetary values",
                    NaturalLanguageFormat = "I noticed you're using {0} for a monetary value. In financial applications, this can lead to precision errors. Switch to decimal type to ensure accurate calculations.",
                    Suggestions = new List<string>
                    {
                        "Change the type from double/float to decimal",
                        "Use decimal literals (e.g., 10.5m instead of 10.5)",
                        "Update any calculations to use decimal-safe operations",
                        "Consider using Money pattern for complex currency handling"
                    },
                    DocumentationLinks = new List<string>
                    {
                        "https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/floating-point-numeric-types",
                        "https://docs.tradingplatform.com/financial-precision"
                    },
                    RelatedRules = new List<string> { "TP0002", "TP0003" },
                    Confidence = 0.95
                },

                ["TP0101"] = new RuleTranslation
                {
                    Title = "Extend canonical base class",
                    NaturalLanguageFormat = "The service class '{0}' should extend {1} to follow our canonical patterns. This ensures consistent lifecycle management, logging, and error handling across all services.",
                    Suggestions = new List<string>
                    {
                        "Inherit from the suggested canonical base class",
                        "Implement required lifecycle methods (OnInitializeAsync, OnStartAsync, OnStopAsync)",
                        "Remove direct logger instantiation and use inherited logging methods",
                        "Update constructor to call base class constructor with appropriate parameters"
                    },
                    DocumentationLinks = new List<string>
                    {
                        "https://docs.tradingplatform.com/canonical-patterns",
                        "https://docs.tradingplatform.com/service-lifecycle"
                    },
                    RelatedRules = new List<string> { "TP0103", "TP0104" },
                    Confidence = 0.90
                },

                ["TP0102"] = new RuleTranslation
                {
                    Title = "Use TradingResult for operation returns",
                    NaturalLanguageFormat = "Method '{0}' returns {1} directly. Wrap the return value in TradingResult<T> to provide consistent error handling and enable proper failure propagation.",
                    Suggestions = new List<string>
                    {
                        "Change return type to TradingResult<T> or Task<TradingResult<T>>",
                        "Use TradingResult.Success() for successful operations",
                        "Use TradingResult.Failure() for error cases instead of throwing exceptions",
                        "Update callers to handle TradingResult pattern"
                    },
                    DocumentationLinks = new List<string>
                    {
                        "https://docs.tradingplatform.com/result-pattern",
                        "https://docs.tradingplatform.com/error-handling"
                    },
                    RelatedRules = new List<string> { "TP0501" },
                    Confidence = 0.88
                },

                ["TP0501"] = new RuleTranslation
                {
                    Title = "No silent failures allowed",
                    NaturalLanguageFormat = "Detected {0}. All errors must be properly handled and logged. Silent failures can lead to data corruption and difficult-to-diagnose issues in production.",
                    Suggestions = new List<string>
                    {
                        "Add proper error logging before returning or throwing",
                        "Use TradingResult.Failure() to propagate errors",
                        "Include contextual information in error messages",
                        "Consider adding telemetry for error tracking"
                    },
                    DocumentationLinks = new List<string>
                    {
                        "https://docs.tradingplatform.com/error-handling",
                        "https://docs.tradingplatform.com/logging-guidelines"
                    },
                    RelatedRules = new List<string> { "TP0102", "TP0502" },
                    Confidence = 0.92
                }
            };
        }

        private class RuleTranslation
        {
            public string Title { get; set; }
            public string NaturalLanguageFormat { get; set; }
            public List<string> Suggestions { get; set; } = new List<string>();
            public List<string> DocumentationLinks { get; set; } = new List<string>();
            public List<string> RelatedRules { get; set; } = new List<string>();
            public double Confidence { get; set; } = 0.85;

            public string GetNaturalLanguageFeedback(DiagnosticInfo diagnostic)
            {
                if (string.IsNullOrEmpty(NaturalLanguageFormat))
                    return diagnostic.Message;

                // Extract parameters from diagnostic message if needed
                var parameters = ExtractParameters(diagnostic.Message);
                try
                {
                    return string.Format(NaturalLanguageFormat, parameters);
                }
                catch
                {
                    return diagnostic.Message;
                }
            }

            public CodeExamples GetCodeExamples(DiagnosticInfo diagnostic)
            {
                // This would be enhanced with actual code snippets from the diagnostic
                return new CodeExamples
                {
                    Current = "// Current implementation",
                    Suggested = "// Suggested improvement"
                };
            }

            public Dictionary<string, object> GetMetadata(DiagnosticInfo diagnostic)
            {
                return new Dictionary<string, object>
                {
                    ["rule_confidence"] = Confidence,
                    ["auto_fixable"] = Suggestions.Any(),
                    ["breaking_change"] = false,
                    ["category"] = diagnostic.Category
                };
            }

            public List<FixSuggestion> GetFixSuggestions(DiagnosticInfo diagnostic)
            {
                // Convert text suggestions to structured fix suggestions
                return Suggestions.Select(s => new FixSuggestion
                {
                    Type = "text_replacement",
                    From = "current_code",
                    To = "suggested_code",
                    RequiresDiRegistration = s.Contains("dependency injection")
                }).ToList();
            }

            public ImpactAnalysis GetImpactAnalysis()
            {
                return new ImpactAnalysis
                {
                    FilesAffected = 1,
                    MethodsAffected = 1,
                    EstimatedFixTimeMinutes = Suggestions.Count * 2,
                    Complexity = Confidence > 0.9 ? "low" : "medium"
                };
            }

            private string[] ExtractParameters(string message)
            {
                // Simple parameter extraction - would be enhanced for production
                return message.Split('\'')
                    .Where((s, i) => i % 2 == 1)
                    .ToArray();
            }
        }
    }
}