using System.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private static SourceText FormatTypeShapeProviderMainFile(TypeShapeProviderModel provider)
    {
        using SourceWriter writer = new();
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
        Debug.Assert(
            provider.ProvidedTypes.Values.Select(t => t.ReflectionName).Distinct().Count() == provider.ProvidedTypes.Count, 
            "The string-based type identifier should be unique to each generated type.");

        writer.WriteLine("""
            /// <inheritdoc/>
            public override global::PolyType.ITypeShape? GetTypeShape(global::System.Type type)
            {
                // This method looks up type shapes from the entire transitive type graph
                // being generated, as such in certain cases it can grow very large.
                // In order to avoid performance issues associated loading all application
                // types at once, perform a string-based lookup first before calling into
                // a separate method returning the matching shape. The helper method guards
                // the returned shape with a check against the literal type expression to aid
                // trimmability of shapes of unused types.
                switch (type?.ToString())
                {
            """);

        writer.Indentation += 2;
        foreach (TypeShapeModel typeModel in provider.ProvidedTypes.Values)
        {
            writer.WriteLine($$"""
                case {{FormatStringLiteral(typeModel.ReflectionName)}}:
                {
                    return GetMatchingTypeShape(type);

                    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
                    ITypeShape? GetMatchingTypeShape(System.Type type) =>
                        type == typeof({{typeModel.Type.FullyQualifiedName}}) ? {{typeModel.SourceIdentifier}} : null;
                }
                """);
        }

        writer.Indentation -= 2;
        writer.WriteLine("""
                    default:
                        return null;
                }
            }
            """);
    }

    private SourceText FormatGeneratedTypeMainFile(TypeDeclarationModel typeDeclaration)
    {
        const string LocalTypeShapeProviderName = "__LocalTypeShapeProvider__";

        using SourceWriter writer = new();
        StartFormatSourceFile(writer, typeDeclaration);

        if (!provider.TargetSupportsIShapeableOfT)
        {
            writer.WriteLine($"[global::PolyType.Abstractions.TypeShapeProvider(typeof({LocalTypeShapeProviderName}))]");
        }

        switch (typeDeclaration.ShapeableImplementations.Count)
        {
            case var _ when !provider.TargetSupportsIShapeableOfT:
            case 0:
                writer.WriteLine(typeDeclaration.TypeDeclarationHeader);
                break;

            case 1:
                writer.WriteLine("#nullable disable annotations // Use nullable-oblivious interface implementation", disableIndentation: true);
                writer.WriteLine($"{typeDeclaration.TypeDeclarationHeader} : global::PolyType.IShapeable<{typeDeclaration.ShapeableImplementations.First().FullyQualifiedName}>");
                writer.WriteLine("#nullable enable annotations // Use nullable-oblivious interface implementation", disableIndentation: true);
                break;
                
            case var count:
                writer.WriteLine($"{typeDeclaration.TypeDeclarationHeader} :");
                writer.WriteLine("#nullable disable annotations // Use nullable-oblivious interface implementation", disableIndentation: true);
                writer.Indentation++;
                foreach (TypeId typeToImplement in typeDeclaration.ShapeableImplementations)
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
                /// <summary>Gets the source generated <see cref="global::PolyType.SourceGenModel.SourceGenTypeShapeProvider"/> corresponding to the current assembly.</summary>
                public static global::PolyType.SourceGenModel.SourceGenTypeShapeProvider GeneratedTypeShapeProvider =>
                    {{provider.ProviderDeclaration.Id.FullyQualifiedName}}.{{ProviderSingletonProperty}};
                """);

            emittedMembers++;
        }
        
        if (provider.TargetSupportsIShapeableOfT)
        {
            foreach (TypeId typeToImplement in typeDeclaration.ShapeableImplementations)
            {
                if (emittedMembers++ > 0)
                {
                    writer.WriteLine();
                }

                writer.WriteLine($"""
                    static global::PolyType.ITypeShape<{typeToImplement.FullyQualifiedName}> global::PolyType.IShapeable<{typeToImplement.FullyQualifiedName}>.GetTypeShape() =>
                        {provider.ProviderDeclaration.Id.FullyQualifiedName}.{ProviderSingletonProperty}.{GetShapeModel(typeToImplement).SourceIdentifier};
                    """);
            }
        }
        else
        {
            if (emittedMembers++ > 0)
            {
                writer.WriteLine();
            }

            writer.WriteLine($$"""
                [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                private sealed class {{LocalTypeShapeProviderName}} : global::PolyType.ITypeShapeProvider
                {
                    global::PolyType.ITypeShape? global::PolyType.ITypeShapeProvider.GetTypeShape(global::System.Type type)
                    {
                """);

            writer.Indentation += 2;

            foreach (TypeId typeToImplement in typeDeclaration.ShapeableImplementations)
            {
                writer.WriteLine($$"""
                    if (type == typeof({{typeToImplement.FullyQualifiedName}}))
                    {
                        return {{provider.ProviderDeclaration.Id.FullyQualifiedName}}.{{ProviderSingletonProperty}}.{{GetShapeModel(typeToImplement).SourceIdentifier}};
                    }
                    """);

                writer.WriteLine();
            }

            writer.WriteLine("return null;");
            writer.Indentation -= 2;
            writer.WriteLine("""
                    }
                }
                """);
        }

        EndFormatSourceFile(writer);
        return writer.ToSourceText();
    }
}
