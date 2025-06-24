// File: TradingPlatform.Auditing.Analyzers\MethodComplexityAnalyzer.cs

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TradingPlatform.Auditing
{
    /// <summary>
    /// Analyzer that checks for method complexity
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MethodComplexityAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TP004";
        private const string Category = "Complexity";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Method has high cyclomatic complexity",
            "Method '{0}' has cyclomatic complexity of {1}, which exceeds the threshold of {2}",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Methods with high cyclomatic complexity are difficult to understand and maintain.");

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
            var complexity = CalculateCyclomaticComplexity(method);
            
            const int threshold = 10;
            
            if (complexity > threshold)
            {
                var diagnostic = Diagnostic.Create(
                    Rule, 
                    method.Identifier.GetLocation(), 
                    method.Identifier.Text, 
                    complexity, 
                    threshold);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private int CalculateCyclomaticComplexity(MethodDeclarationSyntax method)
        {
            var complexity = 1; // Base complexity
            
            if (method.Body != null)
            {
                var walker = new ComplexityWalker();
                walker.Visit(method.Body);
                complexity += walker.DecisionPoints;
            }
            else if (method.ExpressionBody != null)
            {
                // Expression-bodied members have lower complexity
                var walker = new ComplexityWalker();
                walker.Visit(method.ExpressionBody);
                complexity += walker.DecisionPoints;
            }
            
            return complexity;
        }

        private class ComplexityWalker : CSharpSyntaxWalker
        {
            public int DecisionPoints { get; private set; }

            public override void VisitIfStatement(IfStatementSyntax node)
            {
                DecisionPoints++;
                base.VisitIfStatement(node);
            }

            public override void VisitWhileStatement(WhileStatementSyntax node)
            {
                DecisionPoints++;
                base.VisitWhileStatement(node);
            }

            public override void VisitForStatement(ForStatementSyntax node)
            {
                DecisionPoints++;
                base.VisitForStatement(node);
            }

            public override void VisitForEachStatement(ForEachStatementSyntax node)
            {
                DecisionPoints++;
                base.VisitForEachStatement(node);
            }

            public override void VisitSwitchStatement(SwitchStatementSyntax node)
            {
                DecisionPoints += node.Sections.Count;
                base.VisitSwitchStatement(node);
            }

            public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
            {
                DecisionPoints++;
                base.VisitConditionalExpression(node);
            }

            public override void VisitCatchClause(CatchClauseSyntax node)
            {
                DecisionPoints++;
                base.VisitCatchClause(node);
            }

            public override void VisitBinaryExpression(BinaryExpressionSyntax node)
            {
                if (node.IsKind(SyntaxKind.LogicalAndExpression) || 
                    node.IsKind(SyntaxKind.LogicalOrExpression))
                {
                    DecisionPoints++;
                }
                base.VisitBinaryExpression(node);
            }
        }
    }
}