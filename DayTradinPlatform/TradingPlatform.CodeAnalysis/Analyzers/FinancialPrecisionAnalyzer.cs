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
    /// Analyzer that enforces the use of System.Decimal for all monetary values
    /// and financial calculations, preventing precision loss from double/float usage.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FinancialPrecisionAnalyzer : TradingPlatformAnalyzerBase
    {
        protected override DiagnosticDescriptor Rule => DiagnosticDescriptors.UseDecimalForMoney;

        protected override ImmutableArray<DiagnosticDescriptor> AdditionalRules => 
            ImmutableArray.Create(
                DiagnosticDescriptors.AvoidPrecisionLoss,
                DiagnosticDescriptors.ValidateFinancialCalculation);

        protected override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest =>
            ImmutableArray.Create(
                SyntaxKind.FieldDeclaration,
                SyntaxKind.PropertyDeclaration,
                SyntaxKind.ParameterList,
                SyntaxKind.VariableDeclaration,
                SyntaxKind.MethodDeclaration,
                SyntaxKind.CastExpression);

        protected override void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            switch (context.Node)
            {
                case FieldDeclarationSyntax field:
                    AnalyzeFieldDeclaration(context, field);
                    break;
                case PropertyDeclarationSyntax property:
                    AnalyzePropertyDeclaration(context, property);
                    break;
                case ParameterListSyntax parameterList:
                    AnalyzeParameterList(context, parameterList);
                    break;
                case VariableDeclarationSyntax variable:
                    AnalyzeVariableDeclaration(context, variable);
                    break;
                case MethodDeclarationSyntax method:
                    AnalyzeMethodDeclaration(context, method);
                    break;
                case CastExpressionSyntax cast:
                    AnalyzeCastExpression(context, cast);
                    break;
            }
        }

        private void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context, FieldDeclarationSyntax field)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(field.Declaration.Type);
            if (typeInfo.Type == null) return;

            foreach (var variable in field.Declaration.Variables)
            {
                if (IsMonetaryIdentifier(variable.Identifier.Text) && IsFloatingPointType(typeInfo.Type))
                {
                    ReportDiagnostic(context, variable.Identifier, Rule,
                        variable.Identifier.Text, typeInfo.Type.Name, "decimal");
                }
            }
        }

        private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax property)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(property.Type);
            if (typeInfo.Type == null) return;

            if (IsMonetaryIdentifier(property.Identifier.Text) && IsFloatingPointType(typeInfo.Type))
            {
                ReportDiagnostic(context, property.Identifier, Rule,
                    property.Identifier.Text, typeInfo.Type.Name, "decimal");
            }
        }

        private void AnalyzeParameterList(SyntaxNodeAnalysisContext context, ParameterListSyntax parameterList)
        {
            foreach (var parameter in parameterList.Parameters)
            {
                if (parameter.Type == null) continue;

                var typeInfo = context.SemanticModel.GetTypeInfo(parameter.Type);
                if (typeInfo.Type == null) continue;

                if (IsMonetaryIdentifier(parameter.Identifier.Text) && IsFloatingPointType(typeInfo.Type))
                {
                    ReportDiagnostic(context, parameter.Identifier, Rule,
                        parameter.Identifier.Text, typeInfo.Type.Name, "decimal");
                }
            }
        }

        private void AnalyzeVariableDeclaration(SyntaxNodeAnalysisContext context, VariableDeclarationSyntax variable)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(variable.Type);
            if (typeInfo.Type == null) return;

            foreach (var declarator in variable.Variables)
            {
                if (IsMonetaryIdentifier(declarator.Identifier.Text) && IsFloatingPointType(typeInfo.Type))
                {
                    ReportDiagnostic(context, declarator.Identifier, Rule,
                        declarator.Identifier.Text, typeInfo.Type.Name, "decimal");
                }
            }
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method)
        {
            // Check return type
            if (method.ReturnType != null)
            {
                var returnTypeInfo = context.SemanticModel.GetTypeInfo(method.ReturnType);
                if (returnTypeInfo.Type != null && 
                    IsFinancialMethod(method.Identifier.Text) && 
                    IsFloatingPointType(returnTypeInfo.Type))
                {
                    ReportDiagnostic(context, method.ReturnType, Rule,
                        $"{method.Identifier.Text} return type", returnTypeInfo.Type.Name, "decimal");
                }
            }

            // Check for financial calculations using Math methods that return double
            var mathInvocations = method.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => IsMathMethodReturningDouble(context, inv));

            foreach (var invocation in mathInvocations)
            {
                if (IsUsedInFinancialContext(context, invocation))
                {
                    ReportDiagnostic(context, invocation, DiagnosticDescriptors.AvoidPrecisionLoss,
                        GetMethodName(invocation));
                }
            }
        }

        private void AnalyzeCastExpression(SyntaxNodeAnalysisContext context, CastExpressionSyntax cast)
        {
            var castTypeInfo = context.SemanticModel.GetTypeInfo(cast.Type);
            var expressionTypeInfo = context.SemanticModel.GetTypeInfo(cast.Expression);

            if (castTypeInfo.Type == null || expressionTypeInfo.Type == null) return;

            // Check for decimal to double/float casts
            if (IsDecimalType(expressionTypeInfo.Type) && IsFloatingPointType(castTypeInfo.Type))
            {
                ReportDiagnostic(context, cast, DiagnosticDescriptors.AvoidPrecisionLoss,
                    "decimal", castTypeInfo.Type.Name);
            }
        }

        private bool IsMonetaryIdentifier(string identifier)
        {
            var lowerIdentifier = identifier.ToLowerInvariant();
            var monetaryKeywords = new[]
            {
                "price", "cost", "amount", "total", "subtotal", "tax", "fee", "charge",
                "balance", "payment", "revenue", "profit", "loss", "margin", "commission",
                "discount", "credit", "debit", "money", "currency", "dollar", "cent",
                "value", "rate", "interest", "principal", "premium", "salary", "wage",
                "income", "expense", "budget", "invoice", "bill", "quote", "bid", "ask"
            };

            return monetaryKeywords.Any(keyword => lowerIdentifier.Contains(keyword));
        }

        private bool IsFinancialMethod(string methodName)
        {
            var lowerMethodName = methodName.ToLowerInvariant();
            var financialMethods = new[]
            {
                "calculate", "compute", "get", "total", "sum", "average", "convert",
                "discount", "tax", "interest", "commission", "profit", "loss"
            };

            return financialMethods.Any(keyword => lowerMethodName.Contains(keyword)) &&
                   IsMonetaryIdentifier(methodName);
        }

        private bool IsFloatingPointType(ITypeSymbol type)
        {
            if (type == null) return false;

            var typeName = type.ToDisplayString();
            return typeName == "double" || typeName == "System.Double" ||
                   typeName == "float" || typeName == "System.Single";
        }

        private bool IsDecimalType(ITypeSymbol type)
        {
            if (type == null) return false;

            var typeName = type.ToDisplayString();
            return typeName == "decimal" || typeName == "System.Decimal";
        }

        private bool IsMathMethodReturningDouble(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol method) return false;

            return method.ContainingType?.Name == "Math" &&
                   method.ContainingNamespace?.ToString() == "System" &&
                   method.ReturnType?.Name == "Double";
        }

        private bool IsUsedInFinancialContext(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            // Check if the result is assigned to a monetary variable
            var parent = node.Parent;
            while (parent != null)
            {
                if (parent is AssignmentExpressionSyntax assignment)
                {
                    if (assignment.Left is IdentifierNameSyntax identifier &&
                        IsMonetaryIdentifier(identifier.Identifier.Text))
                    {
                        return true;
                    }
                }
                else if (parent is VariableDeclaratorSyntax declarator &&
                         IsMonetaryIdentifier(declarator.Identifier.Text))
                {
                    return true;
                }
                else if (parent is ReturnStatementSyntax &&
                         parent.Parent?.Parent is MethodDeclarationSyntax method &&
                         IsFinancialMethod(method.Identifier.Text))
                {
                    return true;
                }

                // Don't traverse too far up
                if (parent is MethodDeclarationSyntax || parent is PropertyDeclarationSyntax)
                    break;

                parent = parent.Parent;
            }

            return false;
        }

        private string GetMethodName(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                return memberAccess.Name.ToString();
            }
            return invocation.Expression.ToString();
        }
    }
}