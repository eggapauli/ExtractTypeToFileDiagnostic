using FluentAssertions;
using NUnit.Framework;
using System.Threading.Tasks;

namespace ExtractTypeToFileDiagnostic.Test.Diagnostics
{
    [TestFixture]
    public class GivenCorrectDocument : DiagnosticTest
    {
        [Test]
        public async Task ShouldNotProduceDiagnosticIfDocumentIsEmpty()
        {
            var analyzer = CreateDiagnosticAnalyzer();
            var document = CreateSampleProject().AddDocument("Class1.cs", string.Empty);

            var diagnostics = await GetDiagnosticsFromDocumentsAsync(analyzer, document);

            diagnostics.Should().BeEmpty();
        }

        [Test]
        public async Task ShouldNotProduceDiagnosticForNestedType()
        {
            var analyzer = CreateDiagnosticAnalyzer();
            var content = @"using System;
using System.Linq;

namespace TestNamespace
{
    class TypeA {
        class TypeB { }
    }
}";
            var document = CreateSampleProject().AddDocument("TypeA.cs", content);

            var diagnostics = await GetDiagnosticsFromDocumentsAsync(analyzer, document);

            diagnostics.Should().BeEmpty();
        }
    }
}
