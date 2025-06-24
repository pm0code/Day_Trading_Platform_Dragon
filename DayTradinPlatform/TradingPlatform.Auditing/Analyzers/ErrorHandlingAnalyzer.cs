// File: TradingPlatform.Auditing.Analyzers\ErrorHandlingAnalyzer.cs

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TradingPlatform.Auditing
{
    /// <summary>
    /// Analyzer that enforces proper error handling patterns
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ErrorHandlingAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TP003";
        private const string Category = "Error Handling";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Catch block should log or rethrow",
            "Catch block should log the error or rethrow the exception",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "All catch blocks must either log the error or rethrow the exception to ensure errors are not silently swallowed.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeCatchClause, SyntaxKind.CatchClause);
        }

        private void AnalyzeCatchClause(SyntaxNodeAnalysisContext context)
        {
            var catchClause = (CatchClauseSyntax)context.Node;
            var block = catchClause.Block;
            
            if (block == null || block.Statements.Count == 0)
            {
                var diagnostic = Diagnostic.Create(Rule, catchClause.CatchKeyword.GetLocation());
                context.ReportDiagnostic(diagnostic);
                return;
            }

            var blockText = block.ToString();
            
            // Check if the catch block logs or rethrows
            bool hasLogging = blockText.Contains("Log") || blockText.Contains("logger");
            bool hasRethrow = blockText.Contains("throw");
            
            if (!hasLogging && !hasRethrow)
            {
                var diagnostic = Diagnostic.Create(Rule, catchClause.CatchKeyword.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}