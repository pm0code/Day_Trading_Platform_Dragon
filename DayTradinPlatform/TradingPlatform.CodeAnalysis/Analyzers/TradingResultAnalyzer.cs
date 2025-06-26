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
    /// Analyzer that enforces the use of TradingResult<T> for all operation results,
    /// ensuring consistent error handling and result patterns across the platform.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TradingResultAnalyzer : TradingPlatformAnalyzerBase
    {
        protected override DiagnosticDescriptor Rule => DiagnosticDescriptors.UseTradingResult;

        protected override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest =>
            ImmutableArray.Create(
                SyntaxKind.MethodDeclaration,
                SyntaxKind.ThrowStatement,
                SyntaxKind.ReturnStatement);

        protected override void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            switch (context.Node)
            {
                case MethodDeclarationSyntax method:
                    AnalyzeMethodDeclaration(context, method);
                    break;
                case ThrowStatementSyntax throwStatement:
                    AnalyzeThrowStatement(context, throwStatement);
                    break;
                case ReturnStatementSyntax returnStatement:
                    AnalyzeReturnStatement(context, returnStatement);
                    break;
            }
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method)
        {
            // Skip if in test project
            if (IsInTestProject(context)) return;

            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method);
            if (methodSymbol == null) return;

            // Skip private methods and property accessors
            if (methodSymbol.DeclaredAccessibility == Accessibility.Private ||
                methodSymbol.MethodKind == MethodKind.PropertyGet ||
                methodSymbol.MethodKind == MethodKind.PropertySet)
                return;

            // Check if this is an operation method that should return TradingResult
            if (IsOperationMethod(methodSymbol) && !ReturnsTradingResult(methodSymbol))
            {
                // Skip if it's an interface implementation that we can't change
                if (methodSymbol.ExplicitInterfaceImplementations.Any())
                    return;

                // Skip framework override methods
                if (IsFrameworkOverride(methodSymbol))
                    return;

                var returnTypeName = methodSymbol.ReturnType.ToDisplayString();
                ReportDiagnostic(context, method.Identifier, Rule,
                    methodSymbol.Name, returnTypeName);
            }

            // Check for methods that throw exceptions instead of returning TradingResult
            if (ReturnsTradingResult(methodSymbol))
            {
                CheckForDirectExceptionThrows(context, method);
            }
        }

        private void AnalyzeThrowStatement(SyntaxNodeAnalysisContext context, ThrowStatementSyntax throwStatement)
        {
            // Check if we're in a method that returns TradingResult
            var containingMethod = throwStatement.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (containingMethod == null) return;

            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(containingMethod);
            if (methodSymbol == null) return;

            if (ReturnsTradingResult(methodSymbol))
            {
                // This is problematic - should return TradingResult.Failure instead
                ReportDiagnostic(context, throwStatement.ThrowKeyword,
                    DiagnosticDescriptors.NoSilentFailure,
                    "Use TradingResult.Failure instead of throwing exceptions");
            }
        }

        private void AnalyzeReturnStatement(SyntaxNodeAnalysisContext context, ReturnStatementSyntax returnStatement)
        {
            if (returnStatement.Expression == null) return;

            var containingMethod = returnStatement.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (containingMethod == null) return;

            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(containingMethod);
            if (methodSymbol == null || !ReturnsTradingResult(methodSymbol)) return;

            // Check if returning null instead of TradingResult
            if (returnStatement.Expression.IsKind(SyntaxKind.NullLiteralExpression))
            {
                ReportDiagnostic(context, returnStatement.Expression,
                    DiagnosticDescriptors.UseTradingResult,
                    "Returning null", "Use TradingResult.Failure or TradingResult.Success");
            }

            // Check if returning a raw value that should be wrapped
            var returnType = context.SemanticModel.GetTypeInfo(returnStatement.Expression);
            if (returnType.Type != null && !IsTradingResultType(returnType.Type))
            {
                // Check if it's a direct value that should be wrapped
                var expectedType = GetExpectedTradingResultType(methodSymbol);
                if (expectedType != null && IsAssignableTo(returnType.Type, expectedType))
                {
                    ReportDiagnostic(context, returnStatement.Expression,
                        DiagnosticDescriptors.UseTradingResult,
                        "Direct value return", "Wrap with TradingResult.Success()");
                }
            }
        }

        private bool IsOperationMethod(IMethodSymbol method)
        {
            // Skip constructors and special methods
            if (method.MethodKind != MethodKind.Ordinary &&
                method.MethodKind != MethodKind.ExplicitInterfaceImplementation)
                return false;

            // Methods that perform operations should use TradingResult
            var operationPrefixes = new[]
            {
                "Create", "Update", "Delete", "Get", "Find", "Search", "Load", "Save",
                "Execute", "Process", "Calculate", "Validate", "Submit", "Send",
                "Fetch", "Query", "Insert", "Remove", "Apply", "Perform", "Handle",
                "Connect", "Disconnect", "Start", "Stop", "Initialize", "Import", "Export"
            };

            var operationSuffixes = new[]
            {
                "Async", "Data", "Info", "Result", "Response", "Record", "Entity"
            };

            var methodName = method.Name;

            // Check prefixes
            if (operationPrefixes.Any(prefix => methodName.StartsWith(prefix)))
                return true;

            // Check if it's an async method (likely an operation)
            if (method.IsAsync && !method.Name.StartsWith("On") && !method.Name.EndsWith("Handler"))
                return true;

            // Check if returns data types that suggest operations
            var returnTypeName = method.ReturnType.ToDisplayString();
            if (operationSuffixes.Any(suffix => returnTypeName.Contains(suffix)))
                return true;

            return false;
        }

        private bool IsFrameworkOverride(IMethodSymbol method)
        {
            if (!method.IsOverride) return false;

            var baseMethod = method.OverriddenMethod;
            while (baseMethod != null)
            {
                var ns = baseMethod.ContainingNamespace?.ToString() ?? "";
                if (ns.StartsWith("System") || ns.StartsWith("Microsoft"))
                    return true;

                baseMethod = baseMethod.OverriddenMethod;
            }

            return false;
        }

        private bool IsTradingResultType(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol namedType)
            {
                if (namedType.Name == "TradingResult")
                    return true;

                // Check for Task<TradingResult<T>>
                if (namedType.Name == "Task" && namedType.TypeArguments.Length == 1)
                {
                    var innerType = namedType.TypeArguments[0];
                    return IsTradingResultType(innerType);
                }

                // Check for ValueTask<TradingResult<T>>
                if (namedType.Name == "ValueTask" && namedType.TypeArguments.Length == 1)
                {
                    var innerType = namedType.TypeArguments[0];
                    return IsTradingResultType(innerType);
                }
            }

            return false;
        }

        private ITypeSymbol GetExpectedTradingResultType(IMethodSymbol method)
        {
            var returnType = method.ReturnType;
            if (returnType is INamedTypeSymbol namedType)
            {
                // Extract T from TradingResult<T>
                if (namedType.Name == "TradingResult" && namedType.TypeArguments.Length == 1)
                {
                    return namedType.TypeArguments[0];
                }

                // Extract T from Task<TradingResult<T>>
                if ((namedType.Name == "Task" || namedType.Name == "ValueTask") && 
                    namedType.TypeArguments.Length == 1)
                {
                    var innerType = namedType.TypeArguments[0] as INamedTypeSymbol;
                    if (innerType?.Name == "TradingResult" && innerType.TypeArguments.Length == 1)
                    {
                        return innerType.TypeArguments[0];
                    }
                }
            }

            return null;
        }

        private bool IsAssignableTo(ITypeSymbol source, ITypeSymbol target)
        {
            if (source == null || target == null) return false;

            // Check exact match
            if (SymbolEqualityComparer.Default.Equals(source, target))
                return true;

            // Check inheritance
            var baseType = source.BaseType;
            while (baseType != null)
            {
                if (SymbolEqualityComparer.Default.Equals(baseType, target))
                    return true;
                baseType = baseType.BaseType;
            }

            // Check interface implementation
            if (target.TypeKind == TypeKind.Interface)
            {
                return source.AllInterfaces.Any(i => 
                    SymbolEqualityComparer.Default.Equals(i, target));
            }

            return false;
        }

        private void CheckForDirectExceptionThrows(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method)
        {
            var throwStatements = method.DescendantNodes().OfType<ThrowStatementSyntax>();
            
            foreach (var throwStatement in throwStatements)
            {
                // Check if it's inside a catch block (re-throwing is OK)
                var catchClause = throwStatement.FirstAncestorOrSelf<CatchClauseSyntax>();
                if (catchClause != null && throwStatement.Expression == null)
                    continue; // Re-throw is acceptable

                // Otherwise, this is problematic
                ReportDiagnostic(context, throwStatement.ThrowKeyword,
                    DiagnosticDescriptors.NoSilentFailure,
                    "Return TradingResult.Failure instead of throwing exceptions");
            }
        }
    }
}