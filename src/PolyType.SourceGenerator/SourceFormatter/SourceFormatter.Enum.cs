using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatEnumTypeShapeFactory(SourceWriter writer, string methodName, EnumShapeModel enumShapeType)
    {
        writer.WriteLine($$"""
            private global::PolyType.Abstractions.ITypeShape<{{enumShapeType.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenEnumTypeShape<{{enumShapeType.Type.FullyQualifiedName}}, {{enumShapeType.UnderlyingType.FullyQualifiedName}}>
                {
                    UnderlyingType = {{GetShapeModel(enumShapeType.UnderlyingType).SourceIdentifier}},
                    Provider = this,
                    Members = new global::System.Collections.Generic.Dictionary<string, {{enumShapeType.UnderlyingType.FullyQualifiedName}}>({{enumShapeType.Members.Count}}, global::System.StringComparer.Ordinal)
                    {
                        {{string.Join("\n            ", enumShapeType.Members.Select(member => $"[\"{member.Key}\"] = {member.Value},"))}}
                    }
                };
            }
            """);
    }
}
