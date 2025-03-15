using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Helpers;
using PolyType.SourceGenerator.Model;
using System.Collections.Immutable;
using System.Reflection;

namespace PolyType.SourceGenerator;

[Generator]
public sealed class PolyTypeGenerator : IIncrementalGenerator
{
    public static string SourceGeneratorName { get; } = typeof(PolyTypeGenerator).FullName;
    public static string SourceGeneratorVersion { get; } = typeof(SourceFormatter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0.0";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if LAUNCH_DEBUGGER
        System.Diagnostics.Debugger.Launch();
#endif
        IncrementalValueProvider<PolyTypeKnownSymbols> knownSymbols = context.CompilationProvider
            .Select((compilation, _) => new PolyTypeKnownSymbols(compilation));

        IncrementalValuesProvider<TypeExtensionModel> externalTypeShapeExtensions = context.MetadataReferencesProvider
            .Combine(context.CompilationProvider)
            .Combine(knownSymbols)
            .SelectMany((tuple, token) => Parser.DiscoverTypeShapeExtensions(tuple.Right, tuple.Left.Right, tuple.Left.Left, token));

        IncrementalValuesProvider<TypeExtensionModel> ownTypeShapeExtensions = context.CompilationProvider
            .Combine(knownSymbols)
            .SelectMany((tuple, token) => Parser.DiscoverTypeShapeExtensions(tuple.Right, tuple.Left.Assembly, token));

        // In combining extensions from multiple sources, we *could* apply a merge policy that the local compilation wins when there's a conflict.
        // Type extensions have some aspects that can be merged (e.g. associated types) and some that cannot be (e.g. Kind and Marshaller).
        IncrementalValueProvider<IReadOnlyDictionary<INamedTypeSymbol, TypeExtensionModel>> allTypeShapeExtensions = externalTypeShapeExtensions
            .Collect()
            .Combine(ownTypeShapeExtensions.Collect())
            .Select((tuple, token) => (IReadOnlyDictionary<INamedTypeSymbol, TypeExtensionModel>)tuple.Left.Concat(tuple.Right).ToDictionary<TypeExtensionModel, INamedTypeSymbol, TypeExtensionModel>(e => e.Target, e => e, SymbolEqualityComparer.Default));

        IncrementalValueProvider<TypeShapeProviderModel?> providerModel = context.SyntaxProvider
            .ForTypesWithAttributeDeclarations(
                attributeFullyQualifiedNames: ["PolyType.GenerateShapeAttribute<T>", "PolyType.GenerateShapeAttribute"],
                (node, _) => node is TypeDeclarationSyntax)
            .Collect()
            .Combine(knownSymbols)
            .Combine(allTypeShapeExtensions)
            .Select((tuple, token) => Parser.ParseFromGenerateShapeAttributes(tuple.Left.Left, tuple.Left.Right, tuple.Right, token));

        context.RegisterSourceOutput(providerModel, GenerateSource);
    }

    private void GenerateSource(SourceProductionContext context, TypeShapeProviderModel? provider)
    {
        if (provider is null)
        {
            return;
        }

        OnGeneratingSource?.Invoke(provider);

        foreach (EquatableDiagnostic diagnostic in provider.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.CreateDiagnostic());
        }

        SourceFormatter.GenerateSourceFiles(context, provider);
    }

    public Action<TypeShapeProviderModel>? OnGeneratingSource { get; init; }
}
