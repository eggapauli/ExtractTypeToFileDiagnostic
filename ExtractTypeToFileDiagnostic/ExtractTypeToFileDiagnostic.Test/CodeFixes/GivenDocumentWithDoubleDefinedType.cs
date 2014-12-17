using NUnit.Framework;

namespace ExtractTypeToFileDiagnostic.Test.CodeFixes
{
    [TestFixture]
    public class GivenDocumentWithDoubleDefinedType : DiagnosticAndCodeFixTest
    {
        private const string Content = @"using System;
using System.Linq;

namespace TestNamespace
{
    class TypeA { }

    class TypeA { }
}";

        // TODO think about what a fix should do
    }
}
