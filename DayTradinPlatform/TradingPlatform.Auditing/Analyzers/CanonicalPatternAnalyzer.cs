// File: TradingPlatform.Auditing.Analyzers\CanonicalPatternAnalyzer.cs

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TradingPlatform.Auditing
{
    /// <summary>
    /// Roslyn analyzer that enforces canonical pattern usage for services, providers, and engines.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CanonicalPatternAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TP001";
        private const string Category = "Design";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Service should use canonical pattern",
            "'{0}' should inherit from canonical base class",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Services, Providers, and Engines should inherit from their respective canonical base classes.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
        }

        private void AnalyzeClass(SyntaxNodeAnalysisContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            var className = classDeclaration.Identifier.Text;

            // Skip test classes and interfaces
            if (className.Contains("Test") || className.Contains("Mock") || 
                classDeclaration.Modifiers.Any(SyntaxKind.AbstractKeyword))
            {
                return;
            }

            // Check if it's a service/provider/engine that should be canonical
            bool shouldBeCanonical = false;
            string expectedBase = "";

            if (className.EndsWith("Service") && !className.Contains("Canonical"))
            {
                shouldBeCanonical = true;
                expectedBase = "CanonicalServiceBase";
            }
            else if (className.EndsWith("Provider") && !className.Contains("Canonical"))
            {
                shouldBeCanonical = true;
                expectedBase = "CanonicalProvider";
            }
            else if (className.EndsWith("Engine") && !className.Contains("Canonical"))
            {
                shouldBeCanonical = true;
                expectedBase = "CanonicalEngine";
            }
            else if ((className.EndsWith("Monitor") || className.EndsWith("Calculator")) && !className.Contains("Canonical"))
            {
                shouldBeCanonical = true;
                expectedBase = "CanonicalServiceBase or CanonicalRiskEvaluator";
            }

            if (!shouldBeCanonical) return;

            // Check if it inherits from canonical base
            bool inheritsFromCanonical = false;
            if (classDeclaration.BaseList != null)
            {
                foreach (var baseType in classDeclaration.BaseList.Types)
                {
                    var baseTypeName = baseType.Type.ToString();
                    if (baseTypeName.Contains("Canonical"))
                    {
                        inheritsFromCanonical = true;
                        break;
                    }
                }
            }

            if (!inheritsFromCanonical)
            {
                var diagnostic = Diagnostic.Create(Rule, classDeclaration.Identifier.GetLocation(), className);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}