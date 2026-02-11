using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatSurrogateTypeShapeFactory(SourceWriter writer, string methodName, SurrogateShapeModel surrogateShapeModel)
    {
        string? methodFactoryMethodName = CreateMethodsFactoryName(surrogateShapeModel);
        string? eventFactoryMethodName = CreateEventsFactoryName(surrogateShapeModel);
        string? associatedTypesFactoryMethodName = GetAssociatedTypesFactoryName(surrogateShapeModel);
        string? attributeFactoryName = GetAttributesFactoryName(surrogateShapeModel);

        writer.WriteLine($$"""
            private global::PolyType.ITypeShape<{{surrogateShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenSurrogateTypeShape<{{surrogateShapeModel.Type.FullyQualifiedName}}, {{surrogateShapeModel.SurrogateType.FullyQualifiedName}}>
                {
                    Marshaler = new {{surrogateShapeModel.MarshalerType.FullyQualifiedName}}()!,
                    SurrogateTypeFunc = () => {{GetShapeModel(surrogateShapeModel.SurrogateType).SourceIdentifier}},
                    MethodsFactory = {{FormatNull(methodFactoryMethodName)}},
                    EventsFactory = {{FormatNull(eventFactoryMethodName)}},
                    GetAssociatedTypeShape = {{FormatNull(associatedTypesFactoryMethodName)}},
                    AttributeFactory = {{FormatNull(attributeFactoryName)}},
                    Provider = this,
                };
            }
            """, trimDefaultAssignmentLines: true);

        if (methodFactoryMethodName is not null)
        {
            writer.WriteLine();
            FormatMethodsFactory(writer, methodFactoryMethodName, surrogateShapeModel);
        }

        if (eventFactoryMethodName is not null)
        {
            writer.WriteLine();
            FormatEventsFactory(writer, eventFactoryMethodName, surrogateShapeModel);
        }

        if (associatedTypesFactoryMethodName is not null)
        {
            writer.WriteLine();
            FormatAssociatedTypesFactory(writer, surrogateShapeModel, associatedTypesFactoryMethodName);
        }

        if (attributeFactoryName is not null)
        {
            writer.WriteLine();
            FormatAttributesFactory(writer, attributeFactoryName, surrogateShapeModel.Attributes);
        }
    }
}