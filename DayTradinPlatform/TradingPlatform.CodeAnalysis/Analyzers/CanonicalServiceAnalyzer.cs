using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using TradingPlatform.CodeAnalysis.Framework;

namespace TradingPlatform.CodeAnalysis.Analyzers
{
    /// <summary>
    /// Analyzer that ensures all service classes extend appropriate canonical base classes
    /// and follow established patterns for modularity and dependency injection.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CanonicalServiceAnalyzer : TradingPlatformAnalyzerBase
    {
        protected override DiagnosticDescriptor Rule => DiagnosticDescriptors.UseCanonicalBase;

        protected override ImmutableArray<DiagnosticDescriptor> AdditionalRules =>
            ImmutableArray.Create(
                DiagnosticDescriptors.ImplementLifecycle,
                DiagnosticDescriptors.ImplementHealthCheck);

        protected override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest =>
            ImmutableArray.Create(
                SyntaxKind.ClassDeclaration,
                SyntaxKind.InterfaceDeclaration);

        protected override void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            switch (context.Node)
            {
                case ClassDeclarationSyntax classDeclaration:
                    AnalyzeClassDeclaration(context, classDeclaration);
                    break;
                case InterfaceDeclarationSyntax interfaceDeclaration:
                    AnalyzeInterfaceDeclaration(context, interfaceDeclaration);
                    break;
            }
        }

        private void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration)
        {
            // Skip if in test project
            if (IsInTestProject(context)) return;

            var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
            if (symbol == null) return;

            // Check if this is a service class
            if (IsServiceClass(symbol))
            {
                // Check if it extends canonical base
                if (!ExtendsCanonicalBase(symbol))
                {
                    var suggestedBase = GetSuggestedCanonicalBase(symbol);
                    ReportDiagnostic(context, classDeclaration.Identifier, Rule,
                        symbol.Name, suggestedBase);
                }

                // Check lifecycle implementation
                if (!ImplementsLifecycleMethods(symbol))
                {
                    ReportDiagnostic(context, classDeclaration.Identifier, 
                        DiagnosticDescriptors.ImplementLifecycle, symbol.Name);
                }

                // Check health check implementation
                if (RequiresHealthCheck(symbol) && !ImplementsHealthCheck(symbol))
                {
                    ReportDiagnostic(context, classDeclaration.Identifier,
                        DiagnosticDescriptors.ImplementHealthCheck, symbol.Name);
                }
            }

            // Check for direct logger usage instead of canonical pattern
            CheckForDirectLoggerUsage(context, classDeclaration);
        }

        private void AnalyzeInterfaceDeclaration(SyntaxNodeAnalysisContext context, InterfaceDeclarationSyntax interfaceDeclaration)
        {
            var symbol = context.SemanticModel.GetDeclaredSymbol(interfaceDeclaration);
            if (symbol == null) return;

            // Check if service interfaces follow naming conventions
            if (IsServiceInterface(symbol) && !symbol.Name.StartsWith("I"))
            {
                // This would be handled by naming convention analyzer
                // Just noting it here for completeness
            }
        }

        private bool IsServiceClass(INamedTypeSymbol type)
        {
            var className = type.Name;
            var servicePatterns = new[]
            {
                "Service", "Repository", "Manager", "Handler", "Provider",
                "Factory", "Strategy", "Processor", "Engine", "Coordinator",
                "Orchestrator", "Controller", "Worker", "Agent", "Monitor"
            };

            // Check class name patterns
            if (servicePatterns.Any(pattern => className.EndsWith(pattern)))
                return true;

            // Check if implements service interfaces
            if (type.Interfaces.Any(i => IsServiceInterface(i)))
                return true;

            // Check if has dependency injection constructor
            var constructors = type.Constructors.Where(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public);
            if (constructors.Any(c => c.Parameters.Length > 0 && 
                c.Parameters.All(p => p.Type.TypeKind == TypeKind.Interface)))
                return true;

            return false;
        }

        private bool IsServiceInterface(ITypeSymbol type)
        {
            if (type.Name.StartsWith("I"))
            {
                var interfaceName = type.Name.Substring(1);
                var servicePatterns = new[]
                {
                    "Service", "Repository", "Manager", "Handler", "Provider",
                    "Factory", "Strategy", "Processor", "Engine", "Logger"
                };

                return servicePatterns.Any(pattern => interfaceName.EndsWith(pattern));
            }

            return false;
        }

        private bool ExtendsCanonicalBase(INamedTypeSymbol type)
        {
            return InheritsFrom(type, "CanonicalServiceBase") ||
                   InheritsFrom(type, "CanonicalStrategyBase") ||
                   InheritsFrom(type, "CanonicalRepositoryBase") ||
                   InheritsFrom(type, "CanonicalControllerBase") ||
                   InheritsFrom(type, "CanonicalWorkerBase");
        }

        private string GetSuggestedCanonicalBase(INamedTypeSymbol type)
        {
            var className = type.Name;

            if (className.EndsWith("Repository"))
                return "CanonicalRepositoryBase";
            if (className.EndsWith("Strategy"))
                return "CanonicalStrategyBase";
            if (className.EndsWith("Controller"))
                return "CanonicalControllerBase";
            if (className.EndsWith("Worker") || className.EndsWith("Agent"))
                return "CanonicalWorkerBase";

            // Default for all other services
            return "CanonicalServiceBase";
        }

        private bool ImplementsLifecycleMethods(INamedTypeSymbol type)
        {
            var requiredMethods = new[] { "OnInitializeAsync", "OnStartAsync", "OnStopAsync" };
            var methods = type.GetMembers().OfType<IMethodSymbol>();

            foreach (var requiredMethod in requiredMethods)
            {
                var hasMethod = methods.Any(m => 
                    m.Name == requiredMethod && 
                    m.DeclaredAccessibility == Accessibility.Protected &&
                    m.IsOverride);

                if (!hasMethod)
                    return false;
            }

            return true;
        }

        private bool RequiresHealthCheck(INamedTypeSymbol type)
        {
            // Services that interact with external resources should have health checks
            var requiresHealthCheckPatterns = new[]
            {
                "Repository", "Provider", "Client", "Service", "Connection",
                "DataSource", "ApiClient", "HttpClient"
            };

            return requiresHealthCheckPatterns.Any(pattern => type.Name.Contains(pattern));
        }

        private bool ImplementsHealthCheck(INamedTypeSymbol type)
        {
            // Check if implements IHealthCheck interface
            if (ImplementsInterface(type, "IHealthCheck"))
                return true;

            // Check if has health check method
            var methods = type.GetMembers().OfType<IMethodSymbol>();
            return methods.Any(m => 
                (m.Name == "CheckHealthAsync" || m.Name == "GetHealthAsync") &&
                m.DeclaredAccessibility == Accessibility.Public);
        }

        private void CheckForDirectLoggerUsage(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration)
        {
            // Find all field and property declarations
            var fields = classDeclaration.Members.OfType<FieldDeclarationSyntax>();
            var properties = classDeclaration.Members.OfType<PropertyDeclarationSyntax>();

            foreach (var field in fields)
            {
                var typeInfo = context.SemanticModel.GetTypeInfo(field.Declaration.Type);
                if (IsLoggerType(typeInfo.Type))
                {
                    // Check if it's injected (which is OK) or created directly (which is not)
                    foreach (var variable in field.Declaration.Variables)
                    {
                        if (variable.Initializer != null && 
                            variable.Initializer.Value is ObjectCreationExpressionSyntax)
                        {
                            ReportDiagnostic(context, variable.Identifier,
                                DiagnosticDescriptors.UseCanonicalLogging,
                                "Direct logger instantiation detected. Use canonical logging methods instead.");
                        }
                    }
                }
            }
        }

        private bool IsLoggerType(ITypeSymbol type)
        {
            if (type == null) return false;

            var loggerTypes = new[]
            {
                "ILogger", "ILogger`1", "ILoggerFactory", "Logger",
                "ITradingLogger", "ICanonicalLogger"
            };

            return loggerTypes.Any(loggerType => 
                type.Name == loggerType || 
                (type is INamedTypeSymbol namedType && namedType.ConstructedFrom?.Name == loggerType));
        }
    }
}