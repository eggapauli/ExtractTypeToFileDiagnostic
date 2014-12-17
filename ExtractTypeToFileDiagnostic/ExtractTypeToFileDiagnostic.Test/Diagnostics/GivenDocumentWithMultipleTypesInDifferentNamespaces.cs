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
    public class GivenDocumentWithMultipleTypesInDifferentNamespaces : DiagnosticTest
    {
        private const string Content = @"using System;
using System.Linq;

namespace TestNamespace
{
    namespace SubNamespace
    {
        class TypeB { }
    }

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

            var expectedSpan = new FileLinePositionSpan("TypeA.cs", new LinePosition(7, 14), new LinePosition(7, 19));
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

            diagnostics.All(d => d.GetMessage() == GetDiagnosticMessage("TypeB", "TypeA")).Should().BeTrue();
        }

        private async Task<IEnumerable<Diagnostic>> SetupAndGetDiagnosticsAsync()
        {
            var syntaxTree = SyntaxFactory.ParseSyntaxTree(Content);

            var document = CreateSampleProject()
                .AddDocument("TypeA.cs", Content);

            var analyzer = CreateDiagnosticAnalyzer();
            return await GetDiagnosticsFromDocumentsAsync(analyzer, document);
        }
    }
}
