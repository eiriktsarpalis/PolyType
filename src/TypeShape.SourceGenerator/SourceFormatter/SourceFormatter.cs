﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    public static void FormatProvider(SourceProductionContext context, TypeShapeProviderModel provider)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.AddSource($"{provider.Name}.g.cs", FormatMainFile(provider));
        context.AddSource($"{provider.Name}.ITypeShapeProvider.g.cs", FormatProviderInterfaceImplementation(provider));

        foreach (TypeModel type in provider.ProvidedTypes)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            context.AddSource($"{provider.Name}.{type.Id.GeneratedPropertyName}.g.cs", FormatType(provider, type));
        }
    }

    private static SourceText FormatMainFile(TypeShapeProviderModel provider)
    {
        var writer = new SourceWriter();
        StartFormatSourceFile(writer, provider);

        writer.WriteLine(provider.TypeDeclaration);
        writer.WriteStartBlock();

        writer.WriteLine($$"""
            public {{provider.Name}}() { }

            public static {{provider.Name}} Default => _default ??= new();
            private static {{provider.Name}}? _default;
            """);

        writer.WriteEndBlock();
        EndFormatSourceFile(writer);

        return writer.ToSourceText();
    }

    private static void StartFormatSourceFile(SourceWriter writer, TypeShapeProviderModel provider)
    {
        writer.WriteLine("""
            // <auto-generated/>
            
            #nullable enable annotations
            
            // Suppress warnings about [Obsolete] member usage in generated code.
            #pragma warning disable CS0612, CS0618

            """);

        if (provider.Namespace != null)
        {
            writer.WriteLine($"namespace {provider.Namespace}");
            writer.WriteStartBlock();
        }

        foreach (string typeDeclaration in provider.ContainingTypes)
        {
            writer.WriteLine(typeDeclaration);
            writer.WriteStartBlock();
        }
    }

    private static void EndFormatSourceFile(SourceWriter writer)
    {
        while (writer.IndentationLevel > 0) 
        {
            writer.WriteEndBlock();
        }
    }

    private static string FormatBool(bool value) => value ? "true" : "false";
    private static string FormatNull(string? stringExpr) => stringExpr is null ? "null" : stringExpr;
}
