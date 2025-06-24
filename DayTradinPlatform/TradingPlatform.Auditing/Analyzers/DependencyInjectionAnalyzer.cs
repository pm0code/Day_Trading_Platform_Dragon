// File: TradingPlatform.Auditing.Analyzers\DependencyInjectionAnalyzer.cs

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TradingPlatform.Auditing
{
    /// <summary>
    /// Analyzer that checks for proper dependency injection patterns
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DependencyInjectionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TP005";
        private const string Category = "Dependency Injection";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Service should be injected, not instantiated",
            "Service '{0}' should be injected through constructor, not instantiated with 'new'",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Services should be injected through dependency injection rather than instantiated directly.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        }

        private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var objectCreation = (ObjectCreationExpressionSyntax)context.Node;
            var typeName = objectCreation.Type.ToString();
            
            // Check if it's a service type
            if ((typeName.EndsWith("Service") || 
                 typeName.EndsWith("Provider") || 
                 typeName.EndsWith("Manager") || 
                 typeName.EndsWith("Repository") ||
                 typeName.EndsWith("Monitor") ||
                 typeName.EndsWith("Calculator")) &&
                !typeName.Contains("Mock") &&
                !typeName.Contains("Test"))
            {
                // Check if we're in a test context
                var containingClass = objectCreation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (containingClass?.Identifier.Text.Contains("Test") == true)
                {
                    return;
                }

                // Check if we're in a factory or builder
                var containingMethod = objectCreation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (containingMethod?.Identifier.Text.StartsWith("Create") == true ||
                    containingMethod?.Identifier.Text.StartsWith("Build") == true)
                {
                    return;
                }

                var diagnostic = Diagnostic.Create(Rule, objectCreation.GetLocation(), typeName);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}