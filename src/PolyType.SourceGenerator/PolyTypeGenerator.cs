﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Helpers;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

[Generator]
public sealed class PolyTypeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if LAUNCH_DEBUGGER
        System.Diagnostics.Debugger.Launch();
#endif
        IncrementalValueProvider<PolyTypeKnownSymbols> knownSymbols = context.CompilationProvider
            .Select((compilation, _) => new PolyTypeKnownSymbols(compilation));

        IncrementalValuesProvider<TypeShapeProviderModel> generateShapeOfTModels = context.SyntaxProvider
            .ForTypesWithAttributeDeclaration(
                "PolyType.GenerateShapeAttribute<T>",
                (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax)
            .Combine(knownSymbols)
            .Select((tuple, cancellationToken) => 
                Parser.ParseFromGenerateShapeOfTAttributes(
                    context: tuple.Left,
                    knownSymbols: tuple.Right, 
                    cancellationToken));

        IncrementalValueProvider<TypeShapeProviderModel?> generateDataModels = context.SyntaxProvider
            .ForTypesWithAttributeDeclaration(
                "PolyType.GenerateShapeAttribute",
                (node, _) => node is TypeDeclarationSyntax)
            .Collect()
            .Combine(knownSymbols)
            .Select((tuple, token) => Parser.ParseFromGenerateShapeAttributes(tuple.Left, tuple.Right, token));

        context.RegisterSourceOutput(generateShapeOfTModels, GenerateSource);
        context.RegisterSourceOutput(generateDataModels, GenerateSource);
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

        SourceFormatter.FormatProvider(context, provider);
    }

    public Action<TypeShapeProviderModel>? OnGeneratingSource { get; init; }
}
