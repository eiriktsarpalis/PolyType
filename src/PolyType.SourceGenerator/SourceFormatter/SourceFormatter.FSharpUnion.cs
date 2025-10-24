using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatFSharpUnionTypeShapeFactory(SourceWriter writer, string methodName, FSharpUnionShapeModel unionShapeModel)
    {
        string createUnionCasesMethodName = $"__Create_UnionCases_{unionShapeModel.SourceIdentifier}";
        string? methodFactoryMethodName = CreateMethodsFactoryName(unionShapeModel);
        string? eventFactoryMethodName = CreateEventsFactoryName(unionShapeModel);
        string? associatedTypesFactoryMethodName = GetAssociatedTypesFactoryName(unionShapeModel);

        writer.WriteLine($$"""
            private global::PolyType.ITypeShape<{{unionShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenUnionTypeShape<{{unionShapeModel.Type.FullyQualifiedName}}>
                {
                    BaseType = {{unionShapeModel.UnderlyingModel.SourceIdentifier}},
                    CreateUnionCasesFunc = {{createUnionCasesMethodName}},
                    GetUnionCaseIndexFunc = {{FormatFSharpUnionTagReader(unionShapeModel)}},
                    CreateMethodsFunc = {{FormatNull(methodFactoryMethodName)}},
                    CreateEventsFunc = {{FormatNull(eventFactoryMethodName)}},
                    GetAssociatedTypeShapeFunc = {{FormatNull(associatedTypesFactoryMethodName)}},
                    Provider = this,
                };
            }
            """, trimDefaultAssignmentLines: true);

        writer.WriteLine();
        FormatFSharpUnionCasesFactory(writer, unionShapeModel, createUnionCasesMethodName);

        if (methodFactoryMethodName is not null)
        {
            writer.WriteLine();
            FormatMethodsFactory(writer, methodFactoryMethodName, unionShapeModel);
        }

        if (eventFactoryMethodName is not null)
        {
            writer.WriteLine();
            FormatEventsFactory(writer, eventFactoryMethodName, unionShapeModel);
        }

        if (associatedTypesFactoryMethodName is not null)
        {
            writer.WriteLine();
            FormatAssociatedTypesFactory(writer, unionShapeModel, associatedTypesFactoryMethodName);
        }
    }

    private static void FormatFSharpUnionCasesFactory(SourceWriter writer, FSharpUnionShapeModel unionShapeModel, string methodName)
    {
        // Emit the union case factory method.
        writer.WriteLine($$"""private global::PolyType.Abstractions.IUnionCaseShape[] {{methodName}}() => new global::PolyType.Abstractions.IUnionCaseShape[]""");
        writer.WriteLine("{");
        writer.Indentation++;

        foreach (FSharpUnionCaseShapeModel unionCase in unionShapeModel.UnionCases)
        {
            if (unionCase.Tag > 0)
            {
                writer.WriteLine();
            }

            writer.WriteLine($$"""
                new global::PolyType.SourceGenModel.SourceGenUnionCaseShape<{{unionCase.TypeModel.Type.FullyQualifiedName}}, {{unionShapeModel.Type.FullyQualifiedName}}>
                {
                    UnionCaseType = {{unionCase.TypeModel.SourceIdentifier}},
                    Marshaler = global::PolyType.SourceGenModel.SubtypeMarshaler<{{unionCase.TypeModel.Type.FullyQualifiedName}}, {{unionShapeModel.Type.FullyQualifiedName}}>.Instance,
                    Name = {{FormatStringLiteral(unionCase.Name)}},
                    Tag = {{unionCase.Tag}},
                    IsTagSpecified = false,
                    Index = {{unionCase.Tag}},
                },
                """);
        }

        writer.Indentation--;
        writer.WriteLine("};");
    }

    private static string FormatFSharpUnionTagReader(FSharpUnionShapeModel unionShapeModel)
    {
        return unionShapeModel.TagReaderIsMethod
            ? $"(ref {unionShapeModel.Type.FullyQualifiedName} union) => {unionShapeModel.TagReader}(union)"
            : $"(ref {unionShapeModel.Type.FullyQualifiedName} union) => union.{unionShapeModel.TagReader}";
    }
}
