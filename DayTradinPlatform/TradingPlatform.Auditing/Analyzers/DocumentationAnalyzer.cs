// File: TradingPlatform.Auditing.Analyzers\DocumentationAnalyzer.cs

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TradingPlatform.Auditing
{
    /// <summary>
    /// Analyzer that checks for proper XML documentation
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DocumentationAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TP008";
        private const string Category = "Documentation";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Public member should have XML documentation",
            "Public {0} '{1}' should have XML documentation",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Public members should have XML documentation for API clarity.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeInterface, SyntaxKind.InterfaceDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
        }

        private void AnalyzeClass(SyntaxNodeAnalysisContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            AnalyzeMember(context, classDeclaration, classDeclaration.Identifier, "class");
        }

        private void AnalyzeInterface(SyntaxNodeAnalysisContext context)
        {
            var interfaceDeclaration = (InterfaceDeclarationSyntax)context.Node;
            AnalyzeMember(context, interfaceDeclaration, interfaceDeclaration.Identifier, "interface");
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var method = (MethodDeclarationSyntax)context.Node;
            
            // Skip property accessors
            if (method.Identifier.Text.StartsWith("get_") || method.Identifier.Text.StartsWith("set_"))
            {
                return;
            }
            
            AnalyzeMember(context, method, method.Identifier, "method");
        }

        private void AnalyzeProperty(SyntaxNodeAnalysisContext context)
        {
            var property = (PropertyDeclarationSyntax)context.Node;
            AnalyzeMember(context, property, property.Identifier, "property");
        }

        private void AnalyzeMember(SyntaxNodeAnalysisContext context, MemberDeclarationSyntax member, SyntaxToken identifier, string memberType)
        {
            // Check if it's public
            if (!IsPublic(member))
            {
                return;
            }

            // Skip test classes
            if (identifier.Text.Contains("Test") || identifier.Text.Contains("Mock"))
            {
                return;
            }

            // Check for XML documentation
            var trivia = member.GetLeadingTrivia();
            bool hasXmlDoc = trivia.Any(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || 
                                            t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

            if (!hasXmlDoc)
            {
                var diagnostic = Diagnostic.Create(
                    Rule, 
                    identifier.GetLocation(), 
                    memberType, 
                    identifier.Text);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool IsPublic(MemberDeclarationSyntax member)
        {
            return member.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        }
    }
}