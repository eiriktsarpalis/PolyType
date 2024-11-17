﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter(TypeShapeProviderModel provider)
{
    public static string[] ReservedIdentifiers { get; } = [ProviderSingletonProperty,GetShapeMethodName];

    private const string InstanceBindingFlagsConstMember = "__BindingFlags_Instance_All";
    private const string ProviderSingletonProperty = "Default";
    private const string GetShapeMethodName = "GetShape";

    public static void GenerateSourceFiles(SourceProductionContext context, TypeShapeProviderModel provider)
    {
        SourceFormatter formatter = new(provider);
        formatter.AddAllSourceFiles(context, provider);
    }

    private void AddAllSourceFiles(SourceProductionContext context, TypeShapeProviderModel provider)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.AddSource($"{provider.ProviderDeclaration.SourceFilenamePrefix}.g.cs", FormatMainFile(provider));
        context.AddSource($"{provider.ProviderDeclaration.SourceFilenamePrefix}.ITypeShapeProvider.g.cs", FormatProviderInterfaceImplementation(provider));

        foreach (TypeShapeModel type in provider.ProvidedTypes.Values)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            context.AddSource($"{provider.ProviderDeclaration.SourceFilenamePrefix}.{type.SourceIdentifier}.g.cs", FormatType(provider, type));
        }

        foreach (TypeDeclarationModel typeDeclaration in provider.AnnotatedTypes)
        {
            if (typeDeclaration.ImplementsITypeShapeProvider)
            {
                context.AddSource($"{typeDeclaration.SourceFilenamePrefix}.ITypeShapeProvider.g.cs", FormatITypeShapeProviderStub(typeDeclaration, provider));
            }

            foreach (TypeId typeToImplement in typeDeclaration.ShapeableOfTImplementations)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                string sourceFile = typeToImplement == typeDeclaration.Id
                    ? $"{typeDeclaration.SourceFilenamePrefix}.IShapeable.g.cs"
                    : $"{typeDeclaration.SourceFilenamePrefix}.IShapeable.{GetShapeModel(typeToImplement).SourceIdentifier}.g.cs";
                context.AddSource(sourceFile, FormatIShapeableOfTStub(typeDeclaration, typeToImplement, provider));
            }
        }
    }

    private static SourceText FormatMainFile(TypeShapeProviderModel provider)
    {
        var writer = new SourceWriter();
        StartFormatSourceFile(writer, provider.ProviderDeclaration);

        writer.WriteLine("""/// <summary>The source generated <see cref="global::PolyType.ITypeShapeProvider"/> implementation for the current assembly.</summary>""");
        writer.WriteLine($"""[global::System.CodeDom.Compiler.GeneratedCodeAttribute({FormatStringLiteral(PolyTypeGenerator.SourceGeneratorName)}, {FormatStringLiteral(PolyTypeGenerator.SourceGeneratorVersion)})]""");
        writer.WriteLine(provider.ProviderDeclaration.TypeDeclarationHeader);
        writer.WriteLine('{');
        writer.Indentation++;

        writer.WriteLine($$"""
            private const global::System.Reflection.BindingFlags {{InstanceBindingFlagsConstMember}} = 
                global::System.Reflection.BindingFlags.Public | 
                global::System.Reflection.BindingFlags.NonPublic | 
                global::System.Reflection.BindingFlags.Instance;

            /// <summary>Gets the default instance of the <see cref="{{provider.ProviderDeclaration.Name}}"/> class.</summary>
            public static {{provider.ProviderDeclaration.Name}} {{ProviderSingletonProperty}} { get; } = new();

            /// <summary>Initializes a new instance of the <see cref="{{provider.ProviderDeclaration.Name}}"/> class.</summary>
            private {{provider.ProviderDeclaration.Name}}() { }
            """);

        writer.Indentation--;
        writer.WriteLine('}');
        EndFormatSourceFile(writer);

        return writer.ToSourceText();
    }

    private static void StartFormatSourceFile(SourceWriter writer, TypeDeclarationModel typeDeclaration)
    {
        writer.WriteLine("// <auto-generated/>");
        writer.WriteLine();

#if DEBUG
        writer.WriteLine("""
            #nullable enable

            """);
#else
        writer.WriteLine("""
            #nullable enable annotations
            #nullable disable warnings

            """);
#endif
        
        if (typeDeclaration.Namespace is string @namespace)
        {
            writer.WriteLine($"namespace {@namespace}");
            writer.WriteLine('{');
            writer.Indentation++;
        }

        foreach (string containingType in typeDeclaration.ContainingTypes)
        {
            writer.WriteLine(containingType);
            writer.WriteLine('{');
            writer.Indentation++;
        }
    }

    private static void EndFormatSourceFile(SourceWriter writer)
    {
        while (writer.Indentation > 0) 
        {
            writer.Indentation--;
            writer.WriteLine('}');
        }
    }

    private TypeShapeModel GetShapeModel(TypeId typeId) => provider.ProvidedTypes[typeId];

    private static string FormatBool(bool value) => value ? "true" : "false";
    private static string FormatNull(string? stringExpr) => stringExpr is null ? "null" : stringExpr;
    private static string FormatStringLiteral(string value) => SymbolDisplay.FormatLiteral(value, quote: true);
}
