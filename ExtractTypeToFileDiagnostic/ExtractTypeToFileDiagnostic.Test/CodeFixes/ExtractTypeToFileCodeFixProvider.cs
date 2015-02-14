using System.Collections.Immutable;
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
using System.IO;

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

            var document = context.Document;
            var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Single();


            if (document.Project.Documents.Any(d => Path.GetFileNameWithoutExtension(d.Name) == declaration.Identifier.Text))
            {
                context.RegisterFix(
                    CodeAction.Create("Integrate type into existing file", c => IntegrateTypeAsync(document, root, declaration, c)),
                    diagnostic);
            }
            else if (DocumentContainsMultipleTypeDeclarations(root))
            {
                context.RegisterFix(
                    CodeAction.Create("Extract type to file", c => ExtractTypeAsync(document, root, declaration, c)),
                    diagnostic);
            }
            else
            {
                context.RegisterFix(
                    CodeAction.Create("Rename file to match type name", c => RenameFileAsync(document, root, declaration, c)),
                    diagnostic);
            }
        }

        private bool DocumentContainsMultipleTypeDeclarations(SyntaxNode root)
        {
            return root.DescendantNodes().OfType<TypeDeclarationSyntax>().Skip(1).Any();
        }

        private async Task<Solution> IntegrateTypeAsync(Document document, SyntaxNode root, TypeDeclarationSyntax declaration, CancellationToken ct)
        {
            return document.Project.Solution;
        }

        private async Task<Solution> ExtractTypeAsync(Document document, SyntaxNode root, TypeDeclarationSyntax declaration, CancellationToken ct)
        {
            var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
            var typeSymbol = semanticModel.GetDeclaredSymbol(declaration, ct);

            var child = declaration
                .Ancestors()
                .OfType<NamespaceDeclarationSyntax>()
                .Aggregate(
                    (MemberDeclarationSyntax)declaration,
                    (acc, nsDecl) => nsDecl.WithMembers(SyntaxFactory.List(new MemberDeclarationSyntax[] { acc })));

            var extractedTypeRoot = ((CompilationUnitSyntax)root).WithMembers(SyntaxFactory.List(new[] { child }));

            return document
                .WithSyntaxRoot(root.RemoveNode(declaration, SyntaxRemoveOptions.KeepNoTrivia))
                .Project
                .AddDocument(typeSymbol.MetadataName + ".cs", extractedTypeRoot.GetText())
                .Project
                .Solution;
        }

        private async Task<Solution> RenameFileAsync(Document document, SyntaxNode root, TypeDeclarationSyntax declaration, CancellationToken ct)
        {
            var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
            var typeSymbol = semanticModel.GetDeclaredSymbol(declaration, ct);

            return document.Project
                .RemoveDocument(document.Id)
                .AddDocument(typeSymbol.MetadataName + ".cs", await document.GetTextAsync().ConfigureAwait(false))
                .Project
                .Solution;
        }
    }
}