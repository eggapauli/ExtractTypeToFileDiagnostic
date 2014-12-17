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
    public class GivenDocumentWithMultipleTopLevelTypes : DiagnosticAndCodeFixTest
    {
        private const string Content = @"using System;
using System.Linq;

namespace TestNamespace
{
    class TypeA { }
    class TypeB { }
}";

        [Test]
        public async Task ShouldCreateNewDocument()
        {
            var solution = await SetupAndApplyAsync();

            solution.Projects.Single().Documents.Should().ContainSingle(d => d.Name == "TypeB.cs");
        }

        [Test]
        public async Task ShouldExtractTypeToNewDocument()
        {
            var solution = await SetupAndApplyAsync();

            var content = await solution.Projects.Single().Documents.Single(d => d.Name == "TypeB.cs").GetTextAsync();
            content.ToString().Should().Be(@"using System;
using System.Linq;

namespace TestNamespace
{
    class TypeB { }
}");
        }

        [Test]
        public async Task ShouldNotRemoveOldDocument()
        {
            var solution = await SetupAndApplyAsync();

            solution.Projects.Single().Documents.Should().ContainSingle(d => d.Name == "TypeA.cs");
        }


        [Test]
        public async Task ShouldRemoveTypeFromOldDocument()
        {
            var solution = await SetupAndApplyAsync();

            var content = await solution.Projects.Single().Documents.Single(d => d.Name == "TypeA.cs").GetTextAsync();
            content.ToString().Should().Be(@"using System;
using System.Linq;

namespace TestNamespace
{
    class TypeA { }
}");
        }

        [Test]
        public async Task ShouldNotHaveDiagnostics()
        {
            var solution = await SetupAndApplyAsync();
            var document = solution.Projects.Single().Documents.Single(d => d.Name == "TypeB.cs");
            var analyzer = CreateDiagnosticAnalyzer();
            var diagnostics = await GetDiagnosticsFromDocumentsAsync(analyzer, document);
            diagnostics.Should().BeEmpty();
        }

        private async Task<Solution> SetupAndApplyAsync()
        {
            var syntaxTree = SyntaxFactory.ParseSyntaxTree(Content);

            var diagnostic = Diagnostic.Create(
                ExtractTypeToFileAnalyzer.Rule,
                Location.Create(syntaxTree, new TextSpan(Content.IndexOf("TypeB"), "TypeB".Length)),
                "TypeB",
                "TypeA");

            var document = CreateSampleProject()
                .AddDocument("TypeA.cs", Content);

            var codeFixProvider = CreateCodeFixProvider();
            return await ApplyFixAsync(codeFixProvider, document, diagnostic);
        }
    }
}
