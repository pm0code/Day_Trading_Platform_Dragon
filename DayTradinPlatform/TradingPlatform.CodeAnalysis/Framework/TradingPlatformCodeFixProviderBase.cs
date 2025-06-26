using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TradingPlatform.CodeAnalysis.Framework
{
    /// <summary>
    /// Base class for all TradingPlatform code fix providers.
    /// Provides common functionality for automatic code remediation
    /// following canonical patterns and high-performance standards.
    /// </summary>
    public abstract class TradingPlatformCodeFixProviderBase : CodeFixProvider
    {
        #region Abstract Members

        /// <summary>
        /// Gets the diagnostic IDs this provider can fix.
        /// </summary>
        public abstract override ImmutableArray<string> FixableDiagnosticIds { get; }

        /// <summary>
        /// Gets the title for the code fix.
        /// </summary>
        protected abstract string CodeFixTitle { get; }

        /// <summary>
        /// Applies the fix to the document.
        /// </summary>
        protected abstract Task<Document> ApplyFixAsync(
            Document document,
            SyntaxNode root,
            SyntaxNode nodeToFix,
            CancellationToken cancellationToken);

        #endregion

        #region CodeFixProvider Implementation

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
                return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the node that triggered the diagnostic
            var nodeToFix = root.FindNode(diagnosticSpan, findInsideTrivia: true, getInnermostNodeForTie: true);
            if (nodeToFix == null)
                return;

            // Register the code fix
            var codeAction = CodeAction.Create(
                title: CodeFixTitle,
                createChangedDocument: cancellationToken => ApplyFixAsync(
                    context.Document,
                    root,
                    nodeToFix,
                    cancellationToken),
                equivalenceKey: CodeFixTitle);

            context.RegisterCodeFix(codeAction, diagnostic);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Adds a using directive if not already present.
        /// </summary>
        protected async Task<Document> AddUsingDirectiveAsync(
            Document document,
            string namespaceName,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var compilation = root as CompilationUnitSyntax;
            if (compilation == null)
                return document;

            // Check if using already exists
            var hasUsing = compilation.Usings.Any(u => u.Name.ToString() == namespaceName);
            if (hasUsing)
                return document;

            // Add the using directive
            var usingDirective = SyntaxFactory.UsingDirective(
                SyntaxFactory.ParseName(namespaceName))
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

            var newUsings = compilation.Usings.Add(usingDirective);
            var newCompilation = compilation.WithUsings(newUsings);

            return document.WithSyntaxRoot(newCompilation);
        }

        /// <summary>
        /// Replaces a type reference in the syntax tree.
        /// </summary>
        protected SyntaxNode ReplaceType(
            SyntaxNode root,
            SyntaxNode nodeToReplace,
            string newTypeName)
        {
            var newType = SyntaxFactory.ParseTypeName(newTypeName)
                .WithTriviaFrom(nodeToReplace);

            return root.ReplaceNode(nodeToReplace, newType);
        }

        /// <summary>
        /// Creates a new method with TradingResult return type.
        /// </summary>
        protected MethodDeclarationSyntax CreateTradingResultMethod(
            MethodDeclarationSyntax originalMethod,
            TypeSyntax innerReturnType)
        {
            // Determine if method is async
            var isAsync = originalMethod.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));

            // Create TradingResult<T> type
            var tradingResultType = SyntaxFactory.GenericName("TradingResult")
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList(innerReturnType)));

            // Wrap in Task if async
            TypeSyntax returnType = tradingResultType;
            if (isAsync)
            {
                returnType = SyntaxFactory.GenericName("Task")
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(tradingResultType)));
            }

            // Create new method with updated return type
            return originalMethod.WithReturnType(returnType);
        }

        /// <summary>
        /// Wraps a return statement in TradingResult.Success().
        /// </summary>
        protected StatementSyntax WrapInTradingResultSuccess(
            ReturnStatementSyntax returnStatement)
        {
            if (returnStatement.Expression == null)
            {
                // Handle void returns
                return SyntaxFactory.ReturnStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("TradingResult"),
                            SyntaxFactory.IdentifierName("Success")))
                    .WithArgumentList(SyntaxFactory.ArgumentList()));
            }

            // Wrap the existing expression
            var successCall = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("TradingResult"),
                    SyntaxFactory.IdentifierName("Success")))
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(returnStatement.Expression))));

            return SyntaxFactory.ReturnStatement(successCall)
                .WithLeadingTrivia(returnStatement.GetLeadingTrivia())
                .WithTrailingTrivia(returnStatement.GetTrailingTrivia());
        }

        /// <summary>
        /// Creates an exception handler that returns TradingResult.Failure().
        /// </summary>
        protected CatchClauseSyntax CreateTradingResultCatchClause()
        {
            return SyntaxFactory.CatchClause()
                .WithDeclaration(
                    SyntaxFactory.CatchDeclaration(
                        SyntaxFactory.IdentifierName("Exception"))
                    .WithIdentifier(SyntaxFactory.Identifier("ex")))
                .WithBlock(
                    SyntaxFactory.Block(
                        SyntaxFactory.ReturnStatement(
                            SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("TradingResult"),
                                    SyntaxFactory.IdentifierName("Failure")))
                            .WithArgumentList(
                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SeparatedList(new[]
                                    {
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                SyntaxFactory.Literal("OPERATION_FAILED"))),
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                SyntaxFactory.IdentifierName("ex"),
                                                SyntaxFactory.IdentifierName("Message"))),
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.IdentifierName("ex"))
                                    }))))));
        }

        /// <summary>
        /// Adds canonical base class inheritance.
        /// </summary>
        protected ClassDeclarationSyntax AddCanonicalBaseClass(
            ClassDeclarationSyntax classDeclaration,
            string baseClassName)
        {
            var baseType = SyntaxFactory.SimpleBaseType(
                SyntaxFactory.IdentifierName(baseClassName));

            if (classDeclaration.BaseList == null)
            {
                return classDeclaration.WithBaseList(
                    SyntaxFactory.BaseList(
                        SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(baseType)));
            }

            // Add to existing base list
            var newBaseList = classDeclaration.BaseList.AddTypes(baseType);
            return classDeclaration.WithBaseList(newBaseList);
        }

        /// <summary>
        /// Creates a constructor that calls base class constructor.
        /// </summary>
        protected ConstructorDeclarationSyntax CreateCanonicalConstructor(
            string className,
            string baseClassName)
        {
            return SyntaxFactory.ConstructorDeclaration(className)
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SeparatedList(new[]
                        {
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("logger"))
                                .WithType(SyntaxFactory.IdentifierName("ITradingLogger"))
                        })))
                .WithInitializer(
                    SyntaxFactory.ConstructorInitializer(
                        SyntaxKind.BaseConstructorInitializer,
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SeparatedList(new[]
                            {
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("logger")),
                                SyntaxFactory.Argument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal(className)))
                            }))))
                .WithBody(SyntaxFactory.Block());
        }

        /// <summary>
        /// Formats the syntax node with proper indentation.
        /// </summary>
        protected T Format<T>(T node) where T : SyntaxNode
        {
            return node.WithAdditionalAnnotations(Formatter.Annotation);
        }

        #endregion
    }
}