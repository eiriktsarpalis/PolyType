using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using PolyType.SourceGenerator.Helpers;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class OnlySanctionedShapesAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Parser.UnsanctionedShape);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.AssemblyName is "PolyType")
            {
                // We only forbid implementations *outside* our own assembly.
                return;
            }

            INamedTypeSymbol? noImplAttribute = context.Compilation.GetTypeByMetadataName("PolyType.InternalImplementationsOnlyAttribute");
            if (noImplAttribute is null)
            {
                return;
            }

            context.RegisterSymbolStartAction(
                context =>
                {
                    INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
                    var violatingInterfaces = type.Interfaces.Where(iface => iface.HasAttribute(noImplAttribute)).ToArray();
                    if (violatingInterfaces is [])
                    {
                        return;
                    }

                    context.RegisterSyntaxNodeAction(
                        context =>
                        {
                            var baseTypeSyntax = (SimpleBaseTypeSyntax)context.Node;
                            SymbolInfo info = context.SemanticModel.GetSymbolInfo(baseTypeSyntax.Type, context.CancellationToken);
                            if (violatingInterfaces.FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(info.Symbol, i)) is { } violator)
                            {
                                // We found a violation.
                                context.ReportDiagnostic(Diagnostic.Create(
                                    Parser.UnsanctionedShape,
                                    baseTypeSyntax.GetLocation(),
                                    type.GetFullyQualifiedName(),
                                    violator.GetFullyQualifiedName()));
                            }
                        },
                        SyntaxKind.SimpleBaseType);
                },
                SymbolKind.NamedType);
        });
    }
}
