using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractTypeToFileDiagnostic.Test.Diagnostics
{
    [TestFixture]
    public class GivenDocumentWithDoubleDefinedType : DiagnosticTest
    {
        private const string Content = @"using System;
using System.Linq;

namespace TestNamespace
{
    class TypeA { }

    class TypeA { }
}";

        [Test]
        public async Task ShouldCreateExactlyTwoDiagnostics()
        {
            var diagnostics = await SetupAndGetDiagnosticsAsync();

            diagnostics.Should().HaveCount(2);
        }

        [Test]
        [TestCase(5, 10, 5, 15)]
        [TestCase(7, 10, 7, 15)]
        public async Task ShouldCreateDiagnosticWithCorrectLocation(int startLine, int startCharachter, int endLine, int endCharacter)
        {
            var diagnostics = await SetupAndGetDiagnosticsAsync();

            var expectedSpan = new FileLinePositionSpan(
                "Class1.cs",
                new LinePosition(startLine, startCharachter),
                new LinePosition(endLine, endCharacter));
            diagnostics.Should().Contain(d => d.Location.GetLineSpan().Equals(expectedSpan));
        }

        [Test]
        public async Task ShouldCreateDiagnosticWithNoAdditionalLocations()
        {
            var diagnostics = await SetupAndGetDiagnosticsAsync();

            diagnostics.Should().OnlyContain(d => d.AdditionalLocations.Count == 0);
        }

        [Test]
        public async Task ShouldCreateDiagnosticWithCorrectMessage()
        {
            var diagnostics = await SetupAndGetDiagnosticsAsync();

            diagnostics.All(d => d.GetMessage() == GetDiagnosticMessage("TypeA", "Class1")).Should().BeTrue();
        }

        private async Task<IEnumerable<Diagnostic>> SetupAndGetDiagnosticsAsync()
        {
            var syntaxTree = SyntaxFactory.ParseSyntaxTree(Content);

            var document = CreateSampleProject()
                .AddDocument("Class1.cs", Content);

            var analyzer = CreateDiagnosticAnalyzer();
            return await GetDiagnosticsFromDocumentsAsync(analyzer, document);
        }
    }
}
