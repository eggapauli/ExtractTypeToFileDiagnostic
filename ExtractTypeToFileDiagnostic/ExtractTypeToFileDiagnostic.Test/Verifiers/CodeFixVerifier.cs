using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExtractTypeToFileDiagnostic.Test.Verifiers
{
    public static class CodeFixVerifier
    {
        public static async Task<Solution> ApplyFixAsync(CodeFixProvider codeFixProvider, Document document, Diagnostic diagnostic)
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

        public static async Task VerifyFix(Solution expectedSolution, Solution actualSolution)
        {
            var expectedProjects = expectedSolution.Projects.ToList();
            var actualProjects = actualSolution.Projects.ToList();
            Assert.AreEqual(expectedProjects.Count, actualProjects.Count);

            var zippedProjects = expectedProjects
                .OrderBy(x => x.Name)
                .Zip(actualProjects.OrderBy(x => x.Name), (expected, actual) => new { Expected = expected, Actual = actual });
            foreach (var p in zippedProjects)
            {
                Assert.AreEqual(p.Expected.Name, p.Actual.Name);

                var expectedDocuments = p.Expected.Documents.ToList();
                var actualDocuments = p.Actual.Documents.ToList();

                Assert.AreEqual(expectedDocuments.Count, actualDocuments.Count);

                var zippedDocuments = expectedDocuments
                    .OrderBy(x => x.Name)
                    .Zip(actualDocuments.OrderBy(x => x.Name), (expected, actual) => new { Expected = expected, Actual = actual });

                foreach (var d in zippedDocuments)
                {
                    Assert.AreEqual(d.Expected.Name, d.Actual.Name);

                    var expectedContent = await GetStringFromDocumentAsync(d.Expected);
                    var actualContent = await GetStringFromDocumentAsync(d.Actual);
                    Assert.AreEqual(expectedContent, actualContent);
                }
            }
        }

        private static async Task<string> GetStringFromDocumentAsync(Document document)
        {
            var simplifiedDoc = await Simplifier.ReduceAsync(document, Simplifier.Annotation);
            var root = await simplifiedDoc.GetSyntaxRootAsync();
            root = Formatter.Format(root, Formatter.Annotation, simplifiedDoc.Project.Solution.Workspace);
            return root.GetText().ToString();
        }
    }
}
