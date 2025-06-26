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
    /// Analyzer that enforces performance best practices for ultra-low latency trading systems.
    /// Detects allocations, boxing, and inefficient patterns in performance-critical code.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PerformanceAnalyzer : TradingPlatformAnalyzerBase
    {
        protected override DiagnosticDescriptor Rule => DiagnosticDescriptors.AvoidAllocation;

        protected override ImmutableArray<DiagnosticDescriptor> AdditionalRules =>
            ImmutableArray.Create(
                DiagnosticDescriptors.AvoidBoxing,
                DiagnosticDescriptors.UseObjectPooling,
                DiagnosticDescriptors.UseSpan);

        protected override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest =>
            ImmutableArray.Create(
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ArrayCreationExpression,
                SyntaxKind.ImplicitArrayCreationExpression,
                SyntaxKind.CastExpression,
                SyntaxKind.ConversionOperatorDeclaration,
                SyntaxKind.InvocationExpression);

        protected override void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            // Skip test projects for performance analysis
            if (IsInTestProject(context)) return;

            switch (context.Node)
            {
                case ObjectCreationExpressionSyntax objectCreation:
                    AnalyzeObjectCreation(context, objectCreation);
                    break;
                case ArrayCreationExpressionSyntax arrayCreation:
                    AnalyzeArrayCreation(context, arrayCreation);
                    break;
                case ImplicitArrayCreationExpressionSyntax implicitArray:
                    AnalyzeImplicitArrayCreation(context, implicitArray);
                    break;
                case CastExpressionSyntax cast:
                    AnalyzeCastExpression(context, cast);
                    break;
                case InvocationExpressionSyntax invocation:
                    AnalyzeMethodInvocation(context, invocation);
                    break;
            }
        }

        private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, ObjectCreationExpressionSyntax objectCreation)
        {
            // Check if in performance-critical context
            if (!IsPerformanceCritical(context, objectCreation)) return;

            var typeInfo = context.SemanticModel.GetTypeInfo(objectCreation);
            if (typeInfo.Type == null) return;

            // Check for allocations in hot path
            if (IsInHotPath(context, objectCreation))
            {
                // Suggest object pooling for frequently allocated types
                if (IsFrequentlyAllocatedType(typeInfo.Type))
                {
                    ReportDiagnostic(context, objectCreation, DiagnosticDescriptors.UseObjectPooling,
                        typeInfo.Type.Name);
                }
                else
                {
                    ReportDiagnostic(context, objectCreation, Rule,
                        $"new {typeInfo.Type.Name}()");
                }
            }

            // Check for LINQ usage (which allocates enumerators)
            if (IsLinqMethod(objectCreation))
            {
                ReportDiagnostic(context, objectCreation, Rule,
                    "LINQ operation (consider using for loops or Span<T>)");
            }
        }

        private void AnalyzeArrayCreation(SyntaxNodeAnalysisContext context, ArrayCreationExpressionSyntax arrayCreation)
        {
            if (!IsPerformanceCritical(context, arrayCreation)) return;

            // Suggest ArrayPool for temporary arrays
            if (IsTemporaryArray(context, arrayCreation))
            {
                ReportDiagnostic(context, arrayCreation, DiagnosticDescriptors.UseObjectPooling,
                    "Array (use ArrayPool<T> for temporary arrays)");
            }

            // Suggest Span<T> for local arrays
            if (IsLocalArray(context, arrayCreation))
            {
                ReportDiagnostic(context, arrayCreation, DiagnosticDescriptors.UseSpan,
                    "array", "stackalloc or Span<T>");
            }
        }

        private void AnalyzeImplicitArrayCreation(SyntaxNodeAnalysisContext context, ImplicitArrayCreationExpressionSyntax implicitArray)
        {
            if (!IsPerformanceCritical(context, implicitArray)) return;

            ReportDiagnostic(context, implicitArray, Rule,
                "implicit array creation");
        }

        private void AnalyzeCastExpression(SyntaxNodeAnalysisContext context, CastExpressionSyntax cast)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(cast.Expression);
            var castTypeInfo = context.SemanticModel.GetTypeInfo(cast.Type);

            if (typeInfo.Type == null || castTypeInfo.Type == null) return;

            // Check for boxing operations
            if (IsBoxingOperation(typeInfo.Type, castTypeInfo.Type))
            {
                ReportDiagnostic(context, cast, DiagnosticDescriptors.AvoidBoxing,
                    $"Boxing of {typeInfo.Type.Name} to {castTypeInfo.Type.Name}");
            }
        }

        private void AnalyzeMethodInvocation(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol method) return;

            // Check for string concatenation in loops
            if (IsStringConcatenation(method) && IsInLoop(invocation))
            {
                ReportDiagnostic(context, invocation, Rule,
                    "String concatenation in loop (use StringBuilder)");
            }

            // Check for LINQ in performance-critical paths
            if (IsLinqMethod(method) && IsPerformanceCritical(context, invocation))
            {
                ReportDiagnostic(context, invocation, Rule,
                    $"LINQ method '{method.Name}' (consider using for loops)");
            }

            // Check for params array allocations
            if (HasParamsParameter(method) && IsPerformanceCritical(context, invocation))
            {
                ReportDiagnostic(context, invocation, Rule,
                    $"params array in method '{method.Name}'");
            }
        }

        private bool IsInHotPath(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            // Check if in a method marked with [HotPath] or similar attributes
            var containingMethod = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (containingMethod != null)
            {
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(containingMethod);
                if (methodSymbol != null)
                {
                    var hotPathAttribute = methodSymbol.GetAttributes()
                        .Any(attr => attr.AttributeClass?.Name == "HotPathAttribute" ||
                                   attr.AttributeClass?.Name == "PerformanceCriticalAttribute");
                    if (hotPathAttribute) return true;
                }
            }

            // Check if in a loop
            return IsInLoop(node);
        }

        private bool IsInLoop(SyntaxNode node)
        {
            var parent = node.Parent;
            while (parent != null)
            {
                if (parent is ForStatementSyntax ||
                    parent is ForEachStatementSyntax ||
                    parent is WhileStatementSyntax ||
                    parent is DoStatementSyntax)
                {
                    return true;
                }

                // Don't traverse beyond method boundaries
                if (parent is MethodDeclarationSyntax)
                    break;

                parent = parent.Parent;
            }
            return false;
        }

        private bool IsFrequentlyAllocatedType(ITypeSymbol type)
        {
            var frequentTypes = new[]
            {
                "List", "Dictionary", "HashSet", "StringBuilder",
                "MemoryStream", "Task", "CancellationTokenSource"
            };

            return frequentTypes.Any(t => type.Name.Contains(t));
        }

        private bool IsLinqMethod(ObjectCreationExpressionSyntax objectCreation)
        {
            // Check if creating LINQ-related objects
            var type = objectCreation.Type.ToString();
            return type.Contains("Enumerable") || type.Contains("Queryable");
        }

        private bool IsLinqMethod(IMethodSymbol method)
        {
            return method.ContainingNamespace?.ToString() == "System.Linq" ||
                   method.ContainingType?.Name == "Enumerable" ||
                   method.ContainingType?.Name == "Queryable";
        }

        private bool IsTemporaryArray(SyntaxNodeAnalysisContext context, ArrayCreationExpressionSyntax array)
        {
            // Check if array is used only locally and not stored
            var parent = array.Parent;
            return parent is ArgumentSyntax || 
                   parent is ReturnStatementSyntax ||
                   (parent is EqualsValueClauseSyntax equalsValue && 
                    equalsValue.Parent is VariableDeclaratorSyntax);
        }

        private bool IsLocalArray(SyntaxNodeAnalysisContext context, ArrayCreationExpressionSyntax array)
        {
            // Check if array is a local variable
            var parent = array.Parent;
            while (parent != null)
            {
                if (parent is LocalDeclarationStatementSyntax)
                    return true;
                
                if (parent is MethodDeclarationSyntax || parent is PropertyDeclarationSyntax)
                    break;

                parent = parent.Parent;
            }
            return false;
        }

        private bool IsBoxingOperation(ITypeSymbol fromType, ITypeSymbol toType)
        {
            // Check if converting value type to reference type
            if (fromType.IsValueType && !toType.IsValueType)
            {
                // Special case: Nullable<T> is a value type but doesn't box in some cases
                if (fromType.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T)
                    return false;

                return true;
            }

            return false;
        }

        private bool IsStringConcatenation(IMethodSymbol method)
        {
            return method.ContainingType?.SpecialType == SpecialType.System_String &&
                   (method.Name == "Concat" || method.Name == "op_Addition");
        }

        private bool HasParamsParameter(IMethodSymbol method)
        {
            return method.Parameters.Any(p => p.IsParams);
        }
    }
}