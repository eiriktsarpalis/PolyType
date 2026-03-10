using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.SourceGenerator.Helpers;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

[Generator]
public sealed class PolyTypeGenerator : IIncrementalGenerator
{
    public static string SourceGeneratorName { get; } = typeof(PolyTypeGenerator).FullName;
    public static string SourceGeneratorVersion { get; } = typeof(SourceFormatter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0.0";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        DebugGuard.MaybeLaunchDebuggerOnStartup();

        IncrementalValueProvider<PolyTypeKnownSymbols> knownSymbols = context.CompilationProvider
            .Select((compilation, _) => new PolyTypeKnownSymbols(compilation));

        IncrementalValueProvider<(TypeShapeProviderModel Model, ImmutableArray<Diagnostic> Diagnostics)?> parsed = context.SyntaxProvider
            .ForTypesWithAttributeDeclarations(
                attributeFullyQualifiedNames: ["PolyType.GenerateShapeForAttribute<T>", "PolyType.GenerateShapeForAttribute", "PolyType.GenerateShapeAttribute"],
                (node, _) => node is TypeDeclarationSyntax)
            .Collect()
            .Combine(knownSymbols)
            .Select((tuple, token) => DebugGuard.Invoke(Parser.ParseFromGenerateShapeAttributes, tuple.Left, tuple.Right, token));

        // Pipeline 1: Source generation only.
        // Uses Select to extract just the model; the Select operator deduplicates by
        // comparing model equality, so source generation only re-fires on structural changes.
        IncrementalValueProvider<TypeShapeProviderModel?> providerModel = parsed.Select((t, _) => t?.Model);
        context.RegisterSourceOutput(providerModel, (ctxt, model) => DebugGuard.Invoke(GenerateSource, ctxt, model));

        // Pipeline 2: Diagnostics only.
        // Diagnostics use raw SourceLocation instances that are pragma-suppressible.
        // This pipeline re-fires whenever diagnostics change (e.g. positional shifts)
        // without triggering expensive source regeneration.
        // See https://github.com/dotnet/runtime/issues/92509 for context.
        context.RegisterSourceOutput(
            parsed,
            static (context, tuple) =>
            {
                if (tuple is null)
                {
                    return;
                }

                foreach (Diagnostic diagnostic in tuple.Value.Diagnostics)
                {
                    context.ReportDiagnostic(diagnostic);
                }
            });
    }

    private void GenerateSource(SourceProductionContext context, TypeShapeProviderModel? provider)
    {
        if (provider is null)
        {
            return;
        }

        OnGeneratingSource?.Invoke(provider);
        SourceFormatter.GenerateSourceFiles(context, provider);
    }

    public Action<TypeShapeProviderModel>? OnGeneratingSource { get; init; }
}
