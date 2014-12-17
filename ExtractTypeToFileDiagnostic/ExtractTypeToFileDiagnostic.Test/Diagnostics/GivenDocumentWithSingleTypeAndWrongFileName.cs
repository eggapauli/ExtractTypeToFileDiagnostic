using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractTypeToFileDiagnostic.Test.Diagnostics
{
    [TestFixture]
    public class GivenDocumentWithSingleTypeAndWrongFileName : DiagnosticTest
    {
        private const string Content = @"using System;
using System.Linq;

namespace TestNamespace
{
    class TypeA { }
}";

        [Test]
        public async Task ShouldCreateExactlyOneDiagnostic()
        {
            var diagnostics = await SetupAndGetDiagnosticsAsync();

            diagnostics.Should().HaveCount(1);
        }

        [Test]
        public async Task ShouldCreateDiagnosticWithCorrectLocation()
        {
            var diagnostics = await SetupAndGetDiagnosticsAsync();

            var expectedSpan = new FileLinePositionSpan("Class1.cs", new LinePosition(5, 10), new LinePosition(5, 15));
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
