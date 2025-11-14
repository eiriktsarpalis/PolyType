using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;
using System.Diagnostics;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatUnionTypeShapeFactory(SourceWriter writer, string methodName, UnionShapeModel unionShapeModel)
    {
        string createUnionCasesMethodName = $"__Create_UnionCases_{unionShapeModel.SourceIdentifier}";
        string getUnionCaseIndexMethod = $"__GetUnionCaseIndex_{unionShapeModel.SourceIdentifier}";
        string? methodFactoryMethodName = CreateMethodsFactoryName(unionShapeModel);
        string? eventFactoryMethodName = CreateEventsFactoryName(unionShapeModel);
        string? associatedTypesFactoryMethodName = GetAssociatedTypesFactoryName(unionShapeModel);
        string? attributeFactoryName = GetAttributesFactoryName(unionShapeModel);

        writer.WriteLine($$"""
            private global::PolyType.ITypeShape<{{unionShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenUnionTypeShape<{{unionShapeModel.Type.FullyQualifiedName}}>
                {
                    BaseType = {{unionShapeModel.UnderlyingModel.SourceIdentifier}},
                    CreateUnionCasesFunc = {{createUnionCasesMethodName}},
                    GetUnionCaseIndexFunc = {{getUnionCaseIndexMethod}},
                    CreateMethodsFunc = {{FormatNull(methodFactoryMethodName)}},
                    CreateEventsFunc = {{FormatNull(eventFactoryMethodName)}},
                    GetAssociatedTypeShapeFunc = {{FormatNull(associatedTypesFactoryMethodName)}},
                    AttributeFactory = {{FormatNull(attributeFactoryName)}},
                    Provider = this,
                };
            }
            """, trimDefaultAssignmentLines: true);

        writer.WriteLine();
        FormatUnionCasesFactory(writer, unionShapeModel, createUnionCasesMethodName);

        writer.WriteLine();
        FormatUnionCaseIndexMethod(writer, unionShapeModel, getUnionCaseIndexMethod);

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

        if (attributeFactoryName is not null)
        {
            writer.WriteLine();
            FormatAttributesFactory(writer, attributeFactoryName, unionShapeModel.Attributes);
        }
    }

    private void FormatUnionCasesFactory(SourceWriter writer, UnionShapeModel unionShapeModel, string methodName)
    {
        // The model sorts union cases topologically, re-sort by index here.
        UnionCaseModel[] unionCasesSortedByIndex = unionShapeModel.UnionCases
            .OrderBy(x => x.Index)
            .ToArray();

        // Emit the union case factory method.
        writer.WriteLine($$"""private global::PolyType.Abstractions.IUnionCaseShape[] {{methodName}}() => new global::PolyType.Abstractions.IUnionCaseShape[]""");
        writer.WriteLine("{");
        writer.Indentation++;

        for (int i = 0; i < unionCasesSortedByIndex.Length; i++)
        {
            if (i > 0)
            {
                writer.WriteLine();
            }

            UnionCaseModel unionCase = unionCasesSortedByIndex[i];
            Debug.Assert(unionCase.Index == i);
            TypeShapeModel unionCaseType = unionCase.IsBaseType ? unionShapeModel.UnderlyingModel : GetShapeModel(unionCase.Type);
            writer.WriteLine($$"""
                new global::PolyType.SourceGenModel.SourceGenUnionCaseShape<{{unionCase.Type.FullyQualifiedName}}, {{unionShapeModel.Type.FullyQualifiedName}}>
                {
                    UnionCaseType = {{unionCaseType.SourceIdentifier}},
                    Marshaler = global::PolyType.SourceGenModel.SubtypeMarshaler<{{unionCase.Type.FullyQualifiedName}}, {{unionShapeModel.Type.FullyQualifiedName}}>.Instance,
                    Name = {{FormatStringLiteral(unionCase.Name)}},
                    Tag = {{unionCase.Tag}},
                    IsTagSpecified = {{FormatBool(unionCase.IsTagSpecified)}},
                    Index = {{unionCase.Index}},
                },
                """);
        }

        writer.Indentation--;
        writer.WriteLine("};");
    }

    private static void FormatUnionCaseIndexMethod(SourceWriter writer, UnionShapeModel unionShapeModel, string methodName)
    {
        // Emit the union case index method.
        writer.WriteLine($$"""
            private int {{methodName}}(ref {{unionShapeModel.Type.FullyQualifiedName}} value)
            {
                return value switch
                {
            """);

        writer.Indentation += 2;
        int defaultIndex = -1;
        foreach (UnionCaseModel unionCase in unionShapeModel.UnionCases)
        {
            if (unionCase.IsBaseType)
            {
                defaultIndex = unionCase.Index;
                continue;
            }

            writer.WriteLine($$"""{{unionCase.Type.FullyQualifiedName}} => {{unionCase.Index}},""");
        }

        writer.WriteLine($"""_ => {defaultIndex},""");
        writer.Indentation -= 2;
        writer.WriteLine($$"""
                };
            }
            """);
    }
}
