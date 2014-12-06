using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace ExtractTypeToFileDiagnostic
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ExtractTypeToFileAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ExtractTypeToFileDiagnostic";
        internal const string Title = "Type name doesn't match file name";
        internal const string MessageFormat = "Type name '{0}' doesn't match file name '{1}'";
        internal const string Category = "Naming";

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            Debug.Assert(namedTypeSymbol.Locations.Length == 1, "Didn't expect multiple symbol locations.");

            var location = namedTypeSymbol.Locations.First();

            var actualTypeName = namedTypeSymbol.MetadataName;
            var expectedTypeName = Path.GetFileNameWithoutExtension(location.SourceTree.FilePath);
            if (!actualTypeName.Equals(expectedTypeName))
            {
                var diagnostic = Diagnostic.Create(Rule, location, actualTypeName, expectedTypeName);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
