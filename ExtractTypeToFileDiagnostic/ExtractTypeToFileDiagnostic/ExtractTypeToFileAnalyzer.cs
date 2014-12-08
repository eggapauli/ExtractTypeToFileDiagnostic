using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System;

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

            if (IsNestedType(namedTypeSymbol)) return;

            var actualTypeName = namedTypeSymbol.MetadataName;
            var diagnostics = namedTypeSymbol
                .Locations
                .Select(location => new { Location = location, ExpectedTypeName = Path.GetFileNameWithoutExtension(location.SourceTree.FilePath) })
                .Where(x => actualTypeName != x.ExpectedTypeName)
                .Select(x => Diagnostic.Create(Rule, x.Location, actualTypeName, x.ExpectedTypeName));

            foreach (var diagnostic in diagnostics)
            {
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsNestedType(INamedTypeSymbol namedTypeSymbol)
        {
            return namedTypeSymbol.ContainingType != null;
        }
    }
}
