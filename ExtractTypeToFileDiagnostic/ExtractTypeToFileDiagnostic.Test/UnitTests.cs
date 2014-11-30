using ExtractTypeToFileDiagnostic.Test.Verifiers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
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
            var document = CreateDocument("Class1.cs", "");

            var diagnostics = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, document);

            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics, analyzer, new DiagnosticResult[0]);
        }

        [Test]
        public async Task ShouldProduceDiagnosticWhenTypeNameDoesNotMatchFileName()
        {
            var content = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
        }
    }";
            var document = CreateDocument("Class1.cs", content);

            var expectedDiagnostic = new DiagnosticResult
            {
                Id = ExtractTypeToFileAnalyzer.DiagnosticId,
                Message = string.Format(ExtractTypeToFileAnalyzer.MessageFormat, "TypeName", "Class1"),
                Severity = DiagnosticSeverity.Warning,
                Locations = new[] { new DiagnosticResultLocation("Class1.cs", 11, 15) }
            };

            var analyzer = GetCSharpDiagnosticAnalyzer();
            var diagnostics = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, document);
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics, analyzer, expectedDiagnostic);

            var newDocument = CreateDocument("TypeName.cs", content);
            var actualSolution = await CodeFixVerifier.ApplyFixAsync(GetCSharpCodeFixProvider(), document, diagnostics.Single());

            await CodeFixVerifier.VerifyFix(actualSolution, newDocument.Project.Solution);

            var diagnostics2 = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, newDocument);
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics2, analyzer, new DiagnosticResult[0]);
        }

        private static Document CreateDocument(string fileName, string source)
        {
            return GetSampleProject().AddDocument(fileName, SourceText.From(source));
        }

        private static Project GetSampleProject()
        {
            const string projectName = "ProjectName";

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