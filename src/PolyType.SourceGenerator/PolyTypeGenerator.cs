using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.Roslyn;
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

        IncrementalValueProvider<TypeShapeProviderModel?> providerModel = context.SyntaxProvider
            .ForTypesWithAttributeDeclarations(
                attributeFullyQualifiedNames: ["PolyType.GenerateShapeForAttribute<T>", "PolyType.GenerateShapeForAttribute", "PolyType.GenerateShapeAttribute"],
                (node, _) => node is TypeDeclarationSyntax)
            .Collect()
            .Combine(knownSymbols)
            .Select((tuple, token) => DebugGuard.Invoke(Parser.ParseFromGenerateShapeAttributes, tuple.Left, tuple.Right, token));

        context.RegisterSourceOutput(providerModel, (ctxt, model) => DebugGuard.Invoke(GenerateSource, ctxt, model));

        // Use a separate pipeline for diagnostics that combines the model with the
        // CompilationProvider. This lets us recover the SyntaxTree from the Compilation
        // at emission time, producing SourceLocation instances that are pragma-suppressible.
        // See https://github.com/dotnet/runtime/issues/92509 for context.
        context.RegisterSourceOutput(
            providerModel.Combine(context.CompilationProvider),
            static (context, tuple) =>
            {
                var (model, compilation) = tuple;
                if (model is null)
                {
                    return;
                }

                foreach (EquatableDiagnostic diagnostic in model.Diagnostics)
                {
                    context.ReportDiagnostic(diagnostic.CreateDiagnostic(compilation));
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
