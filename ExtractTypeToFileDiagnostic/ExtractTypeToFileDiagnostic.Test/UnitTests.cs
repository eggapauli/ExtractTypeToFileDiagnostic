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
            var content =
@"using System;
using System.Linq;

namespace TestNamespace
{
    class TypeA { }
}";

            var project = GetSampleProject();
            var documentId = DocumentId.CreateNewId(project.Id);
            var document = project.Solution
                .AddDocument(documentId, "Class1.cs", content)
                .GetDocument(documentId);

            var expectedDiagnostic = new DiagnosticResult
            {
                Id = ExtractTypeToFileAnalyzer.DiagnosticId,
                Message = string.Format(ExtractTypeToFileAnalyzer.MessageFormat, "TypeA", "Class1"),
                Severity = DiagnosticSeverity.Warning,
                Locations = new[] { new DiagnosticResultLocation("Class1.cs", 6, 11) }
            };

            var analyzer = GetCSharpDiagnosticAnalyzer();
            var diagnostics = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, document);
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics, analyzer, expectedDiagnostic);

            var newDocument = project.Solution
                .AddDocument(documentId, "TypeA.cs", content)
                .GetDocument(documentId);
            var actualSolution = await CodeFixVerifier.ApplyFixAsync(GetCSharpCodeFixProvider(), document, diagnostics.Single());

            await CodeFixVerifier.VerifyFix(actualSolution, newDocument.Project.Solution);

            var diagnostics2 = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, newDocument);
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics2, analyzer, new DiagnosticResult[0]);
        }

        [Test]
        public async Task ShouldNotProduceDiagnosticForNestedType()
        {
            var content =
@"using System;
using System.Linq;

namespace TestNamespace
{
    class TypeA {
        class TypeB { }
    }
}";

            var project = GetSampleProject();
            var documentId = DocumentId.CreateNewId(project.Id);
            var document = project.Solution
                .AddDocument(documentId, "TypeA.cs", content)
                .GetDocument(documentId);

            var analyzer = GetCSharpDiagnosticAnalyzer();
            var diagnostics = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, document);
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics, analyzer, new DiagnosticResult[0]);
        }

        [Test]
        public async Task ShouldExtractTypeWhenFileContainsMultipleTypes()
        {
            var content =
@"using System;
using System.Linq;

namespace TestNamespace
{
    class TypeA { }
    class TypeB { }
}";

            var project = GetSampleProject();
            var documentAId = DocumentId.CreateNewId(project.Id);
            var document = project.Solution
                .AddDocument(documentAId, "TypeA.cs", content)
                .GetDocument(documentAId);

            var expectedDiagnostic = new DiagnosticResult
            {
                Id = ExtractTypeToFileAnalyzer.DiagnosticId,
                Message = string.Format(ExtractTypeToFileAnalyzer.MessageFormat, "TypeB", "TypeA"),
                Severity = DiagnosticSeverity.Warning,
                Locations = new[] { new DiagnosticResultLocation("TypeA.cs", 7, 11) }
            };

            var analyzer = GetCSharpDiagnosticAnalyzer();
            var diagnostics = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, document);
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics, analyzer, expectedDiagnostic);

            var expectedTypeAContent =
@"using System;
using System.Linq;

namespace TestNamespace
{
    class TypeA { }
}";

            var expectedTypeBContent =
@"using System;
using System.Linq;

namespace TestNamespace
{
    class TypeB { }
}";

            var documentBId = DocumentId.CreateNewId(project.Id);
            var expectedSolution = project.Solution
                .AddDocument(documentAId, "TypeA.cs", expectedTypeAContent)
                .AddDocument(documentBId, "TypeB.cs", expectedTypeBContent);
            var actualSolution = await CodeFixVerifier.ApplyFixAsync(GetCSharpCodeFixProvider(), document, diagnostics.Single());

            await CodeFixVerifier.VerifyFix(expectedSolution, actualSolution);

            var diagnostics2 = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, expectedSolution.GetDocument(documentAId));
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics2, analyzer, new DiagnosticResult[0]);
            var diagnostics3 = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, expectedSolution.GetDocument(documentBId));
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics2, analyzer, new DiagnosticResult[0]);
        }

        [Test]
        public async Task ShouldWorkWhenTypeToExtractIsInNestedNamespace()
        {
            var content =
@"using System;
using System.Linq;

namespace TestNamespace
{
    namespace SubNamespace
    {
        class TypeB { }
    }

    class TypeA { }
}";

            var project = GetSampleProject();
            var documentAId = DocumentId.CreateNewId(project.Id);
            var document = project.Solution
                .AddDocument(documentAId, "TypeA.cs", content)
                .GetDocument(documentAId);

            var expectedDiagnostic = new DiagnosticResult
            {
                Id = ExtractTypeToFileAnalyzer.DiagnosticId,
                Message = string.Format(ExtractTypeToFileAnalyzer.MessageFormat, "TypeB", "TypeA"),
                Severity = DiagnosticSeverity.Warning,
                Locations = new[] { new DiagnosticResultLocation("TypeA.cs", 8, 15) }
            };

            var analyzer = GetCSharpDiagnosticAnalyzer();
            var diagnostics = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, document);
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics, analyzer, expectedDiagnostic);

            var expectedTypeAContent =
@"using System;
using System.Linq;

namespace TestNamespace
{
    namespace SubNamespace
    {
    }

    class TypeA { }
}";

            var expectedTypeBContent =
@"using System;
using System.Linq;

namespace TestNamespace
{
    namespace SubNamespace
    {
        class TypeB { }
    }
}";

            var documentBId = DocumentId.CreateNewId(project.Id);
            var expectedSolution = project.Solution
                .AddDocument(documentAId, "TypeA.cs", expectedTypeAContent)
                .AddDocument(documentBId, "TypeB.cs", expectedTypeBContent);
            var actualSolution = await CodeFixVerifier.ApplyFixAsync(GetCSharpCodeFixProvider(), document, diagnostics.Single());

            await CodeFixVerifier.VerifyFix(expectedSolution, actualSolution);

            var diagnostics2 = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, expectedSolution.GetDocument(documentAId));
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics2, analyzer, new DiagnosticResult[0]);
            var diagnostics3 = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, expectedSolution.GetDocument(documentBId));
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics2, analyzer, new DiagnosticResult[0]);
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