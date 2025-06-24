// File: TradingPlatform.Auditing.Analyzers\NamingConventionAnalyzer.cs

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TradingPlatform.Auditing
{
    /// <summary>
    /// Analyzer that enforces naming conventions
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NamingConventionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TP007";
        private const string Category = "Naming";

        private static readonly DiagnosticDescriptor InterfaceRule = new DiagnosticDescriptor(
            DiagnosticId + "A",
            "Interface should start with 'I'",
            "Interface '{0}' should start with 'I'",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor PrivateFieldRule = new DiagnosticDescriptor(
            DiagnosticId + "B",
            "Private field should start with underscore",
            "Private field '{0}' should start with underscore",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            ImmutableArray.Create(InterfaceRule, PrivateFieldRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInterface, SyntaxKind.InterfaceDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
        }

        private void AnalyzeInterface(SyntaxNodeAnalysisContext context)
        {
            var interfaceDeclaration = (InterfaceDeclarationSyntax)context.Node;
            var name = interfaceDeclaration.Identifier.Text;
            
            if (!name.StartsWith("I") || (name.Length > 1 && char.IsLower(name[1])))
            {
                var diagnostic = Diagnostic.Create(
                    InterfaceRule, 
                    interfaceDeclaration.Identifier.GetLocation(), 
                    name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeField(SyntaxNodeAnalysisContext context)
        {
            var fieldDeclaration = (FieldDeclarationSyntax)context.Node;
            
            // Check if it's private
            if (!fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)) &&
                !IsImplicitlyPrivate(fieldDeclaration))
            {
                return;
            }

            foreach (var variable in fieldDeclaration.Declaration.Variables)
            {
                var name = variable.Identifier.Text;
                
                // Skip constants and static fields with s_ prefix
                if (fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                {
                    continue;
                }
                
                if (fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
                {
                    if (!name.StartsWith("s_"))
                    {
                        var diagnostic = Diagnostic.Create(
                            PrivateFieldRule, 
                            variable.Identifier.GetLocation(), 
                            name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
                else if (!name.StartsWith("_"))
                {
                    var diagnostic = Diagnostic.Create(
                        PrivateFieldRule, 
                        variable.Identifier.GetLocation(), 
                        name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private bool IsImplicitlyPrivate(FieldDeclarationSyntax field)
        {
            // If no access modifier is specified, fields are private by default
            return !field.Modifiers.Any(m => 
                m.IsKind(SyntaxKind.PublicKeyword) ||
                m.IsKind(SyntaxKind.ProtectedKeyword) ||
                m.IsKind(SyntaxKind.InternalKeyword));
        }
    }
}