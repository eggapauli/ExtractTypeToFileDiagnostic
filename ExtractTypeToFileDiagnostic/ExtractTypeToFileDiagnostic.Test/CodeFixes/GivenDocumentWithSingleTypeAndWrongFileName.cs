using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractTypeToFileDiagnostic.Test.CodeFixes
{
    [TestFixture]
    public class GivenDocumentWithSingleTypeAndWrongFileName : DiagnosticAndCodeFixTest
    {
        private const string Content = @"using System;
using System.Linq;

namespace TestNamespace
{
    class TypeA { }
}";

        [Test]
        public async Task ShouldHaveCorrectName()
        {
            var solution = await SetupAndApplyAsync();

            solution.Projects.Single().Documents.Should().ContainSingle(d => d.Name == "TypeA.cs");
        }

        [Test]
        public async Task ShouldDeleteOldDocument()
        {
            var solution = await SetupAndApplyAsync();

            solution.Projects.Single().Documents.Should().NotContain(d => d.Name == "Class1.cs");
        }

        [Test]
        public async Task ShouldHaveSameContentThanOldDocument()
        {
            var solution = await SetupAndApplyAsync();

            var actualContent = await solution.Projects.Single().Documents.Single(d => d.Name == "TypeA.cs").GetTextAsync();
            actualContent.ToString().Should().Be(Content);
        }

        [Test]
        public async Task ShouldNotHaveDiagnostics()
        {
            var solution = await SetupAndApplyAsync();
            var document = solution.Projects.Single().Documents.Single(d => d.Name == "TypeA.cs");
            var analyzer = CreateDiagnosticAnalyzer();
            var diagnostics = await GetDiagnosticsFromDocumentsAsync(analyzer, document);
            diagnostics.Should().BeEmpty();
        }

        private async Task<Solution> SetupAndApplyAsync()
        {
            var syntaxTree = SyntaxFactory.ParseSyntaxTree(Content);

            var diagnostic = Diagnostic.Create(
                ExtractTypeToFileAnalyzer.Rule,
                Location.Create(syntaxTree, new TextSpan(Content.IndexOf("TypeA"), "TypeA".Length)),
                "TypeA",
                "Class1");

            var document = CreateSampleProject()
                .AddDocument("Class1.cs", Content);

            var codeFixProvider = CreateCodeFixProvider();
            return await ApplyFixAsync(codeFixProvider, document, diagnostic);
        }
    }
}
