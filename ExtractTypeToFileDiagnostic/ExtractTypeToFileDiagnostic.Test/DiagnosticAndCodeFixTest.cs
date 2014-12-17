using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExtractTypeToFileDiagnostic.Test
{
    public abstract class DiagnosticAndCodeFixTest
    {
        protected static Project CreateSampleProject()
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

        protected static DiagnosticAnalyzer CreateDiagnosticAnalyzer()
        {
            return new ExtractTypeToFileAnalyzer();
        }

        protected static CodeFixProvider CreateCodeFixProvider()
        {
            return new ExtractTypeToFileCodeFixProvider();
        }

        protected static async Task<IReadOnlyCollection<Diagnostic>> GetDiagnosticsFromDocumentsAsync(DiagnosticAnalyzer analyzer, Document document)
        {
            var compilation = await document.Project.GetCompilationAsync();
            var tree = await document.GetSyntaxTreeAsync();
            var driver = AnalyzerDriver.Create(compilation, ImmutableArray.Create(analyzer), null, out compilation, CancellationToken.None);
            compilation.GetDiagnostics();
            var diagnostics = (await driver.GetDiagnosticsAsync())
                .Where(d => d.Location == Location.None || d.Location.IsInMetadata || d.Location.SourceTree == tree)
                .OrderBy(d => d.Location.SourceSpan.Start)
                .ToList();
            return new ReadOnlyCollection<Diagnostic>(diagnostics);
        }

        protected static async Task<Solution> ApplyFixAsync(CodeFixProvider codeFixProvider, Document document, Diagnostic diagnostic)
        {
            var actions = new List<CodeAction>();
            var context = new CodeFixContext(document, diagnostic, (a, d) => actions.Add(a), CancellationToken.None);
            await codeFixProvider.ComputeFixesAsync(context);

            if (actions.Count > 1)
            {
                Assert.Fail("Expected only a single code action, but found {0} code actions. Use another overload of this method.", actions.Count);
            }

            var operations = await actions.Single().GetOperationsAsync(CancellationToken.None);
            return operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        }
    }
}
