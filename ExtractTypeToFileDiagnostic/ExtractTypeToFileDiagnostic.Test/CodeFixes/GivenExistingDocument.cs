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
    public class GivenExistingDocument : DiagnosticAndCodeFixTest
    {
        private const string ContentClass1 = @"using System;
using System.Linq;

namespace TestNamespace
{
    class TypeA { }
}";

        private const string ContentTypeA = @"using System;
using System.Linq;

namespace TestNamespace
{
    class SomeType { }
}";

        [Test]
        public async Task ShouldNotCreateNewDocument()
        {
            var solution = await SetupAndApplyAsync();

            solution.Projects.Single().Documents.Should().ContainSingle(d => d.Name == "TypeA.cs");
        }

        private async Task<Solution> SetupAndApplyAsync()
        {
            var syntaxTree = SyntaxFactory.ParseSyntaxTree(ContentClass1);

            var diagnostic = Diagnostic.Create(
                ExtractTypeToFileAnalyzer.Rule,
                Location.Create(syntaxTree, new TextSpan(ContentClass1.IndexOf("TypeA"), "TypeA".Length)),
                "TypeA",
                "Class1");

            var document = CreateSampleProject()
                .AddDocument("TypeA.cs", ContentTypeA).Project
                .AddDocument("Class1.cs", ContentClass1);

            var codeFixProvider = CreateCodeFixProvider();
            return await ApplyFixAsync(codeFixProvider, document, diagnostic);
        }
    }
}
