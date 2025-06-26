using System;
using System.Collections.Generic;

namespace TradingPlatform.CodeAnalysis.Integration.Models
{
    /// <summary>
    /// Core diagnostic information model used across the feedback system.
    /// </summary>
    public class DiagnosticInfo
    {
        public string File { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string Rule { get; set; }
        public string Message { get; set; }
        public string Severity { get; set; }
        public string Category { get; set; }
        public string ProjectName { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Message format for Claude API integration.
    /// </summary>
    public class ClaudeMessage
    {
        public string Type { get; set; }
        public CodeContext Context { get; set; }
        public string Feedback { get; set; }
        public string Severity { get; set; }
        public List<string> Suggestions { get; set; }
        public CodeExamples Examples { get; set; }
        public List<string> DocumentationLinks { get; set; }
        public List<string> RelatedRules { get; set; }
        public double Confidence { get; set; }
    }

    public class CodeContext
    {
        public string File { get; set; }
        public int Line { get; set; }
        public string Rule { get; set; }
        public string Project { get; set; }
    }

    public class CodeExamples
    {
        public string Current { get; set; }
        public string Suggested { get; set; }
    }

    /// <summary>
    /// Message format for Augment Code API integration.
    /// </summary>
    public class AugmentMessage
    {
        public string EventType { get; set; }
        public DateTime Timestamp { get; set; }
        public string Source { get; set; } = "roslyn_analyzer";
        public string AnalyzerVersion { get; set; } = "1.0.0";
        public FileLocation Location { get; set; }
        public AugmentDiagnostic Diagnostic { get; set; }
        public string ActionRequired { get; set; }
        public int Priority { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
        public List<FixSuggestion> FixSuggestions { get; set; }
        public ImpactAnalysis ImpactAnalysis { get; set; }
    }

    public class FileLocation
    {
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; }
        public CharacterRange CharacterRange { get; set; }
    }

    public class CharacterRange
    {
        public int Start { get; set; }
        public int End { get; set; }
    }

    public class AugmentDiagnostic
    {
        public string RuleId { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Severity { get; set; }
    }

    public class FixSuggestion
    {
        public string Type { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public bool RequiresDiRegistration { get; set; }
    }

    public class ImpactAnalysis
    {
        public int FilesAffected { get; set; }
        public int MethodsAffected { get; set; }
        public int EstimatedFixTimeMinutes { get; set; }
        public string Complexity { get; set; }
    }
}