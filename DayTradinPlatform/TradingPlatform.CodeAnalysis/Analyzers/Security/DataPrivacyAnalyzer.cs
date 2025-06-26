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
    /// Analyzer that enforces data privacy best practices including proper handling of PII,
    /// encryption of sensitive data, and compliance with privacy regulations.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DataPrivacyAnalyzer : TradingPlatformAnalyzerBase
    {
        protected override DiagnosticDescriptor Rule => DiagnosticDescriptors.ProtectPII;

        protected override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest =>
            ImmutableArray.Create(
                SyntaxKind.ClassDeclaration,
                SyntaxKind.PropertyDeclaration,
                SyntaxKind.FieldDeclaration,
                SyntaxKind.InvocationExpression,
                SyntaxKind.ObjectCreationExpression);

        // PII field names that require protection
        private static readonly string[] PiiFieldNames = new[]
        {
            "ssn", "socialsecuritynumber", "social_security_number",
            "creditcard", "credit_card", "creditcardnumber", "credit_card_number",
            "bankaccount", "bank_account", "accountnumber", "account_number",
            "routingnumber", "routing_number",
            "driverslicense", "drivers_license", "license_number",
            "passport", "passportnumber", "passport_number",
            "taxid", "tax_id", "ein", "tin",
            "dateofbirth", "date_of_birth", "dob", "birthdate",
            "email", "emailaddress", "email_address",
            "phone", "phonenumber", "phone_number", "mobile",
            "address", "homeaddress", "home_address", "mailingaddress",
            "salary", "income", "compensation",
            "medicalrecord", "medical_record", "healthinfo", "health_info"
        };

        // Trading-specific sensitive data
        private static readonly string[] TradingSensitiveData = new[]
        {
            "apikey", "api_key", "secretkey", "secret_key",
            "accountid", "account_id", "tradingaccount", "trading_account",
            "portfolio", "positions", "holdings",
            "balance", "equity", "margin",
            "strategy", "algorithm", "signal"
        };

        // Logging/output methods that might leak data
        private static readonly string[] LoggingMethods = new[]
        {
            "Log", "LogInformation", "LogDebug", "LogTrace", "LogWarning", "LogError",
            "WriteLine", "Write", "Print", "Debug", "Trace",
            "ToString", "ToJson", "Serialize"
        };

        protected override void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            switch (context.Node)
            {
                case ClassDeclarationSyntax classDecl:
                    AnalyzeClass(context, classDecl);
                    break;
                case PropertyDeclarationSyntax property:
                    AnalyzeProperty(context, property);
                    break;
                case FieldDeclarationSyntax field:
                    AnalyzeField(context, field);
                    break;
                case InvocationExpressionSyntax invocation:
                    AnalyzeMethodInvocation(context, invocation);
                    break;
                case ObjectCreationExpressionSyntax objectCreation:
                    AnalyzeObjectCreation(context, objectCreation);
                    break;
            }
        }

        private void AnalyzeClass(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDecl)
        {
            var className = classDecl.Identifier.Text.ToLowerInvariant();
            
            // Check if class contains sensitive data
            if (ContainsSensitiveDataName(className))
            {
                // Check if class has proper security attributes
                var hasSecurityAttribute = classDecl.AttributeLists
                    .SelectMany(list => list.Attributes)
                    .Any(attr => IsSecurityAttribute(attr));

                if (!hasSecurityAttribute)
                {
                    // Check if it implements proper interfaces
                    var implementsSecureInterface = false;
                    if (classDecl.BaseList != null)
                    {
                        foreach (var baseType in classDecl.BaseList.Types)
                        {
                            var typeSymbol = context.SemanticModel.GetSymbolInfo(baseType.Type).Symbol as ITypeSymbol;
                            if (typeSymbol != null && IsSecureInterface(typeSymbol))
                            {
                                implementsSecureInterface = true;
                                break;
                            }
                        }
                    }

                    if (!implementsSecureInterface)
                    {
                        ReportDiagnostic(context, classDecl.Identifier, Rule,
                            $"Class '{classDecl.Identifier.Text}' appears to contain sensitive data but lacks security attributes");
                    }
                }
            }
        }

        private void AnalyzeProperty(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax property)
        {
            var propertyName = property.Identifier.Text.ToLowerInvariant();
            
            if (IsSensitiveField(propertyName))
            {
                var propertySymbol = context.SemanticModel.GetDeclaredSymbol(property);
                if (propertySymbol == null) return;

                // Check for public exposure of sensitive data
                if (propertySymbol.DeclaredAccessibility == Accessibility.Public)
                {
                    // Check if it has security attributes
                    if (!HasSecurityAttribute(propertySymbol))
                    {
                        ReportDiagnostic(context, property.Identifier, Rule,
                            $"Sensitive property '{property.Identifier.Text}' is publicly exposed without encryption");
                    }
                }

                // Check for direct storage without encryption
                if (property.ExpressionBody != null || 
                    (property.AccessorList?.Accessors.Any(a => a.Body != null || a.ExpressionBody != null) ?? false))
                {
                    // Look for encryption in the implementation
                    var usesEncryption = CheckForEncryption(property);
                    if (!usesEncryption)
                    {
                        ReportDiagnostic(context, property.Identifier, Rule,
                            $"Sensitive property '{property.Identifier.Text}' stored without encryption");
                    }
                }
            }
        }

        private void AnalyzeField(SyntaxNodeAnalysisContext context, FieldDeclarationSyntax field)
        {
            foreach (var variable in field.Declaration.Variables)
            {
                var fieldName = variable.Identifier.Text.ToLowerInvariant();
                
                if (IsSensitiveField(fieldName))
                {
                    var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                    if (fieldSymbol == null) continue;

                    // Check accessibility
                    if (fieldSymbol.DeclaredAccessibility != Accessibility.Private)
                    {
                        ReportDiagnostic(context, variable.Identifier, Rule,
                            $"Sensitive field '{variable.Identifier.Text}' should be private");
                    }

                    // Check if field type suggests unencrypted storage
                    if (fieldSymbol.Type.SpecialType == SpecialType.System_String)
                    {
                        if (!HasSecurityAttribute(fieldSymbol))
                        {
                            ReportDiagnostic(context, variable.Identifier, Rule,
                                $"Sensitive field '{variable.Identifier.Text}' stored as plain string");
                        }
                    }
                }
            }
        }

        private void AnalyzeMethodInvocation(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol method)
                return;

            // Check for logging of sensitive data
            if (IsLoggingMethod(method))
            {
                // Check arguments for sensitive data
                foreach (var arg in invocation.ArgumentList?.Arguments ?? Enumerable.Empty<ArgumentSyntax>())
                {
                    if (ContainsSensitiveData(context, arg.Expression))
                    {
                        ReportDiagnostic(context, arg, Rule,
                            $"Potential logging of sensitive data in {method.Name}");
                    }
                }
            }

            // Check for transmission of sensitive data without encryption
            if (IsNetworkMethod(method))
            {
                // Look for HTTPS/TLS usage
                var usesEncryption = CheckForSecureTransmission(context, invocation);
                if (!usesEncryption)
                {
                    foreach (var arg in invocation.ArgumentList?.Arguments ?? Enumerable.Empty<ArgumentSyntax>())
                    {
                        if (ContainsSensitiveData(context, arg.Expression))
                        {
                            ReportDiagnostic(context, invocation, Rule,
                                "Transmitting sensitive data without encryption");
                            break;
                        }
                    }
                }
            }

            // Check for file/database operations with sensitive data
            if (IsPersistenceMethod(method))
            {
                foreach (var arg in invocation.ArgumentList?.Arguments ?? Enumerable.Empty<ArgumentSyntax>())
                {
                    if (ContainsSensitiveData(context, arg.Expression))
                    {
                        // Check if data is encrypted before storage
                        if (!IsEncryptedBeforeStorage(context, invocation))
                        {
                            ReportDiagnostic(context, invocation, Rule,
                                "Persisting sensitive data without encryption");
                        }
                        break;
                    }
                }
            }
        }

        private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, ObjectCreationExpressionSyntax objectCreation)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(objectCreation);
            if (typeInfo.Type == null) return;

            // Check for creation of objects that handle sensitive data
            if (typeInfo.Type.Name.Contains("Email", System.StringComparison.OrdinalIgnoreCase) ||
                typeInfo.Type.Name.Contains("Sms", System.StringComparison.OrdinalIgnoreCase) ||
                typeInfo.Type.Name.Contains("Notification", System.StringComparison.OrdinalIgnoreCase))
            {
                // Check if sensitive data is being passed to constructor
                if (objectCreation.ArgumentList != null)
                {
                    foreach (var arg in objectCreation.ArgumentList.Arguments)
                    {
                        if (ContainsSensitiveData(context, arg.Expression))
                        {
                            ReportDiagnostic(context, objectCreation, Rule,
                                $"Creating {typeInfo.Type.Name} with potentially unmasked sensitive data");
                        }
                    }
                }
            }
        }

        private bool IsSensitiveField(string fieldName)
        {
            fieldName = fieldName.ToLowerInvariant();
            return PiiFieldNames.Any(pii => fieldName.Contains(pii)) ||
                   TradingSensitiveData.Any(sensitive => fieldName.Contains(sensitive));
        }

        private bool ContainsSensitiveDataName(string name)
        {
            name = name.ToLowerInvariant();
            return name.Contains("user") || name.Contains("customer") || 
                   name.Contains("account") || name.Contains("profile") ||
                   name.Contains("personal") || name.Contains("private") ||
                   name.Contains("confidential") || name.Contains("sensitive");
        }

        private bool ContainsSensitiveData(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        {
            switch (expression)
            {
                case MemberAccessExpressionSyntax memberAccess:
                    var memberName = memberAccess.Name.Identifier.Text.ToLowerInvariant();
                    if (IsSensitiveField(memberName))
                        return true;
                    break;

                case IdentifierNameSyntax identifier:
                    var identifierName = identifier.Identifier.Text.ToLowerInvariant();
                    if (IsSensitiveField(identifierName))
                        return true;
                    break;

                case InterpolatedStringExpressionSyntax interpolated:
                    foreach (var content in interpolated.Contents)
                    {
                        if (content is InterpolationSyntax interpolation &&
                            ContainsSensitiveData(context, interpolation.Expression))
                            return true;
                    }
                    break;

                case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                    return ContainsSensitiveData(context, binary.Left) || 
                           ContainsSensitiveData(context, binary.Right);
            }

            // Check the type of the expression
            var typeInfo = context.SemanticModel.GetTypeInfo(expression);
            if (typeInfo.Type != null && ContainsSensitiveDataName(typeInfo.Type.Name))
                return true;

            return false;
        }

        private bool IsLoggingMethod(IMethodSymbol method)
        {
            return LoggingMethods.Any(log => method.Name.Contains(log, System.StringComparison.OrdinalIgnoreCase)) ||
                   method.ContainingType?.Name.Contains("Logger", System.StringComparison.OrdinalIgnoreCase) == true ||
                   method.ContainingType?.Name.Contains("Log", System.StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool IsNetworkMethod(IMethodSymbol method)
        {
            var typeName = method.ContainingType?.Name ?? "";
            var methodName = method.Name;

            return typeName.Contains("HttpClient", System.StringComparison.OrdinalIgnoreCase) ||
                   typeName.Contains("WebClient", System.StringComparison.OrdinalIgnoreCase) ||
                   typeName.Contains("RestClient", System.StringComparison.OrdinalIgnoreCase) ||
                   methodName.Contains("Send", System.StringComparison.OrdinalIgnoreCase) ||
                   methodName.Contains("Post", System.StringComparison.OrdinalIgnoreCase) ||
                   methodName.Contains("Put", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsPersistenceMethod(IMethodSymbol method)
        {
            var methodName = method.Name;
            var typeName = method.ContainingType?.Name ?? "";

            return methodName.Contains("Save", System.StringComparison.OrdinalIgnoreCase) ||
                   methodName.Contains("Write", System.StringComparison.OrdinalIgnoreCase) ||
                   methodName.Contains("Store", System.StringComparison.OrdinalIgnoreCase) ||
                   methodName.Contains("Insert", System.StringComparison.OrdinalIgnoreCase) ||
                   methodName.Contains("Update", System.StringComparison.OrdinalIgnoreCase) ||
                   typeName.Contains("Repository", System.StringComparison.OrdinalIgnoreCase) ||
                   typeName.Contains("DbContext", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSecurityAttribute(AttributeSyntax attribute)
        {
            var name = attribute.Name.ToString();
            return name.Contains("Encrypt", System.StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Secure", System.StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Protected", System.StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Sensitive", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSecureInterface(ITypeSymbol type)
        {
            var name = type.Name;
            return name.Contains("ISecure", System.StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("IEncrypted", System.StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("IProtected", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool HasSecurityAttribute(ISymbol symbol)
        {
            return symbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name.Contains("Encrypt", System.StringComparison.OrdinalIgnoreCase) == true ||
                attr.AttributeClass?.Name.Contains("Secure", System.StringComparison.OrdinalIgnoreCase) == true ||
                attr.AttributeClass?.Name.Contains("Protected", System.StringComparison.OrdinalIgnoreCase) == true);
        }

        private bool CheckForEncryption(PropertyDeclarationSyntax property)
        {
            var propertyText = property.ToString();
            return propertyText.Contains("Encrypt", System.StringComparison.OrdinalIgnoreCase) ||
                   propertyText.Contains("Decrypt", System.StringComparison.OrdinalIgnoreCase) ||
                   propertyText.Contains("Cipher", System.StringComparison.OrdinalIgnoreCase) ||
                   propertyText.Contains("Crypto", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool CheckForSecureTransmission(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
        {
            // Look for HTTPS URLs or SSL/TLS configuration
            var invocationText = invocation.ToString();
            if (invocationText.Contains("https://", System.StringComparison.OrdinalIgnoreCase) ||
                invocationText.Contains("ssl", System.StringComparison.OrdinalIgnoreCase) ||
                invocationText.Contains("tls", System.StringComparison.OrdinalIgnoreCase))
                return true;

            // Check if method is on a secure client
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression).Type;
                if (receiverType != null && 
                    (receiverType.Name.Contains("Secure", System.StringComparison.OrdinalIgnoreCase) ||
                     receiverType.Name.Contains("Https", System.StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            return false;
        }

        private bool IsEncryptedBeforeStorage(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
        {
            // Simple heuristic - check if encryption method was called nearby
            var block = invocation.FirstAncestorOrSelf<BlockSyntax>();
            if (block != null)
            {
                var blockText = block.ToString();
                return blockText.Contains("Encrypt", System.StringComparison.OrdinalIgnoreCase) ||
                       blockText.Contains("Cipher", System.StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}