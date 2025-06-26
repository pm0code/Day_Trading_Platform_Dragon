using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using TradingPlatform.CodeAnalysis.Framework;
using TradingPlatform.CodeAnalysis.Diagnostics;

namespace TradingPlatform.CodeAnalysis.Analyzers.Security
{
    /// <summary>
    /// Analyzer that detects hardcoded secrets, API keys, passwords, and other sensitive data
    /// in the codebase to prevent security breaches.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SecretLeakageAnalyzer : TradingPlatformAnalyzerBase
    {
        protected override DiagnosticDescriptor Rule => DiagnosticDescriptors.NoHardcodedSecrets;

        protected override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest =>
            ImmutableArray.Create(
                SyntaxKind.StringLiteralExpression,
                SyntaxKind.InterpolatedStringText,
                SyntaxKind.FieldDeclaration,
                SyntaxKind.PropertyDeclaration,
                SyntaxKind.LocalDeclarationStatement,
                SyntaxKind.VariableDeclaration);

        // Patterns that indicate potential secrets
        private static readonly Regex[] SecretPatterns = new[]
        {
            // API Keys
            new Regex(@"['\""](AIza[0-9A-Za-z-_]{35})['\""]", RegexOptions.Compiled), // Google API
            new Regex(@"['\""](sk-[a-zA-Z0-9]{32,})['\""]", RegexOptions.Compiled), // OpenAI
            new Regex(@"['\""]([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})['\""]", RegexOptions.Compiled), // UUID
            new Regex(@"['\""](xox[baprs]-[0-9a-zA-Z]{10,})['\""]", RegexOptions.Compiled), // Slack
            new Regex(@"['\""]([A-Z0-9]{20})['\""]", RegexOptions.Compiled), // AWS Access Key
            new Regex(@"['\""]([a-zA-Z0-9/+=]{40})['\""]", RegexOptions.Compiled), // AWS Secret Key
            
            // Passwords
            new Regex(@"password\s*[:=]\s*['\""](.*?)['\""]", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"pwd\s*[:=]\s*['\""](.*?)['\""]", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"passwd\s*[:=]\s*['\""](.*?)['\""]", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            
            // Connection Strings with embedded credentials
            new Regex(@"(mongodb|postgres|mysql|mssql|redis)://[^:]+:([^@]+)@", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Data Source=.*;Password=([^;]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Server=.*;Pwd=([^;]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            
            // Private Keys
            new Regex(@"-----BEGIN (RSA |EC )?PRIVATE KEY-----", RegexOptions.Compiled),
            new Regex(@"-----BEGIN OPENSSH PRIVATE KEY-----", RegexOptions.Compiled),
            
            // Tokens
            new Regex(@"bearer\s+[a-zA-Z0-9\-._~+/]+=*", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"token\s*[:=]\s*['\""](.*?)['\""]", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            
            // Trading Platform Specific
            new Regex(@"['\""](alpha_[a-zA-Z0-9]{16})['\""]", RegexOptions.Compiled), // AlphaVantage
            new Regex(@"['\""](finnhub_[a-zA-Z0-9]{20})['\""]", RegexOptions.Compiled), // Finnhub
        };

        // Variable names that often contain secrets
        private static readonly string[] SuspiciousVariableNames = new[]
        {
            "apikey", "api_key", "apiKey",
            "secret", "secretkey", "secret_key", "secretKey",
            "password", "passwd", "pwd",
            "token", "authtoken", "auth_token", "authToken",
            "credential", "credentials",
            "privatekey", "private_key", "privateKey",
            "clientsecret", "client_secret", "clientSecret",
            "accesskey", "access_key", "accessKey",
            "connectionstring", "connection_string", "connectionString"
        };

        protected override void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            switch (context.Node)
            {
                case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                    AnalyzeStringLiteral(context, literal);
                    break;
                case InterpolatedStringTextSyntax interpolated:
                    AnalyzeInterpolatedString(context, interpolated);
                    break;
                case FieldDeclarationSyntax field:
                    AnalyzeFieldDeclaration(context, field);
                    break;
                case PropertyDeclarationSyntax property:
                    AnalyzePropertyDeclaration(context, property);
                    break;
                case LocalDeclarationStatementSyntax localDecl:
                    AnalyzeLocalDeclaration(context, localDecl);
                    break;
            }
        }

        private void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context, LiteralExpressionSyntax literal)
        {
            var value = literal.Token.ValueText;
            if (string.IsNullOrWhiteSpace(value) || value.Length < 8)
                return;

            // Check against secret patterns
            foreach (var pattern in SecretPatterns)
            {
                if (pattern.IsMatch(value))
                {
                    // Check if it's a safe context (e.g., test file, example)
                    if (!IsSafeContext(context, literal))
                    {
                        ReportDiagnostic(context, literal, Rule,
                            "potential secret or API key");
                    }
                    return;
                }
            }

            // Check for high entropy strings that might be secrets
            if (HasHighEntropy(value) && value.Length > 20)
            {
                var parent = literal.Parent;
                if (parent is AssignmentExpressionSyntax assignment)
                {
                    var left = assignment.Left.ToString().ToLowerInvariant();
                    if (SuspiciousVariableNames.Any(name => left.Contains(name)))
                    {
                        ReportDiagnostic(context, literal, Rule,
                            "high-entropy string assigned to suspicious variable name");
                    }
                }
            }
        }

        private void AnalyzeInterpolatedString(SyntaxNodeAnalysisContext context, InterpolatedStringTextSyntax interpolated)
        {
            var text = interpolated.TextToken.ValueText;
            if (string.IsNullOrWhiteSpace(text))
                return;

            // Check for patterns in interpolated strings
            foreach (var pattern in SecretPatterns)
            {
                if (pattern.IsMatch(text))
                {
                    if (!IsSafeContext(context, interpolated))
                    {
                        ReportDiagnostic(context, interpolated, Rule,
                            "potential secret in interpolated string");
                    }
                    return;
                }
            }
        }

        private void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context, FieldDeclarationSyntax field)
        {
            foreach (var variable in field.Declaration.Variables)
            {
                var name = variable.Identifier.Text.ToLowerInvariant();
                
                // Check if field name suggests it contains a secret
                if (SuspiciousVariableNames.Any(suspicious => name.Contains(suspicious)))
                {
                    // Check if it has a hardcoded initializer
                    if (variable.Initializer?.Value is LiteralExpressionSyntax literal &&
                        literal.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        var value = literal.Token.ValueText;
                        if (!string.IsNullOrWhiteSpace(value) && value.Length > 4 &&
                            !IsPlaceholder(value))
                        {
                            ReportDiagnostic(context, literal, Rule,
                                $"hardcoded value in field '{variable.Identifier.Text}'");
                        }
                    }
                }
            }
        }

        private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax property)
        {
            var name = property.Identifier.Text.ToLowerInvariant();
            
            // Check if property name suggests it contains a secret
            if (SuspiciousVariableNames.Any(suspicious => name.Contains(suspicious)))
            {
                // Check for hardcoded getter
                if (property.ExpressionBody?.Expression is LiteralExpressionSyntax literal &&
                    literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    var value = literal.Token.ValueText;
                    if (!string.IsNullOrWhiteSpace(value) && value.Length > 4 &&
                        !IsPlaceholder(value))
                    {
                        ReportDiagnostic(context, literal, Rule,
                            $"hardcoded value in property '{property.Identifier.Text}'");
                    }
                }

                // Check for hardcoded initializer
                if (property.Initializer?.Value is LiteralExpressionSyntax initLiteral &&
                    initLiteral.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    var value = initLiteral.Token.ValueText;
                    if (!string.IsNullOrWhiteSpace(value) && value.Length > 4 &&
                        !IsPlaceholder(value))
                    {
                        ReportDiagnostic(context, initLiteral, Rule,
                            $"hardcoded value in property '{property.Identifier.Text}'");
                    }
                }
            }
        }

        private void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context, LocalDeclarationStatementSyntax localDecl)
        {
            foreach (var variable in localDecl.Declaration.Variables)
            {
                var name = variable.Identifier.Text.ToLowerInvariant();
                
                // Check if variable name suggests it contains a secret
                if (SuspiciousVariableNames.Any(suspicious => name.Contains(suspicious)))
                {
                    // Check if it has a hardcoded initializer
                    if (variable.Initializer?.Value is LiteralExpressionSyntax literal &&
                        literal.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        var value = literal.Token.ValueText;
                        if (!string.IsNullOrWhiteSpace(value) && value.Length > 4 &&
                            !IsPlaceholder(value))
                        {
                            ReportDiagnostic(context, literal, Rule,
                                $"hardcoded value in variable '{variable.Identifier.Text}'");
                        }
                    }
                }
            }
        }

        private bool IsSafeContext(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            // Check if in test file
            if (IsInTestProject(context))
                return true;

            // Check if in example or documentation file
            var filePath = node.SyntaxTree.FilePath;
            if (filePath.Contains("Example", System.StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("Sample", System.StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("Demo", System.StringComparison.OrdinalIgnoreCase))
                return true;

            // Check if surrounded by configuration reading code
            var parent = node.Parent;
            while (parent != null && !(parent is MethodDeclarationSyntax))
            {
                if (parent.ToString().Contains("Configuration") ||
                    parent.ToString().Contains("GetEnvironmentVariable") ||
                    parent.ToString().Contains("ConfigurationManager"))
                    return true;

                parent = parent.Parent;
            }

            return false;
        }

        private bool IsPlaceholder(string value)
        {
            var placeholders = new[]
            {
                "your-api-key-here",
                "your_api_key",
                "YOUR_API_KEY",
                "<api-key>",
                "[API_KEY]",
                "{API_KEY}",
                "xxx",
                "XXX",
                "placeholder",
                "PLACEHOLDER",
                "changeme",
                "CHANGEME",
                "todo",
                "TODO"
            };

            return placeholders.Any(p => value.Contains(p, System.StringComparison.OrdinalIgnoreCase));
        }

        private bool HasHighEntropy(string str)
        {
            if (str.Length < 10)
                return false;

            // Simple entropy calculation
            var charCounts = new int[256];
            foreach (char c in str)
            {
                if (c < 256)
                    charCounts[c]++;
            }

            double entropy = 0;
            double len = str.Length;
            foreach (var count in charCounts)
            {
                if (count == 0)
                    continue;

                double freq = count / len;
                entropy -= freq * System.Math.Log(freq, 2);
            }

            // High entropy threshold (typical for random strings)
            return entropy > 4.5;
        }
    }
}