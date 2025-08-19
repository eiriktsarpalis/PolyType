using System.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private static SourceText FormatShapeProviderMainFile(TypeShapeProviderModel provider)
    {
        var writer = new SourceWriter();
        StartFormatSourceFile(writer, provider.ProviderDeclaration);

        writer.WriteLine("""/// <summary>The source generated <see cref="global::PolyType.SourceGenModel.SourceGenTypeShapeProvider"/> implementation for the current assembly.</summary>""");
        writer.WriteLine($"""[global::System.CodeDom.Compiler.GeneratedCodeAttribute({FormatStringLiteral(PolyTypeGenerator.SourceGeneratorName)}, {FormatStringLiteral(PolyTypeGenerator.SourceGeneratorVersion)})]""");
        writer.WriteLine("[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]");
        writer.WriteLine($"{provider.ProviderDeclaration.TypeDeclarationHeader} : global::PolyType.SourceGenModel.SourceGenTypeShapeProvider");
        writer.WriteLine('{');
        writer.Indentation++;

        writer.WriteLine($$"""
            private const global::System.Reflection.BindingFlags {{InstanceBindingFlagsConstMember}} = 
                global::System.Reflection.BindingFlags.Public | 
                global::System.Reflection.BindingFlags.NonPublic | 
                global::System.Reflection.BindingFlags.Instance;

            private const global::System.Reflection.BindingFlags {{AllBindingFlagsConstMember}} = 
                {{InstanceBindingFlagsConstMember}} | global::System.Reflection.BindingFlags.Static;

            /// <summary>Gets the default instance of the <see cref="{{provider.ProviderDeclaration.Name}}"/> class.</summary>
            public static {{provider.ProviderDeclaration.Name}} {{ProviderSingletonProperty}} { get; } = new();

            /// <summary>Initializes a new instance of the <see cref="{{provider.ProviderDeclaration.Name}}"/> class.</summary>
            private {{provider.ProviderDeclaration.Name}}() { }

            /// <summary>Initializes the field ensuring the same instance is always returned.</summary>
            private T {{InitializeMethodName}}<T>(ref T? field, T value) where T : class =>
                global::System.Threading.Interlocked.CompareExchange(ref field, value, null) ?? value;

            """);

        FormatGetShapeProviderMethod(provider, writer);

        writer.Indentation--;
        writer.WriteLine('}');
        EndFormatSourceFile(writer);

        return writer.ToSourceText();
    }

    private static void FormatGetShapeProviderMethod(TypeShapeProviderModel provider, SourceWriter writer)
    {
        writer.WriteLine("""
            /// <inheritdoc/>
            public override global::PolyType.Abstractions.ITypeShape? GetShape(global::System.Type type)
            {
            """);

        writer.Indentation++;

        foreach (TypeShapeModel generatedType in provider.ProvidedTypes.Values)
        {
            writer.WriteLine($$"""
                if (type == typeof({{generatedType.Type.FullyQualifiedName}}))
                {
                    return {{generatedType.SourceIdentifier}};
                }

                """);
        }

        writer.WriteLine("return null;");
        writer.Indentation--;
        writer.WriteLine('}');
    }

    private SourceText FormatGeneratedTypeMainFile(TypeDeclarationModel typeDeclaration)
    {
        Debug.Assert(typeDeclaration.IsWitnessTypeDeclaration || typeDeclaration.ShapeableOfTImplementations.Count > 0,
            "Type declaration must be a witness type or implement at least one IShapeable<T> interface.");
        
        var writer = new SourceWriter();
        StartFormatSourceFile(writer, typeDeclaration);

        switch (typeDeclaration.ShapeableOfTImplementations.Count)
        {
            case 0:
                writer.WriteLine(typeDeclaration.TypeDeclarationHeader);
                break;

            case 1:
                writer.WriteLine("#nullable disable annotations // Use nullable-oblivious interface implementation", disableIndentation: true);
                writer.WriteLine($"{typeDeclaration.TypeDeclarationHeader} : global::PolyType.IShapeable<{typeDeclaration.ShapeableOfTImplementations.First().FullyQualifiedName}>");
                writer.WriteLine("#nullable enable annotations // Use nullable-oblivious interface implementation", disableIndentation: true);
                break;
                
            case var count:
                writer.WriteLine($"{typeDeclaration.TypeDeclarationHeader} :");
                writer.WriteLine("#nullable disable annotations // Use nullable-oblivious interface implementation", disableIndentation: true);
                writer.Indentation++;
                foreach (TypeId typeToImplement in typeDeclaration.ShapeableOfTImplementations)
                {
                    string separator = --count == 0 ? "" : ",";
                    writer.WriteLine($"global::PolyType.IShapeable<{typeToImplement.FullyQualifiedName}>{separator}");
                }
                writer.Indentation--;
                writer.WriteLine("#nullable enable annotations // Use nullable-oblivious interface implementation", disableIndentation: true);
                break;            
        }
        
        writer.WriteLine("{");
        writer.Indentation++;

        int emittedMembers = 0;
        if (typeDeclaration.IsWitnessTypeDeclaration)
        {
            writer.WriteLine($$"""
                /// <summary>Gets the source generated <see cref="global::PolyType.SourceGenModel.SourceGenTypeShapeProvider"/> corresponding to the current witness type.</summary>
                public static global::PolyType.SourceGenModel.SourceGenTypeShapeProvider ShapeProvider =>
                    {{provider.ProviderDeclaration.Id.FullyQualifiedName}}.{{ProviderSingletonProperty}};
                """);

            emittedMembers++;
        }
        
        foreach (TypeId typeToImplement in typeDeclaration.ShapeableOfTImplementations)
        {
            if (emittedMembers++ > 0)
            {
                writer.WriteLine();
            }

            writer.WriteLine($"""
                static global::PolyType.Abstractions.ITypeShape<{typeToImplement.FullyQualifiedName}> global::PolyType.IShapeable<{typeToImplement.FullyQualifiedName}>.GetShape() =>
                    {provider.ProviderDeclaration.Id.FullyQualifiedName}.{ProviderSingletonProperty}.{GetShapeModel(typeToImplement).SourceIdentifier};
                """);
        }

        EndFormatSourceFile(writer);
        return writer.ToSourceText();
    }
}
