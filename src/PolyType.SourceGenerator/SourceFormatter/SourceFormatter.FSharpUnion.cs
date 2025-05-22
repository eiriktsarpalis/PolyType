using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private static void FormatFSharpUnionTypeShapeFactory(SourceWriter writer, string methodName, FSharpUnionShapeModel unionShapeModel)
    {
        string createUnionCasesMethodName = $"__Create_UnionCases_{unionShapeModel.SourceIdentifier}";

        writer.WriteLine($$"""
            private global::PolyType.Abstractions.ITypeShape<{{unionShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenUnionTypeShape<{{unionShapeModel.Type.FullyQualifiedName}}>(this)
                {
                    BaseTypeSetter = {{unionShapeModel.UnderlyingModel.SourceIdentifier}},
                    CreateUnionCasesFunc = {{createUnionCasesMethodName}},
                    GetUnionCaseIndexFunc = {{FormatFSharpUnionTagReader(unionShapeModel)}},
                };
            }
            """);

        writer.WriteLine();
        FormatFSharpUnionCasesFactory(writer, unionShapeModel, createUnionCasesMethodName);
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
                    TypeSetter = {{unionCase.TypeModel.SourceIdentifier}},
                    NameSetter = "{{unionCase.Name}}",
                    TagSetter = {{unionCase.Tag}},
                    IsTagSpecifiedSetter = false,
                    IndexSetter = {{unionCase.Tag}},
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
