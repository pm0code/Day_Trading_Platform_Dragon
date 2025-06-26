using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using TradingPlatform.CodeAnalysis.Framework;
using TradingPlatform.CodeAnalysis.Diagnostics;

namespace TradingPlatform.CodeAnalysis.Analyzers.Security
{
    /// <summary>
    /// Analyzer that detects potential SQL injection vulnerabilities in database queries.
    /// Enforces use of parameterized queries and prevents string concatenation with user input.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SQLInjectionAnalyzer : TradingPlatformAnalyzerBase
    {
        protected override DiagnosticDescriptor Rule => DiagnosticDescriptors.UseSafeSQL;

        protected override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest =>
            ImmutableArray.Create(
                SyntaxKind.AddExpression,
                SyntaxKind.InterpolatedStringExpression,
                SyntaxKind.InvocationExpression);

        // SQL command methods that execute queries
        private static readonly string[] SqlExecuteMethods = new[]
        {
            "ExecuteNonQuery", "ExecuteReader", "ExecuteScalar", "ExecuteAsync",
            "Execute", "Query", "QueryAsync", "QueryFirst", "QueryFirstAsync",
            "QuerySingle", "QuerySingleAsync", "QueryMultiple", "QueryMultipleAsync",
            "ExecuteSqlCommand", "ExecuteSqlRaw", "FromSqlRaw", "FromSql"
        };

        // SQL keywords that indicate a query
        private static readonly string[] SqlKeywords = new[]
        {
            "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER",
            "EXEC", "EXECUTE", "MERGE", "TRUNCATE", "FROM", "WHERE", "JOIN"
        };

        // Methods that are safe sources (not user input)
        private static readonly string[] SafeSources = new[]
        {
            "ToString", "nameof", "GetType", "typeof"
        };

        protected override void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            switch (context.Node)
            {
                case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                    AnalyzeStringConcatenation(context, binary);
                    break;
                case InterpolatedStringExpressionSyntax interpolated:
                    AnalyzeInterpolatedString(context, interpolated);
                    break;
                case InvocationExpressionSyntax invocation:
                    AnalyzeMethodInvocation(context, invocation);
                    break;
            }
        }

        private void AnalyzeStringConcatenation(SyntaxNodeAnalysisContext context, BinaryExpressionSyntax binary)
        {
            // Check if this is SQL string concatenation
            if (!IsSqlRelated(binary))
                return;

            // Check if concatenating with potentially unsafe input
            if (ContainsUnsafeInput(context, binary.Right) || ContainsUnsafeInput(context, binary.Left))
            {
                ReportDiagnostic(context, binary, Rule,
                    "SQL string concatenation with potentially unsafe input");
            }
        }

        private void AnalyzeInterpolatedString(SyntaxNodeAnalysisContext context, InterpolatedStringExpressionSyntax interpolated)
        {
            // Check if this is SQL string
            var contents = interpolated.Contents.ToString();
            if (!ContainsSqlKeywords(contents))
                return;

            // Check for interpolated expressions that might be user input
            foreach (var content in interpolated.Contents)
            {
                if (content is InterpolationSyntax interpolation)
                {
                    if (ContainsUnsafeInput(context, interpolation.Expression))
                    {
                        ReportDiagnostic(context, interpolated, Rule,
                            "SQL interpolated string with potentially unsafe input");
                        return;
                    }
                }
            }
        }

        private void AnalyzeMethodInvocation(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol method)
                return;

            // Check if this is a SQL execution method
            if (!SqlExecuteMethods.Contains(method.Name))
                return;

            // Analyze the SQL command argument (usually first parameter)
            if (invocation.ArgumentList?.Arguments.Count > 0)
            {
                var sqlArg = invocation.ArgumentList.Arguments[0];
                AnalyzeSqlArgument(context, sqlArg.Expression, invocation);
            }
        }

        private void AnalyzeSqlArgument(SyntaxNodeAnalysisContext context, ExpressionSyntax sqlExpression, InvocationExpressionSyntax invocation)
        {
            switch (sqlExpression)
            {
                // Direct string concatenation
                case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                    if (ContainsUnsafeInput(context, binary))
                    {
                        ReportDiagnostic(context, invocation, Rule,
                            "SQL query built with string concatenation");
                    }
                    break;

                // Interpolated strings
                case InterpolatedStringExpressionSyntax interpolated:
                    foreach (var content in interpolated.Contents)
                    {
                        if (content is InterpolationSyntax interpolation &&
                            ContainsUnsafeInput(context, interpolation.Expression))
                        {
                            ReportDiagnostic(context, invocation, Rule,
                                "SQL query built with string interpolation");
                            return;
                        }
                    }
                    break;

                // Method calls that build SQL
                case InvocationExpressionSyntax methodCall:
                    var methodSymbol = context.SemanticModel.GetSymbolInfo(methodCall).Symbol as IMethodSymbol;
                    if (methodSymbol != null)
                    {
                        // Check for string.Format, string.Concat, StringBuilder.Append, etc.
                        if (IsStringBuildingMethod(methodSymbol) && 
                            HasUnsafeArguments(context, methodCall))
                        {
                            ReportDiagnostic(context, invocation, Rule,
                                $"SQL query built using {methodSymbol.Name}");
                        }
                    }
                    break;

                // Variable references - trace back to definition
                case IdentifierNameSyntax identifier:
                    var symbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;
                    if (symbol is ILocalSymbol || symbol is IParameterSymbol || symbol is IFieldSymbol)
                    {
                        // Try to find where this variable is assigned
                        var dataFlow = context.SemanticModel.AnalyzeDataFlow(invocation);
                        if (dataFlow.Succeeded)
                        {
                            // This is a simplified check - in practice would need more sophisticated analysis
                            var variableName = identifier.Identifier.Text;
                            if (variableName.Contains("input", System.StringComparison.OrdinalIgnoreCase) ||
                                variableName.Contains("user", System.StringComparison.OrdinalIgnoreCase) ||
                                variableName.Contains("param", System.StringComparison.OrdinalIgnoreCase))
                            {
                                ReportDiagnostic(context, invocation, Rule,
                                    "SQL query possibly contains user input");
                            }
                        }
                    }
                    break;
            }
        }

        private bool IsSqlRelated(SyntaxNode node)
        {
            var text = node.ToString();
            
            // Check for SQL keywords
            if (ContainsSqlKeywords(text))
                return true;

            // Check if in a method/class related to database operations
            var containingMethod = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (containingMethod != null)
            {
                var methodName = containingMethod.Identifier.Text;
                if (methodName.Contains("Sql", System.StringComparison.OrdinalIgnoreCase) ||
                    methodName.Contains("Query", System.StringComparison.OrdinalIgnoreCase) ||
                    methodName.Contains("Database", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            var containingClass = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (containingClass != null)
            {
                var className = containingClass.Identifier.Text;
                if (className.Contains("Repository", System.StringComparison.OrdinalIgnoreCase) ||
                    className.Contains("DataAccess", System.StringComparison.OrdinalIgnoreCase) ||
                    className.Contains("Dal", System.StringComparison.OrdinalIgnoreCase) ||
                    className.Contains("Dao", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private bool ContainsSqlKeywords(string text)
        {
            var upperText = text.ToUpperInvariant();
            return SqlKeywords.Any(keyword => upperText.Contains(keyword));
        }

        private bool ContainsUnsafeInput(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        {
            switch (expression)
            {
                // Parameter access (could be user input)
                case MemberAccessExpressionSyntax memberAccess:
                    var symbol = context.SemanticModel.GetSymbolInfo(memberAccess).Symbol;
                    if (symbol is IParameterSymbol || 
                        (symbol is IPropertySymbol prop && IsUserInputProperty(prop)))
                        return true;
                    break;

                // Method calls that might return user input
                case InvocationExpressionSyntax invocation:
                    var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (methodSymbol != null && IsUserInputMethod(methodSymbol))
                        return true;
                    break;

                // Array/indexer access (Request["param"])
                case ElementAccessExpressionSyntax elementAccess:
                    var elementSymbol = context.SemanticModel.GetSymbolInfo(elementAccess.Expression).Symbol;
                    if (elementSymbol != null && IsUserInputSource(elementSymbol))
                        return true;
                    break;

                // Identifiers that suggest user input
                case IdentifierNameSyntax identifier:
                    var name = identifier.Identifier.Text.ToLowerInvariant();
                    if (name.Contains("input") || name.Contains("user") || 
                        name.Contains("request") || name.Contains("query"))
                        return true;
                    break;

                // Binary expressions - check both sides
                case BinaryExpressionSyntax binary:
                    return ContainsUnsafeInput(context, binary.Left) || 
                           ContainsUnsafeInput(context, binary.Right);

                // String literals and constants are safe
                case LiteralExpressionSyntax:
                case ConstantPatternSyntax:
                    return false;
            }

            // Default to unsafe for unknown expressions
            return true;
        }

        private bool IsUserInputProperty(IPropertySymbol property)
        {
            var propName = property.Name;
            var typeName = property.ContainingType?.Name ?? "";

            // Common web framework input properties
            if ((typeName.Contains("Request") || typeName.Contains("HttpContext")) &&
                (propName == "QueryString" || propName == "Form" || propName == "Headers" || 
                 propName == "Cookies" || propName == "Params"))
                return true;

            // Common input property names
            return propName.Contains("Input", System.StringComparison.OrdinalIgnoreCase) ||
                   propName.Contains("UserInput", System.StringComparison.OrdinalIgnoreCase) ||
                   propName.Contains("Query", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsUserInputMethod(IMethodSymbol method)
        {
            var methodName = method.Name;
            var typeName = method.ContainingType?.Name ?? "";

            // Safe methods
            if (SafeSources.Contains(methodName))
                return false;

            // Console/File input
            if (typeName == "Console" && (methodName == "ReadLine" || methodName == "Read"))
                return true;

            // Common input methods
            return methodName.Contains("GetInput", System.StringComparison.OrdinalIgnoreCase) ||
                   methodName.Contains("GetUserInput", System.StringComparison.OrdinalIgnoreCase) ||
                   methodName.Contains("ReadInput", System.StringComparison.OrdinalIgnoreCase) ||
                   methodName.Contains("GetParameter", System.StringComparison.OrdinalIgnoreCase) ||
                   methodName.Contains("GetQueryString", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsUserInputSource(ISymbol symbol)
        {
            var name = symbol.Name;
            var typeName = symbol.ContainingType?.Name ?? "";

            // HttpRequest indexers
            return (typeName.Contains("Request") || typeName.Contains("HttpContext")) &&
                   (name == "Item" || name == "get_Item");
        }

        private bool IsStringBuildingMethod(IMethodSymbol method)
        {
            var methodName = method.Name;
            var typeName = method.ContainingType?.Name ?? "";

            return (typeName == "String" && (methodName == "Format" || methodName == "Concat" || methodName == "Join")) ||
                   (typeName == "StringBuilder" && (methodName == "Append" || methodName == "AppendFormat" || methodName == "AppendLine"));
        }

        private bool HasUnsafeArguments(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
        {
            if (invocation.ArgumentList == null)
                return false;

            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                if (ContainsUnsafeInput(context, arg.Expression))
                    return true;
            }

            return false;
        }
    }
}