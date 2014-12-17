using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractTypeToFileDiagnostic.Test.Diagnostics
{
    public class DiagnosticTest : DiagnosticAndCodeFixTest
    {
        protected string GetDiagnosticMessage(string typeName, string fileName)
        {
            return string.Format(ExtractTypeToFileAnalyzer.MessageFormat, typeName, fileName);
        }
    }
}
