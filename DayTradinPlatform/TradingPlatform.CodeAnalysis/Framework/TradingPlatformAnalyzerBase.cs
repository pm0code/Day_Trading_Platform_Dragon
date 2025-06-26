using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace TradingPlatform.CodeAnalysis.Framework
{
    /// <summary>
    /// Base class for all TradingPlatform analyzers.
    /// Provides common functionality for canonical pattern enforcement,
    /// high-performance code analysis, and modular architecture validation.
    /// </summary>
    public abstract class TradingPlatformAnalyzerBase : DiagnosticAnalyzer
    {
        #region Abstract Members

        /// <summary>
        /// Gets the diagnostic descriptor for this analyzer.
        /// </summary>
        protected abstract DiagnosticDescriptor Rule { get; }

        /// <summary>
        /// Gets additional rules this analyzer can report.
        /// </summary>
        protected virtual ImmutableArray<DiagnosticDescriptor> AdditionalRules => ImmutableArray<DiagnosticDescriptor>.Empty;

        /// <summary>
        /// Gets the syntax node types this analyzer is interested in.
        /// </summary>
        protected abstract ImmutableArray<SyntaxKind> SyntaxKindsOfInterest { get; }

        /// <summary>
        /// Analyzes the syntax node and reports diagnostics.
        /// </summary>
        protected abstract void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context);

        #endregion

        #region DiagnosticAnalyzer Implementation

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                var builder = ImmutableArray.CreateBuilder<DiagnosticDescriptor>();
                builder.Add(Rule);
                builder.AddRange(AdditionalRules);
                return builder.ToImmutable();
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            // Configure analyzer for production use
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Register for syntax node analysis
            if (SyntaxKindsOfInterest.Length > 0)
            {
                context.RegisterSyntaxNodeAction(
                    AnalyzeSyntaxNode,
                    SyntaxKindsOfInterest);
            }

            // Allow derived classes to register additional actions
            RegisterAdditionalActions(context);
        }

        /// <summary>
        /// Override to register additional analysis actions.
        /// </summary>
        protected virtual void RegisterAdditionalActions(AnalysisContext context)
        {
            // Default implementation does nothing
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Reports a diagnostic for the given syntax node.
        /// </summary>
        protected void ReportDiagnostic(
            SyntaxNodeAnalysisContext context,
            SyntaxNode node,
            DiagnosticDescriptor rule = null,
            params object[] messageArgs)
        {
            var diagnostic = Diagnostic.Create(
                rule ?? Rule,
                node.GetLocation(),
                messageArgs);

            context.ReportDiagnostic(diagnostic);
        }

        /// <summary>
        /// Reports a diagnostic for the given location.
        /// </summary>
        protected void ReportDiagnostic(
            SyntaxNodeAnalysisContext context,
            Location location,
            DiagnosticDescriptor rule = null,
            params object[] messageArgs)
        {
            var diagnostic = Diagnostic.Create(
                rule ?? Rule,
                location,
                messageArgs);

            context.ReportDiagnostic(diagnostic);
        }

        /// <summary>
        /// Checks if a type inherits from the specified base type.
        /// </summary>
        protected bool InheritsFrom(ITypeSymbol type, string baseTypeName)
        {
            if (type == null || string.IsNullOrWhiteSpace(baseTypeName))
                return false;

            var current = type;
            while (current != null)
            {
                if (current.Name == baseTypeName || 
                    current.ToDisplayString() == baseTypeName)
                    return true;

                current = current.BaseType;
            }

            return false;
        }

        /// <summary>
        /// Checks if a type implements the specified interface.
        /// </summary>
        protected bool ImplementsInterface(ITypeSymbol type, string interfaceName)
        {
            if (type == null || string.IsNullOrWhiteSpace(interfaceName))
                return false;

            return type.AllInterfaces.Any(i => 
                i.Name == interfaceName || 
                i.ToDisplayString() == interfaceName);
        }

        /// <summary>
        /// Gets the containing type of a syntax node.
        /// </summary>
        protected INamedTypeSymbol GetContainingType(
            SyntaxNodeAnalysisContext context,
            SyntaxNode node)
        {
            var typeDeclaration = node.FirstAncestorOrSelf<CSharp.Syntax.TypeDeclarationSyntax>();
            if (typeDeclaration == null)
                return null;

            return context.SemanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
        }

        /// <summary>
        /// Checks if the node is in a test project.
        /// </summary>
        protected bool IsInTestProject(SyntaxNodeAnalysisContext context)
        {
            var compilation = context.Compilation;
            return compilation.AssemblyName?.Contains("Test", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        /// <summary>
        /// Checks if the type is a canonical service type.
        /// </summary>
        protected bool IsCanonicalServiceType(ITypeSymbol type)
        {
            return InheritsFrom(type, "CanonicalServiceBase") ||
                   InheritsFrom(type, "CanonicalStrategyBase") ||
                   InheritsFrom(type, "CanonicalRepositoryBase");
        }

        /// <summary>
        /// Checks if a method returns TradingResult.
        /// </summary>
        protected bool ReturnsTradingResult(IMethodSymbol method)
        {
            if (method == null || method.ReturnType == null)
                return false;

            var returnType = method.ReturnType;
            
            // Check for TradingResult<T>
            if (returnType is INamedTypeSymbol namedType)
            {
                if (namedType.Name == "TradingResult" &&
                    namedType.ContainingNamespace?.Name == "Models")
                {
                    return true;
                }

                // Check for Task<TradingResult<T>>
                if (namedType.Name == "Task" && namedType.TypeArguments.Length == 1)
                {
                    var innerType = namedType.TypeArguments[0] as INamedTypeSymbol;
                    return innerType != null && 
                           innerType.Name == "TradingResult" &&
                           innerType.ContainingNamespace?.Name == "Models";
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the namespace of a syntax node.
        /// </summary>
        protected string GetNamespace(SyntaxNode node)
        {
            var namespaceDeclaration = node.FirstAncestorOrSelf<CSharp.Syntax.NamespaceDeclarationSyntax>();
            return namespaceDeclaration?.Name.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Checks if the code is in a performance-critical path.
        /// </summary>
        protected bool IsPerformanceCritical(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            // Check for performance-critical attributes
            var containingMember = node.FirstAncestorOrSelf<CSharp.Syntax.MemberDeclarationSyntax>();
            if (containingMember == null)
                return false;

            var symbol = context.SemanticModel.GetDeclaredSymbol(containingMember);
            if (symbol == null)
                return false;

            // Check for [PerformanceCritical] or [LatencySensitive] attributes
            return symbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name == "PerformanceCriticalAttribute" ||
                attr.AttributeClass?.Name == "LatencySensitiveAttribute" ||
                attr.AttributeClass?.Name == "HotPathAttribute");
        }

        #endregion

        #region Diagnostic Helpers

        /// <summary>
        /// Creates a diagnostic descriptor with standard formatting.
        /// </summary>
        protected static DiagnosticDescriptor CreateRule(
            string id,
            string title,
            string messageFormat,
            string category,
            DiagnosticSeverity severity = DiagnosticSeverity.Warning,
            string description = null,
            string helpLinkUri = null)
        {
            return new DiagnosticDescriptor(
                id: id,
                title: title,
                messageFormat: messageFormat,
                category: category,
                defaultSeverity: severity,
                isEnabledByDefault: true,
                description: description,
                helpLinkUri: helpLinkUri ?? $"https://github.com/tradingplatform/analyzers/blob/main/docs/{id}.md");
        }

        #endregion
    }

    /// <summary>
    /// Categories for analyzer diagnostics.
    /// </summary>
    public static class AnalyzerCategories
    {
        public const string FinancialPrecision = "Financial Precision";
        public const string CanonicalPatterns = "Canonical Patterns";
        public const string Performance = "Performance";
        public const string Security = "Security";
        public const string Architecture = "Architecture";
        public const string ErrorHandling = "Error Handling";
        public const string Testing = "Testing";
        public const string Documentation = "Documentation";
        public const string Modularity = "Modularity";
        public const string Configuration = "Configuration";
    }

    /// <summary>
    /// Common diagnostic IDs.
    /// </summary>
    public static class DiagnosticIds
    {
        // Financial Precision
        public const string UseDecimalForMoney = "TP0001";
        public const string AvoidPrecisionLoss = "TP0002";
        public const string ValidateFinancialCalculation = "TP0003";

        // Canonical Patterns
        public const string UseCanonicalBase = "TP0101";
        public const string UseTradingResult = "TP0102";
        public const string ImplementLifecycle = "TP0103";
        public const string ImplementHealthCheck = "TP0104";

        // Performance
        public const string AvoidBoxing = "TP0201";
        public const string UseObjectPooling = "TP0202";
        public const string AvoidAllocation = "TP0203";
        public const string UseSpan = "TP0204";

        // Security
        public const string NoHardcodedSecrets = "TP0301";
        public const string UseSafeSQL = "TP0302";
        public const string ProtectPII = "TP0303";

        // Architecture
        public const string LayerViolation = "TP0401";
        public const string CircularDependency = "TP0402";
        public const string ModuleIsolation = "TP0403";

        // Error Handling
        public const string NoSilentFailure = "TP0501";
        public const string UseCanonicalLogging = "TP0502";
        public const string ImplementRetry = "TP0503";
    }
}