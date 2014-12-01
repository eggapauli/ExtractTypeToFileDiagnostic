﻿using ExtractTypeToFileDiagnostic.Test.Verifiers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
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
            var document = GetSampleProject().AddDocument("Class1.cs", "");

            var diagnostics = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, document);

            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics, analyzer, new DiagnosticResult[0]);
        }

        [Test]
        public async Task ShouldProduceDiagnosticWhenTypeNameDoesNotMatchFileName()
        {
            var content = CreateContentWithSingleType();

            var project = GetSampleProject();
            var documentId = DocumentId.CreateNewId(project.Id);
            var document = project.Solution
                .AddDocument(documentId, "Class1.cs", content)
                .GetDocument(documentId);

            var expectedDiagnostic = new DiagnosticResult
            {
                Id = ExtractTypeToFileAnalyzer.DiagnosticId,
                Message = string.Format(ExtractTypeToFileAnalyzer.MessageFormat, "TypeName", "Class1"),
                Severity = DiagnosticSeverity.Warning,
                Locations = new[] { new DiagnosticResultLocation("Class1.cs", 11, 27) }
            };

            var analyzer = GetCSharpDiagnosticAnalyzer();
            var diagnostics = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, document);
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics, analyzer, expectedDiagnostic);

            var newDocument = project.Solution
                .AddDocument(documentId, "TypeName.cs", content)
                .GetDocument(documentId);
            var actualSolution = await CodeFixVerifier.ApplyFixAsync(GetCSharpCodeFixProvider(), document, diagnostics.Single());

            await CodeFixVerifier.VerifyFix(actualSolution, newDocument.Project.Solution);

            var diagnostics2 = await DiagnosticVerifier.GetSortedDiagnosticsFromDocumentsAsync(analyzer, newDocument);
            DiagnosticVerifier.VerifyDiagnosticResults(diagnostics2, analyzer, new DiagnosticResult[0]);
        }

        private static string CreateContentWithSingleType()
        {
            return @"
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