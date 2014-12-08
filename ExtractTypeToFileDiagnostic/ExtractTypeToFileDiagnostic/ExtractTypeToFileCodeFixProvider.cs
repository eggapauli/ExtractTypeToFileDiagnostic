﻿using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using Microsoft.CodeAnalysis.CSharp.Extensions;

namespace ExtractTypeToFileDiagnostic
{
    [ExportCodeFixProvider("ExtractTypeToFileCodeFixProvider", LanguageNames.CSharp), Shared]
    public class ExtractTypeToFileCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> GetFixableDiagnosticIds()
        {
            return ImmutableArray.Create(ExtractTypeToFileAnalyzer.DiagnosticId);
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task ComputeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Single();

            if (DocumentContainsMultipleTypeDeclarations(root))
            {
                context.RegisterFix(
                    CodeAction.Create("Extract type to file", c => ExtractTypeAsync(context.Document, root, declaration, c)),
                    diagnostic);
            }
            else
            {
                context.RegisterFix(
                    CodeAction.Create("Rename file to match type name", c => RenameFileAsync(context.Document, root, declaration, c)),
                    diagnostic);
            }
        }

        private bool DocumentContainsMultipleTypeDeclarations(SyntaxNode root)
        {
            return root.DescendantNodes().OfType<TypeDeclarationSyntax>().Skip(1).Any();
        }

        private async Task<Solution> ExtractTypeAsync(Document document, SyntaxNode root, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

            var child = typeDecl
                .Ancestors()
                .OfType<NamespaceDeclarationSyntax>()
                .Aggregate(
                    (MemberDeclarationSyntax)typeDecl,
                    (acc, nsDecl) => nsDecl.WithMembers(SyntaxFactory.List(new MemberDeclarationSyntax[] { acc })));

            var extractedTypeRoot = ((CompilationUnitSyntax)root).WithMembers(SyntaxFactory.List(new[] { child }));

            return document
                .WithSyntaxRoot(root.RemoveNode(typeDecl, SyntaxRemoveOptions.KeepNoTrivia))
                .Project
                .AddDocument(typeSymbol.MetadataName + ".cs", extractedTypeRoot.GetText())
                .Project
                .Solution;
        }

        private async Task<Solution> RenameFileAsync(Document document, SyntaxNode root, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

            return document.Project
                .RemoveDocument(document.Id)
                .AddDocument(typeSymbol.MetadataName + ".cs", await document.GetTextAsync().ConfigureAwait(false))
                .Project
                .Solution;
        }
    }
}