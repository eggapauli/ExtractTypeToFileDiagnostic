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
            var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false));
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Single();

            if (GetDocument(document.Project, declaration) != null)
            {
                context.RegisterFix(
                    CodeAction.Create("Integrate type into existing file", c => MoveTypeAsync(document, root, declaration, c)),
                    diagnostic);
            }
            else if (DocumentContainsMultipleTypeDeclarations(root))
            {
                context.RegisterFix(
                    CodeAction.Create("Extract type to file", c => ExtractTypeToNewDocumentAsync(document, root, declaration, c)),
                    diagnostic);
            }
            else
            {
                context.RegisterFix(
                    CodeAction.Create("Rename file to match type name", c => RenameFileAsync(document, declaration, c)),
                    diagnostic);
            }
        }

        private Document GetDocument(Project project, TypeDeclarationSyntax typeDecl)
        {
            var typeName = typeDecl.Identifier.Text.ToLowerInvariant();
            return project.Documents
                .SingleOrDefault(d => Path.GetFileNameWithoutExtension(d.Name).ToLowerInvariant() == typeName);
        }

        private bool DocumentContainsMultipleTypeDeclarations(SyntaxNode root)
        {
            return root.DescendantNodes().OfType<TypeDeclarationSyntax>().Skip(1).Any();
        }

        private async Task<Solution> MoveTypeAsync(Document document, CompilationUnitSyntax root, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            var extractedType = await ExtractTypeAsync(document, root, typeDecl, cancellationToken).ConfigureAwait(false);

            document = await RemoveTypeAsync(typeDecl, document).ConfigureAwait(false);

            var existingDocument = GetDocument(document.Project, typeDecl);
            existingDocument = await IntegrateTypeAsync(extractedType, existingDocument).ConfigureAwait(false);

            var solution = existingDocument.Project.Solution.RemoveDocument(document.Id);
            return solution;
        }

        // TODO clean up
        private async Task<Document> IntegrateTypeAsync(SyntaxNode extractedType, Document document)
        {
            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            
            var parent = root;
            MemberDeclarationSyntax nodeToIntegrate = null;
            foreach (var nsDecl in extractedType.DescendantNodes().OfType<NamespaceDeclarationSyntax>())
            {
                var child = parent.ChildNodes().OfType<NamespaceDeclarationSyntax>().SingleOrDefault(x => x.Name.ToString() == nsDecl.Name.ToString());
                if (child == null)
                {
                    nodeToIntegrate = nsDecl;
                    break;
                }
                parent = child;
            }

            nodeToIntegrate = nodeToIntegrate ?? extractedType.DescendantNodes().OfType<TypeDeclarationSyntax>().Single();

            SyntaxNode newRoot;
            if (parent is CompilationUnitSyntax)
            {
                newRoot = ((CompilationUnitSyntax)parent).AddMembers(nodeToIntegrate);
            }
            else if (parent is NamespaceDeclarationSyntax)
            {
                newRoot = root.ReplaceNode(parent, ((NamespaceDeclarationSyntax)parent).AddMembers(nodeToIntegrate));
            }
            else
            {
                throw new NotSupportedException(string.Format("Didn't expect parent to be of type {0}", parent.GetType()));
            }

            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<Solution> ExtractTypeToNewDocumentAsync(Document document, CompilationUnitSyntax root, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            var extractedType = await ExtractTypeAsync(document, root, typeDecl, cancellationToken).ConfigureAwait(false);

            document = await RemoveTypeAsync(typeDecl, document);
            return document
                .Project
                .AddDocument(typeDecl.Identifier.Text + ".cs", extractedType.GetText())
                .Project
                .Solution;
        }

        private async Task<Solution> RenameFileAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

            return document.Project
                .RemoveDocument(document.Id)
                .AddDocument(typeSymbol.MetadataName + ".cs", await document.GetTextAsync().ConfigureAwait(false))
                .Project
                .Solution;
        }

        private async Task<Document> RemoveTypeAsync(TypeDeclarationSyntax typeDecl, Document document)
        {
            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            return document.WithSyntaxRoot(root.RemoveNode(typeDecl, SyntaxRemoveOptions.KeepNoTrivia));
        }

        private async Task<SyntaxNode> ExtractTypeAsync(Document document, CompilationUnitSyntax root, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

            var child = typeDecl
                .Ancestors()
                .OfType<NamespaceDeclarationSyntax>()
                .Aggregate(
                    (MemberDeclarationSyntax)typeDecl,
                    (acc, nsDecl) => nsDecl.WithMembers(SyntaxFactory.List(new MemberDeclarationSyntax[] { acc })));

            return root.WithMembers(SyntaxFactory.List(new[] { child }));
        }
    }
}