// File: TradingPlatform.Auditing.Analyzers\SecurityAnalyzer.cs

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TradingPlatform.Auditing
{
    /// <summary>
    /// Analyzer that checks for security issues
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SecurityAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TP006";
        private const string Category = "Security";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Potential security vulnerability",
            "{0}",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Detects potential security vulnerabilities in code.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeLiteralExpression, SyntaxKind.StringLiteralExpression);
            context.RegisterSyntaxNodeAction(AnalyzeInterpolatedString, SyntaxKind.InterpolatedStringExpression);
        }

        private void AnalyzeLiteralExpression(SyntaxNodeAnalysisContext context)
        {
            var literal = (LiteralExpressionSyntax)context.Node;
            var value = literal.Token.ValueText;
            
            // Check for hardcoded secrets
            if (ContainsSecretPattern(value))
            {
                var parent = literal.Parent;
                if (parent is AssignmentExpressionSyntax assignment)
                {
                    var left = assignment.Left.ToString();
                    if (IsSecretVariable(left) && !IsConfigAccess(assignment))
                    {
                        var diagnostic = Diagnostic.Create(
                            Rule, 
                            literal.GetLocation(), 
                            "Hardcoded secret detected. Use configuration or environment variables instead.");
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private void AnalyzeInterpolatedString(SyntaxNodeAnalysisContext context)
        {
            var interpolated = (InterpolatedStringExpressionSyntax)context.Node;
            
            // Check for SQL injection patterns
            var parent = interpolated.Parent;
            if (parent is ArgumentSyntax argument)
            {
                var invocation = argument.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault();
                if (invocation != null)
                {
                    var methodName = invocation.Expression.ToString();
                    if (methodName.Contains("ExecuteSql") || 
                        methodName.Contains("SqlCommand") ||
                        methodName.Contains("Query"))
                    {
                        var diagnostic = Diagnostic.Create(
                            Rule, 
                            interpolated.GetLocation(), 
                            "Potential SQL injection vulnerability. Use parameterized queries instead.");
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private bool ContainsSecretPattern(string value)
        {
            // Check if the string looks like a secret (at least 10 chars, mix of letters/numbers)
            if (value.Length < 10) return false;
            
            bool hasLetter = false;
            bool hasDigit = false;
            
            foreach (char c in value)
            {
                if (char.IsLetter(c)) hasLetter = true;
                if (char.IsDigit(c)) hasDigit = true;
                if (hasLetter && hasDigit) return true;
            }
            
            return false;
        }

        private bool IsSecretVariable(string variableName)
        {
            var lowerName = variableName.ToLowerInvariant();
            return lowerName.Contains("password") ||
                   lowerName.Contains("secret") ||
                   lowerName.Contains("apikey") ||
                   lowerName.Contains("token") ||
                   lowerName.Contains("connectionstring");
        }

        private bool IsConfigAccess(AssignmentExpressionSyntax assignment)
        {
            var right = assignment.Right.ToString();
            return right.Contains("Configuration[") ||
                   right.Contains("GetEnvironmentVariable") ||
                   right.Contains("ConfigurationManager") ||
                   right.Contains("appSettings");
        }
    }
}