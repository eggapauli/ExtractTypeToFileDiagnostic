using ExtractTypeToFileDiagnostic.Test.Verifiers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using TestHelper;

namespace ExtractTypeToFileDiagnostic.Test
{
    [TestFixture]
    public class UnitTest
    {
        [Test]
        public async Task ShouldNotProduceDiagnosticsWhenFileIsEmpty()
        {
            var analyzer = GetCSharpDiagnosticAnalyzer();
            var document = GetSampleProject().AddDocument("Class1.cs", string.Empty);

            var diagnostics = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, document);

            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics, analyzer, new DiagnosticResult[0]);
        }

        [Test]
        public async Task ShouldRenameFileWhenTypeNameDoesNotMatchFileName()
        {
            var content = CreateSampleContent();

            var project = GetSampleProject();
            var documentId = DocumentId.CreateNewId(project.Id);
            var document = project.Solution
                .AddDocument(documentId, "Class1.cs", content.GetText())
                .GetDocument(documentId);

            var expectedDiagnostic = new DiagnosticResult
            {
                Id = ExtractTypeToFileAnalyzer.DiagnosticId,
                Message = string.Format(ExtractTypeToFileAnalyzer.MessageFormat, "TypeA", "Class1"),
                Severity = DiagnosticSeverity.Warning,
                Locations = new[] { new DiagnosticResultLocation("Class1.cs", 11, 11) }
            };

            var analyzer = GetCSharpDiagnosticAnalyzer();
            var diagnostics = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, document);
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics, analyzer, expectedDiagnostic);

            var newDocument = project.Solution
                .AddDocument(documentId, "TypeA.cs", content.GetText())
                .GetDocument(documentId);
            var actualSolution = await CodeFixVerifier.ApplyFixAsync(GetCSharpCodeFixProvider(), document, diagnostics.Single());

            await CodeFixVerifier.VerifyFix(actualSolution, newDocument.Project.Solution);

            var diagnostics2 = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, newDocument);
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics2, analyzer, new DiagnosticResult[0]);
        }

        [Test]
        public async Task ShouldNotProduceDiagnosticForNestedType()
        {
            var content = SyntaxFactory.CompilationUnit()
                .AddMembers(SyntaxFactory.ClassDeclaration("TypeA")
                    .AddMembers(SyntaxFactory.ClassDeclaration("TypeB")));

            var project = GetSampleProject();
            var documentId = DocumentId.CreateNewId(project.Id);
            var document = project.Solution
                .AddDocument(documentId, "TypeA.cs", content.GetText())
                .GetDocument(documentId);

            var analyzer = GetCSharpDiagnosticAnalyzer();
            var diagnostics = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, document);
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics, analyzer, new DiagnosticResult[0]);
        }

        [Test]
        public async Task ShouldExtractTypeWhenFileContainsMultipleTypes()
        {
            var content = (CompilationUnitSyntax)CreateSampleContent();
            content = content.WithMembers(
                SyntaxFactory.List(new MemberDeclarationSyntax[] {
                    content
                    .ChildNodes()
                    .OfType<NamespaceDeclarationSyntax>()
                    .Single()
                    .AddMembers(content
                        .DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                        .Single()
                        .WithIdentifier(SyntaxFactory.ParseToken("TypeB")))
                }));

            var project = GetSampleProject();
            var documentAId = DocumentId.CreateNewId(project.Id);
            var document = project.Solution
                .AddDocument(documentAId, "TypeA.cs", content.GetText())
                .GetDocument(documentAId);

            var expectedDiagnostic = new DiagnosticResult
            {
                Id = ExtractTypeToFileAnalyzer.DiagnosticId,
                Message = string.Format(ExtractTypeToFileAnalyzer.MessageFormat, "TypeB", "TypeA"),
                Severity = DiagnosticSeverity.Warning,
                Locations = new[] { new DiagnosticResultLocation("TypeA.cs", 12, 11) }
            };

            var analyzer = GetCSharpDiagnosticAnalyzer();
            var diagnostics = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, document);
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics, analyzer, expectedDiagnostic);

            var extractedContent = (CompilationUnitSyntax)CreateSampleContent();
            extractedContent = extractedContent.WithMembers(
                SyntaxFactory.List(new MemberDeclarationSyntax[] {
                    extractedContent
                    .ChildNodes()
                    .OfType<NamespaceDeclarationSyntax>()
                    .Single()
                    .WithMembers(
                        SyntaxFactory.List(new MemberDeclarationSyntax[] {
                            extractedContent
                            .DescendantNodes()
                            .OfType<ClassDeclarationSyntax>()
                            .Single()
                            .WithIdentifier(SyntaxFactory.ParseToken("TypeB"))
                        }))
                }));

            var documentBId = DocumentId.CreateNewId(project.Id);
            var expectedSolution = project.Solution
                .AddDocument(documentAId, "TypeA.cs", CreateSampleContent().GetText())
                .AddDocument(documentBId, "TypeB.cs", extractedContent.GetText());
            var actualSolution = await CodeFixVerifier.ApplyFixAsync(GetCSharpCodeFixProvider(), document, diagnostics.Single());

            await CodeFixVerifier.VerifyFix(expectedSolution, actualSolution, documentBId);

            var diagnostics2 = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, expectedSolution.GetDocument(documentAId));
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics2, analyzer, new DiagnosticResult[0]);
            var diagnostics3 = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, expectedSolution.GetDocument(documentBId));
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics2, analyzer, new DiagnosticResult[0]);
        }

        private static SyntaxNode CreateSampleContent()
        {
            return SyntaxFactory.ParseCompilationUnit(@"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace TestNamespace
{{
    class TypeA {{ }}
}}");
        }

        private static Project GetSampleProject()
        {
            const string projectName = "SampleProject";

            var projectId = ProjectId.CreateNewId(debugName: projectName);

            var CorlibReference = MetadataReference.CreateFromAssembly(typeof(object).Assembly);
            var SystemCoreReference = MetadataReference.CreateFromAssembly(typeof(Enumerable).Assembly);
            var CSharpSymbolsReference = MetadataReference.CreateFromAssembly(typeof(CSharpCompilation).Assembly);
            var CodeAnalysisReference = MetadataReference.CreateFromAssembly(typeof(Compilation).Assembly);

            var solution = new CustomWorkspace()
                .CurrentSolution
                .AddProject(projectId, projectName, projectName, LanguageNames.CSharp)
                .AddMetadataReference(projectId, CorlibReference)
                .AddMetadataReference(projectId, SystemCoreReference)
                .AddMetadataReference(projectId, CSharpSymbolsReference)
                .AddMetadataReference(projectId, CodeAnalysisReference);

            return solution.GetProject(projectId);
        }

        private static CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new ExtractTypeToFileCodeFixProvider();
        }

        private static DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ExtractTypeToFileAnalyzer();
        }
    }
}