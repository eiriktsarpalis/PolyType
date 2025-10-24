﻿using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatEnumTypeShapeFactory(SourceWriter writer, string methodName, EnumShapeModel enumTypeShape)
    {
        string memberDictionaryFactoryName = $"__CreateMemberDictionary_{enumTypeShape.SourceIdentifier}";
        string? associatedTypesFactoryMethodName = GetAssociatedTypesFactoryName(enumTypeShape);

        writer.WriteLine($$"""
            private global::PolyType.ITypeShape<{{enumTypeShape.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenEnumTypeShape<{{enumTypeShape.Type.FullyQualifiedName}}, {{enumTypeShape.UnderlyingType.FullyQualifiedName}}>
                {
                    UnderlyingType = {{GetShapeModel(enumTypeShape.UnderlyingType).SourceIdentifier}},
                    GetAssociatedTypeShapeFunc = {{FormatNull(associatedTypesFactoryMethodName)}},
                    Members = {{memberDictionaryFactoryName}}(),
                    Provider = this,
                };
            }
            """, trimDefaultAssignmentLines: true);

        writer.WriteLine();
        FormatEnumDictionaryFactory(writer, memberDictionaryFactoryName, enumTypeShape);

        if (associatedTypesFactoryMethodName is not null)
        {
            writer.WriteLine();
            FormatAssociatedTypesFactory(writer, enumTypeShape, associatedTypesFactoryMethodName);
        }
    }

    private static void FormatEnumDictionaryFactory(SourceWriter writer, string enumDictionaryFactoryName, EnumShapeModel enumTypeShape)
    {
        writer.WriteLine($$"""
            private static global::System.Collections.Generic.IReadOnlyDictionary<string, {{enumTypeShape.UnderlyingType.FullyQualifiedName}}> {{enumDictionaryFactoryName}}()
            {
                return new global::System.Collections.Generic.Dictionary<string, {{enumTypeShape.UnderlyingType.FullyQualifiedName}}>
                {
            """);

        writer.Indentation += 2;
        foreach (var member in enumTypeShape.Members)
        {
            writer.WriteLine($"""["{member.Key}"] = {member.Value},""");
        }

        writer.Indentation -= 2;
        writer.WriteLine("""
                };
            }
            """);
    }
}
