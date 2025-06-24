// File: TradingPlatform.Auditing.Analyzers\LoggingPatternAnalyzer.cs

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TradingPlatform.Auditing
{
    /// <summary>
    /// Analyzer that enforces proper logging patterns in methods
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class LoggingPatternAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TP002";
        private const string Category = "Logging";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Method should have entry/exit logging",
            "Method '{0}' should have entry/exit logging",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Public methods should have entry and exit logging for proper traceability.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var method = (MethodDeclarationSyntax)context.Node;
            
            // Skip property accessors and private methods
            if (method.Identifier.Text.StartsWith("get_") || 
                method.Identifier.Text.StartsWith("set_") ||
                !method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
            {
                return;
            }

            // Skip constructors and test methods
            var className = method.Parent as ClassDeclarationSyntax;
            if (className?.Identifier.Text.Contains("Test") == true)
            {
                return;
            }

            var body = method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? "";
            
            // Check for entry/exit logging
            bool hasEntryLog = body.Contains("LogEntry") || 
                               body.Contains("LogInformation") && body.Contains("Entry");
            bool hasExitLog = body.Contains("LogExit") || 
                              body.Contains("finally") && body.Contains("Log");

            if (!hasEntryLog || !hasExitLog)
            {
                var diagnostic = Diagnostic.Create(Rule, method.Identifier.GetLocation(), method.Identifier.Text);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}